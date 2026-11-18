[Ekom docs](../README.md) / [Manager](README.md) / Access and permissions

# Manager access and permissions

This page explains how to decide which backoffice users can open Ekom Manager and which stores each of them can work with. You set it with Umbraco user groups and two settings under `Ekom:Manager`.

**Who this page is for:** developers and administrators who configure a site. Staff who get a "forbidden" message or see no stores can start at [Fix common access problems](#fix-common-access-problems).

> **Security default:** an empty `StoreGroupPermissions` map gives every non-administrator who can use the Manager access to **every store**. Adding the first store to the map turns it into an allow-list: from then on, every store that is not listed is denied to non-administrators.

On this page:

- [Two checks: Umbraco and Ekom](#two-checks-umbraco-and-ekom)
- [The settings](#the-settings)
- [Who can use the Manager](#who-can-use-the-manager)
- [Which stores a user can work with](#which-stores-a-user-can-work-with)
- [Common configurations](#common-configurations)
- [Fix common access problems](#fix-common-access-problems)

## Two checks: Umbraco and Ekom

Giving a user the **Ekom** section and letting them use the Manager are two separate things:

- **Umbraco** decides whether the section shows up for the user. The section (alias `ekommanager`) is only shown to users whose group has access to it.
- **Ekom** checks every Manager request on the server. It decides whether the user may use the Manager at all, and which stores are available to them.

Set up both: give the user's Umbraco group access to the Ekom section, and configure Ekom's group rules. Do not rely on hiding the section to protect data. Ekom enforces the store check on the server, so changing a store alias or order ID in a browser request does not give access.

## The settings

Both settings use Umbraco user group **aliases**, not the names shown in the backoffice.

```json
{
  "Ekom": {
    "Manager": {
      "SectionAccessGroup": "ekom,commerce-admins",
      "StoreGroupPermissions": {
        "store-is": ["store-is-managers"],
        "store-uk": ["store-uk-managers"]
      }
    }
  }
}
```

| Setting | Type | Default | What it does |
| --- | --- | --- | --- |
| `Ekom:Manager:SectionAccessGroup` | Comma-separated group aliases | Not set | Groups whose members may use the Manager. |
| `Ekom:Manager:StoreGroupPermissions` | Map of store alias to a list of group aliases | Empty | Which groups may work with each store. Empty means every store for everyone who may use the Manager. |

Ekom compares group aliases and store aliases without regard to upper and lower case. It trims spaces around group aliases and ignores blank entries.

A site set up with an older Ekom version may have `Ekom:SectionAccessRules` instead. It is the older name of `Ekom:Manager:SectionAccessGroup` and has the same format. Ekom reads it only when `SectionAccessGroup` and `StoreGroupPermissions` are both empty, so it stops applying as soon as you add either of them. Move its value to `SectionAccessGroup` when you next change these settings.

## Who can use the Manager

A user must be signed in to the backoffice, approved and not locked out. Then:

- A member of the Umbraco **Administrators** group (alias `admin`) may always use the Manager.
- For anyone else, Ekom collects a list of Manager groups from two places: every alias in `SectionAccessGroup`, and every group listed for any store in `StoreGroupPermissions`. The user may use the Manager when they belong to at least one group on that list.

Because store groups are on the list, a member of a mapped store group may use the Manager and work with that store, even if the group is not in `SectionAccessGroup`.

If neither setting names a group, Ekom lets every signed-in backoffice user use the Manager. Umbraco's section permission still decides who sees the section.

## Which stores a user can work with

Ekom checks the store after the Manager check. Administrators can work with every store that exists. For other users:

| `StoreGroupPermissions` | Result for a non-administrator |
| --- | --- |
| Empty or missing | Every store is allowed. |
| Has one or more entries | Only stores in the map, and only when the user belongs to one of the groups listed for that store. |
| A store is not in a non-empty map | That store is denied. |
| A store is listed with an empty list, or only blank group names | That store is denied. |

An empty or blank store alias is always denied.

The empty-map behavior is intended. Do not add a single store to the map and expect the other stores to stay open: the first entry turns the map into an allow-list.

The store check covers:

- the store list in the **Store** menu,
- order search, view counts and CSV export,
- order details, payments, shipments, activity logs, reviews and the checkout lock,
- status changes, customer information, order lines and shipping provider changes,
- custom order actions, reservation releases and failed gift-card preparation recovery,
- Customers and its CSV export,
- Analytics charts and most sold products,
- the Printing view's jobs and stations.

## Common configurations

### Restrict the Manager, allow every store

Leave the store map empty and name the groups that may use the Manager.

```json
{
  "Ekom": {
    "Manager": {
      "SectionAccessGroup": "ekom",
      "StoreGroupPermissions": {}
    }
  }
}
```

Members of `ekom` can use the Manager and work with every store. Their Umbraco group must also have access to the Ekom section.

### Give each team its own store

```json
{
  "Ekom": {
    "Manager": {
      "SectionAccessGroup": "ekom",
      "StoreGroupPermissions": {
        "store-is": ["store-is-managers"],
        "store-uk": ["store-uk-managers"]
      }
    }
  }
}
```

With these settings:

- a member of `store-is-managers` can use the Manager and work with `store-is`,
- a member of `store-uk-managers` can use the Manager and work with `store-uk`,
- a member of `ekom` only can open the Manager but sees no store, unless they also belong to a store's group,
- every store that is not in the map is denied to non-administrators.

To give one group every store while the map is in use, list that group under each store.

### Leave Ekom's checks open

```json
{
  "Ekom": {
    "Manager": {
      "StoreGroupPermissions": {}
    }
  }
}
```

With no groups configured, every signed-in backoffice user passes Ekom's Manager and store checks. Only the Umbraco section permission limits who can see the Manager. Use this only when every user with the Ekom section may see every store's orders and customers.

## Fix common access problems

- **The Ekom section is missing.** Give the user's Umbraco group access to the section with alias `ekommanager`.
- **The section opens but no store is available.** The store map is not empty. Check that the user belongs to a group listed for an existing store alias.
- **A user in `SectionAccessGroup` gets a "forbidden" message on an order.** Membership of `SectionAccessGroup` does not get past a non-empty store map. Add the user to the group listed for that order's store.
- **Adding the first store hid the other stores.** Add every store to the map with its groups, or empty the map again to open all stores.

## Related pages

- [Manager overview](overview.md): the views and the packages that supply them.
- [Orders](orders.md): what users can do once they have access.
- [Configuration reference](../reference/configuration-reference.md): every `Ekom` setting.
- [Ekom.Printer README](../../Ekom/Printing/Ekom.Printer/README.md): the `Ekom:Printing:ManagerGroups` setting for printing actions.

[Back to Manager](README.md)
