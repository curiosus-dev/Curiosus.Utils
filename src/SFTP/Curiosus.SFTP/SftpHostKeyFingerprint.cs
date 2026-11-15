using System;

namespace Curiosus.SFTP
{
    /// <summary>
    /// SHA-256 fingerprints of SSH host keys, as printed by <c>ssh-keygen -l</c>.
    /// </summary>
    public static class SftpHostKeyFingerprint
    {
        private const string Prefix = "SHA256:";

        /// <summary>
        /// Removes the optional <c>SHA256:</c> prefix, the <c>=</c> padding and the surrounding whitespace, giving the
        /// unpadded base64 form that SSH.NET reports.
        /// </summary>
        public static string Normalize(string fingerprint)
        {
            if (fingerprint == null) throw new ArgumentNullException(nameof(fingerprint));

            var value = fingerprint.Trim();
            if (value.StartsWith(Prefix, StringComparison.Ordinal))
                value = value.Substring(Prefix.Length);

            return value.TrimEnd('=');
        }

        /// <summary>
        /// Whether the fingerprint is the base64 of a 32-byte SHA-256 hash, with or without the prefix and the padding.
        /// </summary>
        public static bool IsValid(string? fingerprint)
        {
            if (String.IsNullOrWhiteSpace(fingerprint)) return false;

            var value = Normalize(fingerprint);
            var padded = value.PadRight(value.Length + (4 - value.Length % 4) % 4, '=');
            var bytes = new byte[48];
            return Convert.TryFromBase64String(padded, bytes, out var written) && written == 32;
        }
    }
}
