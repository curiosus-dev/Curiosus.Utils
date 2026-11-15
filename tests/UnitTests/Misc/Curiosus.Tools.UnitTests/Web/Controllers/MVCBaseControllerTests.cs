using System;
using Curiosus.Tools.Web.Controllers;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Xunit;

namespace Curiosus.Tools.UnitTests.Web.Controllers;

public sealed class MVCBaseControllerTests
{
    private readonly FakeLogCollector _collector = new();

    private TestController CreateController()
    {
        var services = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(new FakeLoggerProvider(_collector)))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        return new TestController
        {
            ControllerContext = new ControllerContext { HttpContext = httpContext },
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
        };
    }

    [Fact]
    public void ErrorView_LogsWithTheCategoryOfTheActualController()
    {
        var result = CreateController().ErrorView("Something went wrong");

        result.Should().BeOfType<ViewResult>();
        var record = _collector.LatestRecord;
        record.Category.Should().Be(typeof(TestController).FullName!.Replace("+", "."));
        record.Message.Should().Be("Something went wrong");
    }

    [Fact]
    [Obsolete("Tests the obsolete overload")]
    public void ObsoleteOverloadWithALogger_StillWorks()
    {
        var result = CreateController().ErrorView(NullLoggerForTests.Instance, "Old call");

        result.Should().BeOfType<ViewResult>();
        _collector.LatestRecord.Category.Should().Be(typeof(TestController).FullName!.Replace("+", "."));
    }

    private sealed class TestController : MVCBaseController
    {
    }

    private static class NullLoggerForTests
    {
        public static ILogger Instance { get; } = Microsoft.Extensions.Logging.Abstractions.NullLogger.Instance;
    }
}
