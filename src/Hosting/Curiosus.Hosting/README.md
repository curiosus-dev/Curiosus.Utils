# Curiosus.Hosting

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Hosting)](https://www.nuget.org/packages/Curiosus.Hosting) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Hosting)](https://www.nuget.org/packages/Curiosus.Hosting) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Hosting.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

App bootstrappers for console tools and background services. They parse CLI arguments, load and validate a YAML config,
set up NLog and the default culture, run registered `IAppInitializer`s and then start the app.

## Installation

```bash
dotnet add package Curiosus.Hosting
```

## Usage

Inherit a configuration from `CuriosusAppConfiguration` and a bootstrapper from `CuriosusServiceAppBootstrapper`
(generic host with hosted services) or `CuriosusToolAppBootstrapper` (a DI container plus a one-shot `ExecuteAsync`).

```csharp
public class AppConfiguration : CuriosusAppConfiguration
{
    public AppConfiguration()
    {
        AppName = "Sample Worker";
    }
}

public class AppBootstrapper : CuriosusServiceAppBootstrapper<CuriosusCLIArguments, AppConfiguration>
{
    public AppBootstrapper()
    {
        ConfigureServices((context, services, configuration) =>
        {
            services.AddHostedService<SampleWorker>();
        });
    }
}

// Program.cs
return await new AppBootstrapper().RunAsync(args);
```

The config is read from `config.yml` in the directory passed with `-c|--config` (the current working directory
by default). It is overridden by `config.secret.yml`, `config.{ASPNETCORE_ENVIRONMENT}.yml`, environment variables
and CLI arguments.

```yaml
AppName: Sample Worker
Culture:
  DefaultCulture: en-US
Log:
  LogConfigurationPath: ./NLog.config
  LogOutputDirectory: ./logs
```

`RunAsync` returns an exit code from `CuriosusExitCodes` (e.g. `IncorrectConfiguration` when validation fails).
The configuration, the CLI arguments and a non-generic `ILogger` are registered in DI.

Other helpers:

- `CuriosusWatchdog` — base `BackgroundService` that calls `ProcessAsync` periodically and survives exceptions.
- `AddThreadPoolTuning(ThreadPoolOptions)` — sets minimum thread pool size, logs its state and can auto-tune it.
- `AddTempDirCleaner(TempFileOptions)` — hosted service that deletes stale temp files.
- `IConfigurationWithMailLogger` — implement it in your config to pass SMTP settings (`LoggerMail`) to the mail
  target variables of your `NLog.config`.

## See also

- [Curiosus.Hosting.Web](https://www.nuget.org/packages/Curiosus.Hosting.Web) — bootstrapper for ASP.NET Core apps
- [Curiosus.Configuration.YML](https://www.nuget.org/packages/Curiosus.Configuration.YML) — YAML configuration provider
- [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools) — `IAppInitializer` and other basic helpers
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
