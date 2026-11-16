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
  SshPort: 22                       # the default
  SshLogin: user
  SshPrivateKeyPath: /secrets/id_rsa
  HostKeyFingerprintsSha256:        # required: ssh-keyscan -p <port> <host> | ssh-keygen -lf -
    - SHA256:nThbg6kXUpJWGl7E1IGOCspRomTxdCARLviKw6E5SY8
  RetryCount: 3
  RetryTimeoutSec: 5
  CheckOnStart: true
```

```csharp
using Curiosus.SFTP.SSH.Net;
using Curiosus.Tools.TempFiles;

// SftpClient needs ITempFileStreamFactory from Curiosus.Tools
services.AddTempFileServices(new TempFileOptions());
services.AddSftpServices().BindConfiguration("Sftp");
```

```csharp
using var client = sftpClientFactory.GetSftpClient();

await client.UploadFileToServerAsync(stream, "/upload", "data/report.csv");

await using var file = await client.DownloadFileFromServerAsync("/upload", "data/report.csv");
```

`AddSftpServices` registers the options, `ISftpClientFactory` and an app initializer (`IAppInitializer`, executed
by Curiosus.Hosting on start) that checks the connection when `CheckOnStart` is `true`. It returns the
`OptionsBuilder<SftpClientOptions>`: bind it with `.BindConfiguration("Sftp")`, or pass a `configure` delegate. The
options are validated when the host starts (data annotations and `SftpClientOptionsValidator`), so an invalid
configuration fails with `OptionsValidationException` before the app runs. Every client takes the current options, so a
reloaded configuration (a rotated password or host key) applies to the next `GetSftpClient()`.

Notes:

- `SshPort` defaults to `22`.
- The server host key must match one of `HostKeyFingerprintsSha256`, otherwise the connection fails with
  `SftpHostKeyVerificationException` (not retried). `AllowAnyHostKey: true` skips the check for local development.
- If both a password and a private key are configured, password authentication is used.
- Missing parent directories are created on upload. If a file with the same size already exists, the upload is
  skipped; if the size differs, it is replaced only when `overwrite` is `true`, otherwise an exception is thrown.
- The namespace is `Curiosus.SFTP.SSH.Net`.

## See also

- [Migration guides](https://curiosus-dev.github.io/Curiosus.Utils/migrations) — upgrading from `Curiosity.*` 1.x and between major versions
- [Curiosus.SFTP 3.0 migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-sftp/3.0)
- [Curiosus.SFTP](https://www.nuget.org/packages/Curiosus.SFTP) — SFTP abstractions and options
- [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools) — temp files and app initializers
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
