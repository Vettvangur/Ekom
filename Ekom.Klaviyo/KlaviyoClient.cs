using Ekom.Klaviyo.Models;
using Microsoft.Extensions.Options;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Ekom.Klaviyo;

internal interface IKlaviyoClient
{
    Task BulkUpsertCatalogItemsAsync(IReadOnlyList<KlaviyoProductItem> items, CancellationToken ct = default);
}

internal sealed class KlaviyoClient : IKlaviyoClient
{
	private readonly HttpClient _http;
	private readonly KlaviyoOptions _opt;

	private static readonly JsonSerializerOptions JsonOptions = new()
	{
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
		DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
	};

	public KlaviyoClient(HttpClient http, IOptions<KlaviyoOptions> options)
	{
		_http = http;
    }

    public Task BulkUpsertCatalogItemsAsync(IReadOnlyList<KlaviyoProductItem> items, CancellationToken ct = default)
    {
        if (!_opt.Enabled || items.Count == 0) return Task.CompletedTask;

        // Klaviyo bulk jobs are typically capped at 100 items..
        if (items.Count > 100)
            throw new InvalidOperationException("Catalog bulk job supports up to 100 items per request.");

        var payload = new
        {
            data = new
            {
                type = "catalog-item-bulk-update-job",
                attributes = new
                {
                    items = items.Select(i => new
                    {
                        type = "catalog-item",
                        attributes = new
                        {
                            external_id = i.ExternalId,
                            title = i.Title,
                            price = i.Price,
                            url = i.Url,
                            image_full_url = i.ImageFullUrl,
                            published = i.Published,

                            categories = i.Categories,
                            custom_metadata = MergeMetadata(i)
                        }
                    }).ToArray()
                }
            }
        };

        return PostAsync("/api/catalog-item-bulk-update-jobs", payload, ct);

        static Dictionary<string, object?> MergeMetadata(KlaviyoProductItem i)
        {
            var d = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase)
            {
                ["store_key"] = i.StoreAlias,
                ["sku"] = i.Sku
            };

            if (i.CustomMetadata is not null)
            {
                foreach (var kv in i.CustomMetadata)
                    d[kv.Key] = kv.Value;
            }

            return d;
        }
    }

    private async Task PostAsync(string path, object payload, CancellationToken ct)
	{
		var json = JsonSerializer.Serialize(payload, JsonOptions);
		using var content = new StringContent(json, Encoding.UTF8, "application/json");
		using var response = await _http.PostAsync(path, content, ct);

		if (!response.IsSuccessStatusCode)
		{
			var body = await response.Content.ReadAsStringAsync(ct);
			throw new HttpRequestException($"Klaviyo API error ({(int)response.StatusCode}): {body}");
		}
	}
}
