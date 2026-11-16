using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Options;
using Moq;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Curiosus.RabbitMQ.UnitTests;

public class RabbitPublisher_Should
{
    private static readonly TimeSpan PublishTimeout = TimeSpan.FromMilliseconds(500);

    private readonly Mock<IRabbitConnectionProvider> _connectionProvider = new();

    private RabbitPublisher CreatePublisher(IRabbitMQMessageSerializer? serializer = null)
    {
        var options = new RabbitMQOptions
        {
            ConnectionUri = new Uri("amqp://app:pass@rabbit.local/"),
            PublishTimeout = PublishTimeout
        };

        return new RabbitPublisher(
            _connectionProvider.Object,
            serializer ?? new RabbitMQJsonMessageSerializer(),
            Options.Create(options),
            TimeProvider.System);
    }

    [Fact]
    public async Task FailWithTimeout_WhenConnectionCantBeOpened()
    {
        var unreachable = new BrokerUnreachableException(new Exception("connection refused"));
        _connectionProvider.Setup(p => p.GetConnectionAsync(It.IsAny<CancellationToken>())).ThrowsAsync(unreachable);
        await using var publisher = CreatePublisher();
        var stopwatch = Stopwatch.StartNew();

        var act = () => publisher.PublishAsync("orders", "paid", new { Id = 1 },
            new RabbitPublishProperties { MessageId = "m-1" });

        var exception = (await act.Should().ThrowAsync<RabbitPublishException>()).Which;
        stopwatch.Elapsed.Should().BeGreaterThanOrEqualTo(PublishTimeout - TimeSpan.FromMilliseconds(50));
        exception.Reason.Should().Be(RabbitPublishFailure.Timeout);
        exception.InnerException.Should().BeSameAs(unreachable);
        exception.Exchange.Should().Be("orders");
        exception.RoutingKey.Should().Be("paid");
        exception.MessageId.Should().Be("m-1");
        exception.ReplyCode.Should().BeNull();
        exception.Message.Should().Contain("m-1").And.Contain("orders").And.NotContain("pass");
    }

    [Fact]
    public async Task FailWithTimeout_WhenConnectionDoesntRecover()
    {
        var connection = new Mock<IConnection>();
        connection.SetupGet(c => c.IsOpen).Returns(false);
        _connectionProvider.Setup(p => p.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(connection.Object);
        await using var publisher = CreatePublisher();

        var act = () => publisher.PublishAsync("orders", "paid", new { Id = 1 });

        (await act.Should().ThrowAsync<RabbitPublishException>()).Which.Reason.Should().Be(RabbitPublishFailure.Timeout);
        connection.Verify(
            c => c.CreateChannelAsync(It.IsAny<CreateChannelOptions?>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ThrowOperationCanceled_WhenCallerCancels()
    {
        _connectionProvider.Setup(p => p.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("unreachable");
            });
        await using var publisher = CreatePublisher();
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        var act = () => publisher.PublishAsync("orders", "paid", new { Id = 1 }, cancellationToken: cts.Token);

        (await act.Should().ThrowAsync<OperationCanceledException>()).Which.CancellationToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task PropagateSerializerException_WithoutPublishing()
    {
        var serializer = new Mock<IRabbitMQMessageSerializer>();
        serializer.Setup(s => s.Serialize(It.IsAny<object>())).Throws(new NotSupportedException("can't serialize"));
        await using var publisher = CreatePublisher(serializer.Object);

        var act = () => publisher.PublishAsync<object>("orders", "paid", new { Id = 1 });

        await act.Should().ThrowAsync<NotSupportedException>().WithMessage("can't serialize");
        _connectionProvider.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task ThrowObjectDisposed_AfterDispose()
    {
        var publisher = CreatePublisher();
        await publisher.DisposeAsync();

        var act = () => publisher.PublishAsync("orders", "paid", new { Id = 1 });

        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task ThrowObjectDisposed_WhenDisposedWhileWaiting()
    {
        _connectionProvider.Setup(p => p.GetConnectionAsync(It.IsAny<CancellationToken>()))
            .Returns<CancellationToken>(async ct =>
            {
                await Task.Delay(Timeout.Infinite, ct);
                throw new InvalidOperationException("unreachable");
            });
        var publisher = CreatePublisher();

        var publish = publisher.PublishAsync("orders", "paid", new { Id = 1 });
        await publisher.DisposeAsync();

        var act = () => publish;
        await act.Should().ThrowAsync<ObjectDisposedException>();
    }

    [Fact]
    public async Task CheckArguments()
    {
        await using var publisher = CreatePublisher();

        await FluentActions.Awaiting(() => publisher.PublishAsync(null!, "paid", 1))
            .Should().ThrowAsync<ArgumentNullException>();
        await FluentActions.Awaiting(() => publisher.PublishAsync("orders", null!, 1))
            .Should().ThrowAsync<ArgumentNullException>();
    }
}
