using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.DAL.UnitTests;

public class DbOptionsTests
{
    private const string ValidConnectionString = "Host=localhost;Database=app;Username=app;Password=secret";

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EmptyConnectionString_FailsDataAnnotations(string? connectionString)
    {
        var options = new DbOptions { ConnectionString = connectionString! };

        var results = Validate(options);

        results.Should().ContainSingle()
            .Which.MemberNames.Should().Equal(nameof(DbOptions.ConnectionString));
    }

    [Fact]
    public void OnlyConnectionString_IsValid()
    {
        var options = new DbOptions { ConnectionString = ValidConnectionString };

        Validate(options).Should().BeEmpty();
    }

    [Fact]
    public async Task MissingConnectionString_FailsWhenHostStarts()
    {
        using var host = CreateHost(
            new Dictionary<string, string?> { ["Db:IsGlobalLoggingEnabled"] = "true" },
            services => services.AddOptions<DbOptions>()
                .BindConfiguration(DbOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart());

        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Message.Should().Contain(nameof(DbOptions.ConnectionString));
    }

    [Fact]
    public async Task BoundOptions_StartHost()
    {
        using var host = CreateHost(
            new Dictionary<string, string?>
            {
                ["Db:ConnectionString"] = ValidConnectionString,
                ["Db:ReadOnlyConnectionString"] = "Host=replica;Database=app",
                ["Db:IsSensitiveDataLoggingEnabled"] = "true",
            },
            services => services.AddOptions<DbOptions>()
                .BindConfiguration(DbOptions.SectionName)
                .ValidateDataAnnotations()
                .ValidateOnStart());

        await host.StartAsync();

        var options = host.Services.GetRequiredService<IOptions<DbOptions>>().Value;
        options.ConnectionString.Should().Be(ValidConnectionString);
        options.ReadOnlyConnectionString.Should().Be("Host=replica;Database=app");
        options.IsGlobalLoggingEnabled.Should().BeFalse();
        options.IsSensitiveDataLoggingEnabled.Should().BeTrue();

        await host.StopAsync();
    }

    [Fact]
    public async Task Subclass_InheritsBaseRules()
    {
        using var host = CreateHost(
            new Dictionary<string, string?> { ["Db:UseReadOnlyReplica"] = "false" },
            AddAppDbOptions);

        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Message.Should().Contain(nameof(DbOptions.ConnectionString));
    }

    [Fact]
    public async Task Subclass_RunsItsOwnValidator()
    {
        using var host = CreateHost(
            new Dictionary<string, string?>
            {
                ["Db:ConnectionString"] = ValidConnectionString,
                ["Db:UseReadOnlyReplica"] = "true",
            },
            AddAppDbOptions);

        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Failures.Should().ContainSingle()
            .Which.Should().Contain("Db:ReadOnlyConnectionString");
    }

    [Fact]
    public async Task ValidSubclass_StartsHost()
    {
        using var host = CreateHost(
            new Dictionary<string, string?>
            {
                ["Db:ConnectionString"] = ValidConnectionString,
                ["Db:ReadOnlyConnectionString"] = "Host=replica;Database=app",
                ["Db:UseReadOnlyReplica"] = "true",
                ["Db:CommandTimeoutSec"] = "60",
            },
            AddAppDbOptions);

        await host.StartAsync();

        host.Services.GetRequiredService<IOptions<AppDbOptions>>().Value.CommandTimeoutSec.Should().Be(60);

        await host.StopAsync();
    }

    [Fact]
    public async Task Subclass_RangeAttribute_FailsWhenHostStarts()
    {
        using var host = CreateHost(
            new Dictionary<string, string?>
            {
                ["Db:ConnectionString"] = ValidConnectionString,
                ["Db:CommandTimeoutSec"] = "0",
            },
            AddAppDbOptions);

        var act = () => host.StartAsync();

        (await act.Should().ThrowAsync<OptionsValidationException>())
            .Which.Message.Should().Contain(nameof(AppDbOptions.CommandTimeoutSec));
    }

    private static List<ValidationResult> Validate(object options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results;
    }

    private static IHost CreateHost(Dictionary<string, string?> configuration, Action<IServiceCollection> configureServices)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(configuration);
        configureServices(builder.Services);
        return builder.Build();
    }

    // The pattern of the migration guide: an app subclass of DbOptions with its own rules.
    private static void AddAppDbOptions(IServiceCollection services)
    {
        services.AddOptions<AppDbOptions>()
            .BindConfiguration(DbOptions.SectionName)
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<AppDbOptions>, AppDbOptionsValidator>();
    }

    public class AppDbOptions : DbOptions
    {
        [Range(1, 3600)]
        public int CommandTimeoutSec { get; set; } = 30;

        public bool UseReadOnlyReplica { get; set; }
    }

    public class AppDbOptionsValidator : IValidateOptions<AppDbOptions>
    {
        public ValidateOptionsResult Validate(string? name, AppDbOptions options)
        {
            var builder = new ValidateOptionsResultBuilder();
            if (options.UseReadOnlyReplica && String.IsNullOrWhiteSpace(options.ReadOnlyConnectionString))
            {
                builder.AddError("is required when UseReadOnlyReplica is true", "Db:ReadOnlyConnectionString");
            }

            return builder.Build();
        }
    }
}
