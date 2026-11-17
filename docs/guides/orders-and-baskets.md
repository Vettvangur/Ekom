[Ekom docs](../README.md) / [Guides](README.md) / Orders and baskets

# Orders and baskets

A basket is the order a customer is still building in a store. It is saved in the database from the first added line, and the same record becomes the order when checkout completes. This page shows how to read the current basket, add and change lines, store customer details, apply coupons and gift cards, and hand the basket to checkout.

All examples use the `Ekom.API.Order` facade. Inject it (the examples call it `orderApi`), or use `Order.Instance` where injection is not possible. Every method that changes a basket returns the recalculated `IOrderInfo`. Render totals and lines from that result, not from values you calculated in the browser.

On this page:

- [How Ekom finds the current basket](#how-ekom-finds-the-current-basket)
- [Add order lines](#add-order-lines)
- [Change and remove lines](#change-and-remove-lines)
- [Refresh prices after a customer signs in](#refresh-prices-after-a-customer-signs-in)
- [Linked lines](#linked-lines)
- [Save customer, consent and tracking details](#save-customer-consent-and-tracking-details)
- [Save your own values on the order](#save-your-own-values-on-the-order)
- [Choose shipping and payment providers](#choose-shipping-and-payment-providers)
- [Apply coupons and gift cards](#apply-coupons-and-gift-cards)
- [Order status and the basket lifecycle](#order-status-and-the-basket-lifecycle)
- [Hand the basket to checkout](#hand-the-basket-to-checkout)
- [Read a completed order safely](#read-a-completed-order-safely)
- [React to order changes](#react-to-order-changes)

## How Ekom finds the current basket

Read the current basket with `GetOrderAsync`:

```csharp
using Ekom.API;
using Ekom.Models;

IOrderInfo? basket = await orderApi.GetOrderAsync("Store", ct);
```

`GetOrderAsync()` without a store alias uses the store of the current storefront request, and returns `null` when there is none. Pass the store alias whenever you know it, and always outside a storefront request. If no store has that alias, `GetOrderAsync(storeAlias)` throws `StoreNotFoundException`.

Ekom finds the basket in one of two ways.

**Cookie.** By default the basket ID is a GUID in a cookie:

| Store setting | Cookie name |
| --- | --- |
| `ShareBasketBetweenStores` off (default) | `ekmOrder-{StoreAlias}`, one basket per store |
| `ShareBasketBetweenStores` on | `ekmOrder`, one basket shared by the stores that have this setting |

Ekom creates the cookie when it creates the basket, which happens when the first line is added. Reading the basket never creates one. The cookie is written for path `/`, with `SameSite=Lax`, `Secure` when the request is HTTPS, and `HttpOnly=false`. It expires after `Ekom:BasketCookieLifetime` days. The default is `1`.

**Member.** When the store's `UserBasket` setting is on and a member is signed in, Ekom uses the order ID saved in the member's `orderId` property instead of the cookie. When the order reaches a final status, Ekom clears that property.

Code that has to work in both modes uses the `Order` facade. Do not read the cookie or the member property yourself.

`GetOrderAsync` returns `null` when there is no basket, or when the order it finds has a final status (see [Order status and the basket lifecycle](#order-status-and-the-basket-lifecycle)). `WaitingForPayment` is not a final status, so the basket is still returned while the customer is at the payment provider. If the customer changes the basket then, Ekom releases that payment attempt and its stock holds. A payment that arrives later for the released attempt does not complete the changed basket. See [Checkout](checkout.md).

### Remove or restore the basket cookie

- `DeleteOrderCookie(storeAlias)` deletes the basket cookie. The order stays in the database.
- `EnsureOrderCookie(orderId, storeAlias)` writes the cookie only when it is missing, as the built-in payment return does. It never replaces a cookie that points to a different basket, so it cannot switch a customer to another order. It does nothing for member baskets.

Browser and headless clients must keep cookies for the whole flow. See [Headless storefronts](headless.md).

## Add order lines

Adding the first line creates an order with the legacy status `Incomplete` when there is no basket yet.

```csharp
IOrderInfo basket = await orderApi.AddOrderLineAsync(
    productKey,
    quantity: 2,
    storeAlias: "Store",
    settings: new AddOrderSettings
    {
        VariantKey = variantKey,
        OrderAction = OrderAction.AddOrUpdate,
    },
    ct);
```

Ekom finds the product and variant in the store, checks that the variant belongs to the product, checks that a variant was chosen when the product requires one, checks stock, recalculates totals and discounts, and saves the order.

`OrderAction` decides what happens when the basket already has a line with the same product and variant:

| Action | What it does |
| --- | --- |
| `AddOrUpdate` | The default. Adds the quantity to the matching line, or creates a line. A negative quantity lowers the line. If the line reaches exactly `0`, it is removed. A result below `0` throws `OrderLineNegativeException`. |
| `Set` | Sets the matching line to the quantity, or creates a line. The quantity must be greater than `0`. |
| `New` | Always creates a separate line, even for a product and variant that are already in the basket. |

A quantity of `0` throws `ArgumentException` for every action. To remove a line, use the remove methods below. A line that is the parent of [linked lines](linked-order-lines.md) is never matched, so adding the same product again creates or updates a separate line.

These are the errors you can expect, and the status code the HTTP endpoint returns for each:

| Exception | When | HTTP |
| --- | --- | --- |
| `ProductNotFoundException` | The product is not in the store. | 404 |
| `VariantNotFoundException` | The variant is not in the store. | 404 |
| `VariantRequiredException` | The product needs a variant and none was given. | 400 |
| `OrderLineNegativeException` | The line would go below zero. | 400 |
| `NotEnoughStockException` | There is not enough stock. | 409 |

`AddOrderSettings` can also carry `CustomData`, `Consent`, `Tracking` and `AlgoliaQueryId`. Only `CustomData` keys that start with `orderline` (for example `orderlineEngraving`) are saved on the line, in `OrderLineInfo.Properties`. Other keys are ignored.

### From the browser

`POST /ekom/order/add` does the same over HTTP. It accepts JSON or form data with `productId`, `variantId`, `storeAlias`, `quantity` (a decimal, default `1`), `action`, `consent`, `tracking`, `algoliaQueryId` and `linkedProducts`. Every other field becomes line custom data. The request body may be at most 64,000 bytes. Ekom HTML-encodes the custom data values from this endpoint before it saves them. See the [HTTP API reference](../reference/http-api.md) for all order endpoints.

## Change and remove lines

Use the line `Key` to change one line:

```csharp
basket = await orderApi.UpdateOrderlineQuantityAsync(
    lineKey,
    quantity: 3,
    storeAlias: "Store",
    ct: ct);

basket = await orderApi.RemoveOrderLineAsync(
    lineKey,
    "Store",
    ct: ct);
```

- `UpdateOrderlineQuantityAsync` sets the quantity and checks stock. A quantity of `0` or less removes the line.
- A line key that is not in the basket throws `OrderLineNotFoundException`. If there is no basket, the methods throw `OrderInfoNotFoundException`.
- Removing a line also removes the lines linked directly to it.

`RemoveOrderLineProductAsync(productKey, storeAlias, settings)` removes a line by product, and by variant when you set `RemoveOrderSettings.VariantKey`. It removes only the first matching line. A basket can hold several lines for the same product, so remove by line key when you can.

Over HTTP, `POST /ekom/order/removeorderline` and `POST /ekom/order/Updateorderlinequantity` take `lineId` and `storeAlias`. The quantity update also takes `quantity`, a whole number of `0` or more.

### Saving and concurrent changes

Each change to a basket loads the order, changes it and saves the whole order. Ekom does not compare the saved order with the version you loaded, so saving an old copy can overwrite newer changes, including the status. Reload the order before you change it, and make changes through the `Order` methods instead of keeping a copy in memory.

The methods that change only order information (`UpdateCustomDataAsync`, `RemoveCustomDataAsync` and `UpdateOrderLineMetadataAsync`) work differently. They save only when the stored order is still the version they read. Otherwise they throw `InvalidOperationException` and you retry.

## Refresh prices after a customer signs in

Prices can depend on who the customer is. When a guest signs in, call `ReInitializeOrder` to price the basket again for the new customer:

```csharp
basket = await orderApi.ReInitializeOrder("Store", ct: ct);
```

For each line, Ekom reads the product and variant from the catalog again and raises `AddingOrderline` and `AddingOrderlineAsync`, then the added and updated line events. Set `OrderSettings.FireEvents = false` to skip the events. Prices come from the product's `Prices` for the current customer and from any `OrderDynamicRequest.Prices` or `VariantPrices` that an adding-line handler supplies. Price overrides from the earlier basket are not used again.

What stays the same:

- line keys, chosen variants and quantities
- `OrderLineInfo.Properties`
- `OrderLineSettings.Link` and `CountToTotal`
- title, SKU and type metadata, unless a handler sets a new value
- separate lines for the same product and variant stay separate

The adding-line handler receives the line's existing metadata in `Settings.CustomData`, and its key, link and count settings in `Settings.OrderDynamicRequest`, without the old price overrides.

What it does not do:

- It does not check stock, remove lines that are no longer available, or lower quantities. Adding lines, changing quantities and checkout still check stock.
- It is not an add to basket. It does not write "line added" entries to the activity log.
- If a product or variant can no longer be found, the line keeps its old prices and Ekom logs a warning.
- An empty basket is returned unchanged. Without a basket, it throws `OrderInfoNotFoundException`.

Ekom prices a copy of the basket and saves it once, after every line has been priced. If a handler throws or the request is cancelled before the save, the basket stays as it was. A handler may return a replacement order, but it must keep the basket ID, the lines, the quantities, the metadata keys and the link and count settings. Ekom cannot undo work a handler does outside the order, such as calls to other systems. If an order-updated handler fails after the save, Ekom logs the error and the refresh still counts as saved. Always use the basket the method returns.

### Try it on the sample site

The Umbraco 17 sample site shows member prices:

1. Run `dotnet run --project Samples/U17/Ekom.Site.U17/Ekom.Site.U17.csproj --launch-profile Ekom.Site.U17` and open the front page.
2. In the Umbraco backoffice, create a member with a username and password.
3. Add products to the basket as a guest.
4. Sign in with the member login form on the front page, click **Reinitialise basket prices**, and look at the basket.
5. Sign out and click the button again to get guest prices back.

In the Development environment, the sample's product and variant factories give signed-in members 20% off, unless the existing sale price is lower. Products with discounts turned off keep their price. Include variant products and linked or custom-data lines, and refresh more than once to check that discounts do not add up. You can also set a product's stock to zero after adding it and check that the refresh keeps the line. The member prices and the refresh button exist only in Development.

## Linked lines

Linked lines are a parent product with add-ons that each have their own quantity, such as engraving or gift wrapping. Add the whole group in one call with `AddLinkedOrderLinesAsync`, or send `linkedProducts` to `POST /ekom/order/add`. Each child line stores the key of its parent line in `OrderLineSettings.Link`.

Children are ordinary lines: they count towards quantity, stock, discounts, tax and totals, and you can change or remove them on their own. Removing the parent also removes its children. Removing a child leaves the parent and the other children.

See [Linked order lines](linked-order-lines.md) for examples and rules.

## Save customer, consent and tracking details

```csharp
basket = await orderApi.UpdateCustomerInformationAsync(
    new Dictionary<string, string>
    {
        ["storeAlias"] = "Store",
        ["customerName"] = "Jane Doe",
        ["customerEmail"] = "jane@example.com",
        ["customerAddress"] = "Laugavegur 1",
        ["customerCity"] = "Reykjavik",
        ["customerZipCode"] = "101",
        ["shippingName"] = "Jane Doe",
    },
    ct: ct);
```

- The dictionary must contain `storeAlias`. Without it, Ekom throws `ArgumentException`.
- Keys that start with lowercase `customer` are saved on the customer. Keys that start with lowercase `shipping` are saved on the shipping address.
- A non-empty `customerEmail` must be a valid email address, or Ekom throws `InvalidCustomerEmailException` (HTTP 400).
- The customer name and email can each be at most 200 characters.
- The built-in payment step requires a customer name and email.

The HTTP endpoint is `POST /ekom/order/updatecustomer`, with JSON or form data. Requests to the public update endpoints are limited to 200 fields. Field names can be at most 100 characters and values at most 4,096 characters. An order can hold at most 500 customer fields and 500 shipping fields. A request over a limit returns 400.

`OrderSettings.Consent` and `OrderSettings.Tracking` can be sent with a line change or a customer update. `UpdateTrackingAsync` replaces the tracking data, and the consent when you pass it, on the current basket. See [Tracking and consent](tracking-and-consent.md).

When an order gets its first customer email, Ekom raises `CustomerEmailAddedAsync`. Tracking integrations use it to mark the start of checkout.

## Save your own values on the order

`OrderInfo.CustomData` holds your own text values for the whole order, such as a gift message or a purchase order number. In checkout forms, name a field `CustomData[GiftMessage]`. In JSON requests, send a `CustomData` object. Keys ignore upper and lower case, and an update adds or replaces the keys you send without removing the others. See [Order custom data](order-custom-data.md).

## Choose shipping and payment providers

Choosing a provider saves a copy of it on the order and recalculates the totals. It does not take a payment.

```csharp
basket = await orderApi.UpdateShippingInformationAsync(
    shippingProviderKey,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);

basket = await orderApi.UpdatePaymentInformationAsync(
    paymentProviderKey,
    "Store",
    new Dictionary<string, string>(),
    ct: ct);
```

- Before you accept a choice, get the providers that are available for the same store, destination country and order amount. See [Payment and shipping providers](providers.md).
- If Ekom cannot find the provider in the store, it returns the basket unchanged and does not throw.
- The values in the dictionary are saved with the chosen provider, and recognized customer fields in it update the customer. Treat every value that comes from the browser as untrusted.
- The HTTP endpoints are `POST /ekom/order/updateshippingprovider` and `POST /ekom/order/updatepaymentprovider`. Both need `storeAlias` and the provider GUID in `ShippingProvider` or `PaymentProvider`, or they return 400.

The currency also belongs to the store and the order. Read [Stores](stores.md) before you let customers switch the basket currency.

## Apply coupons and gift cards

### Coupons

```csharp
bool changed = await orderApi.ApplyCouponToOrderAsync("SPRING10", "Store", ct);

await orderApi.RemoveCouponFromOrderAsync("Store", settings: null, ct);
```

- Ekom converts the code to lower case before it looks it up.
- An unknown code throws `DiscountNotFoundException` (HTTP 404). A code with no uses left throws `DiscountHasNoUsageException` (HTTP 406). A code whose discount is not available in the store throws `DiscountUnableToFindCouponException` (HTTP 404).
- A valid coupon can still lose to a better discount. Then the method returns `false`, and `POST /ekom/order/coupon/apply` returns status `450`.
- Attaching a code does not use it up. Ekom marks the code as used when checkout completes.
- Ekom checks the remaining uses again when payment starts. If none are left, payment returns `409` with the code `couponUnavailable`.
- If an order still completes with a code that has no uses left, Ekom adds an alert to the order's activity log and raises an [order review](order-reviews.md).
- Uses are counted per code, not per customer or signed-in member.

See [Discount APIs and behavior](discounts/developers.md) for the full rules.

### Gift cards

```csharp
basket = await orderApi.AddGiftcardAsync(
    new Giftcard
    {
        Code = "GIFT-100",
        Amount = 100m,
        ValidUntil = DateTime.UtcNow.AddMonths(6),
    },
    "Store",
    ct: ct);

basket = await orderApi.RemoveGiftcardAsync("GIFT-100", "Store", ct: ct);
```

- The code must not be empty (`ArgumentException`) and the amount must be greater than zero (`ArgumentOutOfRangeException`).
- Adding a code that is already on the order throws `ArgumentException`. Codes are compared without regard to upper and lower case.
- Removing a code that is not on the order throws `ArgumentException`.
- `AddGiftcardAsync` trusts the `Giftcard` you pass. It does not check the code, the balance or a claim against any gift-card system. Only pass values your application has checked.

The public endpoint `POST /ekom/order/giftcards` does not trust the browser. It reads only the code and asks your `IGiftcardValidator` for the gift card to add:

```csharp
services.AddScoped<IGiftcardValidator, MyGiftcardValidator>();
```

`ValidateAsync(code, order, ct)` returns the `Giftcard` with the amount your gift-card system will honour, or `null` to reject the code. Without a registered validator the endpoint returns `403`. Without a basket it returns `404`. A rejected code, a code longer than 200 characters, or a gift card with an amount of zero or less returns `400`. `DELETE /ekom/order/giftcards/{code}?storeAlias=Store` removes a gift card.

### Grand total and charged amount

Gift cards change what the customer pays, not what the order is worth.

| Property | What it holds |
| --- | --- |
| `GrandTotal` | The order value after discounts, including VAT and shipping and payment fees, before gift cards. |
| `GrandTotalWithOutVat` | The same value without VAT. On `OrderInfo` only. |
| `ChargedAmount` | What is left to pay: `GrandTotal` minus the gift cards that apply, never below zero. |

A gift card applies when its amount is above zero and it is claimed, has no `ValidUntil`, or has not expired. An order worth 5,000 that is paid entirely with gift cards has `GrandTotal.Value == 5000` and `ChargedAmount.Value == 0`.

Use `ChargedAmount` for the amount to pay. The built-in checkout fills the payment request's `OrderItem.GrandTotal` from `ChargedAmount`. Do not change that to the order's `GrandTotal`. The `EkomOrders.TotalAmount` column, and the totals in the manager, hold `ChargedAmount`.

Orders saved by Ekom versions before this split can hold an old `GrandTotal` value in their stored JSON. `OrderInfo` calculates the properties again when it loads an order, but the stored JSON is not rewritten. Reports that read stored JSON directly cannot assume every saved `GrandTotal` has the current meaning.

Checkout compares the saved purchase JSON, including the calculated totals. Deploy the same Ekom version to every server, save open baskets through the normal update methods before payment, and do not re-save submitted purchases in bulk. A purchase saved again with different totals can cause a checkout conflict or a payment review, even when only the calculation changed. Keep the submitted copy and settle it through the payment attempt.

## Order status and the basket lifecycle

Each order has three statuses: order, payment and fulfillment. See [Order, payment, and fulfillment statuses](order-statuses.md). The basket methods on this page also use the older single status, `Ekom.Utilities.OrderStatus`, which Ekom keeps for compatibility. A typical order goes through these steps:

1. Adding the first line creates an order with status `Incomplete`.
2. Customer and provider updates add details and recalculate the order.
3. When the customer pays online, Ekom sets `WaitingForPayment` before it sends the customer to the provider.
4. Your payment integration confirms a successful payment on the server and starts completion.
5. Completion takes the stock, marks the coupon as used, writes the activity log, clears the member's basket reference, and sets `ReadyForDispatch`. A `CompleteCheckout` handler can turn off the status change by setting `UpdateOrderStatus` to `false`.
6. An offline payment provider sets `OfflinePayment` and completes at once. Completion keeps `OfflinePayment` and does not set `ReadyForDispatch`.
7. Later, fulfillment and the manager can use `ReadyForPickup`, `ReadyForDispatchWhenStockArrives`, `Dispatched`, `Returned`, `Cancelled` or `Closed`.

The enum also has `PaymentFailed`, `Pending` and `Wishlist`.

`Order.IsOrderFinal(status)` decides which statuses end a basket. These are final:

- `OfflinePayment`
- `Pending`
- `ReadyForDispatch`
- `ReadyForPickup`
- `ReadyForDispatchWhenStockArrives`
- `Dispatched`
- `Returned`
- `Cancelled`
- `Closed`

`Incomplete`, `WaitingForPayment`, `PaymentFailed` and `Wishlist` are not final. This rule only decides what `GetOrderAsync` returns as the current basket. It does not mean every non-final order is safe for the customer to edit.

`UpdateStatusAsync` changes the status and raises the status events. It does not complete the order. Call `CompleteOrderAsync(orderId)` only from a trusted payment or offline integration, after the payment result has been confirmed. Completion does stock, coupon, activity log and customer work that a status change does not. If the order does not exist, `CompleteOrderAsync` returns without doing anything.

## Hand the basket to checkout

When the basket has customer details and providers, send it to the payment pipeline. Do not change the status yourself.

```csharp
CheckoutResponse response = await orderApi.PayAsync(
    new PaymentRequest
    {
        PaymentProvider = paymentProviderKey,
        ShippingProvider = shippingProviderKey,
        StoreAlias = "Store",
        Culture = "en-US",
        ReturnUrl = "/checkout/receipt",
    },
    "Store",
    basket.UniqueId,
    ct);
```

Depending on the provider, the response holds provider HTML, a redirect, a validation error or a stock error. A customer returning to your site does not prove that they paid. Your payment integration must confirm the result with the provider and start completion on the server. See [Checkout](checkout.md).

## Read a completed order safely

Pick the read method by what you need:

| Method | Returns |
| --- | --- |
| `GetOrderAsync(storeAlias)` | The current basket, found by member or cookie. Never a final order. |
| `GetCompletedOrderAsync(storeAlias)` | The order the member or cookie points to, only when it is final. Use it for a receipt right after completion. Throws `ArgumentException` when the store alias is empty. |
| `GetOrderAsync(orderId)` | Any saved order with that GUID, including final orders. It does not check who the customer is. |

For a receipt page, take the completed order ID from the trusted checkout result and load the order on the server. Check that it belongs to the signed-in customer or the protected checkout session, and that its status fits a receipt, before you show customer details. Knowing an order GUID is not permission to see it.

`GET /ekom/order/{orderId}` has no authorization in Ekom. Protect it in your host or proxy, or do not expose it where order IDs and customer details must stay private. Do not use it to fill a basket page: it can return final orders, and those must not be changed.

`GET /ekom/order` and `GET /ekom/order/storeAlias/{storeAlias}` return the current basket, or `404` when there is none.

## React to order changes

`OrderEvents` lets you run code when an order changes. The async events are:

- `OrderUpdatingAsync` and `OrderUpdatedAsync`
- `CustomerEmailAddedAsync`
- `CustomerInformationUpdatingAsync` and `CustomerInformationUpdatedAsync`
- `OrderStatusChangedAsync`, `PaymentStatusChangedAsync` and `FulfillmentStatusChangedAsync`, raised each time the matching status changes. See [Events](../reference/events.md).
- `AddingOrderlineAsync`, `AddedOrderlineAsync`, `UpdatedOrderlineAsync` and `RemovedOrderlineAsync`
- `ShippingProviderAddedAsync` and `PaymentProviderAddedAsync`

`CheckoutEvents` has separate events for payment preparation, processing, payment items and completion. Handlers there can change how checkout works, including stock checks and status updates, so cover them with integration tests.

- `OrderSettings.FireEvents = false` turns off the events for one change. `FireOnOrderUpdatedEvent = false` turns off only the order-updated events.
- A status change made with `FireEvents = false`, or in the manager with **Trigger status events** off, raises no status change events.
- Handlers can run while Ekom holds the lock on the order. Do not change the same order from a handler unless you use `OrderSettings.IsEventHandler = true`, which lets the change through the lock.
- Payment callbacks can be retried, so make completion handlers safe to run twice.
- Remove static handlers when the application shuts down.

See the [Events reference](../reference/events.md) for the event arguments and which values a handler can change.

## Related pages

- [Checkout](checkout.md): take the basket through payment to a completed order.
- [Linked order lines](linked-order-lines.md): add a product together with its add-ons.
- [Order custom data](order-custom-data.md): save your own values on the order.
- [Order, payment, and fulfillment statuses](order-statuses.md): what each status means and how it changes.
- [Headless storefronts](headless.md): use these features over HTTP from a separate front end.
- [.NET API reference](../reference/dotnet-api.md): every method on the `Order` facade.

[Back to Guides](README.md)
