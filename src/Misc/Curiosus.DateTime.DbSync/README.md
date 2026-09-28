# Curiosus.DateTime.DbSync

[![NuGet](https://img.shields.io/nuget/v/Curiosus.DateTime.DbSync)](https://www.nuget.org/packages/Curiosus.DateTime.DbSync) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.DateTime.DbSync)](https://www.nuget.org/packages/Curiosus.DateTime.DbSync) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.DateTime.DbSync.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

`IDateTimeService` that returns the current UTC time of the database server instead of the local clock.
Use it when several app instances must agree on "now" (expirations, schedules) even if their clocks drift.

## Installation

```bash
dotnet add package Curiosus.DateTime.DbSync
```

## Usage

```yaml
DbDateTime:
  SyncPeriodMin: 30
```

```csharp
// requires ICuriosusDataContextFactory (Curiosus.DAL) to be registered
services.AddDbSyncDateTimeServices(configuration.DbDateTime);

public class InvoiceService
{
    private readonly IDateTimeService _dateTimeService;

    public InvoiceService(IDateTimeService dateTimeService)
    {
        _dateTimeService = dateTimeService;
    }

    public bool IsOverdue(Invoice invoice) => invoice.DueDateUtc < _dateTimeService.GetCurrentTimeUtc();
}
```

`DbSyncDateTimeService` reads the server time with `ICuriosusDataContext.GetImmediateServerTimeUtcAsync` and keeps
the offset from the local clock, so `GetCurrentTimeUtc()` doesn't query the database. The offset is measured by an
`IAppInitializer` on startup (run by the [Curiosus.Hosting](https://www.nuget.org/packages/Curiosus.Hosting)
bootstrappers or `host.InitAsync()`) and refreshed by a hosted service every `SyncPeriodMin` minutes.

Inherit from `DbSyncDateTimeService` and call `AddDbSyncDateTimeServices<TService>(options)` to customize it.
Register it before any other `IDateTimeService` (e.g. `AddLocalDateTimeService()`): registrations use `TryAdd`.

## See also

- [Curiosus.DAL](https://www.nuget.org/packages/Curiosus.DAL) — data context abstractions
- [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools) — `IDateTimeService` and `IAppInitializer`
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
