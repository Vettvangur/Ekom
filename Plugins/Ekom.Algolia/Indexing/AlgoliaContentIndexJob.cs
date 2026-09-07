namespace Ekom.Algolia.Indexing;

internal sealed record AlgoliaContentIndexJob(
    AlgoliaContentIndexJobType Type,
    IReadOnlyCollection<int> NodeIds,
    IReadOnlyCollection<Guid> NodeKeys,
    string? IndexName = null,
    TaskCompletionSource? Completion = null);

internal enum AlgoliaContentIndexJobType
{
    Upsert,
    Delete,
    Rebuild
}
