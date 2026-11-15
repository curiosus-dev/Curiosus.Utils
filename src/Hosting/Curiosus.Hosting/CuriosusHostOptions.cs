namespace Curiosus.Hosting
{
    /// <summary>
    /// Options of <see cref="CuriosusHostingExtensions.AddCuriosusDefaults{TBuilder}"/>.
    /// </summary>
    public class CuriosusHostOptions
    {
        /// <summary>
        /// Directory with the YAML configuration files or a single file; default: <c>-c</c> / <c>--config</c>, then the
        /// content root.
        /// </summary>
        public string? ConfigurationPath { get; set; }

        /// <summary>
        /// Whether <c>config.yml</c> may be missing. Default: <c>false</c>.
        /// </summary>
        public bool IsConfigurationOptional { get; set; }

        /// <summary>
        /// Whether to log the configuration when the host starts, secrets masked. Default: <c>true</c>.
        /// </summary>
        public bool LogConfigurationOnStart { get; set; } = true;
    }
}
