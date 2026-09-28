# Curiosus.DAL

[![NuGet](https://img.shields.io/nuget/v/Curiosus.DAL)](https://www.nuget.org/packages/Curiosus.DAL) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.DAL)](https://www.nuget.org/packages/Curiosus.DAL) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.DAL.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Data access abstractions shared by the Curiosus DAL packages: read-only and read-write data contexts,
context factories, transactions with completion events, a raw SQL executor contract and `DbOptions`.
Depend on it from application code; pick an implementation such as
[Curiosus.DAL.EF](https://www.nuget.org/packages/Curiosus.DAL.EF) or [Curiosus.DAL.Dapper](https://www.nuget.org/packages/Curiosus.DAL.Dapper).

## Installation

```bash
dotnet add package Curiosus.DAL
```

## Usage

Main types:

- `ICuriosusReadOnlyDataContext` / `ICuriosusDataContext` — `Connection`, `CommandTimeoutSec`, `SaveChangesAsync`,
  `BeginTransactionAsync`, `OnTransactionCompleted` / `OnTransactionCompletedAsync` events;
- `ICuriosusDataContextFactory` / `ICuriosusReadOnlyDataContextFactory` (and typed `<TContext>` variants) — you implement them for your context;
- `ICuriosusDataContextTransaction` — commit/rollback, raises completion events after a successful commit;
- `ISqlExecutor` — raw SQL and stored procedure calls over a data context;
- `TransactionHelper.SafeRollbackTransactionsAsync` — rolls back several transactions, logging failures instead of throwing;
- `DbOptions` — `ConnectionString` (required), `ReadOnlyConnectionString`, `IsGlobalLoggingEnabled`, `IsSensitiveDataLoggingEnabled`.

```csharp
using Curiosus.DAL;

public class OrderService(ICuriosusDataContextFactory contextFactory, ILogger<OrderService> logger)
{
    public async Task CompleteAsync(CancellationToken ct)
    {
        using var context = contextFactory.CreateContext();
        await using var transaction = await context.BeginTransactionAsync(cancellationToken: ct);
        try
        {
            // ... change data ...
            await context.SaveChangesAsync(ct);
            transaction.OnTransactionCompletedAsync += _ => Task.CompletedTask; // e.g. publish events
            await transaction.CommitAsync(ct);
        }
        catch
        {
            await TransactionHelper.SafeRollbackTransactionsAsync(logger, ct, transaction);
            throw;
        }
    }
}
```

`DbOptions` implements `IValidatableOptions`, so it can be checked with `options.AssertValid()` from
[Curiosus.Configuration](https://www.nuget.org/packages/Curiosus.Configuration).

## See also

- [Curiosus.DAL.EF](https://www.nuget.org/packages/Curiosus.DAL.EF) — EF Core base contexts implementing these interfaces
- [Curiosus.DAL.Dapper](https://www.nuget.org/packages/Curiosus.DAL.Dapper) — `ISqlExecutor` implementation on Dapper
- [Curiosus.DAL.EF.Polly](https://www.nuget.org/packages/Curiosus.DAL.EF.Polly) — Polly policy for EF concurrency errors
- [Curiosus.DAL.NpgSQL](https://www.nuget.org/packages/Curiosus.DAL.NpgSQL) — Npgsql helpers and bulk insert
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
