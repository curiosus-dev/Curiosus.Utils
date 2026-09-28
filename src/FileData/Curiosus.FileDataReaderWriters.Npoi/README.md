# Curiosus.FileDataReaderWriters.Npoi

[![NuGet](https://img.shields.io/nuget/v/Curiosus.FileDataReaderWriters.Npoi)](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters.Npoi) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.FileDataReaderWriters.Npoi)](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters.Npoi) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.FileDataReaderWriters.Npoi.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

[NPOI](https://github.com/nissl-lab/npoi)-based `IFileWriter` implementations that write XLSX files with the
streaming workbook (`SXSSFWorkbook`), keeping only a window of rows in memory. Use it to export large tables to Excel.

## Installation

```bash
dotnet add package Curiosus.FileDataReaderWriters.Npoi
```

## Usage

- `NpoiXlsFileWriter(outputFilePath, logger, rowAccessWindowSize = 100)` — one XLSX file, written on `Dispose()`.
- `NpoiXlsMultiFileWriter(savePath, fileName, logger, rowAccessWindowSize = 100)` — splits data into
  `report.xlsx`, `report_part_2.xlsx`, ... when a sheet reaches the XLSX row limit (1 048 576) and repeats the headers
  in every part. Call `Flush()` to write the last part before disposing.

Numbers, `bool`, `string` and `DateTime` values are written as typed cells (dates use `yyyy-MM-dd HH:mm:ss`),
other values via `ToString()`; `null` and whitespace strings leave the cell empty.

```csharp
using Curiosus.FileDataReaderWriters.Npoi;
using Curiosus.FileDataReaderWriters.Style;
using Curiosus.FileDataReaderWriters.Writers;

using (var writer = new NpoiXlsFileWriter("orders.xlsx", logger))
{
    var bold = writer.AddFormat(new FormatSettings { FontStyle = FontStyle.Bold, FontSize = 12 });
    var money = writer.AddFormat(new FormatSettings { DataFormat = "#,##0.00" });

    writer.AddHeaders(new[] { new CellData("Order", bold), new CellData("Created", bold), new CellData("Total", bold) });
    foreach (var order in orders)
    {
        writer.AppendLine(new[] { new CellData(order.Number), new CellData(order.CreatedAt), new CellData(order.Total, money) });
    }
} // the file is saved here

var multiWriter = new NpoiXlsMultiFileWriter(outputDirectory, "orders.xlsx", logger);
// ... AddHeaders / AppendLine ...
multiWriter.Flush();
multiWriter.Dispose();
```

## See also

- [Curiosus.FileDataReaderWriters](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters) — `IFileWriter` and formatting contracts
- [Curiosus.FileDataReaderWriters.SylvanCsv](https://www.nuget.org/packages/Curiosus.FileDataReaderWriters.SylvanCsv) — CSV reader and writer
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
