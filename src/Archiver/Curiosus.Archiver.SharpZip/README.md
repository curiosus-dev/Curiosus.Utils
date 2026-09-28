# Curiosus.Archiver.SharpZip

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Archiver.SharpZip)](https://www.nuget.org/packages/Curiosus.Archiver.SharpZip) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Archiver.SharpZip)](https://www.nuget.org/packages/Curiosus.Archiver.SharpZip) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Archiver.SharpZip.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

[SharpZipLib](https://github.com/icsharpcode/SharpZipLib)-based implementation of `IArchiver` from
[Curiosus.Archiver](https://www.nuget.org/packages/Curiosus.Archiver). Creates ZIP archives with maximum
compression, Unicode entry names and optional Zip64, writing them to the temp directory.

## Installation

```bash
dotnet add package Curiosus.Archiver.SharpZip
```

## Usage

`AddSharpZipArchiver` registers `SharpZipArchiver` as a singleton `IArchiver` and also registers
the temp file services from [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools)
(`TempFileOptions`, `ITempFileStreamFactory` and an app initializer that creates the temp directory).
`TempFileOptions` is validated on registration.

```yaml
TempFiles:
  TempPath: ./.tmp
  CleaningFrequencyHours: 24
  FileTtlHours: 24
```

```csharp
using Curiosus.Archiver;
using Curiosus.Archiver.SharpZip;
using Curiosus.Tools.TempFiles;

var tempFileOptions = builder.Configuration.GetSection("TempFiles").Get<TempFileOptions>() ?? new TempFileOptions();
builder.Services.AddSharpZipArchiver(tempFileOptions);

// later, in a service
public class LogsArchiver(IArchiver archiver)
{
    public Task<TempFileStream> ArchiveAsync(string logsDirectory, CancellationToken ct)
        => archiver.ZipDirAsync(logsDirectory, useZip64: true, cts: ct);
}
```

The section name (`TempFiles` above) is up to you. Pass `useZip64: false` if the archive must be opened
by old software (Windows XP, Android before 6.0).

## See also

- [Curiosus.Archiver](https://www.nuget.org/packages/Curiosus.Archiver) — `IArchiver` abstraction
- [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools) — temp files infrastructure
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
