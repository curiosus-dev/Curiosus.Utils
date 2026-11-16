using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools
{
    /// <summary>
    /// Registers options bound from a configuration section so that only the first registration binds and validates.
    /// </summary>
    internal static class BoundOptionsRegistration
    {
        /// <summary>
        /// The first call binds <typeparamref name="TOptions"/> to <paramref name="configSectionPath"/>, adds
        /// <paramref name="configure"/>, data annotations validation and validation on start; later calls only add
        /// their <paramref name="configure"/>, so errors are reported once and their section path is ignored.
        /// </summary>
        public static OptionsBuilder<TOptions> AddBoundOptions<TOptions>(
            this IServiceCollection services,
            string configSectionPath,
            Action<TOptions>? configure)
            where TOptions : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

            var builder = services.AddOptions<TOptions>();
            var isBound = services.Any(d => d.ServiceType == typeof(BindingMarker<TOptions>));
            if (!isBound)
            {
                services.AddSingleton(new BindingMarker<TOptions>());
                builder.BindConfiguration(configSectionPath);
            }

            if (configure != null) builder.Configure(configure);

            if (!isBound)
                builder.ValidateDataAnnotations().ValidateOnStart();

            return builder;
        }

        /// <summary>
        /// Marks that <typeparamref name="TOptions"/> are already bound and validated.
        /// </summary>
        // ReSharper disable once UnusedTypeParameter
        private sealed class BindingMarker<TOptions>;
    }
}
