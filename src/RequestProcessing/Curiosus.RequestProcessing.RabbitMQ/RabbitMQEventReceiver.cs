using System;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Curiosus.Configuration;
using Curiosus.RequestProcessing.RabbitMQ.Options;
using Curiosus.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Curiosus.RequestProcessing.RabbitMQ;

/// <summary>
/// Service for receiving events from RabbitMQ event source.
/// </summary>
/// <remarks>
/// Received events are confirmed (ack) or rejected (reject without requeue) in a background loop.
/// <see cref="StopAsync"/> sends the decisions made before it and closes the connection to RabbitMQ;
/// <see cref="DisposeAsync"/> stops the receiver if it was not stopped and releases its resources.
/// </remarks>
public class RabbitMQEventReceiver : BackgroundService, IEventReceiver, IAsyncDisposable
{
    /// <summary>
    /// Count of retries to restore connection to RabbitMQ manually if auto recovery fails.
    /// </summary>
    /// <remarks>
    /// Useful when we got an <see cref="AlreadyClosedException"/> or another RabbitMQ exception.
    /// </remarks>
    private const int MaxConnectionRestoreRetries = 10;

    /// <summary>
    /// Timeout of closing <see cref="_connection"/>.
    /// </summary>
    private static readonly TimeSpan ConnectionCloseTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Time <see cref="DisposeAsync"/> gives <see cref="StopAsync"/> to send the decisions made before it.
    /// </summary>
    private static readonly TimeSpan StopOnDisposeTimeout = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Default period for RabbitMQ network recovery.
    /// </summary>
    private static readonly TimeSpan NetworkRecoveryPeriod = TimeSpan.FromSeconds(10);

    private readonly RabbitMQEventReceiverOptions _eventReceiverOptions;
    private readonly ILogger _logger;

    private readonly int _qos;

    /// <summary>
    /// Guards the channel and the connection: they are replaced on manual recovery.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    /// <summary>
    /// Time for waiting connection full recovering before sending data to the queues.
    /// </summary>
    private readonly TimeSpan _recoverWaitDelay;

    private readonly Channel<EventProcessingResult> _processingResultsQueue;

    private IConnection? _connection;
    private IChannel? _channel;
    private readonly CancellationTokenSource _cts = new();

    /// <summary>
    /// Set by <see cref="StopAsync"/>: decisions are only sent, connection is not recovered anymore.
    /// </summary>
    private volatile bool _isStopping;

    private int _isStopped;

    /// <inheritdoc />
    public event EventHandler<IRequestProcessingEvent>? OnEventReceived;

    /// <inheritdoc cref="RabbitMQEventReceiver"/>
    public RabbitMQEventReceiver(
        RabbitMQEventReceiverOptions eventReceiverOptions,
        ILogger logger,
        int qos)
    {
        if (qos < 0) throw new ArgumentOutOfRangeException(nameof(qos));

        _eventReceiverOptions = eventReceiverOptions ?? throw new ArgumentNullException(nameof(eventReceiverOptions));
        eventReceiverOptions.AssertValid();

        _logger = logger;
        _qos = qos;

        // get more time for waiting because of auto recovery
        // give time to auto recovery and only after that we will try to restore all manually
        _recoverWaitDelay = TimeSpan.FromMilliseconds(NetworkRecoveryPeriod.TotalMilliseconds * 2);

        _processingResultsQueue = Channel.CreateUnbounded<EventProcessingResult>();
    }

    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Starting {nameof(RabbitMQEventReceiver)}...");

