# Native SQL stock reservations

Ekom stores single-item product/variant and discount/coupon reservations in
`EkomStockReservation`, using the existing LINQ2DB `DatabaseFactory` connection for
SQL Server or SQLite. Hangfire is no longer a dependency and Ekom no longer registers
Hangfire storage or jobs. Umbraco U10, U17 and U18 migrations add the reservation table
and indexes. U18 compiles the shared U17 implementation.

## Defaults and checkout

**Default checkout continues to validate and deduct stock without creating reservations.**
Set `Ekom:Reservations:Enabled` to `true` to reserve eligible inventory and line
discount/coupon stock before payment. Registering Ekom automatically registers `IStockReservationService` and its .NET
`BackgroundService`. The worker starts sweeping only after schema/index setup and
successful cache initialization. It expires reservations explicitly created by an
integration, checkout, or an existing reservation API call. `WorkerEnabled` is independent
of `Enabled`; explicit APIs continue to work when checkout reservations are disabled.

Default checkout is useful when payment is short-lived, stock is plentiful, or a shop
prefers to leave inventory available until payment completion. Explicit reservations
are useful for scarce inventory or longer payment redirects, at the cost of tying up
inventory for abandoned attempts and handling late payment reconciliation.

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

`Reservations.Timeout` is numeric **minutes**, defaulting to 30. The public virtual
`Configuration.ReservationTimeout` resolves nested `Ekom:Reservations:Timeout` first,
then legacy `Ekom:ReservationTimeout`, then 30. Request `Duration` and worker duration
properties use .NET `TimeSpan`. Configuration is
read at startup; restart after changing these worker settings.

| Setting | Default | Why choose a lower/disabled value | Why choose a higher/enabled value |
| --- | --- | --- | --- |
| `Reservations.Enabled` | `false` | Checkout validates stock and deducts on completion. Explicit APIs and outstanding holds remain supported. | Reserve eligible stock before requesting payment. |
| `Reservations.Timeout` | 30 minutes | Shorter holds return abandoned stock sooner; shoppers have less time to finish payment. | Longer holds accommodate slow payment providers; stock stays unavailable longer after abandonment. Must be positive when creating a reservation. |
| `Reservations.PollInterval` | 30 seconds | Faster sweeps reduce expiry lag; generate more database traffic. | Slower sweeps reduce idle database load; expired stock remains unavailable longer. Valid range: greater than zero through one day. |
| `Reservations.BatchSize` | 100 | Smaller batches reduce per-sweep work and contention, especially on SQLite; drain backlogs more slowly. | Larger batches drain backlogs faster; increase database work and sweep duration. Range: 1-10,000. Cleanup additionally caps each sweep at 1,000 IDs to fit SQL Server parameter limits. |
| `Reservations.WorkerEnabled` | `true` | Set `false` on request-only nodes when another Ekom node owns sweeping, or during controlled maintenance. Explicit create/consume/release still work; overdue records accumulate if no worker is running. | Leave `true` for automatic restart recovery. Multiple enabled nodes may sweep the same records; conditional SQL transitions arbitrate the stock restoration. |
| `Reservations.CompletedRetention` | 7 days | Shorter retention reduces table growth, but shortens lifecycle diagnostics and the creation-idempotency window. Zero permits cleanup on the next sweep. | Longer retention preserves retry recognition and diagnosis longer, at the cost of more storage. Choose a period longer than payment/webhook retries and operational reconciliation. Must be nonnegative. |

Expiration timestamps are UTC. Sweeping is polling, not a deadline guarantee: downtime,
backlog, locks or failures can delay restoration. Keep clocks synchronized across nodes.

## Explicit reservation service

Inject `Ekom.Services.IStockReservationService`. A request represents **one** stock row:

```csharp
var result = await reservations.ReserveAsync(new StockReservationRequest
{
    Key = productOrVariantKey,
    Quantity = 2m,
    StoreAlias = "main", // required for product/variant reservations with PerStoreStock
    Duration = TimeSpan.FromMinutes(20), // omit to use ReservationTimeout
    IdempotencyKey = $"{orderId}:{paymentAttemptId}:{lineId}",
    OrderId = orderId.ToString(),
    PaymentAttemptId = paymentAttemptId,
}, ct);

if ((result.Status is StockReservationStatus.Created or StockReservationStatus.AlreadyExists)
    && result.State == StockReservationState.Active && result.ExpiresUtc > DateTime.UtcNow)
{
    await Order.Instance.AddReservationsToOrderAsync(
        new[] { result.ReservationId! }, order, "main", ct);
}
```

