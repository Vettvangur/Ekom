# Customers

[Ekom docs](../README.md) / [Manager](README.md) / Customers

The **Customers** view in Ekom Manager lists the people who ordered in a period, with their lifetime spend, number of orders and how often they come back. Use it to look up one customer's orders or to export a customer list. It is built from order history: it is not a separate customer database, and you cannot edit customers here.

**Who this page is for:** staff who look up customers. The last parts, [Choose how customers are identified](#choose-how-customers-are-identified) and [Endpoints and data access](#endpoints-and-data-access), are for developers.

On this page:

- [Which customers and orders are counted](#which-customers-and-orders-are-counted)
- [Read the summary and the list](#read-the-summary-and-the-list)
- [Open a customer](#open-a-customer)
- [Export customers to CSV](#export-customers-to-csv)
- [Choose how customers are identified](#choose-how-customers-are-identified)
- [Endpoints and data access](#endpoints-and-data-access)

## Which customers and orders are counted

Customers uses the same store, currency, date range and status filters as Orders and Analytics. When you change them in one view, the others follow. The other Orders filters (search, providers, SKU, coupon, gift card and tracking) stay set in Orders but do not affect Customers. No customer data is shared between stores.

A customer appears in the list when they have at least one counted order created in the selected period. The date range includes both the first and the last day.

**Counted orders.** By default, Open and Closed orders count, whatever their payment status, so an Open order with payment Pending counts too. Draft and Cancelled orders do not count unless you select them in the **Order** status filter.

The filters work in two ways:

- The **Order** status filter decides which orders count, both in the period and in each customer's lifetime figures and history.
- The **Payment** and **Fulfillment** filters only decide which customers appear: a customer needs a matching order in the period. They do not narrow that customer's lifetime figures.

**Lifetime figures** (first order, number of orders, lifetime spend, New or Returning) use all of the customer's counted orders in the selected store and currency, not only the orders in the period.

## Read the summary and the list

The cards at the top summarize the customers in the list:

| Card | Meaning |
| --- | --- |
| **Customers** | Number of customers in the list. |
| **Returning** | Share of those customers with two or more counted orders. |
| **Average lifetime spend** | Per currency: the customers' lifetime spend divided by the number of customers who ordered in that currency. |
| **Average orders per customer** | Counted orders divided by customers. |
| **Average days between orders** | The average of each returning customer's reorder interval. Customers with one order are left out. |

Under the cards, a line tells you how many counted orders in the period have no email (or no username, depending on the site's setting). Those orders cannot be linked to a customer and are left out of Customers.

### Search, segment and sort

- **Search** finds customers by name, email or username, as recorded on any of their counted orders. Upper and lower case do not matter. Press Enter or click **Search**.
- **All**, **New** and **Returning** filter the list. A new customer has one counted order; a returning customer has two or more.
- **Sort by** offers **Newest first order** (the default), **Oldest first order**, **Latest order**, **Most orders** and **Name**.

The list shows 20 customers per page. For each customer it shows the name with a **New** or **Returning** badge, email or username, first order, number of orders, lifetime spend, average order, last order and days since last order.

### What the figures mean

- **First order** is the creation date of the customer's earliest counted order. It is not the date they registered an account.
- **Lifetime spend** is the sum of the charged amounts of the customer's counted orders. It is not reduced by refunds.
- **Average order** is lifetime spend divided by counted orders, per currency.
- **Days since last order** counts from the creation date of the latest counted order to today's date on the server. A future date shows 0.
- **Average reorder interval** is the time between the first and last order divided by the number of orders minus one. A customer with one order shows a dash.

Amounts in different currencies are never added together. When no currency is selected, each currency gets its own line.

## Open a customer

Click a customer's name. A panel opens with:

- **Lifetime spend**, **Orders**, **Average order**, **First order**, **Days since last order** and **Average reorder interval (days)**,
- **Orders**: every counted order of the customer, newest first, 20 per page, with order, payment and fulfillment status, created and paid dates and total. Tick **Show other orders (excluded from lifetime metrics)** to see the customer's orders that do not count, for example Drafts and Cancelled orders,
- **Latest order snapshot**: details recorded on the customer's latest counted order.

Click an order number to open it in Orders.

The snapshot shows only these fields, when they were recorded: phone, SSN, billing address, apartment, postcode, city and country, shipping name, address, apartment, postcode, city and country, last payment method, last shipping method and marketing consent. It names the order and date it comes from. It is what the customer entered on that order, not a current profile or consent record. If the saved order cannot be read, the panel says so and the order list still works.

The address bar keeps the open customer and store, so you can share a link to the customer with a colleague who has access to that store.

You cannot edit a customer, see current gift-card balances or current marketing preferences here.

## Export customers to CSV

Click **Export** to download `customers.csv`. It uses the same filters, search, segment and sort as the list, and includes every matching customer, not only the current page.

The file has one row per customer and currency, with the columns `Name`, `Email`, `Username`, `First order`, `Last order`, `Orders`, `Days since last order`, `Average reorder days`, `Currency`, `Lifetime spend` and `Average order`. It is UTF-8 with a byte order mark. A value that starts with `=`, `+`, `-` or `@` gets a `'` in front, so a spreadsheet does not run it as a formula.

## Choose how customers are identified

**For developers.** Ekom groups orders into customers by email address or by username. Set it under `Ekom:Manager:Customers:IdentityField`:

```json
{
  "Ekom": {
    "Manager": {
      "Customers": {
        "IdentityField": "CustomerUsername"
      }
    }
  }
}
```

| Value | Groups orders by |
| --- | --- |
| `CustomerEmail` (default) | The order's customer email. |
| `CustomerUsername` | The order's customer username. Ekom does not fall back to email when the username is empty. |

Any other value stops the site at startup with a validation error. Identifiers are trimmed and compared without regard to upper and lower case. Changing the setting changes the grouping straight away; it does not rewrite any order.

The same setting decides how Analytics counts unique and returning customers.

## Endpoints and data access

**For developers.** The view calls these endpoints. They need the same backoffice sign-in and store access as Orders, and return `403 Forbidden` for a store the user may not work with.

| Endpoint | Purpose |
| --- | --- |
| `GET /ekom/manager/customers` | Search. Takes `store`, `start`, `end`, `orderStatus`, `query`, `segment` (`all`, `new` or `returning`), `sort`, `page`, `pageSize` (1 to 100, default 20) and `currency`. |
| `GET /ekom/manager/customers/detail` | One customer. Takes `store`, `identity`, `page`, `other`, `currency` and `orderStatus`. Without `orderStatus`, Open and Closed orders count. |
| `GET /ekom/manager/customers/export` | The CSV file, with the same filters as search. |

Invalid dates, page or segment return `400 Bad Request`. An unknown customer returns `404 Not Found`.

The list, cards, search and export read only the order table's own columns and never read the saved order content (`OrderInfo`). Only the detail endpoint reads `OrderInfo`, and only for the customer's latest counted order, after the store check and the customer match. It returns only the snapshot fields listed above, and leaves out missing values.

For older callers, the `orderStatus` values `ReadyForDispatch` and `Dispatched` match the period on the paid date instead of the creation date. The Manager screens do not send these values.

## Related pages

- [Orders](orders.md): open one of the customer's orders.
- [Analytics](../analytics.md): new and returning buyers over a period.
- [Order, payment, and fulfillment statuses](../guides/order-statuses.md): what Open, Closed, Draft and Cancelled mean.
- [Access and permissions](access-and-permissions.md): which stores a user can see.

[Back to Manager](README.md)
