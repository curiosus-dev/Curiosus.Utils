using System;
using System.Linq;
using Curiosus.SFTP.SSH.Net;
using FluentAssertions;
using Xunit;

namespace Curiosus.SFTP.UnitTests
{
    public class HostKeyVerificationTests
    {
        // base64 of 32 bytes, as ssh-keygen -l prints it (43 characters, no padding)
        private const string Fingerprint = "nThbg6kXUpJWGl7E1IGOCspRomTxdCARLviKw6E5SY8";
        private const string Other = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";

        private static SftpClientOptions Options(Action<SftpClientOptions>? change = null)
        {
            var options = new SftpClientOptions
            {
                SshServer = "sftp.example.com",
                SshPort = 22,
                SshLogin = "user",
                SshPassword = "password",
                HostKeyFingerprintsSha256 = [Fingerprint],
            };
            change?.Invoke(options);
            return options;
        }

        [Fact]
        public void Validation_RequiresFingerprintsOrTheOptOut()
        {
            Options(o => o.HostKeyFingerprintsSha256 = []).Validate()
                .Should().ContainSingle(e => e.FieldName == nameof(SftpClientOptions.HostKeyFingerprintsSha256));
            Options(o => { o.HostKeyFingerprintsSha256 = []; o.AllowAnyHostKey = true; }).Validate().Should().BeEmpty();
            Options().Validate().Should().BeEmpty();
        }

        [Fact]
        public void Validation_RejectsFingerprintsTogetherWithTheOptOut()
        {
            Options(o => o.AllowAnyHostKey = true).Validate()
                .Should().ContainSingle(e => e.FieldName == nameof(SftpClientOptions.AllowAnyHostKey));
        }

        [Theory]
        [InlineData("SHA256:" + Fingerprint)]
        [InlineData(Fingerprint + "=")]
        [InlineData(" SHA256:" + Fingerprint + "= ")]
        public void Validation_AcceptsTheForms_OfSshKeygen(string fingerprint)
        {
            Options(o => o.HostKeyFingerprintsSha256 = [fingerprint]).Validate().Should().BeEmpty();
        }

        [Theory]
        [InlineData("")]
        [InlineData("not base64!")]
        [InlineData("MD5:16:27:ac:a5:76:28:2d:36:63:1b:56:4d:eb:df:a6:48")]
        [InlineData("AAAA")]   // 3 bytes
        public void Validation_RejectsMalformedFingerprints(string fingerprint)
        {
            Options(o => o.HostKeyFingerprintsSha256 = [Fingerprint, fingerprint]).Validate()
                .Should().ContainSingle(e => e.FieldName == "HostKeyFingerprintsSha256:1");
        }

        [Theory]
        [InlineData(Fingerprint, true)]
        [InlineData(Other, false)]
        [InlineData("SHA256:" + Fingerprint, true)]
        [InlineData(Fingerprint + "=", true)]
        public void IsTrusted_ComparesNormalizedFingerprints(string presented, bool expected)
        {
            SftpClient.IsTrusted(Options(), presented).Should().Be(expected);
        }

        [Fact]
        public void IsTrusted_AcceptsAnyOfSeveralFingerprints()
        {
            var options = Options(o => o.HostKeyFingerprintsSha256 = ["SHA256:" + Other + "=", Fingerprint]);

            SftpClient.IsTrusted(options, Other).Should().BeTrue();
            SftpClient.IsTrusted(options, Fingerprint).Should().BeTrue();
            SftpClient.IsTrusted(options, Fingerprint.ToLowerInvariant()).Should().BeFalse("base64 is case-sensitive");
        }

        [Fact]
        public void IsTrusted_OptOutTrustsAnyKey()
        {
            SftpClient.IsTrusted(Options(o => { o.HostKeyFingerprintsSha256 = []; o.AllowAnyHostKey = true; }), Other)
                .Should().BeTrue();
        }

        [Fact]
        public void Exception_IsNotAnSshException_SoItIsNotRetried()
        {
            var exception = new SftpHostKeyVerificationException("h", 22, Other, null);

            exception.Should().BeAssignableTo<System.Security.Authentication.AuthenticationException>();
            exception.Should().NotBeAssignableTo<Renci.SshNet.Common.SshException>();
            exception.Message.Should().Contain("SHA256:" + Other);
        }
    }
}
