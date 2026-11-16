---
title: Migration guides
description: How to move apps from Curiosity.* 1.x to the current Curiosus.* packages, and the migration page of every major version.
---

# Migration guides

Every package version that breaks your code or changes behaviour you rely on has a migration page here: what
changed, why, before → after code and a checklist. The package CHANGELOGs link to these pages.

This page is the starting point for apps that still use `Curiosity.*` 1.x: the order of the steps, where each package
went and the changes that apply to all packages.

## From Curiosity.* 1.x to Curiosus.*

Take the steps in this order. After each step the app builds, its tests pass and it runs; only then take the next one.

1. **Target framework.** Move the app to `net10.0`, see [Target framework](#target-framework).
2. **Rename.** Replace the `Curiosity.*` packages with the current `Curiosus.*` releases. This step only renames, see
   [Rename Curiosity to Curiosus](#rename-curiosity-to-curiosus); take the major updates the CHANGELOGs list along the
   way (NLog 6, RabbitMQ.Client 7).
3. **Configuration and hosting:** `Curiosus.Configuration`, `Curiosus.Configuration.YML`, `Curiosus.Hosting`,
   `Curiosus.Hosting.Web` (Startup → `Program.cs`).
4. **Tools:** `Curiosus.Tools`.
5. **Web and localization:** `Curiosus.Tools.Web`, `Curiosus.Localization*`, `Curiosus.DataAnnotations`,
   `Curiosus.Cache.MemoryCache`.
6. **Data access and time:** `Curiosus.DAL*`, `Curiosus.DateTime.DbSync`.
7. **Request processing and messaging:** `Curiosus.RequestProcessing*`, `Curiosus.RabbitMQ`.
8. **E-mail and SMS, then notifications:** `Curiosus.EMail*`, `Curiosus.SMS*`, then `Curiosus.Notifications*`.
9. **Files and transfer:** `Curiosus.Archiver*`, `Curiosus.FileDataReaderWriters*`, `Curiosus.SFTP*`.

Steps 3–9 follow the [milestones](https://github.com/curiosus-dev/Curiosus.Utils/milestones) of the redesign. Each of
them takes the next major of its packages from the table below, with the migration page linked there.

## Packages

Every `Curiosity.*` package has a `Curiosus.*` successor: version 2.0.0 of it is the rename with the same API, later
versions list their changes in the CHANGELOG of the package (step 2). The last column is the next major version of the package and its migration page; "planned in" links the
issues that will add the page. Removed packages get a page too: it tells what replaces them.

| Curiosity.* 1.x | Curiosus.* | Next major |
|---|---|---|
| `Curiosity.Archiver` | [Curiosus.Archiver](../packages/archiver/curiosus-archiver.md) | 3.0, planned in [#92](https://github.com/curiosus-dev/Curiosus.Utils/issues/92) |
| `Curiosity.Archiver.SharpZip` | [Curiosus.Archiver.SharpZip](../packages/archiver/curiosus-archiver-sharpzip.md) | removed in favour of `Curiosus.Archiver`, planned in [#92](https://github.com/curiosus-dev/Curiosus.Utils/issues/92) |
| `Curiosity.Cache.MemoryCache` | [Curiosus.Cache.MemoryCache](../packages/misc/curiosus-cache-memorycache.md) | removed in favour of `IMemoryCache.GetOrCreateAsync`, planned in [#59](https://github.com/curiosus-dev/Curiosus.Utils/issues/59) |
| `Curiosity.Configuration` | [Curiosus.Configuration](../packages/configuration/curiosus-configuration.md), [2.1 notes](./curiosus-configuration/2.1.md) | [3.0](./curiosus-configuration/3.0.md): options on `Microsoft.Extensions.Options`, planned in [#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44) |
| `Curiosity.Configuration.YML` | [Curiosus.Configuration.YML](../packages/configuration/curiosus-configuration-yaml.md) | removed in favour of `AddCuriosusConfiguration` of `Curiosus.Hosting`, planned in [#40](https://github.com/curiosus-dev/Curiosus.Utils/issues/40) |
| `Curiosity.DAL` | [Curiosus.DAL](../packages/dal/curiosus-dal.md) | [3.0](./curiosus-dal/3.0.md) (in progress): `DbOptions` on `Microsoft.Extensions.Options`, [#44](https://github.com/curiosus-dev/Curiosus.Utils/issues/44); later removed in favour of `Curiosus.DAL.EF`, planned in [#68](https://github.com/curiosus-dev/Curiosus.Utils/issues/68) |
| `Curiosity.DAL.Dapper` | [Curiosus.DAL.Dapper](../packages/dal/curiosus-dal-dapper.md) | removed in favour of Dapper or EF Core raw SQL, planned in [#71](https://github.com/curiosus-dev/Curiosus.Utils/issues/71) |
| `Curiosity.DAL.EF` | [Curiosus.DAL.EF](../packages/dal/curiosus-dal-ef.md) | 3.0, planned in [#68](https://github.com/curiosus-dev/Curiosus.Utils/issues/68) |
| `Curiosity.DAL.EF.Polly` | [Curiosus.DAL.EF.Polly](../packages/dal/curiosus-dal-ef-polly.md) | removed in favour of `EnableRetryOnFailure` and Polly v8, planned in [#72](https://github.com/curiosus-dev/Curiosus.Utils/issues/72) |
| `Curiosity.DAL.NpgSQL` | [Curiosus.DAL.NpgSQL](../packages/dal/curiosus-dal-npgsql.md) | 3.0, planned in [#70](https://github.com/curiosus-dev/Curiosus.Utils/issues/70) |
| `Curiosity.DataAnnotations` | [Curiosus.DataAnnotations](../packages/misc/curiosus-dataannotations.md) | removed, helpers move to `Curiosus.Tools`, planned in [#57](https://github.com/curiosus-dev/Curiosus.Utils/issues/57) |
| `Curiosity.DateTime.DbSync` | [Curiosus.DateTime.DbSync](../packages/misc/curiosus-datetime-dbsync.md) | [3.0](./curiosus-datetime-dbsync/3.0.md) |
| `Curiosity.EMail` | [Curiosus.EMail](../packages/email/curiosus-email.md) | 3.0, planned in [#80](https://github.com/curiosus-dev/Curiosus.Utils/issues/80), [#81](https://github.com/curiosus-dev/Curiosus.Utils/issues/81) |
| `Curiosity.EMail.Mailgun` | `Curiosus.EMail.Mailgun` (removed, last version 3.0.0) | [removed](./curiosus-email-mailgun/4.0.md) in favour of `Curiosus.EMail.SMTP` with Mailgun SMTP credentials or `Curiosus.EMail.UnisenderGo`, [#106](https://github.com/curiosus-dev/Curiosus.Utils/issues/106) |
| `Curiosity.EMail.SMTP` | [Curiosus.EMail.SMTP](../packages/email/curiosus-email-smtp.md) | [3.0](./curiosus-email-smtp/3.0.md) (in progress), planned in [#85](https://github.com/curiosus-dev/Curiosus.Utils/issues/85), [#86](https://github.com/curiosus-dev/Curiosus.Utils/issues/86) |
| `Curiosity.EMail.UnisenderGo` | [Curiosus.EMail.UnisenderGo](../packages/email/curiosus-email-unisendergo.md) | 4.0, planned in [#81](https://github.com/curiosus-dev/Curiosus.Utils/issues/81), [#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82) |
| `Curiosity.FileDataReaderWriters` | [Curiosus.FileDataReaderWriters](../packages/filedata/curiosus-filedatareaderwriters.md) | 3.0, planned in [#94](https://github.com/curiosus-dev/Curiosus.Utils/issues/94) |
| `Curiosity.FileDataReaderWriters.Npoi` | [Curiosus.FileDataReaderWriters.Npoi](../packages/filedata/curiosus-filedatareaderwriters-npoi.md) | follows the base package, [#94](https://github.com/curiosus-dev/Curiosus.Utils/issues/94) |
| `Curiosity.FileDataReaderWriters.SylvanCsv` | [Curiosus.FileDataReaderWriters.SylvanCsv](../packages/filedata/curiosus-filedatareaderwriters-sylvancsv.md) | renamed to `Curiosus.FileDataReaderWriters.Csv`, planned in [#93](https://github.com/curiosus-dev/Curiosus.Utils/issues/93) |
| `Curiosity.Hosting` | [Curiosus.Hosting](../packages/hosting/curiosus-hosting.md) | [4.0](./curiosus-hosting/4.0.md) (in progress), planned in [#45](https://github.com/curiosus-dev/Curiosus.Utils/issues/45), [#46](https://github.com/curiosus-dev/Curiosus.Utils/issues/46) |
| `Curiosity.Hosting.Web` | [Curiosus.Hosting.Web](../packages/hosting/curiosus-hosting-web.md) | [4.0](./curiosus-hosting-web/4.0.md): `Startup` → `Program.cs`, only `CuriosusWebHost` is left |
| `Curiosity.Localization` | [Curiosus.Localization](../packages/localization/curiosus-localization.md) | 3.0, planned in [#58](https://github.com/curiosus-dev/Curiosus.Utils/issues/58) |
| `Curiosity.Localization.MVC` | [Curiosus.Localization.MVC](../packages/localization/curiosus-localization-mvc.md) | 3.0, planned in [#58](https://github.com/curiosus-dev/Curiosus.Utils/issues/58) |
| `Curiosity.Notifications` | [Curiosus.Notifications](../packages/notifications/curiosus-notifications.md) | 3.0, planned in [#89](https://github.com/curiosus-dev/Curiosus.Utils/issues/89) |
| `Curiosity.Notifications.EMail` | [Curiosus.Notifications.EMail](../packages/notifications/curiosus-notifications-email.md) | 3.0, planned in [#88](https://github.com/curiosus-dev/Curiosus.Utils/issues/88) |
| `Curiosity.Notifications.SMS` | [Curiosus.Notifications.SMS](../packages/notifications/curiosus-notifications-sms.md) | 3.0, planned in [#88](https://github.com/curiosus-dev/Curiosus.Utils/issues/88) |
| `Curiosity.RabbitMQ` | [Curiosus.RabbitMQ](../packages/messaging/curiosus-rabbitmq.md) | [4.0](./curiosus-rabbitmq/4.0.md) (in progress): shared connection, confirmed publisher, options from configuration, planned in [#75](https://github.com/curiosus-dev/Curiosus.Utils/issues/75), [#109](https://github.com/curiosus-dev/Curiosus.Utils/issues/109) |
| `Curiosity.RequestProcessing` | [Curiosus.RequestProcessing](../packages/requestprocessing/curiosus-requestprocessing.md) | 3.0, planned in [#78](https://github.com/curiosus-dev/Curiosus.Utils/issues/78) |
| `Curiosity.RequestProcessing.Postgres` | [Curiosus.RequestProcessing.Postgres](../packages/requestprocessing/curiosus-requestprocessing-postgres.md), [2.1 notes](./curiosus-requestprocessing-postgres/2.1.md) | 3.0, planned in [#79](https://github.com/curiosus-dev/Curiosus.Utils/issues/79) |
| `Curiosity.RequestProcessing.RabbitMQ` | [Curiosus.RequestProcessing.RabbitMQ](../packages/requestprocessing/curiosus-requestprocessing-rabbitmq.md) | 4.0, planned in [#77](https://github.com/curiosus-dev/Curiosus.Utils/issues/77) |
| `Curiosity.SFTP` | [Curiosus.SFTP](../packages/sftp/curiosus-sftp.md) | [3.0](./curiosus-sftp/3.0.md) (in progress), planned in [#96](https://github.com/curiosus-dev/Curiosus.Utils/issues/96), [#97](https://github.com/curiosus-dev/Curiosus.Utils/issues/97) |
| `Curiosity.SFTP.SSH.NET` | [Curiosus.SFTP.SSH.NET](../packages/sftp/curiosus-sftp-ssh-net.md) | [3.0](./curiosus-sftp/3.0.md) (in progress), planned in [#96](https://github.com/curiosus-dev/Curiosus.Utils/issues/96), [#97](https://github.com/curiosus-dev/Curiosus.Utils/issues/97) |
| `Curiosity.SMS` | [Curiosus.SMS](../packages/sms/curiosus-sms.md) | 3.0, planned in [#81](https://github.com/curiosus-dev/Curiosus.Utils/issues/81) |
| `Curiosity.SMS.Iqsms` | [Curiosus.SMS.Iqsms](../packages/sms/curiosus-sms-iqsms.md) | 4.0, planned in [#81](https://github.com/curiosus-dev/Curiosus.Utils/issues/81), [#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82) |
| `Curiosity.SMS.Smsc` | [Curiosus.SMS.Smsc](../packages/sms/curiosus-sms-smsc.md) | 4.0, planned in [#81](https://github.com/curiosus-dev/Curiosus.Utils/issues/81), [#82](https://github.com/curiosus-dev/Curiosus.Utils/issues/82) |
| `Curiosity.Tools` | [Curiosus.Tools](../packages/misc/curiosus-tools.md) | [3.0](./curiosus-tools/3.0.md) (in progress), planned in [#47](https://github.com/curiosus-dev/Curiosus.Utils/issues/47)–[#56](https://github.com/curiosus-dev/Curiosus.Utils/issues/56) |
| `Curiosity.Tools.Web` | [Curiosus.Tools.Web](../packages/misc/curiosus-tools-web.md) | [4.0](./curiosus-tools-web/4.0.md) (in progress), planned in [#60](https://github.com/curiosus-dev/Curiosus.Utils/issues/60)–[#67](https://github.com/curiosus-dev/Curiosus.Utils/issues/67) |

## Rename Curiosity to Curiosus

`Curiosus.*` 2.0.0 renamed the packages, assemblies, namespaces, types and methods: `Curiosity` became `Curiosus`.
Nothing else changed in that release, so the step is a search and replace.

1. Package references: `Curiosity.` → `Curiosus.` in `PackageReference` items (or `Directory.Packages.props`), with
   the current versions from the table above.
2. Namespaces: `using Curiosity.` → `using Curiosus.` and `namespace`-qualified names (`Curiosity.Tools.` →
   `Curiosus.Tools.`).
3. Types and methods that carry the name, for example:
   - `CuriosityWatchdog` → `CuriosusWatchdog`, `CuriosityAppBootstrapper` → `CuriosusAppBootstrapper` (and the service,
     web and tool bootstrappers), `ICuriosityAppConfiguration` → `ICuriosusAppConfiguration`;
   - `CuriosityDataContext` → `CuriosusDataContext`, `ICuriosityDataContextFactory` → `ICuriosusDataContextFactory`;
   - `AddCuriosity*` → `AddCuriosus*` (`AddCuriosusNotificator`, `AddCuriosusDataProtection`, ...),
     `UseCuriosity*` → `UseCuriosus*` (`UseCuriosusExceptionHandler`).

   A regex replacement in C# files covers all of them and skips `Curiosity.Migrations`:
   `\bCuriosity(?!\.Migrations)` → `Curiosus`.
4. NLog.config: rules and `<extensions>` that name `Curiosity.*` loggers or assemblies, see [NLog](#nlog).

Don't replace:

- `Curiosity.Migrations`: it lives in its own repository and is renamed there, on its own schedule.
- `Curiosity` in your own type names, database objects, configuration values or stored data that is not about these
  packages.

## Changes that apply to all packages

### Target framework

The packages target `net9.0` and `net10.0`; most `Curiosity.*` 1.x packages also targeted `netstandard2.1`. Move the app to
`net10.0` before anything else: `net9.0` support will be dropped in the next majors
([#108](https://github.com/curiosus-dev/Curiosus.Utils/issues/108)). A major version whose only breaking change is the
target framework links here instead of having its own page.

### NLog

- `Curiosus.Hosting` 3.0.0 moved apps to **NLog 6**, which rejects some NLog.config options of NLog 5: see the migration
  steps in the [Curiosus.Hosting CHANGELOG](https://github.com/curiosus-dev/Curiosus.Utils/blob/main/src/Hosting/Curiosus.Hosting/CHANGELOG.md).
- Logger names in NLog.config rules: `Curiosity.*` → `Curiosus.*` (loggers named after classes).
- Loggers that keep their names for now:
  - `appStartEmailLog`, `appStopEmailLog`: unchanged ([#43](https://github.com/curiosus-dev/Curiosus.Utils/issues/43));
  - `performance`: unchanged ([#52](https://github.com/curiosus-dev/Curiosus.Utils/issues/52)); `stuckcode` is gone
    with `StuckCodeManager` in `Curiosus.Tools` 3.0;
  - `performanceWeb`: changes with [#64](https://github.com/curiosus-dev/Curiosus.Utils/issues/64);
  - `worker*` of request processing: change with [#78](https://github.com/curiosus-dev/Curiosus.Utils/issues/78).

### RabbitMQ.Client 7

`Curiosus.RabbitMQ` 3.0.0 and `Curiosus.RequestProcessing.RabbitMQ` 3.0.0 use `RabbitMQ.Client` 7, which has an
async-only API. App code that uses `RabbitMQ.Client` directly moves with them: `IModel` → `IChannel`,
`CreateConnection` → `CreateConnectionAsync`, `BasicPublish` → `BasicPublishAsync`, `EventingBasicConsumer` →
`AsyncEventingBasicConsumer`; see the
[RabbitMQ.Client v7 migration guide](https://github.com/rabbitmq/rabbitmq-dotnet-client/blob/main/v7-MIGRATION.md)
and the CHANGELOGs of both packages. The next majors replace most hand-written RabbitMQ code with a shared connection,
publisher and consumer host ([#75](https://github.com/curiosus-dev/Curiosus.Utils/issues/75),
[#77](https://github.com/curiosus-dev/Curiosus.Utils/issues/77),
[#109](https://github.com/curiosus-dev/Curiosus.Utils/issues/109)).

### System.Text.Json

The packages serialize with `System.Text.Json` only; the last `Newtonsoft.Json` usage, `TrimStringNewtonsoftConverter`
of `Curiosus.Tools.Web`, is obsolete and goes away with
[#65](https://github.com/curiosus-dev/Curiosus.Utils/issues/65). Apps may keep `AddNewtonsoftJson` for their own models;
to move them, see
[Migrate from Newtonsoft.Json to System.Text.Json](https://learn.microsoft.com/dotnet/standard/serialization/system-text-json/migrate-from-newtonsoft).

### Startup → Program.cs

Web apps move from a `Startup` class to top-level statements in `Program.cs` on `WebApplication`, created with
`CuriosusWebHost.CreateBuilder(args)` of `Curiosus.Hosting.Web` 4.0
([#42](https://github.com/curiosus-dev/Curiosus.Utils/issues/42)): see its [migration page](./curiosus-hosting-web/4.0.md).

## Writing a migration page

For contributors: a pull request that changes behaviour consumers must act on adds or extends the migration page of
each package it changes. That is every breaking change, and also changed output, messages or defaults, and security
fixes that need credentials rotated. When unsure, write the page: a short page costs less than a surprised consumer.

- One page per package version: `docs/migrations/<package-id-lowercase>/<major.minor>.md`, for example
  `curiosus-hosting/4.0.md` or `curiosus-configuration/2.1.md` for a minor version that changes behaviour, and a `_category_.json` with the package id as the label next to the first page of a
  package. Copy [`_template.md`](https://github.com/curiosus-dev/Curiosus.Utils/blob/main/docs/migrations/_template.md).
- Front matter sets `id`, `slug` and `title` explicitly (Docusaurus would read `4.0` as a number prefix otherwise) and
  `sidebar_position: 100 - major`, so the newest major comes first (a minor of the same major gets the same position
  and sorts by title).
- Several issues that break one package in one major share its page, one section per issue, each linking its issue.
- The page says which target framework it requires, lists the breaking changes, and for each: why, before → after code,
  a checklist.
- A removed package gets a page for the major it would have had next (`curiosus-hosting-web/4.0.md`); the README
  package table and the NuGet deprecation message link to it, and the pages of the packages that replace it too.
- The CHANGELOG entries of the change (the **Breaking** entry when there is one) link the page with its absolute URL,
  because CHANGELOGs are read on GitHub, nuget.org and this site:
  `https://curiosus-dev.github.io/Curiosus.Utils/migrations/curiosus-hosting/4.0`. Packages that ship the change
  through a dependency (`Curiosus.Hosting` for `Curiosus.Configuration`) link it from their entry too.
- The pull request updates the row of the package in the [Packages](#packages) table and adds the page to the
  "See also" list of the package README (absolute URL), next to the link to these guides that every package README has.
