using System.ComponentModel.DataAnnotations;

namespace Curiosus.Tools.HttpRequestTimeoutCalculator
{
    /// <summary>
    /// Options for <see cref="IHttpRequestParamsCalculator"/>
    /// </summary>
    public class HttpRequestParamsCalculatorOptions
    {
        /// <summary>
        /// Data rate in MegaByte per second. Must be greater than 0.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int DataRateMBps { get; set; } = 8;

        /// <summary>
        /// Multiplier for timeout calculation. Must be greater than 0.
        /// </summary>
        /// <remarks>
        /// Multiplier used for more precisely calculation because throughput rate is not permanent and
        /// different delays are expected during HTTP request
        /// Used formula: size / <see cref="DataRateMBps"/> * <see cref="TimeoutMultiplier"/>
        /// </remarks>
        [Range(0d, double.MaxValue, MinimumIsExclusive = true)]
        public double TimeoutMultiplier { get; set; } = 1.5;

        /// <inheritdoc />
        public override string ToString()
        {
            return $"{nameof(DataRateMBps)}={DataRateMBps};{nameof(TimeoutMultiplier)}={TimeoutMultiplier}";
        }
    }
}
