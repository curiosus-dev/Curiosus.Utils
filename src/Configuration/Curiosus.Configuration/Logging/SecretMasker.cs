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
            "password|pwd|passphrase|secret|token|credential|apikey|privatekey(?!path)|signature|salt|keys?$|connectionstring",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ValueSecretRegex = new(
            // URI user info
            @"[a-z][a-z0-9+.\-]*://[^/?#\s@]+@"
            // secret query parameters
            + @"|[?&](access_token|token|api[_-]?key|key|sig|signature|code|secret|client_secret|password|pwd|sas|sv)=[^&#\s]"
            // webhook URLs whose path is the secret
            + @"|hooks\.slack\.com/services/|discord(app)?\.com/api/webhooks/|webhook\.office\.com/|outlook\.office\.com/webhook/"
            + @"|api\.telegram\.org/bot\d+:",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        private static readonly Regex ConnectionStringNameRegex = new(
            "connectionstring",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

        /// <summary>
        /// Masks the value of a configuration key: a connection string keeps its non-secret parts, the value of a key
        /// with a secret segment or a value that carries a secret becomes <see cref="Mask"/>, other values are returned as is.
        /// </summary>
        public static string MaskValue(string path, string value)
        {
            var segments = path.Split(':');
            if (segments.Any(IsConnectionStringName)) return MaskConnectionString(value);
            if (segments.Any(IsSecretName) || HasSecretInValue(value)) return Mask;

            return value;
        }

        /// <summary>
        /// Returns <c>true</c> when the value itself carries a secret, whatever the name of its key is: a URI with user info
        /// (<c>amqp://user:pass@host</c>), a URL with a secret query parameter (<c>?token=</c>, <c>?sig=</c>,
        /// <c>?key=</c>, <c>?code=</c>, ...) or a webhook URL whose path is the secret (Slack, Discord, Teams, a Telegram
        /// bot token).
        /// </summary>
        /// <remarks>
        /// The unescaped value is checked too, so a percent-encoded URL nested in a query string is found as well.
        /// </remarks>
        public static bool HasSecretInValue(string value)
            => ValueSecretRegex.IsMatch(value) || ValueSecretRegex.IsMatch(Uri.UnescapeDataString(value));

        /// <summary>
        /// Returns <c>true</c> when the value is a string or a <see cref="Uri"/> that carries a secret,
        /// see <see cref="HasSecretInValue(string)"/>.
        /// </summary>
        public static bool HasSecretInValue(object? value) => value switch
        {
            string text => HasSecretInValue(text),
            Uri { IsAbsoluteUri: true } uri => !String.IsNullOrEmpty(uri.UserInfo) || HasSecretInValue(uri.OriginalString),
            Uri uri => HasSecretInValue(uri.OriginalString),
            _ => false,
        };

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
