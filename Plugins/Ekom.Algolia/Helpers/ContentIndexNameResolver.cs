namespace Ekom.Algolia;

internal sealed class ContentIndexNameResolver
{
    private readonly AlgoliaOptions _options;

    public ContentIndexNameResolver(Microsoft.Extensions.Options.IOptions<AlgoliaOptions> options)
    {
        _options = options.Value;
    }

    public string Resolve(string indexName, string culture)
    {
        var env = string.IsNullOrWhiteSpace(_options.Environment) ? "prod" : _options.Environment.Trim();
        return $"{indexName.Trim()}.{env}.{culture.Trim()}".ToLowerInvariant();
    }
}
