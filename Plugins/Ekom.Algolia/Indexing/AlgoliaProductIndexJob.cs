namespace Ekom.Algolia.Indexing;

internal sealed record AlgoliaProductIndexJob(
    AlgoliaProductIndexJobType Type,
    string StoreAlias,
    IReadOnlyCollection<Guid> ProductKeys);

internal enum AlgoliaProductIndexJobType
{
    Upsert,
    Delete,
    RebuildStore
}
