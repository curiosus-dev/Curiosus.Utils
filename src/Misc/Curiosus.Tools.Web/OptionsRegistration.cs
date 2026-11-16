using System;
using System.Linq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web
{
    /// <summary>
    /// Registration of options bound from a configuration section, shared by the <c>AddXxx</c> methods of the package.
    /// </summary>
    internal static class OptionsRegistration
    {
        /// <summary>
        /// Binds <typeparamref name="TOptions"/> to <paramref name="configSectionPath"/>, then applies
        /// <paramref name="configure"/>, validates data annotations and validates on start. A missing section binds nothing.
        /// </summary>
        /// <remarks>
        /// Binding and validation are registered by the first call only, so that a second <c>AddXxx</c> doesn't report
        /// every error twice: the section of the first call is bound, <paramref name="configure"/> of every call applies.
        /// </remarks>
        public static OptionsBuilder<TOptions> AddSectionOptions<TOptions>(
            this IServiceCollection services,
            string configSectionPath,
            Action<TOptions>? configure)
            where TOptions : class
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

            var builder = services.AddOptions<TOptions>();
            if (!services.Any(d => d.ServiceType == typeof(SectionBinding<TOptions>)))
            {
                services.AddSingleton(new SectionBinding<TOptions>());
                builder.BindConfiguration(configSectionPath).ValidateDataAnnotations().ValidateOnStart();
            }

            if (configure != null) builder.Configure(configure);

            return builder;
        }

        // marks that the section of TOptions is bound and validated
        private sealed class SectionBinding<TOptions>;
    }
}
