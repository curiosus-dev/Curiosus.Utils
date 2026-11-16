using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using Curiosus.RequestProcessing.Workers;
using Microsoft.Extensions.Logging;

namespace Curiosus.RequestProcessing.UnitTests;

public class TestRequest : IRequest
{
    public long Id { get; init; }

    public CultureInfo RequestCulture { get; init; } = CultureInfo.InvariantCulture;
}

public class TestProcessingInfo : IProcessingRequestInfo
{
    public DateTime ProcessingStarted { get; } = DateTime.UtcNow;
}

public class TestWorker<TOptions> : WorkerBase<TestRequest, WorkerBasicExtraParams, TestProcessingInfo, TOptions>
    where TOptions : RequestProcessorNodeOptions
{
    public TestWorker(TOptions nodeOptions) : base(nodeOptions)
    {
    }

    protected override Task ProcessRequestAsync(TestRequest request, CancellationToken cancellationToken) => Task.CompletedTask;

    protected override Task HandleExceptionAsync(TestRequest request, Exception ex) => Task.CompletedTask;

    protected override TestProcessingInfo GetRequestInfo(TestRequest request) => new();
}

public class TestDispatcher<TOptions>
    : RequestDispatcherBase<TestRequest, TestWorker<TOptions>, WorkerBasicExtraParams, TestProcessingInfo, TOptions>
    where TOptions : RequestProcessorNodeOptions
{
    public TestDispatcher(
        TOptions nodeOptions,
        EventWaitHandle manualResetEvent,
        IReadOnlyList<TestWorker<TOptions>> workers,
        ILogger logger) : base(nodeOptions, manualResetEvent, workers, logger)
    {
    }

    protected override Task<IReadOnlyList<TestRequest>?> GetRequestsAsync(
        int maxRequestsCount,
        CancellationToken cancellationToken = default)
        => Task.FromResult<IReadOnlyList<TestRequest>?>(null);
}

/// <summary>
/// Bootstrapper without event sources: it starts the dispatcher and the periodic check only.
/// </summary>
public class TestBootstrapper<TOptions> : RequestProcessorBootstrapperBase<
    TestRequest,
    WorkerBasicExtraParams,
    TestWorker<TOptions>,
    TestDispatcher<TOptions>,
    TestProcessingInfo,
    TOptions>
    where TOptions : RequestProcessorNodeOptions
{
    public TestBootstrapper(TOptions nodeOptions, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        : base(nodeOptions, loggerFactory, serviceProvider)
    {
    }

    public TOptions Options => NodeOptions;

    protected override IReadOnlyList<IEventSource> GetEventSources() => Array.Empty<IEventSource>();

    protected override TestDispatcher<TOptions> CreateDispatcher(IReadOnlyList<IEventSource> monitoredDatabases)
        => new(NodeOptions, EventWaitHandle, CreateWorkers(NodeOptions), LoggerFactory.CreateLogger("dispatcher"));

    protected override Task StartEventReceiverAsync(IEventSource eventSource, CancellationToken cancellationToken = default)
        => Task.CompletedTask;

    protected override WorkerBasicExtraParams CreateWorkerParams(string workerName, ILogger logger) => new(logger);
}
