# Curiosus.Configuration.YML

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Configuration.YML)](https://www.nuget.org/packages/Curiosus.Configuration.YML) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Configuration.YML)](https://www.nuget.org/packages/Curiosus.Configuration.YML) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Configuration.YML.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

YAML configuration provider: `YamlConfigurationProvider<T>` reads layered `config*.yml` files, environment
variables and command line arguments and binds them to a POCO. Use it when the application is configured
with YAML files instead of `appsettings.json`.

## Installation

```bash
dotnet add package Curiosus.Configuration.YML
```

## Usage

Files are looked up in the given directory (the current directory if `null`), in this order:

| File | Required | Loaded when |
|---|---|---|
| `config.yml` | yes (unless `isConfigOptional: true`) | always |
| `config.secret.yml` | no | always |
| `config.{env}.yml`, `config.{env}.secret.yml` | no | `ASPNETCORE_ENVIRONMENT` is set and is not `Production` |
| `config.{env}.{USER}.yml`, `config.{env}.{USER}.secret.yml` | no | as above and `USER` is set |

Environment variables and command line arguments are applied on top. Keep `*.secret.yml` out of source control.

```yaml
# config.yml
ServiceUrl: https://example.com
Smtp:
  Host: smtp.example.com
  Port: 587
```

```csharp
using Curiosus.Configuration;

var provider = new YamlConfigurationProvider<AppConfiguration>(configurationBasePath: null, cliArgs: args);
var configuration = provider.GetConfiguration(); // strongly typed, cached
var raw = provider.GetRawConfiguration();        // IConfiguration

// or plug the same sources into a host
var builder = WebApplication.CreateBuilder(args);
provider.ConfigureAppConfiguration(builder.Configuration);

public class AppConfiguration
{
    public string ServiceUrl { get; set; } = null!;
    public SmtpSection Smtp { get; set; } = new();
}

public class SmtpSection
{
    public string Host { get; set; } = null!;
    public int Port { get; set; }
}
```

Note: the provider lives in the `Curiosus.Configuration` namespace.

## See also

- [Curiosus.Configuration](https://www.nuget.org/packages/Curiosus.Configuration) — options validation, logging and the base provider
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
