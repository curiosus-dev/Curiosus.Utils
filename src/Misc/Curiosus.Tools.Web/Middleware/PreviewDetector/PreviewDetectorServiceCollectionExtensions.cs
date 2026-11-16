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
        /// Registers <see cref="PreviewDetectOptions"/>, validated when the host starts, for
        /// <c>app.UsePreviewDetector()</c>.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Configures options in code; bind them from configuration with
        /// <c>.BindConfiguration("PreviewDetect")</c> on the result.</param>
        /// <returns>Options builder, validated at start.</returns>
        public static OptionsBuilder<PreviewDetectOptions> AddPreviewDetector(
            this IServiceCollection services,
            Action<PreviewDetectOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var builder = services.AddOptions<PreviewDetectOptions>();
            if (configure != null) builder.Configure(configure);
            builder.ValidateOnStart();
            services.TryAddEnumerable(
                ServiceDescriptor.Singleton<IValidateOptions<PreviewDetectOptions>, PreviewDetectOptionsValidator>());

            return builder;
        }
    }
}
