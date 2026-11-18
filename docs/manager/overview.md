[Ekom docs](../README.md) / [Manager](README.md) / Overview

# Ekom Manager overview

Ekom Manager is the **Ekom** section of the Umbraco backoffice, where staff work with orders, customers, sales figures and picking-slip printing. This page lists the views, explains the store, currency and date filters they share, and shows which packages supply the Manager and how access is checked.

**Who this page is for:** staff who want to know what each view is for, and developers who install and secure the Manager. The [Packages](#packages-that-supply-the-manager) and [How access is checked](#how-access-is-checked) parts are for developers.

On this page:

- [The views in the Manager](#the-views-in-the-manager)
- [Store, currency and date range](#store-currency-and-date-range)
- [Track picking-slip printing](#track-picking-slip-printing)
- [Packages that supply the Manager](#packages-that-supply-the-manager)
- [How access is checked](#how-access-is-checked)
- [Backoffice endpoints](#backoffice-endpoints)

## The views in the Manager

Open the **Ekom** section in the backoffice. The links at the top of the page switch between the views. The section opens on Orders.

| View | What you do there |
| --- | --- |
| **Orders** | Search and filter orders, export them to CSV, open an order to see its details, and change its order, payment and fulfillment status. See [Orders](orders.md). |
| **Analytics** | Follow order amount, number of orders and average order value over time, new and returning buyers, when orders are placed, and the most sold products. See [Analytics](../analytics.md). |
| **Customers** | See the customers who ordered in a period, their lifetime spend and their order history. See [Customers](customers.md). |
| **Printing** | Follow picking-slip print jobs and the warehouse printer stations. Shown only when the Ekom.Printer package is installed and printing is turned on. See [Track picking-slip printing](#track-picking-slip-printing). |

## Store, currency and date range

All views work on one store at a time. Choose the store and currency in the **Store** menu at the top right. The menu lists the stores you have access to, and under each store the currencies it is configured with and the currencies recorded on its orders. Amounts are shown in the selected currency, and only orders in that currency are counted.

When you open the Manager:

- the store is the first store you have access to,
- the currency is the store's own currency, or the first one listed if the store's currency is not there,
- the **Date range** is 1 January of the current year to today.

The store, currency, date range and status filters are shared. If you change them in Orders, Analytics and Customers use the same values, and the other way round. The other order filters (search text, provider, SKU, coupon, gift card and tracking) apply to Orders only.

A date range includes both the first and the last day.

## Track picking-slip printing

The **Printing** view shows the picking slips that Ekom.Printer has created, and the printer stations that print them. A station is one Windows computer running the Ekom printer agent. You see only the jobs and stations of stores you have access to.

The **Print jobs** list can be filtered by **Status**, **Station** and **Order number**. The status **Needs attention** shows jobs that need review or failed to generate. The list shows 25 jobs per page. Each job has one of these statuses:

| Status | Meaning |
| --- | --- |
| Generating slip | Ekom is creating the picking slip. |
| Queued | The slip is ready and waits for the agent. |
| With agent | The agent has collected the slip. |
| Submitted to printer | The Windows print queue accepted the job. This does not confirm that paper came out. |
| Confirmed printed | A person confirmed that the slip was printed. |
| Needs review | Printing could not be confirmed. Check the printer before you print it again. |
| Generation failed | The picking slip could not be created. |
| Cancelled | The job was cancelled before printing. |

Every job has **Preview** (while its slip is still stored) and **History**. Users who may manage printing also see these actions, depending on the status:

| Action | Shown for | What it does |
| --- | --- | --- |
| Retry generation | Generation failed | Tries again to create the slip. |
| Cancel | Generating slip, Queued, Generation failed | Cancels the job. |
| Reprint | Submitted to printer, Confirmed printed | Prints another copy of the stored slip. |
| Mark as printed | Needs review | Records that you checked the paper and the slip was printed. Nothing is printed again. |
| Print again after checking | Needs review | Closes the uncertain job and queues one replacement with the same number of copies. Clear any waiting copy from the Windows print queue first, or the original can still print. |

Each action asks you to confirm. A reprint uses the stored slip, not newer order data.

The **Printer stations** cards show for each station: whether it is **Online**, **Offline** or needs an update (**Update required**), the printer name, the store, the statuses that trigger printing, the number of copies, whether automatic printing is enabled, and when the agent last connected. **Online** means the agent contacted the website recently. It does not mean the printer has paper. Station settings are read-only here: they are changed in the website configuration and in the Windows agent.

For developers: Umbraco administrators can always use the manage actions. To give them to other backoffice groups, add the group aliases to `Ekom:Printing:ManagerGroups`. Setup, configuration and the meaning of each setting are in the [Ekom.Printer README](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/Printing/Ekom.Printer/README.md).

## Packages that supply the Manager

The Manager has two parts. The Ekom package for your Umbraco version supplies the server endpoints and the access checks. The matching web-assets package supplies the backoffice screens under `App_Plugins/Ekom`. Install the web-assets package in the site project that contains `wwwroot`.

| Umbraco | Ekom package | Web-assets package |
| --- | --- | --- |
| 17 | `Ekom.U17` | `Ekom.Web.U17` |
| 18 | `Ekom.U18` | `Ekom.Web.U18` |
| 19 (release candidate) | `Ekom.U19` | `Ekom.Web.U19` |

All three use the same Manager. `Ekom.Web.U18` and `Ekom.Web.U19` are built from the `Ekom.Web.U17` client source, and `Ekom.U18` and `Ekom.U19` compile the `Ekom.U17` source, so the views, labels and endpoints are the same on 17, 18 and 19.

The section alias is `ekommanager` and its label in the backoffice is **Ekom**.

## How access is checked

Two separate checks decide what a backoffice user can do:

1. **Umbraco** decides whether the user sees the section. Give the user's group access to the **Ekom** section (alias `ekommanager`) in the Umbraco user group settings.
2. **Ekom** checks every Manager request on the server. It decides whether the user may use the Manager at all, and which stores they may work with.

Umbraco administrators pass Ekom's check and can work with every store. For other users, the rules are set under `Ekom:Manager` in `appsettings.json`.

> **Default:** when `Ekom:Manager:StoreGroupPermissions` is empty or missing, every user who passes the Manager check can work with **every store**. As soon as the map has one entry, it becomes an allow-list and every store that is not listed is denied to non-administrators.

Hiding the section is not a security boundary. Ekom enforces the store check on the server. Read [Access and permissions](access-and-permissions.md) before you give the section to a user group.

## Backoffice endpoints

The Manager screens call endpoints under `/ekom/manager`, and the Printing view calls endpoints under `/ekom/printer/manager`. They are for the backoffice, not a storefront API. Every endpoint requires a signed-in backoffice user who passes Ekom's Manager check. Endpoints that read or change store data also check that the user may work with that store, and return `403 Forbidden` when not.

## Related pages

- [Orders](orders.md): find, export and update orders.
- [Access and permissions](access-and-permissions.md): who can open the Manager and which stores they see.
- [Order, payment, and fulfillment statuses](../guides/order-statuses.md): what each status means.
- [Ekom.Printer README](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/Printing/Ekom.Printer/README.md): set up picking-slip printing.
- [Compatibility](../getting-started/compatibility.md): which packages match your Umbraco version.

[Back to Manager](README.md)
