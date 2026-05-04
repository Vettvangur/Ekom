# Ekom.Algolia

[![Nuget](https://img.shields.io/nuget/vpre/Ekom.Algolia?color=ed0f0f)](https://www.nuget.org/packages/Ekom.Algolia/)
[![Publish Ekom.Algolia](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-algolia.yml/badge.svg?branch=Ekom)](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-algolia.yml)

Algolia integration plugin for Ekom (Umbraco).

## Features
- Product indexing with background queue/worker.
- Category indexing with background queue/worker.
- Product search service with Algolia SDK `SearchForHits` requests.
- Category search service with Algolia SDK `SearchForHits` requests.
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
                    Filters = "Available:1"
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
        "Categories": true,
        "BatchSize": 1000,
        "ProductProperties": [
          "title",
          "summary",
          "description",
          "channels|array",
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
        "Categories": true,
        "QuerySuggestions": true,
        "IncludeUserToken": true,
        "VaryCacheByUserToken": false,
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
          "Alias": "Store"
        }
      ]
    }
  }
}
```

### Settings reference

| Setting | Type | Default | Description |
| --- | --- | --- | --- |
| `Enabled` | `bool` | `true` | Enables the Algolia plugin. |
| `ApplicationId` | `string` | required | Algolia application ID. |
| `AdminApiKey` | `string` | required | API key used for indexing, settings, replicas, and query suggestion provisioning. |
| `SearchApiKey` | `string` | required | API key used by `IAlgoliaSearchService` search requests. |
| `InsightsApiKey` | `string` | `null` | Optional key for Insights events. Falls back to `AdminApiKey` when omitted. |
| `AnalyticsRegion` | `string` | `null` | Algolia analytics region for query suggestions, usually `us` or `eu`. If omitted, the plugin tries both. |
| `Environment` | `string` | `prod` | Environment segment used in generated index names. |
| `Indexing:Enabled` | `bool` | `true` | Enables indexing features. |
| `Indexing:Products` | `bool` | `true` | Enables product indexing. |
| `Indexing:Categories` | `bool` | `true` | Enables category indexing. |
| `Indexing:BatchSize` | `int` | `1000` | Batch size for Algolia save/replace/delete operations. |
| `Indexing:ProductProperties` | `string[]` | `[]` | Additional product properties/metafields to include in product records. Supports modifiers documented below. |
| `Indexing:SortedReplicas` | `object[]` | `[]` | Replica definitions using `Attribute` and `Direction` (`Asc` or `Desc`). |
| `Indexing:Dispatching:MaxBatchSize` | `int` | `100` | Maximum queued jobs processed in one worker batch. |
| `Indexing:Dispatching:FlushIntervalSeconds` | `int` | `2` | Worker delay between queue flushes. |
| `Indexing:Dispatching:MaxQueueSize` | `int` | `10000` | Maximum in-memory queue size. |
| `Indexing:Dispatching:MaxConcurrency` | `int` | `2` | Maximum indexing worker concurrency. |
| `Search:Enabled` | `bool` | `true` | Enables Algolia search services. |
| `Search:Products` | `bool` | `true` | Enables product search. |
| `Search:Categories` | `bool` | `true` | Enables category search. |
| `Search:QuerySuggestions` | `bool` | `false` | Enables query suggestion search and provisioning. |
| `Search:IncludeUserToken` | `bool` | `true` | Adds `userToken` to Algolia search requests using `IAlgoliaUserTokenProvider`, unless the query already has a token. |
| `Search:VaryCacheByUserToken` | `bool` | `false` | Includes `userToken` in search cache keys. Keep `false` for shared cache; set `true` when Algolia personalization changes result order/content per user. |
| `Search:MinimumQueryLength` | `int` | `2` | Minimum query length before search executes. Set `0` to disable this guard. |
| `Search:MaxHitsPerPage` | `int` | `100` | Upper bound for requested `HitsPerPage`. Set `0` or less to avoid clamping. |
| `Search:Cache:Enabled` | `bool` | `true` | Enables in-memory search response caching. |
| `Search:Cache:DurationMinutes` | `int` | `60` | Search cache duration. |
| `Search:Cache:CacheEmptyResults` | `bool` | `true` | Whether empty result sets are cached. |
| `Search:QuerySuggestionsProvisioning:Enabled` | `bool` | `true` | Creates/updates Algolia query suggestion configuration automatically. |
| `Search:QuerySuggestionsProvisioning:UseReplicas` | `bool` | `false` | Includes source index replicas in query suggestion generation. |
| `Search:QuerySuggestionsProvisioning:MinimumHits` | `int` | `5` | Minimum hits required for query suggestions. |
| `Search:QuerySuggestionsProvisioning:MinimumLetters` | `int` | `4` | Minimum letters required for query suggestions. |
| `Search:QuerySuggestionsProvisioning:EnablePersonalization` | `bool` | `false` | Enables personalization for query suggestions configuration. |
| `Search:QuerySuggestionsProvisioning:AllowSpecialCharacters` | `bool` | `false` | Allows special characters in query suggestions. |
| `Search:QuerySuggestionsProvisioning:Exclude` | `string[]` | `[]` | Query suggestion exclusion list. |
| `Events:Enabled` | `bool` | `true` | Enables Algolia Insights events. |
| `Events:ViewedProduct` | `bool` | `true` | Sends product view events. |
| `Events:AddedToCart` | `bool` | `true` | Sends add-to-cart conversion events. |
| `Events:StartedCheckout` | `bool` | `true` | Sends checkout conversion events. |
| `Events:Purchase` | `bool` | `true` | Sends purchase conversion events. |
| `Stores` | `object[]` | `[]` | Store aliases supported by the plugin. Locale/currency are resolved from Ekom store data. |
| `Stores[*]:Alias` | `string` | required | Ekom store alias. |
| `Stores[*]:IncludeStock` | `bool` | `false` | Includes product stock in indexed records for this store. |

## Notes
- Indexing triggers from Umbraco content notifications for `ekmProduct` and `ekmCategory`.
- Search uses the required `SearchApiKey`; indexing and settings operations continue to use `AdminApiKey`.
- When `Search.QuerySuggestions` is enabled, the plugin provisions the separate `query_suggestions...` index configuration automatically with the Admin API key.
- Set `AnalyticsRegion` to `us` or `eu` if you know your Algolia analytics region; if omitted, the plugin tries `us` and then `eu`.
- `IAlgoliaSearchService.SearchProductsAsync(...)` returns hits together with paging metadata, query text, processing time, and raw facets.
- `IAlgoliaSearchService.SearchCategoriesAsync(...)` searches a dedicated category index scoped by store alias and locale.
- The plugin always resolves and sets the Algolia index name from Ekom store alias, locale, and currency; callers should not set `IndexName` themselves.
- Category indexes omit the currency suffix because category records are scoped by store alias and locale only.
- Search cache keys include the resolved index name and serialized Algolia query payload so all SDK options affect caching.
- `Search:IncludeUserToken` sends user context to Algolia search. The default provider uses authenticated username, then session ID, then request trace identifier.
- `Search:VaryCacheByUserToken` controls whether that token also affects cache keys. Leave it `false` when results are not personalized so users can share cached results.
- Store `Locale` and `Currency` now come from the Ekom store resolved by alias, so `appsettings.json` only needs the store alias.
- Request and order context decide which culture and currency suffix is used; background indexing falls back to the store's default culture/currency.
- `Title` is always indexed as a top-level field, and `NodeName` contains the Umbraco node name.
- `Available` is indexed as `1` for available products and `0` for unavailable products, so it can be used for numeric ranking.
- Product records always include top-level `ProductRanking` and `CategoryRanking` integer fields. Both support negative values. `ProductRanking` reads the product `ekmAlgoliaRank` property and defaults to `0` when missing or invalid. `CategoryRanking` uses the highest valid `ekmAlgoliaRank` value across the product's categories and defaults to `0` when no category has a valid rank.
- Variants are not indexed by default.
- `Indexing.ProductProperties` supports one optional modifier per property: `|array`, `|int`, `|decimal`, `|unix`, or `|unixms`.
- Metafields can be indexed explicitly with `metafield:<alias>`, for example `metafield:material`, `metafield:color|array`, or `metafield:releaseDate|unix`.
- Multi-value metafields are skipped unless `|array` is configured.
- `|array` parses JSON arrays such as checkbox-list values like `["Web","Store"]` into Algolia string arrays.
- `|decimal` accepts either comma or dot decimal separators, so values like `0,1` and `0.0` are indexed as decimals.
- Invalid `|array`, `|int`, and `|decimal` values are skipped instead of being indexed as strings.
- Manual reindex all endpoint: `GET` or `POST /umbraco/backoffice/api/Ekom/AlgoliaBackoffice/RebuildIndexesAsync`.
- Manual reindex store endpoint: `GET` or `POST /umbraco/backoffice/api/Ekom/AlgoliaBackoffice/RebuildStoreIndexesAsync?storeAlias=Store`.
