using System.Collections.Generic;
using FluentAssertions;
using Xunit;

namespace Curiosus.Configuration.UnitTests;

public class ConfigurationPrinterTests
{
    private const string Secret = "s3cr3t";

    private readonly ConfigurationPrinter _printer = new();

    [Fact]
    public void GetLog_SecretProperties_Masked()
    {
        var options = new SecretOptions();

        var log = _printer.GetLog(options);

        log.Should().NotContain(Secret);
        log.Should().Contain("smtp.example.com")
            .And.ContainEquivalentOf("Host=db")
            .And.ContainEquivalentOf("Database=app")
            .And.ContainEquivalentOf("Password=***");
    }

    [Fact]
    public void GetLog_NestedOptions_Masked()
    {
        var options = new ParentOptions();

        var log = _printer.GetLog(options);

        log.Should().NotContain(Secret).And.Contain("smtp.example.com");
    }

    [Fact]
    public void GetLog_Indexer_Skipped()
    {
        var options = new IndexerOptions();

        var log = _printer.GetLog(options);

        log.Should().Contain("Name").And.NotContain("Item");
    }

    [Fact]
    public void GetLog_EmptyArrayElement_DoesNotThrow()
    {
        var options = new ArrayOptions();

        var log = _printer.GetLog(options);

        log.Should().Contain("Items");
    }

    [Fact]
    public void GetLog_Dictionary_ReadableAndMasked()
    {
        var options = new DictionaryOptions();

        var log = _printer.GetLog(options);

        log.Should().Contain("{Region=eu, Auth:Key=***}").And.NotContain(Secret);
    }

    [Fact]
    public void GetLog_NullSecret_PrintedAsNull()
    {
        var options = new SecretOptions { Password = null };

        var log = _printer.GetLog(options);

        log.Should().MatchRegex(@"Password: null");
    }

    private class SecretOptions : ILoggableOptions
    {
        public string Host { get; set; } = "smtp.example.com";

        public string? Password { get; set; } = Secret;

        public string ApiKey { get; set; } = Secret;

        public string BotToken { get; set; } = Secret;

        public string[] Tokens { get; set; } = [Secret];

        public string ConnectionString { get; set; } = $"Host=db;Database=app;Password={Secret}";
    }

    private class ParentOptions : ILoggableOptions
    {
        public SecretOptions Smtp { get; set; } = new();

        public ILoggableOptions[] Children { get; set; } = [new SecretOptions()];
    }

    private class IndexerOptions : ILoggableOptions
    {
        public string Name { get; set; } = "name";

        public string this[int index] => index.ToString();
    }

    private class ArrayOptions : ILoggableOptions
    {
        public ILoggableOptions[] Items { get; set; } = [new EmptyOptions(), new EmptyOptions()];
    }

    private class EmptyOptions : ILoggableOptions
    {
    }

    private class DictionaryOptions : ILoggableOptions
    {
        public Dictionary<string, string> Settings { get; set; } = new()
        {
            ["Region"] = "eu",
            ["Auth:Key"] = Secret,
        };
    }
}
