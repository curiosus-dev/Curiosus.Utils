using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Curiosus.RequestProcessing.Postgres
{
    /// <summary>
    /// Validates a list of PostgreSQL <c>LISTEN</c>/<c>NOTIFY</c> channel names: at least one name, each non-empty,
    /// at most 63 bytes in UTF-8 and without the NUL character.
    /// </summary>
    /// <remarks>
    /// The error names the index of every invalid name (<c>EventNames:1</c>). Apply it to a property of your own
    /// options that lists channels to get the same rules as <see cref="PostgresEventReceiverOptions.EventNames"/>.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
    public sealed class PgChannelNamesAttribute : ValidationAttribute
    {
        /// <inheritdoc />
        public override bool RequiresValidationContext => true;

        /// <inheritdoc />
        protected override ValidationResult? IsValid(object? value, ValidationContext validationContext)
        {
            var memberName = validationContext.MemberName ?? validationContext.DisplayName;
            if (value is not IEnumerable<string?> names)
            {
                return value == null
                    ? new ValidationResult("at least one channel name is required", [memberName])
                    : new ValidationResult("must be a list of channel names", [memberName]);
            }

            var errors = new List<string>();
            var index = 0;
            foreach (var name in names)
            {
                var error = PgChannelName.Validate(name);
                if (error != null) errors.Add($"{memberName}:{index}: {error}");
                index++;
            }

            if (index == 0) return new ValidationResult("at least one channel name is required", [memberName]);

            return errors.Count == 0
                ? ValidationResult.Success
                : new ValidationResult(String.Join(" ", errors), [memberName]);
        }
    }
}
