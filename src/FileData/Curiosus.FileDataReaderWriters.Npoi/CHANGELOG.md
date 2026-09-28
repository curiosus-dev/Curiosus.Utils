# Changelog

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.FileDataReaderWriters.Npoi` to `Curiosus.FileDataReaderWriters.Npoi` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.
- Upgraded `NPOI` up to `2.8.1` (fixes transitive vulnerable `System.Security.Cryptography.Xml`).

## [1.1.0] - 2026-02-13

- Upgraded `NPOI` up to `2.7.5`.

## [1.0.0] - 2024-02-02

Package was released.
