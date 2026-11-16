using System;
using Curiosus.RequestProcessing.RabbitMQ.Options;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.RequestProcessing.RabbitMQ.UnitTests;

public class RabbitMQEventReceiverOptions_Should
{
    [Fact]
    public void BeValid_WithoutClientName()
    {
        // arrange
        var options = ValidOptions();

        // act
        var result = Validate(options);

        // assert
        result.Succeeded.Should().BeTrue();
        options.ClientName.Should().Be(Environment.MachineName);
    }

    [Theory]
    [InlineData(nameof(RabbitMQEventReceiverOptions.HostName), "")]
    [InlineData(nameof(RabbitMQEventReceiverOptions.UserName), null)]
    [InlineData(nameof(RabbitMQEventReceiverOptions.Password), "")]
    [InlineData(nameof(RabbitMQEventReceiverOptions.ClientName), " ")]
    [InlineData(nameof(RabbitMQEventReceiverOptions.QueueName), null)]
    [InlineData(nameof(RabbitMQEventReceiverOptions.Port), 0)]
    [InlineData(nameof(RabbitMQEventReceiverOptions.Port), 65536)]
    public void BeInvalid_WithInvalidValue(string property, object? value)
    {
        // arrange
        var options = ValidOptions();
        typeof(RabbitMQEventReceiverOptions).GetProperty(property)!.SetValue(options, value);

        // act
        var result = Validate(options);

        // assert
        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(property);
    }

    [Theory]
    [InlineData("0.99", false)]
    [InlineData("1", true)]
    [InlineData("19.99", true)]
    [InlineData("20", false)]
    public void ValidateQosMultiplier_From1To20Exclusive(string multiplier, bool isValid)
    {
        // arrange
        var options = ValidOptions();
        options.QosMultiplier = Decimal.Parse(multiplier, System.Globalization.CultureInfo.InvariantCulture);

        // act
        var result = Validate(options);

        // assert
        result.Succeeded.Should().Be(isValid);
    }

    private static RabbitMQEventReceiverOptions ValidOptions() => new()
    {
        UserName = "guest",
        Password = "guest",
        QueueName = "requests"
    };

    private static ValidateOptionsResult Validate(RabbitMQEventReceiverOptions options)
        => new DataAnnotationValidateOptions<RabbitMQEventReceiverOptions>(Microsoft.Extensions.Options.Options.DefaultName)
            .Validate(Microsoft.Extensions.Options.Options.DefaultName, options);
}
