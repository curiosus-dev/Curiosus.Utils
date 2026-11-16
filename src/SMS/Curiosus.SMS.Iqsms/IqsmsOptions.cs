using System.ComponentModel.DataAnnotations;

namespace Curiosus.SMS.Iqsms;

/// <summary>
/// Options for <see cref="Curiosus.SMS.Iqsms.IqsmsSender"/>.
/// </summary>
/// <remarks>
/// <see cref="IoCExtensions.AddIqsmsSender"/> binds them from the configuration section <see cref="SectionName"/>
/// and validates them when the host starts.
/// </remarks>
public class IqsmsOptions
{
    /// <summary>Default configuration section of the options.</summary>
    public const string SectionName = "Iqsms";

    /// <summary>
    /// Login for iqsms. Required.
    /// </summary>
    [Required]
    public string Login { get; set; } = null!;

    /// <summary>
    /// Password for iqsms. Required.
    /// </summary>
    [Required]
    public string Password { get; set; } = null!;

    /// <summary>
    /// Sender's name. Optional.
    /// </summary>
    public string? Sender { get; set; }

    /// <summary>
    /// When true, automatically transforms phone numbers to the form +71234567890
    /// </summary>
    /// <remarks>
    /// iqsms.ru requires phone number in format +71234567890
    /// </remarks>
    public bool AutoTransformPhoneNumber { get; set; } = true;
}
