using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Curiosus.Configuration;
using Curiosus.Hosting;
using Curiosus.Hosting.Web;

namespace Curiosus.SampleWebApp
{
    public class Configuration : CuriosusWebAppConfiguration
    {
        public Configuration()
        {
            AppName = "Test App";
        }

        public override IReadOnlyCollection<ConfigurationValidationError> Validate(string? prefix = null)
        {
            Console.WriteLine("demo");
            return base.Validate(prefix);
        }
    }

    public class Program
    {
        public class CliArgs : CuriosusCLIArguments
        {
            public CliArgs() : base("test")
            {
            }
        }

        public static Task<int> Main(string[] args)
        {
            var bootstrapper = new CuriosusWebAppBootstrapper<CliArgs, Configuration, Startup>();

            return bootstrapper.RunAsync(args);
        }
    }
}