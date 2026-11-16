#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Curiosus.Hosting.TempFiles;
using Curiosus.Tools.TempFiles;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.Hosting.UnitTests
{
    public sealed class AddTempDirCleanerTests
    {
        [Fact]
        public async Task Cleaner_DeletesStaleFiles_KeepsFreshOnes()
        {
            using var dir = new TempDirectory();
            var stale = dir.Write("stale.tmp", "x");
            var fresh = dir.Write("fresh.tmp", "x");
            File.SetLastAccessTime(stale, DateTime.Now.AddHours(-2));

            var builder = CreateBuilder(("TempFiles:TempPath", dir.Path), ("TempFiles:FileTtlHours", "1"));
            builder.Services.AddTempDirCleaner();

            using var host = builder.Build();
            await host.StartAsync();
            try
            {
                for (var i = 0; i < 100 && File.Exists(stale); i++)
                    await Task.Delay(50);

                File.Exists(stale).Should().BeFalse();
                File.Exists(fresh).Should().BeTrue();
            }
            finally
            {
                await host.StopAsync();
            }
        }

        [Theory]
        [InlineData("TempPath", "")]
        [InlineData("CleaningFrequencyHours", "0")]
        [InlineData("FileTtlHours", "-1")]
        public async Task InvalidOptions_FailTheStart(string property, string value)
        {
            var builder = CreateBuilder(($"TempFiles:{property}", value));
            builder.Services.AddTempDirCleaner();

            using var host = builder.Build();
            var act = () => host.StartAsync();

            (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Message.Should().Contain(property);
        }

        [Fact]
        public void CustomSection_IsBound_AndConfigureOverridesIt()
        {
            using var provider = BuildProvider(
                s => s.AddTempDirCleaner("App:Temp", o => o.FileTtlHours = 5),
                ("App:Temp:TempPath", "/data/tmp"),
                ("App:Temp:FileTtlHours", "48"));

            var options = provider.GetRequiredService<IOptions<TempFileOptions>>().Value;
            options.TempPath.Should().Be("/data/tmp");
            options.FileTtlHours.Should().Be(5);
        }

        [Fact]
        public void WithTempFileServices_SharesTheOptions_BindsAndValidatesOnce()
        {
            using var provider = BuildProvider(
                s =>
                {
                    s.AddTempFileServices();
                    s.AddTempDirCleaner();
                    s.Count(d => d.ServiceType == typeof(IConfigureOptions<TempFileOptions>)).Should().Be(1);
                },
                ("TempFiles:FileTtlHours", "0"));

            var act = () => provider.GetRequiredService<IOptions<TempFileOptions>>().Value;

            act.Should().Throw<OptionsValidationException>().Which.Failures.Should().ContainSingle();
        }

        private static ServiceProvider BuildProvider(
            Action<IServiceCollection> register,
            params (string Key, string Value)[] values)
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddSingleton<IConfiguration>(Configuration(values));
            register(services);
            return services.BuildServiceProvider();
        }

        private static IConfigurationRoot Configuration(params (string Key, string Value)[] values) =>
            new ConfigurationBuilder()
                .AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)))
                .Build();

        private static HostApplicationBuilder CreateBuilder(params (string Key, string Value)[] values)
        {
            var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
            builder.Configuration.AddInMemoryCollection(values.Select(x => new KeyValuePair<string, string?>(x.Key, x.Value)));
            return builder;
        }
    }
}
