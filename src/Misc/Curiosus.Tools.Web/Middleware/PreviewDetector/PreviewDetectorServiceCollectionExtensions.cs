using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.Middleware
{
    /// <summary>
    /// Registration of <see cref="PreviewDetectOptions"/> for <c>app.UsePreviewDetector()</c>.
    /// </summary>
    public static class PreviewDetectorServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="PreviewDetectOptions"/>, bound from the configuration section and validated when the host
        /// starts, for <c>app.UsePreviewDetector()</c>.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Path of the configuration section, <see cref="PreviewDetectOptions.SectionName"/>
        /// by default. A missing section binds nothing.</param>
        /// <param name="configure">Configures options in code, after the section is bound. Only the first call binds its
        /// section and registers validation; later calls add their <paramref name="configure"/> only.</param>
        /// <returns>Options builder, validated at start.</returns>
        public static OptionsBuilder<PreviewDetectOptions> AddPreviewDetector(
            this IServiceCollection services,
            string configSectionPath = PreviewDetectOptions.SectionName,
            Action<PreviewDetectOptions>? configure = null)
        {
            var builder = services.AddSectionOptions(configSectionPath, configure);
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<PreviewDetectOptions>, PreviewDetectOptionsValidator>());

            return builder;
        }
    }
}
