using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.DataProtection
{
    /// <summary>
    /// Validates <see cref="CuriosusDataProtectionOptions"/> when the host starts.
    /// </summary>
    internal sealed class CuriosusDataProtectionOptionsValidator : IValidateOptions<CuriosusDataProtectionOptions>
    {
        public ValidateOptionsResult Validate(string? name, CuriosusDataProtectionOptions options)
        {
            var failures = new List<string>();

            if (!String.IsNullOrWhiteSpace(options.KeyRingPath) && !Directory.Exists(options.KeyRingPath))
                failures.Add($"DataProtection: KeyRingPath \"{options.KeyRingPath}\" must be an existing directory");

            if (options.DisableAutomaticKeyGeneration && String.IsNullOrWhiteSpace(options.KeyRingPath))
                failures.Add("DataProtection: DisableAutomaticKeyGeneration requires KeyRingPath with the keys");

            return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
        }
    }
}
