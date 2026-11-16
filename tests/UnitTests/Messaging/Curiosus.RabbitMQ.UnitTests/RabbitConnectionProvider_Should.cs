using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using RabbitMQ.Client.Exceptions;
using Xunit;

namespace Curiosus.RabbitMQ.UnitTests;

public class RabbitConnectionProvider_Should
{
    private static RabbitConnectionProvider CreateProvider() => new(
        Options.Create(new RabbitMQOptions { ConnectionUri = new Uri("amqp://127.0.0.1:1/"), ClientName = "unreachable" }),
        NullLogger<RabbitConnectionProvider>.Instance);

    [Fact]
    public async Task ThrowBrokerUnreachable_AndTryAgainOnNextCall()
    {
        await using var provider = CreateProvider();

        var act = async () => await provider.GetConnectionAsync();

        await act.Should().ThrowAsync<BrokerUnreachableException>();
        await act.Should().ThrowAsync<BrokerUnreachableException>("a failed attempt is not remembered");
    }

    [Fact]
    public async Task ObserveFailedOpen_WhenEveryWaiterCancelled()
    {
        var unobserved = 0;

        void OnUnobserved(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            if (e.Exception.InnerException is BrokerUnreachableException) Interlocked.Increment(ref unobserved);
        }

        TaskScheduler.UnobservedTaskException += OnUnobserved;
        try
        {
            await StartAndAbandonOpenAsync();
            await Task.Delay(TimeSpan.FromSeconds(1));

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();

            unobserved.Should().Be(0);
        }
        finally
        {
            TaskScheduler.UnobservedTaskException -= OnUnobserved;
        }
    }

    private static async Task StartAndAbandonOpenAsync()
    {
        var provider = CreateProvider();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        var act = async () => await provider.GetConnectionAsync(cts.Token);

        // the provider is not disposed: disposing cancels the attempt instead of letting it fail
        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
