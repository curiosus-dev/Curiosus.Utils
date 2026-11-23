using System.Net.Security;
using Curiosus.RequestProcessing.RabbitMQ.Options;
using FluentAssertions;
using Xunit;

namespace Curiosus.RequestProcessing.RabbitMQ.UnitTests;

public class RabbitMQEventReceiverConnection_Should
{
    [Fact]
    public void NotUseTls_ByDefault()
    {
        // arrange
        var options = Options();

        // act
        var factory = RabbitMQEventReceiver.CreateConnectionFactory(options);

        // assert
        factory.Ssl.Enabled.Should().BeFalse();
        factory.Port.Should().Be(5672);
    }

    [Fact]
    public void UseTls_WithServerNameOfHost_WhenEnabled()
    {
        // arrange
        var options = Options();
        options.UseTls = true;
        options.Port = 5671;

        // act
        var factory = RabbitMQEventReceiver.CreateConnectionFactory(options);

        // assert
        factory.Ssl.Enabled.Should().BeTrue();
        factory.Ssl.ServerName.Should().Be("rabbit.example.com");
        factory.Port.Should().Be(5671);
    }

    [Fact]
    public void RejectAnyCertificateError_WhenTlsEnabled()
    {
        // arrange
        var options = Options();
        options.UseTls = true;

        // act
        var factory = RabbitMQEventReceiver.CreateConnectionFactory(options);

        // assert
        factory.Ssl.AcceptablePolicyErrors.Should().Be(SslPolicyErrors.None);
    }

    [Fact]
    public void KeepPort_WhenTlsEnabled()
    {
        // arrange
        var options = Options();
        options.UseTls = true;

        // act
        var factory = RabbitMQEventReceiver.CreateConnectionFactory(options);

        // assert
        factory.Port.Should().Be(5672);
    }

    [Fact]
    public void PassConnectionSettings()
    {
        // arrange
        var options = Options();
        options.ClientName = "worker-1";

        // act
        var factory = RabbitMQEventReceiver.CreateConnectionFactory(options);

        // assert
        factory.HostName.Should().Be("rabbit.example.com");
        factory.UserName.Should().Be("app");
        factory.Password.Should().Be("secret");
        factory.ClientProvidedName.Should().Be("worker-1_event_receiver");
        factory.AutomaticRecoveryEnabled.Should().BeTrue();
        factory.TopologyRecoveryEnabled.Should().BeTrue();
    }

    private static RabbitMQEventReceiverOptions Options() => new()
    {
        HostName = "rabbit.example.com",
        UserName = "app",
        Password = "secret",
        QueueName = "requests"
    };
}
