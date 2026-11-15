# Curiosus.Hosting.Web

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Hosting.Web)](https://www.nuget.org/packages/Curiosus.Hosting.Web) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Hosting.Web)](https://www.nuget.org/packages/Curiosus.Hosting.Web) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Hosting.Web.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

The [Curiosus.Hosting](https://www.nuget.org/packages/Curiosus.Hosting) startup for ASP.NET Core apps:
`CuriosusWebHost.CreateBuilder(args)` creates a `WebApplicationBuilder` with the same conventions (`-c` / `--config`,
`-v` / `--version`, content root of a service app), and `AddCuriosusDefaults()` adds YAML configuration, culture, app initializers and the
rest of the Curiosus defaults.

## Installation

```bash
dotnet add package Curiosus.Hosting.Web
```

## Usage

```csharp
var builder = CuriosusWebHost.CreateBuilder(args).AddCuriosusDefaults();
builder.AddCuriosusNLog(configureNLogExtensions: extensions => extensions.RegisterNLogWeb()); // optional, ${aspnet-*}
builder.Services.AddCuriosusOptions<SiteOptions>("Site");
builder.Services.AddControllersWithViews();

var app = builder.Build();
app.MapControllers();

return await CuriosusApp.RunAsync(app);
```

Kestrel is configured by ASP.NET Core itself: `Urls` (or `ASPNETCORE_URLS`, `--urls`) or the `Kestrel` section of
`config.yml`, IIS integration comes with `WebApplication`. The package brings `NLog.Web.AspNetCore` for
`RegisterNLogWeb()`.

## See also

- [Migration guides](https://curiosus-dev.github.io/Curiosus.Utils/migrations) — upgrading from `Curiosity.*` 1.x and between major versions
- [Curiosus.Hosting.Web 4.0 migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-hosting-web/4.0)
- [Curiosus.Hosting 4.0 migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-hosting/4.0)
- [Curiosus.Hosting](https://www.nuget.org/packages/Curiosus.Hosting) — the startup for workers and tools
- [Curiosus.Tools.Web](https://www.nuget.org/packages/Curiosus.Tools.Web) — middleware, model binders and web helpers
- [Sample web app](https://github.com/curiosus-dev/Curiosus.Utils/blob/main/samples/Curiosus.SampleWebApp/Program.cs)
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
