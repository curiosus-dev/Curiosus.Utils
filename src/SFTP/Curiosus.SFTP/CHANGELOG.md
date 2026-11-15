# Changelog

## [Unreleased]

### Security

- The server host key is verified: `HostKeyFingerprintsSha256` (or `AllowAnyHostKey` for development) is required, so a
  client of `Curiosus.SFTP.SSH.NET` no longer connects to whoever answers on the server's address. See the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-sftp/3.0). ([#97](https://github.com/curiosus-dev/Curiosus.Utils/issues/97))

### Added

- `SftpClientOptions.HostKeyFingerprintsSha256`, `SftpClientOptions.AllowAnyHostKey`, `SftpHostKeyFingerprint` and
  `SftpHostKeyVerificationException`. ([#97](https://github.com/curiosus-dev/Curiosus.Utils/issues/97))

### Changed

- **Breaking:** options validation fails without `HostKeyFingerprintsSha256` unless `AllowAnyHostKey` is set, with both,
  and with a malformed fingerprint. See the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-sftp/3.0). ([#97](https://github.com/curiosus-dev/Curiosus.Utils/issues/97))

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.SFTP` to `Curiosus.SFTP` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.

## [1.4.0] - 2026-02-13

### Changed

- Upgraded dependencies.

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
