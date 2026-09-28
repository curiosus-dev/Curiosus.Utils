using System;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.Middleware
{
    public static class ExceptionHandlerApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds a middleware to the pipeline that will catch exceptions, and re-execute the request in an alternate pipeline.
        /// The request will not be re-executed if the response has already started.
        /// The request will not be logged.
        /// </summary>
        public static IApplicationBuilder UseCuriosusExceptionHandler(this IApplicationBuilder app, string errorHandlingPath)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (String.IsNullOrWhiteSpace(errorHandlingPath))
                throw new ArgumentNullException(nameof(errorHandlingPath));

            return app.UseCuriosusExceptionHandler(new CuriosusExceptionHandlerOptions
            {
                ExceptionHandlingPath = errorHandlingPath
            });
        }
        
        /// <summary>
        /// Adds a middleware to the pipeline that will catch exceptions, and re-execute the request in an alternate pipeline.
        /// The request will not be re-executed if the response has already started.
        /// The request will not be logged.
        /// </summary>
        /// <param name="app"></param>
        /// <param name="options"></param>
        /// <returns></returns>
        public static IApplicationBuilder UseCuriosusExceptionHandler(this IApplicationBuilder app, CuriosusExceptionHandlerOptions options)
        {
            if (app == null)
                throw new ArgumentNullException(nameof(app));
            if (options == null)
                throw new ArgumentNullException(nameof(options));

            return app.UseMiddleware<CuriosusExceptionHandleMiddleware>(Options.Create(options));
        }
    }
}