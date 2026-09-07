namespace Ekom.Algolia.Indexing;

internal sealed record AlgoliaProductIndexJob(
    AlgoliaProductIndexJobType Type,
    string StoreAlias,
    IReadOnlyCollection<Guid> ProductKeys,
    TaskCompletionSource? Completion = null);

internal enum AlgoliaProductIndexJobType
{
    Upsert,
    Delete,
    RebuildStore
}
