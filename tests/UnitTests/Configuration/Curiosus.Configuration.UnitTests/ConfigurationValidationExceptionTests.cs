using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Curiosus.Configuration.UnitTests;

public class ConfigurationValidationExceptionTests
{
    [Fact]
    public void Constructor_PassesMessageToBase()
    {
        var errors = new[] { new ConfigurationValidationError("Host", "can't be empty") };

        var exception = new ConfigurationValidationException("Options are invalid", errors);

        exception.Message.Should().Be("Options are invalid");
        exception.Errors.Should().BeEquivalentTo(errors);
    }

    [Fact]
    public void AssertValid_WithPrefix_MessageListsEveryError()
    {
        var options = new InvalidOptions();

        var act = () => options.AssertValid("Smtp");

        var exception = act.Should().Throw<ConfigurationValidationException>().Which;
        exception.Message.Should().StartWith("Option Smtp are invalid.")
            .And.Contain("Smtp:Host: can't be empty")
            .And.Contain("Smtp:Port: must be positive");
        exception.Errors.Should().HaveCount(2);
    }

    [Fact]
    public void AssertValid_WithoutPrefix_MessageHasNoDoubleSpace()
    {
        var options = new InvalidOptions();

        var act = () => options.AssertValid();

        act.Should().Throw<ConfigurationValidationException>()
            .Which.Message.Should().StartWith("Options are invalid.")
            .And.NotContain("  ");
    }

    private class InvalidOptions : IValidatableOptions
    {
        public IReadOnlyCollection<ConfigurationValidationError> Validate(string? prefix = null)
        {
            var errors = new ConfigurationValidationErrorCollection(prefix);
            errors.AddError("Host", "can't be empty");
            errors.AddError("Port", "must be positive");
            return errors;
        }
    }
}
