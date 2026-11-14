using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Curiosus.DateTime.DbSync.UnitTests;

public class DbTimeSyncServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 14, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeTimeProvider _localClock = new(Start);
    private readonly DbSyncTimeProvider _timeProvider;
    private readonly FakeDbClock _dbClock;
    private readonly DbSyncTimeOptions _options = new();

    public DbTimeSyncServiceTests()
    {
        _timeProvider = new DbSyncTimeProvider(_localClock);
        _dbClock = new FakeDbClock(_localClock);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(-5)]
    public async Task SyncOnce_DbAheadOrBehind_OffsetHasItsSign(int seconds)
    {
        _dbClock.Offset = TimeSpan.FromSeconds(seconds);

        (await CreateService().SyncOnceAsync(CancellationToken.None)).Should().BeTrue();

        _timeProvider.Offset.Should().Be(TimeSpan.FromSeconds(seconds));
        _timeProvider.IsSynchronized.Should().BeTrue();
        _timeProvider.GetUtcNow().Should().Be(_localClock.GetUtcNow() + TimeSpan.FromSeconds(seconds));
    }

    [Fact]
    public async Task SyncOnce_RoundTrip_CompensatedByHalf()
    {
        _dbClock.Offset = TimeSpan.FromSeconds(3);
        _dbClock.Rtt = TimeSpan.FromMilliseconds(400);

        await CreateService().SyncOnceAsync(CancellationToken.None);

        // Without compensation the offset would be 3.2 s: the database answered 200 ms after the local reading.
        _timeProvider.Offset.Should().Be(TimeSpan.FromSeconds(3));
    }

    [Fact]
    public async Task SyncOnce_RoundTripAboveLimit_SampleDiscarded()
    {
        _dbClock.Offset = TimeSpan.FromSeconds(3);
        _dbClock.Rtt = _options.MaxAcceptedRtt + TimeSpan.FromMilliseconds(1);

        (await CreateService().SyncOnceAsync(CancellationToken.None)).Should().BeFalse();

        _timeProvider.Offset.Should().Be(TimeSpan.Zero);
        _timeProvider.IsSynchronized.Should().BeFalse();
    }

    [Fact]
    public async Task SyncOnce_Failure_KeepsPreviousOffset()
    {
        var service = CreateService();
        _dbClock.Offset = TimeSpan.FromSeconds(7);
        await service.SyncOnceAsync(CancellationToken.None);

        _dbClock.Failure = new InvalidOperationException("db is down");
        var act = () => service.SyncOnceAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        _timeProvider.Offset.Should().Be(TimeSpan.FromSeconds(7));
        _timeProvider.IsSynchronized.Should().BeTrue();
    }

    [Fact]
    public async Task StartAsync_DbAvailable_SynchronizedBeforeStartReturns()
    {
        _dbClock.Offset = TimeSpan.FromSeconds(2);
        var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        try
        {
            _timeProvider.IsSynchronized.Should().BeTrue();
            _timeProvider.Offset.Should().Be(TimeSpan.FromSeconds(2));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StartAsync_DbUnavailable_NoExceptionRetriedUntilAvailable()
    {
        _dbClock.Failure = new InvalidOperationException("database does not exist");
        _dbClock.Offset = TimeSpan.FromSeconds(4);
        var service = CreateService();

        await service.StartAsync(CancellationToken.None);
        try
        {
            _timeProvider.IsSynchronized.Should().BeFalse();
            _timeProvider.Offset.Should().Be(TimeSpan.Zero);
            _dbClock.Calls.Should().Be(1);

            await AdvanceUntilAsync(_options.RetryPeriod, () => _dbClock.Calls >= 2);
            _timeProvider.IsSynchronized.Should().BeFalse();

            _dbClock.Failure = null;
            await AdvanceUntilAsync(_options.RetryPeriod, () => _timeProvider.IsSynchronized);
            _timeProvider.Offset.Should().Be(TimeSpan.FromSeconds(4));
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public async Task StartAsync_DbHangs_StartReturnsAfterInitialTimeout()
    {
        _dbClock.Gate = new TaskCompletionSource();
        var service = CreateService();

        var start = service.StartAsync(CancellationToken.None);
        start.IsCompleted.Should().BeFalse();
        _localClock.Advance(_options.InitialSyncTimeout);

        await start.WaitAsync(TimeSpan.FromSeconds(5));
        _timeProvider.IsSynchronized.Should().BeFalse();
        _dbClock.Gate.SetResult();
        await service.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task ExecuteAsync_AfterFirstSync_ResyncsEverySyncPeriod()
    {
        _dbClock.Offset = TimeSpan.FromSeconds(1);
        var service = CreateService();
        await service.StartAsync(CancellationToken.None);
        try
        {
            _dbClock.Offset = TimeSpan.FromSeconds(-2);
            await Task.Delay(50);
            _localClock.Advance(_options.SyncPeriod - TimeSpan.FromSeconds(1));
            await Task.Delay(50);
            _dbClock.Calls.Should().Be(1, "no resync before the period");

            await AdvanceUntilAsync(_options.SyncPeriod, () => _timeProvider.Offset == TimeSpan.FromSeconds(-2));
            var calls = _dbClock.Calls;
            await AdvanceUntilAsync(_options.SyncPeriod, () => _dbClock.Calls > calls);
        }
        finally
        {
            await service.StopAsync(CancellationToken.None);
        }
    }

    [Fact]
    public void Offset_ConcurrentReadsAndWrites_AlwaysOneOfWrittenValues()
    {
        var a = TimeSpan.FromTicks(0x0000_0001_FFFF_FFFF);
        var b = TimeSpan.FromTicks(-0x0000_0002_0000_0001);
        using var stop = new CancellationTokenSource(TimeSpan.FromMilliseconds(300));

        var writer = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                _timeProvider.SetOffset(a);
                _timeProvider.SetOffset(b);
            }
        });
        var readers = Enumerable.Range(0, 4).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                var offset = _timeProvider.Offset;
                if (offset != a && offset != b && offset != TimeSpan.Zero) return offset;
            }

            return (TimeSpan?)null;
        })).ToArray();

        Task.WaitAll([writer, .. readers]);
        readers.Select(r => r.Result).Should().AllSatisfy(torn => torn.Should().BeNull());
    }

    [Fact]
    public void Provider_TimestampsAndTimersComeFromLocalClock()
    {
        _timeProvider.SetOffset(TimeSpan.FromHours(1));

        _timeProvider.GetTimestamp().Should().Be(_localClock.GetTimestamp());
        _timeProvider.TimestampFrequency.Should().Be(_localClock.TimestampFrequency);
        _timeProvider.LocalTimeZone.Should().Be(_localClock.LocalTimeZone);
    }

    private DbTimeSyncService CreateService() => new(
        _timeProvider,
        _dbClock,
        Options.Create(_options),
        NullLogger<DbTimeSyncService>.Instance);

    /// <summary>
    /// Advances the fake clock by <paramref name="step"/> and waits for the background loop to react.
    /// </summary>
    private async Task AdvanceUntilAsync(TimeSpan step, Func<bool> condition)
    {
        // ExecuteAsync runs on a background task (.NET 10) and may create its timer after an advance:
        // advance again until the loop reacts
        for (var i = 0; i < 100 && !condition(); i++)
        {
            if (i % 10 == 0) _localClock.Advance(step);
            await Task.Delay(10);
        }

        condition().Should().BeTrue();
    }
}
