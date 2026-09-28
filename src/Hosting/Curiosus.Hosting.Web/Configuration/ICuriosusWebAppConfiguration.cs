using Curiosus.Hosting.ThreadPool;
using Curiosus.Tools;

namespace Curiosus.Hosting.Web
{
    /// <summary>
    /// Basic web app configuration. 
    /// </summary>
    public interface ICuriosusWebAppConfiguration : ICuriosusAppConfiguration
    {
        /// <summary>
        /// Urls to listen for web app.
        /// </summary>
        public string? Urls { get; }
        
        /// <summary>
        /// Some Kestrel options.
        /// </summary>
        KestrelOptions? Kestrel { get; }
        
        /// <summary>
        /// Thread pool options.
        /// </summary>
        ThreadPoolOptions ThreadPool { get; }
        
        /// <summary>
        /// Name collection of the fields the data in which we want to hide. Used by <see cref="SensitiveDataProtector"/>
        /// </summary>
        public string[]? SensitiveDataFieldNames { get; }
        
        /// <summary>
        /// Do we need to enable IIS Integration.
        /// </summary>
        /// <remarks>
        /// By calling UseIISIntegration for WebHostBuilder.
        /// </remarks>
        bool UseIISIntegration { get; }
    }
}