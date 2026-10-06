# Checkout

Checkout turns the current basket into a payment attempt and, after a successful online callback or an offline payment, a completed order. Provider selection and payment submission are separate operations.

## Recommended flow

1. Load the current order and reject an empty basket.
2. Save customer and shipping information.
3. List and select a shipping provider.
4. List and select a payment provider.
5. Submit the order to the payment pipeline.
6. Let the payment provider or offline path complete the order.
7. Render the receipt using the completed order ID.

The sample storefront implements these stages in `CheckoutContactInformation.cshtml`, `CheckoutDeliveryOptions.cshtml`, and `CheckoutPaymentOptions.cshtml`.

## Prepare the order

```csharp
IOrderInfo? order = await orderApi.GetOrderAsync("Store", ct);
if (order is null || order.TotalQuantity <= 0)
{
    throw new InvalidOperationException("The basket is empty.");
}

order = await orderApi.UpdateCustomerInformationAsync(
    new Dictionary<string, string>
    {
        ["storeAlias"] = "Store",
        ["customerName"] = "Jane Doe",
        ["customerEmail"] = "jane@example.com",
        ["customerAddress"] = "Example Street 1",
        ["customerCity"] = "Reykjavik",
        ["customerZipCode"] = "101",
    },
    ct: ct);

order = await orderApi.UpdateShippingInformationAsync(
    shippingProviderKey,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);

order = await orderApi.UpdatePaymentInformationAsync(
    paymentProviderKey,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

The default payment validation requires a non-empty customer name and email. Provider updates persist ordered provider snapshots and recalculate the order. Passing provider-specific form values in the data dictionary makes them available to the ordered provider.

Adding the first customer email raises the checkout-started order event used by optional tracking. Shipping and payment selection raise their corresponding order events.

### Shipping validation

Cart updates keep a selected shipping provider when its amount or destination constraints become invalid, so the customer can still see their selection. Checkout revalidates it against the order's store, current qualifying amount (excluding shipping/payment fees), and shipping country, falling back to the customer country. Validation also runs after coupon processing and blocks payment rather than silently switching shipping methods.

Deleted or disabled shipping providers are still removed from incomplete orders. A persisted `ShippingProviderInvalidation` marker prevents that removal from being mistaken for an intentional no-shipping order. Selecting another available provider clears the marker.

Orders without a shipping selection remain allowed. To explicitly clear a selection or invalidation marker, use:

```csharp
order = await orderApi.UpdateShippingInformationAsync(
    Guid.Empty, "Store", new Dictionary<string, string>(),
    new OrderSettings { OrderInfo = order, ClearShippingProvider = true }, ct);
```

An empty provider ID without `ClearShippingProvider` retains the existing API no-op behavior. At checkout, explicitly submitting `ShippingProvider = Guid.Empty` selects no shipping and clears any previous selection; omitting the field does not clear a saved selection or invalidation marker.

For invalid shipping, the API returns HTTP 400 with a `ShippingValidationError` body (`code`, `providerKey`, `providerName`, `reason`, `message`, `currentAmount`, and applicable minimum/maximum amounts). MVC redirects to `PaymentRequest.ReturnUrl` with `errorStatus=invalidShippingProvider`, `errorReason`, and `errorMessage`. Set the return URL to your checkout/payment step and render the message as escaped text. Headless clients must display the error and let the customer change their selection. Other checkout validation responses are unchanged.

## Submit payment

For server-side code, call `Ekom.API.Order.PayAsync`:

```csharp
var request = new PaymentRequest
{
    PaymentProvider = paymentProviderKey,
    ShippingProvider = shippingProviderKey,
    StoreAlias = "Store",
    Culture = "en-US",
    ReturnUrl = "/checkout/receipt",
};

CheckoutResponse response = await orderApi.PayAsync(
    request,
    "Store",
    order.UniqueId,
    ct);
```

For HTTP clients:

```http
POST /ekom/checkout/pay?culture=en-US
Content-Type: application/json

