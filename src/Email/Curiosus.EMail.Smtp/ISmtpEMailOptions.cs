using Curiosus.Configuration;
using MailKit.Security;

namespace Curiosus.EMail.Smtp
{
    /// <summary>
    /// Options for sending EMail via SMTP.
    /// </summary>
    public interface ISmtpEMailOptions : ILoggableOptions, IValidatableOptions
    {
        /// <summary>
        /// SMTP server host.
        /// </summary>
        string SmtpServer { get; }

        /// <summary>
        /// SMTP server port.
        /// </summary>
        public int SmtpPort { get; }

        /// <summary>
        /// SMTP account user name.
        /// </summary>
        string SmtpLogin { get; }
        
        /// <summary>
        /// SMTP account password.
        /// </summary>
        string SmtpPassword { get; }
        
        /// <summary>
        /// Sender's EMail.
        /// </summary>
        string EMailFrom { get; }
        
        /// <summary>
        /// Reply address for EMail.
        /// </summary>
        string? ReplyTo { get; }
        
        /// <summary>
        /// Human readable sender name.
        /// </summary>
        string SenderName { get; }

        /// <summary>
        /// Should sender send email if incorrect extra params were passed (eg. with another type)?
        /// </summary>
        bool IgnoreIncorrectExtraParamsType { get; }

        /// <summary>
        /// How the connection is secured. <c>null</c>: <see cref="SecureSocketOptions.SslOnConnect"/> on port 465,
        /// <see cref="SecureSocketOptions.StartTls"/> otherwise (the connection fails if the server doesn't offer it).
        /// <see cref="SecureSocketOptions.None"/> only for a local relay; <see cref="SecureSocketOptions.Auto"/> and
        /// <see cref="SecureSocketOptions.StartTlsWhenAvailable"/> are rejected because they allow a downgrade to plain text.
        /// </summary>
        SecureSocketOptions? Security { get; }

        /// <summary>
        /// Accepts any server certificate: for development and test relays only. Rejected in <c>Production</c> and together
        /// with <see cref="SecureSocketOptions.None"/>.
        /// </summary>
        bool AllowInvalidServerCertificate { get; }
    }
}
