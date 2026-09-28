# Curiosus.DAL.EF.Polly

[![NuGet](https://img.shields.io/nuget/v/Curiosus.DAL.EF.Polly)](https://www.nuget.org/packages/Curiosus.DAL.EF.Polly) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.DAL.EF.Polly)](https://www.nuget.org/packages/Curiosus.DAL.EF.Polly) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.DAL.EF.Polly.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

A prebuilt [Polly](https://github.com/App-vNext/Polly) (v7 API) policy builder that handles Entity Framework Core
update errors, for retrying optimistic-concurrency conflicts and failed updates.

## Installation

```bash
dotnet add package Curiosus.DAL.EF.Polly
```

## Usage

`PolicyExtensions.GetHandleDbConcurrencyExceptionsPolicy()` returns a `PolicyBuilder` that handles
`DbUpdateConcurrencyException`, `DbUpdateException` and `InvalidOperationException` whose inner exception is one of them.
Add the retry strategy you need:

```csharp
using Curiosus.DAL.EF.Polly;
using Microsoft.EntityFrameworkCore;
using Polly;

var retryPolicy = PolicyExtensions.GetHandleDbConcurrencyExceptionsPolicy()
    .WaitAndRetryAsync(3, attempt => TimeSpan.FromMilliseconds(100 * attempt));

await retryPolicy.ExecuteAsync(async ct =>
{
    // use a new context per attempt so that stale tracked entities are not reused
    await using var context = new ShopDataContext(options);
    var order = await context.Orders.SingleAsync(x => x.Id == orderId, ct);
    order.Status = OrderStatus.Paid;
    await context.SaveChangesAsync(ct);
}, cancellationToken);
```

## See also

- [Curiosus.DAL.EF](https://www.nuget.org/packages/Curiosus.DAL.EF) — EF Core base data contexts
- [Curiosus.DAL](https://www.nuget.org/packages/Curiosus.DAL) — data access abstractions
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
