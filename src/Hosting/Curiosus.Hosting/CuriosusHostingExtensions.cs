using System;
using Curiosus.Configuration;
using Curiosus.Tools.AppInitializer;
using Curiosus.Tools.Performance;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Extension methods for <see cref="IHostApplicationBuilder"/> to set up a Curiosus app.
    /// </summary>
    public static class CuriosusHostingExtensions
    {
        /// <summary>
        /// Sets up everything a Curiosus app needs:
        /// <list type="bullet">
        /// <item><description>YAML configuration, see <see cref="CuriosusConfigurationExtensions.AddCuriosusConfiguration{TBuilder}"/>;</description></item>
        /// <item><description>the configuration logged on start, secrets masked;</description></item>
        /// <item><description><see cref="TimeProvider.System"/> unless a clock is registered, <c>PerformanceMeasurer</c>, localization;</description></item>
        /// <item><description>before hosted services start: the <c>Culture</c> section applied process-wide, <c>UniqueIdGenerator</c>
        /// initialized from <c>UniqueId:GeneratorId</c>, every <see cref="IAppInitializer"/> run;</description></item>
        /// <item><description>"{AppName} is started/stopped" written to the <c>appStartEmailLog</c> / <c>appStopEmailLog</c> loggers;</description></item>
        /// <item><description>systemd and Windows service lifetimes (no-op outside a service).</description></item>
        /// </list>
        /// Logging providers are not changed: call <c>AddCuriosusNLog()</c> to log through NLog.
        /// </summary>
        /// <param name="builder">Application builder.</param>
        /// <param name="configure">Configures <see cref="CuriosusHostOptions"/>.</param>
        /// <returns>The same builder.</returns>
        public static TBuilder AddCuriosusDefaults<TBuilder>(
            this TBuilder builder,
            Action<CuriosusHostOptions>? configure = null)
            where TBuilder : IHostApplicationBuilder
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var options = new CuriosusHostOptions();
            configure?.Invoke(options);

            builder.AddCuriosusConfiguration(options.ConfigurationPath, options.IsConfigurationOptional);

            var services = builder.Services;
            if (options.LogConfigurationOnStart)
            {
                services.LogConfigurationOnStart();
            }

            services.TryAddSingleton(TimeProvider.System);
            services.AddCuriosusPerformanceMeasurer();
            services.AddLocalization(o => o.ResourcesPath = "Resources");

            services.TryAddEnumerable(ServiceDescriptor.Transient<IAppInitializer, FireAndForgetInitializer>());
            services.TryAddEnumerable(ServiceDescriptor.Singleton<IHostedService, AppInitializerRunner>());
            services.AddCuriosusAppLifecycleLogging();

            services.AddSystemd();
            services.AddWindowsService();

            return builder;
        }
    }
}
