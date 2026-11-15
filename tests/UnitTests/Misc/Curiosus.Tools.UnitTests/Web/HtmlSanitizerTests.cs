using System;
using System.Linq;
using System.Net;
using AngleSharp.Html.Parser;
using Curiosus.Tools.Web;
using FluentAssertions;
using Ganss.Xss;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Curiosus.Tools.UnitTests.Web;

public sealed class HtmlSanitizerTests
{
    private static IHtmlSanitizer Create(System.Action<HtmlSanitizerOptions>? configure = null) =>
        new ServiceCollection().AddCuriosusHtmlSanitizer(configure).BuildServiceProvider()
            .GetRequiredService<IHtmlSanitizer>();

    [Theory]
    [InlineData("<p>&lt;script&gt;alert(document.cookie)&lt;/script&gt;</p>")]
    [InlineData("<p>&lt;img src=x onerror=alert(1)&gt;</p>")]
    [InlineData("<p>&lt;iframe src=//evil.example&gt;&lt;/iframe&gt;</p>")]
    [InlineData("<p>&amp;lt;script&amp;gt;alert(1)&amp;lt;/script&amp;gt;</p>")]
    [InlineData("<script>alert(1)</script><p>text</p>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<a href=\"javascript:alert(1)\">x</a>")]
    [InlineData("<a href=\"JaVaScRiPt:alert(1)\">x</a>")]
    public void EncodedOrLiveScript_NeverBecomesExecutableMarkup(string input)
    {
        var output = Create().Sanitize(input);

        // parsed as a browser would: no script-capable elements, no event handlers, no javascript: links
        var document = new HtmlParser().ParseDocument($"<body>{output}</body>");
        document.QuerySelectorAll("script, iframe, object, embed").Should().BeEmpty(output);
        document.All.SelectMany(e => e.Attributes).Should()
            .NotContain(a => a.Name.StartsWith("on", StringComparison.OrdinalIgnoreCase), output);
        document.QuerySelectorAll("[href]").Select(e => e.GetAttribute("href")!).Should()
            .NotContain(h => h.TrimStart().StartsWith("javascript:", StringComparison.OrdinalIgnoreCase), output);
    }

    [Fact]
    public void EntitiesStayEncoded()
    {
        Create().Sanitize("<p>&lt;b&gt; &amp; &nbsp;</p>").Should().Be("<p>&lt;b&gt; &amp; &nbsp;</p>");
    }

    [Fact]
    public void OldDecodeAfterSanitize_WasExploitable()
    {
        // what HtmlSanitizerHelper did before 4.0: the reason it is gone
        var decoded = WebUtility.HtmlDecode(new HtmlSanitizer().Sanitize("<p>&lt;script&gt;alert(1)&lt;/script&gt;</p>"));

        decoded.Should().Contain("<script>");
    }

    [Theory]
    [InlineData("<audio controls=\"\" preload=\"none\"><source src=\"https://example.com/a.mp3\"></audio>")]
    [InlineData("<video controls=\"\" muted=\"\" loop=\"\" autoplay=\"\"><source src=\"https://example.com/v.mp4\"></video>")]
    [InlineData("<a href=\"mailto:info@example.com\">mail</a>")]
    [InlineData("<a href=\"tel:+79000000000\">call</a>")]
    public void ExtendedAllowList_Survives(string input)
    {
        Create().Sanitize(input).Should().Be(input);
    }

    [Fact]
    public void IdClassAndEventHandlers_AreStrippedByDefault()
    {
        Create().Sanitize("<p id=\"config\" class=\"overlay\" onclick=\"alert(1)\">x</p>")
            .Should().Be("<p>x</p>");
    }

    [Fact]
    public void Configure_CanAllowClassAgain()
    {
        Create(o => o.AllowedAttributes.Add("class")).Sanitize("<p class=\"note\">x</p>")
            .Should().Be("<p class=\"note\">x</p>");
    }
}