Use `IsDiscount = true` and an optional `Coupon` for discount stock. `Coupon = null`
means the master discount row; a non-null coupon means that exact coupon row. The
service persists the resolved stock row identity, store, coupon, quantity and optional
order/payment-attempt associations. Expiry does not depend on an ambient HTTP store,
nor on later `PerStoreStock` configuration changes. Associations are metadata; they do
not update an order, validate payment ownership or authorize a transition.

Quantities must be positive, have at most two decimal places, and fit `decimal(18,2)`.
Discount quantities must also be whole numbers within `Int32`. Product/variant
reservations use the ordinary stock table, not warehouse display balances. The caller
is responsible for catalog eligibility, backorder and configured stock-buffer policy;
the SQL service enforces sufficient physical stock plus the request's optional
`MinimumRemainingStock` (default zero). Checkout supplies the effective catalog stock
buffer and skips inventory for backorder lines, matching the existing checkout policy.

### Lifecycle and results

- `ReserveAsync`: conditional stock deduction and reservation insertion commit in one
  transaction. Missing/insufficient stock returns `InsufficientStock` without a hold.
- `ConsumeAsync`: completes an active, unexpired reservation without restoring stock.
  Repeating it returns `AlreadyConsumed`.
- `ReleaseAsync`: cancels an active hold and restores the persisted quantity once.
  Repeating it returns `AlreadyReleased`. A consumed reservation is not restored.
- `ExpireAsync`: restores an active due hold; returns `NotDue` before its deadline.
- Consuming a due active hold first expires/restores it atomically and returns
  `LateConsumption`. Consuming an already expired record also returns
  `LateConsumption`. A released hold returns `AlreadyReleased`; a missing/cleaned
  record returns `NotFound`. None of these outcomes mean payment stock was secured.
- Expire/release workers use a conditional active-to-terminal SQL update and the
  stock restoration inside the **same transaction**. If stock restoration fails, the
  terminal transition rolls back as well.

On `LateConsumption`, reconcile the payment explicitly (for example, refund it or
secure fresh stock under a new attempt). The service does not silently deduct again.
Results include the persisted state and expiry when a record exists; these are a
snapshot, so always inspect the later consume result. Infrastructure failures throw;
they are not reported as successful lifecycle results.

### Idempotency

The optional caller key is hashed and uniquely indexed in SQL, independently of SQL
Server's string collation. With no key, each call deliberately creates a distinct
reservation. With the same key and payload, a retry returns the existing ID and
`AlreadyExists`, **even if the original reservation is terminal**; it neither extends
nor reactivates the hold. Changed key, quantity, kind, store, coupon, effective duration
or associations with a reused idempotency key returns `Conflict`.

After terminal cleanup deletes the row, the key can create a new reservation. Choose
retention accordingly and use a new attempt-specific key for a genuinely new hold.
After an uncertain network/commit outcome, retry creation with the same key. Without
creation idempotency, callers cannot safely assume that an exception means no hold was
committed.

### Integrating checkout

The protected checkout hooks and processing event signatures are preserved. With
`Enabled=true`, the standard payment paths reserve product/variant stock (including
per-store identities), plus one coupon/master-discount use per discounted line, before
calling the payment provider. Order-level coupons retain their separate mark-used policy.
Processing events can disable inventory validation/reservation; discount policy remains
independent. IDs are saved through `AddReservationsToOrderAsync` before requesting payment.

Preparation is exclusively owned per order through `EkomCheckoutPreparation` in SQL,
across application nodes. A competing request fails before it can adopt, save or
compensate holds. The row also retains the IDs protected by successful persistence;
a later request with a stale order snapshot cannot release those IDs on failure.
Ownership is retained until compensation finishes or persisted IDs are protected.

Partial preparation failure releases holds created by that preparation, including
legacy holds immediately saved by a line hook. Their IDs are removed from the order
before ownership is released. Earlier submitted/recovered holds and their order
associations are preserved. Cancellation does not cancel compensation.
Once a save starts, a thrown save can mean an uncertain commit: ownership and
holds are retained for reconciliation rather than released. Reservation attachment
saves log post-commit cache and subscriber failures without reporting a failed save;
pre-save errors still propagate. Previously persisted holds are preserved on retries.
After payment submission starts, an exception can mean
an uncertain provider outcome: holds are retained for reconciliation or expiry.

Retries reuse active holds without extending their lifetime, and recover checkout holds
left by an interrupted order save. Changed stock requirements, consumed holds, expired
holds and completed orders reject a new payment request. Expired/released/missing holds
on completion fail explicitly; they are never silently replaced. Reconcile late payments
and use a new order for a genuinely new payment after expiry. Provider charge idempotency
still depends on the payment integration; stock idempotency is not charge idempotency.

