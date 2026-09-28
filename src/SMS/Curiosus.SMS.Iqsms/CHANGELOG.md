# Changelog

## [2.0.0] - 2026-09-27

### Changed

- **Breaking:** package renamed from `Curiosity.SMS.Iqsms` to `Curiosus.SMS.Iqsms` and now published by the [curiosus-dev](https://www.nuget.org/profiles/curiosus-dev) organization. Namespaces, assemblies and types were renamed accordingly (`Curiosity*` → `Curiosus*`): replace `Curiosity` with `Curiosus` in your code to migrate.
- Dropped `netstandard2.1` support. Now multi-targeting `net9.0` and `net10.0`.
- Upgraded `RestSharp` up to `114.0.0` (fixes known vulnerabilities).

## [1.1.0]

Upgraded dependencies.

## [1.0.3]

Convert response with text "invalid mobile phone" to delivery error.

## [1.0.2]

Detect no money message.

## [1.0.0] - 2023-08-04

First release.
