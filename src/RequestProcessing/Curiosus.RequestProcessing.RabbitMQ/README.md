# Curiosus.RequestProcessing.RabbitMQ

[![NuGet](https://img.shields.io/nuget/v/Curiosus.RequestProcessing.RabbitMQ)](https://www.nuget.org/packages/Curiosus.RequestProcessing.RabbitMQ) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.RequestProcessing.RabbitMQ)](https://www.nuget.org/packages/Curiosus.RequestProcessing.RabbitMQ) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.RequestProcessing.RabbitMQ.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

RabbitMQ transport for [Curiosus.RequestProcessing](https://www.nuget.org/packages/Curiosus.RequestProcessing):
messages from a durable RabbitMQ queue are dispatched to a pool of workers and acknowledged after successful
processing or rejected after a failure.

## Installation

```bash
dotnet add package Curiosus.RequestProcessing.RabbitMQ
```

## Usage

Node options must implement `IRabbitMQRequestProcessorNodeOptions`:

```csharp
public class MyNodeOptions : RequestProcessorNodeOptions, IRabbitMQRequestProcessorNodeOptions
{
    public RabbitMQEventReceiverOptions RabbitMQEventReceiver { get; } = new();
}
```

```yaml
RequestProcessor:
  Name: my-consumer
  WorkersCount: 4
  RabbitMQEventReceiver:
    HostName: localhost    # default localhost
    Port: 5672             # default 5672
    UserName: guest        # required
    Password: guest        # required
    ClientName: my-app     # required
    QueueName: requests    # required, declared as durable on start
    QosMultiplier: 1       # prefetch = WorkersCount * QosMultiplier, 1..20
```

The dispatcher turns received messages (`ReceivedEvents`) into `RabbitMQRequestWrapper<TRequest>`; the base class
confirms or rejects each message in `HandleRequestProcessingCompletionAsync`:

```csharp
public class MyDispatcher : RabbitMQRequestDispatcherBase<
    MyRequest, MyWorker, WorkerBasicExtraParams, MyProcessingInfo, MyNodeOptions>
{
    public MyDispatcher(
        MyNodeOptions nodeOptions,
        EventWaitHandle manualResetEvent,
        IReadOnlyList<MyWorker> workers,
        ILogger logger,
        ConcurrentQueue<RabbitMQEvent> receivedEvents)
        : base(nodeOptions, manualResetEvent, workers, logger, receivedEvents)
    {
    }

    protected override Task<IReadOnlyList<RabbitMQRequestWrapper<MyRequest>>?> GetRequestsAsync(
        int maxRequestsCount,
        CancellationToken cancellationToken = default)
    {
        var result = new List<RabbitMQRequestWrapper<MyRequest>>(maxRequestsCount);
        while (result.Count < maxRequestsCount && ReceivedEvents.TryDequeue(out var rabbitMQEvent))
        {
            var request = JsonSerializer.Deserialize<MyRequest>(rabbitMQEvent.Payload)!;
            result.Add(new RabbitMQRequestWrapper<MyRequest>(request.Id, CultureInfo.InvariantCulture, request, rabbitMQEvent));
        }

        return Task.FromResult<IReadOnlyList<RabbitMQRequestWrapper<MyRequest>>?>(result);
    }
}
```

The bootstrapper derives from `RabbitMQRequestProcessorBootstrapperBase<...>` and implements `CreateDispatcher`
(pass `RabbitMQReceivedEvents` to the dispatcher) and `CreateWorkerParams`. The worker derives from
`WorkerBase<RabbitMQRequestWrapper<MyRequest>, ...>` and reads the message from `SourceRequest`.

```csharp
services.AddRabbitMQRequestProcessor<
    MyRequest, MyWorker, WorkerBasicExtraParams, MyBootstrapper, MyNodeOptions, MyDispatcher, MyProcessingInfo>(
    nodeOptions);
```

`RabbitMQEvent.Payload` is a copy of the message body. `RabbitMQEvent.ReceivedData` is the `BasicDeliverEventArgs`
of [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client) 7: read `BasicProperties` (for example,
`CorrelationId`) and `DeliveryTag` from it, but not `Body`, which is valid only while the message is being delivered.

The receiver uses RabbitMQ automatic recovery and additionally reconnects manually (up to 10 attempts) after
channel failures such as consumer timeouts. Rejected messages are not requeued. The receiver closes its connection
on `StopAsync` or `DisposeAsync`.

A complete consumer and producer is in
[samples/RequestProcessing/RabbitMQ](https://github.com/curiosus-dev/Curiosus.Utils/tree/main/samples/RequestProcessing/RabbitMQ).

## See also

- [Curiosus.RequestProcessing](https://www.nuget.org/packages/Curiosus.RequestProcessing) — workers, dispatcher, bootstrapper
- [Curiosus.RequestProcessing.Postgres](https://www.nuget.org/packages/Curiosus.RequestProcessing.Postgres) — Postgres transport
- [Curiosus.RabbitMQ](https://www.nuget.org/packages/Curiosus.RabbitMQ) — RabbitMQ RPC client
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
