# Curiosus.SMS.Smsc

[![NuGet](https://img.shields.io/nuget/v/Curiosus.SMS.Smsc)](https://www.nuget.org/packages/Curiosus.SMS.Smsc) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.SMS.Smsc)](https://www.nuget.org/packages/Curiosus.SMS.Smsc) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.SMS.Smsc.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

`ISmsSender` implementation for [SMSC](https://smsc.ru/). Use it to send SMS via an smsc.ru account,
directly or through [Curiosus.Notifications.SMS](https://www.nuget.org/packages/Curiosus.Notifications.SMS).

## Installation

```bash
dotnet add package Curiosus.SMS.Smsc
```

## Usage

`SmscOptions` requires `SmscLogin` and `SmscPassword`; `SmscSender` (sender name) is optional.
For example, bound from configuration:

```yaml
Smsc:
  SmscLogin: my-login
  SmscPassword: my-password
  SmscSender: MyCompany
```

```csharp
var smscOptions = configuration.GetSection("Smsc").Get<SmscOptions>()!;

// registers ISmscSender and, by default, ISmsSender
services.AddSmscSmsSender(smscOptions);
```

```csharp
var result = await smsSender.SendSmsAsync("+71234567890", "Hello!", cancellationToken);
if (result.IsSuccess)
{
    var cost = result.Body.Cost;
    var count = result.Body.SmsCount;
}

// per-message credentials or sender name
await smsSender.SendSmsAsync("+71234567890", "Hello!", new SmscExtraParams("other-login", "other-password", null));
```

Pass `useAsDefaultSender: false` to register only `ISmscSender` (e.g. when another provider is the default `ISmsSender`).

Notes:

- SMSC error codes are mapped to `SmsError` (`Auth`, `NoMoney`, `RateLimit`, `DeliveryError`, `Unknown`);
  the raw SMSC response is available in `SmsSentResult.ResponseJson`.
- If SMSC rejects a message because of the sender name (error 6), the sender retries once without the sender name.

## See also

- [Curiosus.SMS](https://www.nuget.org/packages/Curiosus.SMS) — `ISmsSender` abstraction
- [Curiosus.SMS.Iqsms](https://www.nuget.org/packages/Curiosus.SMS.Iqsms) — sender for iqsms.ru
- [Curiosus.Notifications.SMS](https://www.nuget.org/packages/Curiosus.Notifications.SMS) — SMS notification channel
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
