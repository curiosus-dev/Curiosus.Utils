# Curiosus.Archiver

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Archiver)](https://www.nuget.org/packages/Curiosus.Archiver) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Archiver)](https://www.nuget.org/packages/Curiosus.Archiver) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Archiver.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Abstractions for creating and extracting ZIP archives: the `IArchiver` interface and the `FileNames` model.
Reference it from code that should not depend on a concrete ZIP library; the implementation is provided by
[Curiosus.Archiver.SharpZip](https://www.nuget.org/packages/Curiosus.Archiver.SharpZip).

## Installation

```bash
dotnet add package Curiosus.Archiver
```

## Usage

`IArchiver` can:

- `ZipDirAsync` — zip a directory recursively into a temp file and return it as a `TempFileStream`;
- `ZipFilesToStreamAsync` — zip a list of files into a `TempFileStream` (the stream is positioned at the start);
- `ZipFilesToFileAsync` — zip a list of files into the temp directory and return the archive path;
- `UnzipFile` — extract an archive into a directory (a new temp directory by default) and return its path.

`FileNames` pairs the path of a file on disk with the name it gets inside the archive
(by default the file name from the path). Duplicate entry names get a numeric suffix.

```csharp
using Curiosus.Archiver;

public class ReportExporter(IArchiver archiver)
{
    public async Task<Stream> ExportAsync(string reportPath, string logPath, CancellationToken ct)
    {
        var files = new[]
        {
            new FileNames(reportPath, "report.xlsx"),
            new FileNames(logPath),
        };

        // TempFileStream deletes the archive when it is disposed
        return await archiver.ZipFilesToStreamAsync(files, zipFileName: "export.zip", cts: ct);
    }
}
```

The `IList<string>` overloads of `ZipFilesToStreamAsync` and `ZipFilesToFileAsync` are obsolete; use the `FileNames` overloads.

## See also

- [Curiosus.Archiver.SharpZip](https://www.nuget.org/packages/Curiosus.Archiver.SharpZip) — `IArchiver` implementation based on SharpZipLib
- [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools) — temp files (`TempFileStream`, `TempFileOptions`)
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
