const propertyEditors = [
  { schema: 'Ekom.Price', ui: 'Ekom.PropertyEditorUi.Price', name: 'Ekom Price Editor', label: 'Ekom Price', icon: 'icon-coins' },
  { schema: 'Ekom.Stock', ui: 'Ekom.PropertyEditorUi.Stock', name: 'Ekom Stock Editor', label: 'Ekom Stock', icon: 'icon-box' },
  { schema: 'Ekom.Coupon', ui: 'Ekom.PropertyEditorUi.Coupon', name: 'Ekom Coupon Editor', label: 'Ekom Coupon', icon: 'icon-barcode' },
  { schema: 'Ekom.Currency', ui: 'Ekom.PropertyEditorUi.Currency', name: 'Ekom Currency Picker', label: 'Ekom Currency', icon: 'icon-coins-dollar' },
  { schema: 'Ekom.Country', ui: 'Ekom.PropertyEditorUi.Country', name: 'Ekom Country Picker', label: 'Ekom Country', icon: 'icon-globe' },
  { schema: 'Ekom.Metafield', ui: 'Ekom.PropertyEditorUi.Metafield', name: 'Ekom Metafield Picker', label: 'Ekom Metafield', icon: 'icon-tags' },
  { schema: 'Ekom.Metavalue', ui: 'Ekom.PropertyEditorUi.Metavalue', name: 'Ekom Metavalue Editor', label: 'Ekom Metavalue', icon: 'icon-tags' },
  { schema: 'Ekom.Range', ui: 'Ekom.PropertyEditorUi.Range', name: 'Ekom Range Editor', label: 'Ekom Range', icon: 'icon-navigation-horizontal' },
  { schema: 'Ekom.Cache', ui: 'Ekom.PropertyEditorUi.Cache', name: 'Ekom Cache Editor', label: 'Ekom Cache', icon: 'icon-time' },
  { schema: 'Ekom.Zone', ui: 'Ekom.PropertyEditorUi.Zone', name: 'Ekom Zone Picker', label: 'Ekom Zone', icon: 'icon-map-location' },
  { schema: 'Ekom.Property', ui: 'Ekom.PropertyEditorUi.Property', name: 'Ekom Property Editor', label: 'Ekom Property', icon: 'icon-settings' },
] as const;

export const propertyEditorManifests: Array<UmbExtensionManifest> = propertyEditors.flatMap((editor) => [
  {
    type: 'propertyEditorSchema',
    alias: editor.schema,
    name: editor.name,
    meta: {
      defaultPropertyEditorUiAlias: editor.ui,
    },
  },
  {
    type: 'propertyEditorUi',
    alias: editor.ui,
    name: `${editor.name} UI`,
    element: '/App_Plugins/Ekom/dist/ekom-property-editor.element.js',
    meta: {
      label: editor.label,
      propertyEditorSchemaAlias: editor.schema,
      icon: editor.icon,
      group: 'Ekom',
      supportsReadOnly: true,
    },
  },
]);
