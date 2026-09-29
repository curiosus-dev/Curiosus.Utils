# Curiosus.RabbitMQ

[![NuGet](https://img.shields.io/nuget/v/Curiosus.RabbitMQ)](https://www.nuget.org/packages/Curiosus.RabbitMQ) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.RabbitMQ)](https://www.nuget.org/packages/Curiosus.RabbitMQ) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.RabbitMQ.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Request-reply (RPC) client for RabbitMQ: sends a JSON request to a queue and awaits the reply on a dedicated response
queue, with automatic and manual connection recovery and resending. Use it to call services that process requests
from RabbitMQ. Built on the async API of [RabbitMQ.Client](https://www.nuget.org/packages/RabbitMQ.Client) 7.

## Installation

```bash
dotnet add package Curiosus.RabbitMQ
```

## Usage

```yaml
RabbitMQ:
  HostName: localhost
  Port: 5672
  UserName: guest
  Password: guest
  ExchangeName: ""
  ClientName: billing-api
```

```csharp
services.AddRabbitMQRPC(configuration.RabbitMQ); // validates options, registers RabbitMqRpcClientFactory

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

The reply queue is named `{requestQueue}_responses_{ClientName}`; pass `clientNameSuffix` to `CreateClientAsync`
when one process needs several clients for the same queue. Dispose the client (`await using`) to close its connection.

`SendWithManualAcknowledgeAsync` returns `ManualAckRabbitResult<T>`: process `Data` and await
`ConfirmAcknowledgeAsync()` to ack the reply only after it was handled:

```csharp
var result = await client.SendWithManualAcknowledgeAsync<BalanceResponse, BalanceRequest>(
    new BalanceRequest(accountId),
    cancellationToken: cancellationToken);

await SaveBalanceAsync(result.Data, cancellationToken);
await result.ConfirmAcknowledgeAsync(cancellationToken);
```

`GetConsumersCountAsync` returns the count of consumers of the request queue.

Messages are serialized with `System.Text.Json`. `RabbitMqRpcClient.DefaultJsonSerializerOptions` stay wire-compatible
with the Newtonsoft.Json format of 1.x; pass `jsonSerializerOptions` to `CreateClientAsync` to override them.

## See also

- [Curiosus.RequestProcessing.RabbitMQ](https://www.nuget.org/packages/Curiosus.RequestProcessing.RabbitMQ)
  — receive and process requests from RabbitMQ
- [Curiosus.Configuration](https://www.nuget.org/packages/Curiosus.Configuration) — options validation
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
