using System;
using System.Linq;
using Curiosus.EMail;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.Email.UnisenderGo
{
    /// <summary>
    /// Extension methods for registering UnisenderGo email services in <see cref="IServiceCollection"/>.
    /// </summary>
    public static class IoCExtensions
    {
        /// <summary>
        /// Adds <see cref="IUnisenderGoEmailSender"/>, its named <c>HttpClient</c> and, when <paramref name="useAsDefaultSender"/>
        /// is set, <see cref="IEMailSender"/> to services. <see cref="UnisenderGoEmailOptions"/> are bound from the
        /// <paramref name="configSectionPath"/> section of <c>IConfiguration</c>, then <paramref name="configure"/> is
        /// applied; they are validated when the app starts.
        /// </summary>
        /// <param name="services">Services.</param>
        /// <param name="configSectionPath">
        /// Path of the configuration section, <see cref="UnisenderGoEmailOptions.SectionName"/> by default. A missing section
        /// binds nothing.
        /// </param>
        /// <param name="configure">Configures the options in code, after the binding.</param>
        /// <param name="useAsDefaultSender">Registers the sender as <see cref="IEMailSender"/> too.</param>
        /// <returns>The builder of <see cref="UnisenderGoEmailOptions"/>.</returns>
        /// <remarks>
        /// Only the first call binds its section and registers the validation; later calls add their
        /// <paramref name="configure"/> only, so every validation error is reported once.
        /// </remarks>
        public static OptionsBuilder<UnisenderGoEmailOptions> AddUnisenderGoEmailSender(
            this IServiceCollection services,
            string configSectionPath = UnisenderGoEmailOptions.SectionName,
            Action<UnisenderGoEmailOptions>? configure = null,
            bool useAsDefaultSender = true)
        {
            ArgumentNullException.ThrowIfNull(services);
            ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

            var builder = services.AddOptions<UnisenderGoEmailOptions>();

            // a second call must not bind or validate again: duplicate validators repeat every error message
            if (!services.Any(d => d.ServiceType == typeof(UnisenderGoEmailOptionsRegistration)))
            {
                services.AddSingleton<UnisenderGoEmailOptionsRegistration>();
                builder.BindConfiguration(configSectionPath);
                if (configure != null) builder.Configure(configure);
                builder.ValidateDataAnnotations().ValidateOnStart();
            }
            else if (configure != null)
            {
                builder.Configure(configure);
            }

            services.AddHttpClient(UnisenderGoEmailSender.HttpClientName);
            services.TryAddSingleton<IUnisenderGoEmailSender, UnisenderGoEmailSender>();
            if (useAsDefaultSender)
            {
                services.TryAddSingleton<IEMailSender>(s => s.GetRequiredService<IUnisenderGoEmailSender>());
            }

            return builder;
        }

        /// <summary>
        /// Marks that the options of the sender are bound and validated.
        /// </summary>
        private sealed class UnisenderGoEmailOptionsRegistration;
    }
}
