# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

@.claude/curiosus.md

## Project Overview

Curiosus.Utils is a collection of .NET NuGet utility libraries published under MIT license by SIIS Ltd. It provides reusable components for configuration, data access, email/SMS, file processing, messaging, notifications, hosting, and more.

## Architecture

### Module Pattern

Every feature area follows an **abstraction + implementation** pattern:

- **Base project** (`Curiosus.EMail`, `Curiosus.SMS`, `Curiosus.SFTP`, etc.) — defines interfaces and shared types
- **Implementation projects** (`Curiosus.EMail.Smtp`, `Curiosus.EMail.Mailgun`, `Curiosus.SMS.Smsc`, etc.) — concrete implementations with third-party dependencies

This pattern repeats across: Archiver, DAL, Email, FileData, Hosting, Localization, Notifications, RequestProcessing, SFTP, SMS.

### Dependency Flow

```
Curiosus.Configuration (base)
    └── Curiosus.Tools → used by most higher-level libraries
        ├── Curiosus.DAL.EF, Curiosus.DAL.Dapper
        ├── Curiosus.EMail → Smtp, Mailgun, UnisenderGo
        ├── Curiosus.SMS → Smsc, Iqsms
        ├── Curiosus.Notifications → Notifications.EMail, Notifications.SMS
        └── Curiosus.Hosting → Curiosus.Hosting.Web
```

`Curiosus.Tools` is the foundational utility library — most other projects depend on it.

### Source Layout

- `src/` — 37 library projects grouped by feature area
- `tests/UnitTests/` — xUnit tests with FluentAssertions and Moq
- `tests/IntegrationTests/` — integration tests (e.g., UnisenderGo email)
- `samples/` — sample applications demonstrating usage

## Key Technical Details

- **Multi-targeting:** `net9.0;net10.0` for libraries and tests, defined once as `CuriosusTargetFrameworks` in `Directory.Build.props` (projects use `<TargetFrameworks>$(CuriosusTargetFrameworks)</TargetFrameworks>`); samples target `net10.0` only
- **JSON:** `System.Text.Json` only. `Newtonsoft.Json` remains solely for the obsolete `TrimStringNewtonsoftConverter` in `Curiosus.Tools.Web` (removal tracked in #77) — do not add new usages
- **Nullable reference types:** enabled globally
- **Central package management:** `Directory.Packages.props` manages all NuGet versions — update versions there, not in individual .csproj files
- **Test framework:** xUnit + FluentAssertions + Moq
- **CI/CD and releases:** see `.claude/curiosus.md`; each package has its own `CHANGELOG.md` (its top `## [x.y.z]` section is the package version) and is released independently
- **Documentation:** Docusaurus on GitHub Pages at https://curiosus-dev.github.io/Curiosus.Utils/, built from `docs/`
  (site settings in `docs.json`). `docs/index.md` is a symlink to `README.md`, package READMEs are symlinked into
  `docs/packages/<area>/` — add one for a new package. `website/` is synced from dotnet-tools.
- **Migration guides:** a PR that changes behaviour consumers must act on (a breaking change, but also changed output,
  messages or defaults, or a security fix that needs credential rotation) adds or extends the page of the package
  version in `docs/migrations/<package-id-lowercase>/<major.minor>.md` (copy `docs/migrations/_template.md`), links it
  with its absolute URL from the CHANGELOG entries (the **Breaking** entry when there is one) of that package and of
  packages that ship the change through a dependency, updates the package row in `docs/migrations/index.md` and adds
  the page to "See also" of the package README (every package README links the guides there).
  A PR removing a package adds its page and links it from the README package table and the NuGet deprecation message.
  Conventions: "Writing a migration page" in `docs/migrations/index.md`.

## Gotchas

- Add projects to `Curiosus.Utils.sln` by hand (copy the entries of a sibling project): `dotnet sln add` also adds
  `x64`/`x86` platforms to every project of the solution.
- The repository is a GitHub fork of `siisltd/Curiosity.Utils`, so `gh pr create` targets the parent by default:
  pass `--repo curiosus-dev/Curiosus.Utils` (or run `gh repo set-default curiosus-dev/Curiosus.Utils` once).
- On `net10.0` `BackgroundService.StartAsync` runs `ExecuteAsync` on a background task, so timers it creates may not
  exist yet when a test advances a `FakeTimeProvider` right after `StartAsync`: advance repeatedly until the loop reacts.
