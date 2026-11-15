using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Runs a built host and turns the outcome into an exit code (<see cref="CuriosusExitCodes"/>).
    /// </summary>
    public static class CuriosusApp
    {
        /// <summary>
        /// Runs a service or web app until it is stopped, then disposes the host.
        /// </summary>
        /// <param name="host">Built host.</param>
        /// <param name="cancellationToken">Stops the app.</param>
        /// <returns>
        /// <see cref="CuriosusExitCodes.Success"/>, <see cref="CuriosusExitCodes.InvalidOptions"/>,
        /// <see cref="CuriosusExitCodes.UnhandledException"/> (also when a <see cref="BackgroundService"/> failed and
        /// stopped the host) or <see cref="CuriosusExitCodes.Cancellation"/>.
        /// </returns>
        public static Task<int> RunAsync(IHost host, CancellationToken cancellationToken = default)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));

            var lifetime = host.Services.GetService<IHostApplicationLifetime>();
            return RunCoreAsync(host, async ct =>
            {
                // not host.RunAsync: it disposes the host before a failure could be logged
                await host.StartAsync(ct);
                await host.WaitForShutdownAsync(ct);
                return CuriosusExitCodes.Success;
            }, () => lifetime?.ApplicationStopping.IsCancellationRequested == true, cancellationToken);
        }

        /// <summary>
        /// Runs a tool: starts the host (initializers and hosted services), runs <paramref name="command"/> in a DI scope,
        /// stops and disposes the host.
        /// </summary>
        /// <param name="host">Built host.</param>
        /// <param name="command">
        /// The tool's work; returns the exit code (non-zero when it failed). Its token is cancelled when the app is
        /// stopped (Ctrl+C, SIGTERM) or <paramref name="cancellationToken"/> is cancelled.
        /// </param>
        /// <param name="cancellationToken">Stops the tool.</param>
        /// <returns>The code of <paramref name="command"/>, or the code of the failure as in <see cref="RunAsync(IHost, CancellationToken)"/>.</returns>
        public static Task<int> RunAsync(
            IHost host,
            Func<IServiceProvider, CancellationToken, Task<int>> command,
            CancellationToken cancellationToken = default)
        {
            if (host == null) throw new ArgumentNullException(nameof(host));
            if (command == null) throw new ArgumentNullException(nameof(command));

            // whether the app was being stopped when the command failed; StopAsync below sets ApplicationStopping anyway
            var stopRequested = false;
            return RunCoreAsync(host, async ct =>
            {
                await host.StartAsync(ct);
                try
                {
                    var lifetime = host.Services.GetRequiredService<IHostApplicationLifetime>();
                    using var stopping = CancellationTokenSource.CreateLinkedTokenSource(ct, lifetime.ApplicationStopping);
                    await using var scope = host.Services.CreateAsyncScope();
                    try
                    {
                        return await command(scope.ServiceProvider, stopping.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        stopRequested = stopping.IsCancellationRequested;
                        throw;
                    }
                }
                finally
                {
                    // also after a failed command, so hosted services stop and "{AppName} is stopped" is written
                    await host.StopAsync(CancellationToken.None);
                }
            }, () => stopRequested, cancellationToken);
        }

        private static async Task<int> RunCoreAsync(
            IHost host,
            Func<CancellationToken, Task<int>> run,
            Func<bool> stopRequested,
            CancellationToken cancellationToken)
        {
            var logger = host.Services.GetService<ILoggerFactory>()?.CreateLogger(typeof(CuriosusApp).FullName!);
            try
            {
                var code = await run(cancellationToken);

                var failed = host.Services.GetServices<IHostedService>()
                    .OfType<BackgroundService>()
                    .FirstOrDefault(s => s.ExecuteTask is { IsFaulted: true });
                if (failed != null)
                {
                    Log(logger, LogLevel.Critical, failed.ExecuteTask!.Exception!, $"{failed.GetType()} failed and stopped the app");
                    return CuriosusExitCodes.UnhandledException;
                }

                return code;
            }
            catch (Exception e) when (IsInvalidOptions(e))
            {
                Log(logger, LogLevel.Critical, e, "Options are invalid");
                return CuriosusExitCodes.InvalidOptions;
            }
            catch (OperationCanceledException e) when (cancellationToken.IsCancellationRequested || stopRequested())
            {
                Log(logger, LogLevel.Warning, e, "The app was cancelled");
                return CuriosusExitCodes.Cancellation;
            }
            catch (Exception e)
            {
                Log(logger, LogLevel.Critical, e, "The app stopped with an unhandled exception");
                return CuriosusExitCodes.UnhandledException;
            }
            finally
            {
                if (host is IAsyncDisposable asyncDisposable)
                    await asyncDisposable.DisposeAsync();
                else
                    host.Dispose();
            }
        }

        // several failed ValidateOnStart registrations come as an AggregateException
        private static bool IsInvalidOptions(Exception e) =>
            e is OptionsValidationException ||
            e is AggregateException aggregate && aggregate.InnerExceptions.All(x => x is OptionsValidationException);

        private static void Log(ILogger? logger, LogLevel level, Exception exception, string message)
        {
            if (logger != null)
                logger.Log(level, exception, message);
            else
                Console.Error.WriteLine($"{message}: {exception}");
        }
    }
}
