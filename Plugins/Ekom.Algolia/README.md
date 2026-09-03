# Ekom.Algolia

[![Nuget](https://img.shields.io/nuget/vpre/Ekom.Algolia?color=ed0f0f)](https://www.nuget.org/packages/Ekom.Algolia/)
[![Publish Ekom.Algolia](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-algolia.yml/badge.svg?branch=Ekom)](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-algolia.yml)

Algolia integration plugin for Ekom (Umbraco).

## Features
- Product indexing with background queue/worker.
- Category indexing with background queue/worker.
- Standard Umbraco content indexing with background queue/worker.
- Product search service with Algolia SDK `SearchForHits` requests.
- Category search service with Algolia SDK `SearchForHits` requests.
- Standard content search and federated multi-index search.
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
      "Replacement": {
        "MaxRetries": 800
      },
      "Indexing": {
        "Enabled": true,
        "Products": true,
        "Categories": true,
        "Variants": false,
        "BatchSize": 1000,
        "ProductProperties": [
          "channels|array",
          "packageCount|int",
          "weight|decimal",
          "publishedAt|unix",
          "description|striphtml"
        ],
        "Dispatching": {
          "MaxBatchSize": 100,
          "FlushIntervalSeconds": 2,
          "MaxQueueSize": 10000,
          "MaxConcurrency": 2
        }
      },
      "ContentIndexing": {
        "Enabled": true,
        "EnforcePublisherOnly": true,
        "BatchSize": 1000,
        "OversizedRecords": {
          "Behavior": "Fail",
          "MaxSizeBytes": 100000
        },
        "Indexes": [
          {
            "IndexName": "SearchIndex",
            "ContentTypes": [
              {
                "Alias": "article",
                "Properties": [
                  "title",
                  "summary|striphtml",
                  "publishedAt|unix"
                ]
              }
            ]
          }
        ]
      },
      "Search": {
        "Enabled": true,
        "Products": true,
        "Categories": true,
        "GroupVariantsByProduct": true,
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
          "Alias": "Store",
          "LanguageSettings": {
            "QueryLanguages": ["en"],
            "IndexLanguages": ["en"],
            "RemoveStopWords": true,
            "IgnorePlurals": true
          }
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
| `Replacement:MaxRetries` | `int` | `800` | Maximum number of status polling retries while atomically replacing an existing index. |
| `Indexing:Enabled` | `bool` | `true` | Enables indexing features. |
| `Indexing:Products` | `bool` | `true` | Enables product indexing. |
| `Indexing:Categories` | `bool` | `true` | Enables category indexing. |
| `Indexing:Variants` | `bool` | `false` | Indexes product variants as separate product records so variant SKUs can be searched directly. |
| `Indexing:BatchSize` | `int` | `1000` | Batch size for Algolia save/replace/delete operations. |
| `Indexing:ProductProperties` | `string[]` | `[]` | Additional product properties/metafields to include in product records. Supports modifiers documented below. |
| `Indexing:AttributesForFaceting` | `string[]` | `[]` | Algolia facet expressions to preserve on product indexes, such as `filterOnly(categoryPageId)` or `searchable(brand)`. |
| `Indexing:FacetAttributes` | `string[]` | `[]` | Product properties/metafields to include under `attributes` and configure as facets. |
| `Indexing:VariantFacetAttributes` | `object` | `{}` | Maps facet output names to `variant:` or `variantGroup:` property sources. |
| `Indexing:SortedReplicas` | `object[]` | `[]` | Replica definitions using `Attribute` and `Direction` (`Asc` or `Desc`). |
| `Indexing:Dispatching:MaxBatchSize` | `int` | `100` | Maximum queued jobs processed in one worker batch. |
| `Indexing:Dispatching:FlushIntervalSeconds` | `int` | `2` | Worker delay between queue flushes. |
| `Indexing:Dispatching:MaxQueueSize` | `int` | `10000` | Maximum in-memory queue size. |
| `Indexing:Dispatching:MaxConcurrency` | `int` | `2` | Maximum indexing worker concurrency. |
| `ContentIndexing:Enabled` | `bool` | `false` | Enables standard Umbraco content indexing. |
| `ContentIndexing:BatchSize` | `int` | `1000` | Batch size for content index rebuild operations. |
| `ContentIndexing:OversizedRecords:Behavior` | `Fail` or `Skip` | `Fail` | Fails content indexing or skips records that exceed the configured size limit. |
| `ContentIndexing:OversizedRecords:MaxSizeBytes` | `int` | `100000` | Maximum serialized UTF-8 size of one content record. |
| `ContentIndexing:Indexes` | `object[]` | `[]` | Content indexes to maintain. Index names resolve as `{IndexName}.{Environment}.{Culture}`. |
| `ContentIndexing:Indexes[*]:ContentTypes[*]:Alias` | `string` | required | Umbraco content type alias to include in the content index. |
| `ContentIndexing:Indexes[*]:ContentTypes[*]:Properties` | `string[]` | `[]` | Property aliases to index. Use `|unix` or `|unixms` for numeric dates, or `|striphtml` for searchable plain text from rich-text values. |
| `Search:Enabled` | `bool` | `true` | Enables Algolia search services. |
| `Search:Products` | `bool` | `true` | Enables product search. |
| `Search:Categories` | `bool` | `true` | Enables category search. |
| `Search:GroupVariantsByProduct` | `bool` | `true` | When variant indexing is enabled, applies Algolia `distinct` so product searches group variant records by product. |
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
| `Stores[*]:EnableAvailabilityUpdates` | `bool` | `false` | Partially updates indexed availability after stock changes for this store. When `IncludeStock` is enabled, also updates the indexed stock amount. |
| `Stores[*]:LanguageSettings:QueryLanguages` | `string[]` | `[]` | ISO 639-1 languages used for language-specific query processing. |
| `Stores[*]:LanguageSettings:IndexLanguages` | `string[]` | `[]` | ISO 639-1 languages used for language-specific indexing. |
| `Stores[*]:LanguageSettings:RemoveStopWords` | `bool` | `null` | Enables or disables stop-word removal for this store's product indexes. |
| `Stores[*]:LanguageSettings:IgnorePlurals` | `bool` | `null` | Enables or disables matching singular, plural, and inflected forms for this store's product indexes. |

## Usage notes

### Insights user-token correlation with InstantSearch

Ekom issues an opaque first-party user-token cookie the first time `IAlgoliaUserTokenProvider.GetOrCreateUserToken()` is called. Use that same value to initialize the browser's Search Insights client so frontend click events and server-side conversion events share one Algolia user token. The token is not derived from a username or other personally identifiable information.

Resolve the token while rendering the Razor page and initialize Search Insights with it. Do not let Search Insights generate a competing user token.

```cshtml
@using Ekom.Algolia.Services
@inject IAlgoliaUserTokenProvider AlgoliaUserTokenProvider

@{
    var algoliaUserToken = AlgoliaUserTokenProvider.GetOrCreateUserToken();
}

<script>
    aa('init', {
        appId: '@algoliaOptions.ApplicationId',
        apiKey: '@algoliaOptions.SearchApiKey',
        useCookie: false,
    });
    aa('setUserToken', '@algoliaUserToken');
</script>
```

When a result is added to an Ekom order, include that result's Algolia `queryID` as `algoliaQueryId` in the add-to-order request. Ekom persists the first query ID received for each order line along with the shared user token in `OrderInfo.Tracking.Algolia`.

```js
await fetch('/ekom/order/add', {
  method: 'POST',
  headers: { 'Content-Type': 'application/json' },
  body: JSON.stringify({
    productId,
    quantity: 1,
    storeAlias,
    algoliaQueryId: searchResult.queryID,
  }),
});
```

The stored token is used for Ekom's add-to-cart, checkout, and purchase Insights events. Ekom also uses each persisted line query ID for the corresponding conversion event. The Order Info tracking view shows an Algolia subsection only when this data exists.

### Stock availability updates

Enable partial product-record updates when stock changes:

```json
{
  "Ekom": {
    "Algolia": {
      "Stores": [
        {
          "Alias": "Store",
          "EnableAvailabilityUpdates": true
        }
      ]
    }
  }
}
```

Ekom updates `Available` only for stores with `Stores[*]:EnableAvailabilityUpdates` enabled, and only when effective sellable availability changes, including stock-buffer, backorder, and variant availability rules. For products with variants, the parent record becomes unavailable only when every variant is unavailable. If `Stores[*]:IncludeStock` is `true`, every stock change also partially updates `Stock`; indexed variant records additionally update `variantStock`. Existing records are updated only—stock changes never create incomplete Algolia records.

### Indexing triggers and API keys

Product and category indexing is triggered from Umbraco content notifications for `ekmProduct` and `ekmCategory`.

Search requests use `SearchApiKey`. Indexing, settings updates, replicas, delete operations, and query suggestion provisioning use `AdminApiKey`.

```json
{
  "Ekom": {
    "Algolia": {
      "ApplicationId": "APP_ID",
      "AdminApiKey": "ADMIN_API_KEY",
      "SearchApiKey": "SEARCH_API_KEY"
    }
  }
}
```

When `Search:QuerySuggestions` is enabled, the plugin provisions the separate `query_suggestions...` index configuration automatically. Set `AnalyticsRegion` to `us` or `eu` if you know it; if omitted, the plugin tries `us` and then `eu`.

```json
{
  "Ekom": {
    "Algolia": {
      "AnalyticsRegion": "eu",
      "Search": {
        "QuerySuggestions": true
      }
    }
  }
}
```

### Per-store language settings

Configure Algolia's language processing separately for each store. These settings are applied to the store's primary product indexes and sorted replicas. Algolia recommends configuring both `QueryLanguages` and `IndexLanguages` so query-time and index-time processing are consistent.

```json
{
  "Ekom": {
    "Algolia": {
      "Stores": [
        {
          "Alias": "IcelandicStore",
          "LanguageSettings": {
            "QueryLanguages": ["is"],
            "IndexLanguages": ["is"],
            "RemoveStopWords": false,
            "IgnorePlurals": true
          }
        },
        {
          "Alias": "EnglishStore",
          "LanguageSettings": {
            "QueryLanguages": ["en"],
            "IndexLanguages": ["en"],
            "RemoveStopWords": true,
            "IgnorePlurals": true
          }
        }
      ]
    }
  }
}
```

Language values are validated against Algolia's supported ISO 639-1 language codes. Omitted boolean settings preserve Algolia's defaults; explicitly setting them to `false` disables the corresponding processing. These settings don't apply to standard content or query-suggestions indexes.

### Oversized content records

Standard content records are measured before they are sent to Algolia. The default `ContentIndexing:OversizedRecords:Behavior` is `Fail`, which stops the operation and logs enough Umbraco context to identify the node. Set it to `Skip` to exclude oversized records and continue indexing:

```json
{
  "Ekom": {
    "Algolia": {
      "ContentIndexing": {
        "OversizedRecords": {
          "Behavior": "Skip",
          "MaxSizeBytes": 100000
        }
      }
    }
  }
}
```

Diagnostics include the index name, record size, `NodeId`, `objectID`, node name, content type alias, URL, and the five largest field names with their approximate byte sizes. Field values are not logged. When `Skip` is used during incremental indexing, any previous record for that node is deleted from Algolia to prevent stale content.

```text
Algolia content record is too large for index SearchIndex.prod.en-US: size 102824/100000 bytes, NodeId 1234, ObjectID f0446822-c9cd-4bd9-8351-2e582153ce43, Name Example article, ContentTypeAlias article, Url /articles/example/, LargestFields body=101542, summary=640. Behavior: Fail.
```

### Index naming and store context

The plugin resolves Algolia index names from the configured environment, store alias, locale, and currency. Callers should not set `SearchForHits.IndexName`; the search service sets it before executing the request.

Product index names include currency when a currency is resolved:

```text
primary.prod.Store.products.en-US.USD
```

Category index names omit currency because category records are scoped by store alias and locale only:

```text
primary.prod.Store.categories.en-US
```

Standard content index names resolve as `{IndexName}.{Environment}.{Culture}`:

```text
SearchIndex.prod.en-US
```

Only the store alias must be configured in `appsettings.json`. Locale and currency are resolved from the Ekom store and the current request/order context. Background indexing falls back to the store's default culture and currency.

```json
{
  "Ekom": {
    "Algolia": {
      "Stores": [
        {
          "Alias": "Store"
        }
      ]
    }
  }
}
```

### Searching

`IAlgoliaSearchService.SearchProductsAsync(...)` returns typed product hits with paging metadata, query text, processing time, and raw facets.

```csharp
var response = await algoliaSearchService.SearchProductsAsync(
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
```

Other search methods target their own index types:

- `SearchCategoriesAsync(...)` searches category records scoped by store alias and locale.
- `SearchContentAsync(...)` searches configured standard content indexes.
- `FederatedSearchAsync(...)` executes products, categories, query suggestions, and content searches in one Algolia multi-search request.

### Search caching and user tokens

Search cache keys include the resolved index name and serialized Algolia query payload, so SDK options such as filters, facets, page, and hits-per-page affect caching.

`Search:IncludeUserToken` sends user context to Algolia. The default provider uses authenticated username first, then session ID, then request trace identifier.

`Search:VaryCacheByUserToken` controls whether that token also affects cache keys. Leave it `false` for shared cache when results are not personalized. Set it to `true` when Algolia personalization changes result order or content per user.

```json
{
  "Ekom": {
    "Algolia": {
      "Search": {
        "IncludeUserToken": true,
        "VaryCacheByUserToken": false,
        "Cache": {
          "Enabled": true,
          "DurationMinutes": 60
        }
      }
    }
  }
}
```

### Product records and ranking fields

Product records provide the following fields without requiring any `Indexing:ProductProperties` configuration:

- Identity: `objectID`, `Sku`, `ProductId`, and `IsVariant`.
- Content: `NodeName`, `Title`, `Summary`, `Description`, and `Url`.
- Images: `image_url` and `ImageUrls`.
- Pricing: `Price`, `PriceWithVat`, `PriceWithoutVat`, and `Currency`.
- Availability and ranking: `Available`, `ProductRanking`, and `CategoryRanking`.
- Store context and dates: `StoreAlias`, `Locale`, `CreatedAt`, and `UpdatedAt`.
- Categories: `categoryPageId`, `hierarchical_categories.lvl0`, additional hierarchy levels when present, and `category_paths`.

Optional fields are omitted when no value is available. `Stock` is included only when `Stores[*]:IncludeStock` is enabled. Variant-specific fields are included when variant indexing is enabled, as described below.

`Title` is a required top-level field. `NodeName` contains the Umbraco node name.

`Available` is indexed as a numeric value so it can be used for ranking:

```json
{
  "Title": "Running shoe",
  "NodeName": "Running shoe - black",
  "Available": 1
}
```

Product records also include `ProductRanking` and `CategoryRanking` integer fields. Both support negative values.

- `ProductRanking` reads the product `ekmAlgoliaRank` property and defaults to `0` when missing or invalid.
- `CategoryRanking` uses the highest valid `ekmAlgoliaRank` value across the product's categories and defaults to `0` when no category has a valid rank.

```json
{
  "ProductRanking": 10,
  "CategoryRanking": 5
}
```

### Category pages

`categoryPageId` contains the GUIDs of every category assigned to the product and all their ancestors. Values are deduplicated while preserving root-to-leaf order. This lets a category page filter by its own GUID and include products assigned to descendant categories. Variant records inherit the same identifiers.

```json
{
  "categoryPageId": [
    "0f0ea901-6280-4586-b7fe-7ae731ed9489",
    "4925fa5f-163a-4882-80af-b12e6501c30c",
    "acee53b7-c6f6-4cb2-b940-508f1b49208d"
  ]
}
```

Configure `categoryPageId` as a persistent top-level filter-only facet, then apply the category GUID as a fixed InstantSearch filter:

```json
{
  "Ekom": {
    "Algolia": {
      "Indexing": {
        "AttributesForFaceting": [
          "filterOnly(categoryPageId)"
        ]
      }
    }
  }
}
```

The plugin reapplies configured facet expressions during product index settings updates and rebuilds. When variant indexing is enabled, it also adds `filterOnly(ProductId)` and `filterOnly(categoryPageId)` automatically.

```jsx
<Configure
  filters={`categoryPageId:"${categoryKey}"`}
  analyticsTags={['category-page']}
/>
```

Category hierarchy changes are not propagated to product records automatically. After moving, deleting, publishing, or changing a category hierarchy, queue a product-index rebuild with `IAlgoliaProductIndexService.RebuildStoreAsync(storeAlias)` or `RebuildAllAsync()`. The existing product indexing worker performs the rebuild in the background.

### Variant indexing

Variants are not indexed by default. Enable variant indexing when products use placeholder parent SKUs and the real sellable SKUs live on variants.

```json
{
  "Ekom": {
    "Algolia": {
      "Indexing": {
        "Variants": true
      },
      "Search": {
        "GroupVariantsByProduct": true
      }
    }
  }
}
```

When enabled, the plugin creates one additional product record per variant. Variant records use the variant SKU as top-level `Sku`, preserve the parent product SKU as `ParentSku`, and include `ProductId` and `VariantId` for grouping and selection.

```json
{
  "objectID": "product-key_variant-key",
  "ProductId": "product-key",
  "VariantId": "variant-key",
  "Sku": "REAL-VARIANT-SKU",
  "ParentSku": "PLACEHOLDER-SKU",
  "IsVariant": true,
  "variantSku": "REAL-VARIANT-SKU",
  "variantTitle": "Black / XL"
}
```

Product indexes are configured with `AttributeForDistinct = ProductId`. `Search:GroupVariantsByProduct` defaults to `true`, so normal searches return grouped product results while SKU searches can still match variant records. Set it to `false` if you want one hit per matching variant.

```json
{
  "Ekom": {
    "Algolia": {
      "Search": {
        "GroupVariantsByProduct": false
      }
    }
  }
}
```

### Additional product properties and metafields

`Indexing:ProductProperties` adds extra product properties and metafields that are not part of the default product record fields listed above. The built-in `summary` and `description` aliases may also be configured with `|striphtml` to transform their top-level record fields. Each entry supports one optional modifier: `|array`, `|int`, `|decimal`, `|unix`, `|unixms`, or `|striphtml`.

```json
{
  "Ekom": {
    "Algolia": {
      "Indexing": {
        "ProductProperties": [
          "channels|array",
          "packageCount|int",
          "weight|decimal",
          "publishedAt|unix",
          "description|striphtml"
        ]
      }
    }
  }
}
```

Metafields can be indexed explicitly with `metafield:<alias>`:

```json
{
  "Ekom": {
    "Algolia": {
      "Indexing": {
        "ProductProperties": [
          "metafield:material",
          "metafield:color",
          "metafield:releaseDate|unix",
          "metafield:longDescription|striphtml"
        ]
      }
    }
  }
}
```

Modifier behavior:

- `|array` parses JSON arrays such as `["Web","Store"]` into Algolia string arrays.
- `|decimal` accepts comma or dot decimal separators, such as `0,1` and `0.0`.
- `|striphtml` converts direct HTML or rich-text JSON with a `markup` property to plain text. It removes script and style content, decodes HTML entities, and normalizes tags and whitespace to single spaces.
- Metafields with `Enable Multiple Choice` enabled are automatically indexed as string arrays. The `|array` modifier can still be used to explicitly index other metafields as arrays.
- Invalid `|array`, `|int`, and `|decimal` values are skipped instead of being indexed as strings.
- Only one modifier is supported for each configured field.

### Facet attributes

`Indexing:AttributesForFaceting` accepts raw Algolia facet expressions for existing top-level record fields. Entries may be plain attribute names or use Algolia modifiers such as `filterOnly(...)`, `searchable(...)`, and `afterDistinct(...)`. Configured entries are trimmed, deduplicated case-insensitively, and merged with facets generated by the plugin.

`Indexing:FacetAttributes` places selected product properties and metafields under the record's `attributes` object and configures them as Algolia facets. The entries use the same aliases and optional modifiers as `ProductProperties`.

```json
{
  "Ekom": {
    "Algolia": {
      "Indexing": {
        "FacetAttributes": [
          "brand",
          "metafield:material",
          "metafield:availableSizes"
        ]
      }
    }
  }
}
```

This produces values such as:

```json
{
  "attributes": {
    "brand": "Nike",
    "material": "Leather",
    "availableSizes": ["M", "L", "XL"]
  }
}
```

`Indexing:VariantFacetAttributes` maps output facet names to properties on either the variant group or variant node. When variant indexing is disabled, each configured facet is added to the product record as a distinct array of values from all variants. When variant indexing is enabled, facets are stored on individual variant records to support combination-safe filtering. This supports two-level variants such as color groups containing size variants:

```json
{
  "Ekom": {
    "Algolia": {
      "Indexing": {
        "Variants": true,
        "VariantFacetAttributes": {
          "color": "variantGroup:title",
          "size": "variant:title"
        }
      }
    }
  }
}
```

For one-level variants where both values are stored on each variant node, use property aliases instead:

```json
"VariantFacetAttributes": {
  "color": "variant:color",
  "size": "variant:size"
}
```

Variant facets are stored on each variant record, so combined filters such as color and size must match the same variant. Product facet attributes are inherited by variant records, and a variant attribute overrides a product attribute with the same output name.

With variants enabled, the plugin configures these facets with Algolia's `afterDistinct` modifier and groups search results by `ProductId`. This provides product-level facet counts while preserving variant-level combinations. Algolia recommends that facet values are consistent within each distinct group, so validate counts for catalogues where one product has many different variant values. A full product reindex is required after adding or changing facet attributes.

### Manual reindexing

Use the backoffice endpoints to rebuild Algolia indexes manually. Both endpoints support `GET` and `POST`.

Rebuild all configured store indexes:

```http
POST /umbraco/backoffice/api/EkomAlgoliaBackoffice/RebuildIndexes
```

Rebuild one store:

```http
POST /umbraco/backoffice/api/EkomAlgoliaBackoffice/RebuildStoreIndexes?storeAlias=Store
```
