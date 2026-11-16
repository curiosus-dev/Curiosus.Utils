using System.ComponentModel.DataAnnotations;
using MailKit.Security;

namespace Curiosus.EMail.Smtp
{
    /// <summary>
    /// Options for sending EMail via SMTP.
    /// </summary>
    /// <remarks>
    /// Validated by data annotations and <see cref="SmtpEMailOptionsValidator"/> when the app starts.
    /// </remarks>
    public class SmtpEMailOptions
    {
        /// <summary>
        /// SMTP server host.
        /// </summary>
        [Required]
        public string SmtpServer { get; set; } = null!;

        /// <summary>
        /// SMTP server port. Default: 25.
        /// </summary>
        [Range(1, 65535)]
        public int SmtpPort { get; set; } = 25;

        /// <summary>
        /// SMTP account user name.
        /// </summary>
        [Required]
        public string SmtpLogin { get; set; } = null!;

        /// <summary>
        /// SMTP account password.
        /// </summary>
        [Required]
        public string SmtpPassword { get; set; } = null!;

        /// <summary>
        /// Sender's EMail.
        /// </summary>
        [Required]
        public string EMailFrom { get; set; } = null!;

        /// <summary>
        /// Reply address for EMail.
        /// </summary>
        public string? ReplyTo { get; set; }

        /// <summary>
        /// Human readable sender name.
        /// </summary>
        [Required]
        public string SenderName { get; set; } = null!;

        /// <summary>
        /// Should sender send email if incorrect type of extra params was passed?
        /// </summary>
        public bool IgnoreIncorrectExtraParamsType { get; set; }

        /// <summary>
        /// How the connection is secured. <c>null</c>: <see cref="SecureSocketOptions.SslOnConnect"/> on port 465,
        /// <see cref="SecureSocketOptions.StartTls"/> otherwise (the connection fails if the server doesn't offer it).
        /// <see cref="SecureSocketOptions.None"/> only for a local relay; <see cref="SecureSocketOptions.Auto"/> and
        /// <see cref="SecureSocketOptions.StartTlsWhenAvailable"/> are rejected because they allow a downgrade to plain text.
        /// </summary>
        public SecureSocketOptions? Security { get; set; }

        /// <summary>
        /// Accepts any server certificate, which lets anyone on the network path read the credentials and messages. Meant
        /// for development and test relays; the sender logs a warning whenever it is set. Fails validation together with
        /// <see cref="SecureSocketOptions.None"/>.
        /// </summary>
        public bool AllowInvalidServerCertificate { get; set; }
    }
}
