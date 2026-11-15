using System;
using System.IO;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Curiosus.Tools.Web.DataProtection
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to set up ASP.NET Core data protection.
    /// </summary>
    public static class DataProtectionServiceCollectionExtensions
    {
        /// <summary>
        /// Adds ASP.NET Core data protection configured from <paramref name="section"/>
        /// (<see cref="CuriosusDataProtectionOptions"/>); an empty section keeps the framework defaults. Data is always
        /// protected: there is no way to turn protection off.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="section">Configuration section, for example <c>configuration.GetSection("DataProtection")</c>.</param>
        /// <returns>Data protection builder for <c>ProtectKeysWithCertificate</c>, another key repository, etc.</returns>
        /// <remarks>
        /// The options are validated when the host starts: <c>KeyRingPath</c> must exist, and
        /// <c>DisableAutomaticKeyGeneration</c> needs <c>KeyRingPath</c>.
        /// </remarks>
        public static IDataProtectionBuilder AddCuriosusDataProtection(this IServiceCollection services, IConfiguration section)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));
            if (section == null) throw new ArgumentNullException(nameof(section));

            services.AddOptions<CuriosusDataProtectionOptions>()
                .Bind(section)
                .Validate(
                    o => String.IsNullOrWhiteSpace(o.KeyRingPath) || Directory.Exists(o.KeyRingPath),
                    "DataProtection: KeyRingPath must be an existing directory")
                .Validate(
                    o => !o.DisableAutomaticKeyGeneration || !String.IsNullOrWhiteSpace(o.KeyRingPath),
                    "DataProtection: DisableAutomaticKeyGeneration requires KeyRingPath with the keys")
                .ValidateOnStart();

            // the builder is configured now, so the values are read now as well
            var options = section.Get<CuriosusDataProtectionOptions>() ?? new CuriosusDataProtectionOptions();
            var builder = services.AddDataProtection();

            if (!String.IsNullOrWhiteSpace(options.ApplicationName))
                builder.SetApplicationName(options.ApplicationName);

            if (!String.IsNullOrWhiteSpace(options.KeyRingPath))
                builder.PersistKeysToFileSystem(new DirectoryInfo(options.KeyRingPath));

            if (options.DisableAutomaticKeyGeneration)
                builder.DisableAutomaticKeyGeneration();

            return builder;
        }
    }
}
