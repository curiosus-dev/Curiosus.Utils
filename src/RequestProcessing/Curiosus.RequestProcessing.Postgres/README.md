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

Node options derive from `PostgresRequestProcessorNodeOptions`. `PostgresEventReceiver` has `EventNames`
(required, channels to `LISTEN` to), `KeepAliveSec` (default 0) and `ReconnectionPauseMs` (default 100); it is
validated with the node options when the host starts.

```csharp
public class MyNodeOptions : PostgresRequestProcessorNodeOptions
{
    [Required]
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
services
    .AddPostgresRequestProcessor<
        MyRequest, MyWorker, WorkerBasicExtraParams, MyBootstrapper, MyNodeOptions, MyDispatcher, MyProcessingInfo>()
    .BindConfiguration("RequestProcessor");
```

```yaml
RequestProcessor:
  Name: my-node
  WorkersCount: 4
  ConnectionString: Host=localhost;Database=app
  PostgresEventReceiver:
    EventNames: [ new_request ]
```

On the database side, send a notification when a request is added and put the channel into
`PostgresEventReceiver.EventNames`. From C#, send it with `NotifyAsync` in the transaction that inserts the request:
the notification goes out on commit and is dropped on rollback.

```csharp
await using var connection = await dataSource.OpenConnectionAsync(ct);
await using var transaction = await connection.BeginTransactionAsync(ct);
// INSERT INTO requests ...
await connection.NotifyAsync("new_request", cancellationToken: ct); // or dataSource.NotifyAsync(...) outside a transaction
await transaction.CommitAsync(ct);
```

From a trigger, use `PERFORM pg_notify('new_request', '');` or `NOTIFY new_request;`.

Channel names are used exactly as written, they are case-sensitive: `MyChannel` in `EventNames` receives
`pg_notify('MyChannel', ...)`, `NOTIFY "MyChannel"` and `NotifyAsync("MyChannel")`, but not an unquoted
`NOTIFY MyChannel`, which PostgreSQL folds to `mychannel`. Lower-case names work everywhere. A name must be non-empty,
at most 63 bytes in UTF-8 and have no NUL character, otherwise options validation fails. The payload of a notification
must be shorter than 8000 bytes.

## See also

- [Migration guides](https://curiosus-dev.github.io/Curiosus.Utils/migrations) — upgrading from `Curiosity.*` 1.x and between major versions
- [Curiosus.RequestProcessing.Postgres 3.0 migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-requestprocessing-postgres/3.0)
- [Curiosus.RequestProcessing](https://www.nuget.org/packages/Curiosus.RequestProcessing) — workers, dispatcher, bootstrapper
- [Curiosus.RequestProcessing.RabbitMQ](https://www.nuget.org/packages/Curiosus.RequestProcessing.RabbitMQ) — RabbitMQ transport
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
