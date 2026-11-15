using System;
using System.Collections.Generic;

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
        /// Whether <paramref name="url"/> is an absolute <c>http</c>/<c>https</c> URL to one of <see cref="AllowedHosts"/>.
        /// </summary>
        public bool IsAllowed(string url)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)) return false;
            if (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp) return false;

            foreach (var host in AllowedHosts)
            {
                if (String.Equals(uri.IdnHost, host.Trim(), StringComparison.OrdinalIgnoreCase)) return true;
            }

            return false;
        }
    }
}
