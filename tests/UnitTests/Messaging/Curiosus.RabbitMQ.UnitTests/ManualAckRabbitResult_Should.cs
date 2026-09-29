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
        var result = new ManualAckRabbitResult<string>("data", _ =>
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
    public async Task ConfirmAcknowledgeAsync_PassCancellationToken()
    {
        // arrange
        using var cts = new CancellationTokenSource();
        CancellationToken passedToken = default;
        var result = new ManualAckRabbitResult<string>("data", token =>
        {
            passedToken = token;
            return Task.CompletedTask;
        });

        // act
        await result.ConfirmAcknowledgeAsync(cts.Token);

        // assert
        passedToken.Should().Be(cts.Token);
    }

    [Fact]
    public async Task ConfirmAcknowledgeAsync_AllowRetry_WhenConfirmationFailed()
    {
        // arrange
        var calls = 0;
        var result = new ManualAckRabbitResult<string>("data", _ =>
        {
            if (Interlocked.Increment(ref calls) == 1) throw new OperationCanceledException();
            return Task.CompletedTask;
        });

        // act
        var firstAttempt = () => result.ConfirmAcknowledgeAsync();
        await firstAttempt.Should().ThrowAsync<OperationCanceledException>();
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
        var result = new ManualAckRabbitResult<string>("data", async _ =>
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
}
