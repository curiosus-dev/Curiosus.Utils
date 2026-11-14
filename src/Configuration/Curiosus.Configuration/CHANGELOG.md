# Changelog

## [Unreleased]

### Added

- `[Secret]` and `[NotSecret]` attributes on option properties override the name-based masking of
  `ConfigurationPrinter`: `[Secret]` masks a property whatever its name, `[NotSecret]` prints the value of a property
  whose name only looks secret (a public site key). `[Secret]` wins when both are set; overrides inherit them.
  ([#39](https://github.com/curiosus-dev/Curiosus.Utils/issues/39))

### Security

- `ConfigurationPrinter` masks secrets: the value of every property or dictionary key whose name contains `password`,
  `pwd`, `passphrase`, `secret`, `token`, `credential`, `apikey`, `privatekey` (not `privatekeypath`) or ends with `key`
  (case-insensitive) is printed as `***`; for names containing
  `connectionstring` only the values of well-known non-secret keys (host, port, database, user, timeouts, ...) are
  printed, all other values are masked, and a value that can't be parsed or contains a URI is masked entirely. Earlier versions printed them
  in clear text, so credentials of options printed at startup reached every log target: rotate them.
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
