using Microsoft.Extensions.Options;

namespace Ekom.Algolia;

internal sealed class IndexNameBuilder
{
    private readonly AlgoliaOptions _options;

    public IndexNameBuilder(IOptions<AlgoliaOptions> options)
    {
        _options = options.Value;
    }

    public string BuildPrimary(
        string entity,
        AlgoliaResolvedStore? store = null,
        string? localeOverride = null,
        string? currencyOverride = null)
        => Build(AlgoliaIndexKind.Primary, entity, store, replica: null, localeOverride, currencyOverride);

    public string BuildReplica(
        string entity,
        AlgoliaSortedReplicaOptions replica,
        AlgoliaResolvedStore? store = null,
        string? localeOverride = null,
        string? currencyOverride = null)
        => Build(AlgoliaIndexKind.Replica, entity, store, replica, localeOverride, currencyOverride);

    public string BuildQuerySuggestions(
        string entity,
        AlgoliaResolvedStore? store = null,
        string? localeOverride = null,
        string? currencyOverride = null)
        => Build(AlgoliaIndexKind.QuerySuggestions, entity, store, replica: null, localeOverride, currencyOverride);

    public string Build(
        AlgoliaIndexKind kind,
        string entity,
        AlgoliaResolvedStore? store,
        AlgoliaSortedReplicaOptions? replica,
        string? localeOverride = null,
        string? currencyOverride = null)
    {
        var env = string.IsNullOrWhiteSpace(_options.Environment) ? "prod" : _options.Environment;

        var tokenKind = kind switch
        {
            AlgoliaIndexKind.Primary => "primary",
            AlgoliaIndexKind.Replica => "replica",
            AlgoliaIndexKind.QuerySuggestions => "query_suggestions",
            _ => "primary"
        };

        var name = tokenKind + "." + env;

        if (!string.IsNullOrWhiteSpace(store?.Alias))
            name += "." + store.Alias;

        name += "." + entity;

        if (replica != null && !string.IsNullOrWhiteSpace(replica.Attribute))
        {
            var dir = replica.Direction == AlgoliaSortDirection.Desc ? "desc" : "asc";
            name += $"_sorted_by_{dir}_{replica.Attribute}";
        }

        var locale = localeOverride ?? store?.Locale;
        if (!string.IsNullOrWhiteSpace(locale))
            name += "." + locale;

        var currency = currencyOverride ?? store?.Currency;
        if (!string.IsNullOrWhiteSpace(currency))
            name += "." + currency;

        return name.ToLowerInvariant();
    }
}
