# Failed gift-card preparation recovery

An external hold can succeed before an integration finishes parsing or validating the provider response. Ekom must retain that financial outcome even when the hold cannot be accepted as a payment contribution. Fixing a provider's numeric/date formatting is separate from this recovery lifecycle.

## Integration contract

`ICheckoutGiftcardReservations` remains source-compatible. Successful calls still return accepted `Giftcard` contributions. The registered adapter may additionally implement `ICheckoutGiftcardReservationRecovery` for Manager release and provider reconciliation.

If reserve fails after any external effects may have occurred, throw `GiftcardReservationFailureException` with a `GiftcardReservationFailure` receipt. Do **not** catch it in an outer adapter and translate it to a generic checkout conflict before Ekom can persist the receipt. Ekom does not reference application-specific exception classes or parse activity-log text.

| Outcome | Required evidence |
| --- | --- |
| `NoEffects` | The entire invocation created or changed no holds. No acquired claims. |
| `Compensated` | All effects are known and terminal; every newly acquired hold has a confirmed release. |
| `Unresolved` | One or more known effects remain active, unconfirmed, settled, or otherwise unsafe. |
| `Unknown` | The invocation may have effects that cannot yet be identified. |

`AllEffectsAccountedFor` is an invocation-level guarantee, not a count of returned claims. Leave it false if any card request or late response could still create an unknown effect. Releasing all known claims does not resolve an additional unknown request.

Each `GiftcardRecoveryClaim` retains the provider, non-secret merchant/site account identity, card and claim references, original request correlation, requested amount/currency, raw returned amount/type and expiry, and cleanup status. `NewlyAcquired` must be positively established by the integration for this order and attempt. Never infer ownership from card number, amount, or an unscoped claim ID.

Raw scalar fields can remain text when typed parsing fails. They are evidence, not accepted monetary values. Do not put entire HTTP bodies, API keys, credentials, or exception messages in recovery fields. Treat references as sensitive data and apply appropriate database access, backup, and retention controls.

`GiftcardReservationRejectedException` retains its stricter existing guarantee: the entire call created no holds and changed no existing holds. Do not use it for acquired-then-released operations, partial requests, timeouts, or uncertain responses.

### Mapping application-specific failures

For an integration with a local hold-failure exception, translate its structured receipt into the Ekom contract inside `ReserveAsync`. Preserve the original order and attempt IDs. Map a confirmed newly acquired hold release to claim status `Released`; an unsuccessful or uncertain release becomes `ReleaseUnconfirmed` or `Unknown`. Map the entire invocation to `Compensated` only when **every** external effect is accounted for and released. A single `ReleaseConfirmed` boolean is insufficient for a multi-card invocation.

Release and reconciliation methods must use the original provider identity and order/attempt correlation. They must not remove customer selections from Ekom, refund settled balances, or call normal payment completion. `ReleaseRecoveryClaimsAsync` may receive only one claim and must not release other claims from the same order. Repeated confirmed release must be safe; a timeout or unverified not-found result is not confirmation.

`ReconcileReservationFailureAsync` is read-only provider verification. Return `AllEffectsAccountedFor = true` only when the original request is terminal and every effect is known. Never report absence of effects merely because a local expiry passed or a lookup returned no current match. If the provider cannot prove ownership/outcome, retain unknown evidence and escalate.

## Durable lifecycle

Ekom records reservation intent, original selections, and attempt identity before the external reserve call in `EkomGiftcardReservationRecovery`. Failure receipts and cleanup outcomes are stored separately from `OrderInfo.Giftcards` and submitted purchase snapshots. They must not change the amount payable or become valid payment contributions.

Confirmed compensated failures may close only the same unsubmitted preparation attempt after other owned reservation state is safely resolved. Codes and intended amounts remain selected for basket review. Unconfirmed outcomes retain the financial fence and block retry; ordinary cancellation or generic Release Lock cannot make them safe.

The new table is created idempotently by Ekom's reservation schema startup, including existing installations. Back up and deploy the matching Ekom packages together across application instances before enabling the new adapter behavior. Mixed-version writers do not share the new recovery policy.

Once external effects may exist, evidence persistence and cleanup are independent of HTTP cancellation. Ambiguous database acknowledgement retains the fence. There is no automatic takeover based on age.

### Process crashes and provider correlation

An exception receipt cannot recover data that never reached Ekom before a process crash. Use stable order/attempt request identities for provider idempotency and lookup, or an integration-owned durable journal. The pre-call intent ensures an unknown operation remains discoverable and blocked; it does not invent missing provider claim IDs.

If the database is unavailable after an external effect, Ekom cannot promise to persist an unseen reference. Keep provider-side correlation and any independent evidence, and reconcile the durable unknown intent before another payment. Never equate missing local metadata with no external hold.

## Manager recovery

The Umbraco 17/18 order detail includes a separate failed-preparation recovery section, independent of current gift-card selections. It shows the original attempt, masked card and claim references, requested versus raw returned amount, expiry, cleanup status, and release/reconciliation availability. Resolved records remain available as history.

Actions revalidate store access, the current evidence snapshot, both checkout fences, the active attempt, and payment state. Stale confirmations cannot release replacement claims or reset newer attempts. Provider actions are audited without writing card codes or raw provider payloads into routine logs.

- **Release** targets only the selected eligible newly acquired claim. It is not a refund.
- **Reconcile** obtains authoritative provider evidence for the original request. It does not itself release, redeem, or refund claims.
- Unknown requests without a claim reference remain reconciliation-only.
- Locks clear only when confirmed recovery and all other local safeguards allow closure. Partial cleanup does not reopen checkout.

If a Manager recovery process stops before its result is durably recorded, its in-progress fence is intentionally not timed out or automatically stolen. Contact support for provider verification and controlled reconciliation. Likewise, unrelated/protected stock or preparation ownership is not speculatively erased merely because gift-card cleanup succeeded.

## Historical incidents without structured evidence

Do not import a card number and amount as proof of ownership, parse activity-log prose, or edit checkout tables to manufacture a release capability.

1. Have an authorized operator identify the exact provider claim and merchant/site account from provider records and retained evidence.
2. Verify its original Ekom order/attempt or other authoritative provider request correlation. If this cannot be proven, keep checkout blocked and escalate to provider support.
3. Verify no request/payment is still in flight and no redemption, settlement, or completion occurred. A timeout or old lock is not proof.
4. Obtain provider-confirmed release or reconciliation for those exact effects, retaining reference, operator, time, and outcome in the audit trail. Do not refund or release unrelated claims.
5. Only after external effects are verified resolved, use the authorized recovery path appropriate to that historical order. No automatic historical claim-association/import endpoint is provided; legacy Release Lock remains an explicit administrative override and must not substitute for the external checks above.

Automated tests use mocked providers and local databases. Never run recovery tests against live orders or real holds.
