# Stock reservations

Ekom reservations are native SQL records processed by an in-process .NET `BackgroundService`. They do not use a Hangfire scheduler. Product/variant holds and discount/coupon holds live in `EkomStockReservation` and use the same LINQ2DB connection as the rest of Ekom, on SQL Server or SQLite.

Reservations are optional for the standard checkout, but the explicit service is always registered and available.

## Configuration

```json
{
  "Ekom": {
    "Reservations": {
      "Enabled": false,
      "Timeout": 30,
      "PollInterval": "00:00:30",
      "BatchSize": 100,
      "WorkerEnabled": true,
      "CompletedRetention": "7.00:00:00"
    }
  }
}
```

| Setting | Default | Behavior |
| --- | --- | --- |
| `Enabled` | `false` | When true, standard checkout reserves eligible inventory and line discount/coupon stock before provider submission. Explicit APIs work in either mode. |
| `Timeout` | 30 minutes | Default positive reservation duration. `Configuration.ReservationTimeout` falls back to legacy `Ekom:ReservationTimeout`, then 30 minutes. |
| `PollInterval` | 30 seconds | Delay between expiry sweeps; must be greater than zero and no more than one day. |
| `BatchSize` | `100` | Due rows processed per batch, from 1 through 10,000. Cleanup caps a delete sweep at 1,000 IDs. |
| `WorkerEnabled` | `true` | Enables expiry/cleanup on this node independently of checkout reservation creation. |
| `CompletedRetention` | 7 days | How long terminal rows remain for diagnostics and creation-idempotency recognition; must be nonnegative. |

Options are read at startup. The worker waits until the reservation schema/index setup and Ekom cache initialization have succeeded.

## Explicit service

Inject `Ekom.Services.IStockReservationService`. One request represents one stock identity:

```csharp
var result = await reservations.ReserveAsync(new StockReservationRequest
{
    Key = productOrVariantKey,
    Quantity = 2m,
    StoreAlias = "Store",
    Duration = TimeSpan.FromMinutes(20),
    MinimumRemainingStock = 3m,
    IdempotencyKey = $"{orderId}:{paymentAttemptId}:{lineId}",
    OrderId = orderId.ToString(),
    PaymentAttemptId = paymentAttemptId,
}, ct);
```

For a discount, set `IsDiscount = true`. `Coupon = null` addresses the discount's master stock; a value addresses that exact coupon row. `StoreAlias` is required for product/variant holds when `PerStoreStock` is enabled.

Quantities must be positive, fit `decimal(18,2)`, and use no more than two decimal places. Discount quantities must also be whole `Int32` values. The SQL service enforces physical stock and `MinimumRemainingStock`; callers outside checkout are responsible for catalog eligibility, backorder, and stock-buffer policy.

## Lifecycle

| Method | Transition |
| --- | --- |
| `ReserveAsync` | Atomically deducts stock and inserts an active reservation. Insufficient stock makes no hold. |
| `ConsumeAsync` | Marks an active, unexpired hold consumed without restoring stock. |
| `ReleaseAsync` | Marks an active hold released and restores its persisted quantity once. |
| `ExpireAsync` | Restores an active hold only when due. |

Inspect both `StockReservationResult.Status` and `State`. Creation returns `Created`, `AlreadyExists`, `Conflict`, or `InsufficientStock` as applicable. Repeated terminal operations return explicit outcomes such as `AlreadyConsumed` and `AlreadyReleased`.

Consuming a due active hold first expires/restores it atomically and returns `LateConsumption`; consuming an already expired row does the same. It does not silently deduct stock again. A paid order with `LateConsumption` requires explicit reconciliation, such as obtaining fresh stock or refunding payment.

Release and expiry perform the conditional active-to-terminal transition and stock restoration in one SQL transaction. If restoration fails, the transition rolls back.

## Idempotency

`IdempotencyKey` is optional. Ekom hashes and uniquely indexes it independently of database collation.

- The same key and same payload returns the original ID with `AlreadyExists`, even after that row is terminal.
- Reusing the key with changed stock identity, quantity, kind, store, coupon, duration, or order/payment associations returns `Conflict`.
- Omitting a key intentionally creates a separate hold on every call.
- After terminal cleanup deletes the row, the same key can create a new hold.

Choose a key that identifies a payment attempt and keep retention longer than expected retries. After an uncertain network outcome, retry with the same key.

## Attach a hold to an order

An external hold must be created with the target `OrderId` and matching store metadata before it can be attached:

```csharp
if ((result.Status is StockReservationStatus.Created or StockReservationStatus.AlreadyExists)
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

Attachment validates ownership, stock kind/key/identity, store, coupon, aggregate quantity, active state and expiry. Arbitrary unowned IDs cannot be claimed. `RemoveReservationsFromOrderAsync` only removes order metadata; release or consume the reservation first.

## Checkout behavior

With `Enabled = false`, checkout validates inventory during payment preparation and deducts it during completion. With `Enabled = true`, preparation creates holds before payment submission and saves their IDs to the order. Backorder lines are skipped by the default stock policy, while effective stock buffers become `MinimumRemainingStock`.

Preparation ownership is coordinated in `EkomCheckoutPreparation`, one order at a time across nodes. A competing request cannot adopt or compensate another preparation's holds. Partial failure releases only holds created by that preparation. Once a save or payment submission may have happened, Ekom retains ownership/holds rather than guessing whether the operation committed.

Retries reuse matching active holds without extending expiry. Changed requirements, terminal holds, expired holds, or a completed order reject another payment attempt. Payment-provider idempotency remains the provider integration's responsibility.

Completion verifies attached IDs against the order's eligible requirements, consumes covered active holds, deducts only uncovered quantities, and inserts an `EkomCheckoutStockCompletion` receipt in one SQL transaction. Existing holds are honored even if `Enabled` is later switched off. Completion receipts are retained without scheduled cleanup.

`EkomCheckoutPreparation` ownership has no timed takeover. A crashed owner or uncertain save can block further preparation. There is no automatic recovery endpoint: stop or confirm the old request, inspect order reservation IDs and provider state, preserve submitted/protected IDs, and only then repair the owner row. The expiry worker can release due stock but does not unlock preparation ownership.

## Worker behavior

The worker polls for due active rows in bounded batches and expires them. Multiple nodes may run it; conditional SQL transitions ensure only one restores a row. Run the worker on at least one Ekom node. Disable it on request-only nodes only when another node owns sweeping.

The worker also deletes terminal rows older than `CompletedRetention`. SQL deadlock/contention and unique-key races have bounded retries. Sweep failures use exponential backoff with jitter up to roughly five minutes; per-record failures also back off so one broken row does not permanently block a batch.

Expiry is polling, not a deadline guarantee. Downtime, locks, backlog, and clock skew can leave a due hold unavailable beyond its timestamp. Keep node clocks synchronized and monitor old active records.

Metrics are emitted from the `Ekom.Reservations` meter:

- `ekom.reservations.outcomes` with a `status` tag
- `ekom.reservations.expiry.failures`
- `ekom.reservations.worker.failures`
- `ekom.reservations.cleaned`

The feature has no reservation administration UI. Logs include reservation IDs/outcomes but omit coupon values and idempotency keys.
