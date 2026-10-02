using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Curiosus.Tools
{
    /// <summary>
    /// Extensions for <see cref="HttpContent"/>.
    /// </summary>
    public static class HttpContentExtensions
    {
        /// <summary>
        /// Reads the content as a string like <see cref="HttpContent.ReadAsStringAsync()"/>, but does not throw
        /// on a charset .NET has no built-in encoding for.
        /// </summary>
        /// <remarks>
        /// Code page charsets such as <c>windows-1251</c> are decoded without registering
        /// <see cref="CodePagesEncodingProvider"/> globally, unknown charsets fall back to UTF-8.
        /// </remarks>
        public static async Task<string> ReadAsStringOrUtf8Async(
            this HttpContent content,
            CancellationToken cancellationToken = default)
        {
            if (content == null) throw new ArgumentNullException(nameof(content));

            var bytes = await content.ReadAsByteArrayAsync(cancellationToken);
            var preamble = Encoding.UTF8.Preamble;
            if (bytes.AsSpan().StartsWith(preamble))
                return Encoding.UTF8.GetString(bytes, preamble.Length, bytes.Length - preamble.Length);

            return GetEncoding(content.Headers.ContentType?.CharSet).GetString(bytes);
        }

        private static Encoding GetEncoding(string? charset)
        {
            if (String.IsNullOrWhiteSpace(charset))
                return Encoding.UTF8;

            var name = charset.Trim('"', '\'', ' ');
            try
            {
                return Encoding.GetEncoding(name);
            }
            catch (ArgumentException)
            {
                return CodePagesEncodingProvider.Instance.GetEncoding(name) ?? Encoding.UTF8;
            }
        }
    }
}
