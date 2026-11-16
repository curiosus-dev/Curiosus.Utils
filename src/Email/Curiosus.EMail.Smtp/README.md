# Curiosus.EMail.SMTP

[![NuGet](https://img.shields.io/nuget/v/Curiosus.EMail.SMTP)](https://www.nuget.org/packages/Curiosus.EMail.SMTP) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.EMail.SMTP)](https://www.nuget.org/packages/Curiosus.EMail.SMTP) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.EMail.SMTP.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

SMTP implementation of `IEMailSender` from [Curiosus.EMail](https://www.nuget.org/packages/Curiosus.EMail),
built on [MailKit](https://github.com/jstedfast/MailKit). Use it to send e-mails through any SMTP server.

## Installation

```bash
dotnet add package Curiosus.EMail.SMTP
```

## Usage

Options (`SmtpEMailOptions`, validated when the app starts: an invalid section throws `OptionsValidationException`):

```yaml
Smtp:
  SmtpServer: smtp.example.com   # required
  SmtpPort: 587                  # default 25, 1-65535
  SmtpLogin: noreply@example.com # required
  SmtpPassword: <secret>         # required
  EMailFrom: noreply@example.com # required
  SenderName: Example            # required
  ReplyTo: support@example.com   # optional
  IgnoreIncorrectExtraParamsType: false
  Security: StartTls             # default: SslOnConnect on port 465, StartTls otherwise; None only for a local relay
  AllowInvalidServerCertificate: false  # true accepts any certificate (dev relays); logs a warning
```

```csharp
using Curiosus.EMail;
using Curiosus.EMail.Smtp;

builder.Services.AddSmtpEMailSender(); // binds "Smtp"; ISmtpEMailSender and, by default, IEMailSender

// in a service that receives IEMailSender sender: send with the default sender settings
await sender.SendAsync("user@example.com", "Invoice", "Your invoice is attached", cancellationToken: ct);

// override sender address/name/reply-to for one message
await sender.SendAsync("user@example.com", "Hi", "<p>Hi!</p>", true,
    new SmtpEMailExtraParams(eMailFrom: "sales@example.com", senderName: "Sales"), ct);
```

`AddSmtpEMailSender("Mail:Smtp")` binds another section, `AddSmtpEMailSender(configure: o => ...)` changes the options
in code after the binding (it works without a section too). The cross-field rules are in `SmtpEMailOptionsValidator`.
Pass `useAsDefaultSender: false` to register only `ISmtpEMailSender` when another provider is the default `IEMailSender`.
Each send opens a new connection, secures it with TLS (implicit on port 465, STARTTLS otherwise; the connection fails
when the server doesn't offer it) and authenticates with the login and password. The server certificate is validated:
chain, host name and revocation. `Auto` and `StartTlsWhenAvailable` are rejected because they fall back to plain text.

## See also

- [Migration guides](https://curiosus-dev.github.io/Curiosus.Utils/migrations) — upgrading from `Curiosity.*` 1.x and between major versions
- [Curiosus.EMail.SMTP 3.0 migration notes](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-email-smtp/3.0)
- [Curiosus.EMail](https://www.nuget.org/packages/Curiosus.EMail) — `IEMailSender` abstraction
- [Curiosus.EMail.UnisenderGo](https://www.nuget.org/packages/Curiosus.EMail.UnisenderGo) — another provider
- [Curiosus.EMail.Mailgun removal](https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-email-mailgun/4.0) — sending through Mailgun over SMTP with this package
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
