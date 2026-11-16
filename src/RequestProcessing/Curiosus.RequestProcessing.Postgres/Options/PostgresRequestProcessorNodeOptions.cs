using System.ComponentModel.DataAnnotations;
using Microsoft.Extensions.Options;

namespace Curiosus.RequestProcessing.Postgres
{
    /// <summary>
    /// Options of a request processor node that receives events from PostgreSQL.
    /// </summary>
    /// <remarks>
    /// Derive from it to add options of the app; <see cref="PostgresEventReceiver"/> is validated with the node options.
    /// </remarks>
    public class PostgresRequestProcessorNodeOptions : RequestProcessorNodeOptions
    {
        /// <summary>
        /// Options of the PostgreSQL events receiver.
        /// </summary>
        [Required]
        [ValidateObjectMembers]
        public PostgresEventReceiverOptions PostgresEventReceiver { get; set; } = new();
    }
}
