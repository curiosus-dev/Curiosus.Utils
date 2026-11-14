using System;
using System.IO;
using System.Reflection;
using Microsoft.Extensions.Configuration;
using NLog;
using NLog.Config;
using NLog.Extensions.Logging;

namespace Curiosus.Hosting
{
    internal static class CuriosusLogging
    {
        public const string OutputDirectoryVariable = "outputdir";
        public const string AppNameVariable = "appname";
        public const string AppNameConfigurationKey = "AppName";

        /// <summary>
        /// Loads the NLog configuration file and sets the <c>outputdir</c> and <c>appname</c> variables.
        /// Variables that the file doesn't declare are added, so the file is free to ignore them.
        /// </summary>
        public static void LoadConfiguration(
            CuriosusLoggingOptions options,
            string appName,
            string basePath,
            IConfiguration? configuration,
            bool registerNLogWeb)
        {
            var configurationPath = Path.GetFullPath(options.LogConfigurationPath, basePath);
            var outputDirectory = Path.GetFullPath(options.LogOutputDirectory, basePath);

            var factory = LogManager.Setup()
                .SetupExtensions(extensions =>
                {
                    if (configuration != null) extensions.RegisterConfigSettings(configuration);
                    if (registerNLogWeb) RegisterNLogWeb(extensions);
                })
                .LoadConfigurationFromFile(configurationPath, optional: false)
                .LogFactory;

            var variables = factory.Configuration!.Variables;
            variables[OutputDirectoryVariable] = outputDirectory;
            variables[AppNameVariable] = appName;

            factory.ReconfigExistingLoggers();
        }

        /// <summary>
        /// Registers the NLog.Web <c>${aspnet-*}</c> renderers of a web app. Hosting has no dependency on ASP.NET Core,
        /// so <c>NLog.Web.AspNetCore</c> is looked up by name: a web app that doesn't reference it gets no renderers.
        /// </summary>
        private static void RegisterNLogWeb(ISetupExtensionsBuilder extensions)
        {
            try
            {
                extensions.RegisterAssembly(Assembly.Load("NLog.Web.AspNetCore"));
            }
            catch (FileNotFoundException)
            {
            }
        }
    }
}
