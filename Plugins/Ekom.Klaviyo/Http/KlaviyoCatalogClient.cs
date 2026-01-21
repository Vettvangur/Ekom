using Ekom.Klaviyo.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace Ekom.Klaviyo.Http;

internal interface IKlaviyoCatalogClient
{
    Task BulkUpsertCatalogItemsAsync(IReadOnlyList<KlaviyoProductItem> items, KlaviyoDeleteMode deleteMode, CancellationToken ct = default);
    Task BulkCreateCatalogItemsAsync(IReadOnlyList<KlaviyoProductItem> items, CancellationToken ct = default);
    Task BulkDeleteCatalogItemsAsync(IReadOnlyList<KlaviyoProductItem> items, CancellationToken ct = default);
}

internal sealed class KlaviyoCatalogClient : IKlaviyoCatalogClient
{
    private readonly KlaviyoHttpClient _http;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoCatalogClient> _logger;

    public KlaviyoCatalogClient(
        KlaviyoHttpClient http,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoCatalogClient> logger)
    {
        _http = http;
        _opt = options.Value;
        _logger = logger;
    }

    // ----------------------------
    // CREATE
    // ----------------------------
    public async Task BulkCreateCatalogItemsAsync(
        IReadOnlyList<KlaviyoProductItem> items,
        CancellationToken ct = default)
    {
        if (!IsCatalogApiEnabled() || items.Count == 0)
            return;

        EnsureBatchSize(items);

        _logger.LogDebug("Klaviyo: bulk CREATE {Count} catalog items", items.Count);

        var payload = BuildCreatePayload(items);
        var body = await _http.PostAsync("/api/catalog-item-bulk-create-jobs", payload, ct);

        LogJobAccepted("CREATE", body);
    }

    // ----------------------------
    // UPDATE (UPSERT semantics)
    // ----------------------------
    public async Task BulkUpsertCatalogItemsAsync(
        IReadOnlyList<KlaviyoProductItem> items,
        KlaviyoDeleteMode deleteMode,
        CancellationToken ct = default)
    {
        if (!IsCatalogApiEnabled() || items.Count == 0)
            return;

        EnsureBatchSize(items);

        _logger.LogDebug("Klaviyo: bulk UPDATE {Count} catalog items", items.Count);

        IReadOnlyList<KlaviyoProductItem>? hardDeleteItems = null;
        IReadOnlyList<KlaviyoProductItem> upsertItems = items;

        if (deleteMode == KlaviyoDeleteMode.Hard)
        {
            hardDeleteItems = items.Where(i => !i.Published).ToList();
            upsertItems = items.Where(i => i.Published).ToList();
        }

        if (hardDeleteItems is { Count: > 0 })
        {
            await BulkDeleteCatalogItemsAsync(hardDeleteItems, ct);
        }

        if (upsertItems.Count == 0)
            return;

        var payload = BuildUpdatePayload(upsertItems);
        var body = await _http.PostAsync("/api/catalog-item-bulk-update-jobs", payload, ct);

        var job = TryParseJob(body);
        if (job is null)
        {
            _logger.LogWarning("Klaviyo UPDATE job accepted but response could not be parsed. Body={Body}", body);
            return;
        }

        _logger.LogDebug("Klaviyo UPDATE job accepted. JobId={JobId}, Status={Status}", job.Value.JobId, job.Value.Status);

        var finalBody = body;

        // Optional polling (keep your current behavior)
        if (string.Equals(job.Value.Status, "processing", StringComparison.OrdinalIgnoreCase))
        {
            var polled = await PollCatalogUpdateJobAsync(job.Value.JobId, ct);
            if (!string.IsNullOrWhiteSpace(polled))
                finalBody = polled!;
        }

        // Fallback create for missing items
        var missingItemIds = ExtractMissingItemIds(finalBody);
        if (missingItemIds.Count == 0)
            return;

        var toCreate = upsertItems
            .Where(i => missingItemIds.Contains(BuildCatalogItemId(i.ExternalId)))
            .ToList();

        if (toCreate.Count == 0)
            return;

        _logger.LogDebug(
            "Klaviyo: UPDATE reported {Count} missing items; running CREATE fallback.",
            toCreate.Count);

        await BulkCreateCatalogItemsAsync(toCreate, ct);
    }

