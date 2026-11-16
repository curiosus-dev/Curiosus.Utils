using System.ComponentModel.DataAnnotations;

namespace Curiosus.DAL
{
    /// <summary>
    /// Options for database connection.
    /// </summary>
    /// <remarks>
    /// Like connection string but with more extra info.
    /// Register it (or a subclass) with
    /// <c>services.AddOptions&lt;DbOptions&gt;().BindConfiguration("Db").ValidateDataAnnotations().ValidateOnStart()</c>:
    /// the data annotation rules of this class apply to subclasses too.
    /// </remarks>
    public class DbOptions
    {
        /// <summary>
        /// Connection string for read-write access. Required, can't be empty or whitespace.
        /// </summary>
        [Required]
        public string ConnectionString { get; set; } = null!;

        /// <summary>
        /// Connection string for read-only access.
        /// </summary>
        public string? ReadOnlyConnectionString { get; set; }

        /// <summary>
        /// Option for logging all SQL queries.
        /// </summary>
        public bool IsGlobalLoggingEnabled { get; set; }

        /// <summary>
        /// Option for logging sensitive data of SQL queries (mostly query params).
        /// </summary>
        public bool IsSensitiveDataLoggingEnabled { get; set; }
    }
}
