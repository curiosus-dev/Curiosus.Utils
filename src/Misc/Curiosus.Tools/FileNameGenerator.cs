using System;
using System.Linq;
using Curiosus.Tools.Hashing;

namespace Curiosus.Tools
{
    /// <summary>
    /// File unique name generator.
    /// </summary>
    public static class FileNameGenerator
    {
        /// <summary>
        /// Generates unique file name with specified extensions.
        /// </summary>
        public static string GetUniqueFileName(this string? initialFileName)
        {
            var fileExtension = initialFileName?.Split('.').LastOrDefault();

            var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmss}_{XXHasher.GenerateStringHash()}";
            
            if (!String.IsNullOrEmpty(fileExtension))
            {
                fileName = fileName + "." + fileExtension;
            }

            return fileName;
        }
    }
}