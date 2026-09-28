# Curiosus.Cache.MemoryCache

[![NuGet](https://img.shields.io/nuget/v/Curiosus.Cache.MemoryCache)](https://www.nuget.org/packages/Curiosus.Cache.MemoryCache) [![Downloads](https://img.shields.io/nuget/dt/Curiosus.Cache.MemoryCache)](https://www.nuget.org/packages/Curiosus.Cache.MemoryCache) [![Coverage](https://img.shields.io/endpoint?url=https://raw.githubusercontent.com/curiosus-dev/Curiosus.Utils/badges/Curiosus.Cache.MemoryCache.json)](https://github.com/curiosus-dev/Curiosus.Utils/actions/workflows/release-packages.yml)

`GetOrCreate` extensions for `IMemoryCache` that take the expiration (or `MemoryCacheEntryOptions`) right in the call,
with sync, `Task` and `ValueTask` factories.

## Installation

```bash
dotnet add package Curiosus.Cache.MemoryCache
```

## Usage

```csharp
using Curiosus.Cache.MemoryCache;

public class CurrencyRates
{
    private readonly IMemoryCache _cache;
    private readonly IRatesApi _api;

    public CurrencyRates(IMemoryCache cache, IRatesApi api)
    {
        _cache = cache;
        _api = api;
    }

    public Task<decimal> GetRateAsync(string currency)
    {
        return _cache.GetOrCreateAsync(
            $"rate:{currency}",
            () => _api.LoadRateAsync(currency),
            TimeSpan.FromMinutes(10));
    }
}
```

Available overloads:

- `GetOrCreate(key, Func<T>, TimeSpan | MemoryCacheEntryOptions)`
- `GetOrCreateAsync(key, Func<Task<T>>, TimeSpan | MemoryCacheEntryOptions)`
- `GetOrCreateValueAsync(key, Func<ValueTask<T>>, TimeSpan | MemoryCacheEntryOptions)`
- `GetOrCreateValueAsync(key, Func<ICacheEntry, ValueTask<T>>)` — configure the entry inside the factory

Except for the `ICacheEntry` overload, values are stored in an internal wrapper: read them only through these
methods (not `cache.Get<T>`), and note that `null` results are not cached, so the factory runs again on the next call.
Concurrent misses are not deduplicated: several callers may run the factory at the same time.

## See also

- [Curiosus.Tools](https://www.nuget.org/packages/Curiosus.Tools) — basic helpers
- [Curiosus.Utils](https://github.com/curiosus-dev/Curiosus.Utils) — all packages
