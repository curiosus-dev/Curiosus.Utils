using System.Collections.Generic;

namespace Curiosus.Configuration;

/// <summary>
/// What <c>LogConfigurationOnStart</c> prints.
/// </summary>
public class ConfigurationStartupLogOptions
{
    /// <summary>
    /// Sections to print; empty prints the whole configuration except environment variables without a section.
    /// </summary>
    public List<string> Sections { get; } = [];

    /// <summary>
    /// Sections never printed, for example ones with large or noisy values.
    /// </summary>
    public List<string> ExcludedSections { get; } = [];
}
