using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Curiosus.SFTP.SSH.Net;
using Curiosus.Tools.AppInitializer;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.SFTP.UnitTests
{
    public class SftpClientOptionsTests
    {
        private const string Fingerprint = "nThbg6kXUpJWGl7E1IGOCspRomTxdCARLviKw6E5SY8";
        private const string Password = "s3cret-password";

        private static void Valid(SftpClientOptions o)
        {
            o.SshServer = "sftp.example.com";
            o.SshLogin = "user";
            o.SshPassword = Password;
            o.HostKeyFingerprintsSha256 = [Fingerprint];
        }

        private static ServiceCollection Services(Dictionary<string, string?>? configuration = null)
        {
            var services = new ServiceCollection();
            services.AddSingleton<IConfiguration>(new ConfigurationBuilder()
                .AddInMemoryCollection(configuration ?? [])
                .Build());
            return services;
        }

        private static ServiceProvider BuildProvider(Action<SftpClientOptions> configure)
        {
            var services = Services();
            services.AddSftpServices(configure: configure);
            return services.BuildServiceProvider();
        }

        private static readonly Dictionary<string, string?> SftpSection = new()
        {
            ["Sftp:SshServer"] = "sftp.example.com",
            ["Sftp:SshPort"] = "2222",
            ["Sftp:SshLogin"] = "user",
            ["Sftp:SshPrivateKeyPath"] = "/secrets/id_ed25519",
            ["Sftp:HostKeyFingerprintsSha256:0"] = "SHA256:" + Fingerprint,
            ["Sftp:HostKeyFingerprintsSha256:1"] = Fingerprint,
        };

        private static OptionsValidationException ValidationError(Action<SftpClientOptions> change)
        {
            using var provider = BuildProvider(o =>
            {
                Valid(o);
                change(o);
            });
            var act = () => provider.GetRequiredService<IOptions<SftpClientOptions>>().Value;
            return act.Should().Throw<OptionsValidationException>().Which;
        }

        [Fact]
        public void Defaults()
        {
            var options = new SftpClientOptions();

            options.SshPort.Should().Be(22);
            options.RetryCount.Should().Be(3);
            options.RetryTimeoutSec.Should().Be(5);
            options.CheckOnStart.Should().BeTrue();
            options.HostKeyFingerprintsSha256.Should().BeEmpty();
            options.AllowAnyHostKey.Should().BeFalse();
        }

        [Fact]
        public void ValidOptions_Pass()
        {
            using var provider = BuildProvider(Valid);

            provider.GetRequiredService<IOptions<SftpClientOptions>>().Value.SshServer.Should().Be("sftp.example.com");
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("  ")]
        public void SshServer_IsRequired(string? server)
        {
            ValidationError(o => o.SshServer = server!).Failures
                .Should().ContainSingle(f => f.Contains(nameof(SftpClientOptions.SshServer)));
        }

        [Fact]
        public void SshLogin_IsRequired()
        {
            ValidationError(o => o.SshLogin = "").Failures
                .Should().ContainSingle(f => f.Contains(nameof(SftpClientOptions.SshLogin)));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(65536)]
        public void SshPort_IsAPortNumber(int port)
        {
            ValidationError(o => o.SshPort = port).Failures
                .Should().ContainSingle(f => f.Contains(nameof(SftpClientOptions.SshPort)));
        }

        [Fact]
        public void Retries_CantBeNegative()
        {
            var failures = ValidationError(o =>
            {
                o.RetryCount = -1;
                o.RetryTimeoutSec = -1;
            }).Failures.ToList();

            failures.Should().Contain(f => f.Contains(nameof(SftpClientOptions.RetryCount)));
            failures.Should().Contain(f => f.Contains(nameof(SftpClientOptions.RetryTimeoutSec)));
        }

        [Fact]
        public void Retries_CanBeZero()
        {
            using var provider = BuildProvider(o =>
            {
                Valid(o);
                o.RetryCount = 0;
                o.RetryTimeoutSec = 0;
            });

            provider.GetRequiredService<IOptions<SftpClientOptions>>().Value.RetryCount.Should().Be(0);
        }

        [Fact]
        public void PasswordOrPrivateKey_IsRequired()
        {
            ValidationError(o => o.SshPassword = " ").Failures.Should().ContainSingle()
                .Which.Should().Be("Property SshPassword: specify SshPassword or SshPrivateKeyPath");
        }

        [Fact]
        public void PrivateKey_WithoutPassword_Passes()
        {
            var options = new SftpClientOptions();
            Valid(options);
            options.SshPassword = null;
            options.SshPrivateKeyPath = "/secrets/id_ed25519";

            new SftpClientOptionsValidator().Validate(null, options).Succeeded.Should().BeTrue();
        }

        [Fact]
        public void Errors_DontPrintThePassword()
        {
            ValidationError(o => o.SshServer = "").Message.Should().NotContain(Password);
        }

        [Fact]
        public void AddSftpServices_Twice_DoesNotDuplicateRegistrations()
        {
            var services = Services();
            services.AddSftpServices();
            services.AddSftpServices();

            services.Count(d => d.ServiceType == typeof(IValidateOptions<SftpClientOptions>)).Should().Be(2,
                "one data annotations validator and SftpClientOptionsValidator");
            services.Count(d => d.ServiceType == typeof(IAppInitializer)).Should().Be(1);
            services.Count(d => d.ServiceType == typeof(ISftpClientFactory)).Should().Be(1);
        }

        [Fact]
        public void AddSftpServices_Twice_ReportsEveryErrorOnce()
        {
            var services = Services();
            services.AddSftpServices(configure: Valid);
            services.AddSftpServices(configure: o => o.SshServer = "");
            using var provider = services.BuildServiceProvider();

            var act = () => provider.GetRequiredService<IOptions<SftpClientOptions>>().Value;

            act.Should().Throw<OptionsValidationException>().Which.Failures.Should().ContainSingle();
        }

        [Fact]
        public void Binds_TheDefaultSection()
        {
            SftpClientOptions.SectionName.Should().Be("Sftp");
            var services = Services(SftpSection);
            services.AddSftpServices();
            using var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<IOptions<SftpClientOptions>>().Value;

            options.SshServer.Should().Be("sftp.example.com");
            options.SshPort.Should().Be(2222);
            options.SshPrivateKeyPath.Should().Be("/secrets/id_ed25519");
            options.HostKeyFingerprintsSha256.Should().Equal("SHA256:" + Fingerprint, Fingerprint);
        }

        [Fact]
        public void Binds_ACustomSectionPath()
        {
            var services = Services(SftpSection.ToDictionary(p => "Storage:" + p.Key, p => p.Value));
            services.AddSftpServices("Storage:Sftp");
            using var provider = services.BuildServiceProvider();

            provider.GetRequiredService<IOptions<SftpClientOptions>>().Value.SshPort.Should().Be(2222);
        }

        [Fact]
        public void Configure_OverridesTheBoundValues()
        {
            var services = Services(SftpSection);
            services.AddSftpServices(configure: o => o.SshPort = 2022);
            using var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<IOptions<SftpClientOptions>>().Value;

            options.SshPort.Should().Be(2022);
            options.SshServer.Should().Be("sftp.example.com");
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void SectionPath_CantBeEmpty(string path)
        {
            var act = () => Services().AddSftpServices(path);

            act.Should().Throw<ArgumentException>();
        }

        [Fact]
        public async Task InvalidOptions_FailTheHostStart()
        {
            using var host = new HostBuilder()
                .ConfigureServices(services => services.AddSftpServices(configure: o =>
                {
                    Valid(o);
                    o.HostKeyFingerprintsSha256 = [];
                }))
                .Build();

            var act = () => host.StartAsync();

            (await act.Should().ThrowAsync<OptionsValidationException>())
                .Which.Failures.Should().ContainSingle(f => f.Contains(nameof(SftpClientOptions.HostKeyFingerprintsSha256)));
        }

        [Fact]
        public async Task ValidOptions_StartTheHost()
        {
            using var host = new HostBuilder()
                .ConfigureServices(services => services.AddSftpServices(configure: Valid))
                .Build();

            await host.StartAsync();
            await host.StopAsync();
        }
    }
}
