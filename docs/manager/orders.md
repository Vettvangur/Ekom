[Ekom docs](../README.md) / [Manager](README.md) / Orders

# Orders

The **Orders** view in Ekom Manager is where you find orders, export them, and keep them up to date: change payment and fulfillment status, correct customer details, add or remove lines, and recover a checkout that did not finish. You see only the stores you have access to.

**Who this page is for:** staff who work with orders. The last part, [Add your own order actions](#add-your-own-order-actions), is for developers.

On this page:

- [Find orders](#find-orders)
- [Export orders to CSV](#export-orders-to-csv)
- [Open an order](#open-an-order)
- [Change order, payment and fulfillment status](#change-order-payment-and-fulfillment-status)
- [Correct customer details, lines and shipping](#correct-customer-details-lines-and-shipping)
- [Release reservations](#release-reservations)
- [Checkout lock and manual recovery](#checkout-lock-and-manual-recovery)
- [Failed gift-card preparation](#failed-gift-card-preparation)
- [Add your own order actions](#add-your-own-order-actions)
- [If you get a "forbidden" message](#if-you-get-a-forbidden-message)

For what each order, payment and fulfillment status means, see [Order, payment, and fulfillment statuses](../guides/order-statuses.md).

## Find orders

### Choose a view

The buttons above the list are views. Each shows the number of orders it holds for the current store, currency and date range.

| View | Shows |
| --- | --- |
| **All orders** | Open, Closed and Cancelled orders. Drafts are left out. |
| **Needs review** | Every order with an open review, drafts included. The button is shown only when at least one order needs review. See [Order reviews](../guides/order-reviews.md). |
| **Unfulfilled** | Open orders whose fulfillment is Unfulfilled, Picking, Picked, Packed or Partially Fulfilled. |
| **Unpaid** | Open orders whose payment is Pending, Authorized, Partially Paid or Failed. |
| **Open** | Open orders. |
| **Closed** | Closed orders. |
| **Drafts** | Checkouts that were never finished. No other view lists them. |

The view opens on **All orders**.

### Read the list

Above the list, **Orders**, **Order amount** and **Average** show the number of orders, their total and the average order total for the current view and filters.

The list shows 20 orders per page, newest order number first. Each row shows the order number, date, customer, total, payment status and fulfillment status. On Open orders you can change the payment and fulfillment status straight from the row. See [Change order, payment and fulfillment status](#change-order-payment-and-fulfillment-status).

Use the arrows below the list, or the Left and Right arrow keys, to move between pages. The keys do nothing while a field or a menu has focus.

If you have no access to any store, the list stays empty.

### Search

Type in **Search orders** to search the current view. The text is found anywhere in:

- the customer's name, email, customer ID or username,
- the order number or reference number.

The whole search text is also compared exactly with:

- the transaction ID a payment gateway gave any of the order's payments, so you can paste a reference from the gateway's portal,
- a shipment's tracking number or the carrier's shipment ID.

If the search text is an order ID (a GUID), Ekom also finds the order with that ID, and the order that has a payment attempt with that payment ID.

The date range and the other filters still apply while you search.

### Filter

Click **Filter** to narrow the list further. The number on the button shows how many filters are set. Click **Apply** to use them.

**Status.** Choose an order, payment and fulfillment status. Order status starts on **Open, closed and cancelled**. Choose **All** to include drafts, or **Draft** to see only drafts. Payment and fulfillment start on **Any**. The status filters also apply to Analytics and Customers.

**Payment & shipping.**

- **Payment provider** and **Shipping provider** list the providers of the selected store. They match the provider the order was saved with, not its current name. When you change the store, both lists reload and both choices are cleared.
- **Gift card number** finds orders that used that gift card. Type the full number. Leading zeros count. An order is listed once even if the card was reserved and redeemed. The filter does not match a reservation reference or a transaction ID.

**Order contents.**

- **Product SKU** finds orders with a line for that exact SKU.
- **Coupon code** finds orders with that exact coupon code on the order. Coupons on single order lines are not matched.

**Tracking.** Source, medium, campaign, term, content and click ID, as captured when the order was placed.

All text filters are exact matches, not partial matches. They ignore upper and lower case and spaces at the start and end.

The filters change the list, the totals above it and both CSV exports. They do not change the Analytics charts or most sold products. Analytics uses only the store, currency, date range and status filters.

### Dates

The date range includes both the first and the last day, and is matched against the date the order was created. To find accepted orders that are not paid yet, use the **Unpaid** view or the payment filter with a suitable date range.

### Keep the list up to date

The **Polling** menu reloads the list every 2, 5, 10, 20 or 30 seconds. It is **off** when you open the view. Polling pauses while an order is open, a menu is open, the browser tab is hidden, or you are working in the list, so it does not move things under your cursor. When nothing has changed, the list is not reloaded.

## Export orders to CSV

Click **Export**, then **Export** again in the dialog. The export uses the current view, search, date range, store, currency and every filter. It contains every matching order, not only the page on screen. The file is UTF-8 CSV with a byte order mark, so spreadsheet programs read special characters correctly.

- Without options you get `orders.csv`, with one row per order.
- With **Include order lines** you get `orders-with-orderlines.csv`: a row for each order, followed by a row for each of its lines. Line rows add the line key, product and variant SKU and title, quantity, unit price, VAT, discount and line total. This export reads the full saved order and can take longer for many orders.

Both files end with payment columns for bookkeeping:

| Column | Content |
| --- | --- |
| `paymentStatus` | The order's payment status. |
| `paymentProvider` | The payment provider node. |
| `paymentProviderType` | The provider type, for example `borgun` or `offline`. |
| `paymentTransactionId` | The gateway's transaction ID. |
| `paymentAuthorizationCode` | The authorization code. |
| `paymentCardType` | The card type. |
| `paymentCardNumber` | The masked card number. |
| `paymentMethod` | The payment method. |
| `paymentCapturedDate` | When the payment was captured, in UTC. |

They describe the payment the order was paid with. Apart from the status, they are empty when no payment is recorded on the order. They come after the other columns, so the position of the earlier columns does not change.

## Open an order

Click an order number, or the arrow at the end of its row. The order opens in a panel over the list.

The top of the panel shows the order number, its payment, fulfillment and order status, the customer, date, store and the order's unique ID with a copy button. The arrows at the top left, or the arrow keys, move to the previous or next order on the current page.

Below the status menus are the **Print order** button and any extra actions for the order, then three tabs:

- **Order**: lines, payment, shipment, notes and customer.
- **Activity log**: the order's history. See [Activity logs](activity-logs.md).
- **Tracking & consent**: the marketing tracking values and the consent choices captured when the order was placed, or a note that none were captured.

If the order has an open review, its alert is shown above the tabs with a **Review** button. See [Order reviews](../guides/order-reviews.md).

### Order lines

The lines table shows each product and variant with its SKU, line properties, quantity, unit price, VAT, discount and total. See [Change order lines](#change-order-lines) to add or remove a line.

### Payment

The payment card shows **Subtotal**, **Discount**, **Shipping**, **Tax** and **Total**, and **Gift cards** when gift cards with value are on the order. The gift-card amount is already included in the total. When the order has a coupon code, it is shown beside **Discount**. The discount amount is the order's whole discount, which can include other reductions, so it does not mean that every saving came from that coupon. Coupons on single lines are not shown there.

Below the totals:

- The order's current payment: provider, status, transaction ID, when it was authorized and captured, amount, authorization code, card, payment method and the details the provider recorded. Most values have a copy button.
- A box for each gift card on the order. See [Release reservations](#release-reservations).
- **Payment attempts**, closed by default: every attempt at a payment provider, with its events, transaction ID, payment ID and checkout attempt ID, and every manual payment status change with the name of the user who made it.

### Shipment

The shipment card shows the shipment's state and **Tracking number**, with a copy button and a **Track at** link to the carrier when a tracking link is recorded. It also shows the carrier, service, pickup point, carrier status, and the shipped, estimated and delivered dates when the carrier recorded them. **Method** shows the shipping provider, its price and the delivery address, and **Provider data** lists the details stored on the order for the shipping provider.

**Shipment history**, closed by default, lists every event of the shipment, who caused it, and every earlier shipment of the order, for example one that was deleted and replaced.

### Notes and customer

**Notes** shows the order's custom data, when it has any. See [Order custom data](../guides/order-custom-data.md).

The customer card shows the customer's name, email, phone, shipping address and billing address, including any extra fields captured at checkout. **Open customer** opens the customer in [Customers](customers.md).

### Print an order

Click the printer button above the tabs to print the order. The printout has the order number, statuses, unique ID, created and paid dates, store, amount, notes, billing and shipping details, payment and shipping method, gift cards and lines. Its totals show the coupon code next to the discount, for example **Discount (SPRING10)**.

## Change order, payment and fulfillment status

You can change payment and fulfillment only on **Open** orders. To correct a Closed order, reopen it first.

**From the list.** Click the payment or fulfillment badge on a row and choose a status. The change is saved at once.

**From an open order.** The **Order**, **Payment** and **Fulfillment** menus at the top save as soon as you choose an option. There is no Save button.

The **Order** menu has:

- **Close order**: only when payment is Paid and every item is fulfilled.
- **Cancel order**: asks you to confirm. Cancelling does not refund a payment or undo fulfillment.
- **Reopen order**, on a Closed order: the order becomes Open again with its payment and fulfillment unchanged, and it is not closed automatically again.

Drafts become orders through checkout, not from this menu. The menu cannot turn a Draft or a Cancelled order into an Open order.

In an open order, the status menus are disabled while the order's payment or fulfillment history needs review, while its checkout is locked, and when the Manager could not load its configuration. In the list, the payment and fulfillment badges cannot be changed while the history needs review.

### Set the payment status by hand

The **Payment** menu offers **Pending**, **Authorized**, **Paid**, **Partially Paid**, **Failed** and **Voided**. Choosing one only changes the recorded status. It does not charge, capture, void or refund anything at the payment provider, does not change amounts, and does not set a paid date. Refunded cannot be set by hand.

The change is added to the order's payment history with your name. It overrides what the providers recorded before it, and a late failure from an older payment attempt does not undo it. A later capture, refund or void from a provider changes the status again.

If the order is already fulfilled and you set it to **Paid**, it can close automatically, depending on the store's automatic closing setting.

### Trigger status events

Each status menu has a **Trigger status events** switch. It decides whether Ekom raises its status-change events for this change, so that code the site has set up (for example, an email or a warehouse message) runs. The change is saved and written to the activity log either way. The three menus in an open order share one switch.

By default the switch is on in the list and off in an open order. A developer can set one default for both with `Ekom:Manager:OrderStatusFireEventsByDefault`. See [Manager order-status events](../reference/configuration-reference.md#manager-order-status-events) and the [status guide](../guides/order-statuses.md#change-statuses-in-the-manager).

Changing a status by hand never starts a payment provider operation or fulfillment work.

### When a status change is blocked

If someone else changed the order while you had it open, the change is refused. Reload the order and try again.

If the order's checkout is locked, the status change is refused with a message instead of an error page, and the saved status is shown again. Changing the status does not release the lock.

- If the message says checkout is locked: open the order, confirm with the payment provider that no payment is being processed, then use **Release Lock** (see [Checkout lock and manual recovery](#checkout-lock-and-manual-recovery)). Read its warnings, then try the status change again.
- If the message says the payment needs review or checkout has completed: check the payment and the order first. Do not release the lock or retry without checking.

For developers: the status endpoint returns HTTP `409 Conflict` with the message in this case.

## Correct customer details, lines and shipping

### Edit customer information

Click **Edit** next to **Contact information**. You can change the billing and shipping fields and any extra fields captured at checkout. Click **Save customer information**. Ekom saves the change through its normal customer-information API, so the same checks apply as in checkout. If a value is not valid, nothing is saved and the error is shown.

### Change order lines

Click **Add order line** and enter:

- **Product ID**: the product's key (a GUID),
- **Variant ID**: the variant's key, if the product has variants,
- **Quantity**: a number above zero.

To remove a line, click the cross at the end of it and confirm.

Adding and removing use Ekom's normal order methods with events turned on. The catalog, stock rules and order validation apply as they do on the storefront. The Manager does not bypass them.

### Change the shipping provider

Click **Change shipping provider** (or **Add shipping provider** when the order has none) under **Method**. Choose a provider that is available for the store. Under **Provider data** you can edit the values stored on the order for that provider. Existing keys cannot be renamed, and new keys are saved with the prefix `customshipping`.

### Set or correct the tracking number

Click the pencil next to the tracking number. Enter the **Tracking number** and, if you have one, a **Tracking link**, which must start with `http://` or `https://`. The change is added to the shipment history and the activity log with your name. The pencil is shown only when the order has a shipping provider.

## Release reservations

While a customer pays, Ekom can hold stock and gift-card amounts for the order. These holds are called reservations. Releasing them gives the stock and the gift-card amount back. Releasing is **not a refund** and does not cancel a pending payment at the payment provider. Confirm that the payment has not been collected before you release anything.

### Gift-card boxes

Each gift card on the order has its own box with its number, amount and status, its release button and, when release is not possible, the reason. **More details** shows the reservation reference, dates, transaction reference and the other saved fields. A missing value is shown as **Not recorded**.

| Status | Meaning |
| --- | --- |
| Selected | The card is on the order. No reservation reference or expiry is saved. |
| Reserved | A reservation reference is saved and its expiry is in the future. |
| Reservation expired | A reservation reference is saved and its expiry has passed. This does not confirm that the provider released the hold. |
| Needs checking | An expiry is saved without a reservation reference, or a reference without an expiry. Check the gift-card provider if an amount may still be held. |
| Used | The card has been redeemed. |

The status comes from what Ekom saved on the order. It is not a live balance check at the provider. When a card has a reservation reference and is not used, the date is labelled **Reservation ends**. Otherwise it is labelled **Saved expiry**, because the date alone does not show that the provider holds an amount.

### Release actions

Both actions ask you to confirm and need the same store access as viewing the order.

- **Release gift card**, in each gift-card box, releases only that card's unredeemed hold at the provider, through the registered `ICheckoutGiftcardReservations` integration. Other gift cards, stock reservations and the order status are kept. A card that belongs to a submitted payment attempt cannot be released on its own: use **Release checkout reservations** for that attempt.
- **Release checkout reservations**, with the order's actions, closes the unpaid payment attempt, releases its stock and gift-card holds, clears the saved reservation details and returns the order to **Incomplete**. The order and its lines are kept.

Released gift cards stay on the order with their number, amount and date.

Release is refused when:

- the order is paid or completed, or its payment is being completed,
- the payment needs reconciliation,
- the gift card is redeemed or settled,
- no gift-card reservation integration is registered. The buttons are then disabled, so Ekom never clears its own record without releasing the hold.

The integration must confirm the release at the provider, and releasing the same hold twice must be safe. If the provider fails, Ekom keeps the reservation details so you can investigate or try again.

When you confirm, Ekom reads the saved order again and takes normal checkout ownership. If a checkout or preparation step is running, the release is refused; these buttons never clear a lock left behind by a stopped process. If the order or its payment attempt changed after you opened it, refresh and confirm again. Repeating a release that already finished does nothing. The activity log records who asked for the release and the outcome, without gift-card numbers. The order reloads after a successful release.

A payment callback that arrives after a release needs reconciliation. It does not complete the order as usual.

## Checkout lock and manual recovery

While a checkout is preparing a payment or waiting for one, Ekom locks it so the customer cannot pay twice or change the basket. If the checkout stops before it finishes, the lock stays and the customer cannot pay or edit the basket.

When an order has such a lock, or its payment needs reconciliation, a **Checkout is locked** panel is shown in the open order. It explains in plain words what this means for the customer. **Technical details**, closed by default, shows the attempt ID, the attempt's state and which lock is held. If Ekom could not check the lock, the panel says so, instead of suggesting that the order is unlocked.

### Release the lock

**Release Lock** is a manual override for this situation. It uses your normal store access; it does not need an administrator.

1. Confirm with the payment provider that no payment is being processed, and that the earlier checkout has stopped. An **Incomplete** order can still be locked, and neither the status nor the age of the lock shows that it is safe to release.
2. Click **Release Lock** and confirm.
3. Read the result and any warnings.

Release Lock:

- replaces the payment or preparation lock with a recovery operation,
- closes the old payment attempt and releases its stock reservations,
- clears the saved gift-card reservation details and the reconciliation block,
- returns the order to **Incomplete**.

The order lines, customer details and selected gift cards (number, amount and date) are kept. A paid or completed order, and stock that was already used, are not undone. Release Lock is not a refund and does not return stock. If the order changes before the release runs, refresh and confirm again, so that you do not reset a newer checkout.

**Release Lock is blocked while there is failed gift-card preparation evidence that is pending or not resolved.** Use [Failed gift-card preparation](#failed-gift-card-preparation) instead. Release Lock cannot skip it.

For older checkouts without failed-preparation records, Ekom sends known unredeemed gift-card holds to the registered integration for release. Unlike the normal release actions, a missing integration or an unconfirmed release at the provider does not stop this reset. The result then warns that you must check the gift-card holds yourself, and does not claim that the provider released them. Ekom saves a recovery snapshot of the order and its holds before the reset. The activity log records who released the lock, the recovery reference and the outcome. A database or stock failure is reported as a failure, not as a successful reset.

### What Release Lock cannot know

A lock has no expiry time and no heartbeat, so the panel cannot tell whether the old checkout is still running or has stopped. Release Lock trusts your confirmation. It cannot stop a request already sent to a payment or gift-card provider, and it cannot cancel or refund a payment at the gateway. A successful payment callback that arrives after the reset needs reconciliation, and does not complete the reset basket.

## Failed gift-card preparation

When checkout reserves gift cards and that step fails, Ekom keeps a record of what happened. The **Failed gift-card preparation** section in the open order shows these records separately from gift cards that were accepted as payment, even when the order no longer contains those cards. Each record keeps the original attempt reference, masked card and claim references, the requested and returned amounts, the expiry and the cleanup status. Resolved records stay visible as history.

When the gift-card integration supports it:

- **Release failed-preparation hold** releases exactly one hold that the failed step is known to have created. It never refunds anything or releases other holds. It is only offered when a claim reference is saved.
- **Reconcile failed preparation** asks the provider, without changing anything there, what happened to the original request, including effects without a claim reference.

Each action asks you to confirm, checks the record and the order's payment and lock state again, and records who ran it and the outcome. Unknown effects and partial cleanup keep checkout blocked. Paid, submitted, settled or completed outcomes are not unlocked automatically.

See [Failed gift-card preparation recovery](../guides/giftcard-recovery.md) for what an integration must support, the limits of crash recovery, and how to handle older incidents without records.

## Add your own order actions

**For developers.** You can add buttons to the open order, for example to create a shipping label. Implement `IOrderManagerActionProvider` and register it.

```csharp
using Ekom.Models;
using Ekom.Models.Manager;
using Ekom.Services;

public sealed class ShippingLabelActionProvider(ShippingLabelClient labels) : IOrderManagerActionProvider
{
    private const string Key = "acme.shipping-label";

    public Task<IReadOnlyCollection<OrderManagerAction>> GetActionsAsync(
        IOrderInfo orderInfo, CancellationToken ct = default)
    {
        IReadOnlyCollection<OrderManagerAction> actions =
        [
            new OrderManagerAction
            {
                Key = Key,
                Label = "Create shipping label",
                ConfirmMessage = "Create a shipping label for this order?",
                SortOrder = 100,
            },
        ];
        return Task.FromResult(actions);
    }

    public async Task<OrderManagerActionExecutionResult?> ExecuteAsync(
        IOrderInfo orderInfo, string actionKey, string? userName = null, CancellationToken ct = default)
    {
        if (actionKey != Key)
        {
            return null;
        }

        byte[] pdf = await labels.CreateAsync(orderInfo.OrderNumber, ct);

        return new OrderManagerActionFileResult
        {
            Content = pdf,
            ContentType = "application/pdf",
            FileName = $"label-{orderInfo.OrderNumber}.pdf",
        };
    }
}
```

```csharp
builder.Services.AddTransient<IOrderManagerActionProvider, ShippingLabelActionProvider>();
```

`ShippingLabelClient` stands for your own carrier client.

### How actions are listed

Ekom collects the actions of every registered provider for the order:

- An action with a blank `Key` is ignored.
- Keys are compared without regard to upper and lower case. When two actions have the same key, the first one is kept and a warning is logged.
- Actions are sorted by `SortOrder`, then by `Label`.

`OrderManagerAction` also has `Enabled` and `DisabledReason` (a disabled button with the reason under it), `Look` (`"primary"` for a filled button, otherwise an outline button) and `ConfirmMessage` (the user must confirm first).

### How an action runs

When the user clicks the button, Ekom calls `ExecuteAsync` on each provider in turn until one returns a result. Return `null` for keys that are not yours. The user name of the backoffice user is passed as `userName`. Return one of:

| Result | What the user gets |
| --- | --- |
| `OrderManagerActionSuccessResult` | Its `Message`, shown as a success. |
| `OrderManagerActionBadRequestResult` | Its `Message`, shown as an error. |
| `OrderManagerActionFileResult` | A file download with `Content`, `ContentType` and `FileName`. |

Listing and running an action both check that the user may work with the order's store. If no provider handles the key, the request fails with "Unknown order action". An exception in your provider is logged and the user gets a generic error.

### React after an action

After a successful or file result, Ekom raises `OrderManagerEvents.ActionExecutedAsync` with the order, action key, user name and result. It is not raised for a bad-request result. Subscribe once at startup and filter by action key.

```csharp
using Ekom.Events;

OrderManagerEvents.ActionExecutedAsync += async (sender, args, ct) =>
{
    if (args.ActionKey != "acme.shipping-label")
    {
        return;
    }

    await auditLog.WriteAsync(args.OrderInfo.OrderNumber, args.UserName, ct);
};
```

`auditLog` stands for your own service. If a handler throws, the user gets an error even though the action itself has already run, so make handlers safe to run again.

## If you get a "forbidden" message

If you can open the Manager but get a "forbidden" message for a store or an order, your user group does not have access to that store. Ask the site's administrator to check [Access and permissions](access-and-permissions.md). A store permission list that is not empty denies every store that is not on it.

## Related pages

- [Order, payment, and fulfillment statuses](../guides/order-statuses.md): what each status means and how it changes.
- [Activity logs](activity-logs.md): the order's history and how to add to it.
- [Customers](customers.md): one customer's orders across a period.
- [Stock reservations](../guides/reservations.md): how stock is held during checkout.
- [Failed gift-card preparation recovery](../guides/giftcard-recovery.md): what the gift-card integration must support.
- [Events](../reference/events.md): status-change events and `OrderManagerEvents`.

[Back to Manager](README.md)
