# Changelog

## [2.1.0] - 2026-10-03

### Changed

- Updated `Polly` to `8.0.0`. The package keeps using the Polly v7 API, which Polly 8 still ships,
  so nothing changes for consumers.

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.SFTP.SSH.NET` to `Curiosus.SFTP.SSH.NET` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.
- Upgraded `SSH.NET` up to `2026.0.0` (fixes known vulnerabilities).

## [1.5.0] - 2026-02-13

### Changed

- Upgraded `Polly` up to `7.2.4`.

## [1.4.1] - 2023-03-14

### Fixed

- #51: Fixed namespace for `ResourceManager`.

## [1.4.0] - 2023-01-29

### Changed

- Upgraded `Polly` up to `7.2.3`.
- Upgraded `SSH.NET` up to `2020.0.2`.

## [1.3.0] - 2022-05-14

### Added

- Added method for listing directory content.

## [1.2.0] - 2021-11-16

### Changed

- Using `TempFiles` from `Curiosity.Tools`.
- 
## [1.1.0] - 2021-03-27

### Added

- Added method which returns file content as a stream.

## [1.0.4] - 2021-03-08

### Fixed

- Fixed localization.

## [1.0.3] - 2021-03-08

### Added

- Added changelog file.
