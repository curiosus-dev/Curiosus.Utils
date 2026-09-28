using Microsoft.AspNetCore.Http;

namespace Curiosus.Tools.Web.Middleware
{
    /// <summary>
    /// Options for <see cref="CuriosusExceptionHandleMiddleware"/>
    /// </summary>
    public class CuriosusExceptionHandlerOptions
    {
        /// <summary>
        /// Path with action that will handle exception
        /// </summary>
        public PathString ExceptionHandlingPath { get; set; }
    }
}