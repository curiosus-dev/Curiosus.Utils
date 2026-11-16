using System;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.Middleware
{
    public static partial class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds middleware that detects requests from preview bots/services and redirects to the specified page.
        /// </summary>
        /// <param name="app">Application builder.</param>
        /// <param name="redirectPath">Path to redirect to, starting with <c>/</c>.</param>
        public static IApplicationBuilder UsePreviewDetector(this IApplicationBuilder app, string redirectPath)
        {
            ArgumentNullException.ThrowIfNull(app);
            ArgumentException.ThrowIfNullOrWhiteSpace(redirectPath);

            return app.UseMiddleware<PreviewDetectMiddleware>(Options.Create(new PreviewDetectOptions
            {
                RedirectPath = redirectPath
            }));
        }

        /// <summary>
        /// Adds middleware that detects requests from preview bots/services and redirects to the page of
        /// <see cref="PreviewDetectOptions"/> registered by
        /// <see cref="PreviewDetectorServiceCollectionExtensions.AddPreviewDetector"/>.
        /// </summary>
        /// <param name="app">Application builder.</param>
        /// <exception cref="InvalidOperationException"><c>AddPreviewDetector</c> wasn't called.</exception>
        public static IApplicationBuilder UsePreviewDetector(this IApplicationBuilder app)
        {
            ArgumentNullException.ThrowIfNull(app);

            var registered = app.ApplicationServices
                .GetServices<IValidateOptions<PreviewDetectOptions>>()
                .OfType<PreviewDetectOptionsValidator>()
                .Any();
            if (!registered)
                throw new InvalidOperationException("Call services.AddPreviewDetector() before app.UsePreviewDetector()");

            return app.UseMiddleware<PreviewDetectMiddleware>();
        }
    }
}
