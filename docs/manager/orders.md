# Orders

The Ekom Manager Orders view searches, exports, and updates orders for stores available to the current backoffice user. Ekom enforces store authorization on the server, so changing a store or order ID in a browser request does not grant access.

## Default view

The initial date range runs from January 1 of the current year through today. The initial status is **Completed Orders**, which includes:

- `ReadyForDispatch`
- `OfflinePayment`
- `ReadyForDispatchWhenStockArrives`
- `Dispatched`
- `Closed`
- `ReadyForPickup`

The first allowed store is selected. If the user has no allowed stores, the list remains empty.

The summary cards show the matching order count, payment total, and average order amount. Results are ordered by descending reference ID and paged.

## Dates and statuses

Date inputs are inclusive of both selected days.

When the selected status is `ReadyForDispatch` or `Dispatched`, search and reporting filter on the paid date. Other selections, including **Completed Orders** and **All Orders**, filter on the creation date.

## Search and filters

Free-text search matches:

- customer name;
- reference ID;
- order number;
- customer email;
- customer ID;
- customer username.

If the search text is a GUID, Ekom also compares it with the order's unique ID.

The additional filter panel supports:

- payment provider for the selected store;
- shipping provider for the selected store (Umbraco 17 and 18);
- exact order-level coupon code (Umbraco 17 and 18);
- exact gift-card number;
- exact product SKU;
- tracking source, medium, campaign, term, content, and click ID.

Product SKU, coupon code, and tracking-value comparisons are case-insensitive. They are exact comparisons rather than partial-text searches; leading/trailing whitespace in filter input is ignored. Coupon filtering checks only the saved order-level code, not coupons on individual order lines. Shipping-provider filtering matches the saved provider key, not a title that may have changed.

Gift-card filtering matches the full saved `Giftcards[].Code`, not a reservation claim ID or transaction ID. Input whitespace is trimmed, leading zeroes are preserved, and comparisons are case-insensitive. Any matching gift card includes the order once, regardless of reservation or redemption state. To find abandoned or unpaid orders, select **All Orders** or the relevant unpaid status and an appropriate date range. The filter applies to order results, summary cards, and both CSV export formats; it does not change analytics charts.

In Umbraco 17 and 18, changing the store reloads payment and shipping filter options and clears both provider selections. An empty coupon field or the **All shipping providers** option adds no restriction. These filters combine with the other selected filters and affect the order list, count/payment-summary cards, and both CSV export formats. Analytics charts and most-sold-product reports retain their existing date/status/store filtering.

## Export

Export uses the active date, status, store, search, provider, SKU, and tracking filters. It exports all matching orders rather than only the visible page and returns a UTF-8 CSV with a byte-order mark.

- The standard `orders.csv` contains one row per order.
- Selecting **Include order lines** produces `orders-with-orderlines.csv`, with an order row followed by a row for each line.

The line export adds the line key, product and variant SKU/title, quantity, unit price, VAT, discount, and line total. Including lines requires loading the stored order information and can take longer for a large result set.

## Order details

Select **View** to open an order. The detail view includes:

- unique ID, reference number, creation and paid dates, store, status, and charged amount;
- billing and shipping information, including captured custom properties;
- payment and shipping provider information and custom data;
- product and variant lines, line properties, VAT, discounts, shipping, and totals;
- captured tracking and consent data;
- [activity logs](activity-logs.md);
- actions supplied by registered manager-action providers.

The detail view can be printed with its **Print** action.

In Umbraco 17 and 18, the Discount total includes the saved order-level coupon code when present, for example **Discount (SPRING10)**. Without a code, it remains **Discount**. The amount is the existing total discount, which can include other reductions; the label does not mean that every saving came from that coupon. Separate line-level coupon codes are not included in this total label.

## Status changes

Status can be changed from the list or the detail view.

- A list-row status change fires events.
- In the detail view, **Fire events?** controls the `FireEvents` setting for that update.
- Ekom passes the current backoffice identity name to the status-update flow.

