# Changelog

## [Unreleased]

Options are validated at start and channel names are case-sensitive now: see the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-requestprocessing-postgres/3.0).

### Added

- `PgChannelNamesAttribute` validates a list of channel names with the rules of
  `PostgresEventReceiverOptions.EventNames`, for channel lists of your own options. ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))
- `NotifyAsync` extension methods for `NpgsqlConnection` and `NpgsqlDataSource` send a notification with
  `pg_notify` and parameters. On a connection with an open transaction the notification is sent on commit.
  Use them instead of building `NOTIFY` SQL by string interpolation.
  ([#73](https://github.com/curiosus-dev/Curiosus.Utils/issues/73))

### Security

- `LISTEN` quotes every channel name as an identifier. A name from `PostgresEventReceiverOptions.EventNames` was
  inserted into SQL as is, so a name like `x; DROP TABLE requests` ran SQL with the listener's credentials.
  ([#73](https://github.com/curiosus-dev/Curiosus.Utils/issues/73))
- `MonitoredDatabase.ToString()` returns `Host:Port/Database` instead of the connection string, which reached
  exception messages with its password. ([#73](https://github.com/curiosus-dev/Curiosus.Utils/issues/73))

### Changed

- **Breaking:** the options are validated with data annotations of `Microsoft.Extensions.Options` when the host starts
  instead of `IValidatableOptions`/`AssertValid()` of `Curiosus.Configuration`: `IPostgresRequestProcessorNodeOptions`
  is replaced with the `PostgresRequestProcessorNodeOptions` class, and `AddPostgresRequestProcessor` takes an optional
  `Action<TOptions>` instead of an options instance and returns `OptionsBuilder<TOptions>`. To migrate, derive the node
  options from `PostgresRequestProcessorNodeOptions` and call
  `AddPostgresRequestProcessor<...>().BindConfiguration("RequestProcessor")`, see the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-requestprocessing-postgres/3.0#options). ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))
- `DbEventReceiver` no longer validates its options in the constructor: they are validated at start. ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))
- Channel names are used exactly as written, so mixed-case names are case-sensitive: `MyChannel` now receives
  `pg_notify('MyChannel', ...)` and `NOTIFY "MyChannel"`, but no longer an unquoted `NOTIFY MyChannel`. Lower-case names
  are not affected. To migrate, use lower-case names or send with `NotifyAsync`, see the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-requestprocessing-postgres/3.0). ([#73](https://github.com/curiosus-dev/Curiosus.Utils/issues/73))
- Options validation rejects empty, whitespace-only, NUL-containing and longer than 63 bytes channel names at startup;
  empty names were skipped with a warning before. ([#73](https://github.com/curiosus-dev/Curiosus.Utils/issues/73))

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.RequestProcessing.Postgres` to `Curiosus.RequestProcessing.Postgres` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.

## [1.4.0] - 2026-02-12

### Changed

- Build target changed to `net10.0`
- Upgraded `Npgsql` up to `10.0.1`.

## [1.3.0] - 2023-01-29

### Changed

- Upgraded `Npgsql` up to `7.0.1`.

## [1.2.0] - 2022-06-09

### Changed

- Made options parameters generic for dispatcher, bootstrapper and worker.

## [1.1.2] - 2022-06-07

### Changed

- Update `Curiosity.RequestProcessing` to `1.1.2`

## [1.1.1] - 2022-06-07

### Changed

- Update `Curiosity.RequestProcessing` to `1.1.1`

## [1.1.0] - 2022-04-22

### Removed

- Removed filtration from package.

## [1.0.0] - 2021-03-15

First release.
