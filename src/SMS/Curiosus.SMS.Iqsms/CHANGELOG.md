# Changelog

## [Unreleased]

### Security

- The login and the password were sent in the URL query (`GET …/messages/v2/send?login=…&password=…`), where outbound
  proxies, egress gateways, TLS-inspecting firewalls, APM agents and the provider's access logs record them; phone numbers
  and texts leaked the same way, and the full phone number was logged at `Information`. The sender now posts JSON to
  `https://api.iqsms.ru/messages/v2/send.json` with everything in the body, and logs phone numbers masked
  (`+7******4567`). **Rotate the iqsms password** if such logs were kept. ([#84](https://github.com/curiosus-dev/Curiosus.Utils/issues/84))

### Changed

- The response of the JSON API is parsed: message status `accepted` → success; `not enough balance`/`not enough credits`
  → `NoMoney`; `invalid mobile phone` → `DeliveryError`; a package-level error about authorization or HTTP 401 → `Auth`;
  non-2xx, network errors and timeouts → `Communication`; anything else → `Unknown`. `SmsSentResult.ResponseJson` holds
  the raw JSON. Log messages are templates in English. ([#84](https://github.com/curiosus-dev/Curiosus.Utils/issues/84))

## [3.0.0] - 2026-10-03

### Changed

- **Breaking:** the `IqsmsSender(ILogger<IqsmsSender> logger, IqsmsOptions options)` constructor is replaced by one that also takes
  `IHttpClientFactory`. `AddIqsmsSender` registers everything; when creating `IqsmsSender` yourself, call
  `services.AddHttpClient()` and pass `IHttpClientFactory` from the service provider ([#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82)).
- HTTP calls use `HttpClient` from `IHttpClientFactory` instead of `RestSharp`: `AddIqsmsSender` registers the named client
  `IqsmsSender.HttpClientName`, configure it with `services.AddHttpClient(IqsmsSender.HttpClientName, ...)`.
  The package no longer depends on `RestSharp`: reference it directly if your code used it through this package ([#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82)).
- **Breaking:** when `cancellationToken` is cancelled, `SendSmsAsync` throws `OperationCanceledException` instead of returning
  a failed response. Network failures and `HttpClient` timeouts are still returned as a failed response ([#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82)).

### Fixed

- Responses in a charset .NET has no built-in encoding for (such as `windows-1251`) are decoded instead of failing.

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.SMS.Iqsms` to `Curiosus.SMS.Iqsms` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.
- Upgraded `RestSharp` up to `114.0.0` (fixes known vulnerabilities).

## [1.1.0]

Upgraded dependencies.

## [1.0.3]

Convert response with text "invalid mobile phone" to delivery error.

## [1.0.2]

Detect no money message.

## [1.0.0] - 2023-08-04

First release.