Completion validates each attached reservation's order ID, stock kind/key, exact stock
identity, store, coupon and aggregate quantity against the order's eligible requirements.
Checkout-created holds additionally carry a requirements fingerprint. Explicit holds must
include the order ID and matching store metadata. Legacy product/variant wrappers called
inside the current preparation can be atomically associated when attached (see below);
arbitrary unowned IDs are rejected.
Changing the stock scope (for example `PerStoreStock`) during payment requires reconciliation.

In both checkout modes, a SQL transaction consumes verified active holds, deducts only
uncovered quantities (including partial holds), and inserts an `EkomCheckoutStockCompletion`
receipt. A failed deduction rolls back the entire completion transaction. Receipts are
retained independently of reservation cleanup to recognize duplicate stock completion.
`CompleteCheckoutEventArgs.StockValidation=false` skips uncovered inventory deductions,
but still verifies/consumes existing holds and applies line discount stock policy.
Turning `Enabled` off never causes existing holds to be deducted again.
Preparation verifies IDs supplied by hooks, IDs saved directly on the order, and recovered
order-owned holds, even with `Enabled=false` and without a call to the base line hook.
It verifies again after the final save, before invoking the payment hook. Completion also
recovers owned holds when order metadata is incomplete and inventory validation is disabled.
Completion and external attachment reject an order with an active preparation owner.

Order status updates, coupon mark-used, activity logs, checkout events and external payment
effects are outside this stock transaction and may run again on callback retries. Subscribers
must be idempotent. Receipt retention is currently unbounded; no receipt cleanup is scheduled.
Preparation uses compensating single-reservation transactions, not an atomic multi-line hold.
Concurrent provider charge submissions must still be idempotent in the payment integration.

Preparation ownership has **no automatic timed takeover**. Existing order-save APIs
cannot fence a paused process out after a lease timeout; allowing takeover would make
compensation unsafe again. A crashed owner, uncertain save, failed protection write or
failed compensation therefore blocks new preparation for that order. The reservation
worker can still expire its stock holds, but does not unlock preparation. To reconcile,
first stop/confirm termination of the owning request, inspect persisted order IDs and
payment state, preserve any attached/submitted IDs in `ProtectedIds`, and only then clear
`Owner` if retry is appropriate. Expired holds still require payment reconciliation.
There is currently no admin API or automatic recovery job for this operation. Deploy
all checkout nodes with this protocol; older nodes and custom direct reservation/order
mutations do not participate in ownership coordination.

### Wholesale-only backorders and legacy overrides

Register the public `Ekom.Services.ICheckoutStockPolicy` to use the same eligibility rule
in normal preparation, legacy reservation attachment, and payment completion. The default
policy retains the existing exemption for **all** backorder lines. A custom controller
that exempts only wholesale customers **must register its policy explicitly**; Ekom cannot
infer arbitrary override intent. Policy implementations are shared singletons and must be
thread-safe, using durable order/customer data rather than the current HTTP user.

For an integration whose trusted customer model exposes `customer.IsDistributor`:

```csharp
using Ekom.Models;
using Ekom.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

public sealed class WholesaleCheckoutStockPolicy : ICheckoutStockPolicy
{
    public bool RequiresStock(IOrderInfo order, IOrderLine line)
    {
        var wholesale = order.CustomerInformation.Customer.Properties
            .TryGetValue("isDistributor", out var value)
            && string.Equals(value, "true", StringComparison.Ordinal);
        return !(line.Product.Backorder && wholesale);
    }
}

// In your application's service registrations, after registering Ekom:
services.Replace(ServiceDescriptor.Singleton<ICheckoutStockPolicy,
    WholesaleCheckoutStockPolicy>());
```

At the start of your existing `ProcessOrderLinesAsync` override, persist the eligibility
snapshot from your authenticated, server-resolved customer record:

```csharp
order.CustomerInformation.Customer.Properties["isDistributor"] =
    customer.IsDistributor ? "true" : "false";
var wholesale = customer.IsDistributor;

// In the existing line loop:
if (line.Product.Backorder && wholesale) continue;
// Keep your product/selected-variant lookup and Stock >= line.Quantity validation.
var id = await Stock.Instance.ReserveStockAsync(stockKey, -line.Quantity, ct: ct);
await Order.Instance.AddHangfireJobsToOrderAsync([id], order, ct: ct);
```

The flag must come from trusted customer data, not a posted checkout field. The existing
order-save path persists these properties so completion uses the same snapshot on another
node, without a logged-in customer. The controller constructor and protected hook signatures
are unchanged. This override does not need to call `base.ProcessOrderLinesAsync` or add IDs
to the passed `ICollection<string> hangfireJobs`. For new integrations using the base line
hook, populate the same durable flag before invoking the base hook.

