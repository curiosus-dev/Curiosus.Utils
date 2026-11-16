using System.ComponentModel.DataAnnotations;

namespace Curiosus.Email.UnisenderGo
{
    /// <summary>
    /// Options for sending emails via UnisenderGo.
    /// </summary>
    /// <remarks>
    /// Validated by data annotations when the app starts.
    /// </remarks>
    public class UnisenderGoEmailOptions
    {
        /// <summary>
        /// Default configuration section of the options.
        /// </summary>
        public const string SectionName = "UnisenderGo";

        /// <summary>
        /// API key.
        /// </summary>
        [Required]
        public string ApiKey { get; set; } = null!;

        /// <summary>
        /// Region of API. Default: <see cref="UnisenderGoRegion.Russia"/>.
        /// </summary>
        [EnumDataType(typeof(UnisenderGoRegion))]
        public UnisenderGoRegion Region { get; set; } = UnisenderGoRegion.Russia;

        /// <summary>
        /// Sender's EMail
        /// </summary>
        [Required]
        public string EmailFrom { get; set; } = null!;

        /// <summary>
        /// Sender's name.
        /// </summary>
        [Required]
        public string FromName { get; set; } = null!;

        /// <summary>
        /// Should sender send email if incorrect type of extra params was passed?
        /// </summary>
        public bool IgnoreIncorrectExtraParamsType { get; set; }

        /// <summary>
        /// Reply address for EMail.
        /// </summary>
        public string? ReplyTo { get; set; }

        /// <summary>
        /// Link for user's unsubscribe page.
        /// </summary>
        public string? UnsubscribeUrl { get; set; }

        /// <summary>
        /// Is tracking opening links enabled?
        /// </summary>
        public bool? TrackLinks { get; set; }

        /// <summary>
        /// Is tracking email reading enabled?
        /// </summary>
        public bool? TrackReads { get; set; }

        /// <summary>
        /// Should Unisender add their footer with unsubscribed url.
        /// </summary>
        /// <remarks>
        /// This option can not affect on anything if Unisender doesn't activate disabling footer for client.
        /// </remarks>
        public bool? SkipUnisenderUnsubscribeFooter { get; set; }
    }
}
