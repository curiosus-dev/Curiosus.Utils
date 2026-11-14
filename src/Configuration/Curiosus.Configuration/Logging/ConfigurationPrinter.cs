using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;

namespace Curiosus.Configuration
{
    /// <summary>
    /// Service for printing configuration to string.
    /// </summary>
    public class ConfigurationPrinter
    {
        private const string ModelNullText = "null";

        /// <summary>
        /// Returns string representation of configuration.
        /// </summary>
        /// <returns></returns>
        public string GetLog(ILoggableOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));

            return GetLog(options, 0, 30);
        }

        private string GetLog(ILoggableOptions? options, int offset, int defaultWidth, bool printSeparator = true)
        {
            var builder = new StringBuilder();
            if (printSeparator)
            {
                builder.AppendLine("-------------");
            }

            if (options == null) return ModelNullText;

            var type = options.GetType();
            foreach (var propertyInfo in type.GetProperties())
            {
                if (propertyInfo.GetIndexParameters().Length > 0 || propertyInfo.GetMethod is not { IsPublic: true }) continue;

                var propValue = propertyInfo.GetValue(options);
                var currentOffset = (offset + defaultWidth);
                var propName = $"{{{0}, {currentOffset}}}";
                var pro = String.Format(propName, propertyInfo.Name);
                builder.AppendLine($"{pro}: {PropertyValueToString(propertyInfo, propValue, currentOffset, 15)}");
            }

            // removed new line symbol - it's more beaty
            if (builder.Length > 0)
            {
                builder.Remove(builder.Length - 1, 1);
            }

            return builder.ToString();
        }

        private string PropertyValueToString(PropertyInfo propertyInfo, object? value, int offset, int defaultWidth)
        {
            if (value == null) return ModelNullText;
            if (Attribute.IsDefined(propertyInfo, typeof(SecretAttribute))) return SecretMasker.Mask;
            if (Attribute.IsDefined(propertyInfo, typeof(NotSecretAttribute))) return ValueToString(value, offset, defaultWidth);

            return NamedValueToString(propertyInfo.Name, value, offset, defaultWidth);
        }

        private string NamedValueToString(string name, object? value, int offset, int defaultWidth)
        {
            if (value == null) return ModelNullText;

            if (SecretMasker.IsConnectionStringName(name) && value is string connectionString)
            {
                return SecretMasker.MaskConnectionString(connectionString);
            }

            if (SecretMasker.IsSecretName(name)) return SecretMasker.Mask;

            return ValueToString(value, offset, defaultWidth);
        }

        private string ValueToString(object? propValue, int offset, int defaultWidth)
        {
            if (propValue == null) return ModelNullText;

            if (propValue is ILoggableOptions loggableOptions)
            {
                return GetLog(loggableOptions, offset, defaultWidth);
            }

            if (propValue is ILoggableOptions[] loggableOption)
            {
                var log = new StringBuilder();
                var printSeparator = true;
                foreach (var option in loggableOption)
                {
                    log.AppendLine(GetLog(option, offset, defaultWidth, printSeparator));
                    printSeparator = false;
                }

                return log.ToString();
            }

            if (propValue is IDictionary dictionary)
            {
                var entries = new List<string>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    var key = entry.Key.ToString() ?? String.Empty;
                    entries.Add($"{key}={NamedValueToString(key, entry.Value, offset, defaultWidth)}");
                }

                return "{" + string.Join(", ", entries) + "}";
            }

            if (propValue is ICollection collection)
            {
                var arr = new List<object>();
                var enumerator = collection.GetEnumerator();
                while (enumerator.MoveNext())
                {
                    arr.Add(enumerator.Current);
                }

                return "[" + string.Join(",", arr.ToArray()) + "]";
            }

            return propValue.ToString() ?? ModelNullText;
        }
    }
}
