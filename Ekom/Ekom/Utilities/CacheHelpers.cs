using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using System.Security.Cryptography;
using System.Text;

namespace Ekom.Utilities;

public static class CacheHelpers
{
    public static string Sha256(string input)
    {
        if (string.IsNullOrEmpty(input)) return "empty";
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes);
    }

    /// <summary>
    /// Synchronous single-flight cache:
    /// stores Lazy&lt;T&gt; so only one thread executes the factory per key.
    /// Removes the entry on exception so bad results don't poison the cache.
    /// </summary>
    public static T GetOrCreateSingleFlight<T>(
        string key,
        Func<T> factory,
        TimeSpan ttl)
    {
        var cache = Configuration.Resolver.GetService<IMemoryCache>();
        if (cache is null)
            return factory();

        var lazy = cache.GetOrCreate(key, entry =>
        {
            entry.AbsoluteExpirationRelativeToNow = ttl;
            return new Lazy<T>(() => factory(), LazyThreadSafetyMode.ExecutionAndPublication);
        });

        try
        {
            return lazy!.Value;
        }
        catch
        {
            cache.Remove(key);
            throw;
        }
    }
}
