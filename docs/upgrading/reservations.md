# Reservation compatibility

Current Ekom uses native SQL stock reservations. Product/variant and discount/coupon holds are stored in `EkomStockReservation` through Ekom's existing database connection. Ekom no longer registers Hangfire storage or Hangfire reservation jobs.

This implementation is shared by the supported integrations. The Umbraco 13 (`Ekom.U10`), Umbraco 17 (`Ekom.U17`), and Umbraco 18 (`Ekom.U18`) startup migrations create the reservation table and indexes through normal Ekom schema initialization.

For full configuration, lifecycle, integration, and operational details, see [Native SQL stock reservations](../guides/reservations.md).

## Default behavior

Automatic checkout reservations are opt-in and disabled by default. With the default configuration, checkout continues to validate stock and deduct it during completion without creating pre-payment holds.

```json
{
  "Ekom": {
    "Reservations": {
      "Enabled": false,
      "Timeout": 30,
      "PollInterval": "00:00:30",
      "BatchSize": 100,
      "WorkerEnabled": true,
      "CompletedRetention": "7.00:00:00"
    }
  }
}
```

`Enabled` controls automatic checkout hold creation. It does not disable the explicit reservation service, and it does not make existing holds disappear. The expiry worker is controlled independently by `WorkerEnabled`.

`Timeout` is measured in minutes. Ekom resolves `Ekom:Reservations:Timeout` first, then the legacy `Ekom:ReservationTimeout`, then the 30-minute default.

## Moving from legacy Hangfire reservations

Ekom does not migrate, inspect, execute, or delete existing Hangfire jobs or Hangfire tables. Old Hangfire job IDs are not native reservation IDs and are unknown to the native reservation service.

Plan deployment around any outstanding legacy reservation jobs. The repository does not provide a command or script that converts them. Determine how those jobs and their stock effects will be completed or reconciled using the application's existing Hangfire setup before removing that setup. Applications that use Hangfire for unrelated work can retain their own Hangfire packages and registrations.

Do not import old job IDs into `EkomStockReservation` or rewrite them as native IDs; no supported conversion is defined.

## Compatibility names

Several public Hangfire-named APIs remain as compatibility bridges, but their implementation now uses native reservations:

- `ReserveStockAsync` and `ReserveDiscountStockAsync` return native reservation IDs.
- `AddHangfireJobsToOrderAsync` delegates to `AddReservationsToOrderAsync`.
- `OrderInfo.HangfireJobs` and `OrderInfo.ReservationIds` expose the same collection.
- persisted order JSON containing only `HangfireJobs` is still read; when both properties exist, `ReservationIds` takes precedence.
- rollback/complete compatibility methods transition native reservations rather than scheduling Hangfire jobs.

These retained names do not mean Hangfire is still required by Ekom. New integration code should use `IStockReservationService`, `ReservationIds`, and the reservation-named order APIs and should inspect their explicit results.

## Existing and in-flight holds

Disabling `Ekom:Reservations:Enabled` after holds have been created does not cause checkout to ignore them. Ekom continues to recover, verify, and consume valid holds during completion. Expired, released, missing, mismatched, or foreign holds fail validation rather than being silently replaced.

Release and expiry restore stock; consumption keeps the prior deduction. Repeating terminal operations is idempotent. A payment that completes after its reservation deadline requires explicit reconciliation; native reservation consumption does not silently secure new stock for a late payment.

## Deployment checks

When adopting native reservations:

- allow Ekom startup schema initialization to complete before serving checkout traffic;
- decide whether automatic checkout reservations should remain at the default `false` or be explicitly enabled;
- ensure at least one Ekom node has `WorkerEnabled` when expired holds must be swept automatically;
- account for outstanding legacy Hangfire jobs before removing an old Hangfire reservation setup;
- verify custom checkout overrides and any code that stores or interprets reservation/job IDs;
- test expiry, release, successful consumption, retry, and late-payment handling against the application's payment flow.

These checks do not imply a data migration. No migration of legacy Hangfire jobs is implemented by Ekom.
