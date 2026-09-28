using System;
using Microsoft.AspNetCore.Builder;

namespace Curiosus.Tools.Web.Middleware
{
    public static partial class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds middleware to settings locale info to cookie.
        /// </summary>
        public static IApplicationBuilder UseLocalizationSetter(this IApplicationBuilder app)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            return app.UseMiddleware<LocalizationSetterMiddleware>();
        }
    }
}