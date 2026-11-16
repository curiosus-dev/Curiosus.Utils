using System.ComponentModel.DataAnnotations;

namespace Curiosus.Tools.TempFiles
{
    /// <summary>
    /// Options for working with temporary files.
    /// </summary>
    public class TempFileOptions
    {
        /// <summary>Default configuration section of the options.</summary>
        public const string SectionName = "TempFiles";

        /// <summary>
        /// Full path to temp directory.
        /// </summary>
        [Required]
        public string TempPath { get; set; } = "./.tmp";

        /// <summary>
        /// How often the temp directory will be cleaning. At least 1.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int CleaningFrequencyHours { get; set; } = 24;

        /// <summary>
        /// Files lifetime in hours. At least 1.
        /// </summary>
        [Range(1, int.MaxValue)]
        public int FileTtlHours { get; set; } = 24;
    }
}
