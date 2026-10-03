using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Curiosus.RabbitMQ.IntegrationTests.Fixture;
using Curiosus.RequestProcessing.RabbitMQ;
using Curiosus.RequestProcessing.RabbitMQ.Options;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using Xunit;

namespace Curiosus.RabbitMQ.IntegrationTests;

[Collection(RabbitMqCollection.Name)]
public class RabbitMQEventReceiver_Should
{
    private static readonly TimeSpan TestTimeout = TimeSpan.FromSeconds(60);

    private readonly RabbitMqFixture _fixture;

    public RabbitMQEventReceiver_Should(RabbitMqFixture fixture)
    {
        _fixture = fixture;
    }

    private RabbitMQEventReceiver CreateReceiver(string queueName, int qos, Channel<RabbitMQEvent> receivedEvents)
    {
        var options = new RabbitMQEventReceiverOptions
        {
            HostName = _fixture.HostName,
            Port = _fixture.Port,
            UserName = RabbitMqFixture.UserName,
            Password = RabbitMqFixture.Password,
            ClientName = "event_receiver_tests",
            QueueName = queueName
        };

        var receiver = new RabbitMQEventReceiver(options, NullLogger.Instance, qos);
        receiver.OnEventReceived += (_, e) => receivedEvents.Writer.TryWrite((RabbitMQEvent)e);

        return receiver;
    }

    private async Task PublishAsync(string queueName, IEnumerable<string> messages, CancellationToken cancellationToken)
    {
        await using var connection = await _fixture.CreateConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        foreach (var message in messages)
        {
            var props = new BasicProperties { CorrelationId = message };
            await channel.BasicPublishAsync("", queueName, false, props, Encoding.UTF8.GetBytes(message), cancellationToken);
        }
    }

    [Fact]
    public async Task AckConfirmed_DropRejected_AndRequeueUnprocessedEvents()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var queueName = RabbitMqFixture.UniqueName("events");
        var receivedEvents = Channel.CreateUnbounded<RabbitMQEvent>();
        await using var receiver = CreateReceiver(queueName, 10, receivedEvents);
        await receiver.StartAsync(cts.Token);

        // act
        await PublishAsync(queueName, ["confirm", "reject", "unprocessed"], cts.Token);

        var events = new Dictionary<string, RabbitMQEvent>();
        for (var i = 0; i < 3; i++)
        {
            var e = await receivedEvents.Reader.ReadAsync(cts.Token);
            events[Encoding.UTF8.GetString(e.Payload)] = e;
        }

        events["confirm"].ConfirmProcessing();
        events["reject"].RejectProcessing();
        await receiver.StopAsync(cts.Token);

        // assert
        events["confirm"].ReceivedData.BasicProperties.CorrelationId.Should().Be("confirm");
        (await _fixture.GetReadyMessagesCountAsync(queueName, cts.Token)).Should().Be(1);
    }

    [Fact]
    public async Task LimitUnprocessedEvents_ByQos()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var queueName = RabbitMqFixture.UniqueName("events_qos");
        var receivedEvents = Channel.CreateUnbounded<RabbitMQEvent>();
        await using var receiver = CreateReceiver(queueName, 2, receivedEvents);
        await receiver.StartAsync(cts.Token);

        // act
        await PublishAsync(queueName, ["1", "2", "3", "4", "5"], cts.Token);
        await Task.Delay(TimeSpan.FromSeconds(1), cts.Token);
        var receivedBeforeConfirmation = receivedEvents.Reader.Count;

        (await receivedEvents.Reader.ReadAsync(cts.Token)).ConfirmProcessing();
        await receivedEvents.Reader.ReadAsync(cts.Token);
        var third = await receivedEvents.Reader.ReadAsync(cts.Token);

        await receiver.StopAsync(cts.Token);

        // assert
        receivedBeforeConfirmation.Should().Be(2);
        Encoding.UTF8.GetString(third.Payload).Should().Be("3");
    }

    [Fact]
    public async Task RecoverConnection_ClosedByBroker()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var queueName = RabbitMqFixture.UniqueName("events_recovery");
        var receivedEvents = Channel.CreateUnbounded<RabbitMQEvent>();
        await using var receiver = CreateReceiver(queueName, 10, receivedEvents);
        await receiver.StartAsync(cts.Token);

        // act
        await _fixture.CloseAllConnectionsAsync(cts.Token);
        await PublishAsync(queueName, ["after recovery"], cts.Token);
        var received = await receivedEvents.Reader.ReadAsync(cts.Token);
        received.ConfirmProcessing();
        await receiver.StopAsync(cts.Token);

        // assert
        Encoding.UTF8.GetString(received.Payload).Should().Be("after recovery");
        (await _fixture.GetReadyMessagesCountAsync(queueName, cts.Token)).Should().Be(0);
    }

    [Fact]
    public async Task ThrowOnStart_WhenBrokerIsUnreachable()
    {
        // arrange
        using var cts = new CancellationTokenSource(TestTimeout);
        var options = new RabbitMQEventReceiverOptions
        {
            HostName = _fixture.HostName,
            Port = 1,
            UserName = RabbitMqFixture.UserName,
            Password = RabbitMqFixture.Password,
            ClientName = "event_receiver_tests",
            QueueName = "unreachable"
        };
        await using var receiver = new RabbitMQEventReceiver(options, NullLogger.Instance, 1);

        // act
        var act = () => receiver.StartAsync(cts.Token);

        // assert
        await act.Should().ThrowAsync<Exception>();
    }
}
