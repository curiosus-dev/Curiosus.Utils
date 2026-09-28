# Curiosus.SFTP.SSH.NET

[![NuGet](https://img.shields.io/nuget/v/Curiosus.SFTP.SSH.NET)](https://www.nuget.org/packages/Curiosus.SFTP.SSH.NET) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.SFTP.SSH.NET)](https://www.nuget.org/packages/Curiosus.SFTP.SSH.NET) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.SFTP.SSH.NET.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Implementation of [Curiosus.SFTP](https://www.nuget.org/packages/Curiosus.SFTP) `ISftpClient` based on
[SSH.NET](https://github.com/sshnet/SSH.NET), with password or private key authentication and Polly retries.

## Installation

```bash
dotnet add package Curiosus.SFTP.SSH.NET
```

## Usage

Configure `SftpClientOptions` (either `SshPassword` or `SshPrivateKeyPath` is required):

```yaml
Sftp:
  SshServer: sftp.example.com
  SshPort: 22
  SshLogin: user
  SshPrivateKeyPath: /secrets/id_rsa
  RetryCount: 3
  RetryTimeoutSec: 5
  CheckOnStart: true
```

```csharp
using Curiosus.SFTP;
using Curiosus.SFTP.SSH.Net;
using Curiosus.Tools.TempFiles;

var sftpOptions = configuration.GetSection("Sftp").Get<SftpClientOptions>()!;

// SftpClient needs ITempFileStreamFactory from Curiosus.Tools
services.AddTempFileServices(new TempFileOptions());
services.AddSftpServices(sftpOptions);
```

```csharp
using var client = sftpClientFactory.GetSftpClient();

await client.UploadFileToServerAsync(stream, "/upload", "data/report.csv");

await using var file = await client.DownloadFileFromServerAsync("/upload", "data/report.csv");
```

`AddSftpServices` registers the options, `ISftpClientFactory` and an app initializer (`IAppInitializer`, executed
by Curiosus.Hosting on start) that checks the connection when `CheckOnStart` is `true`.

Notes:

- `SshPort` has no default value, set it explicitly (usually `22`).
- If both a password and a private key are configured, password authentication is used.
- Missing parent directories are created on upload. If a file with the same size already exists, the upload is
  skipped; if the size differs, it is replaced only when `overwrite` is `true`, otherwise an exception is thrown.
- The namespace is `Curiosus.SFTP.SSH.Net`.

## See also

- [Curiosus.SFTP](https://www.nuget.org/packages/Curiosus.SFTP) — SFTP abstractions and options
- [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools) — temp files and app initializers
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
