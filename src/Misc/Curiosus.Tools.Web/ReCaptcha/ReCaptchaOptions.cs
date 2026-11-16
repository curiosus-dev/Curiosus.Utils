using System.ComponentModel.DataAnnotations;

namespace Curiosus.Tools.Web.ReCaptcha
{
    /// <summary>
    /// Options required for the reCAPTCHA service to work.
    /// </summary>
    public class ReCaptchaOptions
    {
        /// <summary>Default configuration section of the options.</summary>
        public const string SectionName = "ReCaptcha";

        /// <summary>
        /// Absolute address of the reCAPTCHA verification API. Default: <c>https://www.google.com/recaptcha/api/siteverify</c>.
        /// </summary>
        [Required]
        [Url]
        public string ReCaptchaApiUrl { get; set; } = "https://www.google.com/recaptcha/api/siteverify";

        /// <summary>
        /// Use this key in the HTML code that your site transmits to users' devices. Required.
        /// </summary>
        [Required]
        public string ReCaptchaClientKey { get; set; } = "";

        /// <summary>
        /// Use this secret key to exchange data between the site and the reCAPTCHA service. Required.
        /// </summary>
        [Required]
        public string ReCaptchaServerKey { get; set; } = "";

        /// <summary>
        /// Delay between retries, in seconds; not negative. Not used by <see cref="ReCaptchaService"/>: configure retries
        /// on the <c>HttpClient</c> named <see cref="ReCaptchaService.HttpClientName"/>.
        /// </summary>
        [Range(0, int.MaxValue)]
        public int RetryTimeoutSec { get; set; } = 1;

        /// <summary>
        /// Number of retries; not negative. Not used by <see cref="ReCaptchaService"/>: configure retries on the
        /// <c>HttpClient</c> named <see cref="ReCaptchaService.HttpClientName"/>.
        /// </summary>
        [Range(0, int.MaxValue)]
        public int RetryCount { get; set; } = 3;
    }
}
