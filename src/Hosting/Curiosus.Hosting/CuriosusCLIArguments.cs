using System.Reflection;
using Curiosus.Configuration;
using EntryPoint;

namespace Curiosus.Hosting
{
    /// <summary>
    /// Basic CLI arguments for any Marvin app.
    /// </summary>
    public class CuriosusCLIArguments : BaseCliArguments, ILoggableOptions
    {
        /// <inheritdoc cref="CuriosusCLIArguments"/>
        public CuriosusCLIArguments() : this(Assembly.GetEntryAssembly()?.GetName().Name ?? "MarvinApp")
        {
        }
        
        protected CuriosusCLIArguments(string utilityName) : base(utilityName)
        {
        }

        /// <summary>
        /// Absolute path to directory with app config.
        /// </summary>
        [OptionParameter(
            ShortName: 'c',
            LongName: "config")]
        [Help("Absolute path to directory with app config")]
        public string? ConfigurationDirectory { get; set; }

        /// <summary>
        /// Absolute path to config file.
        /// </summary>
        [Help("Show app version")]
        [Option(
            ShortName: 'v',
            LongName: "version")]
        public bool ShowVersion { get; set; }
    }
}
