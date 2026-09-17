# Ekom Manager

Ekom Manager is Ekom's Umbraco backoffice area for order operations and sales reporting. It is supplied by the Ekom packages and is not a separate application.

The section is displayed as **Ekom** and uses the alias `ekommanager`. It contains two views:

- **Orders** for searching, exporting, reviewing, and updating orders.
- **Analytics** for revenue, order count, average order value, and most-sold-product reporting.

## Supported manager implementations

The manager's server endpoints and authorization rules are shared by the supported packages, but the backoffice UI depends on the Umbraco generation:

| Umbraco | Ekom packages | Manager UI |
| --- | --- | --- |
| 13 | `Ekom.U10` and `Ekom.Web` | AngularJS dashboard |
| 17 | `Ekom.U17` and `Ekom.Web.U17` | Umbraco extension section views |
| 18 | `Ekom.U18` and `Ekom.Web.U18` | The shared Umbraco 17/18 implementation |

`Ekom.U10` is the retained legacy package and assembly name used for the current Umbraco 13 integration. The name does not mean that the current package targets Umbraco 10.

Install the web-assets package in the site project that contains `wwwroot`; it supplies the manager client files under `App_Plugins/Ekom`.

## Orders

The Orders view is scoped to one store available to the current backoffice user. Its default date range is the start of the current year through today, and its default status filter is **Completed Orders**.

The view provides:

- order count, payments total, and average order amount for the current filters;
- status, date range, store, payment provider, exact product SKU, and tracking filters;
- free-text search and paged results;
- CSV export, optionally including order lines;
- order details, status changes, customer-information editing, line changes, printing, activity logs, and registered custom actions.

See [Orders](orders.md) for filter and editing behavior.

## Analytics

The Analytics view filters by status, date range, and store. It displays:

- sales revenue by date;
- total orders by date;
- average order value by date;
- a paged list of most-sold products.

For the `ReadyForDispatch` and `Dispatched` status filters, Ekom applies the date range to `PaidDate`. Other status selections use `CreateDate`. Daily chart buckets use the paid date when present and otherwise the creation date.

## Access model

Manager access has two layers:

1. Umbraco controls whether a user has permission to see the `ekommanager` section.
2. Ekom authorizes manager requests and checks store access on store-specific operations.

Umbraco administrators pass Ekom's manager check and can use every existing store. Non-administrator behavior is configured under `Ekom:Manager`.

An important default is that an empty `StoreGroupPermissions` map is unrestricted: it allows a non-administrator who can use the manager to work with every store. A non-empty map switches to an allow-list, and stores omitted from it are denied.

See [Access and permissions](access-and-permissions.md) before granting the section to a user group.

## Backoffice endpoints

The UI calls authenticated endpoints below `/ekom/manager`. These endpoints are intended for the backoffice, not as a public storefront API. Ekom checks manager authorization on every endpoint and checks the selected or owning store before returning or changing store-specific data.

## Related pages

- [Access and permissions](access-and-permissions.md)
- [Orders](orders.md)
- [Activity logs](activity-logs.md)
- [Upgrade overview](../upgrading/overview.md)
