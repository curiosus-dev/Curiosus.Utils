# Curiosus.SMS

[![NuGet](https://img.shields.io/nuget/v/Curiosus.SMS)](https://www.nuget.org/packages/Curiosus.SMS) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.SMS)](https://www.nuget.org/packages/Curiosus.SMS) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.SMS.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Base abstractions for sending SMS: the `ISmsSender` interface, `SmsSentResult` and `SmsError` codes.
Reference it from code that sends SMS and pick a provider package (smsc.ru, iqsms.ru) or implement your own sender.

## Installation

```bash
dotnet add package Curiosus.SMS
```

## Usage

```csharp
public class PhoneVerifier
{
    private readonly ISmsSender _smsSender;

    public PhoneVerifier(ISmsSender smsSender)
    {
        _smsSender = smsSender;
    }

    public async Task<SmsError> SendCodeAsync(string phone, string code, CancellationToken cancellationToken)
    {
        var result = await _smsSender.SendSmsAsync(phone, $"Your code: {code}", cancellationToken);
        if (result.IsSuccess) return SmsError.None;

        // Auth, Communication, RateLimit, NoMoney, DeliveryError, Unknown
        return (SmsError)result.Errors.First().Code;
    }
}
```

`SendSmsAsync` returns `Response<SmsSentResult>` (from Curiosus.Tools). `SmsSentResult` contains the number of sent
messages and the cost when the provider reports them, and the raw provider response in `ResponseJson`.

To pass provider-specific settings for a single message (e.g. another account or sender name), use the overload
that accepts `ISmsExtraParams`; each provider defines its own implementation (`SmscExtraParams`, `IqsmsExtraParams`).

A custom provider implements both `SendSmsAsync` overloads of `ISmsSender` and reports failures as
`Response.Failed(new Error((int)SmsError.Auth, "..."), new SmsSentResult(null, null, rawResponse))`.

## See also

- [Curiosus.SMS.Smsc](https://www.nuget.org/packages/Curiosus.SMS.Smsc) — sender for smsc.ru
- [Curiosus.SMS.Iqsms](https://www.nuget.org/packages/Curiosus.SMS.Iqsms) — sender for iqsms.ru
- [Curiosus.Notifications.SMS](https://www.nuget.org/packages/Curiosus.Notifications.SMS) — SMS notification channel
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
