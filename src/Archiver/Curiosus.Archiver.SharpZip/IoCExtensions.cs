using System;
using Curiosus.Tools.TempFiles;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.Archiver.SharpZip
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> for registering SharpZip archiver.
    /// </summary>
    public static class IoCExtensions
    {
        /// <summary>
        /// Adds SharpZip archiver to services, with the temp file services it writes archives with
        /// (<see cref="Curiosus.Tools.TempFiles.IoCExtensions.AddTempFileServices"/>).
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Configuration section of the temp file options; a missing section leaves the
        /// defaults. Only the first temp file registration of the app (this one, <c>AddTempFileServices</c>,
        /// <c>AddTempDirCleaner</c>) binds its section and registers the validation; later ones only add their
        /// <paramref name="configure"/>.</param>
        /// <param name="configure">Configures temp file options in code, after the section is bound.</param>
        /// <returns>Options builder of <see cref="TempFileOptions"/>, validated at start.</returns>
        public static OptionsBuilder<TempFileOptions> AddSharpZipArchiver(
            this IServiceCollection services,
            string configSectionPath = TempFileOptions.SectionName,
            Action<TempFileOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<IArchiver, SharpZipArchiver>();

            // requires temp file subsystem
            return services.AddTempFileServices(configSectionPath, configure);
        }
    }
}
