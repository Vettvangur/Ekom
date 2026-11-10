# Status migration rehearsal — 2026-11-09

Rehearsed the actual `OrderStateService` preview and migration against SQLite backup copies of the isolated U17 and U18 sample databases. The running sample databases and original sample database files were not migrated. Refund execution remains excluded.

> Mapping update (2026-11-10): OfflinePayment now maps to Open / Paid / Unfulfilled for legacy invoice/offline orders. The results below record the earlier rehearsal and its previous Pending mapping. Updated mapper and migration tests cover the new rule; explicit statuses already saved by users are preserved.

> Further decision (2026-11-10): Closed now maps to Closed / Paid / Fulfilled and is eligible for migration. ReadyForPickup remains Packed; WaitingForPayment maps to Draft / Pending / Unfulfilled and is eligible unless checkout ownership or an active payment attempt prevents migration. Historical results below predate these decisions.

> Cancelled decision (2026-11-10): default to Cancelled / Pending / Unfulfilled, preserve recorded payment and fulfillment evidence, and exclude conflicts and active payments. The historical results below predate this decision; focused migration tests cover the updated behavior.

## Sample results

| Sample | Legacy status | Count | Effective order / payment / fulfillment | Result |
| --- | --- | ---: | --- | --- |
| U17 | Incomplete | 10 | Draft / Pending / Unfulfilled | Migrated |
| U17 | OfflinePayment | 5 | Open / Pending / Unfulfilled | Migrated |
| U17 | WaitingForPayment | 2 | Draft / Pending / Unfulfilled projection only | Left unmigrated |
| U18 | Incomplete | 3 | Draft / Pending / Unfulfilled | Migrated |
| U18 | OfflinePayment | 1 | Open / Pending / Unfulfilled | Migrated |
| U18 | WaitingForPayment | 1 | Draft / Pending / Unfulfilled projection only | Left unmigrated |

Preview matched the resulting statuses. A second migration changed zero rows. Assertions checked that order IDs, original legacy statuses, payloads, totals, currencies, paid/created/updated dates remained unchanged. Independent hashes of every other database table were identical before and after, including printing and checkout tables.

Local evidence: `/tmp/ekom-status-rehearsal-u17.sqlite.db.report.json` and `/tmp/ekom-status-rehearsal-u18.sqlite.db.report.json`. These contain reference IDs and status results, without customer details.

## Historical cases covered by isolated integration fixtures

The sample history does not contain every legacy status. Separate SQLite fixtures verified:

| Legacy status | Default migrated result |
| --- | --- |
| Incomplete / Wishlist | Draft / Pending / Unfulfilled |
| Pending / OfflinePayment | Open / Pending / Unfulfilled |
| PaymentFailed | Draft / Failed / Unfulfilled |
| ReadyForDispatch / ReadyForDispatchWhenStockArrives | Open / Paid / Unfulfilled |
| ReadyForPickup | Open / Paid / Packed |
| Dispatched | Closed / Paid / Fulfilled |
| Closed / Cancelled / Returned / unknown | Remains unmigrated where historical evidence is incomplete |

Paid-date evidence and conflicting historical evidence are additionally covered by mapper tests. Dispatched stays Open when automatic closing is disabled. Migration preserves the stored legacy value for compatibility and records it in OriginalOrderStatusCol; it does not replay business transitions.

Tests also cover 103 eligible orders over two pages, store access restrictions, active checkout ownership, submitted payment attempts without an owner, unknown statuses, and idempotence. Migration does not invoke status persistence handlers or stock services.

## Closing and reopening

The real status-change service was exercised against a saved order payload:

- Zero fulfilled quantity remains Open.
- Fulfilling all quantities closes a Paid order when configured, and stays Open when disabled.
- Payload and total remain unchanged by fulfillment.
- Explicit Reopen persists Open and AutoCloseSuppressed, so automatic closing does not immediately undo it.
- Explicit Close succeeds after reopening; a stale revision is rejected.
- Rule tests cover partial quantities, pending payment, excess/decreasing quantities and invalid preparation transitions.