{
  "paymentProvider": "00000000-0000-0000-0000-000000000020",
  "shippingProvider": "00000000-0000-0000-0000-000000000010",
  "storeAlias": "Store",
  "culture": "en-US",
  "returnUrl": "/checkout/receipt"
}
```

The API accepts JSON or form data. Known fields are `PaymentProvider`, `ShippingProvider`, `CardNumber`, `CVV`, `Year`, `Month`, `StoreAlias`, `ReturnUrl`, `Culture`, and form field `Nonce`; other values are copied to `PaymentRequest.AdditionalData`. Never collect card fields unless the selected provider explicitly requires and securely handles them.

`/ekom/mvcCheckout/pay` is the antiforgery-protected form endpoint generated by `Html.BeginEkomCheckoutForm(CheckoutFormType.Pay, ...)`. `/ekom/checkout/pay` is the API endpoint used by headless clients.

## What the payment pipeline does

`CheckoutControllerService` performs these operations in order:

1. Applies requested customer/provider updates.
2. Validates required customer data.
3. Acquires per-order checkout preparation ownership.
4. Validates stock and prepares reservations when enabled.
5. Persists reservation IDs before payment submission.
6. Creates payment order items and raises checkout events.
7. Runs the offline or online provider path.

Offline providers have `offlinePayment` enabled on the payment-provider node. Ekom sets `OfflinePayment`, raises payment events, completes checkout immediately, and redirects to the configured success URL.

Online providers are resolved from `basePaymentProvider`, the order moves to `WaitingForPayment`, and Ekom calls the provider's `RequestAsync`. Success URLs include `orderId`. Error and cancel callbacks return through `GET|POST /ekom/checkout/payment-return`, which restores the order cookie and redirects to the provider node's configured error or cancel URL.

### Cancel, edit, and retry reservations

The built-in checkout keeps the same basket/order identity across payment retries. Each submission has a distinct payment-attempt ID and retains an immutable submitted purchase snapshot. The attempt ID is carried in payment settings as `ekomPaymentAttemptId` and on error/cancel return URLs as `attemptId`.

- Stock is reserved before payment when reservations are enabled. Requirements come from order lines; quantities sharing the same stock identity are combined. At submission all holds share the earliest hold deadline, without extending an existing hold.
- A matching cancel/error return releases that attempt's holds immediately and restores the unpaid basket to **Incomplete**. Repeated release is idempotent; an old return cannot release a newer attempt's holds.
- A cart edit releases existing holds before stock validation and persistence. Merely viewing the cart or a receipt does not release stock.
- The next payment submission validates stock again and acquires fresh holds. Closing a payment page without a return or edit leaves its reservations held until timeout.
- Giftcard selections remain in the basket. External claims must be released and reacquired through the application's giftcard lifecycle integration; clearing a local claim field is not proof that external balance is available.

Provider POST returns first redirect through a GET, allowing `SameSite=Lax` basket cookies to accompany the request before the basket cookie is restored. Keep both `orderId` and `attemptId` in custom return URLs and preserve `ekomPaymentAttemptId` in custom payment settings.

Custom `ProcessPaymentAsync` overrides can read the protected `CurrentPaymentAttemptId` and use `BuildPaymentReturnUrl(orderId, outcome)`. Include that attempt ID in `PaymentSettings.OrderCustomData["ekomPaymentAttemptId"]`; the shared success handler needs it to distinguish payment attempts on the same order.

Use the provider's exact payment reference when looking up a gateway attempt. Ekom's order ID remains the same across retries, so a payment lookup that returns the first record associated with that order ID is ambiguous. The verified callback must retain the settings of the payment that actually succeeded, not load the latest basket or latest attempt's settings.

Applications that create external giftcard claims must register an `ICheckoutGiftcardReservations` implementation, for example `services.AddSingleton<ICheckoutGiftcardReservations, MyGiftcardReservations>()` (use the appropriate lifetime for your integration). `ReserveAsync` returns the selected cards with freshly acquired claim metadata; `ReleaseAsync` must confirm release of the supplied unredeemed claims and handle repeated or partially successful calls safely. Check provider response success, not just whether the call threw. If external release cannot be confirmed, throw: the attempt remains pending release and edits/retries stay blocked rather than pretending the balance is available. Existing claimed cards without an adapter are rejected with an actionable conflict.

Keep this adapter in the consuming application: Ekom's `Giftcard` model does not call an external balance service. For GiftToWallet integrations, release by claim ID rather than using a helper that removes the selected card from Ekom. Do not release redeemed/settled claims. Reacquisition must return new metadata and must not silently reuse a released claim; honor any provider-specific single-card restriction.

**Immediate release is an availability-first business policy, not confirmation that the gateway payment was cancelled.** A verified success for a released, expired, superseded, or changed attempt is recorded for reconciliation and does not complete the edited basket. A real late payment may require manual handling or refund. Old callbacks without attempt metadata cannot be used to complete an order that has newer tracked attempts.

Release attempts and outcomes are written directly to the order activity log, independently of the normal activity-log queue. Entries include the reason, attempt/reservation identity, safe stock key and quantity, and whether stock was restored, already released/expired, skipped, or failed. Expiry restoration is also audited. Codes and payment secrets are not logged. Logging failures are reported to the application log and must not restore stock twice or manufacture a successful release outcome.

Per-order operation ownership coordinates checkout, edits, returns, and completion across nodes. Uncertain persistence must retain ownership rather than allowing an old writer to resume against a newer attempt. Interrupted ownership requires explicit reconciliation; there is no unsafe timed takeover.

Historical expired reservations are retained for audit, but are not recovered as holds for a fresh attempt after cancel/reset and a cart edit. Explicitly attached expired IDs and consumed stock still block unsafe reuse.

Expected preparation conflicts return HTTP **409** with a customer-safe `CheckoutStateError` (`code`, `message`, `canRetry`). Codes distinguish `checkout_state_conflict`, `checkoutBusy`, `paymentReviewRequired`, and `checkoutCompleted`. The built-in form checkout redirects with the safe `errorStatus`/`errorMessage` values instead of diagnostic exception text. `canRetry` is false: clients must not automatically resubmit payment on a conflict. Follow the message, refresh the basket when appropriate, and contact the store for payment review. Stock-shortage and shipping-validation responses retain their existing behavior. Unexpected form-checkout failures redirect with `errorStatus=serverError`; diagnostic details remain in application logs.

Do not call `CompleteOrderAsync` merely because a browser returned to the site. A payment integration should complete only after it has verified the provider outcome. `Order.CompleteOrderAsync(orderId, ct)` is intended for trusted server-side payment/offline integrations.

## Completion

### Payment-success logging

The shared Ekom Payments success handler writes a `Success` order activity entry and an Information-level structured application log before calling checkout completion. Both include the payment amount, currency, provider node name, order number, and Ekom order `UniqueId`. New online payment requests save the provider name in their payment settings; older pending requests without a provider name use its key instead. No additional order lookup is needed for this logging. Online integrations must raise `Ekom.Payments.Events.SuccessAsync` after verifying payment; integrations that bypass this event do not reach the shared handler.

Offline checkout writes the same fields with the message `Offline Payment Successfull` after payment events have accepted the checkout and before completion starts. This records offline checkout acceptance, not confirmation that funds have been received.

These payment entries are committed directly to the database rather than sent through the normal activity-log queue. A later checkout failure therefore does not roll back a successfully written payment entry. If the activity insert fails, Ekom emits an Error log and still attempts order completion; the Information log is emitted before the database write. Callback retries can produce repeated success entries, reflecting each received success notification. Direct calls to `Order.CompleteOrderAsync` do not by themselves prove payment success and do not create these entries.

### Order finalisation

`CheckoutService.CompleteAsync` loads the persisted order and raises `CompleteCheckout`/`CompleteCheckoutAsync`. It then completes stock processing, marks the coupon used when applicable, updates status unless disabled by an event handler, writes an activity log, and clears the customer order reference.

Completion stock work is idempotent through a native SQL completion receipt. Other effects, including event subscribers and external integrations, can be invoked again by callback retries and should therefore be idempotent.

## Checkout events

Prefer async handlers:

| Event | Use |
| --- | --- |
| `CheckoutEvents.ProcessingAsync` | Inspect the order or set `StockValidation = false` before preparation. |
| `CheckoutEvents.PaymentOrderItemsPreparingAsync` | Replace or edit the items passed to the payment provider. |
| `CheckoutEvents.PayAsync` | Inspect or edit `PaymentSettings` immediately before provider submission. |
| `CheckoutEvents.CompleteCheckoutAsync` | Run completion integrations; can change `StockValidation` and `UpdateOrderStatus`. |

```csharp
CheckoutEvents.PaymentOrderItemsPreparingAsync += (sender, args, ct) =>
{
    // args.OrderItems is the collection sent to the payment provider.
    return Task.CompletedTask;
};
```

Static handlers must be unsubscribed at shutdown. Disabling stock validation changes a core safety check; only do it when another trusted inventory system owns that decision. Existing attached reservations are still verified and consumed during completion.

## Responses and failures

The checkout response uses provider-oriented status codes:

| Code | Meaning in the built-in flow |
| --- | --- |
| `230` | HTML returned by an online payment provider. The MVC endpoint returns it as `text/html`; the API serializes the response body. |
| `300` | Redirect URL, used by offline payment. |
| `400` | Invalid request or missing required checkout data. |
| `530` | Stock failure, with a `StockError` when available. |

Treat payment submission exceptions as an uncertain provider outcome. Ekom intentionally retains protected reservations after submission starts so that a retry cannot incorrectly release stock for a payment that may have succeeded. Reconcile provider state before retrying. See [Reservations](reservations.md).
