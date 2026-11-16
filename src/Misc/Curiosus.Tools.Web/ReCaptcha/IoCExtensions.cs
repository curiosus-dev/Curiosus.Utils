using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.ReCaptcha
{
    /// <summary>
    /// Registration of <see cref="ReCaptchaService"/>.
    /// </summary>
    public static class IoCExtensions
    {
        /// <summary>
        /// Registers <see cref="ReCaptchaService"/>, its <see cref="ReCaptchaOptions"/> validated when the host starts and
        /// the <c>HttpClient</c> named <see cref="ReCaptchaService.HttpClientName"/>.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Configures options in code; bind them from configuration with
        /// <c>.BindConfiguration("ReCaptcha")</c> on the result.</param>
        /// <returns>Options builder, validated at start.</returns>
        public static OptionsBuilder<ReCaptchaOptions> AddReCaptcha(
            this IServiceCollection services,
            Action<ReCaptchaOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var builder = services.AddOptions<ReCaptchaOptions>();
            if (configure != null) builder.Configure(configure);
            builder.ValidateDataAnnotations().ValidateOnStart();

            services.AddHttpClient(ReCaptchaService.HttpClientName);
            services.TryAddSingleton<ReCaptchaService>();

            return builder;
        }
    }
}
