using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.RequestProcessing.RabbitMQ.Options;
using Curiosus.RequestProcessing.Workers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.RequestProcessing.RabbitMQ.UnitTests;

public class RabbitMQRequestProcessorOptions_Should
{
    [Fact]
    public void BindNestedReceiverOptions_FromConfiguration()
    {
        // arrange
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["RequestProcessor:Name"] = "node",
            ["RequestProcessor:WorkersCount"] = "4",
            ["RequestProcessor:RabbitMQEventReceiver:UserName"] = "guest",
            ["RequestProcessor:RabbitMQEventReceiver:Password"] = "guest",
            ["RequestProcessor:RabbitMQEventReceiver:QueueName"] = "requests",
            ["RequestProcessor:RabbitMQEventReceiver:QosMultiplier"] = "1.5",
        });

        // act
        var options = host.Services.GetRequiredService<TestNodeOptions>();

        // assert
        options.WorkersCount.Should().Be(4);
        options.RabbitMQEventReceiver.QueueName.Should().Be("requests");
        options.RabbitMQEventReceiver.QosMultiplier.Should().Be(1.5m);
        options.RabbitMQEventReceiver.Port.Should().Be(5672);
        host.Services.GetRequiredService<RequestProcessorNodeOptions>().Should().BeSameAs(options);
    }

    [Fact]
    public async Task FailOnStart_WhenReceiverOptionsAreInvalid()
    {
        // arrange: the host fails before the receiver tries to connect
        using var host = BuildHost(new Dictionary<string, string?>
        {
            ["RequestProcessor:Name"] = "node",
            ["RequestProcessor:WorkersCount"] = "4",
            ["RequestProcessor:RabbitMQEventReceiver:UserName"] = "guest",
            ["RequestProcessor:RabbitMQEventReceiver:QosMultiplier"] = "20",
        });

        // act
        var act = () => host.StartAsync();

        // assert
        var failures = String.Join(Environment.NewLine,
            (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Failures);
        failures.Should().Contain("RabbitMQEventReceiver").And.Contain("Password").And.Contain("QueueName")
            .And.Contain("QosMultiplier");
        failures.Should().NotContain("guest", "values, secrets included, are not printed");
    }

    private static IHost BuildHost(IDictionary<string, string?> configuration)
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddLogging();
        builder.Services
            .AddRabbitMQRequestProcessor<
                TestRequest, TestWorker, WorkerBasicExtraParams, TestBootstrapper, TestNodeOptions, TestDispatcher, TestProcessingInfo>()
            .BindConfiguration("RequestProcessor");

        return builder.Build();
    }

    public class TestNodeOptions : RabbitMQRequestProcessorNodeOptions
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

    public class TestWorker
        : WorkerBase<RabbitMQRequestWrapper<TestRequest>, WorkerBasicExtraParams, TestProcessingInfo, TestNodeOptions>
    {
        public TestWorker(TestNodeOptions nodeOptions) : base(nodeOptions)
        {
        }

        protected override Task ProcessRequestAsync(RabbitMQRequestWrapper<TestRequest> request, CancellationToken cancellationToken)
            => Task.CompletedTask;

        protected override Task HandleExceptionAsync(RabbitMQRequestWrapper<TestRequest> request, Exception ex) => Task.CompletedTask;

        protected override TestProcessingInfo GetRequestInfo(RabbitMQRequestWrapper<TestRequest> request) => new();
    }

    public class TestDispatcher
        : RabbitMQRequestDispatcherBase<TestRequest, TestWorker, WorkerBasicExtraParams, TestProcessingInfo, TestNodeOptions>
    {
        public TestDispatcher(
            TestNodeOptions nodeOptions,
            EventWaitHandle handle,
            IReadOnlyList<TestWorker> workers,
            ILogger logger,
            ConcurrentQueue<RabbitMQEvent> receivedEvents) : base(nodeOptions, handle, workers, logger, receivedEvents)
        {
        }

        protected override Task<IReadOnlyList<RabbitMQRequestWrapper<TestRequest>>?> GetRequestsAsync(
            int maxRequestsCount,
            CancellationToken cancellationToken = default)
            => Task.FromResult<IReadOnlyList<RabbitMQRequestWrapper<TestRequest>>?>(null);
    }

    public class TestBootstrapper : RabbitMQRequestProcessorBootstrapperBase<
        TestRequest, WorkerBasicExtraParams, TestWorker, TestDispatcher, TestProcessingInfo, TestNodeOptions>
    {
        public TestBootstrapper(TestNodeOptions nodeOptions, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
            : base(nodeOptions, loggerFactory, serviceProvider)
        {
        }

        protected override TestDispatcher CreateDispatcher(IReadOnlyList<IEventSource> monitoredDatabases)
            => new(NodeOptions, EventWaitHandle, CreateWorkers(NodeOptions), LoggerFactory.CreateLogger("dispatcher"),
                RabbitMQReceivedEvents);

        protected override WorkerBasicExtraParams CreateWorkerParams(string workerName, ILogger logger) => new(logger);
    }
}
