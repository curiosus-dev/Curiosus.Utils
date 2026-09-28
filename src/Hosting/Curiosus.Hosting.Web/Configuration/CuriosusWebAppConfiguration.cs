using System.Collections.Generic;
using Curiosus.Configuration;
using Curiosus.Hosting.ThreadPool;

namespace Curiosus.Hosting.Web
{
    /// <inheritdoc cref="ICuriosusWebAppConfiguration" />
    public abstract class CuriosusWebAppConfiguration : CuriosusAppConfiguration, ICuriosusWebAppConfiguration
    {
        /// <inheritdoc />
        public string? Urls { get; set; }

        /// <inheritdoc />
        public KestrelOptions? Kestrel { get; set; }

        /// <inheritdoc />
        public ThreadPoolOptions ThreadPool { get; set; } = new ThreadPoolOptions();
        
        /// <inheritdoc />
        public string[]? SensitiveDataFieldNames { get; set; }

        /// <inheritdoc />
        public bool UseIISIntegration { get; set; } = false;

        /// <inheritdoc />
        public override IReadOnlyCollection<ConfigurationValidationError> Validate(string? prefix = null)
        {
            return CuriosusWebAppConfigurationValidator.Validate(this, prefix);
        }
    }
}