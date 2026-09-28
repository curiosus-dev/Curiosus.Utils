# Changelog

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
