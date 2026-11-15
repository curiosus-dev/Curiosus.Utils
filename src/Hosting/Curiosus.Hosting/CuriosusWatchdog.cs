using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Curiosus.Hosting
{
    /// <summary>
    /// A base class for services that run <see cref="ProcessAsync"/> periodically and survive its exceptions.
    /// </summary>
    /// <remarks>
    /// Runs start every period (fixed rate): the first one at start, the next one a period after the previous one started.
    /// A run longer than the period is followed by the next one immediately; missed ticks are not queued.
    /// After an exception the watchdog waits <see cref="ExceptionDelay"/>, runs again and counts the next period from
    /// that run. Stopping the host ends the loop without a warning.
    /// </remarks>
    public abstract class CuriosusWatchdog : BackgroundService
    {
        /// <summary>
        /// Logger of the watchdog.
        /// </summary>
        protected ILogger Logger { get; }

        /// <summary>
        /// Interval between the starts of <see cref="ProcessAsync"/>.
        /// </summary>
        protected TimeSpan WatchdogPeriod { get; }

        /// <summary>
        /// Pause after an exception before the next run, 10 minutes by default.
        /// </summary>
        protected TimeSpan ExceptionDelay { get; }

        /// <summary>
        /// Clock of the timer and the pause after an exception, <see cref="TimeProvider.System"/> by default.
        /// </summary>
        protected TimeProvider TimeProvider { get; }

        /// <summary>
        /// <inheritdoc cref="CuriosusWatchdog"/>
        /// </summary>
        /// <param name="logger">Logger, preferably <c>ILogger&lt;TWatchdog&gt;</c>.</param>
        /// <param name="watchdogPeriod">Interval between the starts of <see cref="ProcessAsync"/>.</param>
        /// <param name="exceptionDelay">Pause after an exception, 10 minutes by default.</param>
        /// <param name="timeProvider">Clock, <see cref="TimeProvider.System"/> by default; <c>FakeTimeProvider</c> in tests.</param>
        protected CuriosusWatchdog(
            ILogger logger,
            TimeSpan watchdogPeriod,
            TimeSpan? exceptionDelay = null,
            TimeProvider? timeProvider = null)
        {
            if (watchdogPeriod <= TimeSpan.Zero)
                throw new ArgumentOutOfRangeException(nameof(watchdogPeriod), watchdogPeriod, "The period must be positive.");

            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            WatchdogPeriod = watchdogPeriod;
            ExceptionDelay = exceptionDelay ?? TimeSpan.FromMinutes(10);
            TimeProvider = timeProvider ?? TimeProvider.System;
        }

        /// <summary>
        /// Returns the period before the next run; read after every run, so an override can take it from
        /// <c>IOptionsMonitor&lt;T&gt;</c> to change it at runtime. A changed period counts from the end of that run.
        /// Returns <see cref="WatchdogPeriod"/> by default.
        /// </summary>
        protected virtual TimeSpan GetPeriod() => WatchdogPeriod;

        /// <summary>
        /// The periodic work.
        /// </summary>
        protected abstract Task ProcessAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Handles an exception of <see cref="ProcessAsync"/>: logs it and waits <see cref="ExceptionDelay"/>.
        /// </summary>
        protected virtual async Task HandleExceptionAsync(Exception exception, CancellationToken stoppingToken = default)
        {
            Logger.LogError(exception, "{Watchdog} failed, retrying in {Delay}", GetType().Name, ExceptionDelay);
            await Task.Delay(ExceptionDelay, TimeProvider, stoppingToken);
        }

        /// <inheritdoc />
        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var timer = new PeriodicTimer(GetPeriod(), TimeProvider);
            try
            {
                while (!stoppingToken.IsCancellationRequested)
                {
                    try
                    {
                        await ProcessAsync(stoppingToken);
                    }
                    catch (Exception e) when (!stoppingToken.IsCancellationRequested)
                    {
                        await HandleExceptionAsync(e, stoppingToken);

                        // the pause was the wait: run again at once, and count the next period from that run
                        timer.Dispose();
                        timer = new PeriodicTimer(GetPeriod(), TimeProvider);
                        continue;
                    }

                    // setting Period restarts the timer, so only a changed period may do it
                    var period = GetPeriod();
                    if (period != timer.Period) timer.Period = period;

                    await timer.WaitForNextTickAsync(stoppingToken);
                }
            }
            catch (Exception) when (stoppingToken.IsCancellationRequested)
            {
                Logger.LogDebug("{Watchdog} stopped", GetType().Name);
            }
            finally
            {
                timer.Dispose();
            }
        }

        /// <inheritdoc />
        public override async Task StartAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Starting {Watchdog}", GetType().Name);
            await base.StartAsync(cancellationToken);
        }

        /// <inheritdoc />
        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            Logger.LogInformation("Stopping {Watchdog}", GetType().Name);
            await base.StopAsync(cancellationToken);
        }
    }
}
