# Curiosus.SFTP

[![NuGet](https://img.shields.io/nuget/v/Curiosus.SFTP)](https://www.nuget.org/packages/Curiosus.SFTP) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.SFTP)](https://www.nuget.org/packages/Curiosus.SFTP) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.SFTP.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Abstractions for working with files over SFTP: `ISftpClient`, `ISftpClientFactory`, `SftpClientOptions` and
`SftpFileInfo`. Reference it from code that reads or writes SFTP files; the implementation lives in
[Curiosus.SFTP.SSH.NET](https://www.nuget.org/packages/Curiosus.SFTP.SSH.NET).

## Installation

```bash
dotnet add package Curiosus.SFTP
```

## Usage

```csharp
public class ReportUploader
{
    private readonly ISftpClientFactory _sftpClientFactory;

    public ReportUploader(ISftpClientFactory sftpClientFactory)
    {
        _sftpClientFactory = sftpClientFactory;
    }

    public async Task UploadAsync(byte[] report)
    {
        using var client = _sftpClientFactory.GetSftpClient();

        await client.UploadFileToServerAsync(report, "/reports", "2026/report.csv", overwrite: true);

        foreach (var file in client.ListDirectoryContents("/reports/2026"))
        {
            if (file.Type == SftpFileType.Regular)
            {
                Console.WriteLine($"{file.FullName}: {file.Length} bytes, {file.LastWriteTimeUtc:u}");
            }
        }
    }
}
```

`ISftpClient` operations take a base directory and a file name relative to it:
`UploadFileToServerAsync` (bytes or stream), `DownloadFileFromServer` / `DownloadFileFromServerAsync`
(returns `null` if the file does not exist), `GetDownloadStream`, `DeleteFileFromServer`, `IsExist`,
`ListDirectoryContents` and `CheckConnection`. Clients are `IDisposable`.

`SftpClientOptions`:

| Option | Description |
| --- | --- |
| `SshServer`, `SshPort`, `SshLogin` | Server, port and user name (server and login are required). |
| `SshPassword` | Password authentication. |
| `SshPrivateKeyPath`, `SshPrivateKeyPassphrase` | Private key authentication (used when no password is set). |
| `RetryCount`, `RetryTimeoutSec` | Retries on socket and SSH errors (default 3 retries, 5 seconds apart). |
| `CheckOnStart` | Check the connection on application start (default `true`). |

Either `SshPassword` or `SshPrivateKeyPath` must be specified.

## See also

- [Curiosus.SFTP.SSH.NET](https://www.nuget.org/packages/Curiosus.SFTP.SSH.NET) — implementation based on SSH.NET
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
