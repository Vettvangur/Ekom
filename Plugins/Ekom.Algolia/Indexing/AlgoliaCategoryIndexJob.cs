namespace Ekom.Algolia.Indexing;

internal sealed record AlgoliaCategoryIndexJob(
    AlgoliaCategoryIndexJobType Type,
    string StoreAlias,
    IReadOnlyCollection<Guid> CategoryKeys,
    TaskCompletionSource? Completion = null);

internal enum AlgoliaCategoryIndexJobType
{
    Upsert,
    Delete,
    RebuildStore
}
