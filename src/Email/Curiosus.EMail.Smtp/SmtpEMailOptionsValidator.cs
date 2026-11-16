using MailKit.Security;
using Microsoft.Extensions.Options;

namespace Curiosus.EMail.Smtp
{
    /// <summary>
    /// Validates the rules of <see cref="SmtpEMailOptions"/> that data annotations don't express:
    /// <see cref="SmtpEMailOptions.Security"/> doesn't allow a downgrade to plain text and
    /// <see cref="SmtpEMailOptions.AllowInvalidServerCertificate"/> isn't set without TLS.
    /// </summary>
    /// <remarks>
    /// Registered by <see cref="IoCExtensions.AddSmtpEMailSender"/>.
    /// </remarks>
    public class SmtpEMailOptionsValidator : IValidateOptions<SmtpEMailOptions>
    {
        /// <inheritdoc />
        public ValidateOptionsResult Validate(string? name, SmtpEMailOptions options)
        {
            var builder = new ValidateOptionsResultBuilder();
            if (options.Security is SecureSocketOptions.Auto or SecureSocketOptions.StartTlsWhenAvailable)
            {
                builder.AddError(
                    "Auto and StartTlsWhenAvailable allow a downgrade to plain text: use StartTls, SslOnConnect or None",
                    nameof(options.Security));
            }

            if (options.AllowInvalidServerCertificate && options.Security == SecureSocketOptions.None)
            {
                builder.AddError("has no effect without TLS (Security is None)", nameof(options.AllowInvalidServerCertificate));
            }

            return builder.Build();
        }
    }
}