    // ----------------------------
    // HARD DELETE
    // ----------------------------
    public async Task BulkDeleteCatalogItemsAsync(
        IReadOnlyList<KlaviyoProductItem> items,
        CancellationToken ct = default)
    {
        if (!IsCatalogApiEnabled() || items.Count == 0)
            return;

        EnsureBatchSize(items);

        _logger.LogDebug("Klaviyo: HARD delete {Count} catalog items", items.Count);

        var payload = BuildHardDeletePayload(items);
        var body = await _http.PostAsync("/api/catalog-item-bulk-delete-jobs", payload, ct);

        LogJobAccepted("DELETE", body);
    }

    private bool IsCatalogApiEnabled()
        => _opt.Enabled
           && _opt.Catalog.Enabled
           && _opt.Catalog.Method == KlaviyoCatalogMethods.SyncEvents;

    // ----------------------------
    // Payload builders
    // ----------------------------
    private static object BuildCreatePayload(IReadOnlyList<KlaviyoProductItem> items)
        => BuildBulkPayload("catalog-item-bulk-create-job", items, create: true);

    private static object BuildUpdatePayload(IReadOnlyList<KlaviyoProductItem> items)
        => BuildBulkPayload("catalog-item-bulk-update-job", items, create: false);

    private static object BuildBulkPayload(string jobType, IReadOnlyList<KlaviyoProductItem> items, bool create)
        => new
        {
            data = new
            {
                type = jobType,
                attributes = new
                {
                    items = new
                    {
                        data = items.Select(i => BuildCatalogItemData(i, create))
                    }
                }
            }
        };

    private static object BuildCatalogItemData(KlaviyoProductItem i, bool create)
    {
        if (create)
        {
            return new
            {
                type = "catalog-item",
                attributes = BuildCatalogItemAttributes(i, includeExternalId: true)
            };
        }

        return new
        {
            type = "catalog-item",
            id = BuildCatalogItemId(i.ExternalId),
            attributes = BuildCatalogItemAttributes(i, includeExternalId: false)
        };
    }

    private static object BuildCatalogItemAttributes(KlaviyoProductItem i, bool includeExternalId)
    {
        var a = new Dictionary<string, object?>()
        {
            ["title"] = i.Title,
            ["description"] = i.Description,
            ["price"] = i.Price,
            ["url"] = i.Url,
            ["image_full_url"] = i.ImageFullUrl,
            ["published"] = i.Published,
            ["custom_metadata"] = MergeMetadata(i)
        };

        if (includeExternalId)
            a["external_id"] = i.ExternalId;

        return a;
    }

    private static object BuildHardDeletePayload(IReadOnlyList<KlaviyoProductItem> items)
        => new
        {
            data = new
            {
                type = "catalog-item-bulk-delete-job",
                attributes = new
                {
                    items = new
                    {
                        data = items.Select(item => new
                        {
                            type = "catalog-item",
                            id = BuildCatalogItemId(item.ExternalId)
                        })
                    }
                }
            }
        };

    // ----------------------------
    // Helpers
    // ----------------------------
    private static Dictionary<string, object?> MergeMetadata(KlaviyoProductItem i)
    {
        var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
        {
            ["store_key"] = i.StoreAlias,
            ["sku"] = i.Sku,
            ["currency"] = i.Currency,
            ["summary"] = i.Summary
        };

        if (i.CustomMetadata is not null)
        {
            foreach (var kv in i.CustomMetadata)
                d[kv.Key] = kv.Value;
        }

        return d;
    }

