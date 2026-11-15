# Changelog

## [Unreleased]

### Security

- Stored XSS through `HtmlSanitizerHelper`: it HTML-decoded the sanitizer output, so encoded markup such as
  `&lt;script&gt;` passed the sanitizer as text and came out as a live `<script>`. Content saved through the helper and
  rendered with `Html.Raw` must be treated as unsanitized: sanitize it again on output. See the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-tools-web/4.0#htmlsanitizerhelper--ihtmlsanitizer). ([#61](https://github.com/curiosus-dev/Curiosus.Utils/issues/61))
- `MVCBaseController`: `RedirectInternal` redirected to any `returnUrl` (open redirect after a login, for example to
  `//evil.example`), the `AjaxError` view rendered the exception message as raw HTML (XSS through user input in
  exception messages), error responses showed exception messages (SQL, paths, hosts), the error action took its text from
  the query string, and every public helper was reachable by URL. See the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-tools-web/4.0#mvcbasecontroller-redirects-and-error-pages). ([#62](https://github.com/curiosus-dev/Curiosus.Utils/issues/62))
- `AddCuriosusDataProtection` with `IsEnabled: false` (also `DisableDataProtection()`) replaced the registered
  `IDataProtectionProvider` with a stub whose `Protect` returned the input unchanged, whenever `AddDataProtection()` had
  been called before it, directly or through `AddAuthentication`, antiforgery or Identity. Auth cookies were then only
  Base64Url-encoded (a user could change their own claims), and TempData and app payloads were readable and forgeable.
  The stub is removed: data is always protected. Environments that ran it lose their issued cookies once. See the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-tools-web/4.0#dataprotection). ([#60](https://github.com/curiosus-dev/Curiosus.Utils/issues/60))

### Fixed

