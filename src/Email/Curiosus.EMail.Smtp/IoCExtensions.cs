using System;
using Curiosus.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Curiosus.EMail.Smtp
{
    public static class IoCExtensions
    {
        /// <summary>
        /// Adds SMTP EMail sender to services.
        /// </summary>
        public static IServiceCollection AddSmtpEMailSender(
            this IServiceCollection services,
            ISmtpEMailOptions options,
            bool useAsDefaultSender = true)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (options == null) throw new ArgumentNullException(nameof(options));

            options.AssertValid();
            
            services.TryAddSingleton(options);
            services.TryAddSingleton<ISmtpEMailSender, SmtpEMailSender>();
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, SmtpSecurityStartupCheck>());

            if (useAsDefaultSender)
            {
                services.TryAddSingleton<IEMailSender>(s => s.GetRequiredService<ISmtpEMailSender>());
            }

            return services;
        }
    }
}