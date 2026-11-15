using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

namespace Curiosus.Tools.Web.Controllers
{
    /// <summary>
    /// External hosts that <see cref="MVCBaseController.RedirectInternal"/> may redirect to, for example the public domain
    /// of the site or the address of its front-end. Register with
    /// <c>services.Configure&lt;RedirectAllowListOptions&gt;(o =&gt; o.AllowedHosts.Add("app.example.com"))</c> or bind a
    /// section.
    /// </summary>
    public class RedirectAllowListOptions
    {
        /// <summary>
        /// Host names (<c>app.example.com</c>), compared case-insensitively and exactly: subdomains are not included.
        /// Only <c>http</c> and <c>https</c> URLs to these hosts are allowed.
        /// </summary>
        public IList<string> AllowedHosts { get; set; } = new List<string>();

        /// <summary>
        /// Parses <paramref name="url"/> and checks that it is an absolute <c>http</c>/<c>https</c> URL to one of
        /// <see cref="AllowedHosts"/>. Redirect to the returned <paramref name="uri"/> (its <see cref="Uri.AbsoluteUri"/>),
        /// not to the original string: browsers may parse the original differently.
        /// </summary>
        /// <remarks>
        /// URLs with a backslash, whitespace or control characters are rejected outright: .NET and browsers treat them
        /// differently (<c>https://app.example.com\@evil.example</c>, tabs and line breaks that browsers drop).
        /// </remarks>
        public bool TryGetAllowedUri(string url, [NotNullWhen(true)] out Uri? uri)
        {
            uri = null;
            if (String.IsNullOrEmpty(url)) return false;

            foreach (var c in url)
            {
                if (c == '\\' || Char.IsWhiteSpace(c) || Char.IsControl(c)) return false;
            }

            if (!Uri.TryCreate(url, UriKind.Absolute, out var parsed)) return false;
            if (parsed.Scheme != Uri.UriSchemeHttps && parsed.Scheme != Uri.UriSchemeHttp) return false;
            if (!String.IsNullOrEmpty(parsed.UserInfo)) return false;

            foreach (var host in AllowedHosts)
            {
                if (String.Equals(parsed.IdnHost, host.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    uri = parsed;
                    return true;
                }
            }

            return false;
        }
    }
}
