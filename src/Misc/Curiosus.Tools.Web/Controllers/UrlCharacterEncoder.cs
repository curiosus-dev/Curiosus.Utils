using System;
using System.Text;

namespace Curiosus.Tools.Web.Controllers
{
    /// <summary>
    /// Percent-encodes characters that are not allowed in a URL, keeping the URL structure and existing escapes.
    /// </summary>
    internal static class UrlCharacterEncoder
    {
        private const string AllowedSymbols = "-._~:/?#[]@!$&'()*+,;=";

        public static string EncodeIllegalCharacters(string url)
        {
            if (url == null) throw new ArgumentNullException(nameof(url));

            StringBuilder? builder = null;
            for (var i = 0; i < url.Length; i++)
            {
                var c = url[i];
                if (IsAllowed(url, i))
                {
                    builder?.Append(c);
                    continue;
                }

                builder ??= new StringBuilder(url, 0, i, url.Length + 16);
                var length = Char.IsHighSurrogate(c) && i + 1 < url.Length && Char.IsLowSurrogate(url[i + 1]) ? 2 : 1;
                builder.Append(Uri.EscapeDataString(url.Substring(i, length)));
                i += length - 1;
            }

            return builder?.ToString() ?? url;
        }

        private static bool IsAllowed(string url, int index)
        {
            var c = url[index];
            if (c == '%')
                return index + 2 < url.Length && Uri.IsHexDigit(url[index + 1]) && Uri.IsHexDigit(url[index + 2]);

            return Char.IsAsciiLetterOrDigit(c) || AllowedSymbols.IndexOf(c) >= 0;
        }
    }
}
