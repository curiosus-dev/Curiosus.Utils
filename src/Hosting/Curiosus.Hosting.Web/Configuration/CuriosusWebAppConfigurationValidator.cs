using System;
using System.Collections.Generic;
using Curiosus.Configuration;
using Curiosus.Tools.Web;

namespace Curiosus.Hosting.Web
{
    /// <summary>
    /// Validator of <see cref="ICuriosusWebAppConfiguration"/> implementations.
    /// </summary>
    public static class CuriosusWebAppConfigurationValidator
    {
        /// <summary>
        /// Validates implementation of <see cref="ICuriosusWebAppConfiguration"/>
        /// </summary>
        public static IReadOnlyCollection<ConfigurationValidationError> Validate<T>(this T configuration, string? prefix) where T: ICuriosusWebAppConfiguration
        {
            var errors = new ConfigurationValidationErrorCollection(prefix);

            if (!configuration.UseIISIntegration)
            {
                if (String.IsNullOrWhiteSpace(configuration.Urls) && configuration.Kestrel == null)
                {
                    errors.AddError(nameof(configuration.Urls), $"{nameof(configuration.Urls)} or {nameof(configuration.Kestrel)} must be specified");
                }
                else if (configuration.Urls != null && configuration.Kestrel != null)
                {
                    errors.AddError(nameof(configuration.Urls), $"{nameof(configuration.Urls)} and {nameof(configuration.Kestrel)} can't be used at same time");
                }

                if (configuration.Kestrel != null)
                {
                    errors.AddErrors(configuration.Kestrel.Validate(prefix + $":{nameof(configuration.Kestrel)}"));
                }
            }

            errors.AddErrors(configuration.ThreadPool.Validate(prefix + $":{nameof(configuration.ThreadPool)}"));
            errors.AddErrors(CuriosusAppConfigurationValidator.Validate(configuration, prefix));

            // ReSharper disable once SuspiciousTypeConversion.Global
            if (configuration is IWebAppConfigurationWithPublicDomain config)
            {
                errors.AddErrorIf(String.IsNullOrWhiteSpace(config.PublicDomain), nameof(config.PublicDomain), "can't be null");
            }
            
            return errors;
        }
    }
}