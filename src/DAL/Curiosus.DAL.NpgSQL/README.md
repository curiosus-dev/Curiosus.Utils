# Curiosus.DAL.NpgSQL

[![NuGet](https://img.shields.io/nuget/v/Curiosus.DAL.NpgSQL)](https://www.nuget.org/packages/Curiosus.DAL.NpgSQL) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.DAL.NpgSQL)](https://www.nuget.org/packages/Curiosus.DAL.NpgSQL) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.DAL.NpgSQL.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Helpers for the [Npgsql](https://www.npgsql.org/) PostgreSQL driver: bulk insert via binary `COPY`,
null-aware `NpgsqlDataReader` accessors and `LIKE`/`ILIKE` pattern sanitizing.

## Installation

```bash
dotnet add package Curiosus.DAL.NpgSQL
```

## Usage

### Bulk insert

Implement `IBulkInsertable` and call `BulkInsertViaCopyAsync` on an open `NpgsqlConnection`:

```csharp
using Curiosus.DAL.NpgSQL.BulkInsert;
using Npgsql;
using NpgsqlTypes;

public record Measurement(long SensorId, double? Value, string? Comment) : IBulkInsertable
{
    public void WriteToStream(NpgsqlBinaryImporter writer)
    {
        writer.StartRow();
        writer.Write(SensorId, NpgsqlDbType.Bigint);
        writer.WriteNullable(Value, NpgsqlDbType.Double);
        writer.WriteNullable(Comment, NpgsqlDbType.Text);
    }
}

await using var connection = new NpgsqlConnection(connectionString);
await connection.OpenAsync(ct);
await connection.BulkInsertViaCopyAsync(
    "COPY measurements (sensor_id, value, comment) FROM STDIN (FORMAT BINARY)",
    measurements,
    ct);
```

`WriteToStream` must start the row itself (`StartRow`).

### Reading and LIKE patterns

```csharp
using Curiosus.DAL.NpgSQL;

int? count = reader.GetInt32("count");            // null for DBNull (or a default you pass)
string name = reader.GetString("name", "unknown");

// strips %, _, |, (, ), *, +, ?, {, }, [, ], ~, ! before building a LIKE pattern
var pattern = $"%{LikeFunctionHelpers.RemoveSpecialChars(userInput)}%";
```

## See also

- [Curiosus.DAL](https://www.nuget.org/packages/Curiosus.DAL) — data access abstractions
- [Curiosus.DAL.EF](https://www.nuget.org/packages/Curiosus.DAL.EF) — EF Core base data contexts
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
