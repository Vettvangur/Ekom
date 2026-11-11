# Order, payment, and fulfillment status plan

Status: Implementation started. See the progress section for completed and pending work.
Date: 2026-11-07

## 1. Purpose and scope

Replace the single mixed order status with three independent statuses while preserving existing integrations, stored orders, and checkout behavior. Add accurate partial fulfillment, partial payment, and partial/full refund support.

Sections marked **Agreed** record decisions made during planning. Sections marked **Proposed** describe implementation choices to verify before development.

Support Umbraco 13 (retained Ekom.U10 package name), 17, and 18, including their manager interfaces and the Algolia, Klaviyo, and Mailchimp integration paths affected by order changes.

## 2. Status definitions (Agreed)

### OrderStatus

| Value | Meaning |
| --- | --- |
| Draft | Basket or checkout not yet successfully completed. |
| Open | Checkout completed; the order is active for payment collection or operational work. |
| Closed | The order is closed to the active workflow. Financial and fulfillment history remain available. |
| Cancelled | The order was stopped rather than completed. Cancellation does not itself refund payment or undo fulfillment. |

Draft becomes Open at the same successful checkout boundary that currently sets ReadyForDispatch. Offline checkout also opens the order, with payment Pending. Failed or in-progress checkout stays Draft.

### PaymentStatus

| Code value | Display label | Meaning |
| --- | --- | --- |
| Pending | Pending | Payment has not been confirmed. |
| Authorized | Authorized | Funds have been authorized but not captured. |
| Paid | Paid | The amount due has been fully paid, with no successful refund. |
| PartiallyPaid | Partially Paid | A positive amount less than the amount due has been paid, with no successful refund. |
| Failed | Failed | Payment failed without an overriding successful payment state. |
| PartiallyRefunded | Partially Refunded | Some, but not all, successfully collected funds have been refunded. |
| Refunded | Refunded | All successfully collected funds have been refunded. |
| Voided | Voided | An uncaptured authorization was successfully voided. |

Exact aggregation and mixed-payment rules are proposed in section 7. Payment attempt failures must not erase earlier successful payments.

### FulfillmentStatus

| Code value | Display label | Meaning |
| --- | --- | --- |
| Unfulfilled | Unfulfilled | No items have been fulfilled and preparation has not started. |
| Picking | Picking | Warehouse picking has started. |
| Picked | Picked | Required items have been picked. |
| Packed | Packed | Items are packed and ready for dispatch or customer collection. |
| PartiallyFulfilled | Partially Fulfilled | Some, but not all, required quantities have been dispatched or collected. |
| Fulfilled | Fulfilled | All required quantities have been dispatched or handed to the customer. |

ReadyForDispatch and ReadyForPickup are not new fulfillment values. Packed covers both dispatch-ready and pickup-ready items; the delivery method distinguishes them.

Waiting for stock and Wishlist are excluded from the new model and interface. Do not add stock-waiting quantities, stock-waiting flags, or wishlist markers in this work.

## 3. Closing and reopening (Agreed unless marked)

Store setting: `AutoClosePaidAndFulfilledOrders`, enabled by default. The name is proposed; the behavior is agreed.

```text
OrderStatus == Open
AND PaymentStatus == Paid
AND FulfillmentStatus == Fulfilled
AND AutoClosePaidAndFulfilledOrders == true
=> OrderStatus = Closed
```

- Evaluate after successful payment or fulfillment changes, whichever finishes last.
- Partial payment or partial fulfillment does not qualify.
- Draft and Cancelled orders never auto-close.
- Disabling automatic closure leaves qualifying orders Open for manual closure.
- Record automatic closure, manual closure, and explicit reopening in the activity log.
- A refund does not reopen a Closed order. Reopen explicitly when further operational work is required.
- Closed does not mean immutable financial history: subsequent refunds are supported.
- Basket locking and checkout ownership remain separate from operational closure.

**Proposed:** Reopening must leave the order Open even if it still meets the auto-close condition. Persist an auto-close suppression for that reopening, cleared by an explicit resume-auto-close action or manual closure. Otherwise the next callback could immediately close it again.

**Proposed:** Manual closure requires Paid and Fulfilled by default. An exceptional closure needs an explicit privileged override and reason. Apply configuration changes prospectively; closing an existing backlog is an explicit batch action.

## 4. Legacy migration mapping

The existing enum remains available for compatibility. The table defines migration of old data, not unconditional replacement of all three fields on every future legacy write.

| Legacy value | New OrderStatus | New PaymentStatus | New FulfillmentStatus | Notes |
| --- | --- | --- | --- | --- |
| Incomplete | Draft | Pending | Unfulfilled | Existing basket. |
| WaitingForPayment | Draft | Pending | Unfulfilled | Migrate inactive checkouts; skip orders with active checkout ownership or payment attempts. |
| PaymentFailed | Draft | Failed | Unfulfilled | Preserve retry behavior. |
| Pending | Open | Pending | Unfulfilled | Explicitly agreed. |
| OfflinePayment | Open | Paid | Unfulfilled | Legacy invoice/offline orders are treated as Paid by the agreed compatibility mapping; no provider transaction or PaidDate is created. |
| ReadyForDispatch | Open | Paid | Unfulfilled | Explicitly agreed; today this means checkout completion, not warehouse preparation. |
| ReadyForDispatchWhenStockArrives | Open | Paid | Unfulfilled | Reported unused. Compatibility fallback only; no stock-waiting feature. |
| ReadyForPickup | Open | Paid | Packed | Explicitly agreed fulfillment mapping. |
| Dispatched | Open, then apply auto-close rule | Paid, subject to payment evidence | Fulfilled | Proposed legacy payment default; review contradictions. |
| Closed | Closed | Paid | Fulfilled | Explicitly agreed legacy mapping; no payment transaction or PaidDate is created. |
| Cancelled | Cancelled | Pending by default; preserve recorded payment status or PaidDate | Unfulfilled by default; preserve recorded fulfillment status and quantities | Skip active payments. Invalid statuses, contradictory paid-date evidence, or quantities without a compatible fulfillment summary require review. No payment, stock or notification side effects. |
| Returned | Closed (proposed fallback) | Resolve from evidence | Fulfilled (proposed fallback) | Reported unused; retain legacy return information if encountered. A return does not prove a refund. |
| Wishlist | Draft | Pending | Unfulfilled | Reported unused. Compatibility fallback only; no wishlist feature. |

Apply the auto-close rule after mapping eligible Open orders, respecting the store configuration. Existing Closed orders stay Closed. Migration closes records without replaying checkout side effects or notifying external integrations.

### Ambiguous or contradictory historical records (Proposed)

- Generate a preflight report grouped by legacy status, missing evidence, and contradictory evidence.
- Preserve the original status and migration version/provenance separately from the mutable legacy compatibility column.
- Use reliable provider/payment records first. PaidDate is supporting legacy evidence, not a refund ledger.
- Do not manufacture transaction IDs, refund amounts, authorization records, or shipment timestamps.
- Known legacy conventions may create an explicitly labeled opening balance; they must not look like provider-confirmed transactions.
- Require a reviewed mapping override for unresolved Cancelled, Returned, or contradictory records before completing migration for those records.
- Allow nullable new fields only during staged migration. Reads of unmigrated records use the legacy adapter; new actions requiring unknown financial balances remain unavailable until resolved.
- Do not add an Unknown enum value silently or treat missing evidence as confirmed non-payment.
- Report unexpected strings and old numeric enum representations. Preserve original values for diagnosis.

## 5. Backward-compatible API and storage (Proposed)

### Preserve existing public contracts

The existing type `Ekom.Utilities.OrderStatus` and `OrderInfo.OrderStatus` cannot be replaced in-place without breaking consumers.

- Keep legacy enum names and numeric values unchanged, including unused members.
- Keep existing public property types, method signatures, legacy JSON fields, and event argument types during the compatibility period.
- Avoid adding required members to externally implemented interfaces or abstract provider classes.
- Introduce the new enums in a dedicated namespace, for example `Ekom.Models.Orders`.
- Expose a new status object, for example `order.Status.OrderStatus`, `order.Status.PaymentStatus`, and `order.Status.FulfillmentStatus`.
- Use additive DTOs or a versioned HTTP contract for the new statuses. Preserve old serialization behavior on old endpoints.
- Retain old enum values for parsing and integrations, but omit them from the new manager selectors.
- Document eventual deprecation separately; do not remove legacy contracts in this release.

### Storage and consistency

- Add independent order, payment, and fulfillment columns; do not repurpose OrderStatusCol.
- Add persistence for payment/refund operations and fulfillment quantities.
- Persist canonical statuses and the compatibility projection atomically with concurrency checks.
- Keep a mapping version and immutable original legacy status for migration audit.
- Update explicit SQL projections, filters, indexes, exports, cache hydration, and serialized order snapshots.
- Preserve existing checkout payment-attempt snapshots and reservation recovery behavior.
- New values become authoritative once a record is migrated. Legacy updates flow through one adapter.

### Translate writes by intent

A legacy status update changes only the dimensions it represents. It must not repeatedly reapply migration defaults.

Examples:

- Legacy Dispatched marks remaining quantities fulfilled but preserves an existing refund or partial-payment state.
- Legacy Cancelled cancels the order without refunding or restocking it.
- Legacy ReadyForDispatch remains a legacy checkout-completion signal; it must not regress an already fulfilled order or erase confirmed refunds on a duplicate callback.
- Legacy Closed requests closure; keep existing legacy behavior available while validating new manager actions under the new policy.
- Process the new command and legacy event path once, with duplicate detection and consistent before/after state.

### Project new states back to legacy consumers

The projection is necessarily lossy. Proposed baseline:

| Canonical situation | Legacy projection |
| --- | --- |
| Cancelled | Cancelled |
| Closed | Closed |
| Draft with active submitted payment attempt | WaitingForPayment |
| Draft with failed payment | PaymentFailed |
| Other Draft | Incomplete |
| Open, fully fulfilled | Dispatched |
| Open, Packed, customer pickup | ReadyForPickup |
| Open, fully paid, other fulfillment stages | ReadyForDispatch |
| Open, offline payment still pending | OfflinePayment |
| Other Open combinations | Pending |

Explicit legacy values remain preserved until a canonical transition requires a new projection. Partial payment/refund and partial fulfillment details are available through the new contract; legacy consumers cannot distinguish all new combinations.

Compatibility means existing extensions run against the upgraded Ekom application. Concurrent old and new Ekom binaries writing the same upgraded database, direct third-party SQL writes, and automatic binary downgrade are outside the proposed guarantee. Document backup/rollback requirements before deployment.

## 6. Fulfillment records and aggregation (Proposed)

**Agreed scope boundary (2026-11-08):** Defer split fulfillment across warehouses, independent warehouse pick lists, shipment/parcel records, and separate shipping bookings, labels, or tracking per shipment (including Dropp integration for this workflow). These are not part of this delivery. Keep the planned order-level preparation statuses and quantity-based partial/full fulfillment; these do not introduce a multi-shipment workflow. Existing shipping and warehouse behavior remains in place.

- Track decimal quantities per order line for picking, picked, packed, and fulfilled operations; preserve Ekom quantity precision and linked-line rules.
- Record actor, timestamp, operation identity, and dispatch/pickup reference where available.
- Validate quantities against fulfillable order quantities and reject duplicate or excessive fulfillment.
- Fulfilled requires every fulfillable line quantity to be fulfilled; PartiallyFulfilled takes precedence whenever some but not all quantities are fulfilled.
- With no fulfilled quantities, aggregate preparation conservatively: Packed only when all required quantities are packed; Picked only when all are picked; Picking when preparation has begun; otherwise Unfulfilled.
- Returns do not erase historical fulfilled quantities. Keep return information separate, and restock only through an explicit stock operation.
- Never run checkout stock deduction again when an item is dispatched or collected; audit existing reservation completion first.
- Define fulfillment eligibility for non-shippable products and linked lines before implementation. Do not auto-close an empty order through a vacuous all-items check.

## 7. Payment and refund records (Agreed capability; proposed mechanics)

Support partial and full refunds through capable payment providers and recording refunds already completed externally, including offline payments.

### Payment accounting

Persist amount, currency, operation type, outcome, payment/provider reference, timestamps, actor, and idempotency key. Support authorizations, captures/receipts, voids, refunds, and explicit historical opening balances.

Keep successful captured/received amount, successful refunded amount, and outstanding amount available independently of the display status.

Proposed aggregation priority:

1. If positive collected funds are fully refunded, Refunded.
2. If any smaller successful refund exists, PartiallyRefunded.
3. Otherwise, fully collected amount means Paid; positive insufficient collection means PartiallyPaid.
4. Otherwise use outstanding authorization, confirmed void, failure, or Pending as appropriate.

A refund must not become a new outstanding debt by simply subtracting it from collected payments. A later failed attempt must not replace Paid or PartiallyPaid. Define mixed tenders, credits, zero-value checkout, and order-total adjustments in the implementation design and tests.

### Refund operations

- Select an eligible original payment and a positive refund amount in its currency.
- Support repeated partial refunds up to the total refundable balance and full refund of the remaining balance.
- Prevent concurrent requests from exceeding that balance; reserve pending refund amounts when validating availability.
- Store Requested/Pending, Succeeded, Failed, and reconciliation-needed outcomes separately from PaymentStatus.
- Only confirmed successful refunds affect PaymentStatus.
- A timeout is not proof of failure. Reconcile provider state before retrying with the same idempotency key.
- Recording an external refund confirms an already completed action; it must not also call a provider.
- Require an actor, amount, date, method, and external reference or explanation for externally recorded refunds.
- Expose provider refund capabilities through an optional extension interface so existing providers continue to load.
- Disable unsupported provider refund actions and offer external-refund recording where authorized.
- Enforce existing store access and explicit refund permissions on server endpoints.
- Record refund history and activities without exposing credentials or sensitive payment data.

Refunds alone never change fulfillment, restock items, cancel orders, or reopen Closed orders. A full return-management workflow is separate scope; preserve a clean extension point and legacy return evidence.

## 8. Current implementation touchpoints

Verified during planning:

- `Ekom/Ekom.Core/Ekom/Utilities/Enums.cs`: existing mixed OrderStatus enum.
- `Ekom/Ekom.Core/Ekom/Models/Data/OrderData.cs`: OrderStatusCol and enum conversion, PaidDate, stored OrderInfo JSON.
- `Ekom/Ekom.Core/Ekom/Models/OrderInfo.cs` and `IOrderInfo.cs`: public status exposure.
- `Ekom/Ekom.Core/Ekom/Services/CheckoutService.cs`: successful checkout currently sets ReadyForDispatch; offline checkout retains OfflinePayment.
- `Ekom/Ekom.Core/Ekom/Services/OrderService.cs`: status writes, events, activity logging, cache updates, and ReadyForDispatch-driven PaidDate assignment.
- `Ekom/Ekom.Core/Ekom/API/Order.cs`: IsOrderFinal currently governs basket behavior and includes many operationally open statuses.
- `Ekom/Ekom.Core/Ekom/Repositories/OrderRepository.cs`: persistence, concurrency conditions, legacy numeric conversion, and status queries.
- `Ekom/Ekom.Core/Ekom/Repositories/ManagerRepository.cs`: explicit column selection and hard-coded legacy filters.
- `Ekom/Ekom.Core/Ekom/Services/CheckoutPaymentAttemptService*.cs`: attempt ownership, completion, and recovery.
- Manager APIs and all supported manager frontends: replace the mixed selector with separate status displays and appropriate actions.
- Plugin event subscriptions and mappers: audit checkout, fulfillment, cancellation, and refund semantics; prevent duplicate external events.

## 9. Implementation phases (Proposed)

1. **Finalize contracts and migration preview:** freeze enum values, transition rules, projection rules, provider capability interfaces, and preflight output. Verify actual historical data before choosing ambiguous defaults.
2. **Add schema and compatibility adapter:** additive columns/tables, mapping provenance, legacy reads/writes, idempotent batched backfill, cache/snapshot updates, and concurrency controls.
3. **Move checkout to the new model:** preserve successful checkout timing, offline behavior, payment retries, basket locking, stock/giftcard reservations, coupons, and one-time notifications.
4. **Add payment/refund services:** accounting, partial receipts, provider and external refunds, idempotency, reconciliation, and audit history.
5. **Add fulfillment and closure services:** per-line quantities, preparation actions, partial/full fulfillment, store auto-close configuration, manual close, and explicit reopen.
6. **Update interfaces and integrations:** manager workflows, new HTTP/.NET contracts, filters, reporting, exports, and affected plugins across supported versions.
7. **Validate and document release:** execute migration and compatibility tests, update user/developer documentation and changelogs, and publish upgrade/rollback instructions.

## 10. Acceptance criteria

