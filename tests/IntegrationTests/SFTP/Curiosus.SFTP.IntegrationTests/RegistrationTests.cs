using System;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Curiosus.SFTP.SSH.Net;
using Curiosus.Tools.AppInitializer;
using Curiosus.Tools.TempFiles;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Curiosus.SFTP.IntegrationTests
{
    /// <summary>
    /// <see cref="IoCExtensions.AddSftpServices"/> against a real OpenSSH server: the factory and the start-up check use
    /// the registered options.
    /// </summary>
    public sealed class RegistrationTests : IAsyncLifetime
    {
        private const int SshPort = 2222;

        private readonly IContainer _server = new ContainerBuilder("linuxserver/openssh-server:latest")
            .WithEnvironment("PASSWORD_ACCESS", "true")
            .WithEnvironment("USER_NAME", "test")
            .WithEnvironment("USER_PASSWORD", "test")
            .WithPortBinding(SshPort, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilMessageIsLogged(@"\[ls\.io-init\] done"))
            .Build();

        public Task InitializeAsync() => _server.StartAsync();

        public Task DisposeAsync() => _server.DisposeAsync().AsTask();

        private ServiceProvider BuildProvider(string[] fingerprints)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
            services.AddLogging();
            services.AddSingleton<ITempFileStreamFactory, UnusedTempFileStreamFactory>();
            services.AddSftpServices(configure: o =>
            {
                o.SshServer = _server.Hostname;
                o.SshPort = _server.GetMappedPublicPort(SshPort);
                o.SshLogin = "test";
                o.SshPassword = "test";
                o.HostKeyFingerprintsSha256 = fingerprints;
            });
            return services.BuildServiceProvider();
        }

        [Fact]
        public async Task StartupCheck_ConnectsWithTheRegisteredOptions()
        {
            await using var provider = BuildProvider(await GetServerFingerprintsAsync());

            var initializer = provider.GetServices<IAppInitializer>().Should().ContainSingle().Which;
            await initializer.InitializeAsync();

            using var client = provider.GetRequiredService<ISftpClientFactory>().GetSftpClient();
            client.IsExist("/config", "missing.txt").Should().BeFalse();
        }

        [Fact]
        public async Task StartupCheck_FailsOnAnUnknownHostKey()
        {
            await using var provider = BuildProvider([Convert.ToBase64String(new byte[32]).TrimEnd('=')]);

            var act = () => provider.GetServices<IAppInitializer>().Single().InitializeAsync();

            await act.Should().ThrowAsync<SftpHostKeyVerificationException>();
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

        // the start-up check and IsExist don't create temp files
        private sealed class UnusedTempFileStreamFactory : ITempFileStreamFactory
        {
            public TempFileStream CreateTempFileStream(string? fileName = null) => throw new NotSupportedException();
        }
    }
}
