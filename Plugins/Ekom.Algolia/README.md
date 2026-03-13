# Ekom.Algolia

[![Nuget](https://img.shields.io/nuget/vpre/Ekom.Algolia?color=ed0f0f)](https://www.nuget.org/packages/Ekom.Algolia/)
[![Publish Ekom.Algolia](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-algolia.yml/badge.svg?branch=Ekom)](https://github.com/Vettvangur/Ekom/actions/workflows/publish-ekom-algolia.yml)

Algolia integration plugin for Ekom (Umbraco).

## Features
- Product indexing with background queue/worker.
- Algolia Insights events for view, add-to-cart, checkout, purchase.
- Index naming convention: `{primary|replica|query_suggestions}.ENVIRONMENT[.Domain].ENTITY[_sorted_by_{asc|desc}_ATTRIBUTE][.Locale][.Currency]`.

## Install

Register services:

```csharp
using Ekom.Algolia;

services.AddAlgolia();
```

## Configuration (appsettings.json)

```json
{
  "Ekom": {
    "Algolia": {
      "Enabled": true,
      "ApplicationId": "APP_ID",
      "AdminApiKey": "ADMIN_API_KEY",
      "InsightsApiKey": "INSIGHTS_API_KEY",
      "Environment": "prod",
      "Domain": "example",
      "Indexing": {
        "Enabled": true,
        "Products": true,
        "BatchSize": 1000,
        "IncludeAllProperties": false,
        "ProductProperties": [
          "title",
          "summary",
          "description"
        ],
        "Dispatching": {
          "MaxBatchSize": 100,
          "FlushIntervalSeconds": 2,
          "MaxQueueSize": 10000,
          "MaxConcurrency": 2
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
          "Domain": "example"
        }
      ]
    }
  }
}
```

## Notes
- Indexing triggers from Umbraco content notifications for `ekmProduct`.
- Store `Locale` and `Currency` now come from the Ekom store resolved by alias, so `appsettings.json` only needs the store alias and optional domain.
- Request and order context decide which culture and currency suffix is used; background indexing falls back to the store's default culture/currency.
- Variants are not indexed by default.
