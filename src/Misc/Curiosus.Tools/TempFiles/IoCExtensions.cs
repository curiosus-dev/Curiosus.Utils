using System;
using Curiosus.Tools.AppInitializer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.TempFiles
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/>.
    /// </summary>
    public static class IoCExtensions
    {
        /// <summary>
        /// Adds all services for working with temp file streams to services: <see cref="TempFileOptions"/> bound from
        /// the configuration section and validated at start, <see cref="ITempFileStreamFactory"/> and an app
        /// initializer that creates the temp directory.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Configuration section of the options; a missing section leaves the
        /// defaults. See <see cref="AddTempFileOptions"/> for several registrations.</param>
        /// <param name="configure">Configures options in code, after the section is bound.</param>
        /// <returns>Options builder of <see cref="TempFileOptions"/>.</returns>
        public static OptionsBuilder<TempFileOptions> AddTempFileServices(
            this IServiceCollection services,
            string configSectionPath = TempFileOptions.SectionName,
            Action<TempFileOptions>? configure = null)
        {
            var builder = services.AddTempFileOptions(configSectionPath, configure);

            services.TryAddSingleton<ITempFileStreamFactory, TempFileStreamFactory>();
            services.TryAddEnumerable(ServiceDescriptor.Transient<IAppInitializer, TempFilesInitService>());

            return builder;
        }

        /// <summary>
        /// Registers <see cref="TempFileOptions"/> bound from the configuration section and validated at start, without
        /// the services that use them.
        /// </summary>
        /// <remarks>
        /// Every registration method that needs <see cref="TempFileOptions"/> (<see cref="AddTempFileServices"/>,
        /// <c>AddTempDirCleaner</c> of Curiosus.Hosting, <c>AddSharpZipArchiver</c> of Curiosus.Archiver.SharpZip)
        /// calls it. Only the first of these calls binds its <paramref name="configSectionPath"/> and registers the
        /// validation, so errors are reported once; later calls only add their <paramref name="configure"/> and their
        /// section path is ignored. Each <paramref name="configure"/> is applied after the binding, in the order of the
        /// calls.
        /// </remarks>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Configuration section of the options; a missing section leaves the
        /// defaults.</param>
        /// <param name="configure">Configures options in code, after the section is bound.</param>
        /// <returns>Options builder of <see cref="TempFileOptions"/>.</returns>
        public static OptionsBuilder<TempFileOptions> AddTempFileOptions(
            this IServiceCollection services,
            string configSectionPath = TempFileOptions.SectionName,
            Action<TempFileOptions>? configure = null)
        {
            return services.AddBoundOptions(configSectionPath, configure);
        }
    }
}
