# Curiosus.Hosting

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Hosting)](https://www.nuget.org/packages/Curiosus.Hosting) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Hosting)](https://www.nuget.org/packages/Curiosus.Hosting) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Hosting.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Standard startup for Curiosus workers and tools on the .NET generic host: YAML configuration files, the configuration
logged on start with secrets masked, culture, app initializers, start/stop messages, systemd and Windows service
support and exit codes.

## Installation

```bash
dotnet add package Curiosus.Hosting
```

## Usage

A worker (hosted services run until the app is stopped):

```csharp
var builder = CuriosusHost.CreateBuilder(args).AddCuriosusDefaults();
builder.AddCuriosusNLog();                                  // optional: log through NLog
builder.Services.AddCuriosusOptions<SmtpOptions>("Smtp");   // options per section (Curiosus.Configuration)
builder.Services.AddHostedService<Worker>();

return await CuriosusApp.RunAsync(builder.Build());
```

A tool (one command, its result is the exit code):

```csharp
var builder = CuriosusHost.CreateBuilder(args).AddCuriosusDefaults();
builder.Services.AddSingleton<Importer>();

return await CuriosusApp.RunAsync(builder.Build(), (services, cancellationToken) =>
    services.GetRequiredService<Importer>().RunAsync(cancellationToken));
```

Web apps use [Curiosus.Hosting.Web](https://www.nuget.org/packages/Curiosus.Hosting.Web):
`CuriosusWebHost.CreateBuilder(args).AddCuriosusDefaults()`.

### Configuration

`AddCuriosusDefaults` (or `AddCuriosusConfiguration` alone) loads, from the content root or the directory passed with
`-c` / `--config`: `config.yml`, `config.secret.yml`, `config.{Environment}.yml`, `config.{Environment}.secret.yml`
and, outside `Production`, `config.{Environment}.{UserName}.yml` (+ `.secret`). User secrets, environment variables
and command line arguments still override the files. `-c` can also name a single file, which is then loaded alone.

The environment comes from `DOTNET_ENVIRONMENT`, then `ASPNETCORE_ENVIRONMENT`, then `Production`. The content root
is the binaries directory, wherever the app is started from (`--contentRoot` overrides it): `config.yml` without `-c`,
`NLog.config` and other relative paths are found next to the dll. A relative `-c` is
relative to the current directory, as usual on the command line.

```yaml
AppName: Sample Worker      # used in the start/stop messages and as ${var:appname} of NLog
Culture:
  DefaultCulture: en-US     # applied process-wide before hosted services start
UniqueId:
  GeneratorId: 1            # initializes UniqueIdGenerator
Log:                        # AddCuriosusNLog only
  LogConfigurationPath: NLog.config
  LogOutputDirectory: logs
```

### What AddCuriosusDefaults adds

- the configuration logged on start (`LogConfigurationOnStart` of Curiosus.Configuration), secrets masked;
- `TimeProvider.System` unless a clock is registered, `PerformanceMeasurer`, localization (`Resources`);
- before hosted services start: the `Culture` section, `UniqueIdGenerator`, then every `IAppInitializer` in
  registration order;
- `{AppName} is started` / `is stopped` written to the `appStartEmailLog` / `appStopEmailLog` loggers;
- systemd and Windows service lifetimes (no-op otherwise).

It doesn't touch logging providers: `builder.AddCuriosusNLog()` sets NLog up from the `Log` section (`${var:outputdir}`,
`${var:appname}`, `${configsetting:item=Section.Key}`). An app that moves to centralized log collection drops that call.

### Exit codes

`CuriosusApp.RunAsync` disposes the host and returns `CuriosusExitCodes`: `Success` (0), `InvalidOptions` (1, options
failed validation on start), `UnhandledException` (2, also when a `BackgroundService` failed and stopped the host),
`Cancellation` (3, the app was stopped while starting or while the tool's command ran), or the command's own code.

### Command line

Out of the box: `-c` / `--config <directory or file>`, `-v` / `--version` (prints `App version: x.y.z.w` and exits
with 0 before anything else runs), `--environment`, `--contentRoot` and `--Section:Key value` to override any setting.

Typed command line options and subcommands: parse them with System.CommandLine and build the host inside the command
action, see the [migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-hosting/4.0#command-line-arguments)
and the [sample tool](https://github.com/curiosus-dev/Curiosus.Utils/blob/main/samples/Curiosus.SMS.Iqsms.Sample/Program.cs).

Other helpers:

- `CuriosusWatchdog` — base `BackgroundService` that runs `ProcessAsync` at a fixed rate on a `PeriodicTimer`, pauses
  after an exception and takes a `TimeProvider` for tests.
- `AddTempDirCleaner(TempFileOptions)` — hosted service that deletes stale temp files.

## See also

- [Migration guides](https://curiosus-dev.github.io/Curiosus.Utils/migrations) — upgrading from `Curiosity.*` 1.x and between major versions
- [Curiosus.Hosting 4.0 migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-hosting/4.0)
- [Curiosus.Hosting.Web](https://www.nuget.org/packages/Curiosus.Hosting.Web) — the same for ASP.NET Core apps
- [Curiosus.Configuration](https://www.nuget.org/packages/Curiosus.Configuration) — `AddCuriosusOptions`, `LogConfigurationOnStart`
- [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools) — `IAppInitializer` and other basic helpers
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