During preparation, `ReserveStockAsync` carries a private preparation capability and a
legacy-origin marker. `AddReservationsToOrderAsync` and its Hangfire aliases conditionally
associate only that scope's newly created rows in a serializable SQL transaction. Attachment
checks order ownership, stock identity/key/kind, store, coupon, aggregate quantity, active
state and expiry. A failed batch rolls back the whole association. Retries reuse matching
active holds without extending expiry; a later line failure compensates new holds even
when the override immediately saved them. Competing preparations cannot adopt those holds
while their owner is still working or compensating.

Outside checkout preparation, explicitly create an order-owned hold using
`IStockReservationService` with `OrderId` and the order's `StoreAlias`, then attach its ID.
Generic unowned holds (including wrappers called outside the preparation scope) cannot be
claimed by passing their IDs to an order API. These APIs remain server-side integration
APIs: callers must authorize access to the order. They are not authorization endpoints for
untrusted reservation IDs. Changing the eligibility snapshot after reservation causes
requirements verification to fail and requires reconciliation.

## Compatibility

- All existing public `Stock` method signatures remain. `ReserveStockAsync` and
  `ReserveDiscountStockAsync` still accept **negative** quantities, returning a native
  reservation ID instead of a job ID. Reservation quantities with more than two decimal
  places are now rejected, rather than implicitly rounded to the stock table's precision.
- `CancelRollback(id)` retains its old meaning: consume the reservation, keeping the
  deduction. It throws `StockException` for an outcome other than consumed/already
  consumed. `RollbackJobAsync(id)` and `CompleteRollback(id)` release it. The synchronous
  methods are compatibility bridges; new code should await the injected service and
  inspect its explicit results. Consuming an expired hold is rejected even if the worker
  has not swept it yet: `ConsumeAsync` returns `LateConsumption`, and `CancelRollback`
  throws `StockException`. An active overdue hold is restored atomically as it expires.
- The static Hangfire-named stock-update methods remain direct increment wrappers.
  They are not scheduled jobs and should not be used to release reservation IDs.
- Equal `SetStockAsync` requests and zero `IncrementStockAsync` requests use the balance
  read inside the SQL transaction to detect no-ops. For an existing row, they leave the
  balance and update timestamp untouched and refresh the local cache without emitting
  `StockChangedAsync`.
- Stock event errors are logged after commit rather than propagated as stock-operation
  failures. A subscriber failure does not roll back or retry the committed mutation.
- `OrderInfo.ReservationIds` and `HangfireJobs` expose the same read-only collection.
  Persisted JSON with only `HangfireJobs` still reads correctly. Both names are written
  for compatibility; when both are present, a `ReservationIds` array takes precedence.
  Missing/null collections read as empty. `IOrderInfo.ReservationIds` has a default
  interface implementation delegating to `HangfireJobs` for existing implementers.
- `AddReservationsToOrderAsync` (both overloads) and `RemoveReservationsFromOrderAsync`
  have the old Hangfire-named aliases without obsolete warnings. Removing IDs only
  clears order metadata; explicitly release/consume first.

Existing Hangfire jobs are **not migrated or executed** by this worker. Ekom does not
read or delete Hangfire tables. Plan the deployment around any old outstanding jobs;
old job IDs are unknown to the native service. Applications using Hangfire for unrelated
work can retain their own package and registrations.

## Reliability, caches and operations

The worker uses bounded sweeps and cancellable delays. SQL deadlock, contention and
unique-key races have bounded retries; sweep-level failure gets exponential backoff
with jitter (up to roughly five minutes). Per-record expiry failures persist backoff
up to five minutes so a broken record does not permanently occupy the first batch.
Cleanup deletes only terminal records whose completion time predates retention.

Product stock increments now use conditional SQL arithmetic instead of cached absolute
writes. Explicit `SetStockAsync` remains a deliberate absolute replacement (including
editor/import callers); integrations must coordinate absolute inventory snapshots with
outstanding reservations. Direct database writes outside these APIs are outside this
transaction policy.

Local stock cache refresh and `StockChangedAsync` notifications happen after commit.
Notification failures are logged and cannot replay the stock mutation. They are
best-effort, with no durable event delivery or distributed cache synchronization added
here. Cached reads on other nodes may lag; stock mutations and reservation availability
are decided in SQL. A process dying after commit but before publication can leave cache
or external subscribers stale until their next refresh. Event handlers should not treat
notification order as a transaction log.

Logs include reservation IDs and outcomes (not coupon or idempotency-key values).
The `Ekom.Reservations` meter exposes:

- `ekom.reservations.outcomes` with a `status` tag;
- `ekom.reservations.expiry.failures`;
- `ekom.reservations.worker.failures`;
- `ekom.reservations.cleaned`.

Monitor expiry failures and backlog/old active records using your database and logging
tools. This feature does not add a reservation admin UI.
