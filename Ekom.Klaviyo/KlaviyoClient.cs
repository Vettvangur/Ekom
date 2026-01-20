using Ekom.Klaviyo.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ekom.Klaviyo;

internal interface IKlaviyoClient
{
    Task BulkUpsertCatalogItemsAsync(IReadOnlyList<KlaviyoProductItem> items, CancellationToken ct = default);
    Task BulkCreateCatalogItemsAsync(IReadOnlyList<KlaviyoProductItem> items, CancellationToken ct = default);
    Task BulkDeleteCatalogItemsAsync(IReadOnlyList<KlaviyoProductItem> items, CancellationToken ct = default);
}
internal sealed class KlaviyoClient : IKlaviyoClient
{
    private readonly HttpClient _http;
    private readonly KlaviyoOptions _opt;
    private readonly ILogger<KlaviyoClient> _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public KlaviyoClient(
        HttpClient http,
        IOptions<KlaviyoOptions> options,
        ILogger<KlaviyoClient> logger)
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
        if (!_opt.Enabled || !_opt.ProductEvents.Enabled || items.Count == 0)
            return;

        EnsureBatchSize(items);

        _logger.LogDebug(
            "Klaviyo: bulk CREATE {Count} catalog items",
            items.Count);

        var payload = BuildCreatePayload(items);
        var body = await PostAsync(
            "/api/catalog-item-bulk-create-jobs",
            payload,
            ct);

        var job = TryParseJob(body);

        if (job is null)
        {
            _logger.LogWarning(
                "Klaviyo CREATE job accepted but response could not be parsed. Body={Body}",
                body);

            return;
        }

        _logger.LogDebug(
            "Klaviyo CREATE job accepted. JobId={JobId}, Status={Status}",
            job.Value.JobId,
            job.Value.Status);

        // ONLY poll if Klaviyo says it's processing
        //if (string.Equals(job.Value.Status, "processing", StringComparison.OrdinalIgnoreCase))
        //{
        //    body = await PollCatalogCreateJobAsync(job.Value.JobId, ct);
        //}

