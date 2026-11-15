# Changelog

## [Unreleased]

### Added

- `AddCuriosusOptions<T>("Section")` and a named overload: bind options to a configuration section with
  `Microsoft.Extensions.Options`, validate them with data annotations (nested objects included) and fail the host start
  when they are invalid. ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))
- `LogConfigurationOnStart()`: logs the configuration before hosted services start, one line per value with the
  provider it came from, secrets masked. Without arguments the whole configuration except sectionless environment
  variables (`PATH`, ...); with sections only them; `ExcludedSections` leaves sections out.
  ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))
- `IConfiguration.GetCuriosusOptions<T>("Section", configure)`: binds and validates options like `AddCuriosusOptions<T>`
  for code that needs them while the builder is being set up, before `IOptions` exists.
  ([#43](https://github.com/curiosus-dev/Curiosus.Utils/issues/43))
- Dependencies on `Microsoft.Extensions.Hosting.Abstractions`, `Microsoft.Extensions.Options.ConfigurationExtensions`
  and `Microsoft.Extensions.Options.DataAnnotations`.

### Deprecated

- `IValidatableOptions`, `AssertValid`, `ConfigurationValidationError(Collection)`, `ConfigurationValidationException`,
  `ILoggableOptions`, `ConfigurationPrinter`, `IConfigurationProvider<T>` and `ConfigurationProviderBase<T>` are removed
  in 3.0 in favour of `AddCuriosusOptions<T>` and `LogConfigurationOnStart`; Curiosus packages move their options with
  their next majors. See the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-configuration/3.0).
  ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))

The startup configuration dump and validation errors look different: see the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-configuration/2.1).

### Added

- `[Secret]` and `[NotSecret]` attributes on option properties override the name-based masking of
  `ConfigurationPrinter`: `[Secret]` masks a property whatever its name, `[NotSecret]` prints the value of a property
  whose name only looks secret (a public site key). `[Secret]` wins when both are set; overrides inherit them.
  ([#39](https://github.com/curiosus-dev/Curiosus.Utils/issues/39))

### Security

- `ConfigurationPrinter` masks secrets: the value of every property or dictionary key whose name contains `password`,
  `pwd`, `passphrase`, `secret`, `token`, `credential`, `apikey`, `privatekey` (not `privatekeypath`), `signature`,
  `salt` or ends with `key` or `keys` (case-insensitive), and every value that carries a secret itself (a URI with user
  info `amqp://user:pass@host`, a URL with `?token=`/`?sig=`/`?key=`/`?code=`, a connection
  string pair such as `Password=`, `Pwd=`, `AccountKey=` or `SharedAccessKey=` under any key name (`Server=x;Password=y`,
  `host:6379,password=y`), a Slack, Discord, Teams or Telegram bot webhook URL), is printed as `***`; for names containing `connectionstring` only the values of well-known
  non-secret keys (host, port, database, user, timeouts, ...) are printed, all other values are masked, and a value that
  can't be parsed or contains a URI is masked entirely. Earlier versions printed them in clear text, so credentials of
  options printed at startup reached every log target: rotate them, see the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-configuration/2.1).
  ([#39](https://github.com/curiosus-dev/Curiosus.Utils/issues/39))

### Fixed

- `ConfigurationValidationException.Message` contains the message passed to it, which `AssertValid` fills with every
  validation error, instead of the default "Exception of type ... was thrown".
  `AssertValid` without a prefix says "Options are invalid" instead of "Option  are invalid".
  ([#15](https://github.com/curiosus-dev/Curiosus.Utils/issues/15))
- `ConfigurationPrinter` no longer throws on indexer properties and on `ILoggableOptions` array elements without
  properties, and prints dictionaries as `{key=value, ...}` instead of `[[key, value], ...]`.
  ([#39](https://github.com/curiosus-dev/Curiosus.Utils/issues/39))

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.Configuration` to `Curiosus.Configuration` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.

## [1.3.0] - 2026-02-13

### Changed

- Upgraded `Microsoft`'s packages up to `6.*` versions.

## [1.2.0] - 2023-01-29

### Changed

- Upgraded `Microsoft`'s packages up to `6.*` versions.

## [1.1.0] - 2021-04-13

### Added

- Added support for secret config files.

## [1.0.5] - 2021-03-29

- Sync release.

## [1.0.4] - 2021-03-25

### Fixed

- Add support of CLI args and environment variables.

## [1.0.2] - 2021-03-08

### Fixed

- Made arguments check more correct.
