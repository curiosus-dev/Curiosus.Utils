using System;
using Microsoft.Extensions.Options;

namespace Curiosus.SFTP
{
    /// <summary>
    /// Validates the rules of <see cref="SftpClientOptions"/> across properties: a password or a private key is set, and
    /// the server host key is pinned with well-formed <see cref="SftpClientOptions.HostKeyFingerprintsSha256"/> unless
    /// <see cref="SftpClientOptions.AllowAnyHostKey"/> is set (not both).
    /// </summary>
    /// <remarks>
    /// Rules of single properties are data annotations on <see cref="SftpClientOptions"/>.
    /// </remarks>
    public class SftpClientOptionsValidator : IValidateOptions<SftpClientOptions>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, SftpClientOptions options)
        {
            ArgumentNullException.ThrowIfNull(options);

            var builder = new ValidateOptionsResultBuilder();

            if (String.IsNullOrWhiteSpace(options.SshPassword) && String.IsNullOrWhiteSpace(options.SshPrivateKeyPath))
                builder.AddError(
                    $"specify {nameof(options.SshPassword)} or {nameof(options.SshPrivateKeyPath)}",
                    nameof(options.SshPassword));

            var fingerprints = options.HostKeyFingerprintsSha256 ?? [];
            if (fingerprints.Count == 0 && !options.AllowAnyHostKey)
                builder.AddError(
                    "specify the SHA-256 fingerprints of the server host key (ssh-keyscan -p <port> <host> | ssh-keygen -lf -)",
                    nameof(options.HostKeyFingerprintsSha256));
            if (fingerprints.Count > 0 && options.AllowAnyHostKey)
                builder.AddError(
                    $"can't be set together with {nameof(options.HostKeyFingerprintsSha256)}",
                    nameof(options.AllowAnyHostKey));
            for (var i = 0; i < fingerprints.Count; i++)
            {
                if (!SftpHostKeyFingerprint.IsValid(fingerprints[i]))
                    builder.AddError(
                        "is not a SHA-256 fingerprint (base64 of 32 bytes, as printed by ssh-keygen -l)",
                        $"{nameof(options.HostKeyFingerprintsSha256)}[{i}]");
            }

            return builder.Build();
        }
    }
}
