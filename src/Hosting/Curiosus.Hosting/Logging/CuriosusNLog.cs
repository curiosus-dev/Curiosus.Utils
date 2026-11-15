using System;
using System.IO;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;

namespace Curiosus.Hosting
{
    internal static class CuriosusNLog
    {
        public const string OutputDirectoryVariable = "outputdir";
        public const string AppNameVariable = "appname";

        /// <summary>
        /// Loads the NLog configuration file and sets the <c>outputdir</c> and <c>appname</c> variables.
        /// Variables that the file doesn't declare are added, so the file is free to ignore them.
        /// </summary>
        public static void LoadConfiguration(
            CuriosusNLogOptions options,
            string appName,
            string basePath,
            IConfiguration configuration,
            Action<ISetupExtensionsBuilder>? configureNLogExtensions)
        {
            var configurationPath = Path.GetFullPath(options.LogConfigurationPath, basePath);
            var outputDirectory = Path.GetFullPath(options.LogOutputDirectory, basePath);

            var factory = LogManager.Setup()
                .SetupExtensions(extensions =>
                {
                    extensions.RegisterConfigSettings(configuration);
                    configureNLogExtensions?.Invoke(extensions);
                })
                .LoadConfigurationFromFile(configurationPath, optional: false)
                .LogFactory;

            var variables = factory.Configuration!.Variables;
            variables[OutputDirectoryVariable] = outputDirectory;
            variables[AppNameVariable] = appName;

            factory.ReconfigExistingLoggers();
        }
    }
}
