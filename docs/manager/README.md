[Ekom docs](../README.md) / Manager

# Manager

Ekom Manager is the **Ekom** section of the Umbraco backoffice. Staff use it to find and update orders, look up customers, follow sales in Analytics and track picking-slip printing. It is part of the Ekom packages, not a separate application.

This section has two kinds of reader:

- **Staff** who work with orders every day. The staff pages describe the screens with the labels the backoffice shows, without code.
- **Developers** who install Ekom, decide who can open the section, and extend it. The developer parts show settings and code.

Each page says at the top who it is for.

## Where to start

- Working with orders? Start with [Orders](orders.md).
- Setting up the Manager for a site? Read [Overview](overview.md), then [Access and permissions](access-and-permissions.md) before you give the section to a user group.

## Pages for staff and developers

| Page | For | Read it when you want to |
| --- | --- | --- |
| [Overview](overview.md) | Both | See which views the Manager has, which packages supply it, and how access works. |
| [Orders](orders.md) | Staff, with a developer part | Find, export and update orders, change statuses and release a checkout lock. |
| [Customers](customers.md) | Staff, with a developer part | See who bought in a period and each customer's order history. |
| [Analytics](../analytics.md) | Staff, with a developer part | Read sales, order counts, new and returning buyers, and when orders are placed. |
| [Activity logs](activity-logs.md) | Staff, with a developer part | Read an order's history, or write your own entries from code. |
| [Access and permissions](access-and-permissions.md) | Developers | Decide who can open the Manager and which stores each user can work with. |

## Pages about picking-slip printing

Ekom.Printer is a separate package that prints picking slips on a warehouse printer through a Windows agent. Its setup guide is the [Ekom.Printer README](https://github.com/Vettvangur/Ekom/blob/Ekom/Ekom/Printing/Ekom.Printer/README.md). The **Printing** view in the Manager is described in [Overview](overview.md#track-picking-slip-printing).

| Page | For | Read it when you want to |
| --- | --- | --- |
| [Printer agent protocol](../printer-agent-protocol.md) | Developers building their own printer agent | Talk to the station API without the supplied Windows agent. |
| [Printer agent releases](../contributing/printer-agent-releases.md) | Ekom maintainers, and operators checking a download | Release the Windows agent and the Ekom.Printer package, or verify a downloaded installer. |

## Other sections

- [Guides](../guides/README.md): build each part of a store, including [order statuses](../guides/order-statuses.md) and [order reviews](../guides/order-reviews.md).
- [Reference](../reference/README.md): the .NET API, HTTP API, events and every setting.
- [Getting started](../getting-started/README.md): install Ekom and its web-assets package.
- [Upgrading](../upgrading/README.md): move a site to a newer Umbraco or Ekom version.

[Back to the documentation home](../README.md)
