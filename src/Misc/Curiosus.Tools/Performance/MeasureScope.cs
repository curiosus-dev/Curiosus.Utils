using System;
using System.Diagnostics;
using Microsoft.Extensions.Logging;

namespace Curiosus.Tools.Performance
{
    /// <summary>
    /// A running measurement of <see cref="PerformanceMeasurer"/>. Disposing it logs the elapsed time in whole
    /// milliseconds; only the first <see cref="Dispose"/> logs.
    /// </summary>
    public sealed class MeasureScope : IDisposable
    {
        private static readonly Action<ILogger, string, long, Exception?> LogMeasurement =
            LoggerMessage.Define<string, long>(
                LogLevel.Information,
                new EventId(1, "PerformanceMeasurement"),
                "{MeasurerName} -> {MeasurerSpentTime} ms");

        private readonly ILogger _logger;
        private readonly long _startTimestamp;
        private string _name;
        private int _disposed;

        internal MeasureScope(ILogger logger, string name)
        {
            _logger = logger;
            _name = name;
            _startTimestamp = Stopwatch.GetTimestamp();
        }

        /// <summary>
        /// Replaces the name the measurement is logged with, for example when the id of the measured item is known
        /// only inside the measured block.
        /// </summary>
        /// <param name="name">New name (<c>$"Import {id}"</c>).</param>
        public void ReplaceName(string name)
        {
            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));

            _name = name;
        }

        /// <summary>
        /// Logs the time elapsed since the scope was created.
        /// </summary>
        public void Dispose()
        {
            if (System.Threading.Interlocked.Exchange(ref _disposed, 1) != 0) return;

            var elapsed = Stopwatch.GetElapsedTime(_startTimestamp);
            LogMeasurement(_logger, _name, (long)elapsed.TotalMilliseconds, null);
        }
    }
}
