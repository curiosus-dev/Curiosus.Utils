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
/// <see cref="StopAsync"/> or <see cref="DisposeAsync"/> closes the connection to RabbitMQ.
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
        _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await _channel.BasicQosAsync(0, (ushort)_qos, true, cancellationToken);
        _channel.ChannelShutdownAsync += HandleChannelShutdownAsync;

        await _channel.QueueDeclareAsync(
            _eventReceiverOptions.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(_channel);
        consumer.ReceivedAsync += ProcessReceivedEventAsync;
        await _channel.BasicConsumeAsync(_eventReceiverOptions.QueueName, autoAck: false, consumer, cancellationToken);
    }

    private Task HandleChannelShutdownAsync(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning(
            "RabbitMQ reader channel was shutdown. Cause=\"{Cause}\", Initiator={Initiator}, ReplyCode={ReplyCode}, ReplyText={ReplyText}, ClassId={ClassId}, MethodId={MethodId}",
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
        if (eventProcessingResult.HasValue)
            _processingResultsQueue.Writer.TryWrite(eventProcessingResult.Value);

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
    private Task ProcessReceivedEventAsync(object? sender, BasicDeliverEventArgs e)
    {
        var receivedEvent = new RabbitMQEvent(e, ConfirmEvent, RejectEvent);
        OnEventReceived?.Invoke(this, receivedEvent);

        return Task.CompletedTask;
    }

    private void ConfirmEvent(ulong deliveryTag)
    {
        _processingResultsQueue.Writer.TryWrite(new EventProcessingResult(deliveryTag, EventDecisionType.Confirm));
    }

    private void RejectEvent(ulong deliveryTag)
    {
        _processingResultsQueue.Writer.TryWrite(new EventProcessingResult(deliveryTag, EventDecisionType.Reject));
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            EventProcessingResult? processingResult = null;

            try
            {
                processingResult = await _processingResultsQueue.Reader.ReadAsync(stoppingToken);
                string actionName;

                switch (processingResult.Value.Decision)
                {
                    case EventDecisionType.Confirm:
                        actionName = "confirm";
                        break;
                    case EventDecisionType.Reject:
                        actionName = "reject";
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(processingResult.Value.Decision), processingResult.Value.Decision, null);
                }

                try
                {
                    _logger.LogTrace("Entering lock to {ActionName} message...", actionName);
                    await _lock.WaitAsync(stoppingToken);
                    try
                    {
                        _logger.LogTrace("Entered lock to {ActionName} message", actionName);
                        if (_channel != null)
                        {
                            switch (processingResult.Value.Decision)
                            {
                                case EventDecisionType.Confirm:
                                    await _channel.BasicAckAsync(processingResult.Value.DeliveryTag, false, stoppingToken);
                                    break;
                                case EventDecisionType.Reject:
                                    await _channel.BasicRejectAsync(processingResult.Value.DeliveryTag, false, stoppingToken);
                                    break;
                                default:
                                    throw new ArgumentOutOfRangeException(
                                        nameof(processingResult.Value.Decision),
                                        processingResult.Value.Decision,
                                        null);
                            }
                        }
                    }
                    finally
                    {
                        _lock.Release();
                    }
                    _logger.LogTrace("Exited lock to {ActionName} message", actionName);

                    _logger.LogDebug(
                        "Completed {ActionName} for received event with DeliveryTag={DeliveryTag}. Finalization of this event completed",
                        actionName,
                        processingResult.Value.DeliveryTag);
                }
                catch (RabbitMQClientException e)
                {
                    // if we got this exception, probably we have some some connection issues.
                    // We will try to wait auto recovering. If it fails, we will recreate the channel

                    _logger.LogWarning(e,
                        "Got {ExceptionName} exception while {ActionName} event with DeliveryTag={DeliveryTag}. Waiting for recovering for {RecoverWaitDelay}",
                        e.GetType().Name,
                        actionName,
                        processingResult.Value.DeliveryTag,
                        _recoverWaitDelay);

                    await HandleRecoverySafelyAsync(
                        processingResult,
                        cancellationToken: _cts.Token);
                }
                catch (Exception e) when (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogError(e,
                        "Failed to {ActionName} event with DeliveryTag={DeliveryTag}",
                        actionName,
                        processingResult.Value);
                }

            }
            catch (Exception e) when (stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(
                    e,
                    "Stopping finalizer. Response with DeliveryTag={DeliveryTag} will be dropped",
                    processingResult?.DeliveryTag.ToString() ?? "<unknown>");
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    "Error while finalizing event with DeliveryTag={DeliveryTag}",
                    processingResult?.DeliveryTag.ToString() ?? "<unknown>");
            }
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug($"Stopping {nameof(RabbitMQEventReceiver)}...");

        // stop sending requests
        _logger.LogTrace("Cancelling cts to stop sending requests...");
        await _cts.CancelAsync();

        // maybe, it's safer to cancel all items in a queue? let's think about it later
        //TODO: https://github.com/siisltd/Curiosity.Utils/issues/50

        // stop background thread
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
    /// Closes the connection to RabbitMQ if <see cref="StopAsync"/> was not called and releases resources.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        await CloseConnectionAsync();

        Dispose();
        GC.SuppressFinalize(this);
    }

    private readonly struct EventProcessingResult
    {
        public ulong DeliveryTag { get; }

        public EventDecisionType Decision { get; }

        public EventProcessingResult(
            ulong deliveryTag,
            EventDecisionType decision)
        {
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
