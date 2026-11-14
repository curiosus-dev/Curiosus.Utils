using System;

namespace Curiosus.Configuration
{
    /// <summary>
    /// Marks an option property as not secret: <see cref="ConfigurationPrinter"/> prints its value as is, even when
    /// the property name looks like a secret (for example a public site key named <c>ClientKey</c>).
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class NotSecretAttribute : Attribute
    {
    }
}
