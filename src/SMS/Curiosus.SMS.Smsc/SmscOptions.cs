using System.ComponentModel.DataAnnotations;

namespace Curiosus.SMS.Smsc
{
    /// <summary>
    /// Options for <see cref="Curiosus.SMS.Smsc.SmscSender"/>.
    /// </summary>
    /// <remarks>
    /// <see cref="IoCExtensions.AddSmscSmsSender"/> binds them from the configuration section <see cref="SectionName"/>
    /// and validates them when the host starts.
    /// </remarks>
    public class SmscOptions
    {
        /// <summary>Default configuration section of the options.</summary>
        public const string SectionName = "Smsc";

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
