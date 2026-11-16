using System.Globalization;
using Curiosus.RabbitMQ;
using Curiosus.RequestProcessing.RabbitMQ.Sample.Common;
using Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Configuration;
using Curiosus.Tools;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Curiosus.RequestProcessing.RabbitMQ.Sample.ProducerApp.Core;

/// <summary>
/// Sample to send sample request to RabbitMQ.
/// </summary>
public class SampleRequestsProducer : BackgroundService
{
    private readonly IRabbitConnectionProvider _connectionProvider;
    private readonly IRabbitPublisher _publisher;
    private readonly ILogger _logger;
    private readonly SampleProducerAppConfiguration _configuration;

    public SampleRequestsProducer(
        IRabbitConnectionProvider connectionProvider,
        IRabbitPublisher publisher,
        SampleProducerAppConfiguration configuration,
        ILogger<SampleRequestsProducer> logger)
    {
        _connectionProvider = connectionProvider;
        _publisher = publisher;
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        UniqueIdGenerator.Initialize(GetHashCode() % 1024);

        // the publisher doesn't declare anything: declare the queue once at startup
        var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
        await using (var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken))
        {
            await channel.QueueDeclareAsync(
                _configuration.QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: cancellationToken);
        }

        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var newRequest = new SampleRequest(
                UniqueIdGenerator.Generate(),
                CultureInfo.CurrentCulture,
                "sample data");

            try
            {
                // the default exchange routes by queue name
                await _publisher.PublishAsync(
                    "",
                    _configuration.QueueName,
                    newRequest,
                    new RabbitPublishProperties { CorrelationId = newRequest.Id.ToString() },
                    stoppingToken);
            }
            catch (RabbitPublishException e)
            {
                _logger.LogWarning(e, "Failed to publish request {RequestId}: {Reason}", newRequest.Id, e.Reason);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }

            try
            {
                await Task.Delay(_configuration.DelayBetweenSendMs, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