        LogJobAccepted("CREATE", body);
    }

    // ----------------------------
    // UPDATE (UPSERT semantics)
    // ----------------------------
    public async Task BulkUpsertCatalogItemsAsync(
        IReadOnlyList<KlaviyoProductItem> items,
        CancellationToken ct = default)
    {
        if (!_opt.Enabled || !_opt.ProductEvents.Enabled || items.Count == 0)
            return;

        EnsureBatchSize(items);

        _logger.LogDebug("Klaviyo: bulk UPDATE {Count} catalog items", items.Count);

        IReadOnlyList<KlaviyoProductItem>? hardDeleteItems = null;

        if (_opt.ProductEvents.DeleteMode == KlaviyoDeleteMode.Hard)
        {
            hardDeleteItems = items
                .Where(i => !i.Published)
                .ToList();

            items = items
                .Where(i => i.Published)
                .ToList();
        }

        if (hardDeleteItems != null && hardDeleteItems.Any())
        {
            await BulkDeleteCatalogItemsAsync(hardDeleteItems, ct);
        }

        if (items.Any())
        {
            var payload = BuildUpdatePayload(items);

            var body = await PostAsync("/api/catalog-item-bulk-update-jobs", payload, ct);

            var job = TryParseJob(body);

            if (job is null)
            {
                _logger.LogWarning("Klaviyo UPDATE job accepted but response could not be parsed. Body={Body}", body);
                return;
            }

            _logger.LogDebug("Klaviyo UPDATE job accepted. JobId={JobId}, Status={Status}", job.Value.JobId, job.Value.Status);

            var finalBody = body;

            if (string.Equals(job.Value.Status, "processing", StringComparison.OrdinalIgnoreCase))
            {
                var polled = await PollCatalogUpdateJobAsync(job.Value.JobId, ct);
                if (!string.IsNullOrWhiteSpace(polled))
                    finalBody = polled!;
            }

            var missingItemIds = ExtractMissingItemIds(finalBody);

            if (missingItemIds.Count == 0)
                return;

            var toCreate = items
                .Where(i => missingItemIds.Contains(BuildCatalogItemId(i.ExternalId)))
                .ToList();

            if (toCreate.Count == 0)
                return;

            _logger.LogDebug(
                "Klaviyo: UPDATE reported {Count} missing items; running CREATE fallback.",
                toCreate.Count);

            await BulkCreateCatalogItemsAsync(toCreate, ct);

        }
    }

    // ----------------------------
    // Delete
    // ----------------------------

    public async Task BulkDeleteCatalogItemsAsync(
        IReadOnlyList<KlaviyoProductItem> items,
        CancellationToken ct = default)
    {
        if (!_opt.Enabled || !_opt.ProductEvents.Enabled || items.Count == 0)
            return;

        EnsureBatchSize(items);

        // HARD DELETE
        _logger.LogDebug(
            "Klaviyo: HARD delete {Count} catalog items",
            items.Count);

        var payload = BuildHardDeletePayload(items);

        var body = await PostAsync(
            "/api/catalog-item-bulk-delete-jobs",
            payload,
            ct);

        LogJobAccepted("DELETE", body);
    }

    // ----------------------------
    // Payload builders
    // ----------------------------

    private static object BuildCreatePayload(IReadOnlyList<KlaviyoProductItem> items)
        => BuildBulkPayload(
            jobType: "catalog-item-bulk-create-job",
            items: items,
            create: true);

    private static object BuildUpdatePayload(IReadOnlyList<KlaviyoProductItem> items)
        => BuildBulkPayload(
            jobType: "catalog-item-bulk-update-job",
            items: items,
            create: false);

    private static object BuildBulkPayload(
        string jobType,
        IReadOnlyList<KlaviyoProductItem> items,
        bool create)
        => new
        {
            data = new
            {
                type = jobType,
                attributes = new
                {
                    items = new
                    {
                        data = items.Select(i =>
                            BuildCatalogItemData(i, create))
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

    private static object BuildCatalogItemAttributes(
        KlaviyoProductItem i,
        bool includeExternalId)
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
            a["external_id"] = i.ExternalId; // create only

        return a;
    }

    private static object BuildHardDeletePayload(
        IReadOnlyList<KlaviyoProductItem> items)
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
        var max = _opt.MaxBatchSize <= 0 ? 100 : _opt.MaxBatchSize;
        if (items.Count > max)
            throw new InvalidOperationException(
                $"Catalog bulk job supports up to {max} items per request.");
    }

    private static string BuildCatalogItemId(string externalId)
    {
        if (string.IsNullOrWhiteSpace(externalId))
            throw new InvalidOperationException(
                "ExternalId is required to build the Klaviyo catalog item id.");

        return $"$custom:::$default:::{externalId}";
    }

    // ----------------------------
    // HTTP + logging
    // ----------------------------
    private async Task<string> PostAsync(
        string path,
        object payload,
        CancellationToken ct)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);

        _logger.LogDebug("Klaviyo POST {Path}", path);

        using var content =
            new StringContent(json, Encoding.UTF8, "application/json");

        using var response =
            await _http.PostAsync(path, content, ct);

        var body = await response.Content.ReadAsStringAsync(ct);

        if (!response.IsSuccessStatusCode)
        {
            _logger.LogError(
                "Klaviyo API error {StatusCode} on {Path} | Body={Body} | Json={Json}",
                (int)response.StatusCode,
                path,
                body,
                json);

            throw new HttpRequestException(
                $"Klaviyo API error ({(int)response.StatusCode}): {body}");
        }

        _logger.LogDebug(
            "Klaviyo API accepted job ({Path}) | Response={Body}",
            path,
            body);

        return body;
    }

    //private async Task<string> PollCatalogCreateJobAsync(string jobId, CancellationToken ct)
    //{
    //    var delay = TimeSpan.FromSeconds(2);

    //    for (var attempt = 0; attempt < 20; attempt++)
    //    {
    //        await Task.Delay(delay, ct);

    //        using var resp =
    //            await _http.GetAsync($"/api/catalog-item-bulk-create-jobs/{jobId}/", ct);

    //        var body = await resp.Content.ReadAsStringAsync(ct);

    //        if (!resp.IsSuccessStatusCode)
    //        {
    //            _logger.LogWarning(
    //                "Klaviyo CREATE job poll failed for {JobId}. Body={Body}",
    //                jobId,
    //                body);
    //            return body;
    //        }

    //        using var doc = JsonDocument.Parse(body);
    //        var attrs = doc.RootElement.GetProperty("data").GetProperty("attributes");

    //        var status = attrs.GetProperty("status").GetString();
    //        var total = attrs.GetProperty("total_count").GetInt32();
    //        var completed = attrs.GetProperty("completed_count").GetInt32();
    //        var failed = attrs.GetProperty("failed_count").GetInt32();

    //        _logger.LogDebug(
    //            "Klaviyo CREATE job {JobId}: status={Status}, completed={Completed}/{Total}, failed={Failed}",
    //            jobId, status, completed, total, failed);

    //        // Terminal state
    //        if (!string.Equals(status, "processing", StringComparison.OrdinalIgnoreCase))
    //        {
    //            if (failed > 0)
    //            {
    //                _logger.LogError(
    //                    "Klaviyo CREATE job {JobId} finished with failures. Body={Body}",
    //                    jobId,
    //                    body);
    //            }
    //            else
    //            {
    //                _logger.LogDebug(
    //                    "Klaviyo CREATE job {JobId} completed successfully.",
    //                    jobId);
    //            }

    //            return body;
    //        }
    //    }

    //    _logger.LogWarning(
    //        "Klaviyo CREATE job {JobId} still processing after polling limit.",
    //        jobId);

    //    return "";
    //}

    private async Task<string?> PollCatalogUpdateJobAsync(
        string jobId,
        CancellationToken ct,
        TimeSpan? maxDuration = null)
    {
        var deadline = DateTimeOffset.UtcNow + (maxDuration ?? TimeSpan.FromMinutes(5));

        var delay = TimeSpan.FromSeconds(1);
        var maxDelay = TimeSpan.FromSeconds(15);

        while (DateTimeOffset.UtcNow < deadline)
        {
            await Task.Delay(delay, ct);

            using var resp = await _http.GetAsync($"/api/catalog-item-bulk-update-jobs/{jobId}/", ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Klaviyo UPDATE job poll failed for {JobId}. Body={Body}", jobId, body);
                return null;
            }

            using var doc = JsonDocument.Parse(body);
            var attrs = doc.RootElement.GetProperty("data").GetProperty("attributes");

            var status = attrs.GetProperty("status").GetString();
            var total = attrs.GetProperty("total_count").GetInt32();
            var completed = attrs.GetProperty("completed_count").GetInt32();
            var failed = attrs.GetProperty("failed_count").GetInt32();

            _logger.LogDebug(
                "Klaviyo UPDATE job {JobId}: status={Status}, completed={Completed}/{Total}, failed={Failed}",
                jobId, status, completed, total, failed);

            // Terminal state
            if (!string.Equals(status, "processing", StringComparison.OrdinalIgnoreCase))
                return body;

            // Backoff (1s -> 2s -> 4s -> 8s -> 15s ...)
            var next = TimeSpan.FromSeconds(Math.Min(delay.TotalSeconds * 2, maxDelay.TotalSeconds));
            delay = next;
        }

        // Not an error: job is still processing; we just stopped polling.
        _logger.LogInformation(
            "Klaviyo UPDATE job {JobId} still processing after max polling window.",
            jobId);

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

            // Klaviyo returns meta.id in your sample
            if (e.TryGetProperty("meta", out var meta) && meta.ValueKind == JsonValueKind.Object)
            {
                if (meta.TryGetProperty("id", out var idEl))
                {
                    var id = idEl.GetString();
                    if (!string.IsNullOrWhiteSpace(id))
                    {
                        result.Add(id);
                        continue;
                    }
                }

                // Keep this for other variants you might see
                if (meta.TryGetProperty("external_id", out var extEl))
                {
                    var ext = extEl.GetString();
                    if (!string.IsNullOrWhiteSpace(ext))
                        result.Add(BuildCatalogItemId(ext)); // normalize to item-id form
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
            var jobId = doc.RootElement
                .GetProperty("data")
                .GetProperty("id")
                .GetString();

            _logger.LogDebug(
                "Klaviyo {Operation} job accepted. JobId={JobId}",
                operation,
                jobId);
        }
        catch
        {
            _logger.LogWarning(
                "Klaviyo {Operation} job accepted, but job id could not be parsed. Raw body: {Body}",
                operation,
                body);
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
