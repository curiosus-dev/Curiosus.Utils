# Changelog

## [Unreleased]

### Changed

- **Breaking:** upgraded `RabbitMQ.Client` from `6.8.1` to `7.0.0`. Version 7 has an async-only API, so the RPC client
  API became async too. To migrate:
  - `RabbitMqRpcClientFactory.CreateClient(...)` → `await RabbitMqRpcClientFactory.CreateClientAsync(...)`: same
    parameters plus an optional `CancellationToken`. Connection errors are thrown from the awaited call.
  - `ManualAckRabbitResult<T>.ConfirmAcknowledge()` → `await ManualAckRabbitResult<T>.ConfirmAcknowledgeAsync()`,
    with an optional `CancellationToken` that cancels waiting in this call only. Concurrent calls send one ack.
    A failed ack throws now (`InvalidOperationException` when the channel of the response is closed) instead of
    being logged, and the next call retries it.
  - `RabbitMqRpcClient.GetConsumersCount()` → `await RabbitMqRpcClient.GetConsumersCountAsync()`, with an optional
    `CancellationToken`.
  - Code that uses `RabbitMQ.Client` directly must be migrated to version 7 as well, see its
    [migration guide](https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/main/v7-MIGRATION.md).
- The RPC client completes pending requests asynchronously, so code after `await SendWith...Async(...)` no longer runs
  inside the RabbitMQ consumer and doesn't block the delivery of other responses.

### Fixed

- A response without a correlation id is rejected instead of failing inside the consumer.
- A request that could not be sent because of a connection failure is resent after the recovery or fails, instead of
  waiting for a response forever.
- A response received before a manual reconnect is not acked or rejected on the new channel: its delivery tag is
  unknown there, so the broker would close the new channel.
- A failed automatic ack is logged instead of replacing the response of `SendWithAutoAcknowledgeAsync`, and a failed
  reject doesn't hide the error of `SendWithManualAcknowledgeAsync`.

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.RabbitMQ` to `Curiosus.RabbitMQ` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.
- **Breaking:** RPC client uses `System.Text.Json` instead of `Newtonsoft.Json`. Default options (`RabbitMqRpcClient.DefaultJsonSerializerOptions`) mimic Newtonsoft.Json behavior to stay wire-compatible: case-insensitive property names, public fields, numbers from strings, unescaped non-ASCII characters. Enums are still written as numbers; reading enums from strings requires custom options.

### Added

- `jsonSerializerOptions` parameter in `RabbitMqRpcClientFactory.CreateClient` to customize JSON serialization.

## [1.2.0] - 2026-02-13

### Changed

- Upgraded `RabbitMQ.Client` up to `6.8.1`.
- Upgraded `Newtonsoft.Json` up to `13.0.4`.

## [1.1.0] - 2023-01-29

### Changed

- Upgraded `RabbitMQ.Client` up to `6.4.0`.
- Upgraded `Newtonsoft.Json` up to `13.0.2`.

## [1.0.1] - 2022-01-12

### Added

- Added Rabbit's port to options.

## [1.0.0] - 2022-01-12

Package was released.
