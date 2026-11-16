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
        /// <param name="configure">Configures temp file options in code; bind them from configuration with
        /// <c>.BindConfiguration("TempFiles")</c> on the result.</param>
        /// <returns>Options builder of <see cref="TempFileOptions"/>, validated at start.</returns>
        public static OptionsBuilder<TempFileOptions> AddSharpZipArchiver(
            this IServiceCollection services,
            Action<TempFileOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            services.TryAddSingleton<IArchiver, SharpZipArchiver>();

            // requires temp file subsystem
            return services.AddTempFileServices(configure);
        }
    }
}
