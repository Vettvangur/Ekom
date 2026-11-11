# Order reviews

Order reviews flag issues that require a person to review an order. They are independent of payment, fulfillment and order status. The Umbraco 17 and 18 manager displays open issues in the Needs review view, beside order numbers, and above the order detail tabs. The Needs review tab is hidden when its filtered count is zero. Resolving the last alert in that view returns to All. Counts, totals and exports use the same filtered order set; drafts remain excluded.

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

- `coupon-exhausted`: a coupon cannot be claimed at completion because it is missing or has no uses remaining. Checkout still completes and the discount is flagged for review.
- `completion-stock-failed`: completion throws a stock availability error.
- `completion-failed`: completion throws another error (excluding cancellation).

Failure reviews are recorded before the existing failure email is sent, provided the order was loaded. Review persistence failures are logged without replacing the checkout error or preventing an otherwise successful completion. Reviews use a separate, uncancelled write; completion and review recording are not one atomic transaction.

Draft notes survive background updates in the manager. Saving a note on an alert that another user has resolved returns HTTP 409 and keeps the dialog and note available to copy.

## Manager HTTP API

These routes require the existing `UmbracoUser` authorization policy and access to the order's store. Missing orders or reviews return 404; forbidden access returns 403; invalid input returns 400.

| Method | Route | Purpose |
| --- | --- | --- |
| GET | `/ekom/manager/orders/{orderId}/reviews` | Open reviews, newest first |
| POST | `/ekom/manager/orders/{orderId}/reviews` | Raise or reuse an open review |
| POST | `/ekom/manager/orders/{orderId}/reviews/{reviewId}/resolve` | Resolve an open review |
| POST | `/ekom/manager/orders/{orderId}/reviews/{reviewId}/review` | Record a note or resolve; returns 409 if already resolved |

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

Use `{}` when no comment is needed. The authenticated user's name supplies `resolvedBy`; it cannot be supplied in the body. A manager review submitted after the alert was resolved returns HTTP 409 without replacing the original user, time or comment. The server-side `ResolveAsync` API remains idempotent and returns the existing resolution. Resolution does not retry an integration or change the order status. Successful integration retries do not automatically resolve reviews.

Source/code allow 100 characters each, title 200, message/comment 4000 and reviewer name 200. Empty required fields are rejected; blank comments are stored as null. Timestamps represent UTC.

## Order list and count

`GET /ekom/manager/SearchOrders` includes `needsReview` on each returned order. The optional query parameter `needsReview=true` filters to orders with open reviews; `false` filters to orders without them; omitting it leaves all orders eligible.

The existing response `count` and totals respect this filter plus the existing store/date/search filters. Multiple open reviews count as one order. The `needsReview` projection is included in SearchOrders list results; other order APIs retain their existing shapes.

The query uses indexed `EXISTS` checks, does not load review messages, and adds no per-order database calls. It does not deserialize order JSON to determine review status.

## Storage and installation

`EkomOrderReviews` stores each issue and its optional resolution in one row. An index on `(OrderId, ResolvedUtc)` supports open-review checks. No background queue, extra history table, version field or cached counter is used. Raising awaits a short database transaction; reviewing reads and updates the unresolved row and writes its activity log in a serializable transaction.

Table/index creation is repeatable and runs during fresh setup and the existing startup schema checks on Umbraco 13, 17 and 18. Existing rows are preserved. Non-Umbraco hosts must provision the same schema before using these APIs, as with the other Ekom tables.

## Reviewing in the manager

The Review dialog shows the original title, message, source and creation time. Staff can enter what was done and leave **Problem is resolved** checked to clear the alert, or uncheck it to record a note while leaving the issue open. A note is required when keeping an issue open. Every successful review action records the authenticated user and UTC timestamp in the order activity log. Resolution and activity logging commit atomically; repeated resolution does not duplicate the log or overwrite the first resolution.

`POST /ekom/manager/orders/{orderId}/reviews/{reviewId}/review` accepts `{ "comment": "Checked invoice", "resolved": false }`. Omit `resolved` to resolve. No payment, fulfillment, refund or external integration action is performed.

The order view key `NeedsReview` is supported by order search and export. It applies the normal All orders scope plus an unresolved-review predicate, so several alerts on one order still count as one order.
