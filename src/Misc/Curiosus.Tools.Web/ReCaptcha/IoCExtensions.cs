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
        /// Registers <see cref="ReCaptchaService"/>, its <see cref="ReCaptchaOptions"/> bound from the configuration section
        /// and validated when the host starts, and the <c>HttpClient</c> named <see cref="ReCaptchaService.HttpClientName"/>.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Path of the configuration section, <see cref="ReCaptchaOptions.SectionName"/>
        /// by default. A missing section binds nothing.</param>
        /// <param name="configure">Configures options in code, after the section is bound. Only the first call binds its
        /// section and registers validation; later calls add their <paramref name="configure"/> only.</param>
        /// <returns>Options builder, validated at start.</returns>
        public static OptionsBuilder<ReCaptchaOptions> AddReCaptcha(
            this IServiceCollection services,
            string configSectionPath = ReCaptchaOptions.SectionName,
            Action<ReCaptchaOptions>? configure = null)
        {
            var builder = services.AddSectionOptions(configSectionPath, configure);

            services.AddHttpClient(ReCaptchaService.HttpClientName);
            services.TryAddSingleton<ReCaptchaService>();

            return builder;
        }
    }
}
