using System;
using System.Collections.Generic;
using Curiosus.Configuration;

namespace Curiosus.SFTP
{
    /// <summary>
    /// Options for <see cref="ISftpClient"/>.
    /// </summary>
    public class SftpClientOptions : ILoggableOptions, IValidatableOptions
    {
        private const string ShouldBeGreaterOrEqualZeroErrorMessage = "should be greater or equal to 0";

        /// <summary>
        /// SSH server.
        /// </summary>
        public string SshServer { get; set; } = null!;

        /// <summary>
        /// SSH port.
        /// </summary>
        public int SshPort { get; set; }

        /// <summary>
        /// SSH login.
        /// </summary>
        public string SshLogin { get; set; } = null!;

        /// <summary>
        /// SSH password.
        /// </summary>
        public string SshPassword { get; set; } = null!;

        /// <summary>
        /// Absolute path to SSH key.
        /// </summary>
        public string SshPrivateKeyPath { get; set; } = null!;

        /// <summary>
        /// Passphrase for SSH key (non required).
        /// </summary>
        public string SshPrivateKeyPassphrase { get; set; } = null!;

        /// <summary>
        /// Count of uploading retry.
        /// </summary>
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Timeout between retries, sec.
        /// </summary>
        public int RetryTimeoutSec { get; set; } = 5;

        /// <summary>
        /// Need to check the connection at the start of the app.
        /// </summary>
        public bool CheckOnStart { get; set; } = true;

        /// <summary>
        /// SHA-256 fingerprints of the server host key as printed by <c>ssh-keygen -l</c> (the <c>SHA256:</c> prefix is
        /// optional). Get them with <c>ssh-keyscan -p &lt;port&gt; &lt;host&gt; | ssh-keygen -lf -</c>; list several to
        /// rotate keys or accept several key types. Required unless <see cref="AllowAnyHostKey"/> is set.
        /// </summary>
        public IReadOnlyList<string> HostKeyFingerprintsSha256 { get; set; } = [];

        /// <summary>
        /// Accepts any server host key: local development and tests only, anyone on the network path could impersonate
        /// the server.
        /// </summary>
        public bool AllowAnyHostKey { get; set; }

        /// <inheritdoc cref="IValidatableOptions" />
        public virtual IReadOnlyCollection<ConfigurationValidationError> Validate(string? prefix = null)
        {
            var errors = new ConfigurationValidationErrorCollection(prefix);

            errors.AddErrorIf(String.IsNullOrWhiteSpace(SshServer), nameof(SshServer), "can not be empty");
            errors.AddErrorIf(String.IsNullOrWhiteSpace(SshLogin), nameof(SshLogin), "can not be empty");
            errors.AddErrorIf(SshPort < 0, nameof(SshPort), ShouldBeGreaterOrEqualZeroErrorMessage);
            errors.AddErrorIf(String.IsNullOrWhiteSpace(SshPassword) && String.IsNullOrWhiteSpace(SshPrivateKeyPath), "No auth method", $"specify {nameof(SshPassword)} or {nameof(SshPrivateKeyPath)}");
            errors.AddErrorIf(RetryCount < 0, nameof(RetryCount), ShouldBeGreaterOrEqualZeroErrorMessage);
            errors.AddErrorIf(RetryTimeoutSec < 0, nameof(RetryTimeoutSec), ShouldBeGreaterOrEqualZeroErrorMessage);

            var fingerprints = HostKeyFingerprintsSha256 ?? [];
            errors.AddErrorIf(
                fingerprints.Count == 0 && !AllowAnyHostKey,
                nameof(HostKeyFingerprintsSha256),
                "specify the SHA-256 fingerprints of the server host key (ssh-keyscan -p <port> <host> | ssh-keygen -lf -)");
            errors.AddErrorIf(
                fingerprints.Count > 0 && AllowAnyHostKey,
                nameof(AllowAnyHostKey),
                $"can't be set together with {nameof(HostKeyFingerprintsSha256)}");
            for (var i = 0; i < fingerprints.Count; i++)
            {
                errors.AddErrorIf(
                    !SftpHostKeyFingerprint.IsValid(fingerprints[i]),
                    $"{nameof(HostKeyFingerprintsSha256)}:{i}",
                    "is not a SHA-256 fingerprint (base64 of 32 bytes, as printed by ssh-keygen -l)");
            }

            return errors;
        }
    }
}