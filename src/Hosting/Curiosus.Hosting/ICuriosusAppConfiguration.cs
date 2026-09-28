using Curiosus.Configuration;
using Curiosus.Tools;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Basic configuration for Marvin's app.
    /// </summary>
    public interface ICuriosusAppConfiguration: IValidatableOptions, ILoggableOptions
    {
        /// <summary>
        /// Application name.
        /// </summary>
        /// <remarks>
        /// Uses in logging, etc.
        /// </remarks>
        public string AppName { get; }

        /// <summary>
        /// Culture options.
        /// </summary>
        public CultureOptions Culture { get; }
        
        /// <summary>
        /// Logs configuration options.
        /// </summary>
        public ILoggingConfigurationOptions Log { get; }
    }
}