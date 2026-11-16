using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using Curiosus.RequestProcessing.Workers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.RequestProcessing.UnitTests;

public class RequestProcessorOptionsTests
{
    [Fact]
    public async Task AddRequestProcessor_ValidConfiguration_BindsOptionsAndStarts()
    {
        using var host = BuildHost<RequestProcessorNodeOptions>(new Dictionary<string, string?>
        {
            ["RequestProcessor:Name"] = "node-1",
            ["RequestProcessor:WorkersCount"] = "2",
            ["RequestProcessor:EventsPeriodicCheckSec"] = "5",
        });

        await host.StartAsync();
        try
        {
            var options = host.Services.GetRequiredService<IOptions<RequestProcessorNodeOptions>>().Value;
            options.Name.Should().Be("node-1");
            options.WorkersCount.Should().Be(2);
            options.EventsPeriodicCheckSec.Should().Be(5);
            options.StateFlushPeriodSec.Should().Be(60);

            host.Services.GetRequiredService<RequestProcessorNodeOptions>().Should().BeSameAs(options);
            host.Services.GetRequiredService<TestBootstrapper<RequestProcessorNodeOptions>>().Options.Should().BeSameAs(options);
        }
        finally
        {
            await host.StopAsync();
        }
    }

    [Fact]
    public async Task AddRequestProcessor_InvalidConfiguration_FailsOnStart()
    {
        using var host = BuildHost<RequestProcessorNodeOptions>(new Dictionary<string, string?>
        {
            ["RequestProcessor:Name"] = " ",
            ["RequestProcessor:WorkersCount"] = "0",
        });

        var act = () => host.StartAsync();

        var exception = (await act.Should().ThrowAsync<OptionsValidationException>()).Which;
        exception.Failures.Should().Contain(f => f.Contains(nameof(RequestProcessorNodeOptions.Name)));
        exception.Failures.Should().Contain(f => f.Contains(nameof(RequestProcessorNodeOptions.WorkersCount)));
    }

    [Fact]
    public void AddRequestProcessor_ConfigureDelegate_AppliesIt()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddRequestProcessor<
            TestRequest,
            TestWorker<RequestProcessorNodeOptions>,
            WorkerBasicExtraParams,
            TestBootstrapper<RequestProcessorNodeOptions>,
            RequestProcessorNodeOptions,
            TestDispatcher<RequestProcessorNodeOptions>,
            TestProcessingInfo>(o =>
        {
            o.Name = "node";
            o.WorkersCount = 3;
        });
        using var provider = services.BuildServiceProvider();

