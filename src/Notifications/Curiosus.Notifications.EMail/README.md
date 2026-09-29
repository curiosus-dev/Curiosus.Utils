# Curiosus.Notifications.EMail

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Notifications.EMail)](https://www.nuget.org/packages/Curiosus.Notifications.EMail) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Notifications.EMail)](https://www.nuget.org/packages/Curiosus.Notifications.EMail) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Notifications.EMail.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

EMail channel for [Curiosus.Notifications](https://www.nuget.org/packages/Curiosus.Notifications).
It sends `EmailNotification`s through the `IEMailSender` registered in the container (SMTP, Mailgun, UnisenderGo, ...).

## Installation

```bash
dotnet add package Curiosus.Notifications.EMail
```

## Usage

Register an EMail sender, the channel and a builder that turns your metadata into emails:

```csharp
services.AddSmtpEMailSender(smtpOptions);  // any IEMailSender implementation
services.AddCuriosusEMailChannel();        // also registers INotificator
services.AddCuriosusNotificationBuilder<UserRegisteredEmailBuilder>();
```

```csharp
public class UserRegistered : INotificationMetadata
{
    public string Email { get; init; } = null!;
    public string Name { get; init; } = null!;
}

public class UserRegisteredEmailBuilder : EmailNotificationBuilderBase<UserRegistered>
{
    protected override Task<IReadOnlyList<INotification>> BuildNotificationsAsync(
        UserRegistered metadata,
        CancellationToken cancellationToken = default)
    {
        var email = new EmailNotification(
            metadata.Email,
            "Welcome!",
            $"<p>Hello, {metadata.Name}!</p>",
            isBodyHtml: true);

        return Task.FromResult<IReadOnlyList<INotification>>(new[] { email });
    }
}

await notificator.NotifyAsync(new UserRegistered { Email = "user@example.com", Name = "John" }, cancellationToken);
```

The channel type is `EmailNotification.Type` (`curiosus.notifications.email`). Sender errors are thrown as
`NotificationException` with a mapped `NotificationErrorCode` (`Auth`, `Communication`, `RateLimit`, `NoMoney`,
`IncorrectRequestData`, `Unknown`). Provider-specific options can be passed via `IEMailExtraParams`.

To act on every sending result (logging, analytics), implement `IEMailNotificationPostProcessor`
and register it with `services.AddEMailNotificationPostProcessor<MyPostProcessor>()`.

## See also

- [Curiosus.Notifications](https://www.nuget.org/packages/Curiosus.Notifications) — notificator and base classes
- [Curiosus.EMail](https://www.nuget.org/packages/Curiosus.EMail) — `IEMailSender` abstraction;
  providers: [SMTP](https://www.nuget.org/packages/Curiosus.EMail.SMTP),
  [Mailgun](https://www.nuget.org/packages/Curiosus.EMail.Mailgun),
  [UnisenderGo](https://www.nuget.org/packages/Curiosus.EMail.UnisenderGo)
- [Email notifications documentation](https://curiosus-dev.github.io/Curiosus.Utils/notifications/emails)
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
