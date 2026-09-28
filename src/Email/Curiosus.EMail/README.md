# Curiosus.EMail

[![NuGet](https://img.shields.io/nuget/v/Curiosus.EMail)](https://www.nuget.org/packages/Curiosus.EMail) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.EMail)](https://www.nuget.org/packages/Curiosus.EMail) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.EMail.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Provider-agnostic e-mail sending contract (`IEMailSender`), error codes, e-mail address validation helpers and
a log-only sender for development. Depend on it from application code and register one of the provider packages.

## Installation

```bash
dotnet add package Curiosus.EMail
```

## Usage

`IEMailSender.SendAsync` returns a `Response` (from [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools));
on failure `Errors[i].Code` is an `EmailError` value (`Auth`, `Communication`, `RateLimit`, `NoMoney`,
`IncorrectRequestData`, `Unknown`). Provider-specific settings for a single message are passed as `IEMailExtraParams`.

```csharp
using Curiosus.EMail;

// development: writes e-mails to the log instead of sending them
builder.Services.AddTestLogEMailSender();

public class WelcomeMailer(IEMailSender sender)
{
    public async Task SendAsync(string email, CancellationToken ct)
    {
        var response = await sender.SendAsync(email, "Welcome", "<b>Hello!</b>", isBodyHtml: true, ct);
        if (!response.IsSuccess && response.Errors[0].Code == (int)EmailError.RateLimit)
        {
            // retry later
        }
    }
}
```

Validation helpers:

```csharp
EmailHelper.IsEmailValid("user@example.com");                 // true
"a@example.com; b@example.com".IsValidEmailsList();          // separators: ';', ',' and space
"A@Example.com, b@example.com".ToNormalizedEmailsListString(); // "a@example.com, b@example.com"

public record SignUpRequest([property: ValidEmail] string Email); // DataAnnotations attribute
```

## See also

- [Curiosus.EMail.SMTP](https://www.nuget.org/packages/Curiosus.EMail.SMTP) — SMTP sender (MailKit)
- [Curiosus.EMail.Mailgun](https://www.nuget.org/packages/Curiosus.EMail.Mailgun) — Mailgun sender
- [Curiosus.EMail.UnisenderGo](https://www.nuget.org/packages/Curiosus.EMail.UnisenderGo) — Unisender Go sender
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
