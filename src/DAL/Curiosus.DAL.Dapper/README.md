# Curiosus.DAL.Dapper

[![NuGet](https://img.shields.io/nuget/v/Curiosus.DAL.Dapper)](https://www.nuget.org/packages/Curiosus.DAL.Dapper) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.DAL.Dapper)](https://www.nuget.org/packages/Curiosus.DAL.Dapper) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.DAL.Dapper.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

[Dapper](https://github.com/DapperLib/Dapper)-based implementation of `ISqlExecutor` from
[Curiosus.DAL](https://www.nuget.org/packages/Curiosus.DAL): parameterized raw SQL queries and stored procedure calls
over the connection of an `ICuriosusDataContext` / `ICuriosusReadOnlyDataContext`.

## Installation

```bash
dotnet add package Curiosus.DAL.Dapper
```

## Usage

```csharp
using Curiosus.DAL;
using Curiosus.DAL.Dapper;

builder.Services.AddCuriosusDapper(); // ISqlExecutor -> DapperSqlExecutor (singleton)

// map columns to properties by name (case-insensitive) or by [Column("...")]
DapperTypeMapper.RegisterTypes(typeof(OrderRow));

public class OrderQueries(ISqlExecutor sql, ICuriosusReadOnlyDataContextFactory contextFactory)
{
    public async Task<IEnumerable<OrderRow>> GetByCustomerAsync(long customerId, CancellationToken ct)
    {
        using var context = contextFactory.CreateReadOnlyContext();
        return await sql.QueryManyAsync<OrderRow>(
            context,
            "select id, customer_id, created_at from orders where customer_id = @customerId",
            new Dictionary<string, object> { ["customerId"] = customerId },
            cancellationToken: ct);
    }
}

public class OrderRow
{
    public long Id { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("customer_id")]
    public long CustomerId { get; set; }

    [System.ComponentModel.DataAnnotations.Schema.Column("created_at")]
    public DateTime CreatedAt { get; set; }
}
```

Parameters are passed as `IDictionary<string, object>`; `ignoreNulls` controls whether `null` values are sent.
Always pass values as parameters — never concatenate them into `sqlTemplate`.

## See also

- [Curiosus.DAL](https://www.nuget.org/packages/Curiosus.DAL) — data access abstractions
- [Curiosus.DAL.EF](https://www.nuget.org/packages/Curiosus.DAL.EF) — EF Core contexts that can be used with `ISqlExecutor`
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
