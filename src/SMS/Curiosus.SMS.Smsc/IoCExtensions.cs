using System;
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
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Configures options in code; bind them from configuration with
        /// <c>.BindConfiguration("Smsc")</c> on the result.</param>
        /// <param name="useAsDefaultSender">Registers the sender as <see cref="ISmsSender"/> too, unless one is
        /// registered.</param>
        /// <returns>Options builder, validated at start.</returns>
        public static OptionsBuilder<SmscOptions> AddSmscSmsSender(
            this IServiceCollection services,
            Action<SmscOptions>? configure = null,
            bool useAsDefaultSender = true)
        {
            ArgumentNullException.ThrowIfNull(services);

            var optionsBuilder = services.AddOptions<SmscOptions>();
            if (configure != null) optionsBuilder.Configure(configure);
            optionsBuilder.ValidateDataAnnotations().ValidateOnStart();

            services.AddHttpClient(SmscSender.HttpClientName);
            services.TryAddSingleton<ISmscSender, SmscSender>();

            if (useAsDefaultSender)
            {
                services.TryAddSingleton<ISmsSender>(s => s.GetRequiredService<ISmscSender>());
            }

            return optionsBuilder;
        }
    }
}
