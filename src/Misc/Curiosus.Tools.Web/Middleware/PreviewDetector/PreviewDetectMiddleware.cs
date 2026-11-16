using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.Middleware
{
    /// <summary>
    /// Middleware for detecting requests from bots/services that make preview pages.
    /// </summary>
    public class PreviewDetectMiddleware
    {
        private readonly RequestDelegate _next;
        private readonly PathString _redirectPath;

        /// <summary>
        /// Creates the middleware; the options are validated when the host starts
        /// (<see cref="PreviewDetectorServiceCollectionExtensions.AddPreviewDetector"/>).
        /// </summary>
        public PreviewDetectMiddleware(
            RequestDelegate next,
            IOptions<PreviewDetectOptions> options)
        {
            ArgumentNullException.ThrowIfNull(options);

            _next = next ?? throw new ArgumentNullException(nameof(next));
            _redirectPath = options.Value.RedirectPath;
        }

        /// <summary>
        /// Rewrites the path of a preview request to <see cref="PreviewDetectOptions.RedirectPath"/>.
        /// </summary>
        public Task Invoke(HttpContext context)
        {
            // if this is a bot that makes a Preview, then we will redirect to the specified page
            if (context.IsPreviewRequest())
            {
                context.Request.Path = _redirectPath;
            }

            return _next(context);
        }
    }
}
