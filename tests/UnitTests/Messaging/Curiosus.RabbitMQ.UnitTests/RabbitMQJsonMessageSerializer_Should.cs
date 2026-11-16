using System;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Curiosus.RabbitMQ.UnitTests;

public class RabbitMQJsonMessageSerializer_Should
{
    public record OrderPaid(long OrderId, decimal Amount, string Comment);

    private readonly RabbitMQJsonMessageSerializer _serializer = new();

    [Fact]
    public void HaveJsonContentType()
    {
        _serializer.ContentType.Should().Be("application/json");
    }

    [Theory]
    [InlineData("application/json")]
    [InlineData("application/json; charset=utf-8")]
    [InlineData("Application/JSON")]
    [InlineData(null)]
    public void RoundTrip(string? contentType)
    {
        var message = new OrderPaid(42, 10.5m, "срочно");

        var body = _serializer.Serialize(message);
        var result = _serializer.Deserialize<OrderPaid>(body, contentType);

        result.Should().Be(message);
    }

    [Fact]
    public void WriteWireFormatOfRpcClient()
    {
        var body = _serializer.Serialize(new OrderPaid(42, 10.5m, "срочно"));

        Encoding.UTF8.GetString(body.Span).Should().Be("{\"OrderId\":42,\"Amount\":10.5,\"Comment\":\"срочно\"}");
    }

    [Theory]
    [InlineData("text/plain")]
    [InlineData("application/xml")]
    [InlineData("application/jsonx")]
    public void ThrowOnUnsupportedContentType(string contentType)
    {
        var body = _serializer.Serialize(new OrderPaid(1, 1m, ""));

        var act = () => _serializer.Deserialize<OrderPaid>(body, contentType);

        act.Should().Throw<NotSupportedException>().WithMessage($"*{contentType}*");
    }
}