## Issues fixed during rehearsal

1. Preview now includes the same configured auto-close result and next revision as migration.
2. Store filtering uses a collection translated correctly by LinqToDB on .NET 10.
3. Unknown or ambiguous legacy statuses are skipped before checkout diagnostics attempt to parse their legacy enum.
4. Submitted or otherwise unsettled payment attempts are skipped even without an active operation owner.

## Graph design and verification

The U17/U18 analytics graphs now follow the supplied HTML's area fill, comparison dashes, compact chart frame, segmented metric selector, monospaced axes, horizontal weekday bars and 24 vertical hourly columns. Real values, zero buckets, missing-value gaps, table access and bucket navigation are retained. Classic manager hourly charts retain their existing layout.

Canvas charts redraw when Umbraco installs a new theme stylesheet. Axis labels use compact dates while tooltips and tables retain full date ranges. Live U17 and U18 checks used actual sample orders; sparse history remains visibly sparse rather than introducing decorative data.

## Validation and deployment limits

- Focused status suite: 35 passing on .NET 8 and 35 on .NET 10, including the optional rehearsal entry point (run separately against both copies).
- Analytics tests: 16 passing; TypeScript typecheck passed.
- Both sample sites built successfully; existing dependency/analyzer warnings remain.
- This rehearsal covers SQLite. A SQL Server deployment still needs the same backup/preview/apply/idempotence rehearsal on its actual data and provider.
- No production backfill was run. Review the preview on the intended deployment, migrate in cursor order, and retain a database backup. Ambiguous history and active checkout records require follow-up rather than invented evidence.

## Screenshot comparison follow-up

SQL Server rehearsal was cancelled at the user's request. No SQL Server was contacted or modified; the unfinished SQL-only fixture setup was removed.

Compared the analytics chart with the supplied screenshot and original HTML. Corrected the extra plot inset, restored the roughly 240px plot area, removed top/bottom gridlines, softened the remaining gridlines, matched the light-mode green, and formatted dates as day/month ranges. Axis labels now use compact values while hover and table values retain exact amounts. Muted text adapts to the Umbraco theme. The chart still plots actual data, so the sample's sparse order history differs from the populated design reference.

Validation: 17 analytics tests, TypeScript typecheck, and both sample site builds passed. Existing build warnings remain. Refund execution remains out of scope.


## Cancelled verification — 2026-11-10

Rebuilt and restarted the U17 and U18 samples. Ran the actual migration service against fresh disposable copies of each sample database with eight synthetic Cancelled cases. Existing running sample orders were not migrated or altered.

| Case | Result on both database copies |
| --- | --- |
| No evidence | Cancelled / Pending / Unfulfilled |
| Paid date | Cancelled / Paid / Unfulfilled; paid date preserved |
| Paid and Fulfilled statuses | Both preserved |
| Paid date and partial fulfillment quantities | Paid / PartiallyFulfilled; quantities preserved |
| Active checkout owner | Excluded; unchanged |
| Submitted payment without an owner | Excluded; unchanged |
| Paid date conflicting with Pending | Excluded for review; unchanged |
| Malformed fulfillment quantities | Excluded for review; unchanged |

All 18 migration tests passed for each copy, including explicit conflict-review assertions, preview/apply agreement, idempotence and no stock/status-handler side effects. Additional database assertions verified preserved paid dates, fulfillment quantities, payloads, amounts, currencies and legacy status values. Evidence: `/tmp/ekom-status-rehearsal-cancel-u17.sqlite.db.report.json` and `/tmp/ekom-status-rehearsal-cancel-u18.sqlite.db.report.json`. This was database/service verification, not a live UI check of synthetic orders. Refunds remain out of scope.
