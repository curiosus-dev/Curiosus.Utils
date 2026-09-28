# Curiosus.FileDataReaderWriters

[![NuGet](https://img.shields.io/nuget/v/Curiosus.FileDataReaderWriters)](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.FileDataReaderWriters)](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.FileDataReaderWriters.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Common contracts for row-by-row reading and writing of tabular files: `IFileDataReader`, `IFileWriter`,
cell data and formatting settings, and Excel limits. Code against these interfaces and use
[Curiosus.FileDataReaderWriters.Npoi](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters.Npoi) (XLSX) or
[Curiosus.FileDataReaderWriters.SylvanCsv](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters.SylvanCsv) (CSV).

## Installation

```bash
dotnet add package Curiosus.FileDataReaderWriters
```

## Usage

- `IFileWriter` — `AddFormat(FormatSettings)` / `AddDefaultFormat()` return a format id; `AddHeaders`, `AppendLine`,
  `Append` + `EndLine` write rows of `CellData(value, formatId)`; `Flush`; `Dispose` finishes the file.
- `IFileDataReader` — `Read()` moves to the next non-empty row, `GetRow()` returns its cells (trimmed, empty → `null`),
  `CurrentRowIdx`, `MaxSupportedRowsCount`.
- `FormatSettings` — `FontStyle` (`Bold`, `Italic`), `TextAlignment`, `WrapText`, `FontSize` (default 10),
  `DataFormat` (Excel format string, default `@`).
- `ExcelConstants` — row, column and cell length limits of XLS/XLSX.

```csharp
using Curiosus.FileDataReaderWriters.Readers;
using Curiosus.FileDataReaderWriters.Style;
using Curiosus.FileDataReaderWriters.Writers;

public static class ReportFiles
{
    public static void WriteReport(IFileWriter writer, IEnumerable<(string Name, decimal Amount)> rows)
    {
        var header = writer.AddFormat(new FormatSettings { FontStyle = FontStyle.Bold, TextAlignment = TextAlignment.Center });
        writer.AddHeaders(new[] { new CellData("Name", header), new CellData("Amount", header) });

        foreach (var (name, amount) in rows)
        {
            writer.AppendLine(new[] { new CellData(name), new CellData(amount) });
        }
    }

    public static IEnumerable<IReadOnlyList<string?>> ReadAll(IFileDataReader reader)
    {
        while (reader.Read())
        {
            // implementations may reuse the row buffer, so copy it if you keep it
            yield return reader.GetRow()!.ToArray();
        }
    }
}
```

Formats are ignored by writers that do not support them (CSV).

## See also

- [Curiosus.FileDataReaderWriters.Npoi](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters.Npoi) — XLSX writers (NPOI)
- [Curiosus.FileDataReaderWriters.SylvanCsv](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters.SylvanCsv) — CSV reader and writer
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
