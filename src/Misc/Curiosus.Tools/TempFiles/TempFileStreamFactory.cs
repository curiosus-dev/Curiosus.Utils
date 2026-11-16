using System;
using System.IO;
using Microsoft.Extensions.Options;

namespace Curiosus.Tools.TempFiles
{
    /// <inheritdoc />
    public class TempFileStreamFactory : ITempFileStreamFactory
    {
        /// <summary>
        /// Option for working with temporary files.
        /// </summary>
        private readonly TempFileOptions _tempFileOptions;

        /// <inheritdoc cref="TempFileStreamFactory"/>
        public TempFileStreamFactory(IOptions<TempFileOptions> tempFileOptions)
        {
            ArgumentNullException.ThrowIfNull(tempFileOptions);
            _tempFileOptions = tempFileOptions.Value;
        }

        /// <inheritdoc />
        public TempFileStream CreateTempFileStream(string? fileName = null)
        {
            var tempFileName = String.IsNullOrWhiteSpace(fileName)
                ? $"{Guid.NewGuid()}.tmp"
                : fileName;
            var tempFilePath = Path.Combine(_tempFileOptions.TempPath, tempFileName);

            return new TempFileStream(tempFilePath, FileMode.Create);
        }
    }
}
