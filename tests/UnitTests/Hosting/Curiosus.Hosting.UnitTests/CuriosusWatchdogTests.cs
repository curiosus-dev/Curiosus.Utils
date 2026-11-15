using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Curiosus.Hosting.UnitTests
{
    public sealed class CuriosusWatchdogTests
    {
        private static readonly TimeSpan Period = TimeSpan.FromMinutes(1);

        // fake time moves in steps while ExecuteAsync runs on a background task, so a start may be seen up to a step late;
        // drift and backlog differ by tens of seconds, far more than a step
        private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        private readonly FakeLogger _logger = new();

        [Fact]
        public async Task RunsAtAFixedRate_WithoutDrift()
        {
            // each run takes 20 s: with a fixed delay the starts would drift to 0, 80, 160 s
            var watchdog = new TestWatchdog(_logger, Period, _time, runTime: TimeSpan.FromSeconds(20));
            await watchdog.StartAsync(CancellationToken.None);

            await AdvanceUntilAsync(() => watchdog.StartsQueue.Count >= 3, TimeSpan.FromSeconds(10));
            await watchdog.StopAsync(CancellationToken.None);

            ShouldStartAt(watchdog.Starts, TimeSpan.Zero, Period, 2 * Period);
        }

        [Fact]
        public async Task LongRun_NextStartsImmediately_WithoutBacklog()
        {
            // the first run takes 2.5 periods: ticks at 1 and 2 periods are missed, not queued
            var watchdog = new TestWatchdog(_logger, Period, _time, firstRunTime: Period * 2.5);
            await watchdog.StartAsync(CancellationToken.None);

            await AdvanceUntilAsync(() => watchdog.StartsQueue.Count >= 3, TimeSpan.FromSeconds(10));
            await watchdog.StopAsync(CancellationToken.None);

            ShouldStartAt(watchdog.Starts, TimeSpan.Zero, Period * 2.5, Period * 3);
        }

        [Fact]
        public async Task ChangedPeriod_IsAppliedOnTheNextTick()
        {
            var watchdog = new TestWatchdog(_logger, Period, _time);
            await watchdog.StartAsync(CancellationToken.None);
            await AdvanceUntilAsync(() => watchdog.StartsQueue.Count >= 1, TimeSpan.FromSeconds(10));

            watchdog.Period = TimeSpan.FromMinutes(5);
            await AdvanceUntilAsync(() => watchdog.StartsQueue.Count >= 3, TimeSpan.FromSeconds(30));
            await watchdog.StopAsync(CancellationToken.None);

            // the run at 1 min sees the new period: the next one starts 5 min after it
            ShouldStartAt(watchdog.Starts, TimeSpan.Zero, Period, Period + TimeSpan.FromMinutes(5));
        }

        [Fact]
        public async Task Exception_WaitsExceptionDelay_LogsIt_AndRunsAgain()
        {
            var delay = TimeSpan.FromMinutes(3);
            var watchdog = new TestWatchdog(_logger, Period, _time, exceptionDelay: delay, failFirstRun: true);
            await watchdog.StartAsync(CancellationToken.None);

            await AdvanceUntilAsync(() => watchdog.StartsQueue.Count >= 3, TimeSpan.FromSeconds(10));
            await watchdog.StopAsync(CancellationToken.None);

            // failed at 0, retried after the delay, then a period after the retry
            ShouldStartAt(watchdog.Starts, TimeSpan.Zero, delay, delay + Period);
            var error = _logger.Collector.GetSnapshot().Single(r => r.Level == LogLevel.Error);
            error.Message.Should().Be("TestWatchdog failed, retrying in 00:03:00");
            error.Exception.Should().BeOfType<InvalidOperationException>();
        }

        [Fact]
        public async Task Stop_EndsTheLoopWithoutAWarning()
        {
            var watchdog = new TestWatchdog(_logger, Period, _time);
            await watchdog.StartAsync(CancellationToken.None);
            await AdvanceUntilAsync(() => watchdog.StartsQueue.Count >= 1, TimeSpan.FromSeconds(10));

            await watchdog.StopAsync(CancellationToken.None);

            watchdog.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
            _logger.Collector.GetSnapshot().Should().NotContain(r => r.Level >= LogLevel.Warning);
        }

        [Fact]
        public void NonPositivePeriod_Throws()
        {
            var act = () => new TestWatchdog(_logger, TimeSpan.Zero, _time);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        // ExecuteAsync runs on a background task, so fake time is advanced in small steps until the loop has reacted
        private async Task AdvanceUntilAsync(Func<bool> condition, TimeSpan maxFakeTime)
        {
            var deadline = _time.GetUtcNow() + maxFakeTime * 600;
            var wallClock = Stopwatch.StartNew();
            while (!condition())
            {
                if (_time.GetUtcNow() > deadline || wallClock.Elapsed > TimeSpan.FromSeconds(20))
                    throw new TimeoutException("The watchdog did not reach the expected state.");

                await Task.Delay(1);
                _time.Advance(Step);
            }
        }

        private static void ShouldStartAt(TimeSpan[] starts, params TimeSpan[] expected)
        {
            starts.Length.Should().BeGreaterThanOrEqualTo(expected.Length);
            for (var i = 0; i < expected.Length; i++)
            {
                starts[i].Should().BeCloseTo(expected[i] + Step / 2, Step / 2, $"start #{i} of {string.Join(", ", starts)}");
            }
        }

        private sealed class TestWatchdog : CuriosusWatchdog
        {
            private readonly FakeTimeProvider _time;
            private readonly DateTimeOffset _origin;
            private readonly TimeSpan _runTime;
            private readonly TimeSpan? _firstRunTime;
            private bool _failFirstRun;

            public TestWatchdog(
                ILogger logger,
                TimeSpan period,
                FakeTimeProvider time,
                TimeSpan? exceptionDelay = null,
                TimeSpan runTime = default,
                TimeSpan? firstRunTime = null,
                bool failFirstRun = false)
                : base(logger, period, exceptionDelay, time)
            {
                _time = time;
                _origin = time.GetUtcNow();
                _runTime = runTime;
                _firstRunTime = firstRunTime;
                _failFirstRun = failFirstRun;
                Period = period;
            }

            public ConcurrentQueue<TimeSpan> StartsQueue { get; } = new();

            public TimeSpan[] Starts => StartsQueue.ToArray();

            public TimeSpan Period { get; set; }

            protected override TimeSpan GetPeriod() => Period;

            protected override async Task ProcessAsync(CancellationToken cancellationToken = default)
            {
                var first = StartsQueue.IsEmpty;
                StartsQueue.Enqueue(_time.GetUtcNow() - _origin);

                if (_failFirstRun)
                {
                    _failFirstRun = false;
                    throw new InvalidOperationException("boom");
                }

                var runTime = first && _firstRunTime.HasValue ? _firstRunTime.Value : _runTime;
                if (runTime > TimeSpan.Zero)
                    await Task.Delay(runTime, _time, cancellationToken);
            }
        }
    }
}
