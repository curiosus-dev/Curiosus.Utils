using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Curiosus.RequestProcessing.RabbitMQ.Options;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Xunit;

namespace Curiosus.RequestProcessing.RabbitMQ.UnitTests;

public class RabbitMQEvent_Should
{
    private const ulong DeliveryTag = 42;

    private readonly List<ulong> _confirmed = new();
    private readonly List<ulong> _rejected = new();

    private RabbitMQEvent CreateEvent(byte[] body, string correlationId = "correlation-id")
    {
        var args = new BasicDeliverEventArgs(
            "consumer-tag",
            DeliveryTag,
            false,
            "",
            "requests",
            new BasicProperties { CorrelationId = correlationId },
            body);

        return new RabbitMQEvent(args, _confirmed.Add, _rejected.Add);
    }

    [Fact]
    public void CopyPayload_BecauseBodyIsValidOnlyInsideHandler()
    {
        // arrange
        var body = new byte[] { 1, 2, 3 };

        // act
        var rabbitMQEvent = CreateEvent(body);
        body[0] = 42;

        // assert
        rabbitMQEvent.Payload.Should().Equal(1, 2, 3);
    }

    [Fact]
    public void ExposeDeliveryData()
    {
        // act
        var rabbitMQEvent = CreateEvent([1], "abc");

        // assert
        rabbitMQEvent.ReceivedData.DeliveryTag.Should().Be(DeliveryTag);
        rabbitMQEvent.ReceivedData.BasicProperties.CorrelationId.Should().Be("abc");
    }

    [Fact]
    public void ConfirmProcessing_Once()
    {
        // arrange
        var rabbitMQEvent = CreateEvent([1]);

        // act
        rabbitMQEvent.ConfirmProcessing();
        rabbitMQEvent.ConfirmProcessing();
        rabbitMQEvent.RejectProcessing();

        // assert
        _confirmed.Should().Equal(DeliveryTag);
        _rejected.Should().BeEmpty();
    }

    [Fact]
    public void RejectProcessing_Once()
    {
        // arrange
        var rabbitMQEvent = CreateEvent([1]);

        // act
        rabbitMQEvent.RejectProcessing();
        rabbitMQEvent.RejectProcessing();
        rabbitMQEvent.ConfirmProcessing();

        // assert
        _rejected.Should().Equal(DeliveryTag);
        _confirmed.Should().BeEmpty();
    }

    [Fact]
    public void ProvideCorrelationIdToRequestWrapper()
    {
        // arrange
        var rabbitMQEvent = CreateEvent([1], "abc");

        // act
        var wrapper = new RabbitMQRequestWrapper<string>(1, CultureInfo.InvariantCulture, "request", rabbitMQEvent);
        wrapper.ConfirmProcessing();

        // assert
        wrapper.CorrelationId.Should().Be("abc");
        _confirmed.Should().Equal(DeliveryTag);
    }

    [Fact]
    public async Task EventReceiver_DisposeAsync_WhenNotStarted()
    {
        // arrange
        var options = new RabbitMQEventReceiverOptions
        {
            UserName = "guest",
            Password = "guest",
            ClientName = "tests",
            QueueName = "requests"
        };
        var receiver = new RabbitMQEventReceiver(options, NullLogger.Instance, 1);

        // act
        var act = async () => await receiver.DisposeAsync();

        // assert
        await act.Should().NotThrowAsync();
    }
}
