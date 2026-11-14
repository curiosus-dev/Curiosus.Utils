# Curiosus.DateTime.DbSync

[![NuGet](https://img.shields.io/nuget/v/Curiosus.DateTime.DbSync)](https://www.nuget.org/packages/Curiosus.DateTime.DbSync) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.DateTime.DbSync)](https://www.nuget.org/packages/Curiosus.DateTime.DbSync) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.DateTime.DbSync.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

`TimeProvider` whose "now" follows the database clock, so `now()` in SQL and "now" in the app are comparable on every
node: leases like `locked_until < now()`, `DEFAULT now()` columns, expirations written by one service and checked by
another.

## Installation

```bash
dotnet add package Curiosus.DateTime.DbSync
```

## When to use it

The value is **consistency with the database clock**, not accuracy. If the app never compares times it computes with
times the database computes, and all nodes and the database run NTP, `TimeProvider.System` is enough.

## Usage

Register the data source of the **primary** database (a replica has its own clock), then the provider:

```csharp
builder.Services.AddNpgsqlDataSource(connectionString);           // registers DbDataSource too
builder.Services.AddCuriosusDbSyncTimeProvider()
    .BindConfiguration("DbTimeSync");                             // optional, defaults below
```

```yaml
DbTimeSync:
  SyncPeriod: "00:30:00"        # resync period after the first success
  RetryPeriod: "00:00:10"       # retry period until the first success
  InitialSyncTimeout: "00:00:05" # how long the app start waits for the first sync
  MaxAcceptedRtt: "00:00:01"    # samples with a longer round trip are discarded
```

Inject `DbSyncTimeProvider` where "now" must match the database:

```csharp
public class LeaseService(DbSyncTimeProvider clock)
{
    public bool IsExpired(Lease lease) => lease.LockedUntil < clock.GetUtcNow();
}
```

`registerAsTimeProvider: true` replaces `TimeProvider` app-wide instead. It also shifts time checks of ASP.NET Core and
other libraries (cookie and token expiry), which is why it is off by default. A keyed data source is selected with
`dataSourceKey`.

### How it works

- `DbTimeSyncService` reads `clock_timestamp()` of PostgreSQL and stores the offset "database − local", compensating half
  of the round trip; `GetUtcNow()` returns the local time plus the offset and never queries the database.
- The app start makes the first attempt (up to `InitialSyncTimeout`) and **never fails because of it**: when the
  database is unreachable or not migrated yet, the provider returns the local time (`IsSynchronized` is `false`,
  `Offset` is zero) and the service retries every `RetryPeriod`. A failed resync keeps the previous offset.
- Timers, delays and `GetTimestamp()` use the local monotonic clock. "Now" can jump backwards on a resync: measure
  intervals with `GetTimestamp()`/`GetElapsedTime()`, not by subtracting two `GetUtcNow()` values.
- Another database: register your own `IDbClock` before `AddCuriosusDbSyncTimeProvider`.

### Tests

Use `FakeTimeProvider` (`Microsoft.Extensions.TimeProvider.Testing`) where the code takes a `TimeProvider`, or
`new DbSyncTimeProvider(fakeTimeProvider)` where it takes `DbSyncTimeProvider`.

Coming from 2.x: see the
[migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-datetime-dbsync/3.0).

## See also

- [Migration guides](https://curiosus-dev.github.io/Curiosus.Utils/migrations) — upgrading from `Curiosity.*` 1.x and between major versions
- [Curiosus.DateTime.DbSync 3.0 migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-datetime-dbsync/3.0)
- [TimeProvider](https://learn.microsoft.com/dotnet/api/system.timeprovider) — the .NET clock abstraction
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
