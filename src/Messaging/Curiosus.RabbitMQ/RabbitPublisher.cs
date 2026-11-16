using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Publishes with publisher confirms over a pool of <see cref="RabbitMQOptions.PublisherChannelCount"/> channels.
/// </summary>
internal sealed class RabbitPublisher : IRabbitPublisher, IAsyncDisposable, IDisposable
{
    /// <summary>
    /// How often a publish rechecks the connection while it waits for the recovery.
    /// </summary>
    private static readonly TimeSpan RecoveryPollInterval = TimeSpan.FromMilliseconds(200);

    /// <summary>
    /// Pause after a failed attempt to open the connection.
    /// </summary>
    private static readonly TimeSpan OpenRetryDelay = TimeSpan.FromSeconds(1);

    private static readonly TimeSpan DisposeTimeout = TimeSpan.FromSeconds(5);

    // without a rate limiter: concurrent publishes are bounded by the caller, not throttled or failed by the client
    private static readonly CreateChannelOptions ChannelOptions = new(
        publisherConfirmationsEnabled: true,
        publisherConfirmationTrackingEnabled: true,
        outstandingPublisherConfirmationsRateLimiter: null);

    private readonly IRabbitConnectionProvider _connectionProvider;
    private readonly IRabbitMQMessageSerializer _serializer;
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _publishTimeout;
    private readonly ChannelSlot[] _slots;

    private readonly object _sync = new();
    private readonly CancellationTokenSource _disposeCts = new();

    private IConnection? _subscribedConnection;
    private TaskCompletionSource _recovered = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int _nextSlot = -1;
    private int _disposed;

    public RabbitPublisher(
        IRabbitConnectionProvider connectionProvider,
        IRabbitMQMessageSerializer serializer,
        IOptions<RabbitMQOptions> options,
        TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);

