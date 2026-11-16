using System;
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
        /// is set, <see cref="IEMailSender"/> to services. <see cref="UnisenderGoEmailOptions"/> are validated when the app starts.
        /// </summary>
        /// <param name="services">Services.</param>
        /// <param name="configure">Configures the options in code; bind them from configuration on the result.</param>
        /// <param name="useAsDefaultSender">Registers the sender as <see cref="IEMailSender"/> too.</param>
        /// <returns>
        /// The builder of <see cref="UnisenderGoEmailOptions"/>: bind them with <c>.BindConfiguration("UnisenderGo")</c>.
        /// </returns>
        public static OptionsBuilder<UnisenderGoEmailOptions> AddUnisenderGoEmailSender(
            this IServiceCollection services,
            Action<UnisenderGoEmailOptions>? configure = null,
            bool useAsDefaultSender = true)
        {
            ArgumentNullException.ThrowIfNull(services);

            var builder = services.AddOptions<UnisenderGoEmailOptions>();
            if (configure != null) builder.Configure(configure);
            builder.ValidateDataAnnotations().ValidateOnStart();

            services.AddHttpClient(UnisenderGoEmailSender.HttpClientName);
            services.TryAddSingleton<IUnisenderGoEmailSender, UnisenderGoEmailSender>();
            if (useAsDefaultSender)
            {
                services.TryAddSingleton<IEMailSender>(s => s.GetRequiredService<IUnisenderGoEmailSender>());
            }

            return builder;
        }
    }
}
