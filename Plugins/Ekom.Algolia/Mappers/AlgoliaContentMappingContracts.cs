using Ekom.Algolia.Models.Indexing;
using Umbraco.Cms.Core.Models;

namespace Ekom.Algolia.Mappers;

public sealed record AlgoliaContentEnrichmentContext(
    IContent Content,
    string? Culture,
    string BaseIndexName,
    IReadOnlyDictionary<string, AlgoliaContentFieldTransform>? AllowedPropertyAliases);

public interface IAlgoliaContentEnricher
{
    void Enrich(AlgoliaContentRecord record, AlgoliaContentEnrichmentContext ctx);
    int Order => 0;
}

public sealed record AlgoliaContentPropertyContext(
    IContent Content,
    IProperty Property,
    string? PropertyCulture,
    string? TargetCulture,
    string BaseIndexName);

public interface IAlgoliaContentPropertyValueConverter
{
    bool CanHandle(AlgoliaContentPropertyContext ctx);
    object? Convert(AlgoliaContentPropertyContext ctx, object? source);
    int Order => 0;
}
