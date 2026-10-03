# Curiosus.EMail.UnisenderGo

[![NuGet](https://img.shields.io/nuget/v/Curiosus.EMail.UnisenderGo)](https://www.nuget.org/packages/Curiosus.EMail.UnisenderGo) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.EMail.UnisenderGo)](https://www.nuget.org/packages/Curiosus.EMail.UnisenderGo) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.EMail.UnisenderGo.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

[Unisender Go](https://go.unisender.ru/) implementation of `IEMailSender` from
[Curiosus.EMail](https://www.nuget.org/packages/Curiosus.EMail). Sends transactional e-mails through the
Unisender Go HTTP API, with optional link/read tracking and an unsubscribe URL.

## Installation

```bash
dotnet add package Curiosus.EMail.UnisenderGo
```

## Usage

Options (`UnisenderGoEmailOptions`, validated on registration):

```yaml
UnisenderGo:
  ApiKey: <secret>               # required
  Region: Russia                 # default and currently the only region
  EmailFrom: noreply@example.com # required
  FromName: Example              # required
  ReplyTo: support@example.com   # optional
  UnsubscribeUrl: https://example.com/unsubscribe # optional
  TrackLinks: true               # optional
  TrackReads: true               # optional
  SkipUnisenderUnsubscribeFooter: false # optional, works only if enabled for your account
  IgnoreIncorrectExtraParamsType: false
```

```csharp
using Curiosus.EMail;
using Curiosus.Email.UnisenderGo;

var unisenderOptions = builder.Configuration.GetSection("UnisenderGo").Get<UnisenderGoEmailOptions>()!;
builder.Services.AddUnisenderGoEmailSender(unisenderOptions); // IUnisenderGoEmailSender and, by default, IEMailSender

// in a service that receives IEMailSender sender
var response = await sender.SendAsync("user@example.com", "Welcome", "<p>Hello!</p>", isBodyHtml: true, ct);

// override settings for one message
await sender.SendAsync("user@example.com", "News", "<p>News</p>", true,
    new UnisenderGoEmailExtraParams(fromName: "Newsletter", trackLinks: false), ct);
```

HTTP errors are mapped to `EmailError`: 401 → `Auth`, 403 → `Auth` or `RateLimit` (by Unisender error code),
400 → `IncorrectRequestData`, 429 → `RateLimit`, 404 and 5xx → `Communication`.
Note: the namespace is `Curiosus.Email.UnisenderGo` (while the package id is `Curiosus.EMail.UnisenderGo`).

HTTP requests go through the `HttpClient` named `UnisenderGoEmailSender.HttpClientName` from `IHttpClientFactory`,
which `AddUnisenderGoEmailSender` registers. Configure it for timeouts, a proxy or resilience handlers:

```csharp
services.AddHttpClient(UnisenderGoEmailSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
```

## See also

- [Curiosus.EMail](https://www.nuget.org/packages/Curiosus.EMail) — `IEMailSender` abstraction
- [Curiosus.EMail.SMTP](https://www.nuget.org/packages/Curiosus.EMail.SMTP), [Curiosus.EMail.Mailgun](https://www.nuget.org/packages/Curiosus.EMail.Mailgun) — other providers
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
