using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.EMail.Smtp
{
    /// <summary>
    /// Extension methods for registering the SMTP EMail sender in <see cref="IServiceCollection"/>.
    /// </summary>
    public static class IoCExtensions
    {
        /// <summary>
        /// Adds <see cref="ISmtpEMailSender"/> and, when <paramref name="useAsDefaultSender"/> is set, <see cref="IEMailSender"/>
        /// to services. <see cref="SmtpEMailOptions"/> are validated when the app starts.
        /// </summary>
        /// <param name="services">Services.</param>
        /// <param name="configure">Configures the options in code; bind them from configuration on the result.</param>
        /// <param name="useAsDefaultSender">Registers the sender as <see cref="IEMailSender"/> too.</param>
        /// <returns>The builder of <see cref="SmtpEMailOptions"/>: bind them with <c>.BindConfiguration("Smtp")</c>.</returns>
        public static OptionsBuilder<SmtpEMailOptions> AddSmtpEMailSender(
            this IServiceCollection services,
            Action<SmtpEMailOptions>? configure = null,
            bool useAsDefaultSender = true)
        {
            ArgumentNullException.ThrowIfNull(services);

            var builder = services.AddOptions<SmtpEMailOptions>();
            if (configure != null) builder.Configure(configure);
            builder.ValidateDataAnnotations().ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<SmtpEMailOptions>, SmtpEMailOptionsValidator>());

            services.TryAddSingleton<ISmtpEMailSender, SmtpEMailSender>();
            if (useAsDefaultSender)
            {
                services.TryAddSingleton<IEMailSender>(s => s.GetRequiredService<ISmtpEMailSender>());
            }

            return builder;
        }
    }
}
