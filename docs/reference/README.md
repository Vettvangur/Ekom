# Reference

[Ekom docs](../README.md) / Reference

The reference pages list what Ekom exposes: every setting, event, HTTP route, public API class and service, with its exact name, type and default. Use them to look one thing up while you work.

The pages are for developers who already know what they want to build. They do not walk you through a task. To learn how a feature works and how to set it up, read the matching page in [Guides](../guides/README.md). Each group in a reference page links to its guide.

## Where to start

- You call Ekom from C# on the server: start with the [.NET API](dotnet-api.md).
- Your storefront talks to Ekom from a browser or another server: start with the [HTTP API](http-api.md).
- You want to change how Ekom behaves: look in the [Configuration reference](configuration-reference.md) for a setting, then in [Events](events.md) for a hook.

## Pages in this section

| Page | Read it when you want to |
| --- | --- |
| [.NET API](dotnet-api.md) | Find a method on `Catalog`, `Store`, `Order`, `Providers`, `Discounts`, `Stock`, `Warehouse` or `Prices`. |
| [Services and dependency injection](services-and-dependency-injection.md) | See which services Ekom registers, their lifetimes, and which ones you can replace. |
| [Events](events.md) | Find an event, the data it gives you, and whether it runs before or after the change is saved. |
| [HTTP API](http-api.md) | Find a route: its method, parameters, limits and responses. |
| [Configuration reference](configuration-reference.md) | Find a setting under `Ekom` in `appsettings.json`, its type and its default. |
| [Models and behavior](models-and-behavior.md) | Understand the objects the APIs return, such as `IStore`, `IProduct`, `IPrice` and `IOrderInfo`. |

## Other sections

- [Getting started](../getting-started/README.md): install and configure Ekom.
- [Guides](../guides/README.md): how each part of Ekom works and how to use it.
- [Examples](../examples/README.md): working code you can copy for common tasks.
- [Manager](../manager/README.md): the backoffice area for orders, customers and analytics.
- [Upgrading](../upgrading/README.md): move a site to a newer Umbraco or Ekom version.

[Back to the documentation home](../README.md)
