using System;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Curiosus.Tools;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Curiosus.RabbitMQ.RPC;

/// <summary>
/// RPC client that used RabbitMQ queues to emulate RPC.
/// </summary>
public class RabbitMqRpcClient : IAsyncDisposable
{
    /// <summary>
    /// Count of retries to restore connection to RabbitMQ manually if auto recovery fails.
    /// </summary>
    /// <remarks>
    /// Useful when we got an <see cref="AlreadyClosedException"/> or another RabbitMQ exception.
    /// </remarks>
    private const int MaxConnectionRestoreRetries = 10;

    /// <summary>
    /// Timeout of closing <see cref="_rabbitMqConnection"/>.
    /// </summary>
    private static readonly TimeSpan ConnectionCloseTimeout = TimeSpan.FromSeconds(3);

    /// <summary>
    /// Default JSON options for requests and responses.
    /// </summary>
    /// <remarks>
    /// Mimics Newtonsoft.Json defaults (used before 2.0.0) to keep the wire format compatible with existing RPC peers:
    /// case-insensitive property names, public fields, numbers from strings and unescaped non-ASCII characters.
    /// The instance is read-only; to customize, copy it: <c>new JsonSerializerOptions(DefaultJsonSerializerOptions)</c>.
    /// </remarks>
    public static JsonSerializerOptions DefaultJsonSerializerOptions { get; } = CreateDefaultJsonSerializerOptions();

