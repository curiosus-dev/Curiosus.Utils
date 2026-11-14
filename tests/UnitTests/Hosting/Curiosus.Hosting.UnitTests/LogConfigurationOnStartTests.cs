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

namespace Curiosus.Hosting.UnitTests;

public class LogConfigurationOnStartTests
{
    private const string Secret = "s3cr3t";

    [Fact]
    public void BuildView_OnlyListedSections()
    {
        var view = ConfigurationStartupLogger.BuildView(BuildConfiguration(), ["Smtp"]);

        view.Should().Contain("Smtp:Host = smtp.local").And.NotContain("Other");
    }

    [Fact]
    public void BuildView_SecretsMasked()
    {
        var view = ConfigurationStartupLogger.BuildView(BuildConfiguration(), ["Smtp", "Auth", "ConnectionStrings", "Bot"]);

        view.Should().NotContain(Secret);
        view.Should().Contain("Smtp:Password = ***")
            .And.Contain("Auth:Key = ***")
            .And.Contain("Bot:Tokens:0 = ***")
            .And.ContainEquivalentOf("ConnectionStrings:Default = host=db;database=app;password=***");
    }

    [Fact]
    public void BuildView_ShowsWinningProvider()
    {
        var view = ConfigurationStartupLogger.BuildView(BuildConfiguration(), ["Smtp"]);

        var hostLine = view.Split('\n').Single(l => l.StartsWith("Smtp:Host"));
        hostLine.Should().Contain("MemoryConfigurationProvider");
        view.Split('\n').Single(l => l.StartsWith("Smtp:Port")).Should().Contain("= 2525", "the later provider wins");
    }

    [Fact]
    public void BuildView_MissingSection_SaysNotSet()
    {
        ConfigurationStartupLogger.BuildView(BuildConfiguration(), ["Nope"]).Should().Be("Nope: (not set)");
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
