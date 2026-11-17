[Ekom docs](../README.md) / [Guides](README.md) / Checkout

# Checkout

Checkout takes the customer's basket, sends it to payment, and turns it into a completed order once the payment is confirmed. Read this page when you build the checkout steps of a storefront, connect a payment gateway, or need to know what state an order is in when a payment goes wrong.

Choosing a shipping or payment provider and submitting payment are separate calls. Choosing a provider only saves the choice on the basket. Nothing is charged and no stock is taken until you submit payment.

On this page:

- [The checkout steps](#the-checkout-steps)
- [Prepare the basket](#prepare-the-basket)
- [Keep or clear the shipping choice](#keep-or-clear-the-shipping-choice)
- [Submit payment](#submit-payment)
- [What Ekom does when you submit payment](#what-ekom-does-when-you-submit-payment)
- [Responses and errors](#responses-and-errors)
- [Cancel, edit and retry a payment](#cancel-edit-and-retry-a-payment)
- [How a paid order is completed](#how-a-paid-order-is-completed)
- [Gift cards held at an external provider](#gift-cards-held-at-an-external-provider)
- [Checkout events](#checkout-events)
- [When checkout is blocked](#when-checkout-is-blocked)

## The checkout steps

1. Load the current basket. Stop if it is empty.
2. Save the customer's name, email and address.
3. List the shipping providers and save the customer's choice.
4. List the payment providers and save the customer's choice.
5. Submit payment.
6. The payment provider confirms the payment, or the offline path completes the order at once.
7. Show the receipt for the completed order.

The U17 sample site shows these steps in `Samples/U17/Ekom.Site.U17/Views/CheckoutContactInformation.cshtml`, `CheckoutDeliveryOptions.cshtml` and `CheckoutPaymentOptions.cshtml`.

## Prepare the basket

Inject `Ekom.API.Order`, or use `Order.Instance`.

```csharp
using Ekom.API;
using Ekom.Models;

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
        ["customerAddress"] = "Laugavegur 1",
        ["customerCity"] = "Reykjavik",
        ["customerZipCode"] = "101",
        ["customerCountry"] = "IS",
    },
    ct: ct);

order = await orderApi.UpdateShippingInformationAsync(
    homeDeliveryKey,
    "Store",
    new Dictionary<string, string>
    {
        ["customshippingDeliveryNote"] = "Leave at the back door",
    },
    ct: ct);

order = await orderApi.UpdatePaymentInformationAsync(
    cardPaymentKey,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

- Payment needs a customer name and email. Without them, payment submission returns HTTP 400.
- `UpdateShippingInformationAsync` and `UpdatePaymentInformationAsync` save a copy of the provider on the order and recalculate the order. If the provider key does not exist in the store, they leave the providers as they are and do not throw.
- In the data dictionary, keys that start with `customshipping` are saved on the order's shipping provider, and keys that start with `custompayment` on its payment provider. Ekom HTML-encodes these values. Keys in the form `CustomData[name]` go to the order's custom data. See [Order custom data](order-custom-data.md).
- When the order gets its first customer email, Ekom raises `OrderEvents.CustomerEmailAddedAsync`. Optional purchase tracking uses it as the start of checkout. Choosing a different provider raises `OrderEvents.ShippingProviderAddedAsync` or `OrderEvents.PaymentProviderAddedAsync`.

[Payment and shipping providers](providers.md) shows how to list the providers a customer can choose.

## Keep or clear the shipping choice

When the basket changes, the chosen shipping provider can stop matching its rules, for example when the order amount drops below the provider's minimum. Ekom keeps the choice on the basket so the customer still sees it. When the customer submits payment, Ekom checks it again against:

- the order's store,
- the order amount without shipping and payment fees, never less than zero,
- the shipping country, or the customer's country when no shipping country is set.

Ekom runs this check before stock is prepared and again after coupons are processed. If the choice no longer fits, payment is refused. Ekom never switches to another shipping method by itself.

A shipping provider that no longer exists in the store, for example because it was deleted, is removed from the basket the next time the basket is saved while its status is **Incomplete**. Ekom then saves a `ShippingProviderInvalidation` marker on the order with the old provider's key and name, so the empty choice is not mistaken for an order that needs no shipping. Payment is refused until the customer chooses again. Choosing another available provider removes the marker.

An order without any shipping choice is allowed.

To clear the shipping choice and the marker from code, pass `Guid.Empty` and set `ClearShippingProvider`:

```csharp
order = await orderApi.UpdateShippingInformationAsync(
    Guid.Empty,
    "Store",
    new Dictionary<string, string>(),
    new OrderSettings { OrderInfo = order, ClearShippingProvider = true },
    ct);
```

`Guid.Empty` without `ClearShippingProvider` changes nothing.

In a payment request, `ShippingProvider` set to `Guid.Empty` chooses no shipping and clears an earlier choice and its marker. Leaving the field out keeps what is saved.

## Submit payment

### From server code

`Order.PayAsync` runs the same steps as the HTTP endpoint. It needs a current HTTP request: without one it throws `ArgumentNullException`.

```csharp
var request = new PaymentRequest
{
    ShippingProvider = homeDeliveryKey,
    StoreAlias = "Store",
    Culture = "is-IS",
    ReturnUrl = "/checkout/payment",
};

CheckoutResponse response = await orderApi.PayAsync(request, "Store", order.UniqueId, ct);
```

`Order.PayAsync` uses the payment provider already saved on the order. It does not apply `PaymentRequest.PaymentProvider`, so save the payment choice with `UpdatePaymentInformationAsync` first.

### From a headless client

```http
POST /ekom/checkout/pay?culture=is-IS
Content-Type: application/json

{
  "paymentProvider": "6d1c4b7e-2f0a-4a51-9a0e-3b6f8c2d1e44",
  "shippingProvider": "0b9e2a61-7c3d-4f58-8e21-5a4d6c7b9f10",
  "storeAlias": "Store",
  "culture": "is-IS",
  "returnUrl": "/checkout/payment"
}
```

The endpoint accepts JSON or form data. The known fields are `PaymentProvider`, `ShippingProvider`, `CardNumber`, `CVV`, `Year`, `Month`, `StoreAlias`, `ReturnUrl` and `Culture`, and in form data also `Nonce`. Ekom passes the other fields on in `PaymentRequest.AdditionalData`. A JSON body with the same property twice is rejected.

Do not collect card fields unless the payment provider you use requires them and handles them securely.

To save customer details in the same request, include the field `ekomUpdateInformation` with the `customer...` and `shipping...` fields. Fields in the form `CustomData[name]` are saved to the order's custom data.

### From a Razor form

`Html.BeginEkomCheckoutForm(CheckoutFormType.Pay)` renders a form that posts to `/ekom/mvcCheckout/pay`. This endpoint checks the antiforgery token and answers with redirects. The form already contains a hidden `returnUrl` field with the current page address, without `errorStatus` and `errorMessage`. Do not add a second `returnUrl` field.

`returnUrl` must be one of:

- a path that starts with `/`,
- a path that starts with `~/`,
- an absolute address with the same scheme, host and port as the request. Ekom turns it into a path.

If `returnUrl` is not allowed, or the form has more than one `returnUrl` field, Ekom does not start payment. It redirects to the payment provider's `errorUrl` with `errorStatus=badReturnUrl`, when that URL is on the same site. Otherwise it redirects to `/?errorStatus=badReturnUrl`.

Ekom replaces the query parameters `errorStatus`, `errorReason`, `errorMessage`, `canRetry`, `errorType` and `orderline` in the return URL with its own values. Every other query parameter in the return URL must have exactly one value. Render values from the query string with normal HTML escaping, never as raw HTML.

## What Ekom does when you submit payment

Ekom runs these steps in order. A step that fails stops the rest.

1. **Takes ownership of the order.** Only one checkout or basket change runs for an order at a time, on any server. Ekom refuses payment if the order is paid, completed, waiting for review, or has a payment attempt that is still running. An attempt that was never sent to the gateway, or whose time ran out, is reset first.
2. **Starts a new payment attempt.** Each submission gets its own attempt ID. If the basket has gift cards and you registered a gift-card integration, Ekom asks it to hold the cards now. See [Gift cards held at an external provider](#gift-cards-held-at-an-external-provider).
3. **Saves the request's changes.** Customer details (see above), the payment provider (HTTP only) and the shipping provider.
4. **Checks the customer.** Name and email are required.
5. **Checks the shipping choice.** See [Keep or clear the shipping choice](#keep-or-clear-the-shipping-choice).
6. **Prepares stock.** Ekom refuses payment if the order's stock was already completed, or if the order's coupon has no uses left. It raises `CheckoutEvents.ProcessingAsync`, then checks stock or creates holds, as described in [Stock reservations](reservations.md#what-checkout-does-with-holds). It checks the shipping choice again, builds the payment title, and saves the hold IDs on the order.
7. **Freezes the purchase.** Ekom saves a copy of the order and the hold IDs with the attempt, gives all holds the earliest deadline among them, and marks the attempt as submitted.
8. **Sends the order to payment,** by the offline path or the online path below.

The payment title is the store's `paymentOrderTitle` property followed by ` - ` and the order number. A value that starts with `#` is read as an Umbraco dictionary key. When the property is empty, Ekom uses a built-in Icelandic title.

By default the provider gets one payment item: the payment title, the amount still to pay (`ChargedAmount`), its VAT, and quantity 1. Change the items in `CheckoutEvents.PaymentOrderItemsPreparingAsync`.

### Offline payment

A payment provider node with `offlinePayment` turned on takes no payment online, for example pay on delivery or bank transfer.

1. Ekom sets the order status to `OfflinePayment`.
2. It raises `CheckoutEvents.PayAsync`.
3. It records a payment with a `Captured` transaction. This records that checkout was accepted, not that money was received. If recording fails, Ekom logs an error and completes the order anyway.
4. It completes the order. See [How a paid order is completed](#how-a-paid-order-is-completed).
5. It redirects to the provider's `successUrl` with `?orderId=<order id>` added.

If a step fails, Ekom sets the order status to `PaymentFailed` and the request fails.

### Online payment

1. Ekom sets the order status to `WaitingForPayment`.
2. It finds the payment provider implementation by the node's `basePaymentProvider` value, or by the node name when that is empty.
3. It raises `CheckoutEvents.PayAsync` with the `PaymentSettings` it will send. After the handlers run, Ekom sets `ekomOrderUniqueId`, `ekomOrderReferenceId` and `ekomPaymentAttemptId` in `PaymentSettings.OrderCustomData` again, so a handler cannot remove them.
4. It calls the provider's `RequestAsync` and returns what the provider returns, usually HTML that sends the customer to the gateway.

The settings tell the gateway where to send the customer back:

- **Success:** the provider's `successUrl` with `?orderId=<order id>` added.
- **Error and cancel:** `/ekom/checkout/payment-return` with `orderId`, `attemptId` and `outcome` (`error` or `cancel`).

The success page shows the result to the customer. It does not complete the order. The order is completed when the provider records the payment, as described in [How a paid order is completed](#how-a-paid-order-is-completed).

### If sending to payment fails

If the provider call throws, or the result is an error, Ekom releases the holds of this attempt and sets the order back to **Incomplete**, so the customer can try again. Ekom does this even though it cannot know whether the gateway received the request. If the gateway later confirms a payment for this attempt, the order is flagged for review. See [A payment that arrives after its holds were released](#a-payment-that-arrives-after-its-holds-were-released).

If the release itself fails, Ekom logs an error and the attempt stays as it is, so the order stays blocked until someone checks it.

## Responses and errors

`CheckoutResponse.HttpStatusCode` tells you what happened.

| Code | Meaning | `/ekom/checkout/pay` | `/ekom/mvcCheckout/pay` |
| --- | --- | --- | --- |
| `230` | The online provider returned HTML to show. | Status 230 with the HTML as a JSON string. | Returns the HTML as `text/html`. |
| `300` | Offline payment finished. `ResponseBody` is the success URL. | HTTP redirect to the success URL. | HTTP redirect to the success URL. |
| `400` | Customer name or email missing. | 400 with no body. | Redirect with `errorStatus=invalidData`. |
| `400` | The shipping choice does not fit. | 400 with a `ShippingValidationError` body. | Redirect with `errorStatus=invalidShippingProvider`, `errorReason` and `errorMessage`. |
| `409` | Checkout cannot start in the order's current state. | 409 with a `CheckoutStateError` body. | Redirect with `errorStatus`, `errorMessage` and `canRetry`. |
| `530` | Not enough stock. | 530 with a `StockError` body. | Redirect with `errorStatus=stockError`, `errorType` (`product`, `variant` or `unknown`) and, when known, `orderline`. |

An unexpected error returns HTTP 500 from the API, and a redirect with `errorStatus=serverError` from the form endpoint. Details are written to the application log only. From the API, a missing basket, an order without a payment provider, or a payment provider node that no longer exists returns HTTP 404.

### Shipping errors

The `ShippingValidationError` body has `code` (always `invalidShippingProvider`), `providerKey`, `providerName`, `reason`, `message`, `currentAmount`, and `minimumAmount` or `maximumAmount` when they apply.

| `reason` | Why |
| --- | --- |
| `providerUnavailable` | The provider no longer exists in the store. |
| `belowMinimumAmount` | The order amount is below the provider's start of range. |
| `aboveMaximumAmount` | The order amount is above the provider's end of range. |
| `destinationNotSupported` | The provider's zone does not include the country. |
| `constraintsNotMet` | Any other mismatch. |

Point `returnUrl` at the checkout step where the customer chooses shipping, show `message` as escaped text, and let the customer choose again.

### Checkout state errors

The `CheckoutStateError` body has `code`, `message` and `canRetry`. `message` is written for the customer and never contains technical details.

| `code` | Meaning |
| --- | --- |
| `checkoutBusy` | Another checkout or basket change for this order is running, or a payment attempt is still active. |
| `paymentReviewRequired` | A payment needs to be checked by the store before the customer can pay or change the basket. |
| `checkoutCompleted` | The order is already completed or paid. |
| `couponUnavailable` | The order's coupon has no uses left. |
| `checkout_state_conflict` | Checkout could not be prepared for another reason. |

`canRetry` is always `false`. Do not resubmit payment by yourself after a 409. Show the message, reload the basket when it makes sense, and tell the customer to contact the store when the code is `paymentReviewRequired`.

## Cancel, edit and retry a payment

The built-in checkout keeps the same basket and order ID through every retry. Each submission is a new payment attempt with its own attempt ID and its own frozen copy of the purchase.

### When the customer cancels or the gateway reports an error

The gateway sends the customer to `/ekom/checkout/payment-return`. Ekom then:

1. answers a POST with a redirect to the same address as a GET, so the browser sends the basket cookie, which is `SameSite=Lax`,
2. checks that `orderId` is a valid order and that `attemptId`, when present, belongs to a submitted attempt of that order. Otherwise it returns HTTP 400 or 404,
3. releases the holds of that attempt and sets the order back to **Incomplete**,
4. restores the basket cookie,
5. redirects to the provider's `cancelUrl` for `outcome=cancel`, otherwise to its `errorUrl`. The query string carries what the gateway sent, plus `orderId` and `outcome`.

A return for an older attempt does not release a newer attempt's holds. A second return for the same attempt changes nothing.

### When the basket changes

Every basket change ends the current payment attempt first and releases its holds. This includes adding, removing or changing lines, discounts, coupons, gift cards, currency, and the shipping or payment provider. The next payment submission checks stock again and creates new holds.

These are information updates instead. They are allowed in any order state and keep the payment attempt and its holds:

- `UpdateCustomerInformationAsync`
- `UpdateCustomDataAsync` and `RemoveCustomDataAsync`
- `UpdateOrderLineMetadataAsync`

Viewing the basket or the receipt changes nothing.

Do not change customer details while a payment is running. The frozen purchase includes them, and a payment confirmed for a purchase that has changed does not complete the order: it needs review. Custom data and order line metadata are not part of the frozen purchase.

### When the customer closes the payment page

Nothing happens until the attempt's deadline. Then the expiry worker gives the stock back, and the next payment submission resets the old attempt. An attempt without holds, for example with reservations turned off, also ends after the store's `Reservations:Timeout`.

### A payment that arrives after its holds were released

Releasing holds on cancel, error or edit is a choice for availability: the stock goes back on sale at once. It is not proof that the gateway cancelled the payment.

If the gateway later confirms a payment for an attempt that was released, expired, replaced by a newer attempt, or whose purchase changed, Ekom:

- records the payment as the gateway reported it,
- does not complete the basket,
- opens an order review with the code `paid-checkout-changed`,
- writes `Payment NEEDS RECONCILIATION` to the activity log and the application log.

You must handle this order by hand: compare the payment with the order, then complete the order or refund the payment. A callback without an attempt ID cannot complete an order that has newer attempts.

### Custom payment providers and return URLs

If you write your own return URLs, keep both `orderId` and `attemptId` in them, and keep `ekomPaymentAttemptId` in `PaymentSettings.OrderCustomData`. Ekom needs the attempt ID to tell attempts of the same order apart.

A class that overrides `CheckoutControllerService.ProcessPaymentAsync` can read the current attempt from the protected `CurrentPaymentAttemptId` property, and build an error or cancel URL with `BuildPaymentReturnUrl(orderId, "error")` or `BuildPaymentReturnUrl(orderId, "cancel")`.

When your callback looks up a payment, use the gateway's exact payment reference. The order ID stays the same across retries, so a lookup by order ID can find the wrong attempt. Complete the payment that actually succeeded, with its own settings, not the latest basket or the latest attempt.

## How a paid order is completed

### A recorded payment completes the order

A payment provider verifies the gateway's callback and records the outcome with `IOrderPayments.RecordAsync`. When it records a `Captured` or `Authorized` transaction and the order status is still `WaitingForPayment`, Ekom completes the order. Providers do not call completion themselves. See [Payments](payments.md).

Recording a payment writes a `Success` entry to the order activity log and an Information entry to the application log, in the same database transaction as the payment. Both start with the order number and include the amount, currency, provider name, the gateway's transaction ID and the payment ID. A later failure in completion does not remove them.

If the gateway sends the same callback twice, the payment is stored once. If the first callback stopped before the order was completed, the second one finishes the completion.

Before it completes the order, Ekom checks that the payment belongs to the current attempt, that the frozen purchase has not changed, and that the attempt's holds are still active. If any check fails, the order is not completed. See [A payment that arrives after its holds were released](#a-payment-that-arrives-after-its-holds-were-released).

### What completion does

1. Loads the saved order. If the order does not exist, completion stops without an error.
2. Raises `CheckoutEvents.CompleteCheckout` and then `CheckoutEvents.CompleteCheckoutAsync`. `args.Payment` holds the payment the order was paid with. See [Read the payment in your own code](payments.md#read-the-payment-in-your-own-code).
3. Completes stock: consumes the order's holds and takes the rest from stock, in one transaction. This step is skipped if it already ran for this order.
4. Marks the coupon as used, when the order has a coupon. If the coupon has no uses left, the order still completes, Ekom opens a review with the code `coupon-exhausted` and writes an alert to the activity log.
5. Sets the order status to `ReadyForDispatch`. An offline order keeps its `OfflinePayment` status. A handler can turn this step off with `args.UpdateOrderStatus = false`. Setting the status also clears the customer's reference to this order as their current basket.
6. Writes `Order Completed.` to the activity log, or `Order Completed. Offline payment.` when the status was not changed.

If completion fails, Ekom opens a review (`completion-stock-failed` when stock ran out, `completion-failed` otherwise), sends an email through the site's `IMailService`, and throws. The payment attempt stays in its completing state, which blocks edits and new payments. The same verified callback can run completion again.

Stock completion runs at most once per order, guarded by a receipt in the database. Your event handlers and other integrations can run again when a callback is repeated, so make them safe to repeat.

### Completing an order from code

`Order.CompleteOrderAsync(orderId, ct)` runs completion directly. It does not check the payment attempt, it does not record a payment, and `args.Payment` can be `null`. Call it only from trusted server code that has verified the payment itself. Never call it because a browser came back to the success page.

## Gift cards held at an external provider

Ekom's `Giftcard` model does not talk to any gift-card service. If the gift cards in your store are balances at an external provider that must be held while the customer pays, register an integration that implements `ICheckoutGiftcardReservations`:

```csharp
builder.Services.AddSingleton<ICheckoutGiftcardReservations, WalletGiftcardReservations>();
```

Choose the lifetime your integration needs. Keep the integration in your own application.

### When Ekom calls it

- **`ReserveAsync(orderId, attemptId, selections, ct)`** is called at each payment submission, when the basket has gift cards. Return the selected cards with new claim metadata from the provider. Before the call, Ekom records that a hold is being requested. After the call, it saves the returned claims before it does anything else.
- **`ReleaseAsync(orderId, attemptId, selections, ct)`** is called when the attempt is released: on cancel, on error, on a basket change, or when an expired attempt is reset. `attemptId` is `Guid.Empty` for holds from before attempts were tracked.

Released gift cards stay selected in the basket with their code and amount. The next payment holds them again.

### Rules for ReleaseAsync

- Return only when the provider has confirmed that every card you received is released. Check the provider's response, not only that the call did not throw.
- If you cannot confirm the release, throw. The attempt then stays waiting for release, and basket changes and new payments stay blocked. That is safer than showing a balance as free when it is still held.
- Calls can repeat, and an earlier call can have released only some cards. Handle both.
- Release by claim. Do not remove the card from the basket to release it.
- Never release a card that is redeemed or settled: `Claimed` is `true`, or `TransactionId`, `ClaimDate` or `UsedDate` is set. Ekom does not pass such a card to `ReleaseAsync`. It marks the order as needing payment reconciliation instead.
- You may receive a single card, for example from a Manager action. Release only the cards you receive, never other cards of the same order.

A hold for a new attempt must return new claim metadata. Do not quietly reuse a claim that was released. If the provider allows only one card per payment, enforce that in your integration.

If an order has claimed gift cards and no integration is registered, Ekom refuses to cancel or change it and asks you to register `ICheckoutGiftcardReservations`.

### When ReserveAsync fails

What you throw tells Ekom what may have happened at the provider:

| You throw | Use it when | What Ekom does |
| --- | --- | --- |
| `GiftcardReservationRejectedException` | You are certain the whole call created no hold and changed no existing hold. | Closes the new attempt and keeps earlier claims as they were. The request fails with HTTP 500, or `errorStatus=serverError` from the form endpoint. |
| `GiftcardReservationFailureException` with a `GiftcardReservationFailure` receipt | The call may have created or changed holds, and you know which. This includes holds you acquired and then released again. | Saves the receipt and returns HTTP 409. Checkout stays blocked unless the receipt proves that every effect is released and nothing else holds the order. |
| Any other exception | You do not know what happened, for example after a timeout. | Records unknown effects and blocks checkout for the order until a person resolves it. The request fails with HTTP 500, or `errorStatus=serverError`. |

Throw `GiftcardReservationRejectedException` only when you know for certain that nothing changed. A message such as "insufficient balance", an HTTP status code, a timeout or an uncertain answer is not proof. A call that acquired holds and released them again did have effects: report it with `GiftcardReservationFailureException`.

Cancellation inside `ReserveAsync` counts as uncertain. Ekom cannot tell a cancelled balance lookup from a hold the provider accepted.

[Failed gift-card preparation recovery](giftcard-recovery.md) explains the receipt, the recovery actions in the Manager, and how to set up an integration for recovery.

## Checkout events

Use the async events. The sync events (`Processing`, `PaymentOrderItemsPreparing`, `Pay`, `CompleteCheckout`) still exist and run before the async ones.

| Event | When | What you can change |
| --- | --- | --- |
| `CheckoutEvents.ProcessingAsync` | Before stock is checked or held. | `args.StockValidation = false` skips the product stock check and, with reservations on, the product holds. Discount holds are still made. |
| `CheckoutEvents.PaymentOrderItemsPreparingAsync` | Before the payment items are sent to the provider. | Edit or replace `args.OrderItems`. |
| `CheckoutEvents.PayAsync` | Just before the provider is called. | Edit the properties of `args.PaymentSettings`. |
| `CheckoutEvents.CompleteCheckoutAsync` | At completion, before stock is completed. | `args.StockValidation` and `args.UpdateOrderStatus`. |

```csharp
CheckoutEvents.PaymentOrderItemsPreparingAsync += (sender, args, ct) =>
{
    // args.OrderItems is what the payment provider receives.
    return Task.CompletedTask;
};
```

Handlers run one after the other. An exception in a handler stops the checkout step it belongs to. Unsubscribe static handlers when the site shuts down.

### Turning off the stock check

`StockValidation = false` turns off a safety check. Use it only when another system decides what stock is available. Holds already saved on the order are still checked and consumed at completion.

`ProcessingAsync` and `CompleteCheckoutAsync` each have their own `StockValidation`. If you set it to `false` only in `ProcessingAsync`, completion still takes stock for lines without holds, and completion fails with `NotEnoughStockException` if there is not enough. That happens after the customer has paid. Set it in both events.

## When checkout is blocked

Ekom records an owner for each order while a checkout, basket change, return or completion runs, on any server. If a request stops at a point where Ekom cannot know whether its database writes or the payment went through, Ekom keeps the owner. A later request cannot pick up or undo the old request's work. The owner has no timeout, and Ekom never takes over an order because time has passed.

Ekom finishes its own cleanup even when the browser disconnects. It uses a separate cancellation token for it, so a closed browser does not leave half-saved state. Once the gift-card integration has returned claims, Ekom saves them and, if checkout did not reach payment, releases them, whether the browser is still connected or not.

To unblock an order, a store editor uses **Release Lock** in the Manager:

1. Make sure the old request has stopped.
2. Check with the payment provider that no payment is being processed for the order.
3. Use **Release Lock**.

Release Lock resets checkout state and releases holds, and tries to release known gift-card claims. For older orders it warns, instead of stopping, when a gift-card release cannot be confirmed. It is not a refund, does not cancel a payment at the gateway, and keeps the old claim information in its recovery record.

Release Lock is not available while a failed gift-card preparation is pending or unresolved. Use the gift-card recovery actions first. See [Checkout lock and manual recovery](../manager/orders.md#checkout-lock-and-manual-recovery) and [Failed gift-card preparation recovery](giftcard-recovery.md).

## Related pages

- [Payment and shipping providers](providers.md): set up and list the providers a customer can choose.
- [Payments](payments.md): record payments from a gateway and read them in your code.
- [Stock reservations](reservations.md): how stock is held while the customer pays.
- [Order, payment, and fulfillment statuses](order-statuses.md): the statuses an order moves through.
- [Order reviews](order-reviews.md): orders that a person needs to check.
- [Headless storefronts](headless.md): build checkout over HTTP.

[Back to Guides](README.md)
