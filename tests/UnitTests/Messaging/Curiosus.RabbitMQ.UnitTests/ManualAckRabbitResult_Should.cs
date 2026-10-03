using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace Curiosus.RabbitMQ.UnitTests;

public class ManualAckRabbitResult_Should
{
    [Fact]
    public async Task ConfirmAcknowledgeAsync_InvokeConfirmationOnce()
    {
        // arrange
        var calls = 0;
        var result = new ManualAckRabbitResult<string>("data", () =>
        {
            Interlocked.Increment(ref calls);
            return Task.CompletedTask;
        });

        // act
        await result.ConfirmAcknowledgeAsync();
        await result.ConfirmAcknowledgeAsync();

        // assert
        calls.Should().Be(1);
        result.Data.Should().Be("data");
    }

    [Fact]
    public async Task ConfirmAcknowledgeAsync_AllowRetry_WhenConfirmationFailed()
    {
        // arrange
        var calls = 0;
        var result = new ManualAckRabbitResult<string>("data", () =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new InvalidOperationException();
            return Task.CompletedTask;
        });

        // act
        var firstAttempt = () => result.ConfirmAcknowledgeAsync();
        await firstAttempt.Should().ThrowAsync<InvalidOperationException>();
        await result.ConfirmAcknowledgeAsync();
        await result.ConfirmAcknowledgeAsync();

        // assert
        calls.Should().Be(2);
    }

    [Fact]
    public async Task ConfirmAcknowledgeAsync_InvokeConfirmationOnce_WhenCalledConcurrently()
    {
        // arrange
        var calls = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new ManualAckRabbitResult<string>("data", async () =>
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
        });

        // act
        var first = result.ConfirmAcknowledgeAsync();
        var second = result.ConfirmAcknowledgeAsync();
        gate.SetResult();
        await Task.WhenAll(first, second);

        // assert
        calls.Should().Be(1);
    }

    [Fact]
    public async Task ConfirmAcknowledgeAsync_CancelOnlyCancelledCall()
    {
        // arrange
        var calls = 0;
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var result = new ManualAckRabbitResult<string>("data", async () =>
        {
            Interlocked.Increment(ref calls);
            await gate.Task;
        });
        using var cts = new CancellationTokenSource();

        // act
        var cancelled = result.ConfirmAcknowledgeAsync(cts.Token);
        var notCancelled = result.ConfirmAcknowledgeAsync();
        await cts.CancelAsync();
        var cancelledAct = () => cancelled;
        await cancelledAct.Should().ThrowAsync<OperationCanceledException>();
        gate.SetResult();
        await notCancelled;
        await result.ConfirmAcknowledgeAsync();

        // assert
        cancelled.IsCanceled.Should().BeTrue();
        calls.Should().Be(1);
    }
}
