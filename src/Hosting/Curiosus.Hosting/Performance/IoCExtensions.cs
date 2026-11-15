using Curiosus.Tools.Performance;
using Microsoft.Extensions.DependencyInjection;

namespace Curiosus.Hosting.Performance
{
    public static class IoCExtensions
    {
        /// <summary>
        /// Registers <see cref="PerformanceMeasurer"/>.
        /// </summary>
        /// <param name="services"></param>
        /// <returns></returns>
        public static IServiceCollection AddPerformanceMeasures(this IServiceCollection services)
        {
            services.AddCuriosusPerformanceMeasurer();
            return services;
        }
    }
}