Status updates use Ekom's normal order API and can produce activity entries or invoke application event handlers. Confirm the effects of a target status before changing a production order.

## Customer information

**Edit customer information** edits standard billing and shipping fields plus captured custom customer and shipping properties. Ekom validates and updates the order through its normal customer-information API. A validation failure is returned without a successful update.

## Order lines

The detail view can add a line with:

- a product GUID;
- an optional variant GUID;
- a positive quantity.

It can also remove an existing line after confirmation. Add and remove operations use Ekom's regular order APIs with events enabled, so catalog lookup, stock rules, and order validation still apply. The manager does not bypass them.

## Release reservations

Reservation release requires confirmation and the same store access as viewing the order:

- **Release gift card**, beside each gift-card item, releases only that card's unredeemed external hold through the registered `ICheckoutGiftcardReservations` integration. Other gift cards, stock reservations, and order status are kept. A card committed to a submitted payment attempt cannot be released individually: close the unpaid checkout attempt first using the separate order action. The per-card button never escalates to releasing all reservations. Unavailable buttons show the reason, including missing claim metadata, settlement, payment state, or a missing integration.
- **Release checkout reservations** closes the unpaid payment attempt, releases its stock and gift-card holds, clears reservation metadata, and returns the order to **Incomplete**. The order and its lines are retained.

Released gift cards remain selected with their code, amount, and validity date. These actions are **not refunds** and do not cancel a pending payment at an external payment provider. Confirm that payment has not been collected before proceeding. A late successful callback after release requires payment reconciliation rather than ordinary order completion.

Release is refused for paid/completed orders, completing payments, reconciliation-required orders, or redeemed/settled gift cards. Gift-card releases require a registered integration; without one, applicable release buttons are disabled rather than merely clearing Ekom's metadata. The integration must confirm successful, idempotent external release; a provider failure leaves claim metadata intact for investigation or retry.

Execution re-reads persisted state and acquires normal checkout ownership, even though other Manager actions may use a manager override. A running checkout/preparation operation blocks release; these buttons do not forcibly clear an orphaned owner lock. A changed order or newer payment attempt invalidates the loaded confirmation: refresh and confirm again. Repeating an already completed release is safe. The activity log records the requesting backoffice user and the outcome without logging gift-card codes. The order view refreshes after successful execution.

### Checkout lock status

When an ownership or reconciliation block exists, a **Checkout lock** panel appears below the Payment amount and above Order actions. It identifies payment-operation ownership, checkout-preparation ownership, and payment reconciliation independently, with the linked payment attempt ID and state when available. **Refresh status** re-reads the persisted state; a load failure is shown explicitly rather than implying that the order is unlocked.

An **Incomplete** order can still have retained ownership. Ownership has no automatic expiry, timestamp, or heartbeat, so the panel cannot establish whether an operation is currently running, when it acquired ownership, or whether its owner is orphaned. It does not expose the ownership capability or offer force-unlock. Interrupted operations require provider/database reconciliation and confirmation that the old writer cannot resume before administrative recovery; order status and lock age alone are insufficient.

## Custom order actions

Applications can implement and register `IOrderManagerActionProvider` to expose order-specific actions.

Ekom combines actions from all registered providers. It ignores actions with blank keys, treats keys case-insensitively, keeps the first action when keys are duplicated, and sorts actions by `SortOrder` and then label.

An action can be disabled, request confirmation, and return either a message, a bad-request message, or a downloadable file. Listing and execution are both authorized against the order's store. The current backoffice identity name is passed to the executing provider.

After a successful action, `OrderManagerEvents.ActionExecutedAsync` runs with the order, action key, current backoffice user name, and execution result. Subscribe once at application startup and filter by action key to run follow-up work for a shipping-label action. Handler failures fail the action response, so handlers should be idempotent.

## Access failures

If a user can open the manager but receives a forbidden response for a store or order, review [Access and permissions](access-and-permissions.md). In particular, a non-empty store permission map denies every omitted store.
