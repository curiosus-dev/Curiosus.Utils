using System;
using System.Threading.Tasks;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Curiosus.RequestProcessing.Postgres.IntegrationTests;

[Collection(PostgresCollection.Name)]
public class NotifyAsyncTests(PostgresFixture postgres)
{
    private static readonly TimeSpan Quiet = TimeSpan.FromMilliseconds(500);
    private static readonly TimeSpan Wait = TimeSpan.FromSeconds(10);

    [Fact]
    public async Task NotifyAsync_InTransaction_DeliveredOnlyAfterCommit()
    {
        await using var listener = await ListenAsync("tx_commit");
        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await connection.NotifyAsync("tx_commit", "1");

        (await listener.Connection.WaitAsync(Quiet)).Should().BeFalse("nothing is sent before the commit");
        await transaction.CommitAsync();
        (await listener.Connection.WaitAsync(Wait)).Should().BeTrue();
        listener.Payload.Should().Be("1");
    }

    [Fact]
    public async Task NotifyAsync_InRolledBackTransaction_NotDelivered()
    {
        await using var listener = await ListenAsync("tx_rollback");
        await using var connection = await postgres.DataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        await connection.NotifyAsync("tx_rollback", "1");
        await transaction.RollbackAsync();

        (await listener.Connection.WaitAsync(Quiet)).Should().BeFalse();
    }

    [Fact]
    public async Task NotifyAsync_DataSource_ChannelUsedAsWritten()
    {
        await using var listener = await ListenAsync("CamelChannel");

        await postgres.DataSource.NotifyAsync("CamelChannel", "payload");

        (await listener.Connection.WaitAsync(Wait)).Should().BeTrue();
        listener.Channel.Should().Be("CamelChannel");
    }

    [Fact]
    public async Task NotifyAsync_NullPayload_Delivered()
    {
        await using var listener = await ListenAsync("no_payload");

        await postgres.DataSource.NotifyAsync("no_payload");

        (await listener.Connection.WaitAsync(Wait)).Should().BeTrue();
        listener.Payload.Should().BeEmpty();
    }

    private async Task<Listener> ListenAsync(string channel)
    {
        var connection = await postgres.DataSource.OpenConnectionAsync();
        var listener = new Listener(connection);
        await using var command = new NpgsqlCommand(DbEventReceiver.BuildListenSql([channel]), connection);
        await command.ExecuteNonQueryAsync();
        return listener;
    }

    private sealed class Listener : IAsyncDisposable
    {
        public Listener(NpgsqlConnection connection)
        {
            Connection = connection;
            connection.Notification += (_, e) =>
            {
                Channel = e.Channel;
                Payload = e.Payload;
            };
        }

        public NpgsqlConnection Connection { get; }

        public string? Channel { get; private set; }

        public string? Payload { get; private set; }

        public ValueTask DisposeAsync() => Connection.DisposeAsync();
    }
}
