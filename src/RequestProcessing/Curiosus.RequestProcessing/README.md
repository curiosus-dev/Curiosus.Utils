# Curiosus.RequestProcessing

[![NuGet](https://img.shields.io/nuget/v/Curiosus.RequestProcessing)](https://www.nuget.org/packages/Curiosus.RequestProcessing) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.RequestProcessing)](https://www.nuget.org/packages/Curiosus.RequestProcessing) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.RequestProcessing.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Infrastructure for processing requests from a queue by a fixed pool of workers inside a hosted service.
It is transport-agnostic; use it through [Curiosus.RequestProcessing.Postgres](https://www.nuget.org/packages/Curiosus.RequestProcessing.Postgres)
or [Curiosus.RequestProcessing.RabbitMQ](https://www.nuget.org/packages/Curiosus.RequestProcessing.RabbitMQ),
or build your own event source on top of it.

## Installation

```bash
dotnet add package Curiosus.RequestProcessing
```

## Usage

Building blocks (all generic, you inherit from them):

- `IRequest` — a request to process (`Id`, `RequestCulture`; the worker runs with this culture).
- `WorkerBase<...>` — processes one request at a time: implement `ProcessRequestAsync`, `HandleExceptionAsync`
  and `GetRequestInfo` (an `IProcessingRequestInfo` describing the current request).
- `RequestDispatcherBase<...>` — waits for a signal, asks `GetRequestsAsync(maxRequestsCount)` for as many requests
  as there are free workers and hands them out; override `HandleRequestProcessingStartedAsync` /
  `HandleRequestProcessingCompletionAsync` to mark requests as taken or completed.
- `RequestProcessorBootstrapperBase<...>` — hosted service that creates workers and the dispatcher, starts an
  `IEventReceiver` for every `IEventSource`, wakes the dispatcher on each received event and also every
  `EventsPeriodicCheckSec` seconds, and calls `ResetStuckRequestsAsync` on start.
- `RequestProcessorNodeOptions` — `Name` (required, unique node name), `WorkersCount` (required, > 0),
  `EventsPeriodicCheckSec` (default 60), `StateFlushPeriodSec` (default 60, how often workers load is logged).

A worker (resolved from the container, so it can take any registered services):

```csharp
public class MyRequest : IRequest
{
    public long Id { get; init; }
    public CultureInfo RequestCulture { get; init; } = CultureInfo.InvariantCulture;
}

public class MyProcessingInfo : IProcessingRequestInfo
{
    public DateTime ProcessingStarted { get; } = DateTime.UtcNow;
}

public class MyWorker : WorkerBase<MyRequest, WorkerBasicExtraParams, MyProcessingInfo, MyNodeOptions>
{
    public MyWorker(MyNodeOptions nodeOptions) : base(nodeOptions)
    {
    }

    protected override async Task ProcessRequestAsync(MyRequest request, CancellationToken cancellationToken)
    {
        Logger.LogInformation("Processing request {RequestId}", request.Id);
        await Task.Delay(100, cancellationToken);
    }

    protected override Task HandleExceptionAsync(MyRequest request, Exception ex)
    {
        Logger.LogError(ex, "Failed to process request {RequestId}", request.Id);
        return Task.CompletedTask;
    }

    protected override MyProcessingInfo GetRequestInfo(MyRequest request) => new();
}
```

Each worker gets its own logger named `worker_<index>` via `IWorkerExtraParams` (`WorkerBasicExtraParams` or your
own type created in the bootstrapper's `CreateWorkerParams`).

Registration of a custom transport:

```csharp
services.AddRequestProcessor<
    MyRequest, MyWorker, WorkerBasicExtraParams, MyBootstrapper, MyNodeOptions, MyDispatcher, MyProcessingInfo>(
    nodeOptions);
```

It registers the worker (transient), the bootstrapper as a hosted service and the options (as `MyNodeOptions` and
`RequestProcessorNodeOptions`). Transport packages provide `AddPostgresRequestProcessor` and
`AddRabbitMQRequestProcessor` wrappers with ready-made bootstrapper bases.

## See also

- [Curiosus.RequestProcessing.Postgres](https://www.nuget.org/packages/Curiosus.RequestProcessing.Postgres) — Postgres `LISTEN/NOTIFY` as event source
- [Curiosus.RequestProcessing.RabbitMQ](https://www.nuget.org/packages/Curiosus.RequestProcessing.RabbitMQ) — RabbitMQ queue as event source
- [RabbitMQ consumer/producer sample](https://github.com/curiosus-dev/Curiosus.Utils/tree/master/samples/RequestProcessing/RabbitMQ)
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
