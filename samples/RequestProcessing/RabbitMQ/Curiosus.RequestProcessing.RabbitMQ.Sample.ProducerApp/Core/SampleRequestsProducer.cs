using System.Globalization;
using System.Text;
using System.Text.Json;
using Curiosus.RabbitMQ;
using Curiosus.RequestProcessing.RabbitMQ.Sample.Common;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Configuration;
using Curiosus.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using RabbitMQ.Client.Exceptions;

namespace Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Core;

/// <summary>
/// Sample to send sample request to RabbitMQ.
/// </summary>
public class SampleRequestsProducer : BackgroundService
{
    /// <summary>
    /// Count of retries to restore connection to RabbitMQ manually if auto recovery fails.
    /// </summary>
    /// <remarks>
    /// Useful when we got an <see cref="AlreadyClosedException"/> or another RabbitMQ exception. 
    /// </remarks>
    private const int MaxConnectionRestoreRetries = 10;

    /// <summary>
    /// Default period for RabbitMQ network recovery.
    /// </summary>
    private static readonly TimeSpan NetworkRecoveryPeriod = TimeSpan.FromSeconds(10);

    /// <summary>
    /// Period for service for waiting full recovery.
    /// </summary>
    private static readonly TimeSpan RecoverWaitDelay = TimeSpan.FromMilliseconds(NetworkRecoveryPeriod.TotalMilliseconds * 1.5);

    private readonly RabbitMQOptions _rabbitMQOptions;
    private readonly ILogger _logger;
    private readonly SampleProducerAppConfiguration _configuration;

    private IConnection? _connection;
    private IChannel? _channel;
    private readonly SemaphoreSlim _lock = new(1, 1);

    private readonly CancellationTokenSource _cancellationTokenSource = new();

    public SampleRequestsProducer(
        SampleProducerAppConfiguration configuration,
        ILogger<SampleRequestsProducer> logger)
    {
        _configuration = configuration;
        _logger = logger;
        _rabbitMQOptions = configuration.RabbitMQ;
    }

    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Starting sender...");

        UniqueIdGenerator.Initialize(GetHashCode() % 1024);

        _logger.LogTrace("Entering lock for connecting to RabbitMQ...");
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogTrace("Entered lock for connecting to RabbitMQ");
            await ConnectAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
        _logger.LogTrace("Exited lock for connecting to RabbitMQ");

        // basic start
        await base.StartAsync(cancellationToken);


