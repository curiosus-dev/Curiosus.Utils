using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Configuration.EnvironmentVariables;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Curiosus.Configuration;

/// <summary>
/// Logs the configuration, secrets masked, before hosted services start.
/// </summary>
internal sealed partial class ConfigurationStartupLogger : IHostedLifecycleService
{
    private readonly IConfiguration _configuration;
    private readonly ConfigurationStartupLogOptions _options;
    private readonly ILogger<ConfigurationStartupLogger> _logger;

    public ConfigurationStartupLogger(
        IConfiguration configuration,
        IOptions<ConfigurationStartupLogOptions> options,
        ILogger<ConfigurationStartupLogger> logger)
    {
        _configuration = configuration;
        _options = options.Value;
        _logger = logger;
    }

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        LogConfiguration(BuildView(_configuration, _options));
        return Task.CompletedTask;
    }

    /// <summary>
    /// One line per value: <c>Section:Key = value (provider)</c>, secrets masked.
    /// </summary>
    internal static string BuildView(IConfiguration configuration, ConfigurationStartupLogOptions options)
    {
        var providers = (configuration as IConfigurationRoot)?.Providers.Reverse().ToArray() ?? [];
        var builder = new StringBuilder();

        var sections = options.Sections.Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var scopes = sections.Length == 0
            ? new[] { (Name: (string?)null, Values: configuration.AsEnumerable()) }
            : sections.Select(s => (Name: (string?)s, Values: configuration.GetSection(s).AsEnumerable()));

        foreach (var (name, scopeValues) in scopes)
        {
            var values = scopeValues
                .Where(pair => pair.Value != null && !IsExcluded(pair.Key, options.ExcludedSections))
                .Select(pair => (pair.Key, Value: pair.Value!, Providers: providers.Where(p => p.TryGet(pair.Key, out _)).ToArray()))
                .Where(entry => name != null || !IsSectionlessEnvironmentVariable(entry.Key, entry.Providers))
                .OrderBy(entry => entry.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (values.Length == 0)
            {
                if (name != null) builder.AppendLine($"{name}: (not set)");
                continue;
            }

            foreach (var (key, value, keyProviders) in values)
            {
                builder.Append(key).Append(" = ").Append(SecretMasker.MaskValue(key, value));
                if (keyProviders.Length > 0) builder.Append(" (").Append(keyProviders[0]).Append(')');
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static bool IsExcluded(string key, IEnumerable<string> excludedSections)
        => excludedSections.Any(section => key.Equals(section, StringComparison.OrdinalIgnoreCase)
            || key.StartsWith(section + ConfigurationPath.KeyDelimiter, StringComparison.OrdinalIgnoreCase));

    // PATH, HOSTNAME and the like: process environment, not app configuration
    private static bool IsSectionlessEnvironmentVariable(string key, IReadOnlyCollection<IConfigurationProvider> providers)
        => !key.Contains(ConfigurationPath.KeyDelimiter)
            && providers.Count > 0
            && providers.All(p => p is EnvironmentVariablesConfigurationProvider);

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuration:\n{Configuration}")]
    private partial void LogConfiguration(string configuration);
}
