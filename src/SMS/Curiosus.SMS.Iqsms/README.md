# Curiosus.SMS.Iqsms

[![NuGet](https://img.shields.io/nuget/v/Curiosus.SMS.Iqsms)](https://www.nuget.org/packages/Curiosus.SMS.Iqsms) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.SMS.Iqsms)](https://www.nuget.org/packages/Curiosus.SMS.Iqsms) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.SMS.Iqsms.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

`ISmsSender` implementation for [IQSMS](https://iqsms.ru/). Use it to send SMS via an iqsms.ru account,
directly or through [Curiosus.Notifications.SMS](https://www.nuget.org/packages/Curiosus.Notifications.SMS).

## Installation

```bash
dotnet add package Curiosus.SMS.Iqsms
```

## Usage

`IqsmsOptions` requires `Login` and `Password`; `Sender` (sender name) is optional.
For example, bound from configuration:

```yaml
IqsmsOptions:
  Login: my-login
  Password: my-password
  Sender: MyCompany
  AutoTransformPhoneNumber: true
```

```csharp
var iqsmsOptions = configuration.GetSection("IqsmsOptions").Get<IqsmsOptions>()!;

// registers IIqsmsSender and, by default, ISmsSender
services.AddIqsmsSender(iqsmsOptions);
```

```csharp
var result = await smsSender.SendSmsAsync("89123456789", "Hello iqsms!", cancellationToken);
if (!result.IsSuccess)
{
    logger.LogError("Failed to send SMS: {Response}", result.Body.ResponseJson);
}

// per-message credentials or sender name
await smsSender.SendSmsAsync("+79123456789", "Hello!", new IqsmsExtraParams(null, null, "OtherSender"));
```

Pass `useAsDefaultSender: false` to register only `IIqsmsSender` (e.g. when another provider is the default `ISmsSender`).

Notes:

- iqsms.ru expects numbers as `+71234567890`. With `AutoTransformPhoneNumber` (default `true`) numbers starting
  with `8` or `7` are converted to this format.
- Errors are mapped to `SmsError` (`Auth`, `Communication`, `NoMoney`, `DeliveryError`, `Unknown`);
  the raw iqsms.ru response is available in `SmsSentResult.ResponseJson`. Cost and message count are not reported.

A runnable console sample is in
[samples/Curiosus.SMS.Iqsms.Sample](https://github.com/curiosus-dev/Curiosus.Utils/tree/main/samples/Curiosus.SMS.Iqsms.Sample).

HTTP requests go through the `HttpClient` named `IqsmsSender.HttpClientName` from `IHttpClientFactory`,
which `AddIqsmsSender` registers. Configure it for timeouts, a proxy or resilience handlers:

```csharp
services.AddHttpClient(IqsmsSender.HttpClientName, client => client.Timeout = TimeSpan.FromSeconds(30));
```

## See also

- [Curiosus.SMS](https://www.nuget.org/packages/Curiosus.SMS) — `ISmsSender` abstraction
- [Curiosus.SMS.Smsc](https://www.nuget.org/packages/Curiosus.SMS.Smsc) — sender for smsc.ru
- [Curiosus.Notifications.SMS](https://www.nuget.org/packages/Curiosus.Notifications.SMS) — SMS notification channel
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
