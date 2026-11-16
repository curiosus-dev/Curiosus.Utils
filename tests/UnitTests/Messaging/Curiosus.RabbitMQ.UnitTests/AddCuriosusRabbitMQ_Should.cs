using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Security;
using Curiosus.RabbitMQ.RPC;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.RabbitMQ.UnitTests;

public class AddCuriosusRabbitMQ_Should
{
    private static ServiceProvider BuildServices(
        Action<IServiceCollection>? configure = null,
        string uri = "amqp://app:pass@rabbit.local/")
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Messaging:ConnectionUri"] = uri,
                ["Messaging:ClientName"] = "orders-api-1"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging();
        configure?.Invoke(services);

        return services.BuildServiceProvider();
    }

    [Fact]
    public void RegisterServicesAsSingletons()
    {
        using var provider = BuildServices(s => s.AddCuriosusRabbitMQ("Messaging"));

        provider.GetRequiredService<IOptions<RabbitMQOptions>>().Value.ClientName.Should().Be("orders-api-1");
        provider.GetRequiredService<IRabbitConnectionProvider>().Should().BeOfType<RabbitConnectionProvider>()
            .And.BeSameAs(provider.GetRequiredService<IRabbitConnectionProvider>());
        provider.GetRequiredService<IRabbitMQMessageSerializer>().Should().BeOfType<RabbitMQJsonMessageSerializer>()
            .And.BeSameAs(provider.GetRequiredService<IRabbitMQMessageSerializer>());
        provider.GetRequiredService<IRabbitPublisher>().Should().BeOfType<RabbitPublisher>()
            .And.BeSameAs(provider.GetRequiredService<IRabbitPublisher>());
        provider.GetRequiredService<RabbitMqRpcClientFactory>()
            .Should().BeSameAs(provider.GetRequiredService<RabbitMqRpcClientFactory>());
    }

    [Fact]
    public void BindDefaultSection()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:ConnectionUri"] = "amqp://rabbit.local/",
                ["RabbitMQ:PublisherChannelCount"] = "2"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddCuriosusRabbitMQ();

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<IOptions<RabbitMQOptions>>().Value;
        RabbitMQOptions.SectionName.Should().Be("RabbitMQ");
        options.ConnectionUri.Should().Be(new Uri("amqp://rabbit.local/"));
        options.PublisherChannelCount.Should().Be(2);
    }

    [Fact]
    public void BindCustomSection_ThenApplyConfigure()
    {
        using var provider = BuildServices(s => s.AddCuriosusRabbitMQ("Messaging", o =>
        {
            o.ClientName = "configured-in-code";
            o.PublishTimeout = TimeSpan.FromSeconds(5);
        }));

        var options = provider.GetRequiredService<IOptions<RabbitMQOptions>>().Value;

        options.ConnectionUri.Should().Be(new Uri("amqp://app:pass@rabbit.local/"), "bound from the section");
        options.ClientName.Should().Be("configured-in-code", "configure runs after binding");
        options.PublishTimeout.Should().Be(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void ReturnOptionsBuilder_ForMoreValidation_AlsoOnLaterCalls()
    {
        using var provider = BuildServices(s =>
        {
            s.AddCuriosusRabbitMQ("Messaging").Validate(o => o.PublisherChannelCount > 1, "first rule");
            s.AddCuriosusRabbitMQ("Messaging").Validate(o => o.ClientName != "orders-api-1", "second rule");
        });

        var act = () => provider.GetRequiredService<IOptions<RabbitMQOptions>>().Value;

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().BeEquivalentTo("second rule");
    }

    [Fact]
    public void ConfigureInCode_WithoutSection()
    {
        using var provider = BuildServices(s => s.AddCuriosusRabbitMQ(
            configure: o => o.ConnectionUri = new Uri("amqp://code.local/")));

        var options = provider.GetRequiredService<IOptions<RabbitMQOptions>>().Value;

        options.ConnectionUri.Should().Be(new Uri("amqp://code.local/"));
        options.ClientName.Should().Be(Environment.MachineName);
    }

    [Fact]
    public void BindAndValidateOnce_OnSecondCall_AddingItsConfigure()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:ConnectionUri"] = "amqp://first.local/",
                ["RabbitMQ:ClientName"] = "bad name",
                ["Other:ConnectionUri"] = "amqp://other.local/"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddCuriosusRabbitMQ(configure: o => o.PublisherChannelCount = 2);
        services.AddCuriosusRabbitMQ("Other", o => o.PublishTimeout = TimeSpan.Zero);

        using var provider = services.BuildServiceProvider();
        var act = () => provider.GetRequiredService<IOptions<RabbitMQOptions>>().Value;

        var exception = act.Should().Throw<OptionsValidationException>().Which;
        exception.Failures.Count(f => f.Contains(nameof(RabbitMQOptions.ClientName))).Should().Be(1);
        exception.Failures.Count(f => f.Contains(nameof(RabbitMQOptions.PublishTimeout))).Should().Be(1,
            "the configure of the second call applies");
        exception.Failures.Should().HaveCount(2);
    }

    [Fact]
    public void BindFirstSection_OnSecondCall()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["RabbitMQ:ConnectionUri"] = "amqp://first.local/",
                ["Other:ConnectionUri"] = "amqp://other.local/"
            })
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);

        services.AddCuriosusRabbitMQ();
        services.AddCuriosusRabbitMQ("Other");

        using var provider = services.BuildServiceProvider();
        provider.GetRequiredService<IOptions<RabbitMQOptions>>().Value.ConnectionUri
            .Should().Be(new Uri("amqp://first.local/"));
    }

    [Fact]
    public void RegisterValidatorOnce()
    {
        var services = new ServiceCollection();

        services.AddCuriosusRabbitMQ();
        services.AddCuriosusRabbitMQ();

        services.Count(d => d.ImplementationType == typeof(RabbitMQOptionsValidator)).Should().Be(1);
    }

    [Fact]
    public void KeepCustomSerializer_RegisteredBefore()
    {
        var custom = new CustomSerializer();
        using var provider = BuildServices(s =>
        {
            s.AddSingleton<IRabbitMQMessageSerializer>(custom);
            s.AddCuriosusRabbitMQ("Messaging");
        });

        provider.GetRequiredService<IRabbitMQMessageSerializer>().Should().BeSameAs(custom);
    }

    [Fact]
    public void UseCustomSerializer_RegisteredAfter()
    {
        var custom = new CustomSerializer();
        using var provider = BuildServices(s =>
        {
            s.AddCuriosusRabbitMQ("Messaging");
            s.AddSingleton<IRabbitMQMessageSerializer>(custom);
        });

        provider.GetRequiredService<IRabbitMQMessageSerializer>().Should().BeSameAs(custom);
    }

    [Fact]
    public void ThrowOnEmptySection()
    {
        var act = () => new ServiceCollection().AddCuriosusRabbitMQ(" ");

        act.Should().Throw<ArgumentException>();
    }

    [Fact]
    public void OpenConnection_NamedAfterClientName_WithRecovery()
    {
        using var provider = BuildServices(s => s.AddCuriosusRabbitMQ("Messaging"));

        var factory = ((RabbitConnectionProvider)provider.GetRequiredService<IRabbitConnectionProvider>()).ConnectionFactory;

        factory.ClientProvidedName.Should().Be("orders-api-1");
        factory.AutomaticRecoveryEnabled.Should().BeTrue();
        factory.TopologyRecoveryEnabled.Should().BeTrue();
        factory.HostName.Should().Be("rabbit.local");
        factory.UserName.Should().Be("app");
        factory.Ssl.Enabled.Should().BeFalse();
    }

    [Fact]
    public void ResetAmqpsPolicyErrors_ToNone()
    {
        using var provider = BuildServices(s => s.AddCuriosusRabbitMQ("Messaging"), "amqps://app:pass@rabbit.local/prod");

        var factory = ((RabbitConnectionProvider)provider.GetRequiredService<IRabbitConnectionProvider>()).ConnectionFactory;

        factory.Ssl.Enabled.Should().BeTrue();
        factory.Ssl.ServerName.Should().Be("rabbit.local");
        factory.Ssl.AcceptablePolicyErrors.Should().Be(SslPolicyErrors.None);
        factory.Port.Should().Be(5671);
        factory.VirtualHost.Should().Be("prod");
    }

    [Fact]
    public void NameRpcConnection_AfterClientName()
    {
        var factory = RabbitConnectionFactoryBuilder.Create(new Uri("amqps://rabbit.local/"), "orders-api-1_rpc_client");

        factory.ClientProvidedName.Should().Be("orders-api-1_rpc_client");
        factory.Ssl.AcceptablePolicyErrors.Should().Be(SslPolicyErrors.None);
    }

    private sealed class CustomSerializer : IRabbitMQMessageSerializer
    {
        public string ContentType => "application/x-custom";

        public ReadOnlyMemory<byte> Serialize<T>(T message) => Array.Empty<byte>();

        public T Deserialize<T>(ReadOnlyMemory<byte> body, string? contentType) => default!;
    }
}
