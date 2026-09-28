# Curiosus.DataAnnotations

[![NuGet](https://img.shields.io/nuget/v/Curiosus.DataAnnotations)](https://www.nuget.org/packages/Curiosus.DataAnnotations) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.DataAnnotations)](https://www.nuget.org/packages/Curiosus.DataAnnotations) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.DataAnnotations.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

Small additions to `System.ComponentModel.DataAnnotations`: range attributes bound to an enum, a one-call
object validator and a helper that reads `[StringLength]` limits.

## Installation

```bash
dotnet add package Curiosus.DataAnnotations
```

## Usage

```csharp
using System.ComponentModel.DataAnnotations;
using Curiosus.DataAnnotations;

public enum OrderStatus
{
    New = 1,
    Paid = 2,
    Shipped = 3,
}

public class OrderRequest
{
    [StringLength(64)]
    public string Title { get; set; } = null!;

    // valid range is 1..max value of the enum; use EnumRange(0, typeof(...)) to allow 0
    [EnumRange(typeof(OrderStatus))]
    public OrderStatus Status { get; set; }
}

var request = new OrderRequest { Title = "Books", Status = (OrderStatus)10 };

if (!DataAnnotationsValidator.TryValidate(request, out var results))
{
    foreach (var result in results) Console.WriteLine(result.ErrorMessage);
}

int? maxTitleLength = request.GetPropertyMaxLength(nameof(OrderRequest.Title)); // 64
```

`EnumRangeAttribute` is for `int`-based enums, `EnumLongRangeAttribute` for `long`-based ones.
`DataAnnotationsValidator.TryValidate` validates all properties of the object (not nested objects).

## See also

- [Curiosus.Configuration](https://www.nuget.org/packages/Curiosus.Configuration) — validation of configuration options
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
