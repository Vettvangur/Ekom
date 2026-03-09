using Ekom.Algolia.Models.Indexing;
using Ekom.Models;

namespace Ekom.Algolia.Mappers;

public interface IAlgoliaProductIndexMapper
{
    AlgoliaProductRecord? Map(IProduct product, AlgoliaStoreOptions store, string baseIndexName);
}

public interface IAlgoliaProductEnricher
{
    int Order { get; }
    void Enrich(AlgoliaProductRecord record, AlgoliaProductEnrichmentContext ctx);
}

public interface IAlgoliaProductFieldConverter
{
    int Order { get; }
    bool CanHandle(AlgoliaProductFieldContext ctx);
    object? Convert(AlgoliaProductFieldContext ctx, object? source);
}

public sealed record AlgoliaProductEnrichmentContext(
    IProduct Product,
    AlgoliaStoreOptions Store,
    string BaseIndexName,
    IReadOnlyDictionary<string, AlgoliaFieldTransform> AllowedProperties);

public sealed record AlgoliaProductFieldContext(
    IProduct Product,
    AlgoliaStoreOptions Store,
    string PropertyAlias,
    string BaseIndexName);

public enum AlgoliaFieldTransform
{
    None,
    UnixSeconds,
    UnixMilliseconds
}
