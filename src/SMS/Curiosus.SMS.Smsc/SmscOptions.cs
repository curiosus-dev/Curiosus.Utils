using System.ComponentModel.DataAnnotations;

namespace Curiosus.SMS.Smsc
{
    /// <summary>
    /// Options for <see cref="Curiosus.SMS.Smsc.SmscSender"/>.
    /// </summary>
    /// <remarks>
    /// Register them with <see cref="IoCExtensions.AddSmscSmsSender"/> and bind from configuration with
    /// <c>.BindConfiguration("Smsc")</c>; they are validated when the host starts.
    /// </remarks>
    public class SmscOptions
    {
        /// <summary>
        /// Login for Smsc. Required.
        /// </summary>
        [Required]
        public string SmscLogin { get; set; } = null!;

        /// <summary>
        /// Password for Smsc. Required.
        /// </summary>
        [Required]
        public string SmscPassword { get; set; } = null!;

        /// <summary>
        /// Sender's name. Optional.
        /// </summary>
        public string? SmscSender { get; set; }
    }
}
