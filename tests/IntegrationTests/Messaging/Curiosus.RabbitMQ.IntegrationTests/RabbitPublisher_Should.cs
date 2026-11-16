using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.RabbitMQ.IntegrationTests.Fixture;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RabbitMQ.Client;
using Xunit;

namespace Curiosus.RabbitMQ.IntegrationTests;

[Collection(RabbitMqCollection.Name)]
public class RabbitPublisher_Should
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(90);

    private readonly RabbitMqFixture _fixture;

    public RabbitPublisher_Should(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    public record OrderPaid(long OrderId, decimal Amount);

    private ServiceProvider CreateServices(
        string clientName,
        Uri? connectionUri = null,
        TimeSpan? publishTimeout = null,
        int channelCount = 4)
    {
        var values = new Dictionary<string, string?>
        {
            ["RabbitMQ:ConnectionUri"] = (connectionUri ?? _fixture.ConnectionUri).ToString(),
            ["RabbitMQ:ClientName"] = clientName,
            ["RabbitMQ:PublisherChannelCount"] = channelCount.ToString(CultureInfo.InvariantCulture)
        };
        if (publishTimeout != null) values["RabbitMQ:PublishTimeout"] = publishTimeout.Value.ToString("c");

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(new ConfigurationBuilder().AddInMemoryCollection(values).Build());
        services.AddLogging();
        services.AddCuriosusRabbitMQ();

        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Declares a direct exchange and a queue bound to it with <paramref name="routingKey"/>.
    /// </summary>
    private async Task<(string Exchange, string Queue)> DeclareAsync(string routingKey, CancellationToken cancellationToken)
    {
        var exchange = RabbitMqFixture.UniqueName("orders");
        var queue = RabbitMqFixture.UniqueName("orders_queue");

        await using var connection = await _fixture.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(exchange, ExchangeType.Direct, cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(queue, true, false, false, cancellationToken: cancellationToken);
        await channel.QueueBindAsync(queue, exchange, routingKey, cancellationToken: cancellationToken);

        return (exchange, queue);
    }

    private async Task<BasicGetResult?> GetAsync(string queue, CancellationToken cancellationToken)
    {
        await using var connection = await _fixture.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        return await channel.BasicGetAsync(queue, autoAck: true, cancellationToken);
    }

    [Fact]
    public async Task PublishConfirmedMessage()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var (exchange, queue) = await DeclareAsync("paid", cts.Token);
        await using var services = CreateServices("publisher_confirmed");
        var publisher = services.GetRequiredService<IRabbitPublisher>();

        // act
        await publisher.PublishAsync(exchange, "paid", new OrderPaid(42, 10.5m), cancellationToken: cts.Token);

        // assert
        (await _fixture.GetReadyMessagesCountAsync(queue, cts.Token)).Should().Be(1);
        var message = await GetAsync(queue, cts.Token);
        message.Should().NotBeNull();
        message!.BasicProperties.Persistent.Should().BeTrue();
        message.BasicProperties.ContentType.Should().Be("application/json");
        Guid.TryParse(message.BasicProperties.MessageId, out var messageId).Should().BeTrue();
        messageId.Version.Should().Be(7);
        Encoding.UTF8.GetString(message.Body.Span).Should().Be("{\"OrderId\":42,\"Amount\":10.5}");
        JsonSerializer.Deserialize<OrderPaid>(message.Body.Span, RabbitMQJson.DefaultOptions)
            .Should().Be(new OrderPaid(42, 10.5m));
    }

    [Fact]
    public async Task PublishMessageProperties()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var (exchange, queue) = await DeclareAsync("paid", cts.Token);
        await using var services = CreateServices("publisher_properties");
        var publisher = services.GetRequiredService<IRabbitPublisher>();
        var headers = new Dictionary<string, object?> { ["tenant"] = "acme" };
        var startedAt = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // act
        await publisher.PublishAsync(
            exchange,
            "paid",
            new OrderPaid(1, 1m),
            new RabbitPublishProperties
            {
                MessageId = "order-1-paid",
                CorrelationId = "checkout-1",
                Persistent = false,
                Mandatory = true,
                Headers = headers
            },
            cts.Token);

        // assert
        var message = await GetAsync(queue, cts.Token);
        message.Should().NotBeNull();
        var properties = message!.BasicProperties;
        properties.MessageId.Should().Be("order-1-paid");
        properties.CorrelationId.Should().Be("checkout-1");
        properties.Persistent.Should().BeFalse();
        properties.Timestamp.UnixTime.Should().BeInRange(startedAt - 1, DateTimeOffset.UtcNow.ToUnixTimeSeconds() + 1);
        Encoding.UTF8.GetString((byte[])properties.Headers!["tenant"]!).Should().Be("acme");
        properties.Headers.Should().ContainKey(Constants.PublishSequenceNumberHeader);
        headers.Should().HaveCount(1, "the caller's headers are copied");
    }

    [Fact]
    public async Task FailWithChannelClosed_ForMissingExchange_AndReplaceChannel()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var (exchange, queue) = await DeclareAsync("paid", cts.Token);
        var clientName = RabbitMqFixture.UniqueName("publisher_missing");
        await using var services = CreateServices(clientName, channelCount: 1);
        var publisher = services.GetRequiredService<IRabbitPublisher>();
        var connection = await services.GetRequiredService<IRabbitConnectionProvider>().GetConnectionAsync(cts.Token);

        // act
        var act = () => publisher.PublishAsync(
            RabbitMqFixture.UniqueName("missing"), "paid", new OrderPaid(1, 1m), cancellationToken: cts.Token);

        // assert
        var exception = (await act.Should().ThrowAsync<RabbitPublishException>()).Which;
        exception.Reason.Should().Be(RabbitPublishFailure.ChannelClosed);
        exception.ReplyCode.Should().Be(404);
        exception.ReplyText.Should().Contain("NOT_FOUND");

        await publisher.PublishAsync(exchange, "paid", new OrderPaid(2, 2m), cancellationToken: cts.Token);
        (await _fixture.GetReadyMessagesCountAsync(queue, cts.Token)).Should().Be(1);

        // a channel closed by the broker is not re-created by a recovery that comes before the next publish
        await act.Should().ThrowAsync<RabbitPublishException>();
        var recovered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        connection.RecoverySucceededAsync += (_, _) =>
        {
            recovered.TrySetResult();
            return Task.CompletedTask;
        };
        await _fixture.CloseAllConnectionsAsync(cts.Token);
        await recovered.Task.WaitAsync(cts.Token);
        (await _fixture.GetChannelCountsAsync(clientName, cts.Token)).Should().Equal(0);

        await publisher.PublishAsync(exchange, "paid", new OrderPaid(3, 3m), cancellationToken: cts.Token);
        (await _fixture.GetChannelCountsAsync(clientName, cts.Token)).Should().Equal(1);
        (await _fixture.GetReadyMessagesCountAsync(queue, cts.Token)).Should().Be(2);
    }

    [Fact]
    public async Task FailWithReturned_ForUnroutableMandatoryMessage()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var (exchange, _) = await DeclareAsync("paid", cts.Token);
        await using var services = CreateServices("publisher_returned");
        var publisher = services.GetRequiredService<IRabbitPublisher>();

        // act
        var act = () => publisher.PublishAsync(
            exchange,
            "refunded",
            new OrderPaid(1, 1m),
            new RabbitPublishProperties { MessageId = "unroutable", Mandatory = true },
            cts.Token);

        // assert
        var exception = (await act.Should().ThrowAsync<RabbitPublishException>()).Which;
        exception.Reason.Should().Be(RabbitPublishFailure.Returned);
        exception.ReplyCode.Should().Be(312);
        exception.ReplyText.Should().Be("NO_ROUTE");
        exception.MessageId.Should().Be("unroutable");

        // not mandatory: confirmed and dropped by the broker
        await publisher.PublishAsync(exchange, "refunded", new OrderPaid(1, 1m), cancellationToken: cts.Token);
    }

    [Fact]
    public async Task FailWithTimeout_WhenBrokerDoesntAnswer()
    {
        // arrange: a broker of its own, the shared one must keep answering
        using var cts = new CancellationTokenSource(TestTimeout);
        await using var container = RabbitMqFixture.CreateContainer();
        await container.StartAsync(cts.Token);
        var publishTimeout = TimeSpan.FromSeconds(2);
        var queue = RabbitMqFixture.UniqueName("paused");

        // one channel: the publish after the pause goes to the open channel of the first one
        await using var connected = CreateServices(
            "publisher_paused", RabbitMqFixture.GetConnectionUri(container), publishTimeout, channelCount: 1);
        var connection = await connected.GetRequiredService<IRabbitConnectionProvider>().GetConnectionAsync(cts.Token);
        await using (var channel = await connection.CreateChannelAsync(cancellationToken: cts.Token))
        {
            await channel.QueueDeclareAsync(queue, true, false, false, cancellationToken: cts.Token);
        }

        var publisher = connected.GetRequiredService<IRabbitPublisher>();
        await publisher.PublishAsync("", queue, new OrderPaid(1, 1m), cancellationToken: cts.Token);
        await using var notConnected = CreateServices(
            "publisher_paused_new", RabbitMqFixture.GetConnectionUri(container), publishTimeout);

        await container.PauseAsync(cts.Token);
        try
        {
            // act + assert: no confirm on an open connection
            await AssertTimeoutAsync(() => publisher.PublishAsync("", queue, new OrderPaid(2, 2m), cancellationToken: cts.Token));

            // act + assert: no connection
            var newPublisher = notConnected.GetRequiredService<IRabbitPublisher>();
            await AssertTimeoutAsync(
                () => newPublisher.PublishAsync("", queue, new OrderPaid(3, 3m), cancellationToken: cts.Token));
        }
        finally
        {
            await container.UnpauseAsync(CancellationToken.None);
        }

        async Task AssertTimeoutAsync(Func<Task> publish)
        {
            var stopwatch = Stopwatch.StartNew();
            var exception = (await publish.Should().ThrowAsync<RabbitPublishException>()).Which;
            exception.Reason.Should().Be(RabbitPublishFailure.Timeout);
            stopwatch.Elapsed.Should().BeGreaterThan(publishTimeout - TimeSpan.FromMilliseconds(100))
                .And.BeLessThan(publishTimeout + TimeSpan.FromSeconds(2));
        }
    }

    [Fact]
    public async Task PublishDuringAndAfterRecovery()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var (exchange, queue) = await DeclareAsync("paid", cts.Token);
        var clientName = RabbitMqFixture.UniqueName("publisher_recovery");
        await using var services = CreateServices(clientName);
        var publisher = services.GetRequiredService<IRabbitPublisher>();
        var connection = await services.GetRequiredService<IRabbitConnectionProvider>().GetConnectionAsync(cts.Token);
        await publisher.PublishAsync(exchange, "paid", new OrderPaid(0, 0m), cancellationToken: cts.Token);

        // act
        await _fixture.CloseAllConnectionsAsync(cts.Token);
        await WaitUntilAsync(() => Task.FromResult(!connection.IsOpen), cts.Token);

        var duringRecovery = Enumerable.Range(1, 10)
            .Select(i => publisher.PublishAsync(exchange, "paid", new OrderPaid(i, i), cancellationToken: cts.Token))
            .ToArray();
        await Task.WhenAll(duringRecovery);
        connection.IsOpen.Should().BeTrue();
        await publisher.PublishAsync(exchange, "paid", new OrderPaid(11, 11m), cancellationToken: cts.Token);

        // assert
        (await _fixture.GetReadyMessagesCountAsync(queue, cts.Token)).Should().Be(12);
        (await _fixture.GetChannelCountsAsync(clientName, cts.Token)).Should().ContainSingle();
    }

    [Fact]
    public async Task CancelOnePublish_WhileOthersWaitForRecovery()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var (exchange, queue) = await DeclareAsync("paid", cts.Token);
        await using var services = CreateServices(RabbitMqFixture.UniqueName("publisher_cancel"));
        var publisher = services.GetRequiredService<IRabbitPublisher>();
        var connection = await services.GetRequiredService<IRabbitConnectionProvider>().GetConnectionAsync(cts.Token);
        await publisher.PublishAsync(exchange, "paid", new OrderPaid(0, 0m), cancellationToken: cts.Token);

        await _fixture.CloseAllConnectionsAsync(cts.Token);
        await WaitUntilAsync(() => Task.FromResult(!connection.IsOpen), cts.Token);

        // act
        using var callerCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
        var cancelled = publisher.PublishAsync(exchange, "paid", new OrderPaid(1, 1m), cancellationToken: callerCts.Token);
        var waiting = Enumerable.Range(2, 3)
            .Select(i => publisher.PublishAsync(exchange, "paid", new OrderPaid(i, i), cancellationToken: cts.Token))
            .ToArray();
        await callerCts.CancelAsync();

        // assert
        await FluentActions.Awaiting(() => cancelled).Should().ThrowAsync<OperationCanceledException>();
        connection.IsOpen.Should().BeFalse("the cancelled publish didn't wait for the recovery");
        await Task.WhenAll(waiting);
        (await _fixture.GetReadyMessagesCountAsync(queue, cts.Token)).Should().Be(4);
    }

    [Fact]
    public async Task UseOneConnection_WithChannelCountAtMost()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var (exchange, queue) = await DeclareAsync("paid", cts.Token);
        var clientName = RabbitMqFixture.UniqueName("publisher_threads");
        await using var services = CreateServices(clientName, channelCount: 3);
        var publisher = services.GetRequiredService<IRabbitPublisher>();

        // act
        var threads = Enumerable.Range(0, 8)
            .Select(thread => Task.Run(async () =>
            {
                for (var i = 0; i < 25; i++)
                {
                    await publisher.PublishAsync(
                        exchange, "paid", new OrderPaid(thread * 100 + i, i), cancellationToken: cts.Token);
                }
            }))
            .ToArray();
        await Task.WhenAll(threads);

        // assert
        var channelCounts = await _fixture.GetChannelCountsAsync(clientName, cts.Token);
        channelCounts.Should().ContainSingle().Which.Should().BeInRange(1, 3);
        (await _fixture.GetReadyMessagesCountAsync(queue, cts.Token)).Should().Be(200);
    }

    [Fact]
    public async Task CloseConnection_WhenServicesAreDisposed()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var (exchange, _) = await DeclareAsync("paid", cts.Token);
        var clientName = RabbitMqFixture.UniqueName("publisher_disposed");
        var services = CreateServices(clientName);
        var publisher = services.GetRequiredService<IRabbitPublisher>();
        await publisher.PublishAsync(exchange, "paid", new OrderPaid(1, 1m), cancellationToken: cts.Token);
        (await _fixture.GetChannelCountsAsync(clientName, cts.Token)).Should().ContainSingle();

        // act
        await services.DisposeAsync();

        // assert
        await WaitUntilAsync(async () => (await _fixture.GetChannelCountsAsync(clientName, cts.Token)).Count == 0, cts.Token);
        await FluentActions.Awaiting(() => publisher.PublishAsync(exchange, "paid", new OrderPaid(2, 2m)))
            .Should().ThrowAsync<ObjectDisposedException>();
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, CancellationToken cancellationToken)
    {
        while (!await condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken);
        }
    }
}
