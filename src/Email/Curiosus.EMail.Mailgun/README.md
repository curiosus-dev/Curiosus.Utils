# Curiosus.EMail.Mailgun

[![NuGet](https://img.shields.io/nuget/v/Curiosus.EMail.Mailgun)](https://www.nuget.org/packages/Curiosus.EMail.Mailgun) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.EMail.Mailgun)](https://www.nuget.org/packages/Curiosus.EMail.Mailgun) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.EMail.Mailgun.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

[Mailgun](https://www.mailgun.com/) implementation of `IEMailSender` from
[Curiosus.EMail](https://www.nuget.org/packages/Curiosus.EMail). Sends messages through the Mailgun HTTP API
(US or EU region).

> **Known issue:** in the current version `MailgunEmailOptions.AssertValid()` throws `NotImplementedException`,
> so registering or constructing the sender fails. Use another `IEMailSender` provider until it is fixed.

## Installation

```bash
dotnet add package Curiosus.EMail.Mailgun
```

## Usage

Options (`MailgunEmailOptions`, validated on registration):

```yaml
Mailgun:
  MailgunRegion: US                # US (default) or EU
  MailgunUser: api                 # required, basic auth user
  MailgunApiKey: <secret>          # required
  MailgunDomain: mg.example.com    # required
  EmailFrom: noreply@mg.example.com # required
  ReplyTo: support@example.com     # optional
  IgnoreIncorrectExtraParamsType: false
```

```csharp
using Curiosus.EMail;
using Curiosus.EMail.Mailgun;

var mailgunOptions = builder.Configuration.GetSection("Mailgun").Get<MailgunEmailOptions>()!;
builder.Services.AddMailgunEmailSender(mailgunOptions); // IMailgunEmailSender and, by default, IEMailSender

// in a service that receives IEMailSender sender
var response = await sender.SendAsync("user@example.com", "Welcome", "<p>Hello!</p>", isBodyHtml: true, ct);

// use another domain/key for one message
await sender.SendAsync("user@example.com", "Report", "See attachment", false,
    new MailgunEmailExtraParams(mailgunDomain: "reports.example.com", emailFrom: "reports@reports.example.com"), ct);
```

HTTP 420 from Mailgun is returned as `EmailError.RateLimit`; other unsuccessful responses as `EmailError.Auth`.
Pass `useAsDefaultSender: false` to register only `IMailgunEmailSender`.

HTTP requests go through the `HttpClient` named `MailgunEmailSender.HttpClientName` from `IHttpClientFactory`,
which `AddMailgunEmailSender` registers. Configure it for timeouts, a proxy or resilience handlers:

```csharp
services.AddHttpClient(MailgunEmailSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
```

## See also

- [Curiosus.EMail](https://www.nuget.org/packages/Curiosus.EMail) — `IEMailSender` abstraction
- [Curiosus.EMail.SMTP](https://www.nuget.org/packages/Curiosus.EMail.SMTP), [Curiosus.EMail.UnisenderGo](https://www.nuget.org/packages/Curiosus.EMail.UnisenderGo) — other providers
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
