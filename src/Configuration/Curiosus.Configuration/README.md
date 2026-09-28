# Curiosus.Configuration

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Configuration)](https://www.nuget.org/packages/Curiosus.Configuration) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Configuration)](https://www.nuget.org/packages/Curiosus.Configuration) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Configuration.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Building blocks for strongly typed application configuration: options validation (`IValidatableOptions`),
options logging (`ILoggableOptions`, `ConfigurationPrinter`) and a base class for file-based configuration
providers. Most Curiosus packages validate their options with these types.

## Installation

```bash
dotnet add package Curiosus.Configuration
```

## Usage

Implement `IValidatableOptions` to validate options and `ILoggableOptions` to print them at startup:

```csharp
using Curiosus.Configuration;

public class AppOptions : IValidatableOptions, ILoggableOptions
{
    public string ServiceUrl { get; set; } = null!;
    public int Port { get; set; } = 8080;

    public IReadOnlyCollection<ConfigurationValidationError> Validate(string? prefix = null)
    {
        var errors = new ConfigurationValidationErrorCollection(prefix);
        errors.AddErrorIf(String.IsNullOrWhiteSpace(ServiceUrl), nameof(ServiceUrl), "can't be empty");
        errors.AddErrorIf(Port <= 0, nameof(Port), "must be positive");
        return errors;
    }
}

var options = builder.Configuration.Get<AppOptions>()!;
options.AssertValid(); // throws ConfigurationValidationException with all errors
Console.WriteLine(new ConfigurationPrinter().GetLog(options)); // prints all public properties
```

`ConfigurationValidationErrorCollection` prefixes field names (`prefix:Field`), which is handy for nested options.

### Configuration providers

`ConfigurationProviderBase<T>` (exposed as `IConfigurationProvider<T>`) loads, in order: `config`, `config.secret`,
then — when `ASPNETCORE_ENVIRONMENT` is set and is not `Production` — `config.{env}`, `config.{env}.secret`,
`config.{env}.{USER}` and `config.{env}.{USER}.secret`, then environment variables and command line arguments.
Only `config` is required (unless `isConfigOptional` is `true`). Derived classes decide the file format;
see [Curiosus.Configuration.YML](https://www.nuget.org/packages/Curiosus.Configuration.YML) for YAML.

## See also

- [Curiosus.Configuration.YML](https://www.nuget.org/packages/Curiosus.Configuration.YML) — YAML configuration provider
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