        _logger.LogDebug("Started sender");
    }

    private async Task ConnectAsync(CancellationToken cancellationToken)
    {
        var factory = new ConnectionFactory
        {
            HostName = _rabbitMQOptions.HostName,
            Port = _rabbitMQOptions.Port,
            AutomaticRecoveryEnabled = true,
            NetworkRecoveryInterval = NetworkRecoveryPeriod,
            TopologyRecoveryEnabled = true,
            UserName = _rabbitMQOptions.UserName,
            Password = _rabbitMQOptions.Password,
            ClientProvidedName = $"{_rabbitMQOptions.ClientName}_wallet_balance_check_result_sender"
        };

        _connection = await factory.CreateConnectionAsync(cancellationToken);
        _connection.ConnectionShutdownAsync += HandleOnDisconnectedAsync;

        await InitChannelAsync(_connection, cancellationToken);

        _logger.LogInformation(
            "Connected to RabbitMQ (host \"{RabbitHostName}\", queue = \"{QueueName}\"",
            _rabbitMQOptions.HostName,
            _configuration.QueueName);
    }

    /// <summary>
    /// Handles event when connection was disconnected.
    /// </summary>
    /// <param name="sender"></param>
    /// <param name="e"></param>
    private Task HandleOnDisconnectedAsync(object? sender, ShutdownEventArgs e)
    {
        _logger.LogWarning(
            "RabbitMQ connection was shutdown. Cause=\"{Cause}\", Initiator={Initiator}, ReplyCode={ReplyCode}, ReplyText={ReplyText}",
            e.Cause,
            e.Initiator,
            e.ReplyCode,
            e.ReplyText);

        // if we got AMQP consumer timeout exception let's try to restore connection
        if (e.ReplyCode == 406 && e.ReplyText.Contains("timeout") || e.ReplyCode == 541)
        {
            HandleRecoverySafelyAsync(1, _cancellationTokenSource.Token).WithExceptionLogger(_logger);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Inits new RabbitMQ channel.
    /// </summary>
    private async Task InitChannelAsync(IConnection connection, CancellationToken cancellationToken)
    {
        _channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        _channel.ChannelShutdownAsync += HandleChannelShutdownAsync;

        await _channel.QueueDeclareAsync(
            _configuration.QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken);
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
        if (e.ReplyCode == 406 && e.ReplyText.Contains("timeout") || e.ReplyCode == 541)
        {
            HandleRecoverySafelyAsync(1, _cancellationTokenSource.Token).WithExceptionLogger(_logger);
        }

        return Task.CompletedTask;
    }

    /// <summary>
    /// Handle connection recovery without throwing any exception
    /// </summary>
    private async Task<bool> HandleRecoverySafelyAsync(
        int currentRetriesCount,
        CancellationToken cancellationToken)
    {
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
            _logger.LogDebug("Make a delay for {RecoveryWaitDelay} before recovering", RecoverWaitDelay);
            await Task.Delay(RecoverWaitDelay, cancellationToken);
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
            await _lock.WaitAsync(cancellationToken);
            lockWasTaken = true;
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

                    isRecovered = await HandleRecoverySafelyAsync(currentRetriesCount + 1, cancellationToken);
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
                await _connection.CloseAsync(TimeSpan.FromSeconds(3));
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

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var newRequest = new SampleRequest(
                    UniqueIdGenerator.Generate(),
                    CultureInfo.CurrentCulture,
                    "sample data");

                // publish to rabbit
                var responseMessageBytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(newRequest));

                var responseProps = new BasicProperties { CorrelationId = newRequest.Id.ToString() };

                _logger.LogTrace("Entering lock to publish a message...");
                await _lock.WaitAsync(stoppingToken);
                try
                {
                    _logger.LogTrace("Entered lock to publish a message");

                    await _channel!.BasicPublishAsync(
                        _rabbitMQOptions.ExchangeName,
                        _configuration.QueueName,
                        mandatory: false,
                        responseProps,
                        responseMessageBytes,
                        stoppingToken);
                }
                finally
                {
                    _lock.Release();
                }

                await Task.Delay(_configuration.DelayBetweenSendMs, stoppingToken);
            }
            catch (RabbitMQClientException e)
            {
                // if we got this exception, probably we have some some connection issues.
                // We will try to wait auto recovering. If it fails, we will recreate the channel

                _logger.LogWarning(e,
                    "Got {ExceptionName} exception. Waiting for recovering for {RecoverWaitDelay}",
                    e.GetType(),
                    RecoverWaitDelay);

                var isRecovered = await HandleRecoverySafelyAsync(1, _cancellationTokenSource.Token);
                if (isRecovered)
                {
                    _logger.LogDebug("Connection was successfully recovered");
                }
                else
                {
                    _logger.LogError("Failed to recover connection");
                }
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                // no action
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error while sending request");
            }

            try
            {
                await Task.Delay(_configuration.DelayBetweenSendMs, stoppingToken);
            }
            catch (Exception)
            {
                // ignored
            }
        }
    }

    /// <inheritdoc />
    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug("Stopping sender...");

        _logger.LogTrace("Cancelling receiving...");
        await _cancellationTokenSource.CancelAsync();

        _logger.LogTrace("Waiting for stopping background service...");
        await base.StopAsync(cancellationToken);

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

        _logger.LogDebug("Stopping sender completed");
    }
}
