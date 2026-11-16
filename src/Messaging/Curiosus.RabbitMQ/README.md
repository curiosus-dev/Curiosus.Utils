# Curiosus.RabbitMQ

[![NuGet](https://img.shields.io/nuget/v/Curiosus.RabbitMQ)](https://www.nuget.org/packages/Curiosus.RabbitMQ) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.RabbitMQ)](https://www.nuget.org/packages/Curiosus.RabbitMQ) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.RabbitMQ.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

RabbitMQ for .NET apps: one shared connection with automatic recovery, a publisher that completes only when the broker
confirmed the message, and a request-reply (RPC) client. Built on the async API of
[RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client) 7.

## Installation

```bash
dotnet add package Curiosus.RabbitMQ
```

## Configuration

```yaml
RabbitMQ:
  ConnectionUri: amqps://app:secret@rabbit.example.com:5671/prod  # keep it in secrets: it has the password
  ClientName: orders-api-1      # connection name in the management UI; default: the machine name
  PublishTimeout: "00:00:10"    # default: 30 seconds
  PublisherChannelCount: 4      # default: 4
```

```csharp
builder.Services.AddCuriosusRabbitMQ(); // section "RabbitMQ"
```

`AddCuriosusRabbitMQ` binds `RabbitMQOptions` and validates them when the host starts: the app doesn't start with a
missing or relative URI, a scheme other than `amqp`/`amqps`, a `ClientName` outside `^[A-Za-z0-9._-]{1,64}$` or a
`PublishTimeout` that is not positive. Validation errors never contain the URI. Escape reserved characters of the user
name and the password in the URI (`:` as `%3A`, `@` as `%40`); the path is the virtual host. For `amqps` the server
certificate must be valid for the host of the URI.

It registers `IRabbitConnectionProvider`, `IRabbitPublisher`, `IRabbitMQMessageSerializer` and
`RabbitMqRpcClientFactory` as singletons.

## Publishing

```csharp
public record OrderPaid(long OrderId, decimal Amount);

public class OrdersService(IRabbitPublisher publisher)
{
    public Task OnPaidAsync(long orderId, decimal amount, CancellationToken cancellationToken)
        => publisher.PublishAsync("orders", "paid", new OrderPaid(orderId, amount), cancellationToken: cancellationToken);
}
```

`PublishAsync` serializes the message to JSON, publishes it with publisher confirms and completes when the broker
confirmed it. The message is persistent and has `content-type: application/json`, a `message-id` (a GUID v7) and a
timestamp; set `RabbitPublishProperties` for your own `MessageId`, `CorrelationId`, headers, a transient message or
`Mandatory`.

```csharp
await publisher.PublishAsync(
    "orders",
    "paid",
    message,
    new RabbitPublishProperties
    {
        MessageId = $"order-{orderId}-paid",   // the same id when you publish it again
        CorrelationId = checkoutId,
        Mandatory = true,                       // fail with Returned when no queue is bound for it
        Headers = new Dictionary<string, object?> { ["tenant"] = tenant }
    },
    cancellationToken);
```

### Failures

A message that the broker didn't confirm fails with `RabbitPublishException`; its `Reason` says why:

| Reason | What happened | Did the message arrive? |
|---|---|---|
| `Nacked` | the broker nacked it | no |
| `Returned` | `Mandatory` and no queue is bound for it; `ReplyCode` 312 | no |
| `ChannelClosed` | the broker closed the channel, for example the exchange doesn't exist (`ReplyCode` 404); other publishes in flight on the channel fail too | unknown |
| `ConnectionLost` | the connection closed while the publish was in flight | unknown |
| `Timeout` | no connection, no channel or no confirm within `PublishTimeout`, for example the broker is down or blocked the connection | unknown |

Cancelling the token throws `OperationCanceledException`. The exception has the `Exchange`, `RoutingKey` and
`MessageId` of the message; the publisher doesn't log failures it throws.

While the connection recovers after a network failure or a broker restart, publishes wait for it and are sent on the
recovered connection, up to `PublishTimeout` for the whole call. Nothing is buffered in memory, and a message is never
sent twice by the publisher.

### Idempotent consumers

After `ChannelClosed`, `ConnectionLost` or `Timeout` the message may have reached the broker. If you publish it again,
consumers can get it twice: make them idempotent, for example by remembering processed `message-id`s, and set your own
`MessageId` so a retry carries the same one. A message no queue is bound for is confirmed and dropped by the broker
unless it is `Mandatory`.

### Declare exchanges at startup

The publisher doesn't declare exchanges or queues. A publish to a missing exchange makes the broker close the channel,
which fails every publish in flight on it; the publisher spreads publishes over `PublisherChannelCount` channels and
replaces a closed one, but declare what you publish to once at startup:

```csharp
public class RabbitTopology(IRabbitConnectionProvider connectionProvider) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var connection = await connectionProvider.GetConnectionAsync(cancellationToken);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync("orders", ExchangeType.Direct, durable: true, cancellationToken: cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

## Connection

`IRabbitConnectionProvider.GetConnectionAsync` returns the one connection of the app, named `ClientName`, opened on
first use with automatic and topology recovery. A failed attempt to open it is not remembered: the next call tries
again. Open your own channels on it and close them; the connection itself is closed when the service provider is
disposed. The provider logs opening, shutdown, blocking and recovery of the connection with the host, port, virtual host
and `ClientName`, never the URI.

## Serialization

Messages are serialized with `System.Text.Json` and `RabbitMQJson.DefaultOptions`, the options of the RPC client:
case-insensitive property names, public fields, numbers from strings and unescaped non-ASCII characters. To use another
format, register your own `IRabbitMQMessageSerializer` as a singleton, before or after `AddCuriosusRabbitMQ`.

## RPC client

The RPC client sends a JSON request to a queue and awaits the reply on a dedicated response queue, with automatic and
manual connection recovery and resending. Each client has its own connection, named `{ClientName}_rpc_client`.

```csharp
// default correlation ids come from UniqueIdGenerator (Curiosus.Tools): initialize it once per process
UniqueIdGenerator.Initialize(generatorId: 1);

// rpcClientFactory is an injected RabbitMqRpcClientFactory; CreateClientAsync connects and declares both queues
await using var client = await rpcClientFactory.CreateClientAsync(
    "balance_requests",
    cancellationToken: cancellationToken);

var response = await client.SendWithAutoAcknowledgeAsync<BalanceResponse, BalanceRequest>(
    new BalanceRequest(accountId),
    cancellationToken: cancellationToken);
```

Requests go to the default exchange, routed by the queue name; pass `requestExchange` to `CreateClientAsync` to publish
them to another exchange with the queue name as the routing key. The reply queue is named
`{requestQueue}_responses_{ClientName}`; pass `clientNameSuffix` when one process needs several clients for the same
queue. Dispose the client (`await using`) to close its connection.

`SendWithManualAcknowledgeAsync` returns `ManualAckRabbitResult<T>`: process `Data` and await
`ConfirmAcknowledgeAsync()` to ack the reply only after it was handled:

```csharp
var result = await client.SendWithManualAcknowledgeAsync<BalanceResponse, BalanceRequest>(
    new BalanceRequest(accountId),
    cancellationToken: cancellationToken);

await SaveBalanceAsync(result.Data, cancellationToken);
await result.ConfirmAcknowledgeAsync(cancellationToken);
```

`GetConsumersCountAsync` returns the count of consumers of the request queue. Pass `jsonSerializerOptions` to
`CreateClientAsync` to override `RabbitMQJson.DefaultOptions`, which stay wire-compatible with the Newtonsoft.Json
format of 1.x.

## See also

- [Migration guides](https://curiosus-dev.github.io/Curiosus.Utils/migrations) — upgrading from `Curiosity.*` 1.x and between major versions
- [Curiosus.RabbitMQ 4.0 migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-rabbitmq/4.0)
- [Curiosus.RequestProcessing.RabbitMQ](https://www.nuget.org/packages/Curiosus.RequestProcessing.RabbitMQ)
  — receive and process requests from RabbitMQ
- [Curiosus.Configuration](https://www.nuget.org/packages/Curiosus.Configuration) — options binding and validation
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
