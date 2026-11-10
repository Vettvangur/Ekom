var u = Object.defineProperty;
var h = (o, t, e) => t in o ? u(o, t, { enumerable: !0, configurable: !0, writable: !0, value: e }) : o[t] = e;
var r = (o, t, e) => h(o, typeof t != "symbol" ? t + "" : t, e);
import { UmbLitElement as p } from "@umbraco-cms/backoffice/lit-element";
import { html as c, css as l } from "@umbraco-cms/backoffice/external/lit";
import { umbExtensionsRegistry as d } from "@umbraco-cms/backoffice/extension-registry";
import { UmbExtensionsManifestInitializer as f, createExtensionElement as w } from "@umbraco-cms/backoffice/extension-api";
class m extends p {
  constructor() {
    super();
    r(this, "routes", []);
    new f(
      this,
      d,
      "sectionView",
      (e) => e.alias.startsWith("Ekom.SectionView.Manager."),
      (e) => {
        const i = e.map(({ manifest: s }) => ({
          path: `view/${s.meta.pathname}`,
          component: () => w(s),
          setup: (a) => {
            a.manifest = s;
          }
        })), n = i.find((s) => s.path === "view/orders");
        n && i.push({ ...n, path: "" }), i.push({ path: "**", component: async () => (await import("@umbraco-cms/backoffice/router")).UmbRouteNotFoundElement }), this.routes = i, this.requestUpdate();
      }
    );
  }
  render() {
    return c`<main><umb-router-slot .routes=${this.routes}></umb-router-slot></main>`;
  }
}
r(m, "styles", l`:host { display:block; flex:1 1 auto; min-width:0; width:100%; height:100%; overflow:auto; } main { height:100%; }`);
customElements.define("ekom-manager-section", m);
export {
  m as EkomManagerSectionElement,
  m as default
};
