using System;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Curiosus.SFTP.SSH.Net;
using Curiosus.Tools.TempFiles;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Xunit;

namespace Curiosus.SFTP.IntegrationTests
{
    /// <summary>
    /// A real OpenSSH server (multi-arch image, runs on arm64 too) with password authentication.
    /// </summary>
    public sealed class HostKeyVerificationTests : IAsyncLifetime
    {
        private const int SshPort = 2222;

        private readonly IContainer _server = new ContainerBuilder()
            .WithImage("linuxserver/openssh-server:latest")
            .WithEnvironment("PASSWORD_ACCESS", "true")
            .WithEnvironment("USER_NAME", "test")
            .WithEnvironment("USER_PASSWORD", "test")
            .WithPortBinding(SshPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(@"\[ls\.io-init\] done"))
            .Build();

        private readonly FakeLogCollector _logs = new();

        public Task InitializeAsync() => _server.StartAsync();

        public Task DisposeAsync() => _server.DisposeAsync().AsTask();

        private SftpClient CreateClient(params string[] fingerprints) =>
            new(new SftpClientOptions
            {
                SshServer = _server.Hostname,
                SshPort = _server.GetMappedPublicPort(SshPort),
                SshLogin = "test",
                SshPassword = "test",
                HostKeyFingerprintsSha256 = fingerprints,
                RetryCount = 3,
                RetryTimeoutSec = 5,
            }, new TempFileStreamFactory(Microsoft.Extensions.Options.Options.Create(new TempFileOptions())), LoggerFactory.Create(b => b.AddProvider(new FakeLoggerProvider(_logs))));

        [Fact]
        public async Task CorrectFingerprint_Connects()
        {
            var fingerprints = await GetServerFingerprintsAsync();

            var act = () => CreateClient(fingerprints).CheckConnection();

            act.Should().NotThrow();
        }

        [Fact]
        public async Task WrongFingerprint_ThrowsAfterOneAttempt_WithoutRetryDelays()
        {
            var presented = await GetServerFingerprintsAsync();
            var wrong = Convert.ToBase64String(new byte[32]).TrimEnd('=');
            var client = CreateClient(wrong);
            var stopwatch = Stopwatch.StartNew();

            // an operation under the retry policy: 3 retries × 5 s if the rejection were retried
            var act = () => client.DownloadFileFromServer("/config", "missing.txt");

            act.Should().Throw<SftpHostKeyVerificationException>()
                .Which.PresentedFingerprintSha256.Should().BeOneOf(presented);
            stopwatch.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(5));
            _logs.GetSnapshot().Count(r => r.Level == LogLevel.Error && r.Message.Contains("rejected")).Should().Be(1);
        }

        // SHA-256 of the public key blob, unpadded base64: what ssh-keygen -l prints after "SHA256:"
        private async Task<string[]> GetServerFingerprintsAsync()
        {
            var result = await _server.ExecAsync(["sh", "-c", "cat /config/ssh_host_keys/*.pub"]);
            return result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select(line => line.Split(' ')[1])
                .Select(blob => Convert.ToBase64String(SHA256.HashData(Convert.FromBase64String(blob))).TrimEnd('='))
                .ToArray();
        }
    }
}
