# Order reviews

[Ekom docs](../README.md) / [Guides](README.md) / Order reviews

An order review flags a problem on an order that a person has to look at, for example a failed push to an accounting system or a payment that does not match the order. Ekom raises some reviews itself, and your integrations can raise their own. A review stays open until someone resolves it in the manager.

Reviews are separate from the order, payment and fulfillment statuses. Raising or resolving a review never changes a status.

On this page:

- [Where reviews appear in the manager](#where-reviews-appear-in-the-manager)
- [Raise a review from your integration](#raise-a-review-from-your-integration)
- [Reviews Ekom raises itself](#reviews-ekom-raises-itself)
- [Resolve a review in the manager](#resolve-a-review-in-the-manager)
- [Manager HTTP API](#manager-http-api)
- [Filter the order list by review](#filter-the-order-list-by-review)
- [How reviews are stored](#how-reviews-are-stored)

## Where reviews appear in the manager

In the Umbraco 17, 18 and 19 manager, open reviews appear in three places:

- the **Needs review** view of the order list,
- beside the order number in the list,
- above the tabs in the order details.

The **Needs review** view lists every order with an open review, whatever its status, drafts included. An order that was paid but whose checkout did not complete is still a draft, so it appears here. Every other view leaves drafts out.

The view is hidden when no order matching the current filters needs review. When you resolve the last review in the view, the manager goes back to **All orders**. Counts, totals and exports use the same filtered orders as the list.

## Raise a review from your integration

Inject `IOrderReviewService` from `Ekom.Services`. The standard Ekom registrations add it.

```csharp
using Ekom.Services;

await reviews.RaiseAsync(
    orderId,
    source: "dk-accounting",
    code: "invoice-push-failed",
    title: "Accounting push failed",
    message: "The request timed out. Check whether the invoice exists in DK.",
    ct: ct);
```

- The order must exist. Otherwise Ekom throws `KeyNotFoundException`.
- Ekom trims all four values and converts `source` and `code` to lower case.
- If the order already has an open review with the same source and code, Ekom returns that review unchanged, with its original message and time. Use a different code when two problems need separate reviews.
- After a review is resolved, raising it again creates a new review. A resolved review is never reopened.
- Fixing the problem in your integration does not resolve the review. A person resolves it in the manager.

| Field | Required | Maximum length |
| --- | --- | --- |
| `source` | Yes | 100 characters |
| `code` | Yes | 100 characters |
| `title` | Yes | 200 characters |
| `message` | Yes | 4,000 characters |

An empty, blank or too long value throws `ArgumentException`.

The service is for trusted server code and does not check who is calling. The HTTP endpoints below check manager and store access. Do not put secrets or full exception dumps in messages. Treat messages and comments as plain text when you show them.

## Reviews Ekom raises itself

### Checkout completion

When checkout completes, Ekom raises reviews with the source `checkout`:

| Code | When |
| --- | --- |
| `coupon-exhausted` | The order's coupon could not be marked as used because it no longer exists or has no uses left. Checkout still completes. Check the discount on the order. |
| `completion-stock-failed` | Completion failed because there was not enough stock. |
| `completion-failed` | Completion failed with any other error. A cancelled request does not raise a review. |

For the two failure codes, Ekom raises the review before it sends the failure email, as long as the order could be loaded. If saving the review fails, Ekom logs the error. That never replaces the checkout error and never stops a completion that would otherwise succeed. The review is saved separately from the completion, in its own write that is not cancelled with the request.

### Recorded payments

When a payment is recorded on the order, Ekom raises reviews with the source `payment`:

| Code | When |
| --- | --- |
| `amount-mismatch` | The captured amount for the order, or the amount of an authorization, differs from the amount to charge by 1 or more in the order's currency. Smaller differences are ignored, because gateways round to whole units. The payment is still recorded and the order still completes. |
| `paid-checkout-changed` | A payment arrived for a checkout that had been changed or released before it arrived. The order was not completed and is still a draft. Compare the payment with the order, then complete the order or refund the payment. |

The message gives the amounts and the payment ID. A repeated gateway callback does not raise a second review. If saving the review fails, Ekom logs the error and keeps the payment record. See [Payments](payments.md).

## Resolve a review in the manager

1. Open the order and click **Review** on the alert.
2. The dialog shows the title, message, source and time of the review. Describe what you did.
3. Leave **Problem is resolved** checked to close the review. Uncheck it to save a note and leave the review open. A note is required when you leave it open.
4. Click **Mark as reviewed**, or **Save note** when you left the review open.

Every saved review action writes your user name and the UTC time to the order's activity log. The resolution and the log entry are saved together in one transaction. Resolving a review does not retry the integration, change the order status, or do any payment, fulfillment or refund work.

Your note stays in the dialog while the manager refreshes the order in the background. If another user resolved the review while you were writing, saving returns HTTP 409. The dialog stays open with your note, so you can copy it.

## Manager HTTP API

These endpoints need a signed-in backoffice user (the `UmbracoUser` policy), manager access, and access to the order's store. All responses use camelCase property names.

| Method | Route | What it does |
| --- | --- | --- |
| GET | `/ekom/manager/orders/{orderId}/reviews` | Lists open reviews, newest first. |
| POST | `/ekom/manager/orders/{orderId}/reviews` | Raises a review, or returns the open one with the same source and code. |
| POST | `/ekom/manager/orders/{orderId}/reviews/{reviewId}/review` | Saves a note, or resolves the review. |
| POST | `/ekom/manager/orders/{orderId}/reviews/{reviewId}/resolve` | The same as `/review`. |

The list takes `includeResolved=true` to include resolved reviews, `skip` (default `0`) and `take` (default `50`, from `1` to `200`).

Body to raise a review:

```json
{
  "source": "dk-accounting",
  "code": "invoice-push-failed",
  "title": "Accounting push failed",
  "message": "The request timed out. Check whether the invoice exists in DK."
}
```

Body to resolve a review, or to save a note:

```json
{ "comment": "Confirmed the invoice exists in DK.", "resolved": true }
```

- `resolved` defaults to `true`. Send `"resolved": false` with a comment to save a note and keep the review open.
- Send `{}` to resolve without a comment.
- A blank comment is saved as `null`. A comment can be at most 4,000 characters.
- `resolvedBy` comes from the signed-in user. You cannot set it in the body.

| Status code | When |
| --- | --- |
| 400 | A value is missing, blank or too long, `skip` or `take` is out of range, or a note was sent without a comment. |
| 401 | Ekom cannot find the signed-in user's name. |
| 403 | No manager access, or no access to the order's store. |
| 404 | No order or no review with that ID. |
| 409 | The review is already resolved. The first resolution, with its user, time and comment, is kept. |

### Resolve from C#

`IOrderReviewService` has two methods for this:

- `ReviewAsync(orderId, reviewId, reviewedBy, comment, resolved, ct)` works like the HTTP endpoints. It throws `InvalidOperationException` when the review is already resolved.
- `ResolveAsync(orderId, reviewId, resolvedBy, comment, ct)` can be called more than once. When the review is already resolved, it returns the review as it is, keeps the first resolution and writes nothing to the activity log.

Both return `null` when there is no review with that ID on the order.

## Filter the order list by review

`GET /ekom/manager/SearchOrders` returns `needsReview` on each order. Add `needsReview=true` to list only orders with an open review, or `needsReview=false` for orders without one. Leave it out to list all orders.

The `count` and totals in the response follow this filter, together with the store, date and search filters. An order with several open reviews counts once. Other order APIs do not return `needsReview`.

In the manager's order filter, search and export, the view key `NeedsReview` selects orders in every status, drafts included, that have an open review.

The filter uses an indexed `EXISTS` check. It does not load review messages, does not make a database call per order, and does not read the order JSON.

## How reviews are stored

Each review, with its resolution, is one row in the `EkomOrderReviews` table. An index on `(OrderId, ResolvedUtc)` makes it fast to find open reviews. Times are in UTC.

- Raising a review runs a short serializable transaction.
- Resolving or saving a note reads and updates the open row and writes the activity log entry in one serializable transaction.
- There is no background queue, history table or cached counter.

Ekom creates the table and index when it installs, and checks them at every startup on Umbraco 17, 18 and 19. This is safe to repeat and keeps existing rows. If you run Ekom outside Umbraco, create the same table before you use these APIs, as for the other Ekom tables.

## Related pages

- [Order, payment, and fulfillment statuses](order-statuses.md): the statuses that reviews sit beside.
- [Payments](payments.md): the payment records that can raise reviews.
- [Checkout](checkout.md): how completion works, and where it can fail.
- [Manager orders](../manager/orders.md): the order list and details where staff resolve reviews.
- [HTTP API reference](../reference/http-api.md): every manager endpoint.

[Back to Guides](README.md)
