# Changelog

## [Unreleased]

### Changed

- **Breaking:** `DbOptions` moved to `Microsoft.Extensions.Options`: it no longer implements `IValidatableOptions` and
  `ILoggableOptions`, `Validate(prefix)` is removed and `ConnectionString` is checked by `[Required]` instead. Register it
  (or your subclass) with `AddOptions<T>().BindConfiguration("Db").ValidateDataAnnotations().ValidateOnStart()` instead
  of `AssertValid()`; a subclass that overrode `Validate` moves its rules to an `IValidateOptions<T>`. See the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-dal/3.0#options). ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))

### Removed

- **Breaking:** the dependency on `Curiosus.Configuration`: reference it yourself if your app used it through this
  package. See the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-dal/3.0#options). ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.DAL` to `Curiosus.DAL` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.

## [1.5.0] - 2026-02-13

### Changed

- Upgraded `Microsoft`'s packages up to `10.*` versions.

## [1.4.0] - 2023-01-29

### Changed

- Upgraded `Microsoft`'s packages up to `6.*` versions.

## [1.3.0] - 2021-11-02

### Added

- Added async version of `OnTransactionCompleted` event on transaction completed.
- 
## [1.2.0] - 2021-09-08

### Added

- Added `OnTransactionCompleted` to transaction and context.

## [1.1.1] - 2021-03-29

### Added

- Added optional `IsSensitiveDataLoggingEnabled` to `DbOptions`.

## [1.1.0] - 2021-03-29

### Added

- Added flag to enabling logging for a specific context.
- Added async methods for transactions.
- Added timeout to all methods of `ISqlExecutor`.
- Added cancellation token to all methods of `ISqlExecutor`.
- Added cancellation token to `BeginTransactionAsync` at `DataContext`.

### Changed

- Renamed `ReplicaConnectionString` to `ReadOnlyConnectionString`.
- Renamed `CreateReplicaContext` to `CreateReadOnlyContext`.
- Split `ICuriosityDataContextFactory` into `ICuriosityDataContextFactory` and `ICuriosityReadOnlyDataContextFactory`.
- Replaced foreach on for loop at `SafeRollbackTransactions`

## [1.0.2] - 2021-03-27

### Fixed

- Added Changelog and package icon.
