using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.RequestProcessing.Workers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.RequestProcessing.Postgres.UnitTests;

public class PostgresRequestProcessorOptionsTests
{
    [Fact]
    public async Task AddPostgresRequestProcessor_ValidConfiguration_BindsNestedOptions()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["RequestProcessor:Name"] = "node",
            ["RequestProcessor:WorkersCount"] = "1",
            ["RequestProcessor:PostgresEventReceiver:KeepAliveSec"] = "30",
            ["RequestProcessor:PostgresEventReceiver:EventNames:0"] = "requests",
            ["RequestProcessor:PostgresEventReceiver:EventNames:1"] = "MixedCase",
        });

        await host.StartAsync();
        try
        {
            var options = host.Services.GetRequiredService<IOptions<TestNodeOptions>>().Value;
            options.PostgresEventReceiver.KeepAliveSec.Should().Be(30);
            options.PostgresEventReceiver.ReconnectionPauseMs.Should().Be(100);
            options.PostgresEventReceiver.EventNames.Should().Equal("requests", "MixedCase");
            host.Services.GetRequiredService<TestNodeOptions>().Should().BeSameAs(options);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task AddPostgresRequestProcessor_InvalidNestedOptions_FailsOnStart()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["RequestProcessor:Name"] = "node",
            ["RequestProcessor:WorkersCount"] = "1",
            ["RequestProcessor:PostgresEventReceiver:KeepAliveSec"] = "-1",
            ["RequestProcessor:PostgresEventReceiver:EventNames:0"] = "requests",
            ["RequestProcessor:PostgresEventReceiver:EventNames:1"] = new string('a', 64),
        });

        var act = () => host.StartAsync();

        var failures = String.Join(Environment.NewLine,
            (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Failures);
        failures.Should().Contain("PostgresEventReceiver").And.Contain("KeepAliveSec").And.Contain("EventNames:1");
    }

    [Fact]
    public async Task AddPostgresRequestProcessor_NoEventNames_FailsOnStart()
    {
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["RequestProcessor:Name"] = "node",
            ["RequestProcessor:WorkersCount"] = "1",
        });

        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Failures
            .Should().Contain(f => f.Contains("EventNames"));
    }

    private static IHost BuildHost(IDictionary<string, string?> configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddLogging();
        builder.Services
            .AddPostgresRequestProcessor<
                TestRequest, TestWorker, WorkerBasicExtraParams, TestBootstrapper, TestNodeOptions, TestDispatcher, TestProcessingInfo>()
            .BindConfiguration("RequestProcessor");

        return builder.Build();
    }

    public class TestNodeOptions : PostgresRequestProcessorNodeOptions
    {
    }

    public class TestRequest : IRequest
    {
        public long Id { get; init; }

        public CultureInfo RequestCulture { get; init; } = CultureInfo.InvariantCulture;
    }

    public class TestProcessingInfo : IProcessingRequestInfo
    {
        public DateTime ProcessingStarted { get; } = DateTime.UtcNow;
    }

    public class TestWorker : WorkerBase<TestRequest, WorkerBasicExtraParams, TestProcessingInfo, TestNodeOptions>
    {
        public TestWorker(TestNodeOptions nodeOptions) : base(nodeOptions)
        {
        }

        protected override Task ProcessRequestAsync(TestRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

        protected override Task HandleExceptionAsync(TestRequest request, Exception ex) => Task.CompletedTask;

        protected override TestProcessingInfo GetRequestInfo(TestRequest request) => new();
    }

    public class TestDispatcher
        : RequestDispatcherBase<TestRequest, TestWorker, WorkerBasicExtraParams, TestProcessingInfo, TestNodeOptions>
    {
        public TestDispatcher(TestNodeOptions nodeOptions, EventWaitHandle handle, IReadOnlyList<TestWorker> workers, ILogger logger)
            : base(nodeOptions, handle, workers, logger)
        {
        }

        protected override Task<IReadOnlyList<TestRequest>?> GetRequestsAsync(
            int maxRequestsCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<TestRequest>?>(null);
    }

    /// <summary>
    /// Listens to no database, so the host starts without PostgreSQL.
    /// </summary>
    public class TestBootstrapper : PostgresRequestProcessorBootstrapperBase<
        TestRequest, WorkerBasicExtraParams, TestWorker, TestDispatcher, TestProcessingInfo, TestNodeOptions>
    {
        public TestBootstrapper(TestNodeOptions nodeOptions, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
            : base(nodeOptions, loggerFactory, serviceProvider)
        {
        }

        protected override IReadOnlyList<IEventSource> GetEventSources() => Array.Empty<IEventSource>();

        protected override TestDispatcher CreateDispatcher(IReadOnlyList<IEventSource> monitoredDatabases)
            => new(NodeOptions, EventWaitHandle, CreateWorkers(NodeOptions), LoggerFactory.CreateLogger("dispatcher"));

        protected override WorkerBasicExtraParams CreateWorkerParams(string workerName, ILogger logger) => new(logger);
    }
}
