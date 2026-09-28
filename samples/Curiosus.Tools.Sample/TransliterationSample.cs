using System;

namespace Curiosus.Tools.Sample
{
    public static class TransliterationSample
    {
        public static void Run()
        {
            var text = "Погребальные байки";
            var transliterated = Transliteration.Front(text);

            Console.WriteLine($"Original text: {text}");
            Console.WriteLine($"Transliterated: {transliterated}");
        }
    }
}