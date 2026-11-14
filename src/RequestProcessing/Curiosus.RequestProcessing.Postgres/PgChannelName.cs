using System;
using System.Text;

namespace Curiosus.RequestProcessing.Postgres
{
    /// <summary>
    /// Validates and quotes names of PostgreSQL <c>LISTEN</c>/<c>NOTIFY</c> channels.
    /// </summary>
    internal static class PgChannelName
    {
        /// <summary>
        /// PostgreSQL truncates identifiers longer than <c>NAMEDATALEN - 1</c> bytes.
        /// </summary>
        private const int MaxBytes = 63;

        /// <summary>
        /// Returns the reason why the name can't be a channel name, or <c>null</c> when it can.
        /// </summary>
        public static string? Validate(string? name)
        {
            if (String.IsNullOrWhiteSpace(name)) return "Channel name can't be empty or whitespace.";
            if (name.Contains('\0')) return "Channel name can't contain the NUL character.";
            if (Encoding.UTF8.GetByteCount(name) > MaxBytes) return $"Channel name can't be longer than {MaxBytes} bytes in UTF-8.";

            return null;
        }

        /// <summary>
        /// Throws <see cref="ArgumentException"/> when the name can't be a channel name.
        /// </summary>
        public static void AssertValid(string? name, string paramName)
        {
            var error = Validate(name);
            if (error != null) throw new ArgumentException(error, paramName);
        }

        /// <summary>
        /// Quotes the name as an SQL identifier, so it is used exactly as written: no case folding, no injection.
        /// </summary>
        public static string Quote(string name) => "\"" + name.Replace("\"", "\"\"") + "\"";
    }
}
