# Customers

Customers appears between Orders and Analytics in Umbraco 13, 17 and 18. It is a read-only view of order history, not a separate customer database.

## Shared filters

Store and date range are shared with Orders and Analytics in both directions. The inherited order-status filter selects customers with a qualifying order in the period. Dates are inclusive, using PaidDate for ReadyForDispatch and Dispatched, and CreateDate otherwise, matching the other manager views. Other Orders filters (product, coupon, provider and tracking) remain preserved in Orders but do not filter Customers.

The selected period determines which customers appear. Lifetime metrics and New/Returning classification use their full qualifying history within the selected store. The detail drawer includes all-time order history, with a separate toggle for non-qualifying orders. No customer data crosses store boundaries.

## Identity configuration

```json
{
  "Ekom": {
    "Manager": {
      "Customers": {
        "IdentityField": "CustomerEmail"
      }
    }
  }
}
```

Use CustomerEmail (default) or CustomerUsername. Other values fail startup validation. Identifiers are trimmed and compared case-insensitively. Username mode never falls back to email. Changing this configuration changes grouping without rewriting orders. The missing-identifier indicator counts qualifying orders in the selected store and period with no configured identifier; those orders cannot be assigned to a customer.

## Metrics

Qualifying orders use the existing manager CompletedOrders statuses: ReadyForDispatch, OfflinePayment, ReadyForDispatchWhenStockArrives, Dispatched, Closed and ReadyForPickup. These are accepted orders, not necessarily verified payments. A new customer has one qualifying order; a returning customer has two or more.

- First order is the earliest qualifying CreateDate, not an account registration date.
- Lifetime spend sums TotalAmount (the recorded ChargedAmount). It is not refund-adjusted net revenue.
- Average order divides spend by qualifying orders, separately for each saved currency.
- Days since last order uses the latest qualifying CreateDate and the server's calendar date, clamped to zero for future dates.
- Average reorder interval averages consecutive order gaps. Equivalently, it is (last order - first order) / (order count - 1). Single-order customers display a dash.
- The summary interval averages customer-level intervals among returning customers.
- Currency totals are kept separate. Average lifetime spend for each currency uses the number of customers who ordered in that currency.

Search matches recorded names, emails and usernames. Sort by name, first order, latest order or order count. CSV exports use the same cohort filters and produce one row per customer/currency, with spreadsheet formula escaping.

## Detail and data access

List queries, summary cards, search and CSV export explicitly select EkomOrders columns and never read OrderInfo. The detail endpoint reads the latest qualifying order's OrderInfo only after enforcing store access and matching customer identity. It exposes an allowlisted snapshot of phone, SSN (customerSSN), addresses, payment/shipping method titles and marketing consent. Missing values are omitted; malformed JSON does not prevent viewing order history.

The snapshot is labelled with its source order and date. It is historical information, not a current profile or consent record. Click an order to open the existing manager order view for line items and other order details. Editing a customer, current gift-card balances and current marketing preferences are not part of this order-derived view.

Endpoints under /ekom/manager/customers require the same backoffice and store authorization as Orders: GET for search, GET detail for a customer, and GET export for CSV.
