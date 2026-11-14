using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Curiosus.Tools
{
    /// <summary>
    /// Registration of the clock.
    /// </summary>
    public static class IoCExtensions
    {
        /// <summary>
        /// Registers <see cref="TimeProvider.System"/> as <see cref="TimeProvider"/> unless a provider is already
        /// registered, so a <c>DbSyncTimeProvider</c> or a <c>FakeTimeProvider</c> registered earlier wins.
        /// </summary>
        public static IServiceCollection AddCuriosusTimeProvider(this IServiceCollection services)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton(TimeProvider.System);

            return services;
        }
    }
}
