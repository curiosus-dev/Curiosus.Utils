# Changelog

## [3.0.0] - 2026-10-03

### Changed

- **Breaking:** updated `NLog.Web.AspNetCore` to `6.0.0` and `NLog.MailKit` to `6.0.0`, so apps now run on NLog 6.
  NLog.config files using options removed in NLog 6 fail on startup with `throwExceptions="true"` or
  `throwConfigExceptions="true"`: see the migration steps in the
  [Curiosus.Hosting changelog](https://github.com/curiosus-dev/Curiosus.Utils/blob/main/src/Hosting/Curiosus.Hosting/CHANGELOG.md)
  and the [NLog 6 breaking changes](https://nlog-project.org/2025/04/29/nlog-6-0-major-changes.html).
  Code calling `NLogBuilder.ConfigureNLog(path)` should use
  `LogManager.Setup().RegisterNLogWeb().LoadConfigurationFromFile(path, optional: false)`: without `optional: false`
  a missing file is silently skipped and the app runs without logging, while `ConfigureNLog` threw.
- Updated `NetEscapades.Configuration.Yaml` to `3.0.0` (`YamlDotNet` `13.0.1`) through `Curiosus.Hosting`.

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.Hosting.Web` to `Curiosus.Hosting.Web` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.

## [1.4.0] - 2026-02-13

### Changed

- Upgraded `Microsoft`'s packages up to `10.*` versions.
- Upgraded `NLog.Web.AspNetCore` up to `5.5.0`.
- Upgraded `MailKit` up to `3.6.0`.

## [1.3.0] - 2023-01-29

### Changed

- Upgraded `Microsoft`'s packages up to `6.*` versions.
- Upgraded `NLog`'s packages up to `5.*` versions.
- Upgraded `MailKit` up to `3.4.3`.

## [1.2.3] - 2022-07-15

### Changed

- Updated `Curiosity.Tools.Web`.

## [1.2.2] - 2022-07-15

### Changed

- Updated `Curiosity.Tools.Web`.

## [1.2.1] - 2022-06-07

### Changed

- Updated dependencies.

## [1.2.0] - 2021-11-16

### Added

- Using `IAppInitializer` from `Tools`.

## [1.0.15] - 2021-07-09

### Changed

- Using `Curiosity.Configuration.YAML` v1.0.7.

## [1.1.14] - 2021-04-08

### Changed

- Use `Curiosity.Hosting.1.1.10`.

## [1.1.13] - 2021-04-07

### Changed

- Use `Curiosity.Configuration.YAML.1.0.6`.

## [1.1.12] - 2021-03-25

### Changed

- Use base bootstrapper from `Curiosity.Hosting.1.1.6`.

## [1.1.11] - 2021-03-25

### Changed

- Changed bootstrapping when option `UseIISIntegration` is enabled.

## [1.1.10] - 2021-03-25

### Added

- Added option for using IIS Integration.

## [1.1.9] - 2021-03-15

### Added

- Explicitly installed `System.Data.Annotaitons` package.

## [1.1.8] - 2021-03-15

### Added

- Added performance measurer and stuck code manager to bootstrapper.

## [1.1.7] - 2021-03-13

### Added 

- Added Sensitive data protector to configuration

## [1.1.6] - 2021-03-05

### Changed 

- Updated dependencies.

## [1.1.4] - 2021-03-05

### Fixed 

- Fixed Web configuration validation.

## [1.1.3] - 2021-03-05

### Added 

- Added changelog.

### Changed

- Change return code to standard Curiosity exit codes.
