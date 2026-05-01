//#endregion
//#region src/manifests.ts
var e = [
	{
		type: "section",
		alias: "Ekom.Section",
		name: "Ekom Section",
		meta: {
			label: "Ekom",
			pathname: "ekom"
		}
	},
	{
		type: "sectionView",
		alias: "Ekom.SectionView.Orders",
		name: "Ekom Orders Section View",
		element: "/App_Plugins/Ekom/dist/ekom-section-view.element.js",
		meta: {
			label: "Orders",
			icon: "icon-shopping-basket",
			pathname: "orders"
		},
		conditions: [{
			alias: "Umb.Condition.SectionAlias",
			match: "Ekom.Section"
		}]
	},
	...[
		{
			schema: "Ekom.Price",
			ui: "Ekom.PropertyEditorUi.Price",
			name: "Ekom Price Editor",
			label: "Ekom Price",
			icon: "icon-coins"
		},
		{
			schema: "Ekom.Stock",
			ui: "Ekom.PropertyEditorUi.Stock",
			name: "Ekom Stock Editor",
			label: "Ekom Stock",
			icon: "icon-box"
		},
		{
			schema: "Ekom.Coupon",
			ui: "Ekom.PropertyEditorUi.Coupon",
			name: "Ekom Coupon Editor",
			label: "Ekom Coupon",
			icon: "icon-barcode"
		},
		{
			schema: "Ekom.Currency",
			ui: "Ekom.PropertyEditorUi.Currency",
			name: "Ekom Currency Picker",
			label: "Ekom Currency",
			icon: "icon-coins-dollar"
		},
		{
			schema: "Ekom.Country",
			ui: "Ekom.PropertyEditorUi.Country",
			name: "Ekom Country Picker",
			label: "Ekom Country",
			icon: "icon-globe"
		},
		{
			schema: "Ekom.Metafield",
			ui: "Ekom.PropertyEditorUi.Metafield",
			name: "Ekom Metafield Picker",
			label: "Ekom Metafield",
			icon: "icon-tags"
		},
		{
			schema: "Ekom.Metavalue",
			ui: "Ekom.PropertyEditorUi.Metavalue",
			name: "Ekom Metavalue Editor",
			label: "Ekom Metavalue",
			icon: "icon-tags"
		},
		{
			schema: "Ekom.Range",
			ui: "Ekom.PropertyEditorUi.Range",
			name: "Ekom Range Editor",
			label: "Ekom Range",
			icon: "icon-navigation-horizontal"
		},
		{
			schema: "Ekom.Cache",
			ui: "Ekom.PropertyEditorUi.Cache",
			name: "Ekom Cache Editor",
			label: "Ekom Cache",
			icon: "icon-time"
		},
		{
			schema: "Ekom.Zone",
			ui: "Ekom.PropertyEditorUi.Zone",
			name: "Ekom Zone Picker",
			label: "Ekom Zone",
			icon: "icon-map-location"
		},
		{
			schema: "Ekom.Property",
			ui: "Ekom.PropertyEditorUi.Property",
			name: "Ekom Property Editor",
			label: "Ekom Property",
			icon: "icon-settings"
		}
	].flatMap((e) => [{
		type: "propertyEditorSchema",
		alias: e.schema,
		name: e.name,
		meta: { defaultPropertyEditorUiAlias: e.ui }
	}, {
		type: "propertyEditorUi",
		alias: e.ui,
		name: `${e.name} UI`,
		element: "/App_Plugins/Ekom/dist/ekom-property-editor.element.js",
		meta: {
			label: e.label,
			propertyEditorSchemaAlias: e.schema,
			icon: e.icon,
			group: "Ekom",
			supportsReadOnly: !0
		}
	}])
];
//#endregion
export { e as manifests };
