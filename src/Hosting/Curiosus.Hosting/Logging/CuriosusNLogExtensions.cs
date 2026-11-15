using System;
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
        /// Sets up NLog from the file named by the <c>Log</c> section: replaces the logging providers with NLog, sets
        /// the <c>outputdir</c> (absolute path) and <c>appname</c> variables and makes <c>${configsetting}</c> read the
        /// app configuration.
        /// </summary>
        /// <param name="builder">Application builder.</param>
        /// <param name="configure">Overrides the options bound from the <c>Log</c> section.</param>
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
        /// </remarks>
        /// <exception cref="OptionsValidationException">The options are invalid.</exception>
        public static OptionsBuilder<CuriosusNLogOptions> AddCuriosusNLog(
            this IHostApplicationBuilder builder,
            Action<CuriosusNLogOptions>? configure = null,
            Action<ISetupExtensionsBuilder>? configureNLogExtensions = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var options = builder.Configuration.GetCuriosusOptions(CuriosusNLogOptions.SectionName, configure);

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

            return builder.Services
                .AddCuriosusOptions<CuriosusNLogOptions>(CuriosusNLogOptions.SectionName)
                .Configure(o => configure?.Invoke(o));
        }
    }
}
