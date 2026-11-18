# Stock reservations

[Ekom docs](../README.md) / [Guides](README.md) / Stock reservations

A reservation, also called a hold, takes stock out of the sellable count for a limited time, so that two customers cannot pay for the same last unit. Turn reservations on when stock is scarce and customers pay through an online gateway that takes minutes to finish. You can also create holds from your own code, for example for a quote or a phone order.

A hold covers one stock identity: a product, a variant, a discount, or one coupon code of a discount. Ekom stores holds in the `EkomStockReservation` table, in the same database as the rest of Ekom (SQL Server or SQLite). A background worker inside the site gives expired holds back.

The service behind holds, `Ekom.Services.IStockReservationService`, is always registered. The setting `Ekom:Reservations:Enabled` only controls whether the built-in checkout creates holds by itself.

On this page:

- [Turn on reservations for checkout](#turn-on-reservations-for-checkout)
- [What checkout does with holds](#what-checkout-does-with-holds)
- [Hold stock from your own code](#hold-stock-from-your-own-code)
- [Read the result of a reservation call](#read-the-result-of-a-reservation-call)
- [Retry safely with an idempotency key](#retry-safely-with-an-idempotency-key)
- [Attach a hold to an order](#attach-a-hold-to-an-order)
- [How expired holds are given back](#how-expired-holds-are-given-back)
- [When checkout stays blocked](#when-checkout-stays-blocked)
- [Monitor reservations](#monitor-reservations)

## Turn on reservations for checkout

```json
{
  "Ekom": {
    "Reservations": {
      "Enabled": true,
      "Timeout": 30,
      "PollInterval": "00:00:30",
      "BatchSize": 100,
      "WorkerEnabled": true,
      "CompletedRetention": "7.00:00:00"
    }
  }
}
```

| Setting | Default | Effect |
| --- | --- | --- |
| `Enabled` | `false` | `true`: the built-in checkout creates holds before it sends the customer to payment. `false`: checkout checks stock before payment and takes it when the order completes. The service works in both cases. |
| `Timeout` | `30` | How long a hold lasts, in minutes. Used when a request does not set its own `Duration`. |
| `PollInterval` | `00:00:30` | Time between two runs of the expiry worker. Must be above zero and at most one day. |
| `BatchSize` | `100` | How many due holds the worker handles in one run. From 1 to 10,000. |
| `WorkerEnabled` | `true` | `false`: this server does not run the expiry worker. Checkout still creates holds. |
| `CompletedRetention` | `7.00:00:00` (7 days) | How long finished holds (consumed, released or expired) stay in the table before the worker deletes them. Must not be negative. |

`Timeout` falls back to the older setting `Ekom:ReservationTimeout` when `Ekom:Reservations:Timeout` is not set.

You can set `Enabled` and `Timeout` for one store under `Ekom:Reservations:Stores:{storeAlias}`. A store value wins over the global value.

```json
{
  "Ekom": {
    "Reservations": {
      "Enabled": false,
      "Stores": {
        "Store": { "Enabled": true, "Timeout": 20 }
      }
    }
  }
}
```

`PollInterval`, `BatchSize`, `WorkerEnabled` and `CompletedRetention` are read once at startup. Ekom checks them when it creates the worker and throws `ArgumentOutOfRangeException` if one is out of range, also when `WorkerEnabled` is `false`.

## What checkout does with holds

This section describes the built-in checkout with `Enabled` set to `true`. [Checkout](checkout.md) describes the whole payment flow.

### Before payment

When the customer submits payment, Ekom works out what the order needs:

- One unit of stock per ordered unit, for each product, or for each variant of a product with variants. Lines that do not need stock are skipped. By default these are lines whose product has backorder turned on. See [Changing which lines need stock](stock.md#changing-which-lines-need-stock).
- One use of discount stock per discounted line, not per unit. Ekom holds one use of the coupon code when the line has a coupon, and one use of the discount's own stock when the discount has it.
- Lines that use the same stock are added together.

The stock buffer of a product or variant becomes the `MinimumRemainingStock` of its hold, so a hold never takes stock below the buffer. See [How Ekom decides that a product is available](stock.md#how-ekom-decides-that-a-product-is-available).

Ekom then creates the holds and saves their IDs on the order. If any hold cannot be created, Ekom releases the holds it created in this step and returns a stock error to the customer. Holds from earlier steps stay as they are.

Active holds that the order already owns count toward what it needs, and Ekom does not extend them. Released and expired holds from earlier attempts stay in the table until cleanup, but a new attempt never uses them again. If a hold ID still attached to the order points to a released or expired hold, or if the order already has a consumed hold, Ekom refuses the payment.

### When the customer is sent to the gateway

All holds of the order get the same deadline: the earliest deadline among them. Ekom never moves a deadline later. If the order has no holds, the payment attempt still ends after the store's `Timeout`.

### When the customer cancels, the gateway reports an error, or the basket changes

Ekom releases the holds of the current payment attempt right away and sets the order back to **Incomplete**. The next payment submission checks stock again and creates new holds with a new deadline.

Releasing holds does not cancel a payment at the gateway. If the gateway later reports a successful payment for a released attempt, Ekom does not complete the order. It flags the order for review instead. See [A payment that arrives after its holds were released](checkout.md#a-payment-that-arrives-after-its-holds-were-released).

If the customer closes the payment page without coming back, the holds stay until their deadline and the worker gives the stock back.

### When the order completes

In one database transaction, Ekom:

1. checks the holds saved on the order against what the order needs now,
2. consumes the holds that are still active, without changing stock again,
3. takes the part that no hold covers straight from stock,
4. writes a completion receipt to `EkomCheckoutStockCompletion`.

If any of this fails, nothing is saved. The receipt makes completion safe to repeat: a second completion of the same order does not take stock again. Ekom keeps receipts and never deletes them.

Holds already saved on an order are honoured even if you set `Enabled` to `false` later.

## Hold stock from your own code

Inject `IStockReservationService`. One request holds one stock identity.

```csharp
using Ekom.Models;
using Ekom.Services;

StockReservationResult result = await reservations.ReserveAsync(new StockReservationRequest
{
    Key = variantKey,
    Quantity = 2m,
    StoreAlias = "Store",
    Duration = TimeSpan.FromMinutes(20),
    MinimumRemainingStock = 3m,
    IdempotencyKey = $"quote:{quoteId}:{variantKey}",
    OrderId = order.UniqueId.ToString(),
}, ct);

if (result.Status == StockReservationStatus.InsufficientStock)
{
    // Not enough stock. No hold was created and stock is unchanged.
}
```

| Field | Meaning |
| --- | --- |
| `Key` | The product, variant or discount key. Required. |
| `Quantity` | How much to hold. Above zero, at most 2 decimal places. For a discount, a whole number. |
| `MinimumRemainingStock` | Stock that must be left after the hold, for example a stock buffer. Default `0`. Not used for discounts. |
| `IsDiscount` | `true` to hold discount stock instead of product stock. |
| `Coupon` | With `IsDiscount`: `null` holds the discount's own stock, a code holds that coupon's stock. Not allowed on product holds. |
| `StoreAlias` | Required for product holds when `Ekom:PerStoreStock` is `true`. Set it to the order's store if you will attach the hold to an order. |
| `Duration` | How long the hold lasts. Leave it out to use the store's `Timeout`. |
| `IdempotencyKey` | Optional. See [Retry safely with an idempotency key](#retry-safely-with-an-idempotency-key). |
| `OrderId`, `PaymentAttemptId` | Optional links to an order and a payment. `OrderId` is required if you want to attach the hold to that order. |

Ekom throws `ArgumentException` or `ArgumentOutOfRangeException`, and creates nothing, when:

- `Key` is empty,
- `Quantity` is zero or less, has more than 2 decimal places, or is not a whole number for a discount,
- `MinimumRemainingStock` is negative,
- `Coupon` is set on a product hold,
- a text value is longer than 255 characters,
- `IdempotencyKey` is set but blank,
- `Ekom:PerStoreStock` is `true` and a product hold has no `StoreAlias`,
- `Duration` is negative.

Creating a hold takes the stock in the same database transaction. A product hold also updates Ekom's in-memory stock and raises `StockEvents.StockChangedAsync`, as any other stock change does.

The service only checks stock and `MinimumRemainingStock`. When you call it yourself, you decide whether the product can be sold at all: backorder, the stock buffer and whether the product is published are your responsibility.

### Finish a hold

| Method | What it does |
| --- | --- |
| `ConsumeAsync(id)` | The hold is used: the stock stays taken. Use it when the sale is final. |
| `ReleaseAsync(id)` | The hold is given back: its quantity returns to stock. Works on an active hold even after its deadline. |
| `ExpireAsync(id)` | Gives the hold back only if its deadline has passed. Otherwise returns `NotDue`. The worker uses this. |

Each method changes the hold and the stock in one database transaction. If the stock cannot be restored, for example because the stock row was deleted, the method throws `InvalidOperationException` and the hold stays active. A hold gives its stock back at most once, however often you call these methods.

## Read the result of a reservation call

Every method returns a `StockReservationResult` with `ReservationId`, `Status`, `State` (`Active`, `Consumed`, `Released` or `Expired`) and `ExpiresUtc`. Check `Status` and `State`, not only whether the call threw.

| Status | Meaning |
| --- | --- |
| `Created` | A new hold was created and the stock was taken. |
| `AlreadyExists` | A hold with the same idempotency key and the same request already exists. `ReservationId` is the existing hold. Check `State`: it may already be finished. |
| `Conflict` | The idempotency key was used before with a different request. Nothing was created. |
| `InsufficientStock` | Not enough stock above `MinimumRemainingStock`, or no stock row exists. Nothing was created. |
| `Consumed`, `Released`, `Expired` | The method finished the hold. |
| `AlreadyConsumed`, `AlreadyReleased`, `AlreadyExpired` | The hold was already finished. Nothing changed. |
| `NotDue` | `ExpireAsync` was called before the deadline. Nothing changed. |
| `NotFound` | No hold has this ID. |
| `LateConsumption` | `ConsumeAsync` was called after the deadline. See below. |

### A late consumption needs your attention

When you consume a hold after its deadline, Ekom does not take the stock again. If the hold was still active, Ekom expires it and gives the stock back in the same transaction. If the worker had already expired it, nothing changes. In both cases the result is `LateConsumption`.

If you have taken payment for that hold, you must reconcile it yourself: find stock for the order, or refund the customer.

## Retry safely with an idempotency key

Set `IdempotencyKey` when a call may be retried, for example after a timeout. Use a key that names the payment or quote the hold belongs to, and retry with the same key.

- Same key, same request: you get the original hold back with `AlreadyExists`, even if that hold is already finished.
- Same key, different request: `Conflict`. These fields count: `Key`, `Quantity`, `IsDiscount`, `StoreAlias`, `Coupon`, `OrderId`, `PaymentAttemptId`, `MinimumRemainingStock` and `Duration`.
- No key: every call creates a new hold.

Ekom stores a SHA-256 hash of the key, so upper and lower case and the database collation do not matter. When the worker deletes a finished hold after `CompletedRetention`, its key can create a new hold. Keep `CompletedRetention` longer than the time in which a retry can arrive.

## Attach a hold to an order

To make checkout count a hold you created yourself, attach it to the order with `Order.AddReservationsToOrderAsync`.

```csharp
if (result.Status is StockReservationStatus.Created or StockReservationStatus.AlreadyExists
    && result.State == StockReservationState.Active
    && result.ExpiresUtc > DateTime.UtcNow)
{
    await orderApi.AddReservationsToOrderAsync(
        [result.ReservationId!],
        order,
        order.StoreInfo.Alias,
        ct);
}
```

Ekom only attaches a hold that:

- was created with `OrderId` set to this order,
- has the order's store alias in `StoreAlias`,
- matches a stock identity the order needs, with the same coupon,
- is active and before its deadline.

The holds attached for one stock identity must not add up to more than the order needs. If any condition fails, Ekom throws `StockException` and attaches nothing. You cannot attach a hold that was created for another order or for no order.

You cannot attach or remove holds while a payment attempt is active, or after the order is paid. Ekom throws `CheckoutConflictException` in that case.

`RemoveReservationsFromOrderAsync(storeAlias)` only removes the hold IDs from the current order. It does not give stock back. Release or consume the holds first.

## How expired holds are given back

The worker is a background service inside each site. On each run it:

1. gives back up to `BatchSize` holds whose deadline has passed, oldest first,
2. deletes up to `BatchSize` finished holds older than `CompletedRetention`, and never more than 1,000 in one run,
3. waits `PollInterval`.

It starts only after Ekom has created its tables and loaded its caches.

Several servers can run the worker at the same time. Each change is a conditional database update, so only one server gives back each hold. Run the worker on at least one server. Turn it off with `WorkerEnabled: false` only on servers where another server does the work.

When one hold cannot be given back, the worker logs a warning and waits before it tries that hold again: 5 seconds after the first failure, doubling each time, up to 5 minutes. Other holds are not held up by it. When a whole run fails, the worker waits longer before the next run, up to about 5 minutes. Deadlocks and duplicate-key races are retried up to 5 times inside each call.

The deadline is not exact. A hold stays out of stock after its deadline until the worker reaches it: while the site is down, while the database is locked, or while there is a backlog. Keep server clocks in sync, and watch for active holds that are long past their deadline.

## When checkout stays blocked

While Ekom prepares checkout for an order, it records an owner for that order in `EkomCheckoutPreparation`. Only one request per order can prepare checkout at a time, on any server. Another request gets HTTP 409 and cannot take over or release the first request's holds.

If a request fails while it takes ownership, before it has done any work, Ekom removes only the owner it just wrote. It does not release any stock.

If a request stops after it may have saved holds or started payment, Ekom keeps the owner and the holds rather than guess what happened. The owner has no timeout: a crashed request blocks further checkout for that order until a person acts. There is no automatic recovery. The expiry worker gives back stock from due holds but does not remove the owner.

To unblock the order, a store editor uses **Release Lock** in the Manager. Before you do:

1. Make sure the old request has stopped.
2. Check with the payment provider that no payment is being processed for the order.
3. Then use **Release Lock**. It resets checkout, releases active holds and keeps a record of the state before the reset.

Release Lock is your decision, based on what you checked. It does not cancel a payment at the gateway. See [Checkout lock and manual recovery](../manager/orders.md#checkout-lock-and-manual-recovery).

## Monitor reservations

Ekom reports these metrics on the `Ekom.Reservations` meter:

| Metric | Counts |
| --- | --- |
| `ekom.reservations.outcomes` | Every reservation call, tagged with `status`. |
| `ekom.reservations.expiry.failures` | Holds the worker could not give back. |
| `ekom.reservations.worker.failures` | Worker runs that failed. |
| `ekom.reservations.cleaned` | Finished holds deleted. |

Logs include reservation IDs and outcomes. They do not include coupon codes or idempotency keys.

The order activity log records:

- each checkout release, with the payment attempt, the reason, and whether the release finished or could not be confirmed. The outcome of each single hold goes to the application log.
- each hold of the order that the worker gives back, with the hold ID, the stock key, the quantity and the quantity returned to stock.

Writing the activity log never gives stock back twice. If an expiry log entry cannot be written, Ekom writes an error to the application log and the stock change stands.

There is no screen for reservations in the backoffice.

## Related pages

- [Stock](stock.md): read and change the stock that holds take from.
- [Checkout](checkout.md): the payment flow that creates, releases and consumes holds.
- [Failed gift-card preparation recovery](giftcard-recovery.md): holds on gift cards at an external provider.
- [Orders in the Manager](../manager/orders.md): release holds and unlock checkout by hand.
- [Upgrading reservations](../upgrading/reservations.md): moving from the older Hangfire reservations.
- [Configuration reference](../reference/configuration-reference.md): all `Ekom` settings.

[Back to Guides](README.md)
