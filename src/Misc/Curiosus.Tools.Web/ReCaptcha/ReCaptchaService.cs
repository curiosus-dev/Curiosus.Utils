using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.WebUtilities;

namespace Curiosus.Tools.Web.ReCaptcha
{
    public class ReCaptchaService
    {
        /// <summary>
        /// Name of the <see cref="HttpClient"/> requested from <see cref="IHttpClientFactory"/>.
        /// </summary>
        public const string HttpClientName = "Curiosus.Tools.Web.ReCaptcha";

        private readonly ReCaptchaOptions _options;
        private readonly IHttpClientFactory _httpClientFactory;

        /// <summary>
        /// Creates a service that gets <see cref="HttpClient"/> named <see cref="HttpClientName"/> from the factory.
        /// </summary>
        public ReCaptchaService(ReCaptchaOptions options, IHttpClientFactory httpClientFactory)
        {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
        }

        /// <summary>
        /// Verifies the reCAPTCHA response token.
        /// </summary>
        /// <exception cref="HttpRequestException">The reCAPTCHA API is unavailable or returned an error status code.</exception>
        public async Task<bool> VerifyReCaptchaAsync(string response)
        {
            if (String.IsNullOrWhiteSpace(response))
                return false;

            var requestUri = QueryHelpers.AddQueryString(
                _options.ReCaptchaApiUrl,
                new Dictionary<string, string?>
                {
                    ["secret"] = _options.ReCaptchaServerKey,
                    ["response"] = response
                });

            var httpClient = _httpClientFactory.CreateClient(HttpClientName);
            var reCaptchaResponse = await httpClient.GetFromJsonAsync<ReCaptchaResponse>(requestUri);

            return reCaptchaResponse?.success ?? false;
        }
    }
}