        _logger.LogTrace("Entering lock for connecting to RabbitMQ...");
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogTrace("Entered lock for connecting to RabbitMQ");
            try
            {
                await ConnectAsync(cancellationToken);
            }
            catch
            {
                await DisconnectAsync();
                throw;
            }
        }
        finally
        {
            _lock.Release();
        }
        _logger.LogTrace("Exited lock for connecting to RabbitMQ");

        _logger.LogTrace("Starting response finalizer...");
        await base.StartAsync(cancellationToken);
        _logger.LogTrace("Started response finalizer");

        _logger.LogDebug($"Started {nameof(RabbitMQEventReceiver)}");
    }

    /// <summary>
    /// Connects to RabbitMQ. Should be invoked only from a critical section.
    /// </summary>
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _eventReceiverOptions.HostName,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = NetworkRecoveryPeriod,
            TopologyRecoveryEnabled = true,
            UserName = _eventReceiverOptions.UserName,
            Password = _eventReceiverOptions.Password,
            Port = _eventReceiverOptions.Port,
            ClientProvidedName = $"{_eventReceiverOptions.ClientName}_event_receiver"
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _connection.ConnectionShutdownAsync += HandleOnDisconnectedAsync;

        await InitChannelAsync(_connection, cancellationToken);

        _logger.LogInformation(
            "Connected to RabbitMQ (host \"{RabbitHostName}\", queue = \"{QueueName}\", QoS = {QoS})",
            _eventReceiverOptions.HostName,
            _eventReceiverOptions.QueueName,
            _qos);
    }

    private Task HandleOnDisconnectedAsync(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning(
            "RabbitMQ connection was shutdown. Cause=\"{Cause}\", Initiator={Initiator}, ReplyCode={ReplyCode}, ReplyText={ReplyText}",
            e.Cause,
            e.Initiator,
            e.ReplyCode,
            e.ReplyText);

        // if we got AMQP consumer timeout exception let's try to restore connection
        // not awaited: recovery closes the connection, and the connection waits for its shutdown handlers
        if (IsRecoverableShutdown(e))
        {
            HandleRecoverySafelyAsync(null, 1, _cts.Token).WithExceptionLogger(_logger);
        }

        return Task.CompletedTask;
    }

    private async Task InitChannelAsync(IConnection connection, CancellationToken cancellationToken)
    {
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        _channel = channel;
        await channel.BasicQosAsync(0, (ushort)_qos, true, cancellationToken);
        channel.ChannelShutdownAsync += HandleChannelShutdownAsync;

        await channel.QueueDeclareAsync(
            _eventReceiverOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += (_, e) => ProcessReceivedEventAsync(channel, e);
        await channel.BasicConsumeAsync(_eventReceiverOptions.QueueName, autoAck: false, consumer, cancellationToken);
    }

    private Task HandleChannelShutdownAsync(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning(
            "RabbitMQ reader channel was shutdown. Cause=\"{Cause}\", Initiator={Initiator}, ReplyCode={ReplyCode}, "
            + "ReplyText={ReplyText}, ClassId={ClassId}, MethodId={MethodId}",
            e.Cause,
            e.Initiator,
            e.ReplyCode,
            e.ReplyText,
            e.ClassId,
            e.MethodId);

        // if we got AMQP consumer timeout exception let's try to restore connection
        if (IsRecoverableShutdown(e))
        {
            HandleRecoverySafelyAsync(null, 1, _cts.Token).WithExceptionLogger(_logger);
        }

        return Task.CompletedTask;
    }

    private static bool IsRecoverableShutdown(ShutdownEventArgs e)
    {
        return e.ReplyCode == 406 && e.ReplyText.Contains("timeout") || e.ReplyCode == 541;
    }

    /// <summary>
    /// Handle connection recovery without throwing any exception. Re-queues specified request if recovery completes successfully.
    /// </summary>
    private async Task<bool> HandleRecoverySafelyAsync(
        EventProcessingResult? eventProcessingResult,
        int currentRetriesCount = 1,
        CancellationToken cancellationToken = default)
    {
        // requeue event to process it later after connection restoring
        if (eventProcessingResult.HasValue && !_processingResultsQueue.Writer.TryWrite(eventProcessingResult.Value))
        {
            _logger.LogWarning(
                "Receiver is stopped. Decision for event with DeliveryTag={DeliveryTag} will be dropped",
                eventProcessingResult.Value.DeliveryTag);
        }

        // maybe there is no need to restore client?
        if (_channel?.IsOpen ?? false) return true;

        if (currentRetriesCount > MaxConnectionRestoreRetries)
        {
            _logger.LogError(
                "Exceeded all attempts to restore connections to RabbitMQ ({CurrentRetriesCount}/{MaxRetriesCount})",
                currentRetriesCount,
                MaxConnectionRestoreRetries);
            return false;
        }

        _logger.LogInformation(
            "Trying to restore connection to RabbitMQ ({CurrentRetriesCount}/{MaxRetriesCount})...",
            currentRetriesCount,
            MaxConnectionRestoreRetries);

        // wait for auto recovering
        try
        {
            _logger.LogDebug("Make a delay for {RecoveryWaitDelay} before recovering", _recoverWaitDelay);
            await Task.Delay(_recoverWaitDelay, cancellationToken);
        }
        catch (Exception)
        {
            // ignored
            if (cancellationToken.IsCancellationRequested) return false;
        }

        // maybe there is no need to restore client?
        if (_channel?.IsOpen ?? false) return true;

        bool isRecovered;
        var lockWasTaken = false;
        try
        {
            _logger.LogDebug("Entering lock for restoring connection...");
            try
            {
                await _lock.WaitAsync(cancellationToken);
                lockWasTaken = true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
            _logger.LogDebug("Entered lock for restoring connection");

            // maybe there is no need to restore client?
            if (_channel?.IsOpen ?? false) return true;

            // check, if any channel is still closed, than auto recovering failed, need to restore them manually
            if (_channel?.IsClosed ?? true)
            {
                _logger.LogWarning(
                    "Failed to recover channel automatically. Restoring channel manually ({CurrentRetriesCount}/{MaxRetriesCount})...",
                    currentRetriesCount,
                    MaxConnectionRestoreRetries);

                try
                {
                    _logger.LogDebug("Disconnecting from RabbitMQ...");
                    await DisconnectAsync();
                    await ConnectAsync(cancellationToken);

                    isRecovered = true;
                }
                catch (Exception e)
                {
                    _logger.LogWarning(
                        e,
                        "Failed to restore channel manually ({CurrentRetriesCount}/{MaxRetriesCount})",
                        currentRetriesCount,
                        MaxConnectionRestoreRetries);

                    // in case of error try to recover connection until we can
                    // exit lock before recursive method call
                    if (lockWasTaken)
                    {
                        _lock.Release();
                        lockWasTaken = false;
                        _logger.LogDebug("Exited lock for restoring connection");
                    }

                    isRecovered = await HandleRecoverySafelyAsync(
                        null,
                        currentRetriesCount + 1,
                        cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Channel was restored automatically ({CurrentRetriesCount}/{MaxRetriesCount})",
                    currentRetriesCount,
                    MaxConnectionRestoreRetries);
                isRecovered = true;
            }
        }
        finally
        {
            if (lockWasTaken)
            {
                _lock.Release();
                _logger.LogDebug("Exited lock for restoring connection");
            }
        }

        if (currentRetriesCount == 1)
        {
            if (isRecovered)
            {
                _logger.LogInformation(
                    "Successfully recovered connection ({CurrentRetriesCount}/{MaxRetriesCount})",
                    currentRetriesCount,
                    MaxConnectionRestoreRetries);
            }
            else
            {
                _logger.LogError(
                    "Failed to recover connection ({CurrentRetriesCount}/{MaxRetriesCount})",
                    currentRetriesCount,
                    MaxConnectionRestoreRetries);
            }
        }

        return isRecovered;
    }

    /// <summary>
    /// Closes connection to RabbitMQ. Should be invoked only from a critical section.
    /// </summary>
    private async Task DisconnectAsync()
    {
        // free used channels

        if (_channel != null)
        {
            _channel.ChannelShutdownAsync -= HandleChannelShutdownAsync;

            if (!_channel.IsClosed)
            {
                _logger.LogTrace("Waiting for channel closing...");
                try
                {
                    await _channel.CloseAsync();
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to close channel on RabbitMQ event receiving restoring");
                }
            }
            else
            {
                _logger.LogTrace("Channel have been already closed");
            }

            _logger.LogTrace("Waiting for channel disposing...");
            await _channel.DisposeAsync();
        }
        else
        {
            _logger.LogTrace("Reader channel has been already null");
        }
        _channel = null;

        // free connection

        if (_connection != null)
        {
            _connection.ConnectionShutdownAsync -= HandleOnDisconnectedAsync;

            _logger.LogTrace("Waiting for connection closing...");
            try
            {
                await _connection.CloseAsync(ConnectionCloseTimeout);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to close connection on rpc client restoring");
            }

            _logger.LogTrace("Waiting for connection disposing...");
            await _connection.DisposeAsync();
            _connection = null;
        }
        else
        {
            _logger.LogTrace("Connection has been already null");
        }
    }

    /// <summary>
    /// Processes receives event from RabbitMQ and notifies subscribers.
    /// </summary>
    private Task ProcessReceivedEventAsync(IChannel channel, BasicDeliverEventArgs e)
    {
        var receivedEvent = new RabbitMQEvent(
            e,
            deliveryTag => EnqueueDecision(new EventProcessingResult(channel, deliveryTag, EventDecisionType.Confirm)),
            deliveryTag => EnqueueDecision(new EventProcessingResult(channel, deliveryTag, EventDecisionType.Reject)));
        OnEventReceived?.Invoke(this, receivedEvent);

        return Task.CompletedTask;
    }

    private void EnqueueDecision(EventProcessingResult processingResult)
    {
        if (!_processingResultsQueue.Writer.TryWrite(processingResult))
        {
            _logger.LogWarning(
                "Receiver is stopped. Event with DeliveryTag={DeliveryTag} will be redelivered by RabbitMQ",
                processingResult.DeliveryTag);
        }
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        try
        {
            // completes when StopAsync completes the queue and the decisions made before it are sent
            await foreach (var processingResult in _processingResultsQueue.Reader.ReadAllAsync(stoppingToken))
            {
                await SendDecisionAsync(processingResult, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                "Stopping finalizer. {Count} decisions will be dropped, RabbitMQ redelivers their events",
                _processingResultsQueue.Reader.Count);
        }
    }

    private async Task SendDecisionAsync(EventProcessingResult processingResult, CancellationToken stoppingToken)
    {
        var actionName = processingResult.Decision == EventDecisionType.Confirm ? "confirm" : "reject";
        try
        {
            bool isSent;
            _logger.LogTrace("Entering lock to {ActionName} message...", actionName);
            await _lock.WaitAsync(stoppingToken);
            try
            {
                _logger.LogTrace("Entered lock to {ActionName} message", actionName);

                // a delivery tag of a closed channel is unknown to a new one: the broker would close it with 406
                isSent = ReferenceEquals(processingResult.Channel, _channel);
                if (isSent)
                {
                    if (processingResult.Decision == EventDecisionType.Confirm)
                    {
                        await processingResult.Channel.BasicAckAsync(processingResult.DeliveryTag, false, stoppingToken);
                    }
                    else
                    {
                        await processingResult.Channel.BasicRejectAsync(processingResult.DeliveryTag, false, stoppingToken);
                    }
                }
            }
            finally
            {
                _lock.Release();
            }
            _logger.LogTrace("Exited lock to {ActionName} message", actionName);

            if (isSent)
            {
                _logger.LogDebug(
                    "Completed {ActionName} for received event with DeliveryTag={DeliveryTag}",
                    actionName,
                    processingResult.DeliveryTag);
            }
            else
            {
                _logger.LogWarning(
                    "Can't {ActionName} event with DeliveryTag={DeliveryTag}: its channel was replaced, "
                    + "RabbitMQ redelivers the event",
                    actionName,
                    processingResult.DeliveryTag);
            }
        }
        catch (RabbitMQClientException e) when (!_isStopping)
        {
            // probably, there are connection issues: wait for auto recovery, recreate the channel if it fails
            _logger.LogWarning(
                e,
                "Got {ExceptionName} exception while {ActionName} event with DeliveryTag={DeliveryTag}. "
                + "Waiting for recovering for {RecoverWaitDelay}",
                e.GetType().Name,
                actionName,
                processingResult.DeliveryTag,
                _recoverWaitDelay);

            await HandleRecoverySafelyAsync(processingResult, cancellationToken: _cts.Token);
        }
        catch (Exception e) when (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogError(
                e,
                "Failed to {ActionName} event with DeliveryTag={DeliveryTag}, RabbitMQ redelivers the event",
                actionName,
                processingResult.DeliveryTag);
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// Sends the decisions made before the call, until <paramref name="cancellationToken"/> is cancelled.
    /// Events confirmed or rejected after the call are redelivered by RabbitMQ.
    /// </remarks>
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (Interlocked.Exchange(ref _isStopped, 1) == 1) return;

        _logger.LogDebug($"Stopping {nameof(RabbitMQEventReceiver)}...");

        _isStopping = true;
        _processingResultsQueue.Writer.TryComplete();
        if (ExecuteTask != null)
        {
            _logger.LogTrace("Waiting for sending decisions...");
            try
            {
                await ExecuteTask.WaitAsync(cancellationToken);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    "Stopped waiting for sending decisions. {Count} decisions will be dropped",
                    _processingResultsQueue.Reader.Count);
            }
        }

        // stop recovering
        await _cts.CancelAsync();

        _logger.LogTrace("Stopping finalizer...");
        await base.StopAsync(cancellationToken);
        _logger.LogTrace("Stopped finalizer");

        await CloseConnectionAsync();

        _logger.LogDebug($"Stopped {nameof(RabbitMQEventReceiver)}");
    }

    private async Task CloseConnectionAsync()
    {
        _logger.LogTrace("Entering lock to close channel/connection...");
        await _lock.WaitAsync(CancellationToken.None);
        try
        {
            _logger.LogTrace("Entered lock to close channel/connection");
            await DisconnectAsync();
        }
        finally
        {
            _lock.Release();
        }
        _logger.LogTrace("Exited lock to close channel/connection");
    }

    /// <summary>
    /// Stops the receiver if <see cref="StopAsync"/> was not called and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        using (var stopTimeout = new CancellationTokenSource(StopOnDisposeTimeout))
        {
            await StopAsync(stopTimeout.Token);
        }

        Dispose();
        _cts.Dispose();
        GC.SuppressFinalize(this);
    }

    private readonly struct EventProcessingResult
    {
        /// <summary>
        /// Channel the event was received on: its delivery tags are valid only on it.
        /// </summary>
        public IChannel Channel { get; }

        public ulong DeliveryTag { get; }

        public EventDecisionType Decision { get; }

        public EventProcessingResult(
            IChannel channel,
            ulong deliveryTag,
            EventDecisionType decision)
        {
            Channel = channel;
            DeliveryTag = deliveryTag;
            Decision = decision;
        }
    }

    private enum EventDecisionType
    {
        Confirm,
        Reject
    }
}
