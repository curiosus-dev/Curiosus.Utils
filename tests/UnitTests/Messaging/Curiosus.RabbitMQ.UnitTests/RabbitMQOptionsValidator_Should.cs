using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.RabbitMQ.UnitTests;

public class RabbitMQOptionsValidator_Should
{
    private const string Password = "s3cr3t-p4ss";

    private static RabbitMQOptions ValidOptions() => new()
    {
        ConnectionUri = new Uri($"amqp://app:{Password}@rabbit.local:5672/prod"),
        ClientName = "orders-api-1"
    };

    private static ValidateOptionsResult Validate(RabbitMQOptions options)
        => new RabbitMQOptionsValidator().Validate(Options.DefaultName, options);

    private static ValidateOptionsResult ValidateAttributes(RabbitMQOptions options)
        => new DataAnnotationValidateOptions<RabbitMQOptions>(Options.DefaultName).Validate(Options.DefaultName, options);

    [Theory]
    [InlineData("amqp://app:pass@rabbit.local:5672/prod")]
    [InlineData("amqps://app:pass@rabbit.local/prod")]
    [InlineData("AMQP://rabbit.local")]
    [InlineData("amqp://app:p%3Ass@rabbit.local/%2F")]
    public void Accept_ValidUri(string uri)
    {
        var options = ValidOptions();
        options.ConnectionUri = new Uri(uri);

        Validate(options).Succeeded.Should().BeTrue();
        ValidateAttributes(options).Succeeded.Should().BeTrue();
    }

    [Fact]
    public void Reject_RelativeUri()
    {
        var options = ValidOptions();
        options.ConnectionUri = new Uri("rabbit.local/prod", UriKind.Relative);

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(RabbitMQOptions.ConnectionUri)).And.Contain("absolute");
    }

    [Theory]
    [InlineData("http://app:pass@rabbit.local/")]
    [InlineData("amqpx://rabbit.local/")]
    public void Reject_OtherScheme(string uri)
    {
        var options = ValidOptions();
        options.ConnectionUri = new Uri(uri);

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("scheme").And.NotContain("pass");
    }

    [Fact]
    public void Reject_UriWithoutHost()
    {
        var options = ValidOptions();
        options.ConnectionUri = new Uri("amqp:///prod");

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain("host");
    }

    [Fact]
    public void Reject_UserInfoRabbitMQClientCantParse_WithoutPasswordInMessage()
    {
        var options = ValidOptions();
        options.ConnectionUri = new Uri($"amqp://app:{Password}:more@rabbit.local/prod");

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(RabbitMQOptions.ConnectionUri))
            .And.NotContain(Password)
            .And.NotContain("rabbit.local");
    }

    [Fact]
    public void Reject_SeveralPathSegments()
    {
        var options = ValidOptions();
        options.ConnectionUri = new Uri("amqp://rabbit.local/prod/other");

        Validate(options).Failed.Should().BeTrue();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Reject_NotPositivePublishTimeout(int seconds)
    {
        var options = ValidOptions();
        options.PublishTimeout = TimeSpan.FromSeconds(seconds);

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(nameof(RabbitMQOptions.PublishTimeout));
    }

    [Fact]
    public void Reject_MissingUri()
    {
        var options = ValidOptions();
        options.ConnectionUri = null!;

        Validate(options).Succeeded.Should().BeTrue("[Required] reports a missing URI");
        ValidateAttributes(options).FailureMessage.Should().Contain(nameof(RabbitMQOptions.ConnectionUri));
    }

    [Theory]
    [InlineData("orders api")]
    [InlineData("orders/api")]
    [InlineData("")]
    public void Reject_ClientNameOutsidePattern(string clientName)
    {
        var options = ValidOptions();
        options.ClientName = clientName;

        ValidateAttributes(options).FailureMessage.Should().Contain(nameof(RabbitMQOptions.ClientName));
    }

    [Fact]
    public void Reject_TooLongClientName()
    {
        var options = ValidOptions();
        options.ClientName = new string('a', 65);

        ValidateAttributes(options).Failed.Should().BeTrue();
    }

    [Fact]
    public void HaveDefaults()
    {
        var options = new RabbitMQOptions();

        options.ClientName.Should().Be(Environment.MachineName);
        options.PublishTimeout.Should().Be(TimeSpan.FromSeconds(30));
        options.PublisherChannelCount.Should().Be(4);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(64, true)]
    [InlineData(65, false)]
    public void CheckPublisherChannelCount(int count, bool valid)
    {
        var options = ValidOptions();
        options.PublisherChannelCount = count;

        ValidateAttributes(options).Succeeded.Should().Be(valid);
    }

    [Fact]
    public async Task FailHostStart_WithInvalidOptions_WithoutUriInMessage()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["RabbitMQ:ConnectionUri"] = $"amqp://app:{Password}:x@rabbit.local/prod",
            ["RabbitMQ:ClientName"] = "orders api"
        });

        var act = () => host.StartAsync();

        var exception = (await act.Should().ThrowAsync<OptionsValidationException>()).Which;
        exception.Message.Should().Contain(nameof(RabbitMQOptions.ConnectionUri))
            .And.Contain(nameof(RabbitMQOptions.ClientName))
            .And.NotContain(Password);
    }

    [Theory]
    [InlineData("rabbit.local/prod")]
    [InlineData("http://rabbit.local/")]
    public async Task FailHostStart_WithUriThatIsNotAmqp(string uri)
    {
        using var host = BuildHost(new Dictionary<string, string?> { ["RabbitMQ:ConnectionUri"] = uri });

        var act = () => host.StartAsync();

        await act.Should().ThrowAsync<OptionsValidationException>();
    }

    [Fact]
    public async Task FailHostStart_WithoutUri()
    {
        using var host = BuildHost(new Dictionary<string, string?>());

        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Message
            .Should().Contain(nameof(RabbitMQOptions.ConnectionUri));
    }

    [Fact]
    public async Task StartHost_WithValidOptions_BoundFromConfiguration()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["RabbitMQ:ConnectionUri"] = "amqps://app:secret@rabbit.example.com:5671/prod",
            ["RabbitMQ:ClientName"] = "orders-api-1",
            ["RabbitMQ:PublishTimeout"] = "00:00:10",
            ["RabbitMQ:PublisherChannelCount"] = "2"
        });

        await host.StartAsync();
        await host.StopAsync();

        var options = host.Services.GetRequiredService<IOptions<RabbitMQOptions>>().Value;
        options.ConnectionUri.Should().Be(new Uri("amqps://app:secret@rabbit.example.com:5671/prod"));
        options.ClientName.Should().Be("orders-api-1");
        options.PublishTimeout.Should().Be(TimeSpan.FromSeconds(10));
        options.PublisherChannelCount.Should().Be(2);
    }

    private static IHost BuildHost(Dictionary<string, string?> values)
    {
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddInMemoryCollection(values);
        builder.Services.AddCuriosusRabbitMQ();

        return builder.Build();
    }
}
