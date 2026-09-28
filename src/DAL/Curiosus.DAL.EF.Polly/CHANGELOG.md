# Changelog

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.DAL.EF.Polly` to `Curiosus.DAL.EF.Polly` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.

## [1.2.0] - 2026-02-13

### Changed

- Build target changed to `net10.0`
- Upgraded `Microsoft`'s packages up to `10.*` versions.
- Upgraded `Polly` up to `7.2.4`.

## [1.1.0] - 2023-01-29

### Changed

- Upgraded `Microsoft`'s packages up to `6.*` versions.
- Upgraded `Polly` up to `7.2.3`.

## [1.0.1] - 2021-03-15

### Added 

- Explicitly installed `Annotaitons` package.
