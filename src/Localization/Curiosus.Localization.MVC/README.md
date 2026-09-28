# Curiosus.Localization.MVC

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Localization.MVC)](https://www.nuget.org/packages/Curiosus.Localization.MVC) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Localization.MVC)](https://www.nuget.org/packages/Curiosus.Localization.MVC) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Localization.MVC.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

ASP.NET Core MVC add-on for [Curiosus.Localization](https://www.nuget.org/packages/Curiosus.Localization):
returns source-text based translations as `LocalizedHtmlString` for Razor views and adds plural-form helpers
for `IViewLocalizer`.

## Installation

```bash
dotnet add package Curiosus.Localization.MVC
```

## Usage

`MvcLocalizerCore` extends `LocalizerCore` with `GetHtml`, which returns a `LocalizedHtmlString` that Razor
renders with HTML encoding of the arguments:

```csharp
public static class ViewLNG
{
    private static readonly LocalizationOptions Options = new();
    private static readonly MvcLocalizerCore Localizer = new(typeof(ViewLNG).Assembly, Options);

    public static LocalizedHtmlString Html(string source, params object[] arguments)
    {
        return Localizer.GetHtml(Options.Prefix, source, arguments);
    }
}
```

```cshtml
<h1>@ViewLNG.Html("Добро пожаловать, {0}!", Model.UserName)</h1>
```

Unlike `LocalizerCore.Get`, `GetHtml` always looks the text up in the resources (falling back to the source text),
including for the default language.

Plural forms with the standard `IViewLocalizer` (looks up `key_1`, `key_2` or `key_5`):

```cshtml
@inject IViewLocalizer Localizer
<span>@Localizer.GetQuantityString("OrdersCount", Model.Count, Model.Count)</span>
```

## See also

- [Curiosus.Localization](https://www.nuget.org/packages/Curiosus.Localization) — core localizer and options
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
