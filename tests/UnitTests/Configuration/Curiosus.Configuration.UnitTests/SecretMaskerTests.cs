using FluentAssertions;
using Xunit;

namespace Curiosus.Configuration.UnitTests;

public class SecretMaskerTests
{
    [Theory]
    // library options
    [InlineData("SmtpPassword")]
    [InlineData("Password")]
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
    [InlineData("https://proxy.example.com/?target=amqp%3A%2F%2Fapp%3As3cr3t%40rabbit")]
    [InlineData("https://api.example.com/v1?token=s3cr3t")]
    [InlineData("https://store.blob.core.windows.net/c?sv=2024-01-01&sig=s3cr3t")]
    [InlineData("https://fn.azurewebsites.net/api/f?code=s3cr3t")]
    [InlineData("https://maps.example.com/geo?q=x&key=s3cr3t")]
    [InlineData("https://hooks.slack.com/services/T000/B000/s3cr3t")]
    [InlineData("https://discord.com/api/webhooks/123/s3cr3t")]
    [InlineData("https://api.telegram.org/bot123456:s3cr3t/sendMessage")]
    [InlineData("Host=db;Database=app;Password=s3cr3t")]
    [InlineData("Server=db;User Id=app; Pwd = s3cr3t")]
    [InlineData("cache:6379,password=s3cr3t,ssl=true")]
    [InlineData("Password=\"s3cr3t;x\"")]
    [InlineData("AccountName=acc;AccountKey=s3cr3t")]
    [InlineData("Endpoint=sb://x/;SharedAccessKey=s3cr3t")]
    public void HasSecretInValue_SecretInValue_True(string value)
    {
        SecretMasker.HasSecretInValue(value).Should().BeTrue();
    }

    [Theory]
    [InlineData("https://example.com/path")]
    [InlineData("https://example.com/user@domain")]
    [InlineData("admin@example.com")]
    [InlineData("https://example.com/search?q=token&page=2")]
    [InlineData("https://example.com/?keyword=x")]
    [InlineData("Host=db;Port=5432;Database=app;Username=app")]
    [InlineData("Host=db;Password=")]
    [InlineData("tokenizer=simple;secretary=x")]
    public void HasSecretInValue_NoSecret_False(string value)
    {
        SecretMasker.HasSecretInValue(value).Should().BeFalse();
    }

    [Fact]
    public void HasSecretInValue_UriObjects()
    {
        SecretMasker.HasSecretInValue((object)new System.Uri("amqp://app:s3cr3t@rabbit/vhost")).Should().BeTrue();
        SecretMasker.HasSecretInValue((object)new System.Uri("https://public.example.com")).Should().BeFalse();
        SecretMasker.HasSecretInValue((object)new System.Uri("https://proxy.example.com/?target=amqp://app:s3cr3t@rabbit"))
            .Should().BeTrue();
        SecretMasker.HasSecretInValue((object)new System.Uri("https://proxy.example.com/#amqp://app:s3cr3t@rabbit"))
            .Should().BeTrue();
        SecretMasker.HasSecretInValue(
                (object)new System.Uri("https://proxy.example.com/?target=amqp%3A%2F%2Fapp%3As3cr3t%40rabbit"))
            .Should().BeTrue();
        SecretMasker.HasSecretInValue((object)42).Should().BeFalse();
        SecretMasker.HasSecretInValue((object?)null).Should().BeFalse();
    }

    [Theory]
    [InlineData("Db:Dsn")]
    [InlineData("Cache:Configuration")]
    public void MaskValue_KeyValueSecretUnderNonSecretName_Masked(string path)
    {
        SecretMasker.MaskValue(path, "cache:6379,password=s3cr3t").Should().Be(SecretMasker.Mask);
    }
}
