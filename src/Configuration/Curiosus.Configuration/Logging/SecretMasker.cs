using System;
using System.Data.Common;
using System.Linq;
using System.Text.RegularExpressions;

namespace Curiosus.Configuration
{
    /// <summary>
    /// Decides which option values are secrets and masks them for logs.
    /// </summary>
    internal static class SecretMasker
    {
        public const string Mask = "***";

        private static readonly char[] UriKeyChars = [':', '/', '@'];

        private static readonly Regex SecretNameRegex = new(
            "password|pwd|passphrase|secret|token|credential|apikey|privatekey(?!path)|key$|connectionstring",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ConnectionStringNameRegex = new(
            "connectionstring",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Returns <c>true</c> when a property or key with this name holds a secret.
        /// </summary>
        public static bool IsSecretName(string name) => SecretNameRegex.IsMatch(name);

        /// <summary>
        /// Returns <c>true</c> when a property or key with this name holds a connection string.
        /// Its value is printed with <see cref="MaskConnectionString"/> instead of being masked entirely.
        /// </summary>
        public static bool IsConnectionStringName(string name) => ConnectionStringNameRegex.IsMatch(name);

        /// <summary>
        /// Replaces the values of secret keys (password, account key, token, ...) of a connection string with
        /// <see cref="Mask"/> and keeps the other parts. A value that can't be parsed or looks like a URI
        /// (the password may be in its user info) is masked entirely.
        /// </summary>
        public static string MaskConnectionString(string connectionString)
        {
            var builder = new DbConnectionStringBuilder();
            try
            {
                builder.ConnectionString = connectionString;
            }
            catch (ArgumentException)
            {
                return Mask;
            }

            var keys = builder.Keys.Cast<string>().ToArray();
            if (keys.Any(key => key.IndexOfAny(UriKeyChars) >= 0)) return Mask;

            foreach (var key in keys.Where(IsSecretName))
            {
                builder[key] = Mask;
            }

            return builder.ConnectionString;
        }
    }
}
