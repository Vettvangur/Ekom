import { build } from 'vite';
import { fileURLToPath } from 'node:url';

// Umbraco 13 has no modern backoffice context API. Build its standalone view
// from the same source with plain custom-element lifecycle hooks.
await build({
  configFile: false,
  plugins: [{
    name: 'classic-printing-element',
    enforce: 'pre',
    resolveId(id) {
      if (id === '@umbraco-cms/backoffice/element-api') return '\0classic-element-api';
    },
    load(id) {
      if (id === '\0classic-element-api') return `
        export const UmbElementMixin = (Base) => class extends Base {
          connectedCallback() {}
          disconnectedCallback() {}
        };
      `;
    },
  }],
  build: {
    emptyOutDir: false,
    outDir: fileURLToPath(new URL('../../../Ekom.Web/App_Plugins/Ekom/Manager/libs', import.meta.url)),
    lib: {
      entry: fileURLToPath(new URL('../src/manager/printing-section-view.element.ts', import.meta.url)),
      formats: ['es'],
      fileName: () => 'printing-section-view.element.js',
    },
  },
});
