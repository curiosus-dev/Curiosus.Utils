# Curiosus.DAL.EF

[![NuGet](https://img.shields.io/nuget/v/Curiosus.DAL.EF)](https://www.nuget.org/packages/Curiosus.DAL.EF) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.DAL.EF)](https://www.nuget.org/packages/Curiosus.DAL.EF) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.DAL.EF.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Entity Framework Core base classes for the [Curiosus.DAL](https://www.nuget.org/packages/Curiosus.DAL) abstractions:
`CuriosusReadOnlyDataContext<T>` and `CuriosusDataContext<T>` (a `DbContext` that implements `ICuriosusDataContext`),
a transaction wrapper with completion events and a pagination helper.

## Installation

```bash
dotnet add package Curiosus.DAL.EF
```

## Usage

Derive your context from `CuriosusDataContext<T>`:

```csharp
using Curiosus.DAL.EF;
using Microsoft.EntityFrameworkCore;

public class ShopDataContext : CuriosusDataContext<ShopDataContext>
{
    public ShopDataContext(DbContextOptions<ShopDataContext> options) : base(options)
    {
    }

    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        SpecifyDateKindForAllModels(modelBuilder); // DateTime values are read as DateTimeKind.Utc
    }
}
```

`OnTransactionCompleted` / `OnTransactionCompletedAsync` fire after a successful commit of a transaction started with
`BeginTransaction(Async)`, or — without an explicit transaction — after the first successful `SaveChanges(Async)`
(subscribers are reset after that call):

```csharp
await using var context = new ShopDataContext(options);
context.OnTransactionCompletedAsync += ct => notifier.OrderCreatedAsync(ct);
context.Orders.Add(order);
await context.SaveChangesAsync(ct);

var page = await context.Orders.AsNoTracking()
    .OrderBy(x => x.Id)
    .ToPageAsync(pageIndex: 0, pageSize: 20, totalCount: await context.Orders.CountAsync(ct), ct);
```

`GetImmediateServerTimeUtcAsync` runs `select timezone('UTC'::text, now());`, i.e. PostgreSQL syntax;
override it for other databases. The context factory (`ICuriosusDataContextFactory`) is not included — implement it
for your context.

## See also

- [Curiosus.DAL](https://www.nuget.org/packages/Curiosus.DAL) — data access abstractions
- [Curiosus.DAL.EF.Polly](https://www.nuget.org/packages/Curiosus.DAL.EF.Polly) — retry policy for EF concurrency errors
- [Curiosus.DAL.Dapper](https://www.nuget.org/packages/Curiosus.DAL.Dapper) — raw SQL over the same contexts
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
