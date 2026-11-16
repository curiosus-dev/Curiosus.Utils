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

        private static Dictionary<string, string?> SmtpSection(string section) => new()
        {
            [$"{section}:SmtpServer"] = "smtp.example.com",
            [$"{section}:SmtpPort"] = "587",
            [$"{section}:SmtpLogin"] = "login",
            [$"{section}:SmtpPassword"] = "password",
            [$"{section}:EMailFrom"] = "noreply@example.com",
            [$"{section}:SenderName"] = "Sender",
            [$"{section}:Security"] = "SslOnConnect",
        };

        private static IServiceCollection Services(IDictionary<string, string?>? configuration = null) =>
            new ServiceCollection()
                .AddLogging()
                .AddSingleton<IConfiguration>(new ConfigurationBuilder()
                    .AddInMemoryCollection(configuration ?? new Dictionary<string, string?>())
                    .Build());

        private static T Resolve<T>(IServiceCollection services) where T : class
        {
            using var provider = services.BuildServiceProvider();
            return provider.GetRequiredService<IOptions<T>>().Value;
        }

        private static OptionsValidationException? Validate<T>(Action<IServiceCollection> register) where T : class
        {
            var services = Services();
            register(services);
            try
            {
                Resolve<T>(services);
                return null;
            }
            catch (OptionsValidationException e)
            {
                return e;
            }
        }

        [Fact]
        public void Smtp_BindsTheDefaultSection()
        {
            var services = Services(SmtpSection(SmtpEMailOptions.SectionName));
            services.AddSmtpEMailSender();
            using var provider = services.BuildServiceProvider();

            var options = provider.GetRequiredService<IOptions<SmtpEMailOptions>>().Value;

            SmtpEMailOptions.SectionName.Should().Be("Smtp");
            options.SmtpPort.Should().Be(587);
            options.Security.Should().Be(SecureSocketOptions.SslOnConnect);
            provider.GetRequiredService<IEMailSender>().Should().BeOfType<SmtpEMailSender>();
        }

        [Fact]
        public void Smtp_BindsACustomSection()
        {
            var services = Services(SmtpSection("Mail:Smtp"));
            services.AddSmtpEMailSender("Mail:Smtp");

            Resolve<SmtpEMailOptions>(services).SmtpServer.Should().Be("smtp.example.com");
        }

        [Fact]
        public void Smtp_ConfigureOverridesTheSection()
        {
            var services = Services(SmtpSection(SmtpEMailOptions.SectionName));
            services.AddSmtpEMailSender(configure: o => o.SmtpPort = 2525);

            var options = Resolve<SmtpEMailOptions>(services);

            options.SmtpPort.Should().Be(2525);
            options.SmtpServer.Should().Be("smtp.example.com");
        }

        [Fact]
        public void Smtp_WithoutSection_UsesCodeConfiguration()
        {
            var services = Services();
            services.AddSmtpEMailSender(configure: ValidSmtp);

            Resolve<SmtpEMailOptions>(services).SmtpPort.Should().Be(25);
        }

        [Theory]
        [InlineData("")]
        [InlineData(" ")]
        public void Smtp_RejectsAnEmptySectionPath(string path)
        {
            var act = () => new ServiceCollection().AddSmtpEMailSender(path);

            act.Should().Throw<ArgumentException>();
        }

        [Theory]
        [InlineData(nameof(SmtpEMailOptions.SmtpServer))]
        [InlineData(nameof(SmtpEMailOptions.SmtpLogin))]
        [InlineData(nameof(SmtpEMailOptions.SmtpPassword))]
        [InlineData(nameof(SmtpEMailOptions.EMailFrom))]
        [InlineData(nameof(SmtpEMailOptions.SenderName))]
        public void Smtp_RequiresValues(string property)
        {
            var e = Validate<SmtpEMailOptions>(s => s.AddSmtpEMailSender(configure: o =>
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
            var e = Validate<SmtpEMailOptions>(s => s.AddSmtpEMailSender(configure: o =>
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
            var e = Validate<SmtpEMailOptions>(s => s.AddSmtpEMailSender(configure: o =>
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
            var e = Validate<SmtpEMailOptions>(s => s.AddSmtpEMailSender(configure: o =>
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
        public void Smtp_CalledTwice_BindsAndReportsErrorsOnce()
        {
            var services = Services();
            services.AddSmtpEMailSender();
            services.AddSmtpEMailSender(useAsDefaultSender: false);

            services.Count(d => d.ServiceType == typeof(IValidateOptions<SmtpEMailOptions>)).Should().Be(2,
                "one data annotations validator and one SmtpEMailOptionsValidator");
            services.Count(d => d.ServiceType == typeof(IOptionsChangeTokenSource<SmtpEMailOptions>)).Should().Be(1);
            services.Count(d => d.ServiceType == typeof(ISmtpEMailSender)).Should().Be(1);

            var once = Validate<SmtpEMailOptions>(s => s.AddSmtpEMailSender());
            var twice = Validate<SmtpEMailOptions>(s =>
            {
                s.AddSmtpEMailSender();
                s.AddSmtpEMailSender();
            });
            twice!.Failures.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(once!.Failures);
        }

        [Fact]
        public void Smtp_CalledTwice_KeepsTheFirstSection()
        {
            var services = Services(SmtpSection(SmtpEMailOptions.SectionName));
            services.AddSmtpEMailSender();
            services.AddSmtpEMailSender("Other");

            Resolve<SmtpEMailOptions>(services).SmtpServer.Should().Be("smtp.example.com");
            services.Count(d => d.ServiceType == typeof(IConfigureOptions<SmtpEMailOptions>)).Should().Be(1);
        }

        [Fact]
        public void Smtp_CalledTwice_AppliesBothConfigurations()
        {
            var services = Services();
            services.AddSmtpEMailSender(configure: ValidSmtp);
            services.AddSmtpEMailSender(configure: o => o.SmtpPort = 2525);

            Resolve<SmtpEMailOptions>(services).SmtpPort.Should().Be(2525);
        }

        [Fact]
        public void Smtp_NotDefaultSender_DoesntRegisterIEMailSender()
        {
            var services = new ServiceCollection();
            services.AddSmtpEMailSender(configure: ValidSmtp, useAsDefaultSender: false);

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

        [Fact]
        public void UnisenderGo_BindsTheDefaultSection()
        {
            var services = Services(new Dictionary<string, string?>
            {
                ["UnisenderGo:ApiKey"] = "key",
                ["UnisenderGo:EmailFrom"] = "noreply@example.com",
                ["UnisenderGo:FromName"] = "Example",
                ["UnisenderGo:TrackLinks"] = "true",
            });
            services.AddUnisenderGoEmailSender();

            var options = Resolve<UnisenderGoEmailOptions>(services);

            UnisenderGoEmailOptions.SectionName.Should().Be("UnisenderGo");
            options.ApiKey.Should().Be("key");
            options.TrackLinks.Should().BeTrue();
        }

        [Fact]
        public void UnisenderGo_BindsACustomSection()
        {
            var services = Services(new Dictionary<string, string?>
            {
                ["Mail:Unisender:ApiKey"] = "key",
                ["Mail:Unisender:EmailFrom"] = "noreply@example.com",
                ["Mail:Unisender:FromName"] = "Example",
            });
            services.AddUnisenderGoEmailSender("Mail:Unisender");

            Resolve<UnisenderGoEmailOptions>(services).ApiKey.Should().Be("key");
        }

        [Fact]
        public void UnisenderGo_ConfigureOverridesTheSection()
        {
            var services = Services(new Dictionary<string, string?>
            {
                ["UnisenderGo:ApiKey"] = "from-config",
                ["UnisenderGo:EmailFrom"] = "noreply@example.com",
                ["UnisenderGo:FromName"] = "Example",
            });
            services.AddUnisenderGoEmailSender(configure: o => o.ApiKey = "from-code");

            Resolve<UnisenderGoEmailOptions>(services).ApiKey.Should().Be("from-code");
        }

        [Fact]
        public void UnisenderGo_CalledTwice_ReportsErrorsOnce()
        {
            var once = Validate<UnisenderGoEmailOptions>(s => s.AddUnisenderGoEmailSender());
            var twice = Validate<UnisenderGoEmailOptions>(s =>
            {
                s.AddUnisenderGoEmailSender();
                s.AddUnisenderGoEmailSender("Other");
            });

            twice.Should().NotBeNull();
            twice!.Failures.Should().OnlyHaveUniqueItems().And.BeEquivalentTo(once!.Failures);
        }

        [Theory]
        [InlineData(nameof(UnisenderGoEmailOptions.ApiKey))]
        [InlineData(nameof(UnisenderGoEmailOptions.EmailFrom))]
        [InlineData(nameof(UnisenderGoEmailOptions.FromName))]
        public void UnisenderGo_RequiresValues(string property)
        {
            var e = Validate<UnisenderGoEmailOptions>(s => s.AddUnisenderGoEmailSender(configure: o =>
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
            var e = Validate<UnisenderGoEmailOptions>(s => s.AddUnisenderGoEmailSender(configure: o =>
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
            Validate<UnisenderGoEmailOptions>(s => s.AddUnisenderGoEmailSender(configure: ValidUnisenderGo))
                .Should().BeNull();
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
