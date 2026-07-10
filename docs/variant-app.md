# Variant App custom fields

The Ekom backoffice Variant App can show additional editable text fields in the variant group and variant drawers. This is useful when projects need a small number of extra textstring properties without opening each variant node separately.

## Configuration

Configure the aliases under the existing `Ekom` section in `appsettings.json`:

```json
{
  "Ekom": {
    "VariantApp": {
      "VariantGroups": [ "color" ],
      "Variants": [ "material", "size" ]
    }
  }
}
```

- `VariantGroups` contains property aliases from the `ekmProductVariantGroup` document type.
- `Variants` contains property aliases from the `ekmProductVariant` document type.

## Supported properties

Only Umbraco textstring properties are rendered by the Variant App custom field UI.

Configured aliases are ignored when:

- the alias does not exist on the target document type
- the property is not a textstring property
- the alias is empty or duplicated

## Labels and required validation

Field metadata comes from the Umbraco document type property itself:

- the drawer label uses the property name
- required validation uses the property's mandatory setting

Required fields are validated before drawer Save and before saving all variant changes. The drawer Close action does not validate because it only dismisses the drawer.

## Saving behavior

Custom field changes are included in the Variant App change detection. Updating only a configured custom field is enough to mark the related variant group or variant as changed and save it through the normal Variant App save flow.
