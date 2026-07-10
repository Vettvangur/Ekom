const e = [
  {
    type: "section",
    kind: "default",
    alias: "ekommanager",
    name: "Ekom Manager Section",
    weight: 300,
    meta: {
      label: "Ekom",
      pathname: "ekommanager",
      preventUrlRetention: !0
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionUserPermission",
        match: "ekommanager"
      }
    ]
  },
  {
    type: "sectionView",
    alias: "Ekom.SectionView.Manager.Orders",
    name: "Ekom Manager Orders Section View",
    element: "/App_Plugins/Ekom/dist/orders-section-view.element.js",
    weight: 200,
    meta: {
      label: "Orders",
      pathname: "orders",
      icon: "icon-shopping-basket"
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "ekommanager"
      }
    ]
  },
  {
    type: "sectionView",
    alias: "Ekom.SectionView.Manager.Analytics",
    name: "Ekom Manager Analytics Section View",
    element: "/App_Plugins/Ekom/dist/analytics-section-view.element.js",
    weight: 100,
    meta: {
      label: "Analytics",
      pathname: "analytics",
      icon: "icon-chart-curve"
    },
    conditions: [
      {
        alias: "Umb.Condition.SectionAlias",
        match: "ekommanager"
      }
    ]
  },
  {
    type: "workspaceView",
    alias: "Ekom.WorkspaceView.Product.Variants",
    name: "Ekom Product Variants Workspace View",
    element: "/App_Plugins/Ekom/dist/variants-workspace-view.element.js",
    weight: 90,
    meta: {
      label: "Variants",
      pathname: "variants",
      icon: "icon-layers-alt"
    },
    conditions: [
      {
        alias: "Umb.Condition.WorkspaceAlias",
        match: "Umb.Workspace.Document"
      },
      {
        alias: "Umb.Condition.WorkspaceContentTypeAlias",
        match: "ekmProduct"
      }
    ]
  },
  {
    type: "workspaceFooterApp",
    alias: "Ekom.WorkspaceFooterApp.Product.VariantCount",
    name: "Ekom Product Variant Count Workspace Footer App",
    element: "/App_Plugins/Ekom/dist/variant-count-workspace-footer-app.element.js",
    conditions: [
      {
        alias: "Umb.Condition.WorkspaceAlias",
        match: "Umb.Workspace.Document"
      },
      {
        alias: "Umb.Condition.WorkspaceContentTypeAlias",
        match: "ekmProduct"
      }
    ]
  },
  {
    type: "propertyEditorSchema",
    alias: "Ekom.Cache",
    name: "Ekom Cache Editor",
    meta: {
      defaultPropertyEditorUiAlias: "Ekom.PropertyEditorUi.Cache"
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.Cache",
    name: "Ekom Cache Editor UI",
    element: "/App_Plugins/Ekom/dist/cache-editor.element.js",
    meta: {
      label: "Ekom Cache",
      propertyEditorSchemaAlias: "Ekom.Cache",
      icon: "icon-time",
      group: "Ekom",
      supportsReadOnly: !0
    }
  },
  {
    type: "propertyEditorSchema",
    alias: "Ekom.Price",
    name: "Ekom Price Editor",
    meta: {
      defaultPropertyEditorUiAlias: "Ekom.PropertyEditorUi.Price"
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.Price",
    name: "Ekom Price Editor UI",
    element: "/App_Plugins/Ekom/dist/price-editor.element.js",
    meta: {
      label: "Ekom Price",
      propertyEditorSchemaAlias: "Ekom.Price",
      icon: "icon-bill-dollar",
      group: "Ekom",
      supportsReadOnly: !0
    }
  },
  {
    type: "propertyEditorSchema",
    alias: "Ekom.Coupon",
    name: "Ekom Coupon Editor",
    meta: {
      defaultPropertyEditorUiAlias: "Ekom.PropertyEditorUi.Coupon"
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.Coupon",
    name: "Ekom Coupon Editor UI",
    element: "/App_Plugins/Ekom/dist/coupon-editor.element.js",
    meta: {
      label: "Ekom Coupon",
      propertyEditorSchemaAlias: "Ekom.Coupon",
      icon: "icon-ticket",
      group: "Ekom",
      supportsReadOnly: !0
    }
  },
  {
    type: "propertyEditorSchema",
    alias: "Ekom.Stock",
    name: "Ekom Stock Editor",
    meta: {
      defaultPropertyEditorUiAlias: "Ekom.PropertyEditorUi.Stock"
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.Stock",
    name: "Ekom Stock Editor UI",
    element: "/App_Plugins/Ekom/dist/stock-editor.element.js",
    meta: {
      label: "Ekom Stock",
      propertyEditorSchemaAlias: "Ekom.Stock",
      icon: "icon-box",
      group: "Ekom",
      supportsReadOnly: !0
    }
  },
  {
    type: "propertyEditorSchema",
    alias: "Ekom.Range",
    name: "Ekom Range Editor",
    meta: {
      defaultPropertyEditorUiAlias: "Ekom.PropertyEditorUi.Range"
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.Range",
    name: "Ekom Range Editor UI",
    element: "/App_Plugins/Ekom/dist/range-editor.element.js",
    meta: {
      label: "Ekom Range",
      propertyEditorSchemaAlias: "Ekom.Range",
      icon: "icon-navigation-horizontal",
      group: "Ekom",
      supportsReadOnly: !0
    }
  },
  {
    type: "propertyEditorSchema",
    alias: "Ekom.Currency",
    name: "Ekom Currency Picker",
    meta: {
      defaultPropertyEditorUiAlias: "Ekom.PropertyEditorUi.Currency"
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.Currency",
    name: "Ekom Currency Picker UI",
    element: "/App_Plugins/Ekom/dist/currency-picker.element.js",
    meta: {
      label: "Ekom Currency",
      propertyEditorSchemaAlias: "Ekom.Currency",
      icon: "icon-coins-dollar-alt",
      group: "Ekom",
      supportsReadOnly: !0
    }
  },
  {
    type: "propertyEditorSchema",
    alias: "Ekom.Country",
    name: "Ekom Country Picker",
    meta: {
      defaultPropertyEditorUiAlias: "Ekom.PropertyEditorUi.Country"
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.Country",
    name: "Ekom Country Picker UI",
    element: "/App_Plugins/Ekom/dist/country-picker.element.js",
    meta: {
      label: "Ekom Country",
      propertyEditorSchemaAlias: "Ekom.Country",
      icon: "icon-globe",
      group: "Ekom",
      supportsReadOnly: !0
    }
  },
  {
    type: "propertyEditorSchema",
    alias: "Ekom.Zone",
    name: "Ekom Zone Picker",
    meta: {
      defaultPropertyEditorUiAlias: "Ekom.PropertyEditorUi.Zone"
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.Zone",
    name: "Ekom Zone Picker UI",
    element: "/App_Plugins/Ekom/dist/zone-picker.element.js",
    meta: {
      label: "Ekom Zone",
      propertyEditorSchemaAlias: "Ekom.Zone",
      icon: "icon-globe-alt",
      group: "Ekom",
      supportsReadOnly: !0
    }
  },
  {
    type: "propertyEditorSchema",
    alias: "Ekom.Metafield",
    name: "Ekom Metafield Picker",
    meta: {
      defaultPropertyEditorUiAlias: "Ekom.PropertyEditorUi.Metafield"
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.Metafield",
    name: "Ekom Metafield Picker UI",
    element: "/App_Plugins/Ekom/dist/metafield-picker.element.js",
    meta: {
      label: "Ekom Metafield",
      propertyEditorSchemaAlias: "Ekom.Metafield",
      icon: "icon-tags",
      group: "Ekom",
      supportsReadOnly: !0
    }
  },
  {
    type: "propertyEditorSchema",
    alias: "Ekom.Metavalue",
    name: "Ekom Metavalue Editor",
    meta: {
      defaultPropertyEditorUiAlias: "Ekom.PropertyEditorUi.Metavalue"
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.Metavalue",
    name: "Ekom Metavalue Editor UI",
    element: "/App_Plugins/Ekom/dist/metavalue-editor.element.js",
    meta: {
      label: "Ekom Metavalue",
      propertyEditorSchemaAlias: "Ekom.Metavalue",
      icon: "icon-ordered-list",
      group: "Ekom",
      supportsReadOnly: !0
    }
  },
  {
    type: "propertyEditorSchema",
    alias: "Ekom.Property",
    name: "Ekom Property Editor",
    meta: {
      defaultPropertyEditorUiAlias: "Ekom.PropertyEditorUi.Property",
      settings: {
        properties: [
          {
            alias: "dataType",
            label: "Data Type",
            description: "Select the data type to wrap.",
            propertyEditorUiAlias: "Ekom.PropertyEditorUi.DataTypePicker"
          },
          {
            alias: "useLanguages",
            label: "Use Languages",
            description: "Defaults to stores. Select this to use languages instead.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.Toggle"
          },
          {
            alias: "hideLabel",
            label: "Hide Label",
            description: "Hide the Umbraco property title and description.",
            propertyEditorUiAlias: "Umb.PropertyEditorUi.Toggle"
          }
        ]
      }
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.DataTypePicker",
    name: "Ekom Data Type Picker UI",
    element: "/App_Plugins/Ekom/dist/data-type-picker.element.js",
    meta: {
      label: "Ekom Data Type Picker",
      icon: "icon-autofill",
      group: "Ekom",
      supportsReadOnly: !0
    }
  },
  {
    type: "propertyEditorUi",
    alias: "Ekom.PropertyEditorUi.Property",
    name: "Ekom Property Editor UI",
    element: "/App_Plugins/Ekom/dist/property-editor.element.js",
    meta: {
      label: "Ekom Property",
      propertyEditorSchemaAlias: "Ekom.Property",
      icon: "icon-autofill",
      group: "Ekom",
      supportsReadOnly: !0
    }
  }
];
export {
  e as manifests
};
