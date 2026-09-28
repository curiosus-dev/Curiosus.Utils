# Curiosus.Tools

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Tools)](https://www.nuget.org/packages/Curiosus.Tools) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Tools)](https://www.nuget.org/packages/Curiosus.Tools) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Tools.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Foundation library of the Curiosus packages: basic helpers, models and small services with few dependencies
(logging and DI abstractions, Curiosus.Configuration, NodaTime, MemoryPools). Most other Curiosus packages build on it.

## Installation

```bash
dotnet add package Curiosus.Tools
```

## Usage

```csharp
using Curiosus.Tools;
using Curiosus.Tools.AppInitializer;
using Curiosus.Tools.TempFiles;

// startup: DI registrations
services.AddLocalDateTimeService();                       // IDateTimeService over the local clock
services.AddTempFileServices(new TempFileOptions());      // ITempFileStreamFactory in ./.tmp
services.AddAppInitializer<WarmUpCacheInitializer>();     // IAppInitializer, run on start by Curiosus.Hosting

// unique 64-bit ids based on time; generatorId must differ between running processes
UniqueIdGenerator.Initialize(generatorId: 1);
long id = UniqueIdGenerator.Generate();
string publicId = id.ToPublicId();

// operation results
Response<Order> result = Response.Successful(order);
Response<Order> failed = Response.Failed<Order>(404, "Order not found");

// background work without losing exceptions
SendNotificationAsync(order).WithExceptionLogger(logger);

// hide secrets before logging JSON
var json = new SensitiveDataProtector("password").HideInJson(requestJson);
```

Main areas:

- **App lifecycle** — `IAppInitializer` + `AddAppInitializer`, `FireAndForget`, `ApplicationHelper`, `EnvironmentHelper`.
- **Ids and hashing** — `UniqueIdGenerator`, `PublicId`, `UniqueKeyGenerator`, `XXHasher`.
- **Date and time** — `IDateTimeService`, `DateTimePeriod`, `NullableDateTimePeriod`, `DateTimeHelper`,
  `TimeZoneHelper` (NodaTime).
- **Models** — `Response`/`Response<T>`, `Error`, `Page`, `PaginationResponse`.
- **Text** — string/`StringBuilder` extensions, `PhoneHelper`, `Transliteration`, `WebAddressValidator`,
  `SensitiveDataProtector`, file size humanization.
- **Collections and threading** — `CircularBuffer`, list/dictionary/hash set extensions, `DynamicSemaphoreSlim`.
- **Diagnostics** — `PerformanceManager.Measure` and `StuckCodeManager.Enter` (call their `Initialize(logger)` first).
- **IO and HTTP** — temp file streams, stream extensions, `IHttpRequestParamsCalculator` for size-based timeouts.

See the [sample app](https://github.com/curiosus-dev/Curiosus.Utils/tree/main/samples/Curiosus.Tools.Sample)
for phone, transliteration and sensitive data examples.

## See also

- [Curiosus.Tools.Web](https://www.nuget.org/packages/Curiosus.Tools.Web) — ASP.NET Core helpers
- [Curiosus.Hosting](https://www.nuget.org/packages/Curiosus.Hosting) — app bootstrappers that run `IAppInitializer`s
- [Curiosus.Configuration](https://www.nuget.org/packages/Curiosus.Configuration) — validatable options used here
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