    private void EnsureBatchSize(IReadOnlyList<KlaviyoProductItem> items)
    {
        var max = _opt.Catalog.Dispatching.MaxBatchSize <= 0 ? 100 : _opt.Catalog.Dispatching.MaxBatchSize;
        if (items.Count > max)
            throw new InvalidOperationException($"Catalog bulk job supports up to {max} items per request.");
    }

    internal static string BuildCatalogItemId(string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new InvalidOperationException("ExternalId is required to build the Klaviyo catalog item id.");

        return $"$custom:::$default:::{externalId}";
    }

    private async Task<string?> PollCatalogUpdateJobAsync(string jobId, CancellationToken ct, TimeSpan? maxDuration = null)
    {
        var deadline = DateTimeOffset.UtcNow + (maxDuration ?? TimeSpan.FromMinutes(5));
        var delay = TimeSpan.FromSeconds(1);
        var maxDelay = TimeSpan.FromSeconds(15);

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(delay, ct);

            var body = await _http.GetAsync($"/api/catalog-item-bulk-update-jobs/{jobId}/", ct);

            using var doc = JsonDocument.Parse(body);
            var attrs = doc.RootElement.GetProperty("data").GetProperty("attributes");

            var status = attrs.GetProperty("status").GetString();
            var total = attrs.GetProperty("total_count").GetInt32();
            var completed = attrs.GetProperty("completed_count").GetInt32();
            var failed = attrs.GetProperty("failed_count").GetInt32();

            _logger.LogDebug(
                "Klaviyo UPDATE job {JobId}: status={Status}, completed={Completed}/{Total}, failed={Failed}",
                jobId, status, completed, total, failed);

            if (!string.Equals(status, "processing", StringComparison.OrdinalIgnoreCase))
                return body;

            delay = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, maxDelay.TotalSeconds));
        }

        _logger.LogInformation("Klaviyo UPDATE job {JobId} still processing after max polling window.", jobId);
        return null;
    }

    private static HashSet<string> ExtractMissingItemIds(string jobBody)
    {
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        using var doc = JsonDocument.Parse(jobBody);

        if (!doc.RootElement.TryGetProperty("data", out var data) ||
            !data.TryGetProperty("attributes", out var attrs) ||
            !attrs.TryGetProperty("errors", out var errors) ||
            errors.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        foreach (var e in errors.EnumerateArray())
        {
            var detail = e.TryGetProperty("detail", out var d) ? d.GetString() : null;

            var isMissing =
                !string.IsNullOrWhiteSpace(detail) &&
                (detail.Contains("not found", StringComparison.OrdinalIgnoreCase) ||
                 detail.Contains("does not exist", StringComparison.OrdinalIgnoreCase) ||
                 detail.Contains("could not be found", StringComparison.OrdinalIgnoreCase));

            if (!isMissing)
                continue;

            if (e.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
            {
                if (meta.TryGetProperty("id", out var idEl))
                {
                    var id = idEl.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                        result.Add(id);
                }

                if (meta.TryGetProperty("external_id", out var extEl))
                {
                    var ext = extEl.GetString();
                    if (!string.IsNullOrWhiteSpace(ext))
                        result.Add(BuildCatalogItemId(ext));
                }
            }
        }

        return result;
    }

    private void LogJobAccepted(string operation, string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var jobId = doc.RootElement.GetProperty("data").GetProperty("id").GetString();

            _logger.LogDebug("Klaviyo {Operation} job accepted. JobId={JobId}", operation, jobId);
        }
        catch
        {
            _logger.LogWarning("Klaviyo {Operation} job accepted, but job id could not be parsed. Raw body: {Body}", operation, body);
        }
    }

    private static (string JobId, string Status)? TryParseJob(string body)
    {
        try
        {
            using var doc = JsonDocument.Parse(body);
            var data = doc.RootElement.GetProperty("data");

            return (
                JobId: data.GetProperty("id").GetString()!,
                Status: data.GetProperty("attributes").GetProperty("status").GetString()!
            );
        }
        catch
        {
            return null;
        }
    }
}
