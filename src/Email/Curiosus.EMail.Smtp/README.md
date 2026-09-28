# Curiosus.EMail.SMTP

[![NuGet](https://img.shields.io/nuget/v/Curiosus.EMail.SMTP)](https://www.nuget.org/packages/Curiosus.EMail.SMTP) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.EMail.SMTP)](https://www.nuget.org/packages/Curiosus.EMail.SMTP) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.EMail.SMTP.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

SMTP implementation of `IEMailSender` from [Curiosus.EMail](https://www.nuget.org/packages/Curiosus.EMail),
built on [MailKit](https://github.com/jstedfast/MailKit). Use it to send e-mails through any SMTP server.

## Installation

```bash
dotnet add package Curiosus.EMail.SMTP
```

## Usage

Options (`SmtpEMailOptions`, validated on registration):

```yaml
Smtp:
  SmtpServer: smtp.example.com   # required
  SmtpPort: 587                  # default 25
  SmtpLogin: noreply@example.com # required
  SmtpPassword: <secret>         # required
  EMailFrom: noreply@example.com # required
  SenderName: Example            # required
  ReplyTo: support@example.com   # optional
  IgnoreIncorrectExtraParamsType: false
```

```csharp
using Curiosus.EMail;
using Curiosus.EMail.Smtp;

var smtpOptions = builder.Configuration.GetSection("Smtp").Get<SmtpEMailOptions>()!;
builder.Services.AddSmtpEMailSender(smtpOptions); // ISmtpEMailSender and, by default, IEMailSender

// in a service that receives IEMailSender sender: send with the default sender settings
await sender.SendAsync("user@example.com", "Invoice", "Your invoice is attached", cancellationToken: ct);

// override sender address/name/reply-to for one message
await sender.SendAsync("user@example.com", "Hi", "<p>Hi!</p>", true,
    new SmtpEMailExtraParams(eMailFrom: "sales@example.com", senderName: "Sales"), ct);
```

Pass `useAsDefaultSender: false` to register only `ISmtpEMailSender` when another provider is the default `IEMailSender`.
Each send opens a new connection with `SecureSocketOptions.Auto` and authenticates with the login and password.
Note: the sender currently accepts any server TLS certificate (certificate validation and revocation checks are disabled).

## See also

- [Curiosus.EMail](https://www.nuget.org/packages/Curiosus.EMail) — `IEMailSender` abstraction
- [Curiosus.EMail.Mailgun](https://www.nuget.org/packages/Curiosus.EMail.Mailgun), [Curiosus.EMail.UnisenderGo](https://www.nuget.org/packages/Curiosus.EMail.UnisenderGo) — other providers
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
