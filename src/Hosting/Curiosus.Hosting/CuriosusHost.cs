using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Hosting.Systemd;
using Microsoft.Extensions.Hosting.WindowsServices;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Creates host builders with the Curiosus conventions: environment from <c>DOTNET_ENVIRONMENT</c> or
    /// <c>ASPNETCORE_ENVIRONMENT</c>, content root of a service app at its binaries, <c>-c</c> as <c>--config</c>.
    /// </summary>
    public static class CuriosusHost
    {
        /// <summary>
        /// Configuration key of the configuration directory or file, set by <c>-c</c> / <c>--config</c>.
        /// </summary>
        public const string ConfigurationPathKey = "config";

        /// <summary>
        /// Creates a <see cref="HostApplicationBuilder"/> for a worker or a tool. Chain
        /// <see cref="CuriosusHostingExtensions.AddCuriosusDefaults{TBuilder}"/> to it.
        /// </summary>
        /// <param name="args">Command line arguments.</param>
        public static HostApplicationBuilder CreateBuilder(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            // fallbacks go below DOTNET_ variables and arguments, which the builder adds on top of this configuration
            var configuration = new ConfigurationManager();
            var fallbacks = new Dictionary<string, string?>();
            if (GetFallbackEnvironmentName() is { } environment) fallbacks[HostDefaults.EnvironmentKey] = environment;
            if (GetServiceContentRootPath() is { } contentRoot) fallbacks[HostDefaults.ContentRootKey] = contentRoot;
            configuration.AddInMemoryCollection(fallbacks);

            return Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
            {
                Args = MapShortSwitches(args),
                Configuration = configuration,
            });
        }

        /// <summary>
        /// Returns <c>ASPNETCORE_ENVIRONMENT</c> when <c>DOTNET_ENVIRONMENT</c> is not set, so workers deployed with the
        /// variable of web apps get their environment; <c>null</c> keeps the host default (<c>DOTNET_ENVIRONMENT</c>,
        /// then <c>Production</c>).
        /// </summary>
        public static string? GetFallbackEnvironmentName()
        {
            if (!String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT"))) return null;

            var aspNetCoreEnvironment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            return String.IsNullOrWhiteSpace(aspNetCoreEnvironment) ? null : aspNetCoreEnvironment;
        }

        /// <summary>
        /// Returns <see cref="AppContext.BaseDirectory"/> when the app runs as a Windows service or a systemd unit,
        /// whose current directory is not the app directory; <c>null</c> keeps the default (the current directory).
        /// <c>--contentRoot</c> and <c>DOTNET_CONTENTROOT</c> still win.
        /// </summary>
        public static string? GetServiceContentRootPath() =>
            WindowsServiceHelpers.IsWindowsService() || SystemdHelpers.IsSystemdService()
                ? AppContext.BaseDirectory
                : null;

        /// <summary>
        /// Replaces <c>-c</c> with <c>--config</c>: the command line configuration provider ignores short switches that
        /// it has no mapping for.
        /// </summary>
        public static string[] MapShortSwitches(string[] args)
        {
            if (args == null) throw new ArgumentNullException(nameof(args));

            var mapped = new List<string>(args.Length);
            foreach (var arg in args)
            {
                if (arg == "-c")
                    mapped.Add("--" + ConfigurationPathKey);
                else if (arg.StartsWith("-c=", StringComparison.Ordinal))
                    mapped.Add("--" + ConfigurationPathKey + arg.Substring(2));
                else
                    mapped.Add(arg);
            }

            return mapped.ToArray();
        }
    }
}