        provider.GetRequiredService<RequestProcessorNodeOptions>().WorkersCount.Should().Be(3);
        provider.GetRequiredService<TestWorker<RequestProcessorNodeOptions>>().Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(RequestProcessorNodeOptions.Name), null)]
    [InlineData(nameof(RequestProcessorNodeOptions.Name), "")]
    [InlineData(nameof(RequestProcessorNodeOptions.WorkersCount), 0)]
    [InlineData(nameof(RequestProcessorNodeOptions.WorkersCount), -1)]
    [InlineData(nameof(RequestProcessorNodeOptions.EventsPeriodicCheckSec), 0)]
    [InlineData(nameof(RequestProcessorNodeOptions.StateFlushPeriodSec), 0)]
    public void Validate_InvalidValue_FailsWithPropertyName(string property, object? value)
    {
        var options = ValidOptions();
        typeof(RequestProcessorNodeOptions).GetProperty(property)!.SetValue(options, value);

        var result = Validate(options);

        result.Failed.Should().BeTrue();
        result.FailureMessage.Should().Contain(property);
    }

    [Fact]
    public void Validate_ValidOptions_Succeeds()
    {
        Validate(ValidOptions()).Succeeded.Should().BeTrue();
    }

    [Fact]
    public async Task AddRequestProcessor_Subclass_ValidatesInheritedNestedAndOwnRules()
    {
        using var host = BuildHost<AppNodeOptions>(
            new Dictionary<string, string?>
            {
                ["RequestProcessor:Name"] = "node",
                ["RequestProcessor:WorkersCount"] = "0",
                ["RequestProcessor:BatchSize"] = "0",
                ["RequestProcessor:Storage:ConnectionString"] = "",
                ["RequestProcessor:MaxBatchSize"] = "10",
            },
            services => services.AddSingleton<IValidateOptions<AppNodeOptions>, AppNodeOptionsValidator>());

        var act = () => host.StartAsync();

        var exception = (await act.Should().ThrowAsync<OptionsValidationException>()).Which;
        var failures = String.Join(Environment.NewLine, exception.Failures);
        failures.Should().Contain(nameof(RequestProcessorNodeOptions.WorkersCount));
        failures.Should().Contain(nameof(AppNodeOptions.BatchSize));
        failures.Should().Contain("'AppNodeOptions.Storage' members: 'ConnectionString'");
        failures.Should().Contain("must be at most WorkersCount");
    }

    [Fact]
    public async Task AddRequestProcessor_SubclassTwoLevelsDeep_RunsParentValidatorThroughDerivedOne()
    {
        using var host = BuildHost<DerivedAppNodeOptions>(
            new Dictionary<string, string?>
            {
                ["RequestProcessor:Name"] = "node",
                ["RequestProcessor:WorkersCount"] = "1",
                ["RequestProcessor:Storage:ConnectionString"] = "Host=db",
                ["RequestProcessor:MaxBatchSize"] = "10",
                ["RequestProcessor:Priority"] = "0",
            },
            services => services.AddSingleton<IValidateOptions<DerivedAppNodeOptions>, DerivedAppNodeOptionsValidator>());

        var act = () => host.StartAsync();

        var failures = String.Join(Environment.NewLine,
            (await act.Should().ThrowAsync<OptionsValidationException>()).Which.Failures);
        failures.Should().Contain("must be at most WorkersCount");
        failures.Should().Contain(nameof(DerivedAppNodeOptions.Priority));
    }

    private static RequestProcessorNodeOptions ValidOptions() => new() { Name = "node", WorkersCount = 1 };

    private static ValidateOptionsResult Validate(RequestProcessorNodeOptions options)
        => new DataAnnotationValidateOptions<RequestProcessorNodeOptions>(Options.DefaultName)
            .Validate(Options.DefaultName, options);

    private static IHost BuildHost<TOptions>(
        IDictionary<string, string?> configuration,
        Action<IServiceCollection>? configureServices = null)
        where TOptions : RequestProcessorNodeOptions
    {
        var builder = Host.CreateEmptyApplicationBuilder(new HostApplicationBuilderSettings());
        builder.Configuration.AddInMemoryCollection(configuration);
        builder.Services.AddLogging();
        builder.Services
            .AddRequestProcessor<
                TestRequest,
                TestWorker<TOptions>,
                WorkerBasicExtraParams,
                TestBootstrapper<TOptions>,
                TOptions,
                TestDispatcher<TOptions>,
                TestProcessingInfo>()
            .BindConfiguration("RequestProcessor");
        configureServices?.Invoke(builder.Services);

        return builder.Build();
    }

    /// <summary>
    /// Options of an app: own rules, nested options and a cross-field rule in its own validator.
    /// </summary>
    public class AppNodeOptions : RequestProcessorNodeOptions
    {
        [Range(1, 1000)]
        public int BatchSize { get; set; } = 10;

        public int MaxBatchSize { get; set; } = 1;

        [Required]
        [ValidateObjectMembers]
        public StorageOptions Storage { get; set; } = new();
    }

    public class StorageOptions
    {
        [Required]
        public string ConnectionString { get; set; } = null!;
    }

    public class AppNodeOptionsValidator : IValidateOptions<AppNodeOptions>
    {
        public ValidateOptionsResult Validate(string? name, AppNodeOptions options)
        {
            var builder = new ValidateOptionsResultBuilder();
            if (options.MaxBatchSize > options.WorkersCount)
                builder.AddError("must be at most WorkersCount", nameof(options.MaxBatchSize));

            return builder.Build();
        }
    }

    public class DerivedAppNodeOptions : AppNodeOptions
    {
        [Range(1, 10)]
        public int Priority { get; set; } = 1;
    }

    public class DerivedAppNodeOptionsValidator : IValidateOptions<DerivedAppNodeOptions>
    {
        private readonly AppNodeOptionsValidator _parent = new();

        public ValidateOptionsResult Validate(string? name, DerivedAppNodeOptions options)
        {
            var builder = new ValidateOptionsResultBuilder();
            builder.AddResult(_parent.Validate(name, options));

            return builder.Build();
        }
    }
}
