# Changelog

## [Unreleased]

### Changed

- **Breaking:** rebuilt on `System.TimeProvider`. See the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-datetime-dbsync/3.0).
  ([#69](https://github.com/curiosus-dev/Curiosus.Utils/issues/69))
  - `DbSyncTimeProvider : TimeProvider` replaces `DbSyncDateTimeService : IDateTimeService`: `GetUtcNow()` is the local
    time plus the offset "database − local"; timers and `GetTimestamp()` stay on the local monotonic clock. It is sealed:
    extend it by composition.
  - `AddCuriosusDbSyncTimeProvider(configSectionPath = "DbTimeSync", configure, dataSourceKey)` replaces
    `AddDbSyncDateTimeServices(<T>)`: it binds `DbSyncTimeOptions` from the section
    (`DbSyncTimeOptions.SectionName`), applies `configure` after it and validates at start; the provider becomes the
    app-wide `TimeProvider`. ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))
  - The database time is read by `IDbClock`, by default `PostgresDbClock` over the `DbDataSource` registered in DI
    (`dataSourceKey` for a keyed one), instead of `ICuriosusDataContextFactory`. The package no longer references
    `Curiosus.DAL` and `Curiosus.Tools`.
  - `DbSyncTimeOptions` (`SyncPeriod`, `RetryPeriod`, `InitialSyncTimeout`, `MaxAcceptedRtt` as `TimeSpan`) replaces
    `DbDateTimeOptions.SyncPeriodMin`.
  - `DbTimeSyncService` makes the first sync when it starts, waits at most `InitialSyncTimeout` and never fails the app
    start; it retries until the first success and then resyncs every `SyncPeriod`. `DateTimeServiceInitializer<T>`
    and `DbDateTimeSynchronizationWatchdog<T>` are removed.

### Fixed

- The offset compensates half of the round trip to the database, and samples with a round trip longer than
  `MaxAcceptedRtt` are discarded; 2.x could be off by a full round trip.
- The offset is a single `long` read and written with `Volatile`: 2.x stored it in a struct that a reader could see
  half-written.
- A failed resync keeps the previous offset, and the first sync no longer crashes the start when it runs before the
  database is created or migrated.

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.DateTime.DbSync` to `Curiosus.DateTime.DbSync` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.

## [1.4.0] - 2026-02-13

### Changed

- Upgraded `Microsoft`'s packages up to `10.*` versions.

## [1.3.0] - 2023-01-29

### Changed

- Upgraded `Microsoft`'s packages up to `6.*` versions.

## [1.2.0] - 2021-11-16

### Added

- Using `IAppInitializer` from `Tools`.

## [1.1.0] - 2021-07-29

### Changed

- Added ability to specify own implementation of date time service.

## [1.0.3] - 2021-03-29

### Changed

- Use master connection instead of readonly for DateTime synchronization.
