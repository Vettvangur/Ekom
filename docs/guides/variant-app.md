[Ekom docs](../README.md) / [Guides](README.md) / Variant App

# Variant App

The Variant App lets editors manage all variant groups and variants of a product in one view, without opening each content node. Use it when a product has many variants, for example a shirt in several colors and sizes.

In Ekom, a variant group (`ekmProductVariantGroup`) groups variants, for example one color. A variant (`ekmProductVariant`) is the item the customer buys, for example one size in that color. Both are child nodes of the product (`ekmProduct`).

On this page:

- [Open the Variants view](#open-the-variants-view)
- [What editors can do](#what-editors-can-do)
- [How saving works](#how-saving-works)
- [Add your own text fields](#add-your-own-text-fields)
- [Backoffice routes](#backoffice-routes)
- [Fix common problems](#fix-common-problems)

## Open the Variants view

The view is part of Ekom's web assets package for Umbraco 17, 18 and 19. Install the matching Ekom web assets package in the site's web project, so that `/App_Plugins/Ekom` is served. There is no setting to turn it on.

On a product node, the backoffice shows:

- a **Variants** tab in the product workspace,
- the number of variants in the workspace footer.

## What editors can do

In the Variants view an editor can:

- create variant groups and variants,
- edit titles for each language,
- edit the color and images of a group,
- edit the SKU, images, prices and stock of a variant,
- edit the text fields you add in configuration (see [Add your own text fields](#add-your-own-text-fields)),
- drag groups and variants to change their order,
- delete groups and variants.

Prices are entered for each store and currency of the product's stores. Titles are entered for the languages of those stores.

Stock is entered for each store when `Ekom:PerStoreStock` is `true` and the site has more than one store. Otherwise there is one stock value for all stores. See [Stock](stock.md#choose-one-shared-count-or-a-count-per-store).

The view lists the groups and variants that are not in the recycle bin, ordered by their Umbraco sort order and then by name.

## How saving works

The Variants view keeps track of its own changes, apart from the product editor. Nothing is written until the editor saves.

### Saving publishes the changed variants

- **Save variant changes** at the top of the view saves every new and changed group and variant, and **publishes** them.
- **Save** on the product saves the product first. If there are variant changes, it then saves and publishes them too, in the same way.
- **Save and Publish** on the product asks the editor to confirm when there are variant changes. If the editor confirms, Ekom publishes the product first, then saves and publishes the variant changes. If the editor cancels, nothing is saved.

Only the groups and variants that changed are saved. A variant whose values did not change is not saved again.

To publish groups and variants, the backoffice user needs permission to update and to publish the content. Without it the save returns `403`.

### Deleting

Deleting a group or variant asks for confirmation and removes it from the view. The delete is sent when the editor saves. Ekom moves the node to Umbraco's recycle bin; it does not delete it permanently. A group in the recycle bin keeps its variants.

### Checks before saving

Titles are required. Custom fields marked as mandatory in Umbraco are required. The view checks both when the editor clicks save in the edit drawer, and before it saves all changes. Saving the drawer only keeps the edits in the view; they are written when the editor saves the variant changes. Closing a drawer does not check anything.

### Stock and prices

Stock values are written through Ekom's stock API every time a variant is saved, as a replacement of the stored count. See [Stock](stock.md#changing).

When `Ekom:UseDatabasePrices` is `true`, prices are written to the price table instead of the variant node, and only the prices the editor changed are written. If another user changed the same price after the view loaded it, the save returns `409 Conflict`; reload the view and try again. See [Database price storage](database-price-storage.md).

Content, stock and prices are not saved in one transaction. If one part fails, the parts already saved stay saved.

## Add your own text fields

Add text properties of your own document types to the view under `Ekom:VariantApp`:

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

- `VariantGroups` lists property aliases from `ekmProductVariantGroup`.
- `Variants` lists property aliases from `ekmProductVariant`.

A property is shown only when its property editor is `Umbraco.TextBox`. Ekom skips an alias that is blank, listed twice (upper and lower case are ignored), missing from the document type and its compositions, or that uses another editor. The label and whether the field is required come from the property type in Umbraco.

Changing only a custom field counts as a change, so the group or variant is saved. The server also checks required fields and returns `400 Bad Request` when one is empty.

The view only shows text fields. It does not show other property editors. For other data, use the normal content editor, or set the values through the [catalog import](imports.md).

## Backoffice routes

The view talks to these routes. All are under `/ekom/backoffice/Variants` and need a signed-in Umbraco backoffice user who can edit the content.

| Method and route | What it does |
| --- | --- |
| `GET /{productId}` | Returns the product, groups, variants, stores, languages and custom field definitions. |
| `GET /{productId}/Count` | Returns the number of variants, for the workspace footer. |
| `POST /Groups` | Creates one group. |
| `POST /` | Creates one variant. |
| `POST /Save` | Saves the submitted groups and variants of a product. |
| `POST /Groups/Save` | Saves one group. |
| `POST /Items/Save` | Saves one variant. |
| `DELETE /{nodeId}` | Moves a group or variant to the recycle bin. Other node types are not touched, and the route returns `false`. |
| `GET /Media/Thumbnail` | Redirects to a thumbnail of a media item. |

`productId` and `nodeId` can be an integer ID or a GUID key.

| Response | When |
| --- | --- |
| `400 Bad Request` | A title or required custom field is empty, a node is not of the expected type, or (when saving one group or variant) it does not belong to the product or group sent. |
| `403 Forbidden` | The user cannot edit the content, cannot publish it, or has no access to a store whose price changed. |
| `404 Not Found` | The product, group or variant does not exist. |
| `409 Conflict` | With database prices: a price changed after the view loaded it. |

When `POST /Save` gets a group or variant that does not belong to the product or group, Ekom skips it without an error.

These routes exist for the shipped backoffice view. Their request models can change. Do not connect external systems to them; use Umbraco's content services or the [catalog import](imports.md) instead.

## Fix common problems

- **The Variants tab is missing.** Check that the matching Ekom web assets package (`Ekom.Web.U17`, `Ekom.Web.U18` or `Ekom.Web.U19`) is installed in the web project, and that its `/App_Plugins/Ekom/dist` files are deployed.
- **A custom field is missing.** Check that the alias is on the right document type, and that its property editor is `Umbraco.TextBox`.
- **Saving returns 400.** Check titles and required custom fields. When saving one item, check that it still belongs to the product or group you are editing.
- **Saving returns 403.** Check that the user can update and publish the product's content, and has access to the stores whose prices changed.
- **Stock shows under the wrong store.** Check `Ekom:PerStoreStock` before you edit stock values.

## Related pages

- [Catalog and products](catalog-and-products.md): how products, variant groups and variants are read on the storefront.
- [Stock](stock.md): what the stock values mean and how availability is decided.
- [Database price storage](database-price-storage.md): edit prices without publishing.
- [Catalog imports](imports.md): create and update variants from another system.
- [Configuration reference](../reference/configuration-reference.md): all `Ekom` settings.

[Back to Guides](README.md)
