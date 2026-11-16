# Changelog

## [Unreleased]

### Changed

- **Breaking:** `RequestProcessorNodeOptions` is validated with data annotations of `Microsoft.Extensions.Options`
  when the host starts instead of `IValidatableOptions`/`AssertValid()` of `Curiosus.Configuration`, and
  `AddRequestProcessor` takes an optional `Action<TOptions>` instead of an options instance and returns
  `OptionsBuilder<TOptions>`. To migrate, call `AddRequestProcessor<...>().BindConfiguration("RequestProcessor")`,
  and move the `Validate(prefix)` override of your options subclass to attributes and an `IValidateOptions<T>`, see
  the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-requestprocessing/3.0#options). `TOptions` and `RequestProcessorNodeOptions`
  stay resolvable from DI, so worker and bootstrapper constructors don't change. ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))
- **Breaking:** `RequestProcessorNodeOptions.EventsPeriodicCheckSec` must be at least 1: 0 passed validation and made
  the periodic check loop without a pause. ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))
- The package no longer references `Curiosus.Configuration` directly. ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))

### Fixed

- `AddRequestProcessor` with `RequestProcessorNodeOptions` itself as `TOptions` no longer resolves the options
  recursively (a stack overflow on the first resolution). ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.RequestProcessing` to `Curiosus.RequestProcessing` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.

## [1.5.0] - 2026-02-13

### Changed

- Upgraded `Microsoft`'s packages up to `10.*` versions.

## [1.4.1] - 2023-01-30

### Changed

- Set event only after executing request completion actions.

## [1.4.0] - 2023-01-29

### Changed

- Upgraded `Microsoft`'s packages up to `6.*` versions.

## [1.3.1] - 2022-06-10

### Added

- Added virtual method for fetching free workers to dispatcher.

## [1.3.0] - 2022-06-09

### Changed

- Made options parameters generic for dispatcher, bootstrapper and worker. 
- Made `IsBusy` virtual in `WorkerBase`. 

## [1.2.3] - 2022-06-07

### Added

- Added virtual method to process request after worker completion.

## [1.2.2] - 2022-06-07

### Removed

- Removed properties from `IRequestProcessingEvent`.

## [1.2.1] - 2022-06-07

### Changed

- Renamed `HandleDbEventReceived` -> `HandleEventReceived`.

## [1.2.0] - 2022-06-02

### Added

- Added passing worker name to `CreateWorkerParams` method.

## [1.1.0] - 2022-04-22

### Removed

- Removed filtration from package.

## [1.0.0] - 2021-03-15

First release.
