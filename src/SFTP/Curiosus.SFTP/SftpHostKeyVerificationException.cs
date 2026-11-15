using System;
using System.Security.Authentication;

namespace Curiosus.SFTP
{
    /// <summary>
    /// The SFTP server presented a host key whose fingerprint is not in
    /// <see cref="SftpClientOptions.HostKeyFingerprintsSha256"/>: the connection may be intercepted. It is not retried.
    /// </summary>
    public class SftpHostKeyVerificationException : AuthenticationException
    {
        /// <inheritdoc cref="SftpHostKeyVerificationException"/>
        public SftpHostKeyVerificationException(string host, int port, string presentedFingerprintSha256, Exception? innerException)
            : base(
                $"SFTP host key of {host}:{port} is not trusted: the server presented SHA256:{presentedFingerprintSha256}, " +
                "which is not in HostKeyFingerprintsSha256. The connection may be intercepted; check the server before " +
                "adding the fingerprint.",
                innerException)
        {
            Host = host;
            Port = port;
            PresentedFingerprintSha256 = presentedFingerprintSha256;
        }

        /// <summary>
        /// SFTP server host.
        /// </summary>
        public string Host { get; }

        /// <summary>
        /// SFTP server port.
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// SHA-256 fingerprint of the presented host key (unpadded base64, without the <c>SHA256:</c> prefix).
        /// </summary>
        public string PresentedFingerprintSha256 { get; }
    }
}
