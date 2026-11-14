using System;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Npgsql;
using Xunit;

namespace Curiosus.RequestProcessing.Postgres.UnitTests;

public class ChannelNameTests
{
    [Fact]
    public void BuildListenSql_InjectionAttempt_SingleQuotedIdentifier()
    {
        var sql = DbEventReceiver.BuildListenSql(["a\"b; select 1"]);

        sql.Should().Be("LISTEN \"a\"\"b; select 1\";");
    }

    [Fact]
    public void BuildListenSql_SeveralChannels_EachQuotedAsWritten()
    {
        var sql = DbEventReceiver.BuildListenSql(["requests", "MixedCase"]);

        sql.Should().Be("LISTEN \"requests\";LISTEN \"MixedCase\";");
    }

    [Theory]
    [InlineData("requests")]
    [InlineData("MixedCase")]
    [InlineData("a\"b; select 1")]
    [InlineData("канал")]
    public void Validate_AllowedNames_NoError(string name)
    {
        PgChannelName.Validate(name).Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("a\0b")]
    public void Validate_EmptyWhitespaceOrNul_Error(string name)
    {
        PgChannelName.Validate(name).Should().NotBeNull();
    }

    [Fact]
    public void Validate_63Bytes_NoError_64Bytes_Error()
    {
        PgChannelName.Validate(new string('a', 63)).Should().BeNull();
        PgChannelName.Validate(new string('a', 64)).Should().NotBeNull();
        // 32 Cyrillic letters are 64 bytes in UTF-8
        PgChannelName.Validate(new string('я', 32)).Should().NotBeNull();
    }

    [Fact]
    public void OptionsValidate_InvalidNames_ErrorPerName()
    {
        var options = new PostgresEventReceiverOptions { EventNames = ["ok", "", " ", new string('a', 64), "a\0b"] };

        var errors = options.Validate("Receiver");

        errors.Should().HaveCount(4);
        errors.Select(e => e.FieldName).Should().AllSatisfy(f => f.Should().StartWith("Receiver:EventNames"));
    }

    [Fact]
    public void OptionsValidate_ValidNames_NoErrors()
    {
        var options = new PostgresEventReceiverOptions { EventNames = ["requests", "MixedCase"] };

        options.Validate().Should().BeEmpty();
    }

    [Fact]
    public async Task NotifyAsync_InvalidChannel_ThrowsBeforeConnecting()
    {
        await using var dataSource = NpgsqlDataSource.Create("Host=localhost;Port=1");

        var act = () => dataSource.NotifyAsync(" ");

        await act.Should().ThrowAsync<ArgumentException>();
    }
}
