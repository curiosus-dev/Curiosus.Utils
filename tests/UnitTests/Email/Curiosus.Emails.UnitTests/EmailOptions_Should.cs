#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Curiosus.EMail;
using Curiosus.EMail.Smtp;
using Curiosus.Email.UnisenderGo;
using FluentAssertions;
using MailKit.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Xunit;

namespace Curiosus.Emails.UnitTests
{
    public class EmailOptions_Should
    {
        private static void ValidSmtp(SmtpEMailOptions o)
        {
            o.SmtpServer = "smtp.example.com";
            o.SmtpLogin = "login";
            o.SmtpPassword = "password";
            o.EMailFrom = "noreply@example.com";
            o.SenderName = "Sender";
        }

        private static void ValidUnisenderGo(UnisenderGoEmailOptions o)
        {
            o.ApiKey = "key";
            o.EmailFrom = "noreply@example.com";
            o.FromName = "Example";
        }

        private static OptionsValidationException? Validate<T>(Action<IServiceCollection> register) where T : class
        {
            var services = new ServiceCollection().AddLogging();
            register(services);
            using var provider = services.BuildServiceProvider();
            try
            {
                _ = provider.GetRequiredService<IOptions<T>>().Value;
                return null;
            }
            catch (OptionsValidationException e)
            {
                return e;
            }
        }

        [Fact]
        public void Smtp_BindsConfiguration()
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["Smtp:SmtpServer"] = "smtp.example.com",
                    ["Smtp:SmtpPort"] = "587",
                    ["Smtp:SmtpLogin"] = "login",
                    ["Smtp:SmtpPassword"] = "password",
                    ["Smtp:EMailFrom"] = "noreply@example.com",
                    ["Smtp:SenderName"] = "Sender",
                    ["Smtp:Security"] = "SslOnConnect",
                })
                .Build();
            var services = new ServiceCollection().AddLogging().AddSingleton<IConfiguration>(configuration);
            services.AddSmtpEMailSender().BindConfiguration("Smtp");
            using var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<IOptions<SmtpEMailOptions>>().Value;

            options.SmtpPort.Should().Be(587);
            options.Security.Should().Be(SecureSocketOptions.SslOnConnect);
            provider.GetRequiredService<IEMailSender>().Should().BeOfType<SmtpEMailSender>();
        }

        [Theory]
        [InlineData(nameof(SmtpEMailOptions.SmtpServer))]
        [InlineData(nameof(SmtpEMailOptions.SmtpLogin))]
        [InlineData(nameof(SmtpEMailOptions.SmtpPassword))]
        [InlineData(nameof(SmtpEMailOptions.EMailFrom))]
        [InlineData(nameof(SmtpEMailOptions.SenderName))]
        public void Smtp_RequiresValues(string property)
        {
            var e = Validate<SmtpEMailOptions>(s => s.AddSmtpEMailSender(o =>
            {
                ValidSmtp(o);
                typeof(SmtpEMailOptions).GetProperty(property)!.SetValue(o, " ");
            }));

            e.Should().NotBeNull();
            e!.Failures.Should().ContainSingle().Which.Should().Contain(property);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(65536)]
        public void Smtp_RejectsInvalidPort(int port)
        {
            var e = Validate<SmtpEMailOptions>(s => s.AddSmtpEMailSender(o =>
            {
                ValidSmtp(o);
                o.SmtpPort = port;
            }));

            e.Should().NotBeNull();
            e!.Failures.Should().ContainSingle().Which.Should().Contain(nameof(SmtpEMailOptions.SmtpPort));
        }

        [Fact]
        public void Smtp_RunsTheCrossFieldValidator()
        {
            var e = Validate<SmtpEMailOptions>(s => s.AddSmtpEMailSender(o =>
            {
                ValidSmtp(o);
                o.Security = SecureSocketOptions.Auto;
            }));

            e.Should().NotBeNull();
            e!.Failures.Should().ContainSingle().Which.Should().Contain(nameof(SmtpEMailOptions.Security));
        }

        [Fact]
        public void Smtp_ErrorsDontContainThePassword()
        {
            var e = Validate<SmtpEMailOptions>(s => s.AddSmtpEMailSender(o =>
            {
                ValidSmtp(o);
                o.SmtpPassword = "s3cret-password";
                o.SmtpServer = "";
                o.Security = SecureSocketOptions.Auto;
            }));

            e.Should().NotBeNull();
            e!.Message.Should().NotContain("s3cret-password");
        }

        [Fact]
        public void Smtp_CalledTwice_RegistersOneValidator()
        {
            var services = new ServiceCollection();
            services.AddSmtpEMailSender();
            services.AddSmtpEMailSender(useAsDefaultSender: false);

            services.Count(d => d.ServiceType == typeof(IValidateOptions<SmtpEMailOptions>)
                                && d.ImplementationType == typeof(SmtpEMailOptionsValidator))
                .Should().Be(1);
            services.Count(d => d.ServiceType == typeof(ISmtpEMailSender)).Should().Be(1);
        }

        [Fact]
        public void Smtp_NotDefaultSender_DoesntRegisterIEMailSender()
        {
            var services = new ServiceCollection();
            services.AddSmtpEMailSender(ValidSmtp, useAsDefaultSender: false);

            services.Should().NotContain(d => d.ServiceType == typeof(IEMailSender));
        }

        [Fact]
        public async Task Smtp_InvalidOptions_FailTheHostStart()
        {
            using var host = new HostBuilder()
                .ConfigureServices(s => s.AddSmtpEMailSender())
                .Build();

            var act = () => host.StartAsync();

            (await act.Should().ThrowAsync<OptionsValidationException>())
                .Which.Failures.Should().Contain(f => f.Contains(nameof(SmtpEMailOptions.SmtpServer)));
        }

        [Theory]
        [InlineData(nameof(UnisenderGoEmailOptions.ApiKey))]
        [InlineData(nameof(UnisenderGoEmailOptions.EmailFrom))]
        [InlineData(nameof(UnisenderGoEmailOptions.FromName))]
        public void UnisenderGo_RequiresValues(string property)
        {
            var e = Validate<UnisenderGoEmailOptions>(s => s.AddUnisenderGoEmailSender(o =>
            {
                ValidUnisenderGo(o);
                typeof(UnisenderGoEmailOptions).GetProperty(property)!.SetValue(o, "");
            }));

            e.Should().NotBeNull();
            e!.Failures.Should().ContainSingle().Which.Should().Contain(property);
        }

        [Fact]
        public void UnisenderGo_RejectsUnknownRegion()
        {
            var e = Validate<UnisenderGoEmailOptions>(s => s.AddUnisenderGoEmailSender(o =>
            {
                ValidUnisenderGo(o);
                o.Region = (UnisenderGoRegion)42;
            }));

            e.Should().NotBeNull();
            e!.Failures.Should().ContainSingle().Which.Should().Contain(nameof(UnisenderGoEmailOptions.Region));
        }

        [Fact]
        public void UnisenderGo_AcceptsValidOptions()
        {
            Validate<UnisenderGoEmailOptions>(s => s.AddUnisenderGoEmailSender(ValidUnisenderGo)).Should().BeNull();
        }

        [Fact]
        public async Task UnisenderGo_InvalidOptions_FailTheHostStart()
        {
            using var host = new HostBuilder()
                .ConfigureServices(s => s.AddUnisenderGoEmailSender())
                .Build();

            var act = () => host.StartAsync();

            (await act.Should().ThrowAsync<OptionsValidationException>())
                .Which.Failures.Should().Contain(f => f.Contains(nameof(UnisenderGoEmailOptions.ApiKey)));
        }
    }
}
