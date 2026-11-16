using System;
using Curiosus.Tools.TempFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Curiosus.Hosting.TempFiles
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/>.
    /// </summary>
    public static class IoCExtensions
    {
        /// <summary>
        /// Adds temp directory cleaner as hosted service, with <see cref="TempFileOptions"/> validated at start.
        /// </summary>
        /// <remarks>
        /// Shares the <see cref="TempFileOptions"/> registration with
        /// <see cref="Curiosus.Tools.TempFiles.IoCExtensions.AddTempFileServices"/>: call both and bind the options once.
        /// </remarks>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Configures options in code; bind them from configuration with
        /// <c>.BindConfiguration("TempFiles")</c> on the result.</param>
        /// <returns>Options builder of <see cref="TempFileOptions"/>.</returns>
        public static OptionsBuilder<TempFileOptions> AddTempDirCleaner(
            this IServiceCollection services,
            Action<TempFileOptions>? configure = null)
        {
            var builder = services.AddTempFileOptions(configure);
            services.AddHostedService<TempDirCleaner>();

            return builder;
        }
    }
}
