using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Curiosus.Hosting;

/// <summary>
/// Logs the listed configuration sections, secrets masked, before hosted services start.
/// </summary>
internal sealed partial class ConfigurationStartupLogger : IHostedLifecycleService
{
    private readonly IConfiguration _configuration;
    private readonly IReadOnlyList<string> _sections;
    private readonly ILogger<ConfigurationStartupLogger> _logger;

    public ConfigurationStartupLogger(
        IConfiguration configuration,
        ConfigurationStartupLoggerSections sections,
        ILogger<ConfigurationStartupLogger> logger)
    {
        _configuration = configuration;
        _sections = sections.Sections;
        _logger = logger;
    }

    public Task StartingAsync(CancellationToken cancellationToken)
    {
        LogConfiguration(BuildView(_configuration, _sections));
        return Task.CompletedTask;
    }

    /// <summary>
    /// One line per value: <c>Section:Key = value (provider)</c>, secrets masked.
    /// </summary>
    internal static string BuildView(IConfiguration configuration, IEnumerable<string> sections)
    {
        var providers = (configuration as IConfigurationRoot)?.Providers.Reverse().ToArray() ?? [];
        var builder = new StringBuilder();
        foreach (var section in sections)
        {
            var values = configuration.GetSection(section)
                .AsEnumerable()
                .Where(pair => pair.Value != null)
                .OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase)
                .ToArray();
            if (values.Length == 0)
            {
                builder.AppendLine($"{section}: (not set)");
                continue;
            }

            foreach (var (key, value) in values)
            {
                builder.Append(key).Append(" = ").Append(SecretMasker.MaskValue(key, value!));
                var provider = providers.FirstOrDefault(p => p.TryGet(key, out _));
                if (provider != null) builder.Append(" (").Append(provider).Append(')');
                builder.AppendLine();
            }
        }

        return builder.ToString().TrimEnd();
    }

    public Task StartAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StartedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppingAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public Task StoppedAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    [LoggerMessage(Level = LogLevel.Information, Message = "Configuration:\n{Configuration}")]
    private partial void LogConfiguration(string configuration);
}

/// <summary>
/// Sections listed by every <c>LogConfigurationOnStart</c> call.
/// </summary>
internal sealed class ConfigurationStartupLoggerSections
{
    public List<string> Sections { get; } = [];
}
