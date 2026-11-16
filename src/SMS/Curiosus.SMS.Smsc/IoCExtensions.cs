using System;
using System.Linq;
using System.Net.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.SMS.Smsc
{
    /// <summary>
    /// Registration of the smsc.ru sender.
    /// </summary>
    public static class IoCExtensions
    {
        /// <summary>
        /// Adds <see cref="ISmscSender"/> (and by default <see cref="ISmsSender"/>), the named <see cref="HttpClient"/>
        /// <see cref="SmscSender.HttpClientName"/> and <see cref="SmscOptions"/> validated when the host starts.
        /// </summary>
        /// <remarks>
        /// Only the first call binds its section and registers the validation; later calls add their
        /// <paramref name="configure"/> only, and their <paramref name="configSectionPath"/> is ignored.
        /// </remarks>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Configuration section the options are bound from; a missing section binds
        /// nothing, so options set in <paramref name="configure"/> are enough.</param>
        /// <param name="configure">Configures options in code, after binding: its values override the section.</param>
        /// <param name="useAsDefaultSender">Registers the sender as <see cref="ISmsSender"/> too, unless one is
        /// registered.</param>
        /// <returns>Options builder, validated at start.</returns>
        public static OptionsBuilder<SmscOptions> AddSmscSmsSender(
            this IServiceCollection services,
            string configSectionPath = SmscOptions.SectionName,
            Action<SmscOptions>? configure = null,
            bool useAsDefaultSender = true)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

            var optionsBuilder = services.AddOptions<SmscOptions>();
            if (!services.Any(d => d.ServiceType == typeof(SmscOptionsRegistration)))
            {
                // a second call would bind the section again and report every validation error twice
                services.AddSingleton<SmscOptionsRegistration>();
                optionsBuilder.BindConfiguration(configSectionPath).ValidateDataAnnotations().ValidateOnStart();
            }

            if (configure != null) optionsBuilder.Configure(configure);

            services.AddHttpClient(SmscSender.HttpClientName);
            services.TryAddSingleton<ISmscSender, SmscSender>();

            if (useAsDefaultSender)
            {
                services.TryAddSingleton<ISmsSender>(s => s.GetRequiredService<ISmscSender>());
            }

            return optionsBuilder;
        }

        /// <summary>
        /// Marks that <see cref="AddSmscSmsSender"/> has bound and validated <see cref="SmscOptions"/>.
        /// </summary>
        private sealed class SmscOptionsRegistration
        {
        }
    }
}
