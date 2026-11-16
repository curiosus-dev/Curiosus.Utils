using System;
using System.Linq;
using Curiosus.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using NLog.Config;
using NLog.Extensions.Logging;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Extension methods for <see cref="IHostApplicationBuilder"/> to log through NLog.
    /// </summary>
    public static class CuriosusNLogExtensions
    {
        /// <summary>
        /// Sets up NLog from the file named by the configuration section (<c>Log</c> by default): replaces the logging
        /// providers with NLog, sets the <c>outputdir</c> (absolute path) and <c>appname</c> variables and makes
        /// <c>${configsetting}</c> read the app configuration.
        /// </summary>
        /// <param name="builder">Application builder.</param>
        /// <param name="configSectionPath">Configuration section the options are bound from; a missing section binds
        /// nothing, so the defaults and <paramref name="configure"/> apply.</param>
        /// <param name="configure">Configures options in code, after the binding.</param>
        /// <param name="configureNLogExtensions">Registers more NLog extensions before NLog.config is loaded.
        /// <c>CuriosusWebHost.CreateBuilder</c> of Curiosus.Hosting.Web already registers the <c>${aspnet-*}</c> layout
        /// renderers; a web app on a plain <c>WebApplication.CreateBuilder</c> passes
        /// <c>extensions =&gt; extensions.RegisterNLogWeb()</c>.</param>
        /// <returns>Options builder of <see cref="CuriosusNLogOptions"/>.</returns>
        /// <remarks>
        /// The app name is the <c>AppName</c> configuration key, or <see cref="IHostEnvironment.ApplicationName"/>.
        /// Paths are relative to the content root. Other values reach NLog.config with
        /// <c>${configsetting:item=Section.Key}</c>.
        /// <para>
        /// The app calls it itself, it is not part of the host defaults: an app that moves to centralized log collection
        /// (OpenTelemetry or console output collected by an agent) removes this call and keeps the rest of its setup.
        /// </para>
        /// <para>
        /// NLog is set up, the section is bound and validation is registered by the first call only. A later call adds
        /// its <paramref name="configure"/> to <see cref="IOptions{TOptions}"/> of <see cref="CuriosusNLogOptions"/>
        /// and ignores <paramref name="configSectionPath"/> and <paramref name="configureNLogExtensions"/>: NLog keeps
        /// the configuration the first call loaded.
        /// </para>
        /// </remarks>
        /// <exception cref="OptionsValidationException">The options are invalid.</exception>
        public static OptionsBuilder<CuriosusNLogOptions> AddCuriosusNLog(
            this IHostApplicationBuilder builder,
            string configSectionPath = CuriosusNLogOptions.SectionName,
            Action<CuriosusNLogOptions>? configure = null,
            Action<ISetupExtensionsBuilder>? configureNLogExtensions = null)
        {
            ArgumentNullException.ThrowIfNull(builder);
            ArgumentException.ThrowIfNullOrWhiteSpace(configSectionPath);

            var services = builder.Services;
            if (services.Any(d => d.ServiceType == typeof(CuriosusNLogMarker)))
            {
                // NLog is a process-wide singleton already set up by the first call: loading it again here would
                // replace its targets while the first configuration is in use
                var optionsBuilder = services.AddOptions<CuriosusNLogOptions>();
                if (configure != null) optionsBuilder.Configure(configure);
                return optionsBuilder;
            }

            var options = builder.Configuration.GetCuriosusOptions(configSectionPath, configure);

            CuriosusNLog.LoadConfiguration(
                options,
                CuriosusAppName.Get(builder.Configuration, builder.Environment),
                builder.Environment.ContentRootPath,
                builder.Configuration,
                configureNLogExtensions);

            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            // disposing the host (CuriosusApp.RunAsync does) flushes NLog, so no LogManager.Shutdown() in Program.cs
            builder.Logging.AddNLog(new NLogProviderOptions { ShutdownOnDispose = true });

            services.AddSingleton<CuriosusNLogMarker>();
            var result = services.AddCuriosusOptions<CuriosusNLogOptions>(configSectionPath);
            if (configure != null) result.Configure(configure);
            return result;
        }

        /// <summary>Marks the service collection whose builder <see cref="AddCuriosusNLog"/> has set up.</summary>
        private sealed class CuriosusNLogMarker;
    }
}
