# Analytics reporting

Ekom analytics is an opt-in, best-effort reporting projection. It reads persisted orders and writes dedicated reporting tables. It does not participate in order saves, payment transactions, stock reservations or gift-card lifecycle operations. There are no order-view controls in this phase; administrative operations are available through authenticated manager endpoints.

## Configuration

Configure `Ekom:Analytics`:

```json
{
  "Ekom": {
    "Analytics": {
      "Enabled": true,
      "ScheduledRefreshEnabled": true,
      "RefreshInterval": "00:30:00",
      "LookbackWindow": "7.00:00:00",
      "BatchSize": 250,
      "DelayBetweenBatches": "00:00:00.250",
      "DatabaseCommandTimeoutSeconds": 30,
      "LeaseDuration": "00:05:00",
      "CustomerIdentityPolicyVersion": 1
    }
  }
}
```

Analytics is disabled by default. Disable scheduling separately to keep manual processing available. Options are read when services are constructed; restart instances after configuration changes. Invalid configuration or unavailable analytics schema must not block checkout. The schema is initialized independently when analytics is used, rather than making it a required order migration. Batch size is limited to 10,000 orders and lookback to 366 days; use a full rebuild for older history. Timer durations and SQL timeout conversions are bounded, and the lease must exceed the command timeout by at least five seconds and exceed the batch delay.

Tune batch size, delay and SQL timeout using your production workload. A 30-minute interval is not a strict freshness guarantee: batch execution takes additional time. Database reads and writes still consume shared resources, so load-test alongside checkout before increasing throughput.

## Facts and financial definitions

The reporting tables are `EkomAnalyticsOrders`, `EkomAnalyticsOrderLines` and `EkomAnalyticsPromotionApplications`. Jobs and a database lease coordinate background processing across instances.

- `GrandTotal`: order value after discounts, including VAT, shipping and payment fees, before gift cards.
- `GrandTotalWithoutVat`: the saved pre-gift-card net order value.
- `ChargedAmount`: remaining amount payable, not proof of settlement.
- Merchandise sales: saved discounted line amounts, excluding provider fees.
- Items: quantities respecting `CountToTotal`; product reports expose actual line quantities.
- Saved discount measures retain Ekom's calculation semantics. The general `DiscountAmount` can follow the source-price VAT basis and is not universally gross. Promotion rule amounts are not realized monetary savings.

The mapper reads saved JSON values without constructing live pricing objects or looking up current catalog prices. Missing required financial fields fail projection rather than silently becoming zero. No historical grand-total conversion is performed.

`Incomplete` orders are excluded. Refreshing an incomplete order removes its reporting facts only. An individual refresh replaces all related facts atomically; failures retain the previous successful projection. Reports can therefore be stale or incomplete, and failed orders are not automatically retried.

Refund accounting, verified gift-card-only payment buckets, customer lifetime summaries, current storefront URL enrichment and exact stacked-promotion savings attribution are not supplied by this phase.

## Customer identity

The default `IAnalyticsCustomerIdentityResolver` uses saved customer email, trimmed and normalized case-insensitively. It does not strip dots or plus suffixes, and does not silently fall back to member ID or SSN. Missing email leaves customer identity unknown.

Customer association is the combination of store and an identity key derived from identity type and normalized value. Reports always require a store. The projection also retains the available name/email and source customer identifier, so restrict reporting access appropriately; hashed identity keys do not make the associated customer data anonymous.

Applications can register a replacement `IAnalyticsCustomerIdentityResolver` through DI. Return `AnalyticsCustomerIdentity(Type, Value)` using a stable application identity. Increase `CustomerIdentityPolicyVersion` and explicitly rebuild affected stores when changing identity policy. Ordinary rebuilds keep reports available but can temporarily mix old and new policy rows; coordinate report use during such a change.

## Background jobs

Scheduled jobs use a fixed lookback cutoff and candidates whose created, updated or paid date falls in that window. They do not rely on a timestamp cursor being a complete change feed. Old changes outside the window can remain stale; use manual refresh or a rebuild to repair them.

Rebuilds are store-scoped. They capture the maximum operational `ReferenceId` and process bounded keyset batches, not increasing SQL offsets or an in-memory list of all orders. New orders beyond that boundary are picked up later. Each order has a short independent analytics transaction, and the checkpoint advances after each batch, including failures. Existing reporting tables are not cleared before rebuilding. This first implementation revisits existing source orders; it does not sweep orphan facts for operational orders that have been deleted.

