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
        /// Adds temp directory cleaner as hosted service, with <see cref="TempFileOptions"/> bound from the configuration
        /// section and validated at start.
        /// </summary>
        /// <remarks>
        /// Shares the <see cref="TempFileOptions"/> registration with
        /// <see cref="Curiosus.Tools.TempFiles.IoCExtensions.AddTempFileServices"/>: only the first of these calls binds
        /// its <paramref name="configSectionPath"/> and registers the validation; later calls only add their
        /// <paramref name="configure"/> (see <see cref="Curiosus.Tools.TempFiles.IoCExtensions.AddTempFileOptions"/>).
        /// </remarks>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Configuration section of the options; a missing section leaves the
        /// defaults.</param>
        /// <param name="configure">Configures options in code, after the section is bound.</param>
        /// <returns>Options builder of <see cref="TempFileOptions"/>.</returns>
        public static OptionsBuilder<TempFileOptions> AddTempDirCleaner(
            this IServiceCollection services,
            string configSectionPath = TempFileOptions.SectionName,
            Action<TempFileOptions>? configure = null)
        {
            var builder = services.AddTempFileOptions(configSectionPath, configure);
            services.AddHostedService<TempDirCleaner>();

            return builder;
        }
    }
}
