# Curiosus.FileDataReaderWriters.SylvanCsv

[![NuGet](https://img.shields.io/nuget/v/Curiosus.FileDataReaderWriters.SylvanCsv)](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters.SylvanCsv) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.FileDataReaderWriters.SylvanCsv)](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters.SylvanCsv) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.FileDataReaderWriters.SylvanCsv.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Buffered, low-memory CSV implementations of the
[Curiosus.FileDataReaderWriters](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters) contracts:
`CsvFileDataReader` (an `IFileDataReader` on [Sylvan.Data.Csv](https://github.com/MarkPflug/Sylvan)) and
`CsvFileWriter` (an `IFileWriter` on [CsvHelper](https://joshclose.github.io/CsvHelper/)).

## Installation

```bash
dotnet add package Curiosus.FileDataReaderWriters.SylvanCsv
```

## Usage

### Reading

`CsvFileDataReader.CreateReader(stream, columnsCount = null, startRowIdx = 1, fieldDelimiter = ';', encoding = UTF-8,
disposeFileStream = false)`. Rows are numbered from 1, so `startRowIdx: 2` skips a header row. Empty rows are skipped,
and reading stops after more than 10 empty rows in a row. Cells are trimmed; empty cells are `null`.

```csharp
using Curiosus.FileDataReaderWriters.SylvanCsv;

await using var stream = File.OpenRead("import.csv");
using var reader = CsvFileDataReader.CreateReader(stream, startRowIdx: 2, fieldDelimiter: ',');

while (reader.Read())
{
    var row = reader.GetRow()!; // the list is reused by the next Read()
    Console.WriteLine($"Row {reader.CurrentRowIdx}: {row[0]}");
}
```

### Writing

`CsvFileWriter` writes UTF-8, `;`-delimited RFC 4180 CSV using the current culture. Formats are not supported
(`AddFormat` returns `0` and is ignored).

```csharp
using Curiosus.FileDataReaderWriters.SylvanCsv;
using Curiosus.FileDataReaderWriters.Writers;

using (var writer = new CsvFileWriter("export.csv"))
{
    writer.AddHeaders(new[] { new CellData("Name"), new CellData("Amount") });
    writer.AppendLine(new[] { new CellData("Coffee"), new CellData(3.5m) });
}
```

## See also

- [Curiosus.FileDataReaderWriters](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters) — reader/writer contracts
- [Curiosus.FileDataReaderWriters.Npoi](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters.Npoi) — XLSX writers
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
