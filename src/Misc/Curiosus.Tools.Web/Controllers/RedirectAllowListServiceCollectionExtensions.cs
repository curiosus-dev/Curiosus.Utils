using System;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.Web.Controllers
{
    /// <summary>
    /// Registration of <see cref="RedirectAllowListOptions"/>.
    /// </summary>
    public static class RedirectAllowListServiceCollectionExtensions
    {
        /// <summary>
        /// Registers <see cref="RedirectAllowListOptions"/> bound from the configuration section, the external hosts
        /// <see cref="MVCBaseController.RedirectInternal"/> may redirect to.
        /// </summary>
        /// <param name="services">Service collection.</param>
        /// <param name="configSectionPath">Path of the configuration section,
        /// <see cref="RedirectAllowListOptions.SectionName"/> by default. A missing section binds nothing.</param>
        /// <param name="configure">Configures options in code, after the section is bound. Only the first call binds its
        /// section and registers validation; later calls add their <paramref name="configure"/> only.</param>
        /// <returns>Options builder, validated at start.</returns>
        public static OptionsBuilder<RedirectAllowListOptions> AddRedirectAllowList(
            this IServiceCollection services,
            string configSectionPath = RedirectAllowListOptions.SectionName,
            Action<RedirectAllowListOptions>? configure = null)
        {
            return services.AddSectionOptions(configSectionPath, configure);
        }
    }
}
