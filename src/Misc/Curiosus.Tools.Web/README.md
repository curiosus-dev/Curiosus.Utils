# Curiosus.Tools.Web

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Tools.Web)](https://www.nuget.org/packages/Curiosus.Tools.Web) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Tools.Web)](https://www.nuget.org/packages/Curiosus.Tools.Web) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Tools.Web.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

ASP.NET Core helpers for MVC sites and APIs: middleware (exception handling, request logging, performance
measuring, reverse proxy), model binders, data protection setup, reCAPTCHA, sitemap and assorted HTTP extensions.

## Installation

```bash
dotnet add package Curiosus.Tools.Web
```

## Usage

```csharp
using Curiosus.Tools.Web.Middleware;
using Curiosus.Tools.Web.MimeTypes;
using Curiosus.Tools.Web.ModelBinders;
using Curiosus.Tools.Web.ReverseProxy;

// services
services.AddReverseProxy(configuration.ReverseProxy);   // ForwardedHeaders for ProxyIps; no-op when null
services.AddMimeTypeMapping();                          // MimeMappingService
services.AddMvc(options => options.ModelBinderProviders.Insert(0, new TrimStringModelBinderProvider()));

// pipeline
app.UseReverseProxy(configuration.ReverseProxy);        // forwarded headers + PathBase
app.UseCuriosusExceptionHandler("/error");              // logs and re-executes the pipeline with /error
app.UsePerformanceMeasurer();
app.UseRequestLogging();                                // dumps request bodies, masks SensitiveDataProtector fields
app.UseRouting();
```

`UseRequestLogging` resolves a non-generic `ILogger` from DI (registered by
[Curiosus.Hosting.Web](https://www.nuget.org/packages/Curiosus.Hosting.Web)).

What else is inside:

- **Model binding** — `TrimStringModelBinderProvider`, `InvariantDecimalModelBinderProvider`,
  `DateTimeModelBinderProvider`, `DelimitedArrayModelBinder`, `TrimStringSystemJsonConverter` for `[FromBody]` strings.
- **Middleware** — `UseUserTracer` (trace id cookie, needs `UniqueIdGenerator.Initialize`),
  `UseLocalizationSetter` (language cookie), `UsePreviewDetector` (rewrites the path for link-preview bots).
- **Options-driven services** — `AddCuriosusDataProtection(DataProtectionOptions)` / `DisableDataProtection()`,
  `AddReCaptcha(ReCaptchaOptions)` + `ReCaptchaService`, `AddSiteMap(mvcBuilder)` + `SiteMapBuilder`,
  `ConfigureCookiesForOldBrowsers()` (SameSite fixes).
- **MVC** — `MVCBaseController` (error views, AJAX responses, redirects), `SiteError`,
  `ModelStateExtensions.AddModelErrorIf`, `Page<T>`/`Paginator`, `InvariantDecimalTagHelper`.
- **Extensions** — `HttpContext`/`HttpRequest` (`IsAjaxRequest`, `GetAbsoluteUrl`, ...), `ISession` JSON objects,
  `HtmlSanitizerHelper`.

`TrimStringNewtonsoftConverter` is obsolete and will be removed with the `Newtonsoft.Json` dependency.

## See also

- [Curiosus.Hosting.Web](https://www.nuget.org/packages/Curiosus.Hosting.Web) — ASP.NET Core app bootstrapper
- [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools) — non-web helpers
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
