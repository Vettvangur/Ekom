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
- exact product SKU;
- tracking source, medium, campaign, term, content, and click ID.

Product SKU and tracking-value comparisons are case-insensitive. They are exact comparisons rather than partial-text searches. Changing the store reloads its payment providers and clears a provider selection that is no longer valid.

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

## Custom order actions

Applications can implement and register `IOrderManagerActionProvider` to expose order-specific actions.

Ekom combines actions from all registered providers. It ignores actions with blank keys, treats keys case-insensitively, keeps the first action when keys are duplicated, and sorts actions by `SortOrder` and then label.

An action can be disabled, request confirmation, and return either a message, a bad-request message, or a downloadable file. Listing and execution are both authorized against the order's store. The current backoffice identity name is passed to the executing provider.

## Access failures

If a user can open the manager but receives a forbidden response for a store or order, review [Access and permissions](access-and-permissions.md). In particular, a non-empty store permission map denies every omitted store.
