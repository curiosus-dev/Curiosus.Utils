using System;

namespace Curiosus.Configuration
{
    /// <summary>
    /// Marks an option property as a secret: <see cref="ConfigurationPrinter"/> prints <c>***</c> instead of its value,
    /// whatever the property name is.
    /// </summary>
    /// <remarks>
    /// Takes precedence over <see cref="NotSecretAttribute"/> when a property has both.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property, Inherited = true)]
    public sealed class SecretAttribute : Attribute
    {
    }
}
