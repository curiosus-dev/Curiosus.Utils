# Changelog

## [Unreleased]

### Changed

- **Breaking:** updated `NLog.Extensions.Logging` to `6.0.0` and `NLog.MailKit` to `6.0.0`, so apps now run on NLog 6.
  NLog 6 rejects NLog.config files that use removed options: with `throwExceptions="true"` or `throwConfigExceptions="true"`
  the app fails on startup (for example `'FileTarget' cannot assign unknown property 'enableArchiveFileCompression'`),
  otherwise the option is ignored. To migrate, check your NLog.config against the
  [NLog 6 breaking changes](https://nlog-project.org/2025/04/29/nlog-6-0-major-changes.html):
  - `File` target: remove `enableArchiveFileCompression` (compression now needs the `NLog.Targets.GZipFile` package and
    `xsi:type="GZipFile"`, which compresses the active log file as well), replace `archiveNumbering`/`archiveDateFormat`
    and the `{#}` placeholder of `archiveFileName` with `archiveSuffixFormat`, remove `concurrentWrites`, `networkWrites`,
    `forceManaged`, `fileAttributes`. Archive names change: with `archiveFileName="archive/main.log"` and
    `archiveSuffixFormat="_{1:yyyyMMdd}_{0:00}"` the archive of 2026-10-03 is `archive/main_20261003_00.log`, not
    `archive/main20261003.zip`. `maxArchiveFiles` only cleans up archives matching the new names, so delete the archives
    written by NLog 5 yourself and update scripts that parse archive names.
  - Targets moved out of the `NLog` package need their own package: `Network`/`Syslog`/`Gelf` (`NLog.Targets.Network`),
    `Trace` (`NLog.Targets.Trace`), `WebService` (`NLog.Targets.WebService`), `AtomicFile`, `ConcurrentFile`,
    the System.Net.Mail `Mail` target (`NLog.Targets.Mail`), `${regex-replace}` (`NLog.RegEx`). The MailKit `Mail` target
    used for the mail logger still comes with `NLog.MailKit`.
  - `Console` target batches writes and no longer uses `Console.WriteLine`: set `forceWriteLine="true"` if you need it.
  - Code using NLog APIs directly: `LogManager.LoadConfiguration(path)` is replaced by
    `LogManager.Setup().LoadConfigurationFromFile(path, optional: false)`, `LogManager.Configuration` is nullable.
    Pass `optional: false`: by default a missing file is silently skipped and the app runs without logging, while
    `LoadConfiguration` threw `FileNotFoundException`.
- The sample `NLog.config` no longer compresses archived log files: NLog 6 `File` target has no archive compression.
- Updated `NetEscapades.Configuration.Yaml` to `3.0.0` (`YamlDotNet` `13.0.1`) through `Curiosus.Configuration.YAML`.

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.Hosting` to `Curiosus.Hosting` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.
- Upgraded `MailKit` up to `4.18.0` and `NLog.MailKit` up to `5.3.0` (fixes known vulnerabilities).

## [1.4.0] - 2026-02-13

### Changed

- Upgraded `Microsoft`'s packages up to `10.*` versions.
- Upgraded `MailKit` up to `3.6.0`.
- Upgraded `NLog.Extensions.Logging` up to `5.5.0`.

## [1.3.0] - 2023-01-29

### Changed

- Upgraded `Microsoft`'s packages up to `6.*` versions.
- Upgraded `MailKit` up to `3.4.3`.
- Upgraded `NLog`'s packages up to `5.*` versions.

## [1.2.3] - 2022-06-13

### Added

- Extended methods for configuration host from bootstrapper.

## [1.2.2] - 2022-06-07

### Added

- Added method to transform configuration before printing and validation.

### Added

## [1.2.1] - 2021-11-16

### Added

- Added new method for configuration services for `CuriosityServiceAppBootstrapper`.

## [1.2.0] - 2021-11-16

### Added

- Moved `AppInitializer` from `Curiosity.AppIntiializer`.
- Using `IAppInitializer` from `Tools`.
- Moved TempDirCleaner from `Curiosity.TempFiles`.

## [1.0.14] - 2021-07-29

### Changed

- Added cancellation token to `ProcessAsync` of `CuriosityWatchdog`.

## [1.0.13] - 2021-07-09

### Changed

- Using `Curiosity.Configuration.YAML` v1.0.7.

## [1.1.12] - 2021-06-04

### Added

- Added `Watchdog` for periodic tasks.

## [1.1.11] - 2021-04-08

### Added

- Added `ConfigureServices` overload with arguments passing to `CuriosityToolAppBootstrapper`.

## [1.1.10] - 2021-04-08

### Added

- Added separate method for `AppName` in logging configuration.

## [1.1.9] - 2021-04-07

### Changed

- Use `Curiosity.Configuration.YAML.1.0.6`.

## [1.1.8] - 2021-03-26

### Changed 

- Changed log level at `ThreadPoolMonitoringService`.

## [1.1.7] - 2021-03-26

### Changed 

- Changed name of `ThreadPoolMonitoringService` logger.

## [1.1.6] - 2021-03-25

### Added 

- Adding CLI args to app configuration.

## [1.1.5] - 2021-03-15

### Added 

- Explicitly installed `System.Data.Annotaitons` package.

## [1.1.4] - 2021-03-15

### Added 

- Added IoC extensions and initializer of measurer.
- Added performance measurer and stuck code manager to bootstrappers.

## [1.1.3] - 2021-03-05

### Added 

- Added configuration validation on application start.
