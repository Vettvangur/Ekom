import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    emptyOutDir: true,
    outDir: '../App_Plugins/Ekom/dist',
    lib: {
      entry: {
        manifests: 'src/manifests.ts',
        'cache-editor.element': 'src/property-editors/cache-editor.element.ts',
        'country-picker.element': 'src/property-editors/country-picker.element.ts',
        'coupon-editor.element': 'src/property-editors/coupon-editor.element.ts',
        'currency-picker.element': 'src/property-editors/currency-picker.element.ts',
        'data-type-picker.element': 'src/property-editors/data-type-picker.element.ts',
        'metafield-picker.element': 'src/property-editors/metafield-picker.element.ts',
        'metavalue-editor.element': 'src/property-editors/metavalue-editor.element.ts',
        'analytics-section-view.element': 'src/manager/analytics-section-view.element.ts',
        'orders-section-view.element': 'src/manager/orders-section-view.element.ts',
        'price-editor.element': 'src/property-editors/price-editor.element.ts',
        'property-editor.element': 'src/property-editors/property-editor.element.ts',
        'range-editor.element': 'src/property-editors/range-editor.element.ts',
        'stock-editor.element': 'src/property-editors/stock-editor.element.ts',
        'zone-picker.element': 'src/property-editors/zone-picker.element.ts',
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
