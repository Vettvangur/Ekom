# Manager access and permissions

Configure Ekom's manager authorization under `Ekom:Manager` in `appsettings.json`. These settings use Umbraco backoffice user-group aliases, not display names.

```json
{
  "Ekom": {
    "Manager": {
      "SectionAccessGroup": "ekom,commerce-admins",
      "StoreGroupPermissions": {
        "store-a": ["store-a-managers"],
        "store-b": ["store-b-managers"]
      }
    }
  }
}
```

Group aliases and store aliases are compared case-insensitively. Ekom trims configured group names and ignores blank entries.

## Two authorization layers

Granting an Umbraco user access to the **Ekom** section and authorizing Ekom manager requests are related but separate operations:

- Umbraco section permissions determine whether the `ekommanager` section is visible to the user. In Umbraco 17 and 18, the section manifest explicitly applies Umbraco's section-user-permission condition.
- Ekom's `IManagerAccessService` authorizes the manager endpoints and determines which stores are available.

Configure the user's Umbraco group to include the Ekom section as well as configuring Ekom's group rules. Do not rely on hiding the section as the security boundary; store checks are enforced by the server.

## Manager access

Ekom allows an Umbraco administrator to access the manager.

For a non-administrator, Ekom builds the set of manager groups from:

- every comma-separated alias in `SectionAccessGroup`;
- every group listed for every store in `StoreGroupPermissions`.

The user may access the manager when at least one of their backoffice groups is in that set. Membership in a mapped store group therefore grants both manager access and access to that mapped store.

If neither setting contains a usable group, Ekom's manager authorization is unrestricted for authenticated backoffice users. Umbraco section permissions still control who can see the section.

For Umbraco 13, the AngularJS manager dashboard also has an Umbraco dashboard access-rule list. That list always grants administrators and adds the groups found in the current manager settings. If those settings contain no groups, it falls back to `Ekom:SectionAccessRules`; if that is also empty, the dashboard rule grants only administrators. This dashboard-visibility rule is separate from the permissive empty configuration in `IManagerAccessService`.

## Store access

Store authorization is evaluated independently after manager access:

| Configuration | Non-administrator behavior |
| --- | --- |
| `StoreGroupPermissions` is empty or omitted | Every store is allowed. |
| The map has one or more entries | Only mapped stores with a matching user group are allowed. |
| A store is omitted from a non-empty map | The store is denied. |
| A store maps to an empty or blank group list | The store is denied. |

This empty-map behavior is intentional. Do not add a partial map expecting unlisted stores to remain unrestricted; the first map entry changes the configuration to an allow-list.

Administrators can access all stores returned by Ekom's store service. A blank store alias is always rejected.

The checks cover store lists, order search and export, order details and activity logs, analytics, status changes, customer-information changes, order-line changes, and custom manager actions.

## Common configurations

### Restrict the manager, allow every store

Leave the store map empty and configure only section-level groups:

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

Members of `ekom` pass Ekom's manager check and can work with every store. They must also have Umbraco permission for the Ekom section.

### Restrict users by store

```json
{
  "Ekom": {
    "Manager": {
      "SectionAccessGroup": "ekom",
      "StoreGroupPermissions": {
        "store-a": ["store-a-managers"],
        "store-b": ["store-b-managers"]
      }
    }
  }
}
```

In this configuration:

- a member of `store-a-managers` can open the manager and use `store-a`;
- a member of `store-b-managers` can open the manager and use `store-b`;
- a member of `ekom` can open the manager but cannot use either store unless they also belong to its mapped group;
- every store omitted from the map is denied to non-administrators.

### Leave Ekom authorization unrestricted

```json
{
  "Ekom": {
    "Manager": {
      "StoreGroupPermissions": {}
    }
  }
}
```

With no configured manager groups, Ekom's manager and store checks allow authenticated non-administrator backoffice users. Access is still limited by the Umbraco section permissions assigned to those users. On Umbraco 13, the additional dashboard rule described above means a non-administrator also needs a current manager group or the legacy dashboard access setting for the dashboard to be visible.

## Legacy setting

`Ekom:SectionAccessRules` is a legacy setting used by the Umbraco 13 (`Ekom.U10`) dashboard access-rule configuration when no groups are supplied by the current manager settings. Prefer `Ekom:Manager:SectionAccessGroup` for current configuration. The legacy setting does not define per-store permissions.

## Troubleshooting

- If the section is missing, check the user's Umbraco section permission for alias `ekommanager`.
- If the section opens but no store is available, check for a non-empty `StoreGroupPermissions` map and confirm the user belongs to a group mapped to an existing store alias.
- If a user in `SectionAccessGroup` receives a forbidden response for an order, also grant the group mapped to that order's store. Section membership alone does not bypass a non-empty store map.
- If adding the first store mapping unexpectedly hides other stores, add explicit mappings for them or return to an empty map for unrestricted store access.
