# Changelog

## [3.0.0] - 2026-10-03

### Changed

- **Breaking:** the `MailgunEmailSender(ILogger<MailgunEmailSender> logger, MailgunEmailOptions options)` constructor is replaced by one that also takes
  `IHttpClientFactory`. `AddMailgunEmailSender` registers everything; when creating `MailgunEmailSender` yourself, call
  `services.AddHttpClient()` and pass `IHttpClientFactory` from the service provider ([#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82)).
- HTTP calls use `HttpClient` from `IHttpClientFactory` instead of `RestSharp`: `AddMailgunEmailSender` registers the named client
  `MailgunEmailSender.HttpClientName`, configure it with `services.AddHttpClient(MailgunEmailSender.HttpClientName, ...)`.
  The package no longer depends on `RestSharp`: reference it directly if your code used it through this package ([#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82)).
- **Breaking:** when `cancellationToken` is cancelled, `SendAsync` throws `OperationCanceledException` instead of returning
  a failed response. Network failures and `HttpClient` timeouts are still returned as a failed response ([#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82)).
- HTTP error status codes map to `EmailError` by meaning instead of `EmailError.Auth` for everything except 420:
  400 → `IncorrectRequestData`, 401 and 403 → `Auth`, 420 and 429 → `RateLimit`, 5xx → `Communication`,
  other codes → `Unknown` ([#83](https://github.com/curiosus-dev/Curiosus.Utils/issues/83)).

### Fixed

- `MailgunEmailSender` could not be created: `MailgunEmailOptions.AssertValid()` threw `NotImplementedException`.
  The stub is removed, options are validated by the `AssertValid()` extension of `Curiosus.Configuration` ([#12](https://github.com/curiosus-dev/Curiosus.Utils/issues/12), [#83](https://github.com/curiosus-dev/Curiosus.Utils/issues/83)).
- Network failures and timeouts are reported as `EmailError.Communication` instead of `EmailError.Auth` ([#83](https://github.com/curiosus-dev/Curiosus.Utils/issues/83)).
- Responses in a charset .NET has no built-in encoding for (such as `windows-1251`) are decoded instead of failing.

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.EMail.Mailgun` to `Curiosus.EMail.Mailgun` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.
- Replaced `Newtonsoft.Json` with `System.Text.Json`.
- Upgraded `RestSharp` up to `114.0.0` (fixes known vulnerabilities).

### Fixed

- `replyTo` was sent as `h:Reply-T` header instead of `h:Reply-To`, so replies went to the sender address.

## [1.5.0] - 2026-02-13

### Changed

- Upgraded `Microsoft`'s packages up to `10.*` versions.
- Upgraded `Newtonsoft.Json` up to `13.0.4`.

## [1.4.0] - 2023-01-29

### Changed

- Upgraded `Microsoft`'s packages up to `6.*` versions.
- Upgraded `Newtonsoft.Json` up to `13.0.2`.
- Upgraded `RestSharp` up to `108.0.3`.

## [1.3.6] - 2022-06-20

### Changed

- Changed log level to `warn` instead of `error` at `MailgunEmailSender`.

## [1.3.5] - 2022-05-19

## Added

- Added option for ignoring incorrect extra params type.

## [1.3.4] - 2022-03-09

## Change

- Upgraded `Curiosity.Tools` to `1.4.5`

## [1.3.3] - 2022-02-11

### Added

- ReplyTo address support.

## [1.3.2] - 2021-12-15

### Added

- New email result for rate limit errors.

## [1.3.1] - 2021-12-13

### Changed

- Replaced MailGun by Mailgun in MailgunEmailOptions

## [1.3.0] - 2021-12-13

### Added

- Mailgun region selection in options and extra params.

## [1.2.1] - 2021-12-02

### Added

- Sending email as html
- Improved logging

## [1.2.0] - 2021-12-02

### Added

- Email sending result;
- Mailgun user to options and extra params;

### Changed

- `IEmailLogger` returns `Response` class object.

## [1.1.0] - 2021-09-30

### Added

- Added `IEMailExtraParams` implementation.
- Added `CancellationToken` to sender.

## [1.0.1] - 2021-03-11

### Added

- Added changelog file.
