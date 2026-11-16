using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.HttpRequestTimeoutCalculator
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to register <see cref="IHttpRequestParamsCalculator"/>
    /// </summary>
    public static class IoCExtensions
    {
        /// <summary>
        /// Adds to IoC <see cref="IHttpRequestParamsCalculator"/> with <see cref="HttpRequestParamsCalculatorOptions"/>
        /// bound from the configuration section and validated at start.
        /// </summary>
        /// <remarks>
        /// Only the first call binds its <paramref name="configSectionPath"/> and registers the validation, so errors
        /// are reported once; later calls only add their <paramref name="configure"/> and their section path is ignored.
        /// </remarks>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Configuration section of the options; a missing section leaves the
        /// defaults.</param>
        /// <param name="configure">Configures options in code, after the section is bound.</param>
        /// <returns>Options builder of <see cref="HttpRequestParamsCalculatorOptions"/>.</returns>
        public static OptionsBuilder<HttpRequestParamsCalculatorOptions> AddHttpRequestTimesCalculator(
            this IServiceCollection services,
            string configSectionPath = HttpRequestParamsCalculatorOptions.SectionName,
            Action<HttpRequestParamsCalculatorOptions>? configure = null)
        {
            var builder = services.AddBoundOptions(configSectionPath, configure);

            services.TryAddSingleton<IHttpRequestParamsCalculator, HttpRequestParamsCalculator>();

            return builder;
        }
    }
}
