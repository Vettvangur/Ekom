# Ekom.Algolia

[![Nuget](https://img.shields.io/nuget/vpre/Ekom.Algolia?color=ed0f0f)](https://www.nuget.org/packages/Ekom.Algolia/)
[![Publish Ekom.Algolia](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-algolia.yml/badge.svg?branch=Ekom)](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-algolia.yml)

Algolia integration plugin for Ekom (Umbraco).

## Features
- Product indexing with background queue/worker.
- Product search service with Algolia SDK `SearchForHits` requests.
- Algolia Insights events for view, add-to-cart, checkout, purchase.
- In-memory search result caching with store-scoped invalidation after reindex/update/delete.
- Index naming convention: `{primary|replica|query_suggestions}.ENVIRONMENT.STORE.ENTITY[_sorted_by_{asc|desc}_ATTRIBUTE][.Locale][.Currency]`.

## Install

Register services:

```csharp
using Ekom.Algolia;

services.AddAlgolia();
```

Search products:

```csharp
using Algolia.Search.Models.Search;
using Ekom.Algolia.Models.Search;
using Ekom.Algolia.Services;

public sealed class ProductSearchController
{
    private readonly IAlgoliaSearchService _algoliaSearchService;

    public ProductSearchController(IAlgoliaSearchService algoliaSearchService)
    {
        _algoliaSearchService = algoliaSearchService;
    }

    public async Task<IReadOnlyList<string>> SearchAsync(CancellationToken ct)
    {
        var response = await _algoliaSearchService.SearchProductsAsync(
            new AlgoliaSearchRequest
            {
                StoreAlias = "Store",
                Locale = "en-US",
                Currency = "USD",
                Query = new SearchForHits
                {
                    Query = "shoe",
                    HitsPerPage = 20,
                    Filters = "available:true"
                }
            },
            ct).ConfigureAwait(false);

        return response.Hits.Select(x => x.Title).ToList();
    }
}
```

## Configuration (appsettings.json)

```json
{
  "Ekom": {
    "Algolia": {
      "Enabled": true,
      "ApplicationId": "APP_ID",
      "AdminApiKey": "ADMIN_API_KEY",
      "SearchApiKey": "SEARCH_API_KEY",
      "InsightsApiKey": "INSIGHTS_API_KEY",
      "AnalyticsRegion": "eu",
      "Environment": "prod",
      "Indexing": {
        "Enabled": true,
        "Products": true,
        "BatchSize": 1000,
        "ProductProperties": [
          "title",
          "summary",
          "description",
          "stockCount|int",
          "weight|decimal",
          "publishedAt|unix"
        ],
        "Dispatching": {
          "MaxBatchSize": 100,
          "FlushIntervalSeconds": 2,
          "MaxQueueSize": 10000,
          "MaxConcurrency": 2
        }
      },
      "Search": {
        "Enabled": true,
        "Products": true,
        "QuerySuggestions": true,
        "MinimumQueryLength": 2,
        "MaxHitsPerPage": 100,
        "QuerySuggestionsProvisioning": {
          "Enabled": true,
          "UseReplicas": false,
          "MinimumHits": 5,
          "MinimumLetters": 4,
          "EnablePersonalization": false,
          "AllowSpecialCharacters": false,
          "Exclude": []
        },
        "Cache": {
          "Enabled": true,
          "DurationMinutes": 60,
          "CacheEmptyResults": true
        }
      },
      "Events": {
        "Enabled": true,
        "ViewedProduct": true,
        "AddedToCart": true,
        "StartedCheckout": true,
        "Purchase": true
      },
      "Stores": [
        {
          "Alias": "Store",
          "Domain": "https://example.com"
        }
      ]
    }
  }
}
```

## Notes
- Indexing triggers from Umbraco content notifications for `ekmProduct`.
- Search uses the required `SearchApiKey`; indexing and settings operations continue to use `AdminApiKey`.
- When `Search.QuerySuggestions` is enabled, the plugin provisions the separate `query_suggestions...` index configuration automatically with the Admin API key.
- Set `AnalyticsRegion` to `us` or `eu` if you know your Algolia analytics region; if omitted, the plugin tries `us` and then `eu`.
- `IAlgoliaSearchService.SearchProductsAsync(...)` returns hits together with paging metadata, query text, processing time, and raw facets.
- The plugin always resolves and sets the Algolia index name from Ekom store alias, locale, and currency; callers should not set `IndexName` themselves.
- Search cache keys include the resolved index name and serialized Algolia query payload so all SDK options affect caching.
- Store `Locale` and `Currency` now come from the Ekom store resolved by alias, so `appsettings.json` only needs the store alias and optional domain.
- `Stores[].Domain` must be an absolute URL such as `https://example.com`; invalid values are ignored and logged as warnings during indexing.
- Request and order context decide which culture and currency suffix is used; background indexing falls back to the store's default culture/currency.
- `Title` is always indexed as a top-level field, and `NodeName` contains the Umbraco node name.
- Variants are not indexed by default.
- `Indexing.ProductProperties` supports one optional modifier per property: `|int`, `|decimal`, `|unix`, or `|unixms`.
- `|decimal` accepts either comma or dot decimal separators, so values like `0,1` and `0.0` are indexed as decimals.
- Invalid `|int` and `|decimal` values are skipped instead of being indexed as strings.
- Manual reindex all endpoint: `GET` or `POST /umbraco/backoffice/api/Ekom/AlgoliaBackoffice/RebuildIndexesAsync`.
- Manual reindex store endpoint: `GET` or `POST /umbraco/backoffice/api/Ekom/AlgoliaBackoffice/RebuildStoreIndexesAsync?storeAlias=Store`.
