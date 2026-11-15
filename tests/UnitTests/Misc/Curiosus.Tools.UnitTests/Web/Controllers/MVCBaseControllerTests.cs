using System;
using System.Linq;
using Curiosus.Tools.Web.Controllers;
using Curiosus.Tools.Web.Resources;
using FluentAssertions;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.ActionConstraints;
using Microsoft.AspNetCore.Mvc.ApplicationParts;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Testing;
using Moq;
using Xunit;

namespace Curiosus.Tools.UnitTests.Web.Controllers;

public sealed class MVCBaseControllerTests
{
    private readonly FakeLogCollector _collector = new();

    private OrdersTestController CreateController(Exception? error = null, bool ajax = false)
    {
        // only the standard logging: no non-generic ILogger, as with Curiosus.Hosting 4.0
        var services = new ServiceCollection()
            .AddLogging(logging => logging.AddProvider(new FakeLoggerProvider(_collector)))
            .BuildServiceProvider();

        var httpContext = new DefaultHttpContext { RequestServices = services };
        if (ajax) httpContext.Request.Headers["X-Requested-With"] = "XMLHttpRequest";
        if (error != null)
            httpContext.Features.Set<IExceptionHandlerPathFeature>(new ExceptionHandlerFeature { Error = error, Path = "/orders" });

        var actionContext = new ActionContext(httpContext, new RouteData(), new ControllerActionDescriptor());
        return new OrdersTestController
        {
            ControllerContext = new ControllerContext(actionContext),
            TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>()),
            Url = new UrlHelper(actionContext),
        };
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("/\\evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("")]
    [InlineData(null)]
    public void RedirectInternal_ExternalOrEmpty_GoesHome(string? returnUrl)
    {
        var result = CreateController().GoBack(returnUrl);

        result.Should().BeOfType<RedirectToActionResult>()
            .Which.Should().Match<RedirectToActionResult>(r => r.ActionName == "Index" && r.ControllerName == "Home");
    }

    [Theory]
    [InlineData("/orders/5?x=1", "/orders/5?x=1")]
    [InlineData("~/orders", "~/orders")]
    [InlineData("/каталог", "/%D0%BA%D0%B0%D1%82%D0%B0%D0%BB%D0%BE%D0%B3")]
    public void RedirectInternal_Local_RedirectsThere(string returnUrl, string expected)
    {
        CreateController().GoBack(returnUrl).Should().BeOfType<LocalRedirectResult>()
            .Which.Url.Should().Be(expected);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void ExceptionMessage_NeverReachesTheClient(bool ajax)
    {
        var controller = CreateController(new InvalidOperationException("<script>alert(1)</script> from SQL"), ajax);

        var result = controller.HandleError();

        ((string)controller.ViewBag.ErrorText).Should().Be(LNG.UnexpectedError);
        if (ajax) result.Should().BeOfType<PartialViewResult>().Which.ViewName.Should().Be("AjaxError");
        else result.Should().BeOfType<ViewResult>().Which.ViewName.Should().Be("Error");

        var record = _collector.LatestRecord;
        record.Level.Should().Be(LogLevel.Error);
        record.Category.Should().Be(typeof(OrdersTestController).FullName);
    }

    [Fact]
    public void AppErrorText_IsShown()
    {
        var controller = CreateController();

        controller.ErrorView("Order not found");

        ((string)controller.ViewBag.ErrorText).Should().Be("Order not found");
    }

    [Fact]
    public void Cancellation_IsLoggedAsInformation()
    {
        CreateController(new OperationCanceledException()).HandleError();

        _collector.LatestRecord.Level.Should().Be(LogLevel.Information);
    }

    [Fact]
    public void DerivedController_ExposesOnlyItsOwnActionsAndTheErrorEndpoint()
    {
        var actions = DiscoverActions().Where(a => a.ControllerTypeInfo == typeof(OrdersTestController)).ToList();

        actions.Select(a => a.ActionName).Should().BeEquivalentTo("Index", "GoBack", "ErrorView");

        var errorView = actions.Single(a => a.ActionName == "ErrorView");
        errorView.Parameters.Should().BeEmpty("errorText must not bind from the query string");
        (errorView.ActionConstraints ?? []).OfType<HttpMethodActionConstraint>()
            .Should().BeEmpty("the exception handler re-executes POST requests too");
    }

    private static ControllerActionDescriptor[] DiscoverActions()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(new System.Diagnostics.DiagnosticListener("tests"));
        services.AddControllers().ConfigureApplicationPartManager(manager =>
        {
            manager.ApplicationParts.Clear();
            manager.ApplicationParts.Add(new AssemblyPart(typeof(OrdersTestController).Assembly));
        });

        using var provider = services.BuildServiceProvider();
        return provider.GetRequiredService<IActionDescriptorCollectionProvider>()
            .ActionDescriptors.Items.OfType<ControllerActionDescriptor>().ToArray();
    }
}

public sealed class OrdersTestController : MVCBaseController
{
    public IActionResult Index() => Content("orders");

    public ActionResult GoBack(string? returnUrl) => RedirectInternal(returnUrl);
}
