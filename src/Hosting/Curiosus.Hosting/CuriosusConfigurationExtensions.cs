using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.CommandLine;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Configuration.Json;
using NetEscapades.Configuration.Yaml;
using Microsoft.Extensions.Hosting;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Extension methods for <see cref="IHostApplicationBuilder"/> to load the YAML configuration files.
    /// </summary>
    public static class CuriosusConfigurationExtensions
    {
        private const string BaseFileName = "config";

        /// <summary>
        /// Adds the YAML configuration files below user secrets, environment variables and command line arguments, which
        /// still override them.
        /// </summary>
        /// <param name="builder">Application builder.</param>
        /// <param name="path">
        /// Directory with the files or a single file. Default: the <c>config</c> key (<c>-c</c> / <c>--config</c>),
        /// then the content root. A relative path is relative to the content root.
        /// </param>
        /// <param name="optional">Whether <c>config.yml</c> (or the single file) may be missing.</param>
        /// <remarks>
        /// From a directory, in this order: <c>config.yml</c>, <c>config.secret.yml</c>, <c>config.{Environment}.yml</c>,
        /// <c>config.{Environment}.secret.yml</c> and, outside <c>Production</c>, <c>config.{Environment}.{UserName}.yml</c>
        /// and <c>config.{Environment}.{UserName}.secret.yml</c> for personal settings. All files except
        /// <c>config.yml</c> are optional. A single file is loaded alone.
        /// </remarks>
        public static TBuilder AddCuriosusConfiguration<TBuilder>(
            this TBuilder builder,
            string? path = null,
            bool optional = false)
            where TBuilder : IHostApplicationBuilder
        {
            if (builder == null) throw new ArgumentNullException(nameof(builder));

            var contentRoot = builder.Environment.ContentRootPath;
            var fullPath = Path.GetFullPath(
                path ?? builder.Configuration[CuriosusHost.ConfigurationPathKey] ?? contentRoot,
                contentRoot);

            var files = new List<IConfigurationSource>();
            if (File.Exists(fullPath))
            {
                files.Add(CreateYamlSource(fullPath, optional: false));
            }
            else
            {
                if (!Directory.Exists(fullPath) && !optional)
                    throw new DirectoryNotFoundException($"Configuration directory \"{fullPath}\" does not exist.");

                foreach (var (file, isOptional) in GetFiles(builder.Environment, optional))
                {
                    files.Add(CreateYamlSource(Path.Combine(fullPath, file), isOptional));
                }
            }

            // the files go below user secrets, environment variables and command line arguments, which still override them
            var sources = builder.Configuration.Sources;
            var index = sources.ToList().FindIndex(IsOverrideSource);
            if (index < 0) index = sources.Count;
            foreach (var file in files)
            {
                sources.Insert(index++, file);
            }

            return builder;
        }

        private static IConfigurationSource CreateYamlSource(string path, bool optional)
        {
            var source = new YamlConfigurationSource { Path = path, Optional = optional, ReloadOnChange = false };
            source.ResolveFileProvider();
            return source;
        }

        private static bool IsOverrideSource(IConfigurationSource source) =>
            source is EnvironmentVariablesConfigurationSource or CommandLineConfigurationSource ||
            source is JsonConfigurationSource { Path: "secrets.json" };

        private static IEnumerable<(string File, bool Optional)> GetFiles(IHostEnvironment environment, bool optional)
        {
            var names = new List<string> { BaseFileName, $"{BaseFileName}.{environment.EnvironmentName}" };
            if (!environment.IsProduction())
            {
                names.Add($"{BaseFileName}.{environment.EnvironmentName}.{Environment.UserName}");
            }

            foreach (var name in names)
            {
                yield return ($"{name}.yml", name != BaseFileName || optional);
                yield return ($"{name}.secret.yml", true);
            }
        }
    }
}
