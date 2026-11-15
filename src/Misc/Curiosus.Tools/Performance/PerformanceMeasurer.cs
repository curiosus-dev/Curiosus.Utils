using System;
using Microsoft.Extensions.Logging;

namespace Curiosus.Tools.Performance
{
    /// <summary>
    /// Measures how long a piece of code runs and logs it to the <c>performance</c> logger as
    /// <c>{MeasurerName} -&gt; {MeasurerSpentTime} ms</c>.
    /// </summary>
    /// <remarks>
    /// Register it with <see cref="PerformanceMeasurerServiceCollectionExtensions.AddCuriosusPerformanceMeasurer"/>.
    /// For new code prefer metrics (<c>System.Diagnostics.Metrics.Histogram&lt;double&gt;</c>) and traces
    /// (<c>System.Diagnostics.ActivitySource</c>): they aggregate and reach OpenTelemetry without parsing logs.
    /// </remarks>
    public sealed class PerformanceMeasurer
    {
        /// <summary>
        /// Name of the logger the measurements are written to.
        /// </summary>
        public const string LoggerName = "performance";

        private readonly ILogger _logger;

        /// <summary>
        /// <inheritdoc cref="PerformanceMeasurer"/>
        /// </summary>
        /// <param name="loggerFactory">Creates the <c>performance</c> logger.</param>
        public PerformanceMeasurer(ILoggerFactory loggerFactory)
        {
            if (loggerFactory == null) throw new ArgumentNullException(nameof(loggerFactory));

            _logger = loggerFactory.CreateLogger(LoggerName);
        }

        /// <summary>
        /// Starts a measurement; disposing the returned scope logs the elapsed time.
        /// </summary>
        /// <param name="name">Name of the measured code, final at creation (<c>$"Import {id}"</c>).</param>
        /// <returns>Scope to dispose once, with <c>using var _ = measurer.Measure("Import");</c>.</returns>
        public MeasureScope Measure(string name)
        {
            if (String.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));

            return new MeasureScope(_logger, name);
        }
    }
}
