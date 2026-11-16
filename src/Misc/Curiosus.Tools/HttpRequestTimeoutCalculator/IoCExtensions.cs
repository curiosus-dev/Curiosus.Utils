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
        /// validated at start.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Configures options in code; bind them from configuration with
        /// <c>.BindConfiguration("HttpRequestParams")</c> on the result.</param>
        /// <returns>Options builder of <see cref="HttpRequestParamsCalculatorOptions"/>.</returns>
        public static OptionsBuilder<HttpRequestParamsCalculatorOptions> AddHttpRequestTimesCalculator(
            this IServiceCollection services,
            Action<HttpRequestParamsCalculatorOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var builder = services.AddOptions<HttpRequestParamsCalculatorOptions>();
            if (configure != null) builder.Configure(configure);
            builder.ValidateDataAnnotations().ValidateOnStart();

            services.TryAddSingleton<IHttpRequestParamsCalculator, HttpRequestParamsCalculator>();

            return builder;
        }
    }
}
