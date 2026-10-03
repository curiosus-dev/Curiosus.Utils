# Changelog

## [Unreleased]

### Changed

- `CsvFileWriter` now runs on `CsvHelper` `33.0.0` (through `Curiosus.FileDataReaderWriters`).

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.FileDataReaderWriters.SylvanCsv` to `Curiosus.FileDataReaderWriters.SylvanCsv` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.

## [1.1.0] - 2026-02-13

- Upgraded `Sylvan.Data.Csv` up to `1.4.3`.

## [1.0.0] - 2024-02-02

Package was released.
