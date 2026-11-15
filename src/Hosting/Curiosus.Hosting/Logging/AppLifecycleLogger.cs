using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Writes "{AppName} is started" and "{AppName} is stopped" to the <c>appStartEmailLog</c> / <c>appStopEmailLog</c>
    /// loggers, whatever the logging provider is; with NLog its rules decide where they go (mail, chat, nothing).
    /// </summary>
    internal sealed class AppLifecycleLogger : IHostedService
    {
        public const string AppStartLoggerName = "appStartEmailLog";
        public const string AppStopLoggerName = "appStopEmailLog";

        private readonly string _appName;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly ILogger _startLogger;
        private readonly ILogger _stopLogger;
        private CancellationTokenRegistration _startedRegistration;
        private CancellationTokenRegistration _stoppingRegistration;

        public AppLifecycleLogger(string appName, IHostApplicationLifetime lifetime, ILoggerFactory loggerFactory)
        {
            _appName = appName;
            _lifetime = lifetime;
            _startLogger = loggerFactory.CreateLogger(AppStartLoggerName);
            _stopLogger = loggerFactory.CreateLogger(AppStopLoggerName);
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _startedRegistration = _lifetime.ApplicationStarted.Register(
                () => _startLogger.LogInformation("{AppName} is started", _appName));
            _stoppingRegistration = _lifetime.ApplicationStopping.Register(
                () => _stopLogger.LogInformation("{AppName} is stopped", _appName));

            return Task.CompletedTask;
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _startedRegistration.Dispose();
            _stoppingRegistration.Dispose();

            return Task.CompletedTask;
        }
    }

    internal static class AppLifecycleLoggingExtensions
    {
        /// <summary>
        /// Registers <see cref="AppLifecycleLogger"/>; called by the host defaults (#41).
        /// </summary>
        public static IServiceCollection AddCuriosusAppLifecycleLogging(this IServiceCollection services)
        {
            services.AddHostedService(provider =>
                new AppLifecycleLogger(
                    CuriosusAppName.Get(
                        provider.GetRequiredService<IConfiguration>(),
                        provider.GetRequiredService<IHostEnvironment>()),
                    provider.GetRequiredService<IHostApplicationLifetime>(),
                    provider.GetRequiredService<ILoggerFactory>()));

            return services;
        }
    }
}
