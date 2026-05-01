import { defineConfig } from 'vite';

export default defineConfig({
  build: {
    emptyOutDir: true,
    outDir: '../wwwroot/App_Plugins/Ekom/dist',
    lib: {
      entry: {
        manifests: 'src/manifests.ts',
        'ekom-section-view.element': 'src/section/ekom-section-view.element.ts',
        'ekom-property-editor.element': 'src/property-editors/ekom-property-editor.element.ts',
      },
      formats: ['es'],
    },
    rollupOptions: {
      output: {
        entryFileNames: '[name].js',
        chunkFileNames: '[name].js',
        assetFileNames: '[name][extname]',
      },
    },
  },
});
