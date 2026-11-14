using System;
using System.Collections.Generic;
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

        // Connection string keys whose values are printed as is; values of all other keys are masked.
        private static readonly HashSet<string> PublicConnectionStringKeys = new(StringComparer.OrdinalIgnoreCase)
        {
            "Host", "Server", "Data Source", "Address", "Addr", "Network Address", "Port", "Database", "Initial Catalog",
            "Username", "User Name", "User Id", "UserId", "Uid", "User", "Virtual Host", "VirtualHost",
            "Timeout", "Connect Timeout", "Connection Timeout", "Command Timeout", "Keepalive",
            "SSL Mode", "SslMode", "Encrypt", "Trust Server Certificate", "TrustServerCertificate",
            "Integrated Security", "Trusted_Connection", "Pooling", "Min Pool Size", "Minimum Pool Size",
            "Max Pool Size", "Maximum Pool Size", "Application Name", "Search Path", "Include Error Detail",
            "MultipleActiveResultSets", "Multiple Active Result Sets",
        };

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
        /// Keeps the values of well-known non-secret keys of a connection string (host, port, database, user, ...)
        /// and replaces the values of all other keys with <see cref="Mask"/>. A value that can't be parsed or
        /// contains a URI (the password may be in its user info) is masked entirely.
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

            foreach (var key in keys)
            {
                if (!PublicConnectionStringKeys.Contains(key))
                {
                    builder[key] = Mask;
                    continue;
                }

                var value = builder[key]?.ToString() ?? String.Empty;
                if (value.Contains("://", StringComparison.Ordinal) || value.Contains('@')) return Mask;
            }

            return builder.ConnectionString;
        }
    }
}
