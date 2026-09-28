using System.Globalization;
using Curiosus.Localization;

namespace Curiosus.FileDataReaderWriters.Resources
{
    internal static class LNG
    {
        private static readonly LocalizerCore Localizer;
        private static readonly LocalizationOptions Options;

        static LNG()
        {
            Options = new LocalizationOptions();
            Localizer = new LocalizerCore(typeof(LNG).Assembly, Options);
        }

        public static string Get(string source, params object[] arguments)
        {
            return Localizer.Get(Options.Prefix, source, false, arguments);
        }
    }
}