Only one bulk job owns the database lease at a time. A paused or interrupted job must be resumed or cancelled explicitly. Jobs do not automatically resume after a restart; expired ownership is reported as interrupted. Resuming may repeat a partially completed batch, which is safe because projection replaces facts rather than incrementing totals. No dedicated failed-order retry queue exists.

Projection failures are logged in the application's structured logs and, where possible, in the existing order activity log. Logging failures cannot affect order processing. Operational order JSON and frozen payment snapshots are never rewritten by analytics.

## Administrative endpoints

All routes require resolved authenticated backoffice-user groups in addition to the existing Umbraco manager authorization and permitted-store access. A storefront member or empty manager permission configuration does not grant access. Requests that start work use POST, not GET. No UI changes are required to invoke these endpoints through an authenticated administrative client.

| Method | Route | Operation |
|---|---|---|
| POST | `/ekom/manager/analytics/orders/{orderId}/refresh` | Refresh one saved order immediately |
| POST | `/ekom/manager/analytics/rebuilds` | Start a background rebuild; body `{"store":"Store"}` |
| GET | `/ekom/manager/analytics/jobs?store=Store` | Discover recent jobs, including scheduled and interrupted jobs |
| GET | `/ekom/manager/analytics/jobs/{jobId}` | Read progress and checkpoint |
| POST | `/ekom/manager/analytics/jobs/{jobId}/control` | Body `{"command":"pause"}`, `resume` or `cancel` |

Starting a rebuild returns HTTP 202 and a job-status location. A conflicting job returns 409. Disabled or unavailable processing returns an unavailable response. Permission checks apply to the store associated with an order or job, not just a caller-supplied store string.

## Reporting endpoints

New endpoints query reporting facts only; the existing frontend and legacy manager chart endpoints are not automatically replaced.

All report filters require `store`, ISO `currency`, `start` and exclusive `end`. `status` defaults to `CompletedOrders`; `AllOrders` still excludes incomplete baskets. The completed status set matches the existing manager definition. `dateBasis` is `Created` (default) or `Paid`; paid-date reporting excludes orders without a paid date. Dates use the existing persisted order date convention without implicit timezone conversion; callers must supply boundaries consistently.

| GET route under `/ekom/manager/analytics` | Result |
|---|---|
| `/sales` | Grand-total sales, merchandise, payable amounts, fees, items, distinct known customers, equal-length previous-period totals and latest matching projection time |
| `/daily-sales` | Daily sales and order counts |
| `/products` | Product rankings; `variants=true` groups selected variants separately |
| `/distributions/status` | Order-status counts/shares |
| `/distributions/payment` | Payment-provider counts/shares |
| `/distributions/shipping` | Shipping-provider counts/shares |
| `/distributions/shipping-method` | Shipping-method counts/shares |
| `/promotions` | Distinct associated orders and sales by promotion/coupon |
| `/orders` | Order facts and customer association; optional `customerIdentityKey` filter |

Products, promotions and orders accept `page` and `pageSize` (1-200). Shares are ratios from zero to one. Product revenue share uses merchandise revenue, not charged amount. Coupon-associated sales are not a claim of causal attribution, and sales across promotions are not additive when an order has several promotions. Missing providers remain an unknown bucket; zero payable amount is not sufficient evidence of gift-card-only settlement.

## Rollout and validation

1. Enable analytics in a controlled environment and verify schema permissions.
2. Start an explicit rebuild for each store.
3. Compare projected rows and reports with saved orders, including gift cards, variants, provider fees and coupons.
4. Check failure counts and logs; refresh individual failed orders after correcting their source data.
5. Enable scheduled refresh and tune lookback/batch settings.
6. Measure SQL plans and contention using realistic line counts and million-order workloads before treating default settings as production sizing. Keyset pagination limits batch size, not the amount of source data examined by a sparse date-window query. This feature does not automatically add indexes to operational orders. On SQL Server, evaluate a covering `(StoreAlias, ReferenceId)` source index including `UniqueId`, `OrderStatusCol`, `CreateDate`, `UpdateDate` and `PaidDate` during a controlled maintenance window; evaluate date-specific indexes if discovery remains expensive. Account for order-write overhead before adding them.

Reports during a rebuild reflect a mixture of old and refreshed facts. Job status and projection timestamps communicate progress but do not certify completeness or financial correctness.