- [ ] Existing plugins and consumers can still compile/use old enum values, properties, methods, payloads, and events.
- [ ] Every legacy enum value has migration coverage, including numeric representations and unused fallback values.
- [ ] ReadyForDispatch migrates to Open / Paid / Unfulfilled and does not immediately auto-close.
- [ ] Pending migrates to Open / Pending / Unfulfilled.
- [ ] ReadyForPickup migrates to Packed, preserving payment evidence.
- [ ] Offline checkout opens an unpaid order; incomplete/failed/active online checkout stays Draft.
- [ ] Auto-close works in both payment-first and fulfillment-first order, is enabled by default, and respects per-store disabling.
- [ ] Explicit reopening is not immediately undone by automatic closure.
- [ ] Partial fulfillment is quantity-based, supports multiple operations, and cannot over-fulfill.
- [ ] Partial/full refunds work through supported providers and external recording; unsupported providers remain compatible.
- [ ] Concurrent/duplicate refunds cannot exceed refundable funds or produce duplicate financial effects.
- [ ] Failed or uncertain refunds do not become successful payment-status changes.
- [ ] Refunding a Closed order leaves it Closed and does not alter stock or fulfillment.
- [ ] Migration is resumable and idempotent, reports ambiguity, and never replays payment, stock, email, or plugin actions.
- [ ] Old and new manager filters, customer history, exports, cache hydration, and snapshot recovery remain correct.
- [ ] Appropriate focused and full test suites pass; affected Core, U13/U17/U18, and plugin builds pass.

## 11. Review checklist

Business decisions already agreed are captured above. The following technical policies are proposals to verify with this plan:

- [ ] Additive status object and preserved legacy API, rather than changing the existing property type.
- [ ] Evidence-based resolution and reviewed overrides for ambiguous historical data.
- [ ] Legacy projection precedence and context-aware legacy writes.
- [ ] Reopen auto-close suppression and exceptional manual-close policy.
- [ ] Payment aggregation precedence, historical opening balances, and refund reconciliation.
- [ ] Per-line fulfillment quantities and exclusion of a full return-management feature from this delivery.
- [ ] Deployment compatibility boundary and staged rollout.


## Implementation progress - 2026-11-09

Confirmed scope: scoped CSS; the full new layout targets Umbraco 17 and 18. Keep Umbraco 13 behavior compatible without a full visual port.

Completed first increment:

- Independent string-serialized status enums and an additive read-only Status snapshot on OrderData and concrete OrderInfo.
- Central legacy projection, nullable historical evidence gaps, and mapping/serialization tests. No schema migration or stored status changes occur on reads.
- Shared U17/U18 order list with three badges and a tabbed detail drawer; existing legacy status actions, editing, export, print, plugin actions, locks and giftcard recovery retained.
- Scoped inherited UUI theme colors, responsive layout, keyboard tab navigation, and dialog focus handling. Classic view styling is excluded from the modern visual overrides.

Still pending: schema/backfill and migration preview, canonical status writes and filters, per-line fulfillment operations, auto-close/reopen policy, payment/refund accounting and provider capabilities, full Analytics/Customers/Printing layout port, and live backoffice verification. The current UI keeps legacy status controls until replacement actions are ready. This increment is not the completed status migration or final layout implementation.

Validation: focused status/JSON compatibility tests pass on net8.0 and net10.0; 97 focused manager tests pass; U17/U18 TypeScript checks and Vite builds pass using Node 24. Broad suites retain failures in unchanged database-price tests, migration mocks expecting no EnsurePriceTable call, and a net10 checkout error-redirect assertion. Light/dark fixture views were inspected in the app browser; an authenticated Umbraco end-to-end run is still required.


## Implementation update: HTML-based manager (2026-11-09)

The Orders screen now uses the exported design structure: segmented navigation, shared date controls, five order views, inline totals, compact rows and a wide detail drawer. The drawer has Order, Activity log, and Tracking & consent tabs, with order lines/payment/shipping on the left and customer information on the right. Existing editing, export, print, plugin actions and checkout recovery remain connected. Scoped CSS inherits Umbraco theme variables; the logo is a separate SVG asset shared by U17 and U18 builds.

Independent status persistence, guarded status commands and an explicit paginated migration API have been added. Existing records retain their original legacy value; ambiguous history remains flagged for review. Migration does not replay payment, inventory or notification operations. Automatic closing defaults to Paid + Fulfilled; explicit reopening suppresses automatic closing. Refund execution and refund UI remain deferred, as do multiple warehouse shipments.

Validation: focused status rules and repository tests pass on .NET 8 and .NET 10. Browser checks use representative fixture data; authenticated end-to-end validation in Umbraco and a real database migration rehearsal are still required before release. The Orders layout is the current design port; other manager sections retain their existing screens.
