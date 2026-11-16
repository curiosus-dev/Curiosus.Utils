using System.ComponentModel.DataAnnotations;

namespace Curiosus.Tools
{
    /// <summary>
    /// Culture options for application.
    /// </summary>
    public class CultureOptions
    {
        /// <summary>Default configuration section of the options.</summary>
        public const string SectionName = "Culture";

        /// <summary>
        /// Default culture for application.
        /// </summary>
        [Required]
        public string DefaultCulture { get; set; } = "en-US";
    }
}
