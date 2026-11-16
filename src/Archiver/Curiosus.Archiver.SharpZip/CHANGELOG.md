# Changelog

## [Unreleased]

### Changed

- **Breaking:** `AddSharpZipArchiver` takes an optional `Action<TempFileOptions>` instead of a `TempFileOptions`
  instance and returns `OptionsBuilder<TempFileOptions>`, validated when the host starts; `SharpZipArchiver` takes
  `IOptions<TempFileOptions>`. Migrate with `services.AddSharpZipArchiver().BindConfiguration("TempFiles")`, see the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-archiver-sharpzip/3.0#options). ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.Archiver.SharpZip` to `Curiosus.Archiver.SharpZip` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.

## [1.5.0] - 2026-02-13

### Changed

- Upgraded `SharpZipLib` to `1.4.2`.

## [1.4.0] - 2023-01-29

### Changed

- Upgraded `SharpZipLib` to `1.4.1`.

## [1.3.2] - 2022-12-26

### Fixed

- Added unicode support for file names to `ZipDirAsync` method.

## [1.3.1] - 2022-05-14

### Changed

- Fixed naming of `UnzipFile` method.

## [1.3.0] - 2022-05-14

### Added

- Added method to unzip archive.

## [1.2.0] - 2021-11-16

### Changed

- Using `TempFiles` from `Curiosity.Tools`.
- 
## [1.1.0] - 2021-03-27

### Added

- Added methods with file names list
- Added avoiding name duplication in the archive

### Fixed

- Removed unnecessary check of the file name 

