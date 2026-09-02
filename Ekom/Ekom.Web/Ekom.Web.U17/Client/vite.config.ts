import { defineConfig } from 'vite';

export function createViteConfig(sourceRoot: string, outDir: string) {
  return defineConfig({
    build: {
      emptyOutDir: true,
      outDir,
      lib: {
        entry: {
          manifests: `${sourceRoot}/manifests.ts`,
          'cache-editor.element': `${sourceRoot}/property-editors/cache-editor.element.ts`,
          'country-picker.element': `${sourceRoot}/property-editors/country-picker.element.ts`,
          'coupon-editor.element': `${sourceRoot}/property-editors/coupon-editor.element.ts`,
          'currency-picker.element': `${sourceRoot}/property-editors/currency-picker.element.ts`,
          'data-type-picker.element': `${sourceRoot}/property-editors/data-type-picker.element.ts`,
          'metafield-picker.element': `${sourceRoot}/property-editors/metafield-picker.element.ts`,
          'metavalue-editor.element': `${sourceRoot}/property-editors/metavalue-editor.element.ts`,
          'analytics-section-view.element': `${sourceRoot}/manager/analytics-section-view.element.ts`,
          'catalog-collection-view.element': `${sourceRoot}/catalog/catalog-collection-view.element.ts`,
          'orders-section-view.element': `${sourceRoot}/manager/orders-section-view.element.ts`,
          'price-editor.element': `${sourceRoot}/property-editors/price-editor.element.ts`,
          'property-editor.element': `${sourceRoot}/property-editors/property-editor.element.ts`,
          'range-editor.element': `${sourceRoot}/property-editors/range-editor.element.ts`,
          'sku-product-picker.element': `${sourceRoot}/property-editors/sku-product-picker.element.ts`,
          'stock-editor.element': `${sourceRoot}/property-editors/stock-editor.element.ts`,
          'variant-count-workspace-footer-app.element': `${sourceRoot}/variants/variant-count-workspace-footer-app.element.ts`,
          'variants-workspace-view.element': `${sourceRoot}/variants/variants-workspace-view.element.ts`,
          'zone-picker.element': `${sourceRoot}/property-editors/zone-picker.element.ts`,
        },
        formats: ['es'],
      },
      rollupOptions: {
        external: [/^@umbraco-cms\/backoffice/],
        output: {
          entryFileNames: '[name].js',
          chunkFileNames: '[name].js',
          assetFileNames: '[name][extname]',
        },
      },
    },
  });
}

export default createViteConfig('src', '../App_Plugins/Ekom/dist');
