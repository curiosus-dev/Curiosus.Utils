# Curiosus.Hosting.Web

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Hosting.Web)](https://www.nuget.org/packages/Curiosus.Hosting.Web) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Hosting.Web)](https://www.nuget.org/packages/Curiosus.Hosting.Web) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Hosting.Web.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Bootstrapper for ASP.NET Core apps built on [Curiosus.Hosting](https://www.nuget.org/packages/Curiosus.Hosting):
YAML config with validation, NLog, Kestrel or IIS setup, thread pool tuning and app initializers, plus
`ConfigureCuriosusMvc` with the common MVC setup.

## Installation

```bash
dotnet add package Curiosus.Hosting.Web
```

## Usage

```csharp
public class AppConfiguration : CuriosusWebAppConfiguration
{
    public AppConfiguration()
    {
        AppName = "Sample Web App";
    }
}

public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        var configuration = services.BuildServiceProvider().GetRequiredService<AppConfiguration>();
        services.ConfigureCuriosusMvc(configuration);
    }

    public void Configure(IApplicationBuilder app)
    {
        app.UseRouting();
        app.UseEndpoints(endpoints => endpoints.MapControllers());
    }
}

// Program.cs
var bootstrapper = new CuriosusWebAppBootstrapper<CuriosusCLIArguments, AppConfiguration, Startup>();
return await bootstrapper.RunAsync(args);
```

`config.yml` must set either `Urls` or a `Kestrel` section (not both), unless `UseIISIntegration` is `true`:

```yaml
Urls: http://127.0.0.1:5000
Log:
  LogConfigurationPath: ./NLog.config
  LogOutputDirectory: ./logs
ThreadPool:
  MinWorkerThreads: 64
SensitiveDataFieldNames:
  - password
```

`Kestrel` is passed to Kestrel as is (e.g. `Kestrel:Endpoints:Https:Url` and `Certificate:Path`/`Password`).
`ConfigureCuriosusMvc` adds MVC with string trimming for bound values, localized model binding messages,
data annotations localization, Razor runtime compilation, `IMemoryCache` and a `SensitiveDataProtector`
for `SensitiveDataFieldNames`. Implement `IWebAppConfigurationWithPublicDomain` to require and register `PublicDomain`.

## See also

- [Curiosus.Hosting](https://www.nuget.org/packages/Curiosus.Hosting) — bootstrappers for console tools and services
- [Curiosus.Tools.Web](https://www.nuget.org/packages/Curiosus.Tools.Web) — middleware, model binders and web helpers
- [Sample web app](https://github.com/curiosus-dev/Curiosus.Utils/blob/master/samples/Curiosus.SampleWebApp/Program.cs)
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
