# Move from Hangfire reservations

[Ekom docs](../README.md) / [Upgrading](README.md) / Move from Hangfire reservations

A stock reservation holds stock, or a discount use, for a customer while they pay. Previously Ekom scheduled each hold as a Hangfire job. Now Ekom stores holds in its own `EkomStockReservation` table, through the database connection it already uses, and no longer registers Hangfire storage or Hangfire jobs. This page covers what a site changes when it moves from the Hangfire holds to the database holds. How reservations work today is described in [Stock reservations](../guides/reservations.md).

This change is not tied to an Umbraco version. Follow this page together with the Umbraco version page if you upgrade both at the same time.

On this page:

- [Who needs this page](#who-needs-this-page)
- [What changes](#what-changes)
- [Before you start](#before-you-start)
- [Move the site](#move-the-site)
- [Holds that exist during the move](#holds-that-exist-during-the-move)
- [Check that it worked](#check-that-it-worked)
- [What Ekom does not do for you](#what-ekom-does-not-do-for-you)

## Who needs this page

Your site needs this page if it runs an Ekom version that created stock holds as Hangfire jobs, or if its code stores or reads reservation or Hangfire job IDs.

## What changes

### Where holds are stored

Holds for products, variants, discounts and coupons are rows in `EkomStockReservation`. Ekom creates the table and its indexes at startup, as part of its normal database setup. Old Hangfire job IDs are not reservation IDs, and the reservation service does not know them.

### Automatic holds at checkout are off by default

With the default settings, checkout does not hold stock before payment. It checks stock and deducts it when the order completes. Turn on automatic holds with `Ekom:Reservations:Enabled`.

```json
{
  "Ekom": {
    "Reservations": {
      "Enabled": true,
      "Timeout": 30,
      "PollInterval": "00:00:30",
      "BatchSize": 100,
      "WorkerEnabled": true,
      "CompletedRetention": "7.00:00:00"
    }
  }
}
```

| Setting | Default | What it does |
| --- | --- | --- |
| `Enabled` | `false` | `true`: checkout creates holds before payment. It does not turn the reservation service on or off, and setting it back to `false` does not remove holds that exist. |
| `Timeout` | `30` | Minutes before a hold expires. |
| `PollInterval` | `00:00:30` | How often the background worker expires holds that are past their time. Must be more than zero and at most one day. |
| `BatchSize` | `100` | How many holds the worker expires, and how many old rows it deletes, in one pass. Must be from 1 to 10000. |
| `WorkerEnabled` | `true` | `false`: this server does not run the background worker. |
| `CompletedRetention` | `7.00:00:00` | How long finished holds stay in the table before the worker deletes them. Must not be negative. |

An invalid `PollInterval`, `BatchSize` or `CompletedRetention` makes the worker throw `ArgumentOutOfRangeException` when the site starts.

For the timeout, Ekom reads the first value it finds in this order: `Ekom:Reservations:Stores:<store alias>:Timeout`, `Ekom:Reservations:Timeout`, the old `Ekom:ReservationTimeout`, then 30 minutes. `Enabled` can also be set for one store with `Ekom:Reservations:Stores:<store alias>:Enabled`.

### Names that still say Hangfire

Some public members kept their Hangfire names so that existing code still compiles. They now work on database holds:

| Member | What it does now |
| --- | --- |
| `Stock.ReserveStockAsync`, `Stock.ReserveDiscountStockAsync` | Create a hold and return its reservation ID. |
| `Order.AddHangfireJobsToOrderAsync` | Calls `AddReservationsToOrderAsync`. |
| `IOrderInfo.HangfireJobs` | The same list as `IOrderInfo.ReservationIds`. |
| `Stock.CancelRollback` | Consumes the hold. Throws `StockException` if the hold cannot be consumed. |
| `Stock.RollbackJobAsync`, `Stock.CompleteRollback` | Release the hold. |

Orders saved with only a `HangfireJobs` list are still read. When an order has both `ReservationIds` and `HangfireJobs`, Ekom uses `ReservationIds`.

These names do not mean that Ekom needs Hangfire. In new code, use `IStockReservationService`, `IOrderInfo.ReservationIds` and `AddReservationsToOrderAsync`, and check the result each call returns.

## Before you start

1. Find the Hangfire reservation jobs that are still scheduled. Each one is a customer who may still pay, or stock that is still held.
2. Decide how each of those jobs will finish: let it run on the old setup before you switch, or correct the stock by hand afterwards. Ekom does not run, convert or delete them.
3. Find custom code that stores, reads or acts on reservation or job IDs, and custom checkout code that creates holds.
4. Decide whether checkout should hold stock before payment. If yes, choose the timeout.

## Move the site

1. Let the outstanding Hangfire reservation jobs finish, or record them so you can correct stock afterwards.
2. Update Ekom and start the site. Ekom creates `EkomStockReservation` at startup. Do not send checkout traffic to the site until startup has finished.
3. If you want holds at checkout, set `Ekom:Reservations:Enabled` to `true`.
4. Make sure at least one server has `Ekom:Reservations:WorkerEnabled` set to `true`. Without a worker, holds past their time are not expired, and their stock is not released.
5. Remove the Hangfire setup that existed only for Ekom reservations. Keep Hangfire if the site uses it for its own work.
6. Change custom code to use the reservation names, as listed under [Names that still say Hangfire](#names-that-still-say-hangfire).

Do not copy old job IDs into `EkomStockReservation`, and do not rewrite them as reservation IDs. There is no supported way to convert them.

## Holds that exist during the move

- Setting `Enabled` to `false` after holds exist does not make checkout ignore them. Ekom still finds, checks and consumes valid holds when an order completes.
- A hold that has expired, was released, is missing, belongs to another order, or does not match the order fails the check. Ekom does not quietly create a new hold in its place.
- Releasing or expiring a hold gives the stock back. Consuming a hold keeps the stock deducted. Repeating one of these on the same hold has no further effect.
- A payment that completes after its hold has expired does not get the stock back automatically. Ekom reports it as a late consumption, and you must reconcile that order yourself.

## Check that it worked

1. The site starts, and the `EkomStockReservation` table exists.
2. With `Enabled` set to `true`, start a checkout. A row appears in `EkomStockReservation` and the product's stock goes down.
3. Leave a checkout unpaid until after the timeout. The hold expires and the stock comes back.
4. Pay for a checkout. The hold is consumed and the stock stays deducted.
5. Test a payment that arrives after the hold has expired, in the way your payment flow handles it.

## What Ekom does not do for you

- Run, convert, inspect or delete Hangfire jobs or Hangfire tables.
- Give back stock held by Hangfire jobs that never ran.
- Secure new stock for a payment that arrives after its hold expired.
- Turn on automatic holds at checkout.

## Related pages

- [Stock reservations](../guides/reservations.md): how holds work, the reservation service, and the background worker.
- [Stock](../guides/stock.md): read and change stock.
- [Checkout](../guides/checkout.md): where holds are created and consumed.
- [Configuration reference](../reference/configuration-reference.md): all `Ekom` settings.
- [Before and after an upgrade](overview.md): the checks that apply to every upgrade.

[Back to Upgrading](README.md)
