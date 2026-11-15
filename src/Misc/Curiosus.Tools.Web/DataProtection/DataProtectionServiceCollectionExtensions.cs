using System;
using System.IO;
using Curiosus.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.DataProtection.KeyManagement;
using Microsoft.AspNetCore.DataProtection.Repositories;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.DataProtection
{
    /// <summary>
    /// Extension methods for <see cref="IServiceCollection"/> to set up ASP.NET Core data protection.
    /// </summary>
    public static class DataProtectionServiceCollectionExtensions
    {
        /// <summary>
        /// Adds ASP.NET Core data protection configured from the <paramref name="configSectionPath"/> section
        /// (<see cref="CuriosusDataProtectionOptions"/>); an empty or missing section keeps the framework defaults. Data
        /// is always protected: there is no way to turn protection off.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Path of the configuration section, <c>DataProtection</c> by default.</param>
        /// <returns>Data protection builder for <c>ProtectKeysWithCertificate</c>, another key repository, etc.</returns>
        /// <remarks>
        /// The options are bound with <c>AddCuriosusOptions</c> and validated when the host starts: <c>KeyRingPath</c>
        /// must exist, and <c>DisableAutomaticKeyGeneration</c> needs <c>KeyRingPath</c>.
        /// </remarks>
        public static IDataProtectionBuilder AddCuriosusDataProtection(
            this IServiceCollection services,
            string configSectionPath = "DataProtection")
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddCuriosusOptions<CuriosusDataProtectionOptions>(configSectionPath);
            services.TryAddEnumerable(ServiceDescriptor
                .Singleton<IValidateOptions<CuriosusDataProtectionOptions>, CuriosusDataProtectionOptionsValidator>());

            // registered after AddDataProtection, whose own setup would overwrite these otherwise. They set what
            // SetApplicationName, PersistKeysToFileSystem and DisableAutomaticKeyGeneration set, from the bound options
            var builder = services.AddDataProtection();
            services.AddOptions<DataProtectionOptions>()
                .Configure<IOptions<CuriosusDataProtectionOptions>>((target, source) =>
                {
                    if (!String.IsNullOrWhiteSpace(source.Value.ApplicationName))
                        target.ApplicationDiscriminator = source.Value.ApplicationName;
                });
            services.AddOptions<KeyManagementOptions>()
                .Configure<IOptions<CuriosusDataProtectionOptions>, ILoggerFactory>((target, source, loggerFactory) =>
                {
                    if (!String.IsNullOrWhiteSpace(source.Value.KeyRingPath))
                        target.XmlRepository = new FileSystemXmlRepository(new DirectoryInfo(source.Value.KeyRingPath), loggerFactory);

                    if (source.Value.DisableAutomaticKeyGeneration)
                        target.AutoGenerateKeys = false;
                });

            return builder;
        }
    }
}
