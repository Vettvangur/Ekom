[Ekom docs](../README.md) / [Guides](README.md) / Failed gift-card preparation recovery

# Failed gift-card preparation recovery

This page explains what to do when Ekom asked an external gift-card provider to hold a balance for a payment, and the hold may have been made, but Ekom could not accept it. Ekom then keeps a record of what may have happened at the provider and blocks checkout for that order until a person has resolved it.

The first part is for staff who resolve such an order in the Manager. The second part is for developers who write the gift-card integration.

This only applies to stores that hold gift-card balances at an external provider through an `ICheckoutGiftcardReservations` integration. See [Gift cards held at an external provider](checkout.md#gift-cards-held-at-an-external-provider).

On this page:

- [What happened to the order](#what-happened-to-the-order)
- [Rules that always apply](#rules-that-always-apply)
- [Resolve an order in the Manager](#resolve-an-order-in-the-manager)
- [Handle an old incident without recorded evidence](#handle-an-old-incident-without-recorded-evidence)
- [For developers: report a failed hold](#for-developers-report-a-failed-hold)
- [For developers: support recovery from the Manager](#for-developers-support-recovery-from-the-manager)
- [What Ekom stores, and when](#what-ekom-stores-and-when)

## What happened to the order

When a customer pays and the basket has gift cards, Ekom asks the gift-card integration to hold the cards at the provider. Before it asks, Ekom writes down that a hold is being requested for this order and payment attempt. The hold can succeed at the provider, and still fail in Ekom, for example because the integration could not read the provider's answer, or the server stopped.

In that case money may be held on a gift card that the order does not use. Ekom does not treat that hold as a payment. It keeps the record, which this page calls the evidence, and blocks checkout and basket changes for the order. The customer sees that the store must check the payment.

Fixing the cause, for example a provider that formats amounts or dates in an unexpected way, is separate work. It does not resolve orders that already failed.

## Rules that always apply

- **These actions are not refunds.** They do not refund a payment, retry the payment or retry checkout.
- **Never decide by card number or amount.** A matching card number, amount or claim ID does not prove that a hold belongs to this order.
- **Missing information is not proof.** A timeout, an old lock, an expiry date that has passed, or a missing claim reference does not prove that no hold exists.
- **Do not change the database by hand.** Do not edit Ekom's checkout tables, and do not use activity log text, to make a release possible.
- **Release Lock does not help here.** While a failed gift-card preparation is pending or unresolved, the Manager's **Release Lock** is not available. Use the recovery actions below first.
- **Test only with test data.** Never run recovery tests against live orders or real holds.

## Resolve an order in the Manager

The order details in the Manager have a section called **Failed gift-card preparation**. It appears when the order has such a record, and it is separate from the gift cards the order currently uses. Resolved records stay in the section as history.

For each failed attempt the section shows the attempt ID, when it happened, the recovery state and the reason. For each card it shows:

- the masked card number and the masked claim reference,
- the provider and the masked provider account,
- the amount requested, and the amount and expiry exactly as the provider returned them,
- the cleanup status of the hold.

### Steps

1. **Open the order** in the Manager and find **Failed gift-card preparation**.
2. **Reconcile first** if the section says that unknown provider effects need reconciliation, or if a card says that its claim reference is missing. Select **Reconcile failed preparation** and confirm.
   - Reconciliation asks the provider what happened to the original request. It only reads. It does not release, redeem or refund anything.
3. **Release each hold that can be released.** Select **Release failed-preparation hold** on the card and confirm.
   - This releases only that one hold. It never touches other cards or other holds of the order.
4. **Read the message** after each action:

   | Message | What to do |
   | --- | --- |
   | Gift-card effects are resolved and checkout preparation was safely closed. | Done. The customer can pay again. |
   | Recovery evidence was recorded. Checkout remains blocked until all effects and preparation issues are resolved. | Continue with the next hold, or reconcile again. |
   | Provider recovery could not be confirmed. Checkout remains blocked; reconcile before retrying. | Reconcile, then try the release again. |
   | Checkout or reservation evidence changed. Refresh and confirm recovery again. | Reload the order and start again from step 2. |
   | The recorded recovery action is already complete. | Nothing more to do for that action. |

5. **Repeat** until the first message appears, or until no action is available.

Each action is recorded in the activity log with your user name and the outcome. Card codes and the provider's raw answers are not written to the logs.

### When an action is not available

The section shows the reason next to a button it cannot offer. An action is only available when all of these are true:

- the gift-card integration supports recovery,
- the record still needs recovery: it is pending or unresolved,
- the failed attempt is still the order's current attempt, was never sent to payment, and the order is not paid,
- no other checkout operation has taken over the order since.

A release is also only available when:

- the record is unresolved, not pending. A record stays pending when Ekom never got an answer from the integration, for example because the server stopped. Reconcile it first,
- the integration has proved that this hold was newly made for this order and attempt,
- the hold is active, or an earlier release could not be confirmed,
- the provider, the account, the claim reference and the card are all known.

If you see **Recovery is in progress. Refresh before taking another action. If it remains interrupted, contact support.** after a refresh, an earlier action stopped before its result was saved. Ekom never times this out and never takes it over by itself. Contact support. They need to check the provider and reconcile the order in a controlled way.

### When checkout opens again

Ekom opens checkout for the order only when all of these are true:

- every effect of the failed request is known: either it made no holds at all, or every hold it made is released and confirmed,
- no other failed gift-card request of the order is still unresolved,
- the order is not paid, and no gift card on it is redeemed or settled,
- the order does not hold stock, and no other checkout step still owns the order.

Cleaning up some holds is not enough. Checkout stays blocked until everything is resolved. Ekom does not remove stock holds or other checkout locks just because the gift-card part is clean.

If the order is paid, was sent to payment, or a card is settled, the Manager does not unlock it. Contact support.

## Handle an old incident without recorded evidence

Orders from before Ekom recorded this evidence have no record in the **Failed gift-card preparation** section. Ekom has no endpoint to import one. Do this instead:

1. Have an authorized person find the exact claim and the merchant or site account at the provider, from the provider's records and any evidence you kept.
2. Prove which Ekom order and attempt, or which provider request, made the claim. If you cannot prove it, keep checkout blocked and ask the provider's support.
3. Make sure no request or payment is still running, and that the claim was not redeemed, settled or completed. A timeout or an old lock is not proof.
4. Get the provider to confirm the release, or the final state, of exactly that claim. Write down the reference, who did it, when, and the result. Do not refund or release other claims.
5. Only after that, use the recovery path that fits the order. **Release Lock** is an explicit administrative override for older orders. It does not replace the checks above.

## For developers: report a failed hold

Your `ICheckoutGiftcardReservations.ReserveAsync` returns the accepted gift cards when it succeeds. When it fails, what you throw tells Ekom what may have happened at the provider.

| Situation | Throw |
| --- | --- |
| You are certain the whole call made no hold and changed no existing hold. | `GiftcardReservationRejectedException` |
| The call may have made or changed holds, and you can describe them. This includes holds you made and then released again. | `GiftcardReservationFailureException` with a `GiftcardReservationFailure` receipt |
| You do not know what happened. | Any other exception. Ekom records unknown effects. |

`GiftcardReservationRejectedException` is a strong promise. Do not use it for a call that made holds and released them again, for a call that handled only some cards, for a timeout, or for an uncertain answer.

Throw `GiftcardReservationFailureException` from inside `ReserveAsync`. Do not catch it in an outer layer and turn it into a general checkout error before Ekom has saved the receipt. Ekom does not know your own exception types and does not read activity log text.

### Fill in the receipt

`GiftcardReservationFailure`:

| Field | Set it to |
| --- | --- |
| `OrderId`, `AttemptId` | The order and attempt Ekom passed to `ReserveAsync`. A receipt for another order or attempt is not trusted. |
| `Outcome` | `NoEffects`: the call made or changed no hold, and `Claims` is empty. `Compensated`: every new hold is released and the release is confirmed. `Unresolved`: at least one known hold is still active, not confirmed, settled or otherwise unsafe. `Unknown`: the call may have effects you cannot identify yet. |
| `AllEffectsAccountedFor` | `true` only when the whole call is finished and every effect is known. It is about the call, not about the claims you list. Leave it `false` if any card request or late answer could still make a hold you do not know about. Releasing every known claim does not account for a request whose result you do not know. |
| `Claims` | One `GiftcardRecoveryClaim` per hold, at most 256. |

`GiftcardRecoveryClaim`:

| Field | Set it to |
| --- | --- |
| `Provider`, `Account` | The provider, and the merchant or site account. Not an API key or a token. |
| `CardCode`, `ClaimId` | The card and the provider's claim reference. |
| `ProviderRequestId` | The ID of the original request, when the provider has one. |
| `RequestedAmount`, `Currency` | What you asked the provider to hold. |
| `RawReturnedAmount`, `ReturnedAmountType`, `RawExpiry` | What the provider returned, as text, even when you could not read it as a number or a date. |
| `ExpiresUtc` | The expiry, when you could read it. |
| `NewlyAcquired` | `true` only when you have proved that this hold was made by this call, for this order and attempt. Never decide it from a card number, an amount, or a claim ID alone. |
| `Status` | `Active`, `Released` (release confirmed), `ReleaseUnconfirmed`, `Settled`, or `Unknown`. |

Ekom treats the raw values as evidence, never as a payment amount. Do not put whole HTTP bodies, API keys, credentials or exception messages in these fields.

Ekom closes a failed attempt by itself only when the receipt is `NoEffects` with no claims, or `Compensated` where every claim is newly acquired, `Released`, and has its provider, account, claim reference and card. Everything else waits for a person in the Manager.

### Map your own exceptions

If your integration has its own exception for a failed hold, translate it into the receipt inside `ReserveAsync`:

- Keep the original order ID and attempt ID.
- A new hold whose release you confirmed becomes `Released`. A release that failed or that you are unsure of becomes `ReleaseUnconfirmed` or `Unknown`.
- Use `Compensated` only when every external effect of the call is known and released.
- One `ReleaseConfirmed` flag is not enough for a call that held several cards. Report each card.

## For developers: support recovery from the Manager

The Manager actions only work when your registered integration also implements `ICheckoutGiftcardReservationRecovery`.

```csharp
public sealed class WalletGiftcardReservations
    : ICheckoutGiftcardReservations, ICheckoutGiftcardReservationRecovery
{
    // ReserveAsync and ReleaseAsync as before.

    public Task<IReadOnlyList<GiftcardRecoveryClaim>> ReleaseRecoveryClaimsAsync(
        Guid orderId, Guid attemptId, IReadOnlyList<GiftcardRecoveryClaim> claims, CancellationToken ct = default)
    {
        // Release exactly the supplied claims at the provider.
        throw new NotImplementedException();
    }

    public Task<GiftcardReservationFailure> ReconcileReservationFailureAsync(
        Guid orderId, Guid attemptId, GiftcardReservationFailure evidence, CancellationToken ct = default)
    {
        // Ask the provider what the original request did. Change nothing.
        throw new NotImplementedException();
    }
}
```

### ReleaseRecoveryClaimsAsync

- Ekom passes exactly one claim. Release only that claim, never other claims of the same order.
- Use the original provider, account, order and attempt. Check that the claim belongs to them before you release it.
- Return the same claim with its new status. Do not change any other field. Ekom refuses a result that changes the claim's identity or data.
- Return `Released` only after the provider has confirmed the release. A repeated release of a claim that is already released must also return `Released`.
- A timeout or a "not found" that you have not verified is not a confirmation. Throw, or return another status. Ekom then marks the claim as `ReleaseUnconfirmed`.
- Do not remove the card from the customer's basket, do not refund a settled balance, and do not complete the payment.

### ReconcileReservationFailureAsync

- Only read from the provider. Do not release, redeem or refund anything, for this or any other order.
- Return a receipt for the same order and attempt. Ekom refuses a receipt for another request.
- Set `AllEffectsAccountedFor = true` only when the original request is finished at the provider and every effect is known. Ekom treats any other answer as unknown.
- Never report "no effects" because a local expiry has passed or because a lookup found no current hold.
- You may add the provider and account to a claim that was recorded without them, and mark it `NewlyAcquired`, only when the provider proves that this claim came from the original order and attempt. A card lookup, or a matching card code, claim reference or amount, is not proof. The card code and claim reference must stay the same.
- You cannot change a claim that is `Settled` back to another status.
- If the provider cannot prove who owns a hold or what happened to it, keep the effects unknown and escalate to the provider.

## What Ekom stores, and when

Ekom keeps the evidence in the `EkomGiftcardReservationRecovery` table, one row per attempt:

1. **Before calling `ReserveAsync`**, Ekom saves the order, the attempt and the selected cards, with the state **pending**. If the server stops during the call, this row keeps the order blocked and visible in the Manager.
2. **When `ReserveAsync` returns**, Ekom saves the returned claims as evidence before it checks them or saves the order. These steps can still fail after the provider made holds.
3. **When the claims are saved on the order**, the row is marked as accepted and no longer needs recovery.
4. **When `ReserveAsync` fails**, Ekom saves your receipt, or records unknown effects, and the row needs recovery.

The evidence is kept apart from the gift cards on the order and from the frozen purchase of the payment attempt. It never changes the amount to pay and never counts as a payment.

When the evidence shows that a failed attempt is resolved, Ekom closes only that same attempt, and only if it was never sent to payment and nothing else holds the order. The gift-card codes and amounts stay selected, so the customer can check the basket and pay again.

Once holds may exist at the provider, Ekom saves the evidence and finishes its cleanup even if the browser disconnects. If Ekom cannot tell whether a database write went through, it keeps the order blocked. Ekom never unblocks an order because time has passed.

Ekom stores at most 256 claims and 128 KB of evidence per attempt. Larger evidence is cut short and treated as unknown.

### Limits you need to plan for

- A receipt in an exception cannot recover data that never reached Ekom before the server stopped. Use request IDs at the provider that are based on the order and attempt, so you can look the request up later, or keep your own durable journal in the integration.
- If the database is down after the provider made a hold, Ekom cannot save a reference it never saw. Keep the provider's own records and any other evidence, and reconcile the pending record before the customer pays again. A missing reference in Ekom never means that no hold exists.

### Deploy safely

- Ekom creates the `EkomGiftcardReservationRecovery` table at startup if it does not exist, also on existing sites.
- Back up the database, and deploy the same Ekom version to every server, before you turn on the new behaviour in your integration. Servers on an older version do not follow these recovery rules.
- Card and claim references are sensitive. Protect them with database access rules, and decide how long backups that contain them are kept.

## Related pages

- [Checkout](checkout.md): how gift cards are held and released during payment.
- [Orders in the Manager](../manager/orders.md): the recovery section and Release Lock.
- [Stock reservations](reservations.md): holds on stock, which follow similar rules.
- [Order reviews](order-reviews.md): other orders that a person must check.

[Back to Guides](README.md)
