using System;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.RabbitMQ.IntegrationTests.Fixture;
using Curiosus.RabbitMQ.RPC;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Curiosus.RabbitMQ.IntegrationTests;

[Collection(RabbitMqCollection.Name)]
public class RabbitMqRpcClient_Should
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(60);

    private readonly RabbitMqFixture _fixture;

    public RabbitMqRpcClient_Should(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    public record EchoRequest(string Value);

    public record EchoResponse(string Value);

    private RabbitMqRpcClientFactory CreateFactory()
    {
        var options = new RabbitMQOptions
        {
            HostName = _fixture.HostName,
            Port = _fixture.Port,
            UserName = RabbitMqFixture.UserName,
            Password = RabbitMqFixture.Password,
            ClientName = "rpc_tests"
        };

        return new RabbitMqRpcClientFactory(NullLoggerFactory.Instance, options);
    }

    /// <summary>
    /// Starts a service that replies to each request with its value and "!" appended.
    /// </summary>
    private async Task<IAsyncDisposable> StartEchoServiceAsync(string requestQueueName, CancellationToken cancellationToken)
    {
        var connection = await _fixture.CreateConnectionAsync(cancellationToken);
        var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.QueueDeclareAsync(requestQueueName, true, false, false, cancellationToken: cancellationToken);

        var consumer = new AsyncEventingBasicConsumer(channel);
        consumer.ReceivedAsync += async (_, e) =>
        {
            var request = JsonSerializer.Deserialize<EchoRequest>(e.Body.Span, RabbitMqRpcClient.DefaultJsonSerializerOptions)!;
            var response = JsonSerializer.SerializeToUtf8Bytes(
                new EchoResponse(request.Value + "!"),
                RabbitMqRpcClient.DefaultJsonSerializerOptions);
            var props = new BasicProperties { CorrelationId = e.BasicProperties.CorrelationId };

            await channel.BasicPublishAsync("", e.BasicProperties.ReplyTo!, false, props, response, e.CancellationToken);
            await channel.BasicAckAsync(e.DeliveryTag, false, e.CancellationToken);
        };
        await channel.BasicConsumeAsync(requestQueueName, false, consumer, cancellationToken);

        return new AsyncDisposables(channel, connection);
    }

    [Fact]
    public async Task SendWithAutoAcknowledgeAsync_ReturnResponse()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var requestQueueName = RabbitMqFixture.UniqueName("rpc_auto");
        await using var service = await StartEchoServiceAsync(requestQueueName, cts.Token);
        await using var client = await CreateFactory().CreateClientAsync(requestQueueName, cancellationToken: cts.Token);

        // act
        var first = await client.SendWithAutoAcknowledgeAsync<EchoResponse, EchoRequest>(
            new EchoRequest("hello"), "correlation_1", cts.Token);
        var second = await client.SendWithAutoAcknowledgeAsync<EchoResponse, EchoRequest>(
            new EchoRequest("world"), "correlation_2", cts.Token);

        // assert
        first.Value.Should().Be("hello!");
        second.Value.Should().Be("world!");
        (await client.GetConsumersCountAsync(cts.Token)).Should().Be(1);
    }

    [Fact]
    public async Task SendWithManualAcknowledgeAsync_AckResponse_OnlyAfterConfirmation()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var requestQueueName = RabbitMqFixture.UniqueName("rpc_manual");
        await using var service = await StartEchoServiceAsync(requestQueueName, cts.Token);
        var factory = CreateFactory();

        // act
        var confirmedClient = await factory.CreateClientAsync(
            requestQueueName, "confirmed", disposeResponseQueue: false, cancellationToken: cts.Token);
        var confirmed = await confirmedClient.SendWithManualAcknowledgeAsync<EchoResponse, EchoRequest>(
            new EchoRequest("confirmed"), "correlation_confirmed", cts.Token);
        await confirmed.ConfirmAcknowledgeAsync(cts.Token);
        await confirmedClient.DisposeAsync();

        var unconfirmedClient = await factory.CreateClientAsync(
            requestQueueName, "unconfirmed", disposeResponseQueue: false, cancellationToken: cts.Token);
        var unconfirmed = await unconfirmedClient.SendWithManualAcknowledgeAsync<EchoResponse, EchoRequest>(
            new EchoRequest("unconfirmed"), "correlation_unconfirmed", cts.Token);
        await unconfirmedClient.DisposeAsync();

        // assert
        confirmed.Data.Value.Should().Be("confirmed!");
        unconfirmed.Data.Value.Should().Be("unconfirmed!");

        var responseQueuePrefix = $"{requestQueueName}_responses_rpc_tests";
        (await _fixture.GetReadyMessagesCountAsync($"{responseQueuePrefix}_confirmed", cts.Token)).Should().Be(0);
        (await _fixture.GetReadyMessagesCountAsync($"{responseQueuePrefix}_unconfirmed", cts.Token)).Should().Be(1);
    }

    [Fact]
    public async Task RejectResponse_WithUnknownCorrelationId()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var requestQueueName = RabbitMqFixture.UniqueName("rpc_unknown");
        var client = await CreateFactory().CreateClientAsync(
            requestQueueName, disposeResponseQueue: false, cancellationToken: cts.Token);
        var responseQueueName = $"{requestQueueName}_responses_rpc_tests";

        // act
        await using (var connection = await _fixture.CreateConnectionAsync(cts.Token))
        await using (var channel = await connection.CreateChannelAsync(cancellationToken: cts.Token))
        {
            var props = new BasicProperties { CorrelationId = "unknown" };
            await channel.BasicPublishAsync("", responseQueueName, false, props, Encoding.UTF8.GetBytes("{}"), cts.Token);
        }

        await WaitUntilAsync(
            async () => await _fixture.GetReadyMessagesCountAsync(responseQueueName, cts.Token) == 0,
            cts.Token);
        (await client.GetConsumersCountAsync(cts.Token)).Should().Be(0);
        await client.DisposeAsync();

        // assert: an unacked response would be requeued after closing the channel, a rejected one is dropped
        (await _fixture.GetReadyMessagesCountAsync(responseQueueName, cts.Token)).Should().Be(0);
    }

    [Fact]
    public async Task RecoverConnection_ClosedByBroker()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var requestQueueName = RabbitMqFixture.UniqueName("rpc_recovery");
        await using var client = await CreateFactory().CreateClientAsync(requestQueueName, cancellationToken: cts.Token);
        await using (var service = await StartEchoServiceAsync(requestQueueName, cts.Token))
        {
            await client.SendWithAutoAcknowledgeAsync<EchoResponse, EchoRequest>(new EchoRequest("before"), "before", cts.Token);
        }

        // act
        await _fixture.CloseAllConnectionsAsync(cts.Token);
        await using var restartedService = await StartEchoServiceAsync(requestQueueName, cts.Token);

        EchoResponse? response = null;
        var attempt = 0;
        while (response == null)
        {
            using var attemptCts = CancellationTokenSource.CreateLinkedTokenSource(cts.Token);
            attemptCts.CancelAfter(TimeSpan.FromSeconds(5));
            try
            {
                response = await client.SendWithAutoAcknowledgeAsync<EchoResponse, EchoRequest>(
                    new EchoRequest("after"), $"after_{++attempt}", attemptCts.Token);
            }
            catch (OperationCanceledException) when (!cts.IsCancellationRequested)
            {
                // the client is still recovering
            }
        }

        // assert
        response.Value.Should().Be("after!");
    }

    private static async Task WaitUntilAsync(Func<Task<bool>> condition, CancellationToken cancellationToken)
    {
        while (!await condition())
        {
            await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken);
        }
    }

    private sealed class AsyncDisposables : IAsyncDisposable
    {
        private readonly IAsyncDisposable[] _disposables;

        public AsyncDisposables(params IAsyncDisposable[] disposables)
        {
            _disposables = disposables;
        }

        public async ValueTask DisposeAsync()
        {
            foreach (var disposable in _disposables)
            {
                await disposable.DisposeAsync();
            }
        }
    }
}
