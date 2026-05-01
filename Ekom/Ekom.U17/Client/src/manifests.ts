import { propertyEditorManifests } from './property-editor-manifests';

export const manifests: Array<UmbExtensionManifest> = [
  {
    type: 'section',
    alias: 'Ekom.Section',
    name: 'Ekom Section',
    meta: {
      label: 'Ekom',
      pathname: 'ekom',
    },
  },
  {
    type: 'sectionView',
    alias: 'Ekom.SectionView.Orders',
    name: 'Ekom Orders Section View',
    element: '/App_Plugins/Ekom/dist/ekom-section-view.element.js',
    meta: {
      label: 'Orders',
      icon: 'icon-shopping-basket',
      pathname: 'orders',
    },
    conditions: [
      {
        alias: 'Umb.Condition.SectionAlias',
        match: 'Ekom.Section',
      },
    ],
  },
  ...propertyEditorManifests,
];
