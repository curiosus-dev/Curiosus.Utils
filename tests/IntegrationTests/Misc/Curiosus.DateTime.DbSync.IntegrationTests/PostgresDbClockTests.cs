using System;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Npgsql;
using Testcontainers.PostgreSql;
using Xunit;

namespace Curiosus.DateTime.DbSync.IntegrationTests;

public sealed class PostgresDbClockTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();

    private NpgsqlDataSource _dataSource = null!;

    public async Task InitializeAsync()
    {
        await _container.StartAsync();
        _dataSource = NpgsqlDataSource.Create(_container.GetConnectionString());
    }

    public async Task DisposeAsync()
    {
        await _dataSource.DisposeAsync();
        await _container.DisposeAsync();
    }

    [Fact]
    public async Task GetUtcNowAsync_CloseToLocalUtcNow()
    {
        var clock = new PostgresDbClock(_dataSource);

        var before = DateTimeOffset.UtcNow;
        var dbNow = await clock.GetUtcNowAsync(CancellationToken.None);
        var after = DateTimeOffset.UtcNow;

        // the container shares the host clock
        dbNow.Offset.Should().Be(TimeSpan.Zero);
        dbNow.Should().BeOnOrAfter(before.AddSeconds(-1)).And.BeOnOrBefore(after.AddSeconds(1));
    }

    [Fact]
    public async Task GetUtcNowAsync_NonUtcSessionTimeZone_StillUtc()
    {
        var builder = new NpgsqlConnectionStringBuilder(_container.GetConnectionString()) { Timezone = "Asia/Tokyo" };
        await using var tokyo = NpgsqlDataSource.Create(builder.ConnectionString);
        var clock = new PostgresDbClock(tokyo);

        var dbNow = await clock.GetUtcNowAsync(CancellationToken.None);

        (dbNow - DateTimeOffset.UtcNow).Duration().Should().BeLessThan(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public async Task SyncService_OverPostgres_SmallOffset()
    {
        // the first query of a cold pool also opens the connection, which can take longer than MaxAcceptedRtt on CI
        // and get the sample rejected; this test is about the offset of a normal round trip
        await new PostgresDbClock(_dataSource).GetUtcNowAsync(CancellationToken.None);

        var timeProvider = new DbSyncTimeProvider();
        var service = new DbTimeSyncService(
            timeProvider,
            new PostgresDbClock(_dataSource),
            Microsoft.Extensions.Options.Options.Create(new DbSyncTimeOptions()),
            Microsoft.Extensions.Logging.Abstractions.NullLogger<DbTimeSyncService>.Instance);

        await service.StartAsync(CancellationToken.None);
        await service.StopAsync(CancellationToken.None);

        timeProvider.IsSynchronized.Should().BeTrue();
        timeProvider.Offset.Duration().Should().BeLessThan(TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void LegacyTimestampSwitch_AsConfiguredForThisProject()
    {
        var expected = typeof(PostgresDbClockTests).Assembly.GetName().Name!.Contains("LegacyTimestamp");

        (AppContext.TryGetSwitch("Npgsql.EnableLegacyTimestampBehavior", out var enabled) && enabled).Should().Be(expected);
    }
}
