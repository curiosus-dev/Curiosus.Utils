using System;
using Microsoft.AspNetCore.Builder;

namespace Curiosus.Tools.Web.Middleware
{
    public static partial class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds middleware to trace requests ber user without auth.
        /// </summary>
        public static IApplicationBuilder UseUserTracer(this IApplicationBuilder app)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));

            return app.UseMiddleware<LocalizationSetterMiddleware>();
        }
    }
}