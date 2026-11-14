#nullable enable
using FluentAssertions;
using Xunit;

namespace Curiosus.Hosting.UnitTests;

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
    [InlineData("SshPrivateKeyPassphrase")]
    [InlineData("ReCaptchaServerKey")]
    // app-style names
    [InlineData("ApiToken")]
    [InlineData("BotToken")]
    [InlineData("Auth:Key")]
    [InlineData("ClientSecret")]
    [InlineData("DbPwd")]
    [InlineData("password")]
    [InlineData("AwsCredentials")]
    [InlineData("PrivateKeyPem")]
    [InlineData("SigningKeys")]
    [InlineData("WebhookSignature")]
    [InlineData("PasswordSalt")]
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
    [InlineData("SshPrivateKeyPath")]
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
    [InlineData("Host=db;Database=app;AccountKey=s3cr3t!")]
    [InlineData("Host=db;Database=app;SharedAccessKey=s3cr3t!;ApiToken=s3cr3t!")]
    [InlineData("Host=db;Database=app;Sig=s3cr3t!;SomeUnknownKey=s3cr3t!")]
    public void MaskConnectionString_HidesPasswordOnly(string connectionString)
    {
        var masked = SecretMasker.MaskConnectionString(connectionString);

        masked.Should().NotContain("s3cr3t").And.Contain(SecretMasker.Mask).And.Contain("db").And.Contain("app");
    }

    [Fact]
    public void MaskConnectionString_PublicKeys_Kept()
    {
        var masked = SecretMasker.MaskConnectionString("Host=db;Port=5432;Database=app;Username=app;Password=s3cr3t");

        masked.Should().Be("host=db;port=5432;database=app;username=app;password=***");
    }

    [Theory]
    [InlineData("not a connection string s3cr3t")]
    [InlineData("amqp://user:s3cr3t@db/vhost")]
    [InlineData("amqp://user:s3cr3t@db/vhost?heartbeat=10")]
    [InlineData("postgres://user:s3cr3t@db/app?sslmode=require&x=y")]
    [InlineData("Host=amqps://u:s3cr3t@h/")]
    [InlineData("Server=u:s3cr3t@h;Database=app")]
    public void MaskConnectionString_UnparsableOrUri_FullyMasked(string connectionString)
    {
        SecretMasker.MaskConnectionString(connectionString).Should().Be(SecretMasker.Mask);
    }

    [Theory]
    [InlineData("amqp://user:s3cr3t@rabbit/vhost")]
    [InlineData("https://key:s3cr3t@sentry.example.com/1")]
    [InlineData("see redis://:s3cr3t@cache:6379")]
    public void HasUriCredentials_UriWithUserInfo_True(string value)
    {
        SecretMasker.HasUriCredentials(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/user@domain")]
    [InlineData("admin@example.com")]
    public void HasUriCredentials_NoUserInfo_False(string value)
    {
        SecretMasker.HasUriCredentials(value).Should().BeFalse();
    }
}
