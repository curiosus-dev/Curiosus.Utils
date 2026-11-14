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
    /// Extension methods for <see cref="IHostApplicationBuilder"/> to set up the standard Curiosus logging.
    /// </summary>
    public static class CuriosusLoggingExtensions
    {
        /// <summary>
        /// Name of the logger that gets "{AppName} is started".
        /// </summary>
        public const string AppStartLoggerName = "appStartEmailLog";

        /// <summary>
        /// Name of the logger that gets "{AppName} is stopped".
        /// </summary>
        public const string AppStopLoggerName = "appStopEmailLog";

        /// <summary>
        /// Sets up NLog from the file named by the <c>Log</c> section: replaces the logging providers with NLog, sets
        /// the <c>outputdir</c> (absolute path) and <c>appname</c> variables, makes <c>${configsetting}</c> read the
        /// app configuration and writes "{AppName} is started/stopped" to the <c>appStartEmailLog</c> /
        /// <c>appStopEmailLog</c> loggers.
        /// </summary>
        /// <param name="builder">Application builder.</param>
        /// <param name="configure">Overrides the options bound from the <c>Log</c> section.</param>
        /// <param name="configureNLogExtensions">Registers more NLog extensions before NLog.config is loaded. A web app
        /// passes <c>extensions =&gt; extensions.RegisterNLogWeb()</c> (NLog.Web.AspNetCore) to use the
        /// <c>${aspnet-*}</c> layout renderers.</param>
        /// <returns>Options builder of <see cref="CuriosusLoggingOptions"/>.</returns>
        /// <remarks>
        /// The app name is the <c>AppName</c> configuration key, or <see cref="IHostEnvironment.ApplicationName"/>.
        /// Paths are relative to the content root. Other values reach NLog.config with <c>${configsetting:item=Section.Key}</c>.
        /// </remarks>
        /// <exception cref="OptionsValidationException">The options are invalid.</exception>
        public static OptionsBuilder<CuriosusLoggingOptions> AddCuriosusLogging(
            this IHostApplicationBuilder builder,
            Action<CuriosusLoggingOptions>? configure = null,
            Action<ISetupExtensionsBuilder>? configureNLogExtensions = null)
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var options = builder.Configuration.GetCuriosusOptions(CuriosusLoggingOptions.SectionName, configure);

            var appName = builder.Configuration[CuriosusLogging.AppNameConfigurationKey] is { Length: > 0 } name
                ? name
                : builder.Environment.ApplicationName;

            CuriosusLogging.LoadConfiguration(
                options,
                appName,
                builder.Environment.ContentRootPath,
                builder.Configuration,
                configureNLogExtensions);

            builder.Logging.ClearProviders();
            builder.Logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Trace);
            builder.Logging.AddNLog();

            builder.Services.AddHostedService(provider =>
                new AppLifecycleLogger(
                    appName,
                    provider.GetRequiredService<IHostApplicationLifetime>(),
                    provider.GetRequiredService<ILoggerFactory>()));

            return builder.Services
                .AddCuriosusOptions<CuriosusLoggingOptions>(CuriosusLoggingOptions.SectionName)
                .Configure(o => configure?.Invoke(o));
        }
    }
}
