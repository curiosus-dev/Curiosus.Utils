using System;
using Curiosus.RequestProcessing.RabbitMQ.Options;
using FluentAssertions;
using Xunit;

namespace Curiosus.RequestProcessing.RabbitMQ.UnitTests;

public class RabbitMQEventReceiverOptions_Should
{
    [Fact]
    public void BeValid_WithoutClientName()
    {
        // arrange
        var options = new RabbitMQEventReceiverOptions
        {
            UserName = "guest",
            Password = "guest",
            QueueName = "requests"
        };

        // act
        var errors = options.Validate();

        // assert
        errors.Should().BeEmpty();
        options.ClientName.Should().Be(Environment.MachineName);
    }
}
