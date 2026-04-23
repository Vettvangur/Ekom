using Ekom.Algolia.Models.Indexing;
using Ekom.Models;

namespace Ekom.Algolia.Mappers;

public interface IAlgoliaCategoryIndexMapper
{
    AlgoliaCategoryRecord? Map(ICategory category, AlgoliaResolvedStore store, string baseIndexName);
}

public interface IAlgoliaCategoryEnricher
{
    int Order { get; }
    void Enrich(AlgoliaCategoryRecord record, AlgoliaCategoryEnrichmentContext ctx);
}

public sealed record AlgoliaCategoryEnrichmentContext(
    ICategory Category,
    AlgoliaResolvedStore Store,
    string BaseIndexName);
