using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Curiosus.Tools.Performance
{
    /// <summary>
    /// Registration of <see cref="PerformanceMeasurer"/>.
    /// </summary>
    public static class PerformanceMeasurerServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="PerformanceMeasurer"/> as a singleton, unless it is already registered.
        /// </summary>
        public static IServiceCollection AddCuriosusPerformanceMeasurer(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.TryAddSingleton<PerformanceMeasurer>();

            return services;
        }
    }
}
