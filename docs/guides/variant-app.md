# Variant App

The Variant App is the product workspace UI for managing `ekmProductVariantGroup` and `ekmProductVariant` children without opening each content node. The U17/U18 web package registers a **Variants** workspace view and a footer variant count for `ekmProduct`. U10 uses the corresponding legacy backoffice app.

Install the matching Ekom web-assets package in the site's web project so `/App_Plugins/Ekom` is available. No separate feature flag is required.

## Supported editing

The current workspace view can:

- create variant groups and variants
- edit culture-specific titles
- edit group color and images
- edit variant SKU, prices by store/currency, stock by configured stock scope, and images
- expose configured custom text fields
- drag groups and variants to change sort order
- save draft changes or save and publish
- move selected groups/variants to the recycle bin

The UI loads stores/currencies and languages derived from the product's store context. It excludes trashed children and sorts by Umbraco sort order, then name.

Deleting a group or variant calls the authenticated backoffice API and moves the content to Umbraco's recycle bin. It is not a hard delete.

## Save behavior

The U17/U18 workspace tracks changes independently from the product editor:

- **Save variant changes** sends all changed/new groups and variants through the variant API.
- Saving the product also saves pending variant changes after the document save.
- Save and Publish asks for confirmation when variant changes are pending, then publishes changed variant content after the product.
- Stock values are applied through Ekom's stock API as part of variant saving.
- Required titles and required custom fields are validated before drawer save and before saving all changes.
- Closing a drawer does not validate because it does not persist the item.

The product workspace API is protected with `UmbracoUserAuthorize`; it is a backoffice API, not a storefront/headless catalog API.

## Custom fields

Configure additional fields under `Ekom:VariantApp`:

```json
{
  "Ekom": {
    "VariantApp": {
      "VariantGroups": [ "colorCode" ],
      "Variants": [ "material", "sizeLabel" ]
    }
  }
}
```

`VariantGroups` contains property aliases from `ekmProductVariantGroup`. `Variants` contains aliases from `ekmProductVariant`.

Only properties whose Umbraco editor alias is `Umbraco.TextBox` are included. An alias is ignored when it is blank, duplicated case-insensitively, missing from the composed content type, or uses another editor. Labels and required state come from the Umbraco property type.

Custom field values participate in change detection. Editing only a configured custom field marks the group/variant as changed. The server also validates mandatory values and returns a bad request when one is empty.

The feature intentionally does not render arbitrary property editors. Use the standard content editor or extend the product model/import pipeline for complex custom data.

## Backoffice routes

All current routes are under `/ekom/backoffice/Variants` and require an authenticated Umbraco user:

| Method and route | Purpose |
| --- | --- |
| `GET /{productId}` | Product, groups, variants, stores, languages and custom field definitions. |
| `GET /{productId}/Count` | Variant count for the workspace footer. |
| `POST /Groups` | Create one group. |
| `POST /` | Create one variant. |
| `POST /Save` | Save a product's complete submitted variant structure. |
| `POST /Groups/Save` | Save one group. |
| `POST /Items/Save` | Save one variant. |
| `DELETE /{nodeId}` | Move a group or variant to the recycle bin. |
| `GET /Media/Thumbnail` | Resolve a media thumbnail redirect. |

These endpoints are implementation details for the shipped workspace client. Site integrations should prefer Umbraco content services/import APIs rather than coupling external systems to the backoffice request models.

## Troubleshooting

- If the **Variants** view is missing in U17/U18, verify the matching `Ekom.Web.U17` or `Ekom.Web.U18` package is installed in the web project and its `/App_Plugins/Ekom/dist` files are deployed.
- If a custom field is missing, verify its alias on the correct content type and confirm its property editor alias is `Umbraco.TextBox`.
- If a save returns `400`, check required titles/custom fields and that submitted children still belong to the product/group being edited.
- If stock appears under the wrong scope, verify `Ekom:PerStoreStock` before editing values.
