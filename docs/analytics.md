# Manager Analytics

Analytics shares the selected store, date range and order status with Orders and Customers.
The charts endpoint now includes an optional `customerInsights` object alongside the existing daily series.

## Customer insights

- Unique purchasing customers: distinct nonempty normalized email or username, according to `Ekom:Manager:Customers:IdentityField`.
- First-time buyers: customers whose first qualifying purchase appears in the selected orders.
- Returning customers: customers with a repeat purchase in the selected orders.
- Returning customer rate: returning customers / unique identified purchasing customers. No customers produces a null rate, displayed as a dash.
- First/repeat purchase breakdown: order counts and recorded charged amounts classified using qualifying purchase history in the same store.

A customer who places a first purchase and a repeat purchase in the period appears in both buyer counts, but only once in the unique count. First/repeat order counts do not overlap. History is ordered by PaidDate, falling back to CreateDate, with ReferenceId breaking ties. Later purchases do not retrospectively classify an earlier purchase as returning.

Customer insights include the same qualifying statuses as Customers (ReadyForDispatch, OfflinePayment, ReadyForDispatchWhenStockArrives, Dispatched, Closed, ReadyForPickup). The selected status narrows these orders. All Orders does not include incomplete/cancelled records in these purchasing-customer metrics. Date filtering uses PaidDate for ReadyForDispatch/Dispatched and CreateDate otherwise, matching Orders.

Missing identifiers are excluded from customer metrics and the purchase breakdown, counted separately, and retained in weekday/hour totals. Mixed-currency monetary values follow the existing Analytics suppression rules. Charged amounts are after gift cards, not refund-adjusted revenue.

## Weekday and hour charts

Time charts count qualifying orders within the selected filters, grouped by their recorded CreateDate weekday and hour. They use stored timestamps without timezone conversion. Monday is day zero regardless of database language/DATEFIRST. Empty weekdays and hours display zero. These are total order counts, not averages per occurrence of each weekday/hour.

## Query cost

Customer insights add one historical aggregate and one weekday/hour aggregate per period. Previous-period comparison therefore repeats those two queries for the comparison period. No OrderInfo is selected or parsed, and no per-customer queries are issued. Historical ranking cost still depends on store size; inspect SQL Server execution plans and production timings before claiming a latency target.
