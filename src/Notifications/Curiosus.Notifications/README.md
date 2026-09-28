# Curiosus.Notifications

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Notifications)](https://www.nuget.org/packages/Curiosus.Notifications) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Notifications)](https://www.nuget.org/packages/Curiosus.Notifications) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Notifications.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Base classes for channel-based notifications: an `INotificator` turns your metadata into channel-specific notifications
via builders and sends them through queued background channels. Use it together with a channel package
(EMail, SMS) or implement your own channel on top of `NotificationChannelBase<T>`.

## Installation

```bash
dotnet add package Curiosus.Notifications
```

## Usage

Key concepts:

- `INotificationMetadata` — your data describing an event (e.g. "user registered").
- `INotificationBuilder` — builds `INotification` instances for one channel from one metadata type.
- `INotificationChannel` — sends notifications; `NotificationChannelBase<T>` is a hosted service
  that processes its queue one notification at a time.
- `INotificator` — finds all builders for the metadata type and sends the built notifications to their channels.

```csharp
services.AddCuriosusNotificator();

// a channel is registered as INotificationChannel and IHostedService
services.AddCuriosusNotificationChannel<MyChannel>();
services.AddCuriosusNotificationBuilder<MyBuilder>();
```

```csharp
public class UserRegistered : INotificationMetadata
{
    public string Email { get; init; } = null!;
}

// wait for all channels
try
{
    await notificator.NotifyAsync(new UserRegistered { Email = "user@example.com" }, cancellationToken);
}
catch (NotificationException e) when (e.ErrorCode == NotificationErrorCode.RateLimit)
{
    // retry later
}

// or do not wait, errors are logged
notificator.NotifyAndForgot(new UserRegistered { Email = "user@example.com" });
```

`Notificator` resolves a non-generic `ILogger` from the container (Curiosus.Hosting bootstrappers register it),
and requires at least one channel and one builder to be registered.

## See also

- [Curiosus.Notifications.EMail](https://www.nuget.org/packages/Curiosus.Notifications.EMail) — EMail channel
- [Curiosus.Notifications.SMS](https://www.nuget.org/packages/Curiosus.Notifications.SMS) — SMS channel
- [Notifications documentation](https://curiosityutils.readthedocs.io/en/latest/Notiifications/)
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
