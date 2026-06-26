namespace Ekom.Algolia.Indexing;

public interface IAlgoliaContentIndexService
{
    Task UpdateByIdsAsync(IReadOnlyCollection<int> nodeIds, CancellationToken ct = default);
    Task DeleteByKeysAsync(IReadOnlyCollection<Guid> nodeKeys, CancellationToken ct = default);
    Task RebuildAsync(string? indexName = null, CancellationToken ct = default);
}

internal sealed class AlgoliaContentIndexService : IAlgoliaContentIndexService
{
    private readonly IAlgoliaContentIndexQueue _queue;

    public AlgoliaContentIndexService(IAlgoliaContentIndexQueue queue)
    {
        _queue = queue;
    }

    public Task UpdateByIdsAsync(IReadOnlyCollection<int> nodeIds, CancellationToken ct = default)
    {
        if (nodeIds.Count == 0)
            return Task.CompletedTask;

        return EnqueueAsync(new AlgoliaContentIndexJob(AlgoliaContentIndexJobType.Upsert, nodeIds, []), ct);
    }

    public Task DeleteByKeysAsync(IReadOnlyCollection<Guid> nodeKeys, CancellationToken ct = default)
    {
        if (nodeKeys.Count == 0)
            return Task.CompletedTask;

        return EnqueueAsync(new AlgoliaContentIndexJob(AlgoliaContentIndexJobType.Delete, [], nodeKeys), ct);
    }

    public Task RebuildAsync(string? indexName = null, CancellationToken ct = default)
        => EnqueueAsync(new AlgoliaContentIndexJob(AlgoliaContentIndexJobType.Rebuild, [], [], indexName), ct);

    private async Task EnqueueAsync(AlgoliaContentIndexJob job, CancellationToken ct)
    {
        if (_queue.TryEnqueue(job))
            return;

        await _queue.EnqueueAsync(job, ct).ConfigureAwait(false);
    }
}
