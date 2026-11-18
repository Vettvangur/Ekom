# Analytics

[Ekom docs](README.md) / [Manager](manager/README.md) / Analytics

The **Analytics** view in Ekom Manager shows how a store sells over time: order amount, number of orders and average order value, how many buyers are new or returning, on which weekdays and hours orders are placed, and which products sell most. Use it to follow sales for a period and compare it with the period before.

**Who this page is for:** staff who read the figures. The last parts, [Endpoints](#endpoints) and [Query cost](#query-cost), are for developers.

On this page:

- [Choose what to analyse](#choose-what-to-analyse)
- [Read sales over time](#read-sales-over-time)
- [New and returning buyers](#new-and-returning-buyers)
- [Orders by weekday and hour](#orders-by-weekday-and-hour)
- [Most sold products](#most-sold-products)
- [Endpoints](#endpoints)
- [Query cost](#query-cost)

## Choose what to analyse

Analytics uses the same store, currency, date range and status filters as Orders and Customers. When you change them in one view, the others follow. The other Orders filters (search, providers, SKU, coupon, gift card and tracking) do not affect Analytics.

- **Store and currency**: only orders of that store in that currency are counted.
- **Date range**: includes the first and the last day, and is matched against the date each order was created.
- **Order**, **Payment** and **Fulfillment**: the status filters at the top. With no status chosen, the figures include Open, Closed and **Cancelled** orders. Drafts are left out unless you select them. To leave Cancelled orders out, select Open and Closed in the **Order** filter.

**Compare to previous period** adds the same number of days immediately before the selected range. For example, 1 to 31 March is compared with 29 January to 28 February in a year where February has 28 days. Changes are shown as a percentage, and for the returning customer rate in percentage points.

The **Polling** menu reloads the figures every 2, 5, 10, 20 or 30 seconds. It is off when you open the view.

## Read sales over time

The three cards at the top show for the selected period:

| Card | Meaning |
| --- | --- |
| **Order amount** | The sum of the orders' charged amounts. |
| **Orders** | The number of orders. |
| **Average order value** | Order amount divided by orders. |

The order amount is what was charged on each order after gift cards. It is not reduced by refunds, so it is not net revenue.

Below the cards is a chart of one figure over time. Choose **Order amount**, **Orders** or **Average order value** above it, and **Group by** **Daily**, **Weekly** or **Monthly**. Weeks start on Monday. With a comparison, the previous period is drawn as a dashed line.

- Click a point in the chart, or **Open orders** in the data table, to open those orders in Orders, with that date range and the same store, currency and status filters. The other Orders filters are cleared.
- **Show data table** shows the same figures as a table, with the previous period when you compare.
- **Download CSV** saves `analytics.csv` with one row per day, week or month: `From`, `To`, `Orders`, `Order amount`, `Average order`, `Currency`, and the same figures for the previous period.

If the orders in the period are in more than one currency, Analytics does not add them together: the money figures are hidden, the chart shows orders only, and a message says why. A monetary comparison is also hidden when the two periods are in different currencies.

## New and returning buyers

Three more cards and a table describe the buyers in the period:

| Figure | Meaning |
| --- | --- |
| **Unique purchasing customers** | Different customers with an order in the period. |
| **First-time buyers** | Customers whose first counted purchase in the store is in the period. |
| **Returning customer rate** | Customers with a repeat purchase in the period, divided by unique purchasing customers. Shows a dash when there are no customers. |

The **New and returning purchases** table splits the period's orders into **First purchase** and **Repeat purchase**, with the number of buyers, orders and the order amount for each.

How a purchase is classified:

- Ekom identifies customers by email or by username, as set for [Customers](manager/customers.md#choose-how-customers-are-identified). Upper and lower case and surrounding spaces do not matter.
- Ekom looks at all of the customer's counted orders in the store and currency, not only the period, and numbers them in order of paid date, or creation date when there is no paid date. The order number decides between orders at the same time. The first is the first purchase; every later one is a repeat purchase.
- An order placed later never turns an earlier purchase into a repeat purchase.
- A customer who makes a first and a repeat purchase in the same period is counted as both a first-time buyer and a returning customer, but only once in **Unique purchasing customers**. Each order is either a first or a repeat purchase, never both.

**Counted orders** for buyer figures are the same as in [Customers](manager/customers.md#which-customers-and-orders-are-counted): by default Open and Closed orders. Cancelled orders and drafts are not counted as purchases, even though the sales figures above include Cancelled orders by default. Selecting an Order status narrows which orders count.

Orders without an email (or username) cannot be linked to a customer. They are left out of the buyer figures and the purchase table, but they are still counted in the weekday and hour charts.

## Orders by weekday and hour

**Orders by weekday** and **Orders by hour** count the period's orders by the weekday and the hour (00 to 23) of their creation time. These charts count Open and Closed orders, as the buyer figures do.

- The times are used as they are stored, without time zone conversion.
- Weeks start on Monday, whatever the database's language settings.
- A weekday or hour without orders shows 0.
- The figures are totals for the period, not averages per day or per hour.

## Most sold products

Click **Load product breakdown** to list the most sold products for the selected store, currency, dates and status filters. It can take a minute for long date ranges. Click **Hide products** to close it.

The list shows each product's name, SKU, quantity sold and total, 20 products per page, with the highest quantity first. Variants are counted under their product. The total is the sum of the line totals, including VAT and discounts, without shipping and payment fees.

## Endpoints

**For developers.** The view calls these endpoints. They require a backoffice user with access to the store, and return `403 Forbidden` otherwise.

| Endpoint | Returns |
| --- | --- |
| `GET /ekom/manager/charts` | Daily order amount, orders and average for every day in the range, the date basis, the currency, whether currencies are mixed, and a `customerInsights` object with the buyer figures and the weekday and hour counts. Takes `start`, `end`, `store`, `orderStatus` and `currency`. |
| `GET /ekom/manager/MostSoldProducts` | The most sold products. Takes `start`, `end`, `store`, `orderStatus`, `currency`, and `page` and `pageSize` (default 20). Without `page` and `pageSize` it returns every product. |

`charts` returns `400 Bad Request` when `start` is missing, `end` is before `start`, or the range is longer than 3,660 days.

`MostSoldProducts` responses are marked as cacheable for 24 hours, so a browser or proxy can return an earlier result for the same request.

For older callers, the `orderStatus` values `ReadyForDispatch` and `Dispatched` match the date range, and group the chart, by paid date instead of creation date. The Manager screens do not send these values.

## Query cost

The buyer figures add two queries to each chart request: one over the store's order history, and one for the weekday and hour counts. A comparison runs them again for the previous period. They read only the order table's own columns, never the saved order content, and make no query per customer. The history query reads all of the store's counted orders, so its cost grows with the size of the store. Check SQL Server execution plans and timings on production-sized data before you rely on a response time.

## Related pages

- [Customers](manager/customers.md): the customers behind the buyer figures.
- [Orders](manager/orders.md): open the orders behind a point in the chart.
- [Manager overview](manager/overview.md): the shared store, currency and date filters.
- [Order, payment, and fulfillment statuses](guides/order-statuses.md): what the status filters mean.

[Back to Manager](manager/README.md)
