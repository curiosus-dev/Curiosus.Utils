using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Curiosus.RabbitMQ.RPC;

/// <summary>
/// Factory to creates new instance of <see cref="RabbitMqRpcClient"/>.
/// </summary>
/// <remarks>
/// Each client opens its own connection to <see cref="RabbitMQOptions.ConnectionUri"/>, named
/// <c>{ClientName}_rpc_client</c>; it doesn't use <see cref="IRabbitConnectionProvider"/>.
/// </remarks>
public class RabbitMqRpcClientFactory
{
    private static readonly TimeSpan NetworkRecoveryInterval = TimeSpan.FromSeconds(10);

    private readonly ConnectionFactory _connectionFactory;

    private readonly ILoggerFactory _loggerFactory;
    private readonly RabbitMQOptions _options;
    private readonly ILogger _logger;

    /// <inheritdoc cref="RabbitMqRpcClientFactory"/>
    public RabbitMqRpcClientFactory(
        ILoggerFactory loggerFactory,
        IOptions<RabbitMQOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);

        _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
        _options = options.Value;

        _logger = loggerFactory.CreateLogger<RabbitMqRpcClientFactory>();

        _connectionFactory = RabbitConnectionFactoryBuilder.Create(_options.ConnectionUri, $"{_options.ClientName}_rpc_client");
        _connectionFactory.NetworkRecoveryInterval = NetworkRecoveryInterval;
    }

    /// <summary>
    /// Creates new RPC client, connects it to RabbitMQ and declares request and response queues.
    /// </summary>
    /// <remarks>
    /// Dispose the client with <see cref="RabbitMqRpcClient.DisposeAsync"/> to close its connection.
    /// </remarks>
    /// <param name="requestQueueName">Queue name.</param>
    /// <param name="clientNameSuffix">Extra for client name.</param>
    /// <param name="disposeResponseQueue">Should response queue be deleted after client disposing?</param>
    /// <param name="jsonSerializerOptions">
    /// JSON options for requests and responses. <see cref="RabbitMqRpcClient.DefaultJsonSerializerOptions"/> is used if
    /// not specified.
    /// </param>
    /// <param name="requestExchange">
    /// Exchange the requests are published to with the request queue name as the routing key; the default exchange
    /// <c>""</c>, which routes to the queue of that name, if not specified.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    public async Task<RabbitMqRpcClient> CreateClientAsync(
        string requestQueueName,
        string? clientNameSuffix = null,
        bool disposeResponseQueue = true,
        JsonSerializerOptions? jsonSerializerOptions = null,
        string requestExchange = "",
        CancellationToken cancellationToken = default)
    {
        if (String.IsNullOrWhiteSpace(requestQueueName)) throw new ArgumentNullException(nameof(requestQueueName));
        ArgumentNullException.ThrowIfNull(requestExchange);

        var responseQueueName = $"{requestQueueName}_responses_{_options.ClientName}";
        var loggerName = $"RabbitMqRpcClient_{requestQueueName}";
        var clientName = _options.ClientName;
        if (!String.IsNullOrWhiteSpace(clientNameSuffix))
        {
            responseQueueName = $"{responseQueueName}_{clientNameSuffix}";
            loggerName = $"{loggerName}_{clientNameSuffix}";
            clientName = $"{clientName}_{clientNameSuffix}";
        }

        var logger = _loggerFactory.CreateLogger(loggerName);

        var client = new RabbitMqRpcClient(
            clientName,
            requestExchange,
            requestQueueName,
            responseQueueName,
            logger,
            NetworkRecoveryInterval,
            ct => _connectionFactory.CreateConnectionAsync(ct),
            disposeResponseQueue,
            jsonSerializerOptions);

        await client.InitAsync(cancellationToken);

        _logger.LogDebug(
            "Created new RPC client and made it connected to RabbitMQ (host \"{RabbitMqHostName}\", queue = \"{RequestQueueName}\", response queue = \"{ResponseQueueName}\")",
            _connectionFactory.HostName,
            requestQueueName,
            responseQueueName);

        return client;
    }
}
