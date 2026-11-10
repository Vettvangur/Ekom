# Order reviews

Order reviews flag issues that require a person to review an order. They are independent of payment, fulfillment and order status. Ekom.Core provides persistence, a service and manager endpoints; no manager UI is included yet.

## Raise an issue from an integration

Inject `IOrderReviewService` from `Ekom.Services` (registered by the standard Ekom ASP.NET Core registrations):

```csharp
await reviews.RaiseAsync(
    orderId,
    source: "dk-accounting",
    code: "invoice-push-failed",
    title: "Accounting push failed",
    message: "The request timed out. Check whether the invoice exists in DK.",
    ct: cancellationToken);
```

The order must already exist. Source and code are trimmed and lowercased. A matching unresolved issue for the same order/source/code is returned unchanged, including its original message and creation time. Use a different code when two problems need separate review. After resolution, a later failure creates a new review.

The service is intended for trusted server integrations. HTTP endpoints enforce manager and store access. Do not include secrets or raw exception dumps in messages; treat messages and comments as plain text when building the UI.

## Automatic checkout reviews

Checkout completion automatically raises reviews with source `checkout` for:

- `completion-stock-failed`: completion throws a stock availability error.
- `completion-failed`: completion throws another error (excluding cancellation).
- `coupon-exhausted`: the coupon has no uses left; the order still completes.

Failure reviews are recorded before the existing failure email is sent, provided the order was loaded. Coupon reviews are recorded before the activity-log alert. Review persistence failures are logged without replacing the checkout error or preventing an otherwise successful completion. Reviews use a separate, uncancelled write; completion and review recording are not one atomic transaction.

## Manager HTTP API

These routes require the existing `UmbracoUser` authorization policy and access to the order's store. Missing orders or reviews return 404; forbidden access returns 403; invalid input returns 400.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/ekom/manager/orders/{orderId}/reviews` | Open reviews, newest first |
| POST | `/ekom/manager/orders/{orderId}/reviews` | Raise or reuse an open review |
| POST | `/ekom/manager/orders/{orderId}/reviews/{reviewId}/resolve` | Resolve once, preserving the first resolution |

GET supports `includeResolved=true` for history, `skip` (default 0) and `take` (default 50, maximum 200). History includes open and resolved reviews. All responses use camelCase property names.

Raise body:

```json
{
  "source": "dk-accounting",
  "code": "invoice-push-failed",
  "title": "Accounting push failed",
  "message": "The request timed out. Check whether the invoice exists in DK."
}
```

Resolve body:

```json
{ "comment": "Confirmed the invoice exists in DK." }
```

Use `{}` when no comment is needed. The authenticated user's name supplies `resolvedBy`; it cannot be supplied in the body. Resolving an already resolved review returns its existing resolution without replacing the user, time or comment. Resolution does not retry an integration or change the order status. Successful integration retries do not automatically resolve reviews.

Source/code allow 100 characters each, title 200, message/comment 4000 and reviewer name 200. Empty required fields are rejected; blank comments are stored as null. Timestamps represent UTC.

## Order list and count

`GET /ekom/manager/SearchOrders` includes `needsReview` on each returned order. The optional query parameter `needsReview=true` filters to orders with open reviews; `false` filters to orders without them; omitting it leaves all orders eligible.

The existing response `count` and totals respect this filter plus the existing store/date/search filters. Multiple open reviews count as one order. The `needsReview` projection is included in SearchOrders list results; other order APIs retain their existing shapes.

The query uses indexed `EXISTS` checks, does not load review messages, and adds no per-order database calls. It does not deserialize order JSON to determine review status.

## Storage and installation

`EkomOrderReviews` stores each issue and its optional resolution in one row. An index on `(OrderId, ResolvedUtc)` supports open-review checks. No background queue, extra history table, version field or cached counter is used. Raising awaits a short database transaction; resolving uses a conditional update of an unresolved row.

Table/index creation is repeatable and runs during fresh setup and the existing startup schema checks on Umbraco 13, 17 and 18. Existing rows are preserved. Non-Umbraco hosts must provision the same schema before using these APIs, as with the other Ekom tables.
