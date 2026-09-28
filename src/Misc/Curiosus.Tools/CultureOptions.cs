using System;
using System.Collections.Generic;
using Curiosus.Configuration;

namespace Curiosus.Tools
{
    /// <summary>
    /// Culture options for application.
    /// </summary>
    public class CultureOptions : ILoggableOptions, IValidatableOptions
    {
        /// <summary>
        /// Default culture for application.
        /// </summary>
        public string DefaultCulture { get; set; } = "en-US";

        /// <inheritdoc />
        public virtual IReadOnlyCollection<ConfigurationValidationError> Validate(string? prefix = null)
        {
            var errors = new ConfigurationValidationErrorCollection(prefix);

            errors.AddErrorIf(String.IsNullOrWhiteSpace(DefaultCulture), nameof(DefaultCulture), "not specified");
            
            return errors;
        }
    }
}