[Ekom docs](../README.md) / [Reference](README.md) / Configuration reference

# Configuration reference

Ekom reads its settings from the `Ekom` section of `appsettings.json`, or from any other ASP.NET Core configuration source such as environment variables or user secrets. This page lists every setting, its type, its default and what it changes.

Keys are not case-sensitive. A setting you leave out uses the default in the table. To learn how a feature works before you change its settings, follow the guide link under each heading.

```json
{
  "Ekom": {
    "PerStoreStock": true,
    "BasketCookieLifetime": 7,
    "Reservations": {
      "Enabled": true
    }
  }
}
```

On this page:

- [Catalog and search](#catalog-and-search)
- [Baskets and orders](#baskets-and-orders)
- [Prices and VAT](#prices-and-vat)
- [Stock](#stock)
- [Stock reservations](#stock-reservations)
- [Storefront HTTP API](#storefront-http-api)
- [Manager access](#manager-access)
- [Manager order-status events](#manager-order-status-events)
- [Variant App](#variant-app)
- [Order-discount integration](#order-discount-integration)
- [Headless revalidation](#headless-revalidation)
- [Tracking](#tracking)
- [Payments](#payments)
- [Printing](#printing)
- [A complete example](#a-complete-example)

## Catalog and search

Guide: [Catalog and products](../guides/catalog-and-products.md).

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:ExamineSearchIndex` | string | `ExternalIndex` | The Examine index that catalog search queries. |
| `Ekom:GlobalCatalog` | bool | `false` | `true`: when a product or category is not found in the requested store, Ekom looks for it in the other stores. The product lookup methods take a `global` argument that overrides this setting. The category lookup methods search all stores when either the argument or this setting is `true`. Route lookups always stay in one store. |
| `Ekom:CategoryRootLevel` | int | `3` | The Umbraco content level of the top-level categories. `GetRootCategories` returns the categories at this level. A value that is not a whole number throws `FormatException` when root categories are read, not at startup. |
| `Ekom:CustomImage` | string | `images` | The property alias Ekom reads images from on products, variants and variant groups. |
| `Ekom:DefaultProductOrderBy` | `OrderBy` | `DateDesc` | The sort order a new `ProductQuery` starts with. Values: `UmbracoSortOrderAsc`, `UmbracoSortOrderDesc`, `TitleAsc`, `TitleDesc`, `PriceAsc`, `PriceDesc`, `DateAsc`, `DateDesc`, `SkuAsc`, `SkuDesc`, `Score`, `NoOrder`. An unknown value is treated as `DateDesc`. |
| `Ekom:AbsoluteUrls` | bool | `false` | `true`: the product and category URLs that Ekom reports to Umbraco include the domain, with one URL for each store domain. Use it on sites with several domains. |
| `Ekom:SeedCache` | bool | `false` | `true`: at startup, Ekom asks Umbraco to load all published Ekom catalog nodes (catalog, stores, categories, products, variant groups and variants) into its content cache. |

## Baskets and orders

Guides: [Orders and baskets](../guides/orders-and-baskets.md), [Order, payment, and fulfillment statuses](../guides/order-statuses.md).

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:BasketCookieLifetime` | number (days) | `1` | How long the basket cookie lives. Decimals are allowed, for example `0.5` for 12 hours. |
| `Ekom:ShareBasket` | bool | `false` | `true`: one basket is shared by all stores. The stores must offer the same currencies. A store's `shareBasketBetweenStores` property overrides this setting for that store. |
| `Ekom:UserBasket` | bool | `false` | `true`: a signed-in member has one basket, stored on the member instead of in a cookie. A store's `userBasket` property overrides this setting for that store. |
| `Ekom:AutoClosePaidAndFulfilledOrders` | bool | `true` | `true`: an open order closes automatically when it is paid and fulfilled. An order that staff reopen in the manager does not close automatically again. |
| `Ekom:Stores:{storeAlias}:AutoClosePaidAndFulfilledOrders` | bool | not set | Overrides `Ekom:AutoClosePaidAndFulfilledOrders` for one store. |
| `Ekom:EmailNotifications` | string | not set | Recipient for emails Ekom sends through `IMailService` when the caller names no recipient. Separate several addresses with commas. When no recipient is known, nothing is sent. The SMTP server comes from `Umbraco:CMS:Global:Smtp`. |

## Prices and VAT

Guides: [Stores](../guides/stores.md), [Database price storage](../guides/database-price-storage.md).

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:ApplyVatOnShipping` | bool | `false` | `true`: VAT is added to shipping prices. A store's `applyVatOnShipping` property overrides this setting for that store. |
| `Ekom:VatCalcRounding` | `Rounding` | `AwayFromZero` | How VAT amounts are rounded. Values: `None`, `RoundDown`, `RoundUp`, `RoundToEven`, `AwayFromZero`. An unknown value is treated as `AwayFromZero`. |
| `Ekom:VatIncludedPerUnitPolicy` | enum | `PreserveStickerGross` | For prices that include VAT. `PreserveStickerGross`: the line total is always the shelf price times the quantity. `LineLevelVat`: VAT is calculated again from the line's net amount, so the unit VAT and the order VAT can differ. |
| `Ekom:VatRoundingScope` | enum | `PerUnit` | Where VAT is rounded: `PerUnit` or `PerTotal`. |
| `Ekom:UseDatabasePrices` | bool | `false` | `true`: Ekom reads price overrides from the `EkomPrice` table, so an ERP system can change prices without publishing content. See [Database price storage](../guides/database-price-storage.md). |

## Stock

Guide: [Stock](../guides/stock.md).

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:PerStoreStock` | bool | `false` | `false`: one stock count per product or variant, and store aliases passed to the stock methods are ignored. `true`: one count per product and store. |
| `Ekom:DisableStock` | bool | `false` | `true`: Ekom no longer rejects an order line because there is too little stock. |

## Stock reservations

A reservation holds stock for a customer while they pay. The settings bind to `StockReservationOptions` from `Ekom:Reservations`. Guide: [Stock reservations](../guides/reservations.md).

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:Reservations:Enabled` | bool | `false` | `true`: checkout reserves stock before payment. Write `true` or `false`; other values count as `false`. |
| `Ekom:Reservations:Timeout` | number (minutes) | `30` | How long a reservation lasts before it expires. |
| `Ekom:Reservations:PollInterval` | TimeSpan | `00:00:30` | How often the background worker looks for expired reservations. Must be above zero and at most one day. |
| `Ekom:Reservations:BatchSize` | int | `100` | How many reservations the worker handles in one pass. Must be from 1 to 10000. |
| `Ekom:Reservations:WorkerEnabled` | bool | `true` | `false`: the background worker does not expire or clean up reservations on this server. |
| `Ekom:Reservations:CompletedRetention` | TimeSpan | `7.00:00:00` | How long finished reservations are kept before the worker deletes them. Cannot be negative. |
| `Ekom:Reservations:Stores:{storeAlias}:Enabled` | bool | not set | Overrides `Enabled` for one store. |
| `Ekom:Reservations:Stores:{storeAlias}:Timeout` | number (minutes) | not set | Overrides `Timeout` for one store. |
| `Ekom:ReservationTimeout` | number (minutes) | not set | Older name for the timeout. Ekom reads it only when `Ekom:Reservations:Timeout` is not set. |

The worker settings apply to the whole server. Only `Enabled` and `Timeout` can be set per store.

```json
{
  "Ekom": {
    "Reservations": {
      "Enabled": true,
      "Timeout": 30,
      "PollInterval": "00:00:30",
      "BatchSize": 100,
      "WorkerEnabled": true,
      "CompletedRetention": "7.00:00:00",
      "Stores": {
        "express": {
          "Enabled": false,
          "Timeout": 10
        }
      }
    }
  }
}
```

## Storefront HTTP API

These settings protect the public routes under `/ekom`. Guides: [Headless storefronts](../guides/headless.md); reference: [HTTP API](http-api.md).

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:Api:MaxRequestBodySize` | number (bytes) | `128000` | Largest request body accepted by `/ekom/order`, `/ekom/checkout`, `/ekom/mvcCheckout`, `/ekom/catalog`, `/ekom/provider`, `/ekom/api` and `/ekom/order-discounts`. A larger request gets `413 Payload Too Large`. `0` turns the limit off. Backoffice and manager routes are not limited by this setting. |
| `Ekom:Search:PublicFields` | string array | `nodeName`, `sku`, `title`, `description`, `searchTags`, `id` | The index fields an HTTP catalog request may name in `searchFields`. Other fields are dropped from the request. Calls from your own C# code are not restricted. |

## Manager access

The manager is Ekom's backoffice section for orders, customers and analytics. These settings bind to `ManagerOptions` from `Ekom:Manager`. Guide: [Manager access and permissions](../manager/access-and-permissions.md).

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:Manager:SectionAccessGroup` | string | not set | Comma-separated aliases of the Umbraco user groups that may open the manager. |
| `Ekom:Manager:StoreGroupPermissions:{storeAlias}` | string array | not set | The Umbraco user groups that may work with one store's orders. |
| `Ekom:SectionAccessRules` | string | not set | The older name of `Ekom:Manager:SectionAccessGroup`, with the same format. Ekom reads it only when `SectionAccessGroup` and `StoreGroupPermissions` are both empty. Use `SectionAccessGroup` on a new site. |
| `Ekom:Manager:Customers:IdentityField` | string | `CustomerEmail` | How the Customers view groups orders into customers: `CustomerEmail` or `CustomerUsername`. Any other value stops the site at startup. See [Customers](../manager/customers.md). |

```json
{
  "Ekom": {
    "Manager": {
      "SectionAccessGroup": "ekom,commerce-admins",
      "StoreGroupPermissions": {
        "store": [ "store-editors", "commerce-admins" ]
      }
    }
  }
}
```

How access is decided:

- Umbraco administrators can always open the manager and every store.
- Manager access: a user needs one of the groups in `SectionAccessGroup` or in any `StoreGroupPermissions` entry. When both are empty, Ekom adds no group check of its own.
- Store access: when `StoreGroupPermissions` is empty, every manager user can work with every store. When it has entries, a store without an entry is closed to everyone except administrators. Store aliases and group aliases are matched without regard to case.

## Manager order-status events

`Ekom:Manager:OrderStatusFireEventsByDefault` (bool, not set by default) decides whether a status change made in the manager raises Ekom's status events by default. Staff can still change the **Trigger status events** switch for each change. Guide: [Order, payment, and fulfillment statuses](../guides/order-statuses.md).

```json
{
  "Ekom": {
    "Manager": {
      "OrderStatusFireEventsByDefault": false
    }
  }
}
```

| Value | Status changes from the order list | **Trigger status events** on the order details |
| --- | --- | --- |
| Not set | Raise events | Off |
| `false` | Do not raise events | Off |
| `true` | Raise events | On |

The setting applies only to the manager. It does not change what happens when your code or a payment provider changes a status. Status events can run any handler your site has registered, not only email notifications. See [Status change events](events.md#status-change-events).

## Variant App

The Variant App lets editors manage the variants of a product in one view. Guide: [Variant App](../guides/variant-app.md).

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:VariantApp:VariantGroups` | string array | empty | Extra property aliases on `ekmProductVariantGroup` to show in the Variant App. |
| `Ekom:VariantApp:Variants` | string array | empty | Extra property aliases on `ekmProductVariant` to show in the Variant App. |

## Order-discount integration

Another system, such as a point-of-sale or ERP system, can ask Ekom to calculate a coupon discount through `/ekom/order-discounts`. See [Order-discount integration](http-api.md#order-discount-integration) in the HTTP API reference.

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:OrderDiscountCalculation:ApiKey` | string | not set | The key callers send in the `X-Ekom-Api-Key` header. While it is not set, every request to `/ekom/order-discounts` gets `401 Unauthorized`. Keep it in user secrets or environment variables, not in source control. |

## Headless revalidation

When a product, variant, variant group or category is published, Ekom can tell a headless frontend which page URLs to rebuild. Guide: [Headless storefronts](../guides/headless.md).

`Ekom:Headless:ReValidateApis` is a list. Each entry has these properties:

| Property | Type | Description |
| --- | --- | --- |
| `Store` | string | The store alias. Only URLs in this store are sent. |
| `Url` | string | The frontend endpoint Ekom posts to. |
| `Secret` | string | Sent as the `token` query string value on every call. |

Ekom posts `{ "urls": "<comma-separated URLs>" }` as JSON to `{Url}?token={Secret}`. Because the secret is in the URL, it also appears in Ekom's error log when a call fails. When the section is missing or the list is empty, Ekom does not send revalidation calls.

## Tracking

Tracking sends purchase and basket events to Google Analytics 4 and Meta. The settings bind to `TrackingOptions` from `Ekom:Tracking`. Guide: [Tracking and consent](../guides/tracking-and-consent.md).

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:Tracking:Enabled` | bool | `false` | Turns tracking on. |
| `Ekom:Tracking:CaptureEnabled` | bool | `true` | Captures attribution data from storefront requests automatically. |
| `Ekom:Tracking:LogEventData` | bool | `false` | Logs the data of every tracking event. The data can contain customer details. |
| `Ekom:Tracking:LogPurchaseEventData` | bool | `false` | Logs the data of purchase events only. |
| `Ekom:Tracking:CookieName` | string | `EkomTracking` | Name of the attribution cookie. |
| `Ekom:Tracking:CookieLifetimeDays` | number (days) | `30` | Lifetime of the attribution cookie. |
| `Ekom:Tracking:SiteBaseUrl` | string | not set | Absolute base URL of the site. Meta uses it to build the event source URL when the order has no landing URL. |
| `Ekom:Tracking:Stores` | list | empty | Per-store base URLs. Each entry has `Alias` and `SiteBaseUrl`, which overrides the global value for that store. |

### Consent

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:Tracking:Consent:FallbackAnalyticsConsent` | bool | `false` | Analytics consent to assume when the request carries none. |
| `Ekom:Tracking:Consent:FallbackMarketingConsent` | bool | `false` | Marketing consent to assume when the request carries none. |
| `Ekom:Tracking:Consent:AnalyticsCookieName` | string | not set | Cookie that holds analytics consent. |
| `Ekom:Tracking:Consent:AnalyticsHeaderName` | string | not set | Request header that holds analytics consent. |
| `Ekom:Tracking:Consent:MarketingCookieName` | string | not set | Cookie that holds marketing consent. |
| `Ekom:Tracking:Consent:MarketingHeaderName` | string | not set | Request header that holds marketing consent. |
| `Ekom:Tracking:Consent:Stores` | list | empty | Per-store overrides. Each entry has `Alias` and any of the six settings above. |

The consent headers are consent input only. They do not authenticate requests to Ekom.

### GA4 and Meta

`Ekom:Tracking:Ga4` and `Ekom:Tracking:Meta` have the same settings, except the two that are GA4 only.

| Key under `Ga4` or `Meta` | Type | Default | Description |
| --- | --- | --- | --- |
| `Enabled` | bool | `false` | Turns this provider on. |
| `Testing` | bool | `false` | GA4: the default for `UseDebugEndpoint` and `DebugMode`. Meta: sends the store's `TestEventCode` with each event; without a code, Ekom logs a warning and sends the event normally. |
| `UseDebugEndpoint` | bool | not set | GA4 only. Sends events to the GA4 debug endpoint (`debug/mp/collect`). Not set: follows `Testing`. |
| `DebugMode` | bool | not set | GA4 only. Turns on GA4 debug mode. Not set: follows `Testing`. |
| `Dispatching:Capacity` | int | `1000` | How many events can wait to be sent. |
| `Dispatching:MaxConcurrency` | int | `2` | How many events are sent at the same time. |
| `Events:AddedToCart` | bool | `false` | Send an event when a line is added. |
| `Events:RemovedFromCart` | bool | `false` | Send an event when a line is removed. |
| `Events:StartedCheckout` | bool | `false` | Send an event when a customer email is first added to the basket. |
| `Events:AddedShippingInfo` | bool | `false` | Send an event when a shipping provider is chosen. |
| `Events:AddedPaymentInfo` | bool | `false` | Send an event when a payment provider is chosen. |
| `Stores` | list | empty | Credentials per store. Each entry has `Alias`, `MeasurementId`, `ApiSecret`, `PixelId`, `AccessToken` and `TestEventCode`. GA4 uses `MeasurementId` and `ApiSecret`; Meta uses `PixelId`, `AccessToken` and `TestEventCode`. |

Keep `ApiSecret` and `AccessToken` out of source control and logs.

## Payments

A gateway's settings, such as its merchant ID and secret key, are read from the payment provider node first. A setting that is empty on the node is read from `Ekom:Payments:<base provider name>:<setting>`, for example `Ekom:Payments:borgun:secretCode`, or from `Ekom:Payments:<configurationKey>:<setting>` when the node has a `configurationKey`. The setting names are the properties of each package's settings class, such as `BorgunSettings`. See [Where a gateway's settings come from](../guides/providers.md#where-a-gateways-settings-come-from).

Ekom defines one key that the provider packages share. Ekom binds it into `Ekom.Payments.PaymentsConfiguration`, and only the provider packages act on it:

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:Payments:SendEmailAlerts` | bool | `true` | `true`: when handling a gateway callback throws an exception, the provider package emails the exception and the request URL. This applies to the callbacks of Borgun, Valitor, Netgiro, Straumur, AltaPay (success and fail), PayTrail and Siminn Pay, and to Teya Consumer Loans when recording an approved loan fails. `false`: no email; the error is still logged. A callback that cannot be verified throws no exception, so it sends no email. |

The email is sent through `Ekom.Payments.Core`'s mail service. It uses the SMTP server in `Umbraco:CMS:Global:Smtp` (`Host`, `Port`, `Username`, `Password`, `From`) and sends to `Umbraco:CMS:Content:Notifications:Email`.

Two packages read further keys of their own:

| Key | Read by |
| --- | --- |
| `Ekom:Payments:Valitor:VerificationCode` | `ValitorResponseHelper.Verify`, when you call it without a verification code. |
| `Ekom:Payments:altapay:BaseAddress`, `ApiUserName`, `ApiPassword`, `HostOverride` | The `AltaPay` HTTP client and `AltaService`, which are registered only when your site calls `services.AddAltaPay(configuration)`. The AltaPay payment request itself does not use them. |

## Printing

The optional `Ekom.Printer` package prints picking slips on a warehouse printer through a Windows agent. Its settings are under `Ekom:Printing` and are checked at startup when `Ekom:Printing:Enabled` is `true`. The package README lists every key: see [Ekom.Printer](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/Printing/Ekom.Printer/README.md).

| Key | Type | Default | Description |
| --- | --- | --- | --- |
| `Ekom:Printing:Enabled` | bool | `false` | Turns printing on. |
| `Ekom:Printing:Stations` | list | empty | The print stations. At least one is required when printing is on. |
| `Ekom:Printing:HtmlRetentionDays` | int | `30` | Days to keep printed HTML. |
| `Ekom:Printing:HistoryRetentionDays` | int | `90` | Days to keep print history. Must be at least `HtmlRetentionDays`. |
| `Ekom:Printing:OfflineAfterSeconds` | int | `120` | Seconds without contact before a station counts as offline. Minimum `30`. |
| `Ekom:Printing:ClaimTimeoutSeconds` | int | `300` | Seconds a station has to finish a print job it has claimed. From `30` to `3600`. |
| `Ekom:Printing:ManagerGroups` | string array | empty | Backoffice groups that may retry, cancel and reprint. Administrators always can. |

## A complete example

```json
{
  "Ekom": {
    "PerStoreStock": true,
    "GlobalCatalog": false,
    "CategoryRootLevel": 3,
    "BasketCookieLifetime": 7,
    "DefaultProductOrderBy": "DateDesc",
    "VatCalcRounding": "AwayFromZero",
    "VatRoundingScope": "PerUnit",
    "VatIncludedPerUnitPolicy": "PreserveStickerGross",
    "Reservations": {
      "Enabled": true,
      "Timeout": 30
    },
    "Manager": {
      "SectionAccessGroup": "ekom"
    },
    "OrderDiscountCalculation": {
      "ApiKey": "set-this-in-user-secrets"
    },
    "Tracking": {
      "Enabled": false,
      "CaptureEnabled": true
    }
  }
}
```

## Related pages

- [Configure Ekom](../getting-started/configuration.md): the settings most sites set first.
- [Stock](../guides/stock.md): what `PerStoreStock` and `DisableStock` change.
- [Stock reservations](../guides/reservations.md): how reservations behave at checkout.
- [Manager access and permissions](../manager/access-and-permissions.md): worked examples of the manager settings.
- [HTTP API](http-api.md): the routes the storefront settings protect.
- [Services and dependency injection](services-and-dependency-injection.md): the options classes and services Ekom registers.

[Back to Reference](README.md)
