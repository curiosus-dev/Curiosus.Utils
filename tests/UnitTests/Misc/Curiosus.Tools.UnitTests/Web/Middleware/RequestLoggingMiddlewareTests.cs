using System.Threading.Tasks;
using Curiosus.Tools.Web.Middleware;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Curiosus.Tools.UnitTests.Web.Middleware;

public sealed class RequestLoggingMiddlewareTests
{
    [Fact]
    public void Activation_NeedsOnlyTheStandardLogging()
    {
        // Curiosus.Hosting 4.0 no longer registers a non-generic ILogger
        using var services = new ServiceCollection().AddLogging().BuildServiceProvider();

        var act = () => ActivatorUtilities.CreateInstance<RequestLoggingMiddleware>(
            services, (RequestDelegate)(_ => Task.CompletedTask));

        act.Should().NotThrow();
    }
}
