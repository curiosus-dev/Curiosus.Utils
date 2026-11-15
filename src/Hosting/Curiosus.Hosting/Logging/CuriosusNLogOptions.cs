using System.ComponentModel.DataAnnotations;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Logging options, bound from the <c>Log</c> section.
    /// </summary>
    public class CuriosusNLogOptions
    {
        /// <summary>
        /// Name of the configuration section the options are bound from.
        /// </summary>
        public const string SectionName = "Log";

        /// <summary>
        /// Path to the NLog configuration file, relative to the content root.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string LogConfigurationPath { get; set; } = "NLog.config";

        /// <summary>
        /// Path to the log output directory, relative to the content root. Available in NLog.config as
        /// <c>${var:outputdir}</c>.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string LogOutputDirectory { get; set; } = "log";
    }
}
