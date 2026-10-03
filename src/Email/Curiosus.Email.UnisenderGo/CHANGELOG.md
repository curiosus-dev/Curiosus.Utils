# Changelog

## [3.0.0] - 2026-10-03

### Changed

- **Breaking:** the `UnisenderGoEmailSender(ILogger<UnisenderGoEmailSender> logger, UnisenderGoEmailOptions options)` constructor is replaced by one that also takes
  `IHttpClientFactory`. `AddUnisenderGoEmailSender` registers everything; when creating `UnisenderGoEmailSender` yourself, call
  `services.AddHttpClient()` and pass `IHttpClientFactory` from the service provider ([#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82)).
- HTTP calls use `HttpClient` from `IHttpClientFactory` instead of `RestSharp`: `AddUnisenderGoEmailSender` registers the named client
  `UnisenderGoEmailSender.HttpClientName`, configure it with `services.AddHttpClient(UnisenderGoEmailSender.HttpClientName, ...)`.
  The package no longer depends on `RestSharp`: reference it directly if your code used it through this package ([#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82)).
- **Breaking:** when `cancellationToken` is cancelled, `SendAsync` throws `OperationCanceledException` instead of returning
  a failed response. Network failures and `HttpClient` timeouts are still returned as a failed response ([#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82)).

### Fixed

- Network failures and timeouts are reported as `EmailError.Communication` instead of `EmailError.Unknown` ([#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82)).
- Responses in a charset .NET has no built-in encoding for (such as `windows-1251`) are decoded instead of failing.

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.EMail.UnisenderGo` to `Curiosus.EMail.UnisenderGo` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.
- Replaced `Newtonsoft.Json` with `System.Text.Json`, removed `RestSharp.Serializers.NewtonsoftJson` dependency.
- Upgraded `RestSharp` up to `114.0.0` (fixes known vulnerabilities).

### Fixed

- Subject and body were validated with the recipient address guard, which reported a misleading error message.

## [1.2.0] - 2026-02-13

### Changed

- Upgraded `Newtonsoft.Json` up to `13.0.4`.

## [1.1.0] - 2023-01-29

### Changed

- Upgraded `Newtonsoft.Json` up to `13.0.2`.

## [1.0.9] - 2023-08-08

### Fixed

- Upgraded RestSharp to `108.0.3`.

## [1.0.8] - 2022-07-12

### Fixed

- Simplified converting `bool?` to `int?`.

## [1.0.7] - 2022-07-07

### Added

- Added option for skipping unsubscribe footer from Unisender.

## [1.0.6] - 2022-06-20

### Changed

- Changed log level to `warn` instead of `error` at `UnisenderGoEmailSender`.

## [1.0.5] - 2022-06-17

### Added
                       
- Added extra info about failed request.

## [1.0.4] - 2022-05-24

### Fixed
                       
- Fixed parsing failed emails list.

## [1.0.3] - 2022-05-24

### Fixed
                       
- Fixed parsing failed emails list.

## [1.0.2] - 2022-05-23

### Fixed

- Fixed disabling tracking links/reads.

## [1.0.1] - 2022-05-18

## Changed

- `Validate` methods at `UnisenderGoEmailOptions` was made virtual.

## [1.0.0] - 2022-05-18

- First release.
