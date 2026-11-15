using System;
using System.Threading;
using Curiosus.Tools.Performance;
using Microsoft.Extensions.Logging;

namespace Curiosus.Tools.Sample
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine($"\nResult of {nameof(SensitiveDataProtectorSample)}:");
            SensitiveDataProtectorSample.Run();
            
            Console.WriteLine($"\nResult of {nameof(TransliterationSample)}:");
            TransliterationSample.Run();
            
            Console.WriteLine($"\nResult of {nameof(PhoneHelperSample)}:");
            PhoneHelperSample.Run();


            using var loggerFactory = LoggerFactory.Create(logging => logging.AddConsole());
            var performance = new PerformanceMeasurer(loggerFactory);
            using (performance.Measure("Test"))
            {
                Thread.Sleep(TimeSpan.FromSeconds(1));
            }

            StuckCodeManager.Initialize(loggerFactory.CreateLogger("stuckcode"));
            using (StuckCodeManager.Enter("Stuck code", 1))
            {
                Thread.Sleep(TimeSpan.FromSeconds(2));
            }
            StuckCodeManager.Shutdown();
        }
    }
}