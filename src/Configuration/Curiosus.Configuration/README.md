# Curiosus.Configuration

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Configuration)](https://www.nuget.org/packages/Curiosus.Configuration) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Configuration)](https://www.nuget.org/packages/Curiosus.Configuration) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Configuration.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Options bound from configuration and validated at start (`AddCuriosusOptions<T>`) and a startup configuration dump
with secrets masked (`LogConfigurationOnStart`), on top of `Microsoft.Extensions.Options`.

## Installation

```bash
dotnet add package Curiosus.Configuration
```

## Usage

```csharp
using System.ComponentModel.DataAnnotations;
using Curiosus.Configuration;

public class SmtpOptions
{
    [Required] public string Host { get; set; } = null!;
    [Range(1, 65535)] public int Port { get; set; } = 25;
}

services.AddCuriosusOptions<SmtpOptions>("Smtp");                   // IOptions<SmtpOptions>, validated at start
services.AddCuriosusOptions<SmtpOptions>("backup", "Mail:Backup");  // named: IOptionsMonitor<SmtpOptions>.Get("backup")
services.LogConfigurationOnStart();                                 // startup dump of the whole configuration
```

`AddCuriosusOptions<T>` binds the section, validates data annotations (nested objects with `[ValidateObjectMembers]`
and `[ValidateEnumeratedItems]`) and fails the host start with an `OptionsValidationException` when the options are
invalid; it returns the `OptionsBuilder<T>` for more rules. Libraries don't need it: they call `AddOptions<T>()` with
`ValidateOnStart()` themselves.

`LogConfigurationOnStart` logs one line per value with the provider it came from, so you see whether a value came from
a file, an environment variable or the command line. Without arguments it prints the whole configuration except
environment variables without a section (`PATH`, `HOSTNAME`, ...); pass sections to print only them, or exclude some
with `LogConfigurationOnStart(o => o.ExcludedSections.Add("Serilog"))`. Secrets are masked by the rules below.

### Deprecated API

The types below are removed in 3.0, see the
[migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-configuration/3.0).
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
Console.WriteLine(new ConfigurationPrinter().GetLog(options)); // prints all public properties, secrets masked
```

Masking (`ConfigurationPrinter` and `LogConfigurationOnStart`): `***` is printed instead of the value of every
property or dictionary key whose name contains `password`, `pwd`, `passphrase`, `secret`, `token`, `credential`,
`apikey`, `privatekey` (not `privatekeypath`), `signature`, `salt` or ends with `key` or `keys` (case-insensitive),
and of every value with a URI that has user info (`amqp://user:pass@host`). For names containing `connectionstring`
it prints only the values of well-known non-secret keys (host, port, database, user, timeouts, ...) and masks the rest; a value that can't be parsed or contains a URI (`amqp://user:pass@host`) is masked entirely.

Name matching can be overridden per property:

```csharp
public class AppOptions : ILoggableOptions
{
    [Secret]
    public string Endpoint { get; set; } = ""; // URL with credentials: always masked

    [NotSecret]
    public string ReCaptchaClientKey { get; set; } = ""; // public site key: printed as is
}
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
