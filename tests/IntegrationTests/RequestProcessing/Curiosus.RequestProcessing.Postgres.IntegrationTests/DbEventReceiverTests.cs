using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Curiosus.RequestProcessing.Postgres.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class DbEventReceiverTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(15);

    [Fact]
    public async Task PgNotify_MixedCaseChannel_Received()
    {
        var received = await ReceiveAsync("MixedCase", notifyWith: "SELECT pg_notify('MixedCase', 'payload')");

        received.Should().BeTrue();
    }

    [Fact]
    public async Task PgNotify_ChannelWithQuoteAndSemicolon_ReceivedAndNoStatementInjected()
    {
        const string channel = "x\"; CREATE TABLE injected(); --";

        var received = await ReceiveAsync(channel, notifyWith: null);

        received.Should().BeTrue();
        await using var command = postgres.DataSource.CreateCommand("SELECT to_regclass('injected') IS NULL");
        ((bool)(await command.ExecuteScalarAsync())!).Should().BeTrue();
    }

    [Fact]
    public async Task NotifyAsync_ReachesReceiver()
    {
        var received = await ReceiveAsync("from_notify_async", notifyWith: null);

        received.Should().BeTrue();
    }

    /// <summary>
    /// Starts a receiver for the channel and notifies until it gets the event:
    /// a notification sent before its LISTEN ran is not delivered.
    /// </summary>
    private async Task<bool> ReceiveAsync(string channel, string? notifyWith)
    {
        var options = new PostgresEventReceiverOptions { EventNames = [channel], ReconnectionPauseMs = 100 };
        var receiver = new DbEventReceiver(
            options,
            new MonitoredDatabase(postgres.ConnectionString),
            NullLogger<DbEventReceiver>.Instance);
        var received = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        receiver.OnEventReceived += (_, e) =>
        {
            if (e is DbEvent { EventName: var name } && name == channel) received.TrySetResult();
        };

        using var timeout = new CancellationTokenSource(Timeout);
        await receiver.StartAsync(timeout.Token);
        try
        {
            while (!received.Task.IsCompleted && !timeout.IsCancellationRequested)
            {
                if (notifyWith == null)
                {
                    await postgres.DataSource.NotifyAsync(channel, "payload", timeout.Token);
                }
                else
                {
                    await using var command = postgres.DataSource.CreateCommand(notifyWith);
                    await command.ExecuteNonQueryAsync(timeout.Token);
                }

                await Task.WhenAny(received.Task, Task.Delay(200, CancellationToken.None));
            }

            return received.Task.IsCompleted;
        }
        finally
        {
            await receiver.StopAsync(CancellationToken.None);
            receiver.Dispose();
        }
    }
}
