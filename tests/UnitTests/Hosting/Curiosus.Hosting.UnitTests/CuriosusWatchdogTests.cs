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
        private static readonly TimeSpan Step = TimeSpan.FromSeconds(1);

        private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));
        private readonly CountingTimeProvider _clock;

        public CuriosusWatchdogTests()
        {
            _clock = new CountingTimeProvider(_time);
        }
        private readonly FakeLogger _logger = new();

        [Fact]
        public async Task RunsAtAFixedRate_WithoutDrift()
        {
            // each run takes 20 s: with a fixed delay the starts would drift to 0, 80, 160 s
            var watchdog = new TestWatchdog(_logger, Period, _clock, runTime: TimeSpan.FromSeconds(20));
            await watchdog.StartAsync(CancellationToken.None);

            await AdvanceUntilAsync(watchdog, () => watchdog.StartsQueue.Count >= 3);
            await watchdog.StopAsync(CancellationToken.None);

            ShouldStartAt(watchdog.Starts, TimeSpan.Zero, Period, 2 * Period);
        }

        [Fact]
        public async Task LongRun_NextStartsImmediately_WithoutBacklog()
        {
            // the first run takes 2.5 periods: ticks at 1 and 2 periods are missed, not queued
            var watchdog = new TestWatchdog(_logger, Period, _clock, firstRunTime: Period * 2.5);
            await watchdog.StartAsync(CancellationToken.None);

            await AdvanceUntilAsync(watchdog, () => watchdog.StartsQueue.Count >= 3);
            await watchdog.StopAsync(CancellationToken.None);

            ShouldStartAt(watchdog.Starts, TimeSpan.Zero, Period * 2.5, Period * 3);
        }

        [Fact]
        public async Task ChangedPeriod_IsAppliedOnTheNextTick()
        {
            var watchdog = new TestWatchdog(_logger, Period, _clock);
            await watchdog.StartAsync(CancellationToken.None);
            await AdvanceUntilAsync(watchdog, () => watchdog.StartsQueue.Count >= 1);

            watchdog.Period = TimeSpan.FromMinutes(5);
            await AdvanceUntilAsync(watchdog, () => watchdog.StartsQueue.Count >= 3);
            await watchdog.StopAsync(CancellationToken.None);

            // the run at 1 min sees the new period: the next one starts 5 min after it
            ShouldStartAt(watchdog.Starts, TimeSpan.Zero, Period, Period + TimeSpan.FromMinutes(5));
        }

        [Fact]
        public async Task Exception_WaitsExceptionDelay_LogsIt_AndRunsAgain()
        {
            var delay = TimeSpan.FromMinutes(3);
            var watchdog = new TestWatchdog(_logger, Period, _clock, exceptionDelay: delay, failFirstRun: true);
            await watchdog.StartAsync(CancellationToken.None);

            await AdvanceUntilAsync(watchdog, () => watchdog.StartsQueue.Count >= 3);
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
            var watchdog = new TestWatchdog(_logger, Period, _clock);
            await watchdog.StartAsync(CancellationToken.None);
            await AdvanceUntilAsync(watchdog, () => watchdog.StartsQueue.Count >= 1);

            await watchdog.StopAsync(CancellationToken.None);

            watchdog.ExecuteTask!.IsCompletedSuccessfully.Should().BeTrue();
            _logger.Collector.GetSnapshot().Should().NotContain(r => r.Level >= LogLevel.Warning);
        }

        [Fact]
        public void NonPositivePeriod_Throws()
        {
            var act = () => new TestWatchdog(_logger, TimeSpan.Zero, _clock);

            act.Should().Throw<ArgumentOutOfRangeException>();
        }

        // ExecuteAsync runs on a background task. Fake time moves one step at a time, and only after the loop has blocked
        // again on fake time whenever the step fired a timer, so every run starts at the exact fake time it is due
        private async Task AdvanceUntilAsync(TestWatchdog watchdog, Func<bool> condition)
        {
            int Blocked() => watchdog.Blocked + _clock.Changed;

            await WaitForAsync(() => Blocked() > 0 || condition());
            var steps = 0;
            while (!condition())
            {
                if (++steps > 100_000) throw new TimeoutException("The watchdog did not reach the expected state.");

                var fired = _clock.Fired;
                var blocked = Blocked();
                _time.Advance(Step);

                // a timer that fires while the loop is busy wakes nobody: give up waiting after a short real-time pause
                if (_clock.Fired != fired)
                    await WaitForAsync(() => Blocked() > blocked || condition(), TimeSpan.FromMilliseconds(200));
            }
        }

        private static async Task WaitForAsync(Func<bool> condition, TimeSpan? timeout = null)
        {
            var wallClock = Stopwatch.StartNew();
            var limit = timeout ?? TimeSpan.FromSeconds(10);
            while (!condition() && wallClock.Elapsed < limit)
            {
                await Task.Delay(1);
            }
        }

        private static void ShouldStartAt(TimeSpan[] starts, params TimeSpan[] expected)
        {
            starts.Take(expected.Length).Should().Equal(expected, string.Join(", ", starts));
        }

        private sealed class TestWatchdog : CuriosusWatchdog
        {
            private readonly TimeProvider _time;
            private readonly DateTimeOffset _origin;
            private int _blocked;
            private bool _runCompleted;
            private TimeSpan _lastPeriod;
            private readonly TimeSpan _runTime;
            private readonly TimeSpan? _firstRunTime;
            private bool _failFirstRun;

            public TestWatchdog(
                ILogger logger,
                TimeSpan period,
                TimeProvider time,
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

            /// <summary>
            /// How many times the loop has blocked on fake time: the timer wait, the run's delay, the pause after an exception.
            /// </summary>
            public int Blocked => Volatile.Read(ref _blocked);

            // the base reads the period right before waiting for the next tick, after a completed run
            protected override TimeSpan GetPeriod()
            {
                if (_runCompleted)
                {
                    _runCompleted = false;
                    // a changed period is applied by setting PeriodicTimer.Period after this call, and a tick of the old
                    // period can still fire before that: the clock counts that change instead
                    if (Period == _lastPeriod) Interlocked.Increment(ref _blocked);
                }

                _lastPeriod = Period;
                return Period;
            }

            protected override async Task HandleExceptionAsync(Exception exception, CancellationToken stoppingToken = default)
            {
                // the base creates its delay synchronously, so the loop is blocked once the call returns
                var pause = base.HandleExceptionAsync(exception, stoppingToken);
                Interlocked.Increment(ref _blocked);
                await pause;
            }

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
                {
                    var delay = Task.Delay(runTime, _time, cancellationToken);
                    Interlocked.Increment(ref _blocked);
                    await delay;
                }

                _runCompleted = true;
            }
        }

        /// <summary>
        /// Delegates to <see cref="FakeTimeProvider"/> and counts timer callbacks, to tell whether a step woke the watchdog,
        /// and timer changes, to tell when a changed period is applied.
        /// </summary>
        private sealed class CountingTimeProvider(FakeTimeProvider inner) : TimeProvider
        {
            private int _fired;
            private int _changed;

            public int Fired => Volatile.Read(ref _fired);

            public int Changed => Volatile.Read(ref _changed);

            public override DateTimeOffset GetUtcNow() => inner.GetUtcNow();

            public override long GetTimestamp() => inner.GetTimestamp();

            public override long TimestampFrequency => inner.TimestampFrequency;

            public override TimeZoneInfo LocalTimeZone => inner.LocalTimeZone;

            public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period) =>
                new CountingTimer(this, inner.CreateTimer(s =>
                {
                    Interlocked.Increment(ref _fired);
                    callback(s);
                }, state, dueTime, period));

            private sealed class CountingTimer(CountingTimeProvider owner, ITimer inner) : ITimer
            {
                public bool Change(TimeSpan dueTime, TimeSpan period)
                {
                    var changed = inner.Change(dueTime, period);
                    Interlocked.Increment(ref owner._changed);
                    return changed;
                }

                public void Dispose() => inner.Dispose();

                public ValueTask DisposeAsync() => inner.DisposeAsync();
            }
        }
    }
}
