# Changelog

## [Unreleased]

### Security

- The client accepted any SSH host key (nobody handled `HostKeyReceived`), so anyone who could redirect the traffic
  could impersonate the server, capture the credentials and read or swap files. It now compares the SHA-256 fingerprint
  with `HostKeyFingerprintsSha256` and refuses the connection otherwise. Rotate the SFTP credentials if the server was
  reached over an untrusted network. See the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-sftp/3.0). ([#97](https://github.com/curiosus-dev/Curiosus.Utils/issues/97))

### Changed

- **Breaking:** a host key that isn't configured fails every connection, including the start-up check, with
  `SftpHostKeyVerificationException`, which the retry policy does not retry; `AllowAnyHostKey` logs a warning on every
  connection. See the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-sftp/3.0). ([#97](https://github.com/curiosus-dev/Curiosus.Utils/issues/97))
- **Breaking:** `AddSftpServices(SftpClientOptions)` is replaced by
  `AddSftpServices(string configSectionPath = "Sftp", Action<SftpClientOptions>? configure = null)`, which binds the
  options from the configuration section (`SftpClientOptions.SectionName`), applies `configure` on top, returns
  `OptionsBuilder<SftpClientOptions>` and validates the options (data annotations and `SftpClientOptionsValidator`) when
  the host starts, not on the first resolve: replace the call with `services.AddSftpServices()`. A missing section binds
  nothing, so code-only configuration works. `SftpClientOptions` is no longer registered as a singleton;
  `SftpClientFactory` takes `IOptionsMonitor<SftpClientOptions>` (every new client takes the current options, so a
  configuration reload applies to the next client) and `SftpInitializer` takes `IOptions<SftpClientOptions>`. A second
  `AddSftpServices` call no longer adds a second start-up check. See the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-sftp/3.0#options). ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))
- References `Microsoft.Extensions.Options.ConfigurationExtensions` and `Microsoft.Extensions.Options.DataAnnotations`. ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))

## [2.1.0] - 2026-10-03

### Changed

- Updated `Polly` to `8.0.0`. The package keeps using the Polly v7 API, which Polly 8 still ships,
  so nothing changes for consumers.

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.SFTP.SSH.NET` to `Curiosus.SFTP.SSH.NET` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.
- Upgraded `SSH.NET` up to `2026.0.0` (fixes known vulnerabilities).

## [1.5.0] - 2026-02-13

### Changed

- Upgraded `Polly` up to `7.2.4`.

## [1.4.1] - 2023-03-14

### Fixed

- #51: Fixed namespace for `ResourceManager`.

## [1.4.0] - 2023-01-29

### Changed

- Upgraded `Polly` up to `7.2.3`.
- Upgraded `SSH.NET` up to `2020.0.2`.

## [1.3.0] - 2022-05-14

### Added

- Added method for listing directory content.

## [1.2.0] - 2021-11-16

### Changed

- Using `TempFiles` from `Curiosity.Tools`.
- 
## [1.1.0] - 2021-03-27

### Added

- Added method which returns file content as a stream.

## [1.0.4] - 2021-03-08

### Fixed

- Fixed localization.

## [1.0.3] - 2021-03-08

### Added

- Added changelog file.
