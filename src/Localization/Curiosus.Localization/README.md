# Curiosus.Localization

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Localization)](https://www.nuget.org/packages/Curiosus.Localization) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Localization)](https://www.nuget.org/packages/Curiosus.Localization) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Localization.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Source-text based localization: code uses the default-language text itself as the key, translations live in `.resx`
files under hashed keys and are cached per culture. Also adds plural-form helpers for `IStringLocalizer`.

## Installation

```bash
dotnet add package Curiosus.Localization
```

## Usage

Create `Resources/Strings.resx` (and `Strings.en.resx` etc.) in your assembly and wrap `LocalizerCore` in a static helper:

```csharp
internal static class LNG
{
    private static readonly LocalizationOptions Options = new();
    private static readonly LocalizerCore Localizer = new(typeof(LNG).Assembly, Options);

    public static string Get(string source, params object[] arguments)
    {
        return Localizer.Get(Options.Prefix, source, false, arguments);
    }
}

var message = LNG.Get("Файл {0} не найден", fileName);
```

When `CurrentUICulture` matches `LocalizationOptions.DefaultLanguage` (`ru` by default) the source text is returned
as is. Otherwise the text is looked up in the resources by the key `ResourceKeyGenerator.Generate(prefix, source)`
(`<prefix>_<xxHash of the text>`), falling back to the source text. Set `CheckResourceFiles = true` to fail fast when
resources for `SupportedLanguages` are missing.

Plural forms (Slavic rules: `_1`, `_2`, `_5` suffixes) with any `IStringLocalizer`:

```csharp
// looks up "FilesCount_1", "FilesCount_2" or "FilesCount_5"
var text = localizer.GetQuantityString("FilesCount", count, count);
```

## See also

- [Curiosus.Localization.MVC](https://www.nuget.org/packages/Curiosus.Localization.MVC) — the same for Razor views
- [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools) — xxHash implementation used for resource keys
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
