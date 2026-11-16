# Changelog

## [Unreleased]

### Security

- Server certificates are validated and TLS is required: the sender accepted any certificate and fell back to plain
  text when the server didn't advertise STARTTLS, so an attacker on the network path could read the SMTP login, the
  password and the messages. **Rotate the SMTP passwords** used with earlier versions. See the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-email-smtp/3.0). ([#86](https://github.com/curiosus-dev/Curiosus.Utils/issues/86))

### Changed

- **Breaking:** the connection uses `SslOnConnect` on port 465 and `StartTls` otherwise, failing when the server doesn't
  offer it, and MailKit validates the certificate chain, the host name and revocation. New options `Security`
  (`StartTls`, `SslOnConnect` or `None` for a local relay; `Auto` and `StartTlsWhenAvailable` fail validation) and
  `AllowInvalidServerCertificate` (accepts any certificate, meant for development relays; a warning is logged whenever it
  is set). See the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-email-smtp/3.0). ([#86](https://github.com/curiosus-dev/Curiosus.Utils/issues/86))
- **Breaking:** options use `Microsoft.Extensions.Options`. `AddSmtpEMailSender(string configSectionPath = "Smtp",
  Action<SmtpEMailOptions>? configure = null, bool useAsDefaultSender = true)` binds the options from the configuration
  section (`SmtpEMailOptions.SectionName`), applies `configure`, validates them when the app starts and returns
  `OptionsBuilder<SmtpEMailOptions>`: call `services.AddSmtpEMailSender()` instead of passing an instance.
  `SmtpEMailSender` takes `IOptions<SmtpEMailOptions>`. `ISmtpEMailOptions` is removed (use `SmtpEMailOptions`),
  `SmtpEMailOptions` no longer implements `IValidatableOptions`/`ILoggableOptions`, and `SmtpEMailOptionsValidator` is an
  `IValidateOptions<SmtpEMailOptions>` instead of a static class. The package no longer depends on
  `Curiosus.Configuration`. `SmtpPort` above 65535 now fails validation. See the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-email-smtp/3.0#options). ([#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44))

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.EMail.SMTP` to `Curiosus.EMail.SMTP` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.
- Upgraded `MailKit` up to `4.18.0` (fixes known vulnerabilities).

## [1.5.0] - 2026-02-13

### Changed

- Upgraded `Microsoft`'s packages up to `10.*` versions.
- Upgraded `MailKit` up to `3.6.0`.

## [1.4.0] - 2023-01-29

### Changed

- Upgraded `Microsoft`'s packages up to `6.*` versions.
- Upgraded `MailKit` up to `3.4.3`.

## [1.3.1] - 2022-06-20

### Changed

- Changed log level to `warn` instead of `error` at `SmtpEmailSender`.

## [1.3.0] - 2022-05-19

## Added

- Added option for ignoring incorrect extra params type.

## [1.2.1] - 2022-03-09

## Change

- Upgraded `Curiosity.Tools` to `1.4.5`

## [1.2.0] - 2021-12-02

### Changed

- `IEmailLogger` returns `Response` class object.

## [1.1.0] - 2021-09-30

### Added

- Added `IEMailExtraParams` implementation.
- Added `CancellationToken` to sender.

## [1.0.4] - 2021-03-11

### Added

- Added changelog file.

### Changed

- Upgrade `MailKit` to `v2.10.1`
