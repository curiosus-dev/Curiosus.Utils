# Curiosus.Notifications.SMS

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Notifications.SMS)](https://www.nuget.org/packages/Curiosus.Notifications.SMS) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Notifications.SMS)](https://www.nuget.org/packages/Curiosus.Notifications.SMS) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Notifications.SMS.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

SMS channel for [Curiosus.Notifications](https://www.nuget.org/packages/Curiosus.Notifications).
It sends `SmsNotification`s through the `ISmsSender` registered in the container (smsc.ru, iqsms.ru, ...).

## Installation

```bash
dotnet add package Curiosus.Notifications.SMS
```

## Usage

Register an SMS sender, the channel and a builder that turns your metadata into SMS:

```csharp
services.AddSmscSmsSender(smscOptions);  // any ISmsSender implementation
services.AddCuriosusSmsChannel();        // also registers INotificator
services.AddCuriosusNotificationBuilder<LoginCodeSmsBuilder>();
```

```csharp
public class LoginCodeRequested : INotificationMetadata
{
    public string Phone { get; init; } = null!;
    public string Code { get; init; } = null!;
}

public class LoginCodeSmsBuilder : SmsNotificationBuilderBase<LoginCodeRequested>
{
    protected override Task<IReadOnlyList<INotification>> BuildNotificationsAsync(
        LoginCodeRequested metadata,
        CancellationToken cancellationToken = default)
    {
        var sms = new SmsNotification(metadata.Phone, $"Your code: {metadata.Code}");

        return Task.FromResult<IReadOnlyList<INotification>>(new[] { sms });
    }
}

await notificator.NotifyAsync(new LoginCodeRequested { Phone = "+71234567890", Code = "1234" }, cancellationToken);
```

The channel type is `SmsNotification.Type` (`curiosus.notifications.sms`). Sender errors are thrown as
`NotificationException` with a mapped `NotificationErrorCode` (`Auth`, `Communication`, `RateLimit`, `NoMoney`,
`DeliveryError`, `Unknown`). Provider-specific options (e.g. other credentials) can be passed as `ISmsExtraParams`
in the `SmsNotification` constructor.

To act on every sending result (e.g. store `SmsSentResult.Cost`), implement `ISmsNotificationPostProcessor`
and register it with `services.AddSmsNotificationPostProcessor<MyPostProcessor>()`.

## See also

- [Curiosus.Notifications](https://www.nuget.org/packages/Curiosus.Notifications) — notificator and base classes
- [Curiosus.SMS](https://www.nuget.org/packages/Curiosus.SMS) — `ISmsSender` abstraction;
  providers: [Curiosus.SMS.Smsc](https://www.nuget.org/packages/Curiosus.SMS.Smsc),
  [Curiosus.SMS.Iqsms](https://www.nuget.org/packages/Curiosus.SMS.Iqsms)
- [SMS notifications documentation](https://curiosus-dev.github.io/Curiosus.Utils/notifications/sms)
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
