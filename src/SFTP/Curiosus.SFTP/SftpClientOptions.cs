using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Curiosus.SFTP
{
    /// <summary>
    /// Options for <see cref="ISftpClient"/>.
    /// </summary>
    /// <remarks>
    /// Rules across properties (an authentication method, the host key) are checked by
    /// <see cref="SftpClientOptionsValidator"/>.
    /// </remarks>
    public class SftpClientOptions
    {
        /// <summary>Default configuration section of the options.</summary>
        public const string SectionName = "Sftp";

        /// <summary>
        /// SSH server.
        /// </summary>
        [Required]
        public string SshServer { get; set; } = null!;

        /// <summary>
        /// SSH port. Default: 22.
        /// </summary>
        [Range(1, 65535)]
        public int SshPort { get; set; } = 22;

        /// <summary>
        /// SSH login.
        /// </summary>
        [Required]
        public string SshLogin { get; set; } = null!;

        /// <summary>
        /// SSH password. Either it or <see cref="SshPrivateKeyPath"/> is required; the password wins when both are set.
        /// </summary>
        public string? SshPassword { get; set; }

        /// <summary>
        /// Absolute path to SSH key.
        /// </summary>
        public string? SshPrivateKeyPath { get; set; }

        /// <summary>
        /// Passphrase for SSH key (non required).
        /// </summary>
        public string? SshPrivateKeyPassphrase { get; set; }

        /// <summary>
        /// Count of uploading retry.
        /// </summary>
        [Range(0, int.MaxValue)]
        public int RetryCount { get; set; } = 3;

        /// <summary>
        /// Timeout between retries, sec.
        /// </summary>
        [Range(0, int.MaxValue)]
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
    }
}
