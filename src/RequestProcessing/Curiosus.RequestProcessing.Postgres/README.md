# Curiosus.RequestProcessing.Postgres

[![NuGet](https://img.shields.io/nuget/v/Curiosus.RequestProcessing.Postgres)](https://www.nuget.org/packages/Curiosus.RequestProcessing.Postgres) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.RequestProcessing.Postgres)](https://www.nuget.org/packages/Curiosus.RequestProcessing.Postgres) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.RequestProcessing.Postgres.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Postgres transport for [Curiosus.RequestProcessing](https://www.nuget.org/packages/Curiosus.RequestProcessing):
requests are stored in a Postgres table used as a queue, and workers are woken up by `LISTEN/NOTIFY` events
(plus a periodic check in case an event was lost).

## Installation

```bash
dotnet add package Curiosus.RequestProcessing.Postgres
```

## Usage

Node options must implement `IPostgresRequestProcessorNodeOptions`. `PostgresEventReceiver` has `EventNames`
(required, channels to `LISTEN` to), `KeepAliveSec` (default 0) and `ReconnectionPauseMs` (default 100).

```csharp
public class MyNodeOptions : RequestProcessorNodeOptions, IPostgresRequestProcessorNodeOptions
{
    public PostgresEventReceiverOptions PostgresEventReceiver { get; } = new();

    public string ConnectionString { get; set; } = null!;
}
```

The bootstrapper lists the databases to listen to and creates the dispatcher; the base class starts a
`DbEventReceiver` (with automatic reconnection) for every `MonitoredDatabase`:

```csharp
public class MyBootstrapper : PostgresRequestProcessorBootstrapperBase<
    MyRequest, WorkerBasicExtraParams, MyWorker, MyDispatcher, MyProcessingInfo, MyNodeOptions>
{
    public MyBootstrapper(MyNodeOptions nodeOptions, ILoggerFactory loggerFactory, IServiceProvider serviceProvider)
        : base(nodeOptions, loggerFactory, serviceProvider)
    {
    }

    protected override IReadOnlyList<IEventSource> GetEventSources()
        => new[] { new MonitoredDatabase(NodeOptions.ConnectionString) };

    protected override MyDispatcher CreateDispatcher(IReadOnlyList<IEventSource> monitoredDatabases)
        => new(NodeOptions, EventWaitHandle, CreateWorkers(NodeOptions), LoggerFactory.CreateLogger<MyDispatcher>());

    protected override WorkerBasicExtraParams CreateWorkerParams(string workerName, ILogger logger) => new(logger);

    // optionally: return requests locked by this node before a crash back to the queue
    protected override Task ResetStuckRequestsAsync(
        IReadOnlyList<IEventSource> monitoredDatabases,
        CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
```

The dispatcher (`RequestDispatcherBase`) implements `GetRequestsAsync(maxRequestsCount)`: select and lock up to
`maxRequestsCount` pending requests for this node (`NodeName`) and return them. `MyWorker` and `MyRequest` are
described in [Curiosus.RequestProcessing](https://www.nuget.org/packages/Curiosus.RequestProcessing).

```csharp
services.AddPostgresRequestProcessor<
    MyRequest, MyWorker, WorkerBasicExtraParams, MyBootstrapper, MyNodeOptions, MyDispatcher, MyProcessingInfo>(
    nodeOptions);
```

On the database side, send a notification when a request is added, e.g. `NOTIFY new_request;` from a trigger,
and put `new_request` into `PostgresEventReceiver.EventNames`.

## See also

- [Curiosus.RequestProcessing](https://www.nuget.org/packages/Curiosus.RequestProcessing) — workers, dispatcher, bootstrapper
- [Curiosus.RequestProcessing.RabbitMQ](https://www.nuget.org/packages/Curiosus.RequestProcessing.RabbitMQ) — RabbitMQ transport
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