        _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
        _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _publishTimeout = options.Value.PublishTimeout;
        _slots = Enumerable.Range(0, options.Value.PublisherChannelCount).Select(_ => new ChannelSlot()).ToArray();
    }

    public async Task PublishAsync<T>(
        string exchange,
        string routingKey,
        T message,
        RabbitPublishProperties? properties = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(exchange);
        ArgumentNullException.ThrowIfNull(routingKey);
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        cancellationToken.ThrowIfCancellationRequested();

        var body = _serializer.Serialize(message);
        var messageId = properties?.MessageId ?? Guid.CreateVersion7(_timeProvider.GetUtcNow()).ToString();
        var basicProperties = CreateBasicProperties(messageId, properties);

        using var timeoutCts = new CancellationTokenSource(_publishTimeout, _timeProvider);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(
            cancellationToken,
            timeoutCts.Token,
            _disposeCts.Token);
        var token = linkedCts.Token;

        var slot = _slots[(uint)Interlocked.Increment(ref _nextSlot) % _slots.Length];
        var attempt = new OpenAttempt();
        PooledChannel pooled;
        try
        {
            pooled = await GetOpenChannelAsync(slot, attempt, token);
        }
        catch (OperationCanceledException e) when (token.IsCancellationRequested)
        {
            throw TranslateCancellation(e, attempt.LastError);
        }

        // from here on the message is never sent again: its outcome may be unknown
        try
        {
            await pooled.Channel.BasicPublishAsync(
                exchange,
                routingKey,
                properties?.Mandatory ?? false,
                basicProperties,
                body,
                token);
        }
        catch (PublishReturnException e)
        {
            throw Fail(RabbitPublishFailure.Returned, e.ReplyCode, e.ReplyText, e);
        }
        catch (PublishException e)
        {
            throw Fail(RabbitPublishFailure.Nacked, null, null, e);
        }
        catch (OperationCanceledException e) when (token.IsCancellationRequested)
        {
            throw TranslateCancellation(e, null);
        }
        catch (Exception e) when (e is OperationInterruptedException or IOException
                                  || (e is ObjectDisposedException && Volatile.Read(ref _disposed) == 0))
        {
            // ObjectDisposedException: the slot replaced and disposed a channel the broker had closed
            // the shutdown handler of the channel ran before RabbitMQ.Client failed the publishes in flight
            var closeReason = pooled.BrokerCloseReason;
            throw closeReason != null
                ? Fail(RabbitPublishFailure.ChannelClosed, closeReason.ReplyCode, closeReason.ReplyText, e)
                : Fail(RabbitPublishFailure.ConnectionLost, null, null, e);
        }

        Exception TranslateCancellation(OperationCanceledException e, Exception? lastError)
        {
            if (cancellationToken.IsCancellationRequested) return new OperationCanceledException(e.Message, e, cancellationToken);
            if (_disposeCts.IsCancellationRequested) return new ObjectDisposedException(GetType().FullName);

            return Fail(RabbitPublishFailure.Timeout, null, null, lastError ?? e);
        }

        RabbitPublishException Fail(RabbitPublishFailure reason, ushort? replyCode, string? replyText, Exception inner)
            => new(reason, exchange, routingKey, messageId, replyCode, replyText, inner);
    }

    private BasicProperties CreateBasicProperties(string messageId, RabbitPublishProperties? properties)
    {
        // own properties and headers per publish: RabbitMQ.Client adds its sequence number header to them
        var headers = properties?.Headers == null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(properties.Headers);

        return new BasicProperties
        {
            MessageId = messageId,
            CorrelationId = properties?.CorrelationId,
            ContentType = _serializer.ContentType,
            DeliveryMode = properties?.Persistent ?? true ? DeliveryModes.Persistent : DeliveryModes.Transient,
            Timestamp = new AmqpTimestamp(_timeProvider.GetUtcNow().ToUnixTimeSeconds()),
            Headers = headers
        };
    }

    /// <summary>
    /// Returns an open channel of the slot, waiting for the connection and its recovery until the token is cancelled.
    /// </summary>
    /// <remarks>
    /// RabbitMQ.Client fails a publish at once while the connection is down, so the publish waits here instead.
    /// </remarks>
    private async Task<PooledChannel> GetOpenChannelAsync(ChannelSlot slot, OpenAttempt attempt, CancellationToken token)
    {
        while (true)
        {
            // taken before the checks: a recovery between them and the wait completes it
            var recovered = Volatile.Read(ref _recovered).Task;
            var delay = RecoveryPollInterval;
            try
            {
                var connection = await _connectionProvider.GetConnectionAsync(token);
                SubscribeToRecovery(connection);

                if (connection.IsOpen)
                {
                    var pooled = await slot.GetChannelAsync(connection, token);
                    if (pooled.Channel.IsOpen) return pooled;
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                throw;
            }
            catch (ObjectDisposedException)
            {
                throw;
            }
            catch (Exception e)
            {
                // the connection could not be opened or closed while a channel was being created
                attempt.LastError = e;
                delay = OpenRetryDelay;
            }

            await Task.WhenAny(recovered, Task.Delay(delay, _timeProvider, token));
            token.ThrowIfCancellationRequested();
        }
    }

    private void SubscribeToRecovery(IConnection connection)
    {
        if (ReferenceEquals(Volatile.Read(ref _subscribedConnection), connection)) return;

        lock (_sync)
        {
            if (ReferenceEquals(_subscribedConnection, connection)) return;

            if (_subscribedConnection != null) _subscribedConnection.RecoverySucceededAsync -= OnRecoverySucceededAsync;
            connection.RecoverySucceededAsync += OnRecoverySucceededAsync;
            Volatile.Write(ref _subscribedConnection, connection);
        }
    }

    private Task OnRecoverySucceededAsync(object sender, AsyncEventArgs e)
    {
        // channels are recovered before this event: one still closed won't come back
        foreach (var slot in _slots)
        {
            slot.MarkClosedChannelUnrecovered();
        }

        var previous = Interlocked.Exchange(
            ref _recovered,
            new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously));
        previous.TrySetResult();

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0) return;

        await _disposeCts.CancelAsync();

        lock (_sync)
        {
            if (_subscribedConnection != null) _subscribedConnection.RecoverySucceededAsync -= OnRecoverySucceededAsync;
        }

        try
        {
            await Task.WhenAll(_slots.Select(slot => slot.DisposeAsync().AsTask())).WaitAsync(DisposeTimeout);
        }
        catch
        {
            // closing channels is best effort: closing the connection closes them too
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private sealed class OpenAttempt
    {
        public Exception? LastError { get; set; }
    }

    /// <summary>
    /// One channel of the pool, created on first use and replaced when the broker closed it.
    /// </summary>
    private sealed class ChannelSlot : IAsyncDisposable
    {
        private readonly SemaphoreSlim _lock = new(1, 1);
        private PooledChannel? _current;
        private bool _disposed;

        public async ValueTask<PooledChannel> GetChannelAsync(IConnection connection, CancellationToken cancellationToken)
        {
            var current = Volatile.Read(ref _current);
            if (current is { IsBroken: false }) return current;

            await _lock.WaitAsync(cancellationToken);
            try
            {
                ObjectDisposedException.ThrowIf(_disposed, this);

                current = _current;
                if (current is { IsBroken: true })
                {
                    Volatile.Write(ref _current, null);

                    // not awaited under the lock: disposing waits for the close to complete, which can take longer
                    // than the publish timeout and would block the slot
                    _ = current.AbortAndDisposeAsync();
                    current = null;
                }

                if (current == null)
                {
                    var channel = await CreateChannelAsync(connection, cancellationToken);
                    current = new PooledChannel(channel, connection);
                    Volatile.Write(ref _current, current);
                }

                return current;
            }
            finally
            {
                _lock.Release();
            }
        }

        private static async Task<IChannel> CreateChannelAsync(IConnection connection, CancellationToken cancellationToken)
        {
            // RabbitMQ.Client waits for channel.open-ok up to its continuation timeout (20 s) whatever the token
            var creation = connection.CreateChannelAsync(ChannelOptions, cancellationToken);
            try
            {
                return await creation.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _ = AbortLateChannelAsync(creation);
                throw;
            }
        }

        private static async Task AbortLateChannelAsync(Task<IChannel> creation)
        {
            try
            {
                var channel = await creation;
                await channel.AbortAsync();
                await channel.DisposeAsync();
            }
            catch
            {
                // the channel was not opened, or it closes with its connection
            }
        }

        public void MarkClosedChannelUnrecovered()
        {
            Volatile.Read(ref _current)?.MarkUnrecoveredIfClosed();
        }

        public async ValueTask DisposeAsync()
        {
            await _lock.WaitAsync();
            try
            {
                _disposed = true;
                var current = _current;
                _current = null;
                if (current != null) await current.AbortAndDisposeAsync();
            }
            finally
            {
                _lock.Release();
            }
        }
    }

    /// <summary>
    /// A channel of the pool and how it was closed.
    /// </summary>
    private sealed class PooledChannel
    {
        private readonly IConnection _connection;
        private readonly object _sync = new();

        private ShutdownEventArgs? _brokerCloseReason;
        private volatile bool _unrecovered;
        private Task? _abortTask;

        public PooledChannel(IChannel channel, IConnection connection)
        {
            Channel = channel;
            _connection = connection;

            // RabbitMQ.Client runs a handler added to a closed channel at once, so a close between the creation of the
            // channel and this line is seen too
            channel.ChannelShutdownAsync += OnChannelShutdownAsync;
        }

        public IChannel Channel { get; }

        /// <summary>
        /// Why the broker closed the channel while the connection stayed open; <see langword="null"/> if it didn't.
        /// </summary>
        public ShutdownEventArgs? BrokerCloseReason => Volatile.Read(ref _brokerCloseReason);

        /// <summary>
        /// Whether the channel won't open again: auto-recovery brings back only channels closed with the connection.
        /// </summary>
        public bool IsBroken => BrokerCloseReason != null || _unrecovered;

        public void MarkUnrecoveredIfClosed()
        {
            if (Channel.IsClosed) _unrecovered = true;
        }

        private Task OnChannelShutdownAsync(object sender, ShutdownEventArgs e)
        {
            // the connection is closed first when it goes down: then auto-recovery re-creates the channel
            if (!_connection.IsOpen) return Task.CompletedTask;

            Volatile.Write(ref _brokerCloseReason, e);

            // aborted at once, so a connection recovery before the next publish on the slot doesn't re-create it;
            // not awaited: the channel waits for its shutdown handlers before it fails the publishes in flight.
            // Not disposed here: a publish that already took the channel would get ObjectDisposedException
            _ = AbortAsync();

            return Task.CompletedTask;
        }

        /// <summary>
        /// Aborts the channel once: that also removes it from the channels auto-recovery re-creates.
        /// </summary>
        private Task AbortAsync()
        {
            lock (_sync)
            {
                return _abortTask ??= AbortCoreAsync();
            }
        }

        private async Task AbortCoreAsync()
        {
            await Task.Yield();
            try
            {
                await Channel.AbortAsync();
            }
            catch
            {
                // abort doesn't throw for a closed channel; anything else can't be handled here either
            }
        }

        /// <summary>
        /// Aborts and disposes the channel once the slot no longer hands it out.
        /// </summary>
        public async Task AbortAndDisposeAsync()
        {
            await AbortAsync();
            await DisposeChannelAsync();
        }

        private async Task DisposeChannelAsync()
        {
            try
            {
                await Channel.DisposeAsync();
            }
            catch
            {
                // disposing a closed channel can't fail the publisher
            }
        }
    }
}
