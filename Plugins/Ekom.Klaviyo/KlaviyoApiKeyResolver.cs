using Microsoft.Extensions.Options;

namespace Ekom.Klaviyo;

internal interface IKlaviyoApiKeyResolver
{
    /// <summary>
    /// Resolves the API key for a given store alias.
    /// If store alias is null/empty, returns global key (if configured).
    /// Throws if no key is available.
    /// </summary>
    string ResolveRequired(string? storeAlias);
}

internal sealed class KlaviyoApiKeyResolver : IKlaviyoApiKeyResolver
{
    private readonly KlaviyoOptions _opt;

    public KlaviyoApiKeyResolver(IOptions<KlaviyoOptions> opt)
    {
        _opt = opt.Value;
    }

    public string ResolveRequired(string? storeAlias)
    {
        string? key = null;

        if (!string.IsNullOrWhiteSpace(storeAlias))
        {
            var store = _opt.Stores?.FirstOrDefault(s =>
                string.Equals(s.Alias, storeAlias, StringComparison.OrdinalIgnoreCase));

            key = store?.PrivateApiKey;
        }

        key ??= _opt.PrivateApiKey;

        if (string.IsNullOrWhiteSpace(key))
            throw new InvalidOperationException(
                $"Klaviyo API key is missing. Configure either Ekom:Klaviyo:PrivateApiKey or Ekom:Klaviyo:Stores:{storeAlias}:PrivateApiKey.");

        return key;
    }
}
