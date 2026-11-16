using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using FluentAssertions;
using Xunit;

namespace Curiosus.Tools.UnitTests;

public sealed class CultureOptionsTests
{
    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void EmptyCulture_IsInvalid(string culture)
    {
        var options = new CultureOptions { DefaultCulture = culture };
        var results = new List<ValidationResult>();

        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true)
            .Should().BeFalse();
        results.Should().ContainSingle().Which.MemberNames.Should().Contain(nameof(CultureOptions.DefaultCulture));
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public void SetDefaultCulture_EmptyCulture_Throws(string culture)
    {
        var act = () => ApplicationHelper.SetDefaultCulture(new CultureOptions { DefaultCulture = culture });

        act.Should().Throw<ArgumentException>().WithParameterName(nameof(CultureOptions.DefaultCulture));
    }
}
