using System.Linq;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.Configuration.UnitTests;

public sealed class GetCuriosusOptionsTests
{
    private sealed class SampleOptions
    {
        [Required]
        public string Name { get; set; } = "default";

        [Range(1, 10)]
        public int Count { get; set; } = 1;
    }

    private static IConfiguration Configuration(params (string Key, string Value)[] values) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(values.Select(v => new KeyValuePair<string, string?>(v.Key, v.Value)))
            .Build();

    [Fact]
    public void BindsTheSectionAndKeepsDefaults()
    {
        var options = Configuration(("Sample:Count", "5")).GetCuriosusOptions<SampleOptions>("Sample");

        options.Count.Should().Be(5);
        options.Name.Should().Be("default");
    }

    [Fact]
    public void Configure_RunsAfterBinding()
    {
        var options = Configuration(("Sample:Name", "bound"))
            .GetCuriosusOptions<SampleOptions>("Sample", o => o.Name = "configured");

        options.Name.Should().Be("configured");
    }

    [Fact]
    public void InvalidOptions_Throw()
    {
        var act = () => Configuration(("Sample:Count", "11")).GetCuriosusOptions<SampleOptions>("Sample");

        act.Should().Throw<OptionsValidationException>().Which.Failures.Should().ContainSingle()
            .Which.Should().Contain("Count");
    }
}