- `RequestLoggingMiddleware` takes `ILogger<RequestLoggingMiddleware>` instead of a non-generic `ILogger`, which
  Curiosus.Hosting 4.0 no longer registers; its log category is the class name instead of `main`.
  ([#41](https://github.com/curiosus-dev/Curiosus.Utils/issues/41))

### Changed

- **Breaking:** `HtmlSanitizerHelper` is replaced by `services.AddCuriosusHtmlSanitizer(configure)`, which registers a
  singleton `Ganss.Xss.IHtmlSanitizer`: its output is not decoded, and the default allow-list no longer contains `id`
  and `class` (DOM clobbering, borrowed page styles); `configure` can add them back. See the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-tools-web/4.0#htmlsanitizerhelper--ihtmlsanitizer). ([#61](https://github.com/curiosus-dev/Curiosus.Utils/issues/61))
- **Breaking:** `MVCBaseController.RedirectInternal` redirects only to local URLs (`Url.IsLocalUrl`) and to `http(s)`
  URLs on the hosts of the new `RedirectAllowListOptions` (public domains, the front-end), anything else goes to
  Home/Index; the routable error endpoint is `HandleError` under the action name `ErrorView`, binds no parameters
  and accepts any HTTP method; `ErrorView(errorText)`, `ErrorViewWithCode`, `ErrorViewParams`, `AjaxGridErrorResponse`,
  `AjaxError` and `AjaxSuccess` are `[NonAction]` helpers; error pages show the app's text or the generic message, never
  the exception message; `AjaxError.cshtml` HTML-encodes the text. See the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-tools-web/4.0#mvcbasecontroller-redirects-and-error-pages). ([#62](https://github.com/curiosus-dev/Curiosus.Utils/issues/62))
- **Breaking:** the error actions of `MVCBaseController` (`ErrorView`, `ErrorViewWithCode`, `ErrorViewParams`) no
  longer take a `[FromServices] ILogger` parameter, which failed to resolve with Curiosus.Hosting 4.0: they create
  their logger from `ILoggerFactory` with the actual controller as the category (`HomeController`) instead of `main`. Drop the
  logger argument where a subclass calls them. See the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-tools-web/4.0).
  ([#41](https://github.com/curiosus-dev/Curiosus.Utils/issues/41))
- **Breaking:** `AddCuriosusDataProtection(string configSectionPath = "DataProtection")` replaces
  `AddCuriosusDataProtection(DataProtectionOptions)`
  and returns `IDataProtectionBuilder`: always `AddDataProtection()`, with `ApplicationName`, `KeyRingPath` and
  `DisableAutomaticKeyGeneration` of `CuriosusDataProtectionOptions` (validated on start). `DataProtectionOptions`,
  `IsEnabled`, `DisableDataProtection()`, `SecureLessDataProtectionProvider` and `SecureLessDataProtector` are removed,
  and automatic key generation is no longer disabled unless asked for. See the
  [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-tools-web/4.0#dataprotection). ([#60](https://github.com/curiosus-dev/Curiosus.Utils/issues/60))

## [3.0.0] - 2026-10-03

### Changed

- **Breaking:** the `ReCaptchaService(ReCaptchaOptions options)` constructor is replaced by one that also takes
  `IHttpClientFactory`. `AddReCaptcha` registers everything; when creating `ReCaptchaService` yourself, call
  `services.AddHttpClient()` and pass `IHttpClientFactory` from the service provider ([#67](https://github.com/curiosus-dev/Curiosus.Utils/issues/67)).
- HTTP calls use `HttpClient` from `IHttpClientFactory` instead of `Flurl.Http`: `AddReCaptcha` registers the named client
  `ReCaptchaService.HttpClientName`, configure it with `services.AddHttpClient(ReCaptchaService.HttpClientName, ...)`.
  The package no longer depends on `Flurl.Http`: reference it directly if your code used it through this package ([#67](https://github.com/curiosus-dev/Curiosus.Utils/issues/67)).
- **Breaking:** `ReCaptchaService.VerifyReCaptchaAsync` throws `System.Net.Http` and `System.Text.Json` exceptions
  instead of `FlurlHttpException`: `HttpRequestException` when the reCAPTCHA API is unavailable or returns an error
  status code, `TaskCanceledException` on timeout, `JsonException` when the response is not a verification result.
  Catch these three instead of `FlurlHttpException` ([#67](https://github.com/curiosus-dev/Curiosus.Utils/issues/67)).
- **Breaking:** `ReCaptchaService.VerifyReCaptchaAsync` takes an optional `CancellationToken` (recompile callers) ([#67](https://github.com/curiosus-dev/Curiosus.Utils/issues/67)).
- `ReCaptchaService.VerifyReCaptchaAsync` posts `secret` and `response` as a form, as the reCAPTCHA documentation
  describes, instead of sending them in the URL query ([#67](https://github.com/curiosus-dev/Curiosus.Utils/issues/67)).
- `MVCBaseController.Redirect` encodes non-ASCII and other illegal URL characters without Flurl, with the same result ([#67](https://github.com/curiosus-dev/Curiosus.Utils/issues/67)).

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.Tools.Web` to `Curiosus.Tools.Web` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- **Breaking:** user trace cookie renamed from `curiosity-user-trace-id` to `curiosus-user-trace-id`: existing visitors get a new trace id once.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.
- `SessionExtensions` use `System.Text.Json` instead of `Newtonsoft.Json`; values stored by previous versions remain readable.
- Upgraded `Flurl.Http` up to `4.0.2` (drops transitive vulnerable `Newtonsoft.Json` `12.0.2`) and `HtmlSanitizer` up to `9.2.1039` (fixes known vulnerabilities).

### Deprecated

- `TrimStringNewtonsoftConverter`: use `TrimStringSystemJsonConverter`. `Newtonsoft.Json` support will be removed in the next major version ([#77](https://github.com/siisltd/Curiosity.Utils/issues/77)).

## [1.4.0] - 2026-02-13

### Changed

- Build target changed to `net10.0`
- Upgraded `Microsoft`'s packages up to `10.*` versions.
- Upgraded `HtmlSanitizer` up to `8.1.870`.

## [1.3.1] 

### Added

- Cancellation exception handling, write a message to WARN log

## [1.3.0] - 2023-11-28

### Changed

- Upgraded `HtmlSanitizer` up to `8.0.723`.

## [1.2.0] - 2023-01-29

### Changed

- Upgraded `Microsoft`'s packages up to `6.*` versions.
- Upgraded `HtmlSanitizer` up to `8.0.645`.
- Upgraded `Flurl.Http` up to `3.2.4`.

## [1.1.9] - 2022-07-15

### Changed

- Modified `ModelBinderProvider` classes to use `ILoggerFactory` from context services.

## [1.1.8] - 2022-03-12

### Changed

- Made `PathBase` in `ReverseProxyOptions` optional.

## [1.1.7] - 2021-11-29

### Changed

- Removed extra slash in request logger

## [1.1.6] - 2021-11-18

### Changed

- Improved enum writer to swagger descriptor

### Added

- Added query string to request logger

## [1.1.5] - 2021-11-16

### Added

- Added null reference annotations.

## [1.1.4] - 2021-11-10

### Added

- Added `SwaggerDescriptor`.

## [1.1.3] - 2021-07-29

### Added

- Added `DelimitedArrayModelBinder`.

## [1.1.2] - 2021-07-29

### Added

- Added `TrimStringNewtonsoftConverter`.

### Changed

- Renamed `TrimStringConverter` to `TrimStringSystemJsonConverter`.

## [1.1.1] - 2021-04-15

### Changed

- Printing `ViewBag.ErrorText` as raw html at `AjaxError` view.

## [1.1.0] - 2021-03-27

### Changed

- Made `MVCBaseController` abstract.

### Removed

- Replaced `SiteMapController` by `SiteMapBuilder`.

## [1.0.11] - 2021-03-26

### Added

- Added key ring dir existence check.

## [1.0.10] - 2021-03-26

### Added

- Added ASP.NET Core data protection.

## [1.0.9] - 2021-03-20

### Added

- Added HTML Sanitizer

## [1.0.8] - 2021-03-13

### Added

- Added Sensitive data protector to request logging middleware.
- Added extension methods for middleware.

## [1.0.7] - 2021-03-10

### Added

- Added json converter for trimming strings.

## [1.0.6] - 2021-03-08

### Fixed

- Fixed localization.

## [1.0.5] - 2021-03-08

### Changed

- Upgraded `System.ComponentModel.Annotations` to `v5.0.0`.

## [1.0.4] - 2021-03-08

### Added

- Added MVC base controller.

### Changed

- Changed behavior of exception handle middleware from `.NET Core 2.1` to `.NET Core 3.1`.
