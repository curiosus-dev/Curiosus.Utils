using System.Collections.Generic;
using Curiosus.Configuration;
using Curiosus.Tools;

namespace Curiosus.Hosting
{
    /// <inheritdoc />
    public class CuriosusAppConfiguration : ICuriosusAppConfiguration
    {
        /// <inheritdoc />
        public string AppName { get; set; } = null!;

        /// <inheritdoc />
        public CultureOptions Culture { get; set; } = new CultureOptions();
        
        /// <inheritdoc />
        public CuriosusNLogOptions Log { get; set; } = new CuriosusNLogOptions();

        /// <inheritdoc />
        public virtual IReadOnlyCollection<ConfigurationValidationError> Validate(string? prefix = null)
        {
            return CuriosusAppConfigurationValidator.Validate(this, prefix);
        }
    }
}