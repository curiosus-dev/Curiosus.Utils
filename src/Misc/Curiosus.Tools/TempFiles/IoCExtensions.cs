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
        /// Adds all services for working with temp file streams to services: <see cref="TempFileOptions"/> validated at
        /// start, <see cref="ITempFileStreamFactory"/> and an app initializer that creates the temp directory.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Configures options in code; bind them from configuration with
        /// <c>.BindConfiguration("TempFiles")</c> on the result.</param>
        /// <returns>Options builder of <see cref="TempFileOptions"/>.</returns>
        public static OptionsBuilder<TempFileOptions> AddTempFileServices(
            this IServiceCollection services,
            Action<TempFileOptions>? configure = null)
        {
            var builder = services.AddTempFileOptions(configure);

            services.TryAddSingleton<ITempFileStreamFactory, TempFileStreamFactory>();
            services.TryAddEnumerable(ServiceDescriptor.Transient<IAppInitializer, TempFilesInitService>());

            return builder;
        }

        /// <summary>
        /// Registers <see cref="TempFileOptions"/> validated at start, without the services that use them.
        /// </summary>
        /// <remarks>
        /// Every registration method that needs <see cref="TempFileOptions"/> (<see cref="AddTempFileServices"/>,
        /// <c>AddTempDirCleaner</c> of Curiosus.Hosting, ...) calls it, so the options are validated once however many
        /// of them the app calls. Each <paramref name="configure"/> passed is applied, in the order of the calls.
        /// </remarks>
        /// <param name="services">Service collection.</param>
        /// <param name="configure">Configures options in code; bind them from configuration with
        /// <c>.BindConfiguration("TempFiles")</c> on the result.</param>
        /// <returns>Options builder of <see cref="TempFileOptions"/>.</returns>
        public static OptionsBuilder<TempFileOptions> AddTempFileOptions(
            this IServiceCollection services,
            Action<TempFileOptions>? configure = null)
        {
            ArgumentNullException.ThrowIfNull(services);

            var builder = services.AddOptions<TempFileOptions>();
            if (configure != null) builder.Configure(configure);

            // ValidateDataAnnotations() adds a validator on every call: TryAddEnumerable keeps one, so several
            // registration methods don't report each error several times.
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<TempFileOptions>>(
                new DataAnnotationValidateOptions<TempFileOptions>(Options.DefaultName)));
            builder.ValidateOnStart();

            return builder;
        }
    }
}
