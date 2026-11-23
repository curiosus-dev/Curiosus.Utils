# Changelog

## [Unreleased]

### Added

- `RabbitMQEventReceiverOptions.UseTls`: the receiver connects over TLS and validates the server certificate for
  `HostName`; set `Port` to the TLS listener, 5671 by default ([#162](https://github.com/curiosus-dev/Curiosus.Utils/issues/162)).

### Changed

- **Breaking:** the options are validated with data annotations of `Microsoft.Extensions.Options` when the host starts
  instead of `IValidatableOptions`/`AssertValid()` of `Curiosus.Configuration`: `IRabbitMQRequestProcessorNodeOptions`
  is replaced with the `RabbitMQRequestProcessorNodeOptions` class, and `AddRabbitMQRequestProcessor` binds them from the
  configuration section `configSectionPath` (default `"RequestProcessor"`) and an optional `configure` delegate instead
  of taking an options instance, and returns `OptionsBuilder<TOptions>`. To migrate, derive the node options from
  `RabbitMQRequestProcessorNodeOptions`, call `AddRabbitMQRequestProcessor<...>()` (or pass your section path) and remove
  `AssertValid()`, see the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-requestprocessing-rabbitmq/4.0#options). ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))
- `RabbitMQEventReceiverOptions.Port` must be at most 65535. ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))
- `RabbitMQEventReceiver` no longer validates its options in the constructor: they are validated at start. ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))

## [3.0.0] - 2026-10-03

### Changed

- **Breaking:** upgraded `RabbitMQ.Client` from `6.8.1` to `7.0.0` (through `Curiosus.RabbitMQ`). To migrate:
  - `RabbitMQEvent.ReceivedData` is the `BasicDeliverEventArgs` of RabbitMQ.Client 7: `BasicProperties` is
    `IReadOnlyBasicProperties` now. `DeliveryTag` and `BasicProperties.CorrelationId` are read as before; recompile.
    Don't read `ReceivedData.Body` after the event was received, use `RabbitMQEvent.Payload`.
  - `RabbitMQRequestWrapper<T>.CorrelationId` is `string?`: it is `null` when the message has no correlation id.
  - Code that uses `RabbitMQ.Client` directly must be migrated to version 7 as well, see its
    [migration guide](https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/main/v7-MIGRATION.md) ([#77](https://github.com/curiosus-dev/Curiosus.Utils/issues/77)).
- **Breaking:** the `RabbitMQRequestProcessorBootstrapperBase` constructor takes the node options once: remove the
  last `options` argument, the receiver options are read from `nodeOptions.RabbitMQEventReceiver` ([#18](https://github.com/curiosus-dev/Curiosus.Utils/issues/18)).
- `RabbitMQEventReceiverOptions.ClientName` is the machine name by default instead of failing the options validation
  when it is not set ([#18](https://github.com/curiosus-dev/Curiosus.Utils/issues/18)).

### Removed

- **Breaking:** `RabbitMQEventReceiverOptions.ExchangeName`: it was never used, the receiver consumes the queue
  directly. Remove it from code; a value left in configuration is ignored ([#18](https://github.com/curiosus-dev/Curiosus.Utils/issues/18)).

### Added

- `RabbitMQEventReceiver` implements `IAsyncDisposable`: `DisposeAsync` stops the receiver when `StopAsync`
  was not called and releases its resources.

### Fixed

- `RabbitMQEventReceiver.StopAsync` sends the confirmations and rejections made before it instead of dropping them,
  so their events are not redelivered ([#77](https://github.com/curiosus-dev/Curiosus.Utils/issues/77)).
- A confirmation or rejection made before a manual reconnect is not sent on the new channel: its delivery tag is
  unknown there, so the broker would close the new channel again and again ([#77](https://github.com/curiosus-dev/Curiosus.Utils/issues/77)).
- The RabbitMQ request processing sample starts: its consumer configuration has `ClientName` ([#18](https://github.com/curiosus-dev/Curiosus.Utils/issues/18)).

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.RequestProcessing.RabbitMQ` to `Curiosus.RequestProcessing.RabbitMQ` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.

## [1.3.0] - 2026-02-13

### Changed

- Upgraded dependencies.

## [1.2.3] - 2022-01-30

### Added

- Removed event set from request processing completion action.

## [1.2.2] - 2022-01-30

### Added

- Added `QoS` multiplier.

## [1.2.1] - 2022-01-29

### Fixed

- Added extra event set after RabbitMQ request processing

## [1.2.0] - 2022-01-29

### Changed

- Upgraded dependencies.

## [1.1.0] - 2022-01-29

### Changed

- Made event finalization (confirm/reject) more robust (moved processing to a separated thread).

## [1.0.1] - 2022-01-12

### Added

- Added icon to package
- Added comments to package

## [1.0.0] - 2022-01-12

Package was released.
