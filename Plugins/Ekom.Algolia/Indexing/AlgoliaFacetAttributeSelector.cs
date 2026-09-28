using Ekom.Algolia.Mappers;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;

namespace Ekom.Algolia.Indexing;

internal interface IAlgoliaFacetAttributeSelector
{
    IReadOnlyCollection<string> GetFacetAttributes(AlgoliaIndexingOptions indexing);
}

internal sealed class AlgoliaFacetAttributeSelector : IAlgoliaFacetAttributeSelector
{
    private readonly IServiceScopeFactory _scopeFactory;

    public AlgoliaFacetAttributeSelector(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
    }

    public IReadOnlyCollection<string> GetFacetAttributes(AlgoliaIndexingOptions indexing)
    {
        using var scope = _scopeFactory.CreateScope();
        var filterableAliases = scope.ServiceProvider.GetRequiredService<IMetafieldService>()
            .GetMetafields()
            .Where(field => field.Filterable)
            .Select(field => field.Alias);

        return Merge(indexing.FacetAttributes, filterableAliases);
    }

    internal static IReadOnlyCollection<string> Merge(
        IEnumerable<string> configuredAttributes,
        IEnumerable<string> filterableAliases)
    {
        var attributes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var attribute in configuredAttributes)
        {
            var alias = ProductIndexMapper.ConfiguredField.Parse(attribute).Alias;
            if (!string.IsNullOrWhiteSpace(alias))
                attributes[alias] = attribute;
        }

        foreach (var alias in filterableAliases)
        {
            if (!string.IsNullOrWhiteSpace(alias) && !attributes.ContainsKey(alias.Trim()))
                attributes[alias.Trim()] = $"metafield:{alias.Trim()}";
        }

        return attributes.Values.ToList();
    }
}
