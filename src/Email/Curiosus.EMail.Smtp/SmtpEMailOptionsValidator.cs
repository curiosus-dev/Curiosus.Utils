using System;
using System.Collections.Generic;
using Curiosus.Configuration;
using MailKit.Security;

namespace Curiosus.EMail.Smtp
{
    /// <summary>
    /// Class for validating <see cref="ISmtpEMailOptions"/>.
    /// </summary>
    public static class SmtpEMailOptionsValidator
    {
        /// <summary>
        /// Validates an <see cref="ISmtpEMailOptions"/>.
        /// </summary>
        /// <returns></returns>
        public static IReadOnlyCollection<ConfigurationValidationError> Validate(this ISmtpEMailOptions options, string? prefix = null)
        {
            var errors = new ConfigurationValidationErrorCollection(prefix);

            errors.AddErrorIf(String.IsNullOrWhiteSpace(options.SmtpServer), nameof(options.SmtpServer),"can't be null or empty");
            errors.AddErrorIf(String.IsNullOrWhiteSpace(options.SmtpLogin), nameof(options.SmtpLogin),"can't be null or empty");
            errors.AddErrorIf(String.IsNullOrWhiteSpace(options.SmtpPassword), nameof(options.SmtpPassword),"can't be null or empty");
            errors.AddErrorIf(String.IsNullOrWhiteSpace(options.EMailFrom), nameof(options.EMailFrom),"can't be null or empty");
            errors.AddErrorIf(String.IsNullOrWhiteSpace(options.SenderName), nameof(options.SenderName),"can't be null or empty");
            errors.AddErrorIf(options.SmtpPort <= 0, nameof(options.SmtpPort),"can't be less than 1");
            errors.AddErrorIf(
                options.Security is SecureSocketOptions.Auto or SecureSocketOptions.StartTlsWhenAvailable,
                nameof(options.Security),
                "Auto and StartTlsWhenAvailable allow a downgrade to plain text: use StartTls, SslOnConnect or None");
            errors.AddErrorIf(
                options.AllowInvalidServerCertificate && options.Security == SecureSocketOptions.None,
                nameof(options.AllowInvalidServerCertificate),
                "has no effect without TLS (Security is None)");
            errors.AddErrorIf(
                options.AllowInvalidServerCertificate && IsProductionEnvironment(),
                nameof(options.AllowInvalidServerCertificate),
                "is not allowed in Production: the SMTP credentials and messages would go to any server that presents a " +
                "certificate; fix the server certificate or trust its CA instead");
            
            return errors;
        }

        /// <summary>
        /// The environment the way the .NET host resolves it: <c>DOTNET_ENVIRONMENT</c>, then <c>ASPNETCORE_ENVIRONMENT</c>,
        /// <c>Production</c> when neither is set.
        /// </summary>
        internal static bool IsProductionEnvironment()
        {
            var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT");
            if (String.IsNullOrWhiteSpace(environment))
                environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");

            return String.IsNullOrWhiteSpace(environment) ||
                   String.Equals(environment, "Production", StringComparison.OrdinalIgnoreCase);
        }
    }
}
