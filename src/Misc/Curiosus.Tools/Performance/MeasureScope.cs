using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Curiosus.Tools.Performance
{
    /// <summary>
    /// A running measurement of <see cref="PerformanceMeasurer"/>. Disposing it logs the elapsed time in whole
    /// milliseconds; dispose it once. <c>default(MeasureScope).Dispose()</c> does nothing.
    /// </summary>
    public readonly struct MeasureScope : IDisposable
    {
        private static readonly Action<ILogger, string, long, Exception?> LogMeasurement =
            LoggerMessage.Define<string, long>(
                LogLevel.Information,
                new EventId(1, "PerformanceMeasurement"),
                "{MeasurerName} -> {MeasurerSpentTime} ms");

        private readonly ILogger? _logger;
        private readonly string? _name;
        private readonly long _startTimestamp;

        internal MeasureScope(ILogger logger, string name)
        {
            _logger = logger;
            _name = name;
            _startTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Logs the time elapsed since the scope was created.
        /// </summary>
        public void Dispose()
        {
            if (_logger == null) return;

            var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
            LogMeasurement(_logger, _name!, (long)elapsed.TotalMilliseconds, null);
        }
    }
}