    private static JsonSerializerOptions CreateDefaultJsonSerializerOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            IncludeFields = true,
            NumberHandling = JsonNumberHandling.AllowReadingFromString,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };
        options.MakeReadOnly(populateMissingResolver: true);

        return options;
    }

    /// <summary>
    /// Time for waiting connection full recovering before sending data to the queues.
    /// </summary>
    private readonly TimeSpan _recoverWaitDelay;

    /// <summary>
    /// Guards channels and connection: they are replaced on manual recovery.
    /// </summary>
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly string _requestQueueName;
    private readonly string _responseQueueName;
    private readonly bool _disposeResponseQueue;

    private readonly ILogger _logger;
    private readonly JsonSerializerOptions _jsonSerializerOptions;

    private IChannel? _readerChannel;
    private IChannel? _writerChannel;
    private IConnection? _rabbitMqConnection;

    private readonly Func<CancellationToken, Task<IConnection>> _rabbitMqConnectionFactory;
    private readonly string _clientName;
    private readonly string _exchangeName;

    private readonly ConcurrentDictionary<string, TaskCompletionSource<(string Response, ulong DeliveryTag)>> _responseWaitQueue;
    private readonly Channel<RpcQueueItem> _requestQueue;

    private readonly CancellationTokenSource _cts = new();
    private Task _backgroundSendingTask = Task.CompletedTask;

    /// <inheritdoc cref="RabbitMqRpcClient"/>
    internal RabbitMqRpcClient(
        string clientName,
        string exchangeName,
        string requestQueueName,
        string responseQueueName,
        ILogger logger,
        TimeSpan networkRecoveryInterval,
        Func<CancellationToken, Task<IConnection>> rabbitMqConnectionFactory,
        bool disposeResponseQueue,
        JsonSerializerOptions? jsonSerializerOptions = null)
    {
        if (String.IsNullOrWhiteSpace(clientName)) throw new ArgumentNullException(nameof(clientName));

        _clientName = clientName;

        _exchangeName = exchangeName ?? throw new ArgumentNullException(nameof(exchangeName));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _rabbitMqConnectionFactory = rabbitMqConnectionFactory
                                     ?? throw new ArgumentNullException(nameof(rabbitMqConnectionFactory));
        _disposeResponseQueue = disposeResponseQueue;
        _jsonSerializerOptions = jsonSerializerOptions ?? DefaultJsonSerializerOptions;
        _requestQueueName = requestQueueName ?? throw new ArgumentNullException(nameof(requestQueueName));
        _responseQueueName = responseQueueName ?? throw new ArgumentNullException(nameof(responseQueueName));

        // prepare response and request queues
        _responseWaitQueue = new ConcurrentDictionary<string, TaskCompletionSource<(string Response, ulong DeliveryTag)>>();
        _requestQueue = Channel.CreateUnbounded<RpcQueueItem>();

        // get more time for waiting because of auto recovery
        // give time to auto recovery and only after that we will try to restore all manually
        _recoverWaitDelay = TimeSpan.FromMilliseconds(networkRecoveryInterval.TotalMilliseconds * 2);
    }

    internal async Task InitAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogTrace("Entering lock for client initialization...");
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogTrace("Entered lock for client initialization");
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
        _logger.LogTrace("Exited lock for client initialization");

        // start background task to send request in a single thread
        _backgroundSendingTask = ProcessOutgoingRequestsQueueAsync(_cts.Token);

        _logger.LogDebug(
            "Initialized new RabbitMQ RpcClient (request queue = \"{RequestQueueName}\", response queue = \"{ResponseQueueName}\")",
            _requestQueueName,
            _responseQueueName);
    }

    /// <summary>
    /// Connects to RabbitMQ. Should be invoked only from a critical section.
    /// </summary>
    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        _logger.LogTrace("Connecting to RabbitMQ...");

        _rabbitMqConnection = await _rabbitMqConnectionFactory.Invoke(cancellationToken);
        _rabbitMqConnection.ConnectionShutdownAsync += HandleOnDisconnectedAsync;

        _writerChannel = await _rabbitMqConnection.CreateChannelAsync(cancellationToken: cancellationToken);
        _writerChannel.ChannelShutdownAsync += WriterChannelOnShutdownAsync;

        // declare request queue, if it has been already exists, no action will performed
        await _writerChannel.QueueDeclareAsync(
            _requestQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        var readerChannel = await _rabbitMqConnection.CreateChannelAsync(cancellationToken: cancellationToken);
        _readerChannel = readerChannel;
        readerChannel.ChannelShutdownAsync += ReaderChannelOnShutdownAsync;

        if (_disposeResponseQueue)
        {
            // delete and create response queue
            // it's safer to make queue not auto deletable and just recreate on each rpc client creation
            await readerChannel.QueueDeleteAsync(_responseQueueName, cancellationToken: cancellationToken);
        }

        await readerChannel.QueueDeclareAsync(
            _responseQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);

        // start consuming messages
        var consumer = new AsyncEventingBasicConsumer(readerChannel);
        consumer.ReceivedAsync += (_, e) => ProcessIncomingResponseAsync(readerChannel, e);
        await readerChannel.BasicConsumeAsync(_responseQueueName, autoAck: false, consumer, cancellationToken);

        _logger.LogDebug(
            "Connected to RabbitMQ. Started new channels (host \"{RabbitMqHostName}\", queue = \"{RequestQueueName}\", response queue = \"{ResponseQueueName}\")",
            _rabbitMqConnection.Endpoint.HostName,
            _requestQueueName,
            _responseQueueName);
    }

    private Task WriterChannelOnShutdownAsync(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning(
            "RabbitMQ writer channel was shutdown. Cause=\"{Cause}\", Initiator={Initiator}, ReplyCode={ReplyCode}, ReplyText={ReplyText}, ClassId={ClassId}, MethodId={MethodId}",
            e.Cause,
            e.Initiator,
            e.ReplyCode,
            e.ReplyText,
            e.ClassId,
            e.MethodId);

        // if we got AMQP consumer timeout exception let's try to restore connection
        // not awaited: recovery closes the channel, and the channel waits for its shutdown handlers
        if (IsRecoverableShutdown(e))
        {
            HandleRecoverySafelyAsync(1, _cts.Token).WithExceptionLogger(_logger);
        }

        return Task.CompletedTask;
    }

    private Task ReaderChannelOnShutdownAsync(object? sender, ShutdownEventArgs e)
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
            HandleRecoverySafelyAsync(1, _cts.Token).WithExceptionLogger(_logger);
        }

        return Task.CompletedTask;
    }

    private static bool IsRecoverableShutdown(ShutdownEventArgs e)
    {
        return e.ReplyCode == 406 && e.ReplyText.Contains("timeout") || e.ReplyCode == 541;
    }

    private Task HandleOnDisconnectedAsync(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning(
            "RabbitMQ client connection was disconnected. Cause=\"{Cause}\", Initiator={Initiator}, ReplyCode={ReplyCode}, ReplyText={ReplyText}, ClassId={ClassId}, MethodId={MethodId}",
            e.Cause,
            e.Initiator,
            e.ReplyCode,
            e.ReplyText,
            e.ClassId,
            e.MethodId);

        return Task.CompletedTask;
    }

    /// <summary>
    /// Processes queue of outgoing requests.
    /// </summary>
    private async Task ProcessOutgoingRequestsQueueAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Started task for sending request to {RequestQueueName}...", _requestQueueName);
        await Task.Yield();

        const string noCorrelationId = "<no correlation id>";
        while (!cancellationToken.IsCancellationRequested)
        {
            var correlationId = noCorrelationId;
            try
            {
                var request = await _requestQueue.Reader.ReadAsync(cancellationToken);
                correlationId = request.CorrelationId;

                if (request.CancellationToken.IsCancellationRequested)
                {
                    _logger.LogTrace(
                        "Sending request with CorrelationId={CorrelationId} to RabbitMQ was cancelled by user",
                        correlationId);
                    continue;
                }

                // we check reader channel on each send request to be sure that reader is ok
                // if it is not ok - recover connection
                if (_readerChannel == null || _readerChannel.IsClosed)
                {
                    _logger.LogWarning(
                        "It's seems that there were some connection errors. Reconnecting channel and re-queueing request with CorrelationId={CorrelationId}",
                        correlationId);

                    var isRecovered = await HandleRecoverySafelyAsync(cancellationToken: cancellationToken);
                    if (!isRecovered)
                    {
                        _logger.LogError("Failed to recover reader channel");
                    }

                    continue;
                }

                var props = new BasicProperties
                {
                    CorrelationId = correlationId,
                    ReplyTo = _responseQueueName
                };

                var messageBytes = Encoding.UTF8.GetBytes(request.Message);

                _logger.LogTrace("Entering lock for publishing message...");
                await _lock.WaitAsync(cancellationToken);
                try
                {
                    _logger.LogTrace("Entered lock for publishing message...");
                    if (_writerChannel == null)
                        throw new ArgumentException("Failed to publish message because of null writer channel");

                    await _writerChannel.BasicPublishAsync(
                        _exchangeName,
                        _requestQueueName,
                        mandatory: false,
                        props,
                        messageBytes,
                        cancellationToken);
                }
                finally
                {
                    _lock.Release();
                }
                _logger.LogTrace("Exited lock for publishing message");

                _logger.LogDebug(
                    "Sent message with CorrelationId={CorrelationId}",
                    correlationId);
            }
            catch (RabbitMQClientException e)
            {
                // if we got this exception, probably we have some some connection issues. We will try to wait for auto recovering.
                // If it fails, we will recreate the channel manually

                _logger.LogWarning(e,
                    "Got {ExceptionName} exception. Waiting for recovering for {RecoverWaitDelay}. Message with CorrelationId={CorrelationId} will be re-queued",
                    e.GetType().Name,
                    _recoverWaitDelay,
                    correlationId);

                var isRecovered = await HandleRecoverySafelyAsync(cancellationToken: cancellationToken);
                if (isRecovered)
                {
                    _logger.LogDebug("Connection was successfully recovered. Message with CorrelationId={CorrelationId} was failed", correlationId);
                }
                else
                {
                    _logger.LogError("Failed to recover connection. Message with CorrelationId={CorrelationId} will be failed", correlationId);
                    // remove pending response
                    FailOutgoingRequest(correlationId, e);
                }
            }
            catch (Exception e) when (cancellationToken.IsCancellationRequested)
            {
                // if there is no correlation id, then cancelling was in moment of waiting retrieving item from the request queue
                // no correlation id - no request, no request - no error
                if (correlationId != noCorrelationId)
                {
                    _logger.LogWarning(
                        e,
                        "Sending request with CorrelationId={CorrelationId} to RabbitMQ was cancelled. Reason: {Message}",
                        correlationId,
                        e.Message);

                    // remove pending response
                    FailOutgoingRequest(correlationId, e);
                }
            }
            catch (Exception e)
            {
                _logger.LogError(
                    e,
                    "Error while sending request with CorrelationId={CorrelationId} to RabbitMQ. Reason: {Message}",
                    correlationId,
                    e.Message);

                // remove pending response
                FailOutgoingRequest(correlationId, e);
            }
        }

        // cancel pending response wait queue
        _logger.LogTrace("Cancelling pending response wait queue...");
        foreach (var key in _responseWaitQueue.Keys)
        {
            // ReSharper disable once MethodSupportsCancellation
            _responseWaitQueue.TryRemove(key, out var cts);
            if (cts == null) continue;

            cts.TrySetCanceled();
        }

        _logger.LogDebug("Completed task for sending request to {RequestQueueName}", _requestQueueName);
    }

    private void FailOutgoingRequest(string correlationId, Exception e)
    {
        if (_responseWaitQueue.TryRemove(correlationId, out var tcs))
        {
            tcs.TrySetException(e);
        }
        else
        {
            _logger.LogError("Can find request with CorrelationId={CorrelationId} to set error", correlationId);
        }
    }

    /// <summary>
    /// Handle connection recovery without throwing any exception. Re-queues specified request if recovery completes successfully.
    /// </summary>
    private async Task<bool> HandleRecoverySafelyAsync(
        int currentRetriesCount = 1,
        CancellationToken cancellationToken = default)
    {
        // maybe there is no need to restore client?
        if (AreChannelsOpen()) return true;

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
        if (AreChannelsOpen()) return true;

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
            if (AreChannelsOpen()) return true;

            // check, if any channel is still closed, than auto recovering failed, need to restore them manually
            if ((_readerChannel?.IsClosed ?? true) || (_writerChannel?.IsClosed ?? true))
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
                        "Failed to restore channel manually to queue \"{RequestQueueName}\" ({CurrentRetriesCount}/{MaxRetriesCount})",
                        _requestQueueName,
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

                    isRecovered = await HandleRecoverySafelyAsync(currentRetriesCount + 1, cancellationToken);
                }
            }
            else
            {
                _logger.LogInformation(
                    "Channel was restored automatically (\"{RequestQueueName}\" {CurrentRetriesCount}/{MaxRetriesCount})",
                    _requestQueueName,
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

        // re-queue outgoing request
        if (isRecovered)
        {
            // because of manual connection restoring all delivery tags were reset
            // so we need to cancel all tasks and reset outgoing request queue
            if (currentRetriesCount == 1)
            {
                while (_requestQueue.Reader.TryRead(out _))
                {
                }

                var keys = _responseWaitQueue.Keys;
                foreach (var correlationId in keys)
                {
                    if (_responseWaitQueue.TryRemove(correlationId, out var tcs))
                    {
                        tcs.TrySetException(new RabbitMqRpcReQueueException());
                    }
                    _logger.LogDebug("Set exception to request with CorrelationId={CorrelationId} to re-queue it", correlationId);
                }
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

    private bool AreChannelsOpen()
    {
        return (_readerChannel?.IsOpen ?? false) && (_writerChannel?.IsOpen ?? false);
    }

    /// <summary>
    /// Closes connection to RabbitMQ. Should be invoked only from a critical section.
    /// </summary>
    private async Task DisconnectAsync()
    {
        // free used channels

        if (_readerChannel != null)
        {
            if (_disposeResponseQueue)
            {
                try
                {
                    _logger.LogTrace("Deleting \"{ResponseQueueName}\" queue...", _responseQueueName);
                    await _readerChannel.QueueDeleteAsync(_responseQueueName);
                    _logger.LogDebug("Response queue \"{ResponseQueueName}\" was deleted", _responseQueueName);
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to delete queue \"{ResponseQueueName}\"", _responseQueueName);
                }
            }
            else
            {
                _logger.LogDebug("Skip deleting response queue because of configuration");
            }

            _readerChannel.ChannelShutdownAsync -= ReaderChannelOnShutdownAsync;

            if (!_readerChannel.IsClosed)
            {
                _logger.LogTrace("Waiting for reader channel closing...");
                try
                {
                    await _readerChannel.CloseAsync();
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to close reader channel on rpc client restoring");
                }
            }
            else
            {
                _logger.LogTrace("Reader channel have been already closed");
            }

            _logger.LogTrace("Waiting for reader channel disposing...");
            await _readerChannel.DisposeAsync();
        }
        else
        {
            _logger.LogTrace("Reader channel has been already null");
        }
        _readerChannel = null;

        if (_writerChannel != null)
        {
            _writerChannel.ChannelShutdownAsync -= WriterChannelOnShutdownAsync;

            if (!_writerChannel.IsClosed)
            {
                _logger.LogTrace("Waiting for writer channel closing...");
                try
                {
                    await _writerChannel.CloseAsync();
                }
                catch (Exception e)
                {
                    _logger.LogWarning(e, "Failed to close writer channel on rpc client restoring");
                }
            }
            else
            {
                _logger.LogTrace("Writer channel have been already closed");
            }

            _logger.LogTrace("Waiting for writer channel disposing...");
            await _writerChannel.DisposeAsync();
        }
        else
        {
            _logger.LogTrace("Writer channel has been already null");
        }

        _writerChannel = null;

        // free connection

        if (_rabbitMqConnection != null)
        {
            _rabbitMqConnection.ConnectionShutdownAsync -= HandleOnDisconnectedAsync;

            _logger.LogTrace("Waiting for connection closing...");
            try
            {
                await _rabbitMqConnection.CloseAsync(ConnectionCloseTimeout);
            }
            catch (Exception e)
            {
                _logger.LogWarning(e, "Failed to close connection on rpc client restoring");
            }

            _logger.LogTrace("Waiting for connection disposing...");
            await _rabbitMqConnection.DisposeAsync();
            _rabbitMqConnection = null;
        }
        else
        {
            _logger.LogTrace("Connection has been already null");
        }
    }

    /// <summary>
    /// Returns count of consumers connected to the specified in options queue.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<int> GetConsumersCountAsync(CancellationToken cancellationToken = default)
    {
        uint result;

        _logger.LogTrace("Entering lock for retrieving consumers count...");
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogTrace("Entered lock for retrieving consumers count");
            if (_writerChannel == null)
                throw new InvalidOperationException("Failed to retrieve consumers count because of null writer channel");

            result = await _writerChannel.ConsumerCountAsync(_requestQueueName, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
        _logger.LogTrace("Exited lock for retrieving consumers count");

        return (int)result;
    }

    /// <summary>
    /// Sends request to the queue and wait a reply from remote service. Message will be automatically marked as acknowledged.
    /// </summary>
    /// <param name="request">Request to send, serialized to JSON.</param>
    /// <param name="correlationId">
    /// Correlation id of the request. Generated by <see cref="UniqueIdGenerator"/> if not specified.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    // ReSharper disable once UnusedMember.Global
    public async Task<TResponse> SendWithAutoAcknowledgeAsync<TResponse, TRequest>(
        TRequest request,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        var message = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        correlationId ??= $"{_clientName}_r{UniqueIdGenerator.Generate().ToPublicId()}";

        var responseMessage = await SendAsync(message, correlationId, cancellationToken);
        try
        {
            var response = JsonSerializer.Deserialize<TResponse>(responseMessage.Response, _jsonSerializerOptions);
            if (response == null)
            {
                throw new InvalidOperationException($"Incorrect response message with correlationId = {correlationId}");
            }

            return response;
        }
        finally
        {
            // the response was received: ack it even if the caller has already cancelled the request
            _logger.LogTrace("Entering lock to ack message automatically...");
            await _lock.WaitAsync(CancellationToken.None);
            try
            {
                _logger.LogTrace("Entered lock to ack message automatically");
                if (!(_readerChannel?.IsClosed ?? false))
                {
                    if (_readerChannel != null)
                        await _readerChannel.BasicAckAsync(responseMessage.DeliveryTag, false, CancellationToken.None);
                }
                else
                {
                    _logger.LogWarning(
                        "Reader channel is closed. Can't ack message with DeliveryTag={DeliveryTag}",
                        responseMessage.DeliveryTag);
                }
            }
            finally
            {
                _lock.Release();
            }
            _logger.LogTrace("Exited lock to ack message automatically");
        }
    }

    private async Task<(string Response, ulong DeliveryTag)> SendAsync(
        string message,
        string correlationId,
        CancellationToken cancellationToken = default)
    {
        await using (cancellationToken.Register(() => HandleSendRequestCancellation(correlationId, message)))
        {
            return await SendAsync(message, correlationId, 1, cancellationToken);
        }
    }

    /// <summary>
    /// Handles cancellation of send request from <see cref="SendAsync(string,string,System.Threading.CancellationToken)"/>.
    /// </summary>
    private void HandleSendRequestCancellation(string correlationId, string message)
    {
        _responseWaitQueue.TryRemove(correlationId, out var temp);
        if (temp == null) return;

        if (!temp.TrySetCanceled())
        {
            _logger.LogError(
                "Failed to set cancelled to TaskCompletionSource for message with CorrelationId={CorrelationId} (message = {Message})",
                correlationId,
                message);
        }
    }

    private async Task<(string Response, ulong DeliveryTag)> SendAsync(
        string message,
        string correlationId,
        int currentSendAttemptCount,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // continuations run asynchronously: otherwise the caller's code would run inside the consumer handler
            // and block the delivery of other responses (and closing the channel on recovery)
            var tcs = new TaskCompletionSource<(string Response, ulong DeliveryTag)>(
                TaskCreationOptions.RunContinuationsAsynchronously);
            _responseWaitQueue[correlationId] = tcs;

            await _requestQueue.Writer.WriteAsync(new RpcQueueItem(correlationId, message), cancellationToken);

            return await tcs.Task;
        }
        catch (RabbitMqRpcReQueueException e)
        {
            if (currentSendAttemptCount > MaxConnectionRestoreRetries)
            {
                _logger.LogWarning(
                    e,
                    "Exceed attempts for sending request with CorrelationId={CorrelationId} ({CurrentAttempts}/{MaxAttempts})",
                    correlationId,
                    currentSendAttemptCount,
                    MaxConnectionRestoreRetries);
                throw new InvalidOperationException("Failed to send request because of issues with RabbitMQ", e);
            }

            _logger.LogWarning(
                e,
                "Failed to send request with CorrelationId={CorrelationId}. Trying to resend... ({CurrentAttempts}/{MaxAttempts})",
                correlationId,
                currentSendAttemptCount,
                MaxConnectionRestoreRetries);
            return await SendAsync(message, correlationId, currentSendAttemptCount + 1, cancellationToken);
        }
    }

    /// <summary>
    /// Sends request to the queue and wait a reply from remote service. Message will not be automatically marked as acknowledged.
    /// You should do it manually via <see cref="ManualAckRabbitResult{T}.ConfirmAcknowledgeAsync"/>.
    /// </summary>
    /// <param name="request">Request to send, serialized to JSON.</param>
    /// <param name="correlationId">
    /// Correlation id of the request. Generated by <see cref="UniqueIdGenerator"/> if not specified.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<ManualAckRabbitResult<TResponse>> SendWithManualAcknowledgeAsync<TResponse, TRequest>(
        TRequest request,
        string? correlationId = null,
        CancellationToken cancellationToken = default)
    {
        if (request == null) throw new ArgumentNullException(nameof(request));

        var message = JsonSerializer.Serialize(request, _jsonSerializerOptions);
        correlationId ??= $"{_clientName}_r{UniqueIdGenerator.Generate().ToPublicId()}";

        _logger.LogDebug(
            "Sending request with manual receiving acknowledgment (CorrelationId={CorrelationId})",
            correlationId);

        (string Response, ulong DeliveryTag) responseMessage = default;
        TResponse response;
        try
        {
            responseMessage = await SendAsync(message, correlationId, cancellationToken);
            response = JsonSerializer.Deserialize<TResponse>(responseMessage.Response, _jsonSerializerOptions)!;
        }
        catch (Exception e)
        {
            _logger.LogTrace(e, "Entering lock for reject message because of exception...");
            await _lock.WaitAsync(CancellationToken.None);
            try
            {
                _logger.LogTrace(e, "Entered lock for reject message because of exception");
                if (_readerChannel != null)
                    await _readerChannel.BasicRejectAsync(responseMessage.DeliveryTag, false, CancellationToken.None);
            }
            finally
            {
                _lock.Release();
            }
            _logger.LogTrace(e, "Exited lock for reject message with because of exception");

            _logger.LogDebug(
                "Manually rejected receiving response with DeliveryTag={DeliveryTag} (CorrelationId={CorrelationId}) because of exception",
                responseMessage.DeliveryTag,
                correlationId);
            throw;
        }

        if (response == null)
        {
            _logger.LogTrace("Entering lock for ack message with incorrect type...");
            await _lock.WaitAsync(CancellationToken.None);
            try
            {
                _logger.LogTrace("Entered lock for ack message with incorrect type");
                if (_readerChannel != null)
                    await _readerChannel.BasicAckAsync(responseMessage.DeliveryTag, false, CancellationToken.None);
            }
            finally
            {
                _lock.Release();
            }
            _logger.LogTrace("Exited lock for ack message with incorrect type");

            _logger.LogDebug(
                "Manually acknowledged receiving response with DeliveryTag={DeliveryTag} (CorrelationId={CorrelationId}) because of incorrect type",
                responseMessage.DeliveryTag,
                correlationId);
            throw new InvalidOperationException($"Incorrect response message with correlationId = {correlationId}");
        }

        var wrapper = new ManualAckRabbitResult<TResponse>(
            response,
            ct => AcknowledgeManuallyAsync(responseMessage.DeliveryTag, correlationId, ct));

        return wrapper;
    }

    private async Task AcknowledgeManuallyAsync(ulong deliveryTag, string correlationId, CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogTrace("Entering lock for ack message manually...");
            await _lock.WaitAsync(cancellationToken);
            try
            {
                _logger.LogTrace("Entered lock for ack message manually");
                if (_readerChannel != null)
                    await _readerChannel.BasicAckAsync(deliveryTag, false, cancellationToken);
            }
            finally
            {
                _lock.Release();
            }
            _logger.LogTrace("Exited lock for ack message manually");

            _logger.LogDebug(
                "Manually acknowledged receiving response with DeliveryTag={DeliveryTag} (CorrelationId={CorrelationId})",
                deliveryTag,
                correlationId);
        }
        catch (RabbitMQClientException e)
        {
            // if we got this exception, probably we have some some connection issues. We will try to wait auto recovering. If it fails, we will recreate the channel

            _logger.LogWarning(e,
                "Got {ExceptionName} exception while manually acknowledging response with DeliveryTag={DeliveryTag} (CorrelationId={CorrelationId}). Waiting for recovering for {RecoverWaitDelay}",
                e.GetType().Name,
                deliveryTag,
                correlationId,
                _recoverWaitDelay);

            HandleRecoverySafelyAsync(cancellationToken: _cts.Token).WithExceptionLogger(_logger);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            _logger.LogError(e,
                "Failed to acknowledge the response with DeliveryTag={DeliveryTag} (CorrelationId={CorrelationId})",
                deliveryTag,
                correlationId);
        }
    }

    /// <remarks>
    /// Runs inside the consumer dispatcher of <paramref name="channel"/>: it must not wait for <see cref="_lock"/>,
    /// because the lock holder may be closing this channel and waiting for the dispatcher.
    /// </remarks>
    private async Task ProcessIncomingResponseAsync(IChannel channel, BasicDeliverEventArgs e)
    {
        var correlationId = e.BasicProperties.CorrelationId;
        var message = Encoding.UTF8.GetString(e.Body.Span);

        TaskCompletionSource<(string Response, ulong DeliveryTag)>? tcs = null;
        if (correlationId != null)
        {
            _responseWaitQueue.TryRemove(correlationId, out tcs);
        }

        if (tcs != null)
        {
            _logger.LogDebug("Received response for message with CorrelationId={CorrelationId}", correlationId);
            tcs.TrySetResult((message, e.DeliveryTag));
        }
        else
        {
            _logger.LogWarning(
                "Can't find request for response with CorrelationId={CorrelationId} (DeliveryTag={DeliveryTag}). Message will be rejected",
                correlationId,
                e.DeliveryTag);

            try
            {
                // we don't requeue the message, because item in _responseWaitQueue will not appear magically
                if (channel.IsOpen)
                {
                    await channel.BasicRejectAsync(e.DeliveryTag, false, e.CancellationToken);

                    _logger.LogWarning(
                        "Message with CorrelationId={CorrelationId} (DeliveryTag={DeliveryTag}) was rejected",
                        correlationId,
                        e.DeliveryTag);
                }
                else
                {
                    _logger.LogWarning(
                        "Failed to reject acknowledgment for message with CorrelationId={CorrelationId} (DeliveryTag={DeliveryTag}) because channel is closed",
                        correlationId,
                        e.DeliveryTag);
                }
            }
            catch (RabbitMQClientException exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to reject acknowledgment for message with CorrelationId={CorrelationId} (DeliveryTag={DeliveryTag}) because of problems with connection. Tries to reconnect...",
                    correlationId,
                    e.DeliveryTag);

                HandleRecoverySafelyAsync(cancellationToken: _cts.Token).WithExceptionLogger(_logger);
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to reject acknowledgment for message with CorrelationId={CorrelationId} (DeliveryTag={DeliveryTag})",
                    correlationId,
                    e.DeliveryTag);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // stop sending requests
        _logger.LogTrace("Cancelling cts to stop sending requests...");
        await _cts.CancelAsync();

        // wait for task completion
        _logger.LogTrace("Waiting for sending task completion...");
        await _backgroundSendingTask;

        // close channel
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

        _logger.LogTrace("Rpc client is fully disposed");
    }

    private readonly struct RpcQueueItem
    {
        public string CorrelationId { get; }

        public string Message { get; }

        public CancellationToken CancellationToken { get; }

        public RpcQueueItem(
            string correlationId,
            string message,
            CancellationToken cancellationToken = default)
        {
            CorrelationId = correlationId;
            Message = message;
            CancellationToken = cancellationToken;
        }
    }
}
