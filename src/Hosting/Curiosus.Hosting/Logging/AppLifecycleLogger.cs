using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Writes "{AppName} is started" and "{AppName} is stopped"; NLog.config rules decide where they go.
    /// </summary>
    internal sealed class AppLifecycleLogger : IHostedService
    {
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
            _startLogger = loggerFactory.CreateLogger(CuriosusLoggingExtensions.AppStartLoggerName);
            _stopLogger = loggerFactory.CreateLogger(CuriosusLoggingExtensions.AppStopLoggerName);
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
}
