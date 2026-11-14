using FluentAssertions;
using Xunit;

namespace Curiosus.Configuration.UnitTests;

public class SecretMaskerTests
{
    [Theory]
    // library options
    [InlineData("SmtpPassword")]
    [InlineData("Password")]
    [InlineData("MailgunApiKey")]
    [InlineData("ApiKey")]
    [InlineData("SmscPassword")]
    [InlineData("SshPassword")]
    // app-style names
    [InlineData("ApiToken")]
    [InlineData("BotToken")]
    [InlineData("Auth:Key")]
    [InlineData("ClientSecret")]
    [InlineData("DbPwd")]
    [InlineData("password")]
    public void IsSecretName_SecretNames_True(string name)
    {
        SecretMasker.IsSecretName(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("Host")]
    [InlineData("Port")]
    [InlineData("UserName")]
    [InlineData("KeyPrefix")]
    [InlineData("Timeout")]
    public void IsSecretName_OtherNames_False(string name)
    {
        SecretMasker.IsSecretName(name).Should().BeFalse();
    }

    [Theory]
    [InlineData("ConnectionString")]
    [InlineData("ReadOnlyConnectionString")]
    [InlineData("connectionString")]
    public void IsConnectionStringName_ConnectionStrings_True(string name)
    {
        SecretMasker.IsConnectionStringName(name).Should().BeTrue();
        SecretMasker.IsSecretName(name).Should().BeTrue();
    }

    [Theory]
    [InlineData("Host=db;Port=5432;Database=app;Username=app;Password=s3cr3t!")]
    [InlineData("Server=db;Database=app;User Id=app;Pwd=s3cr3t!")]
    [InlineData("Host=db;Database=app;PASSWORD=\"s3cr3t!;x\"")]
    public void MaskConnectionString_HidesPasswordOnly(string connectionString)
    {
        var masked = SecretMasker.MaskConnectionString(connectionString);

        masked.Should().NotContain("s3cr3t").And.Contain(SecretMasker.Mask).And.Contain("db").And.Contain("app");
    }

    [Fact]
    public void MaskConnectionString_Unparsable_FullyMasked()
    {
        SecretMasker.MaskConnectionString("not a connection string s3cr3t").Should().Be(SecretMasker.Mask);
    }
}
