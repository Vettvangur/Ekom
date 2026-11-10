import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { html, css } from '@umbraco-cms/backoffice/external/lit';
import { umbExtensionsRegistry } from '@umbraco-cms/backoffice/extension-registry';
import { UmbExtensionsManifestInitializer, createExtensionElement } from '@umbraco-cms/backoffice/extension-api';
import type { ManifestSectionView } from '@umbraco-cms/backoffice/section';
import type { UmbRoute } from '@umbraco-cms/backoffice/router';

/** Ekom supplies its own navigation; retain Umbraco routing and extension conditions. */
export class EkomManagerSectionElement extends UmbLitElement {
  private routes: UmbRoute[] = [];

  constructor() {
    super();
    new UmbExtensionsManifestInitializer(this, umbExtensionsRegistry, 'sectionView',
      (manifest) => manifest.alias.startsWith('Ekom.SectionView.Manager.'), (views) => {
        const routes: UmbRoute[] = views.map(({ manifest }) => ({
          path: `view/${manifest.meta.pathname}`,
          component: () => createExtensionElement(manifest),
          setup: (element) => { (element as HTMLElement & { manifest: ManifestSectionView }).manifest = manifest; },
        }));
        const orders = routes.find(route => route.path === 'view/orders');
        if (orders) routes.push({ ...orders, path: '' });
        routes.push({ path: '**', component: async () => (await import('@umbraco-cms/backoffice/router')).UmbRouteNotFoundElement });
        this.routes = routes;
        this.requestUpdate();
      });
  }

  override render() { return html`<main><umb-router-slot .routes=${this.routes}></umb-router-slot></main>`; }
  static override styles = css`:host { display:block; flex:1 1 auto; min-width:0; width:100%; height:100%; overflow:auto; } main { height:100%; }`;
}
customElements.define('ekom-manager-section', EkomManagerSectionElement);
export default EkomManagerSectionElement;
