var h = Object.defineProperty;
var p = (s, n, t) => n in s ? h(s, n, { enumerable: !0, configurable: !0, writable: !0, value: t }) : s[n] = t;
var i = (s, n, t) => p(s, typeof n != "symbol" ? n + "" : n, t);
import { UmbElementMixin as m } from "@umbraco-cms/backoffice/element-api";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT as f } from "@umbraco-cms/backoffice/document";
import { UMB_WORKSPACE_EDITOR_CONTEXT as k } from "@umbraco-cms/backoffice/workspace";
const c = "ekom-variant-count", d = "Ekom.WorkspaceView.Product.Variants";
class w extends m(HTMLElement) {
  constructor() {
    super(...arguments);
    i(this, "documentWorkspaceContext");
    i(this, "workspaceEditorContext");
    i(this, "productId", "");
    i(this, "requestId", 0);
  }
  connectedCallback() {
    super.connectedCallback(), this.style.display = "none", this.consumeContext(k, (t) => {
      this.workspaceEditorContext = t, this.updateBadge();
    }), this.consumeContext(f, (t) => {
      if (t == null)
        return;
      this.documentWorkspaceContext = t;
      const e = t.getUnique();
      e != null && (this.productId = e), this.observe(t.unique, (o) => {
        o == null || o === this.productId || (this.productId = o, this.updateBadge());
      }, "ekomVariantCountProductId"), this.updateBadge();
    });
  }
  disconnectedCallback() {
    this.requestId++, this.setBadge(0), super.disconnectedCallback();
  }
  async updateBadge() {
    var o;
    const t = this.productId || ((o = this.documentWorkspaceContext) == null ? void 0 : o.getUnique()) || "", e = ++this.requestId;
    if (!t) {
      await this.setBadge(0).catch(() => {
      });
      return;
    }
    try {
      const a = await this.fetchJson(`/ekom/backoffice/Variants/${encodeURIComponent(t)}/Count`);
      if (e !== this.requestId)
        return;
      await this.setBadge(a.count), this.setTabBadge(a.count);
    } catch {
      e === this.requestId && (await this.setBadge(0).catch(() => {
      }), this.setTabBadge(0));
    }
  }
  async setBadge(t) {
    var o;
    const e = await ((o = this.workspaceEditorContext) == null ? void 0 : o.getViewContext(d));
    e != null && (e.hints.has(c) && e.hints.removeOne(c), t > 0 && e.hints.addOne({
      unique: c,
      text: String(t),
      color: "default"
    }));
  }
  setTabBadge(t) {
    const e = () => {
      var u;
      const o = l(`[data-mark="workspace:view-link:${d}"]`);
      if (o == null)
        return !1;
      if ((u = o.querySelector("[data-ekom-variant-count-badge]")) == null || u.remove(), t <= 0)
        return !0;
      const a = o.querySelector('[slot="icon"]');
      if (a == null)
        return !1;
      const r = document.createElement("umb-badge");
      return r.setAttribute("data-ekom-variant-count-badge", ""), r.setAttribute("color", "default"), r.textContent = String(t), a.append(r), !0;
    };
    e() || (window.setTimeout(e, 100), window.setTimeout(e, 500));
  }
  async fetchJson(t) {
    const e = await fetch(t, {
      credentials: "same-origin",
      headers: {
        Accept: "application/json"
      }
    });
    if (!e.ok)
      throw new Error(`Request failed with status ${e.status}`);
    return await e.json();
  }
}
function l(s, n = document) {
  const t = n.querySelector(s);
  if (t != null)
    return t;
  const e = n.querySelectorAll("*");
  for (const o of e) {
    if (o.shadowRoot == null)
      continue;
    const a = l(s, o.shadowRoot);
    if (a != null)
      return a;
  }
  return null;
}
customElements.define("ekom-variant-count-workspace-footer-app", w);
export {
  w as default
};
