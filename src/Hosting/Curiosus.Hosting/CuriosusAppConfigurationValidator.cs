using System;
using System.Collections.Generic;
using Curiosus.Configuration;

namespace Curiosus.Hosting
{
    public static class CuriosusAppConfigurationValidator
    {
        /// <summary>
        /// Validates configuration.
        /// </summary>
        public static IReadOnlyCollection<ConfigurationValidationError> Validate(
            this ICuriosusAppConfiguration configuration,
            string? prefix = null)
        {
            var errors = new ConfigurationValidationErrorCollection(prefix);
            
            errors.AddErrorIf(String.IsNullOrWhiteSpace(configuration.AppName), nameof(configuration.AppName), "Can't be empty");
            errors.AddErrors(configuration.Culture.Validate(nameof(configuration.Culture)));
            errors.AddErrorIf(
                String.IsNullOrWhiteSpace(configuration.Log.LogConfigurationPath),
                $"{nameof(configuration.Log)}.{nameof(configuration.Log.LogConfigurationPath)}",
                "Can't be empty");
            errors.AddErrorIf(
                String.IsNullOrWhiteSpace(configuration.Log.LogOutputDirectory),
                $"{nameof(configuration.Log)}.{nameof(configuration.Log.LogOutputDirectory)}",
                "Can't be empty");

            return errors;
        }
    }
}