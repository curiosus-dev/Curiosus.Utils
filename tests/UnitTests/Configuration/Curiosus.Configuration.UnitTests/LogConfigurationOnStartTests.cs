#nullable enable
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Curiosus.Configuration.UnitTests;

public class LogConfigurationOnStartTests
{
    private const string Secret = "s3cr3t";

    [Fact]
    public void BuildView_OnlyListedSections()
    {
        var view = ConfigurationStartupLogger.BuildView(BuildConfiguration(), Sections("Smtp"));

        view.Should().Contain("Smtp:Host = smtp.local").And.NotContain("Other");
    }

    [Fact]
    public void BuildView_SecretsMasked()
    {
        var view = ConfigurationStartupLogger.BuildView(
            BuildConfiguration(),
            Sections("Smtp", "Auth", "ConnectionStrings", "Bot", "Rabbit"));

        view.Should().NotContain(Secret);
        view.Should().Contain("Smtp:Password = ***")
            .And.Contain("Auth:Key = ***")
            .And.Contain("Bot:Tokens:0 = ***")
            .And.Contain("Rabbit:Url = ***")
            .And.ContainEquivalentOf("ConnectionStrings:Default = host=db;database=app;password=***");
    }

    [Fact]
    public void BuildView_ShowsWinningProvider()
    {
        var view = ConfigurationStartupLogger.BuildView(BuildConfiguration(), Sections("Smtp"));

        var hostLine = view.Split('\n').Single(l => l.StartsWith("Smtp:Host"));
        hostLine.Should().Contain("MemoryConfigurationProvider");
        view.Split('\n').Single(l => l.StartsWith("Smtp:Port")).Should().Contain("= 2525", "the later provider wins");
    }

    [Fact]
    public void BuildView_MissingSection_SaysNotSet()
    {
        ConfigurationStartupLogger.BuildView(BuildConfiguration(), Sections("Nope")).Should().Be("Nope: (not set)");
    }

    [Fact]
    public async Task Host_LogsBeforeHostedServicesStart_SeveralCallsAddUp()
    {
        var logs = new ConcurrentQueue<string>();
        var builder = Host.CreateApplicationBuilder();
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddConfiguration(BuildConfiguration());
        builder.Logging.ClearProviders().AddProvider(new ListLoggerProvider(logs));
        builder.Services.AddHostedService(_ => new RecordingService(logs));
        builder.Services.LogConfigurationOnStart("Smtp").LogConfigurationOnStart("Auth", "Smtp");

        using var host = builder.Build();
        await host.StartAsync();
        await host.StopAsync();

        var entries = logs.ToArray();
        var configIndex = System.Array.FindIndex(entries, e => e.StartsWith("Configuration:"));
        configIndex.Should().BeGreaterThanOrEqualTo(0);
        configIndex.Should().BeLessThan(System.Array.IndexOf(entries, RecordingService.Started));
        entries.Count(e => e.StartsWith("Configuration:")).Should().Be(1);
        entries[configIndex].Should().Contain("Smtp:Host").And.Contain("Auth:Key = ***").And.NotContain(Secret);
    }

    [Fact]
    public void BuildView_NoSections_WholeConfigurationMasked()
    {
        var view = ConfigurationStartupLogger.BuildView(BuildConfiguration(), Sections());

        view.Should().Contain("Smtp:Host = smtp.local").And.Contain("Other:Value = x").And.Contain("AllowedHosts = *");
        view.Should().NotContain(Secret);
    }

    [Fact]
    public void BuildView_NoSections_SectionlessEnvironmentVariablesSkipped()
    {
        System.Environment.SetEnvironmentVariable("CURIOSUS_LOGTEST_FLAT", "noise");
        System.Environment.SetEnvironmentVariable("CURIOSUS_LOGTEST_ALLOWEDHOSTS", "example.com");
        System.Environment.SetEnvironmentVariable("CURIOSUS_LOGTEST_SMTP__PASSWORD", Secret);
        System.Environment.SetEnvironmentVariable("CURIOSUS_LOGTEST_SMTP__USER", "mailer");
        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?> { ["AllowedHosts"] = "*" })
                .AddEnvironmentVariables("CURIOSUS_LOGTEST_")
                .Build();

            var view = ConfigurationStartupLogger.BuildView(configuration, Sections());

            view.Should().NotContain("FLAT", "a sectionless variable only the environment sets is not app configuration");
            view.Should().Contain("ALLOWEDHOSTS = example.com (EnvironmentVariablesConfigurationProvider",
                "it overrides a value of another provider");
            view.Should().Contain("SMTP:PASSWORD = ***").And.Contain("SMTP:USER = mailer").And.NotContain(Secret);
        }
        finally
        {
            foreach (var name in new[] { "FLAT", "ALLOWEDHOSTS", "SMTP__PASSWORD", "SMTP__USER" })
            {
                System.Environment.SetEnvironmentVariable("CURIOSUS_LOGTEST_" + name, null);
            }
        }
    }

    [Fact]
    public void BuildView_ExcludedSections_Skipped()
    {
        var options = Sections();
        options.ExcludedSections.Add("Other");
        options.ExcludedSections.Add("Smtp:Port");

        var view = ConfigurationStartupLogger.BuildView(BuildConfiguration(), options);

        view.Should().NotContain("Other:").And.NotContain("Smtp:Port").And.Contain("Smtp:Host");
    }

    private static ConfigurationStartupLogOptions Sections(params string[] sections)
    {
        var options = new ConfigurationStartupLogOptions();
        options.Sections.AddRange(sections);
        return options;
    }

    private static IConfiguration BuildConfiguration() => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Smtp:Host"] = "smtp.local",
            ["Smtp:Port"] = "25",
            ["Smtp:Password"] = Secret,
            ["Auth:Key"] = Secret,
            ["Bot:Tokens:0"] = Secret,
            ["ConnectionStrings:Default"] = $"Host=db;Database=app;Password={Secret}",
            ["Other:Value"] = "x",
            ["AllowedHosts"] = "*",
            ["Rabbit:Url"] = $"amqp://app:{Secret}@rabbit/vhost",
        })
        .AddInMemoryCollection(new Dictionary<string, string?> { ["Smtp:Port"] = "2525" })
        .Build();

    private sealed class RecordingService(ConcurrentQueue<string> logs) : IHostedService
    {
        public const string Started = "hosted service started";

        public Task StartAsync(System.Threading.CancellationToken cancellationToken)
        {
            logs.Enqueue(Started);
            return Task.CompletedTask;
        }

        public Task StopAsync(System.Threading.CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ListLoggerProvider(ConcurrentQueue<string> logs) : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new ListLogger(logs);

        public void Dispose()
        {
        }

        private sealed class ListLogger(ConcurrentQueue<string> logs) : ILogger
        {
            public System.IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

            public bool IsEnabled(LogLevel logLevel) => true;

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, System.Exception? exception,
                System.Func<TState, System.Exception?, string> formatter) => logs.Enqueue(formatter(state, exception));
        }
    }
}
