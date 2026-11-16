using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Curiosus.RabbitMQ;

/// <summary>
/// Opens the connection of <see cref="RabbitMQOptions"/> lazily and closes it on dispose.
/// </summary>
internal sealed class RabbitConnectionProvider : IRabbitConnectionProvider, IAsyncDisposable, IDisposable
{
    private static readonly TimeSpan CloseTimeout = TimeSpan.FromSeconds(5);

    private readonly ConnectionFactory _connectionFactory;
    private readonly string _clientName;
    private readonly ILogger _logger;

    private readonly object _sync = new();
    private readonly CancellationTokenSource _disposeCts = new();

    private IConnection? _connection;
    private Task<IConnection>? _openTask;
    private bool _disposed;

    public RabbitConnectionProvider(IOptions<RabbitMQOptions> options, ILogger<RabbitConnectionProvider> logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(logger);

        var value = options.Value;
        _clientName = value.ClientName;
        _connectionFactory = RabbitConnectionFactoryBuilder.Create(value.ConnectionUri, value.ClientName);
        _logger = logger;
    }

    /// <summary>
    /// The factory the connection is opened with, for tests.
    /// </summary>
    internal ConnectionFactory ConnectionFactory => _connectionFactory;

    public ValueTask<IConnection> GetConnectionAsync(CancellationToken cancellationToken = default)
    {
        var connection = Volatile.Read(ref _connection);
        if (connection != null && !Volatile.Read(ref _disposed)) return ValueTask.FromResult(connection);

        return new ValueTask<IConnection>(GetOrOpenConnectionAsync(cancellationToken));
    }

    private Task<IConnection> GetOrOpenConnectionAsync(CancellationToken cancellationToken)
    {
        Task<IConnection> openTask;
        lock (_sync)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (_connection != null) return Task.FromResult(_connection);

            // a failed attempt is not reused: the next caller tries again
            if (_openTask == null || _openTask.IsFaulted || _openTask.IsCanceled)
            {
                // not the caller's token: other callers wait for the same attempt
                _openTask = Task.Run(() => OpenAsync(_disposeCts.Token), CancellationToken.None);
            }

            openTask = _openTask;
        }

        return openTask.WaitAsync(cancellationToken);
    }

    private async Task<IConnection> OpenAsync(CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "Opening RabbitMQ connection \"{ClientName}\" to {Host}:{Port}, virtual host \"{VirtualHost}\"...",
            _clientName,
            _connectionFactory.HostName,
            _connectionFactory.Port,
            _connectionFactory.VirtualHost);

        IConnection connection;
        try
        {
            connection = await _connectionFactory.CreateConnectionAsync(cancellationToken);
        }
        catch (Exception e) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(
                e,
                "Failed to open RabbitMQ connection \"{ClientName}\" to {Host}:{Port}, virtual host \"{VirtualHost}\"",
                _clientName,
                _connectionFactory.HostName,
                _connectionFactory.Port,
                _connectionFactory.VirtualHost);
            throw;
        }

        connection.ConnectionShutdownAsync += OnConnectionShutdownAsync;
        connection.ConnectionBlockedAsync += OnConnectionBlockedAsync;
        connection.ConnectionUnblockedAsync += OnConnectionUnblockedAsync;
        connection.RecoverySucceededAsync += OnRecoverySucceededAsync;
        connection.ConnectionRecoveryErrorAsync += OnConnectionRecoveryErrorAsync;

        lock (_sync)
        {
            if (!_disposed)
            {
                Volatile.Write(ref _connection, connection);
            }
        }

        if (Volatile.Read(ref _connection) != connection)
        {
            // disposed while opening
            await CloseConnectionAsync(connection);
            throw new ObjectDisposedException(GetType().FullName);
        }

        _logger.LogInformation(
            "Opened RabbitMQ connection \"{ClientName}\" to {Host}:{Port}, virtual host \"{VirtualHost}\"",
            _clientName,
            connection.Endpoint.HostName,
            connection.Endpoint.Port,
            _connectionFactory.VirtualHost);

        return connection;
    }

    private Task OnConnectionShutdownAsync(object sender, ShutdownEventArgs e)
    {
        var level = e.Initiator == ShutdownInitiator.Application ? LogLevel.Information : LogLevel.Warning;
        _logger.Log(
            level,
            e.Exception,
            "RabbitMQ connection \"{ClientName}\" was shut down by {Initiator}: {ReplyCode} {ReplyText}",
            _clientName,
            e.Initiator,
            e.ReplyCode,
            e.ReplyText);

        return Task.CompletedTask;
    }

    private Task OnConnectionBlockedAsync(object sender, ConnectionBlockedEventArgs e)
    {
        _logger.LogWarning(
            "RabbitMQ connection \"{ClientName}\" is blocked by the broker: {Reason}. Publishes wait until it is unblocked",
            _clientName,
            e.Reason);

        return Task.CompletedTask;
    }

    private Task OnConnectionUnblockedAsync(object sender, AsyncEventArgs e)
    {
        _logger.LogInformation("RabbitMQ connection \"{ClientName}\" is unblocked", _clientName);

        return Task.CompletedTask;
    }

    private Task OnRecoverySucceededAsync(object sender, AsyncEventArgs e)
    {
        _logger.LogInformation("RabbitMQ connection \"{ClientName}\" was recovered", _clientName);

        return Task.CompletedTask;
    }

    private Task OnConnectionRecoveryErrorAsync(object sender, ConnectionRecoveryErrorEventArgs e)
    {
        _logger.LogWarning(
            e.Exception,
            "Failed to recover RabbitMQ connection \"{ClientName}\", the next attempt follows",
            _clientName);

        return Task.CompletedTask;
    }

    private async Task CloseConnectionAsync(IConnection connection)
    {
        try
        {
            if (connection.IsOpen)
            {
                await connection.CloseAsync(CloseTimeout);
            }
        }
        catch (Exception e)
        {
            _logger.LogWarning(e, "Failed to close RabbitMQ connection \"{ClientName}\"", _clientName);
        }

        await connection.DisposeAsync();
    }

    public async ValueTask DisposeAsync()
    {
        IConnection? connection;
        lock (_sync)
        {
            if (_disposed) return;

            _disposed = true;
            connection = _connection;
        }

        await _disposeCts.CancelAsync();

        if (connection != null)
        {
            await CloseConnectionAsync(connection);
        }
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
