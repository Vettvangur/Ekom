var a = Object.defineProperty;
var d = (o, n, e) => n in o ? a(o, n, { enumerable: !0, configurable: !0, writable: !0, value: e }) : o[n] = e;
var r = (o, n, e) => d(o, typeof n != "symbol" ? n + "" : n, e);
import { UmbChangeEvent as l } from "@umbraco-cms/backoffice/event";
import { createExtensionElement as u } from "@umbraco-cms/backoffice/extension-api";
import { umbExtensionsRegistry as c } from "@umbraco-cms/backoffice/extension-registry";
import { UmbPropertyEditorConfigCollection as h } from "@umbraco-cms/backoffice/property-editor";
class y extends HTMLElement {
  constructor() {
    super(...arguments);
    r(this, "manifest");
    r(this, "name");
    r(this, "dataSourceAlias");
    r(this, "config");
    r(this, "mandatory");
    r(this, "mandatoryMessage");
    r(this, "nativeEditor");
    r(this, "skus", []);
  }
  get value() {
    return this.skus;
  }
  set value(e) {
    this.skus = this.normalizeSkus(e), this.syncNativeValue();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncReadOnlyState();
  }
  connectedCallback() {
    this.createNativeEditor();
  }
  disconnectedCallback() {
    var e, t;
    (t = (e = this.nativeEditor) == null ? void 0 : e.destroy) == null || t.call(e);
  }
  async createNativeEditor() {
    const e = c.getByAlias("Umb.PropertyEditorUi.ContentPicker");
    if (e == null)
      throw new Error("Could not find the Umbraco Content Picker property editor UI.");
    const t = await u(e);
    if (t == null)
      throw new Error("Could not create the Umbraco Content Picker property editor UI.");
    t.manifest = e, t.name = this.name, t.config = this.config ?? new h([]), t.readonly = this.readonly, t.mandatory = this.mandatory, t.mandatoryMessage = this.mandatoryMessage, t.toggleAttribute("readonly", this.readonly), t.addEventListener("change", (i) => this.onNativeEditorChange(i)), t.addEventListener("property-value-change", (i) => this.onNativeEditorChange(i)), this.nativeEditor = t, this.replaceChildren(t), await this.syncNativeValue();
  }
  async syncNativeValue() {
    if (this.nativeEditor == null)
      return;
    const e = await this.post("skus", { skus: this.skus });
    this.nativeEditor.value = e.map((t) => ({
      type: "document",
      unique: t.key
    }));
  }
  async onNativeEditorChange(e) {
    if (e.stopPropagation(), this.nativeEditor == null)
      return;
    const t = this.getKeys(this.nativeEditor.value), i = await this.post("keys", { keys: t });
    this.skus = i.map((s) => s.sku), this.dispatchEvent(new l());
  }
  getKeys(e) {
    return Array.isArray(e) ? e.map((t) => typeof t == "object" && t != null && "unique" in t ? t.unique : void 0).filter((t) => typeof t == "string") : [];
  }
  normalizeSkus(e) {
    return Array.isArray(e) ? e.filter((t) => typeof t == "string").map((t) => t.trim()).filter((t) => t.length > 0) : [];
  }
  syncReadOnlyState() {
    this.nativeEditor != null && (this.nativeEditor.readonly = this.readonly, this.nativeEditor.toggleAttribute("readonly", this.readonly));
  }
  async post(e, t) {
    const i = await fetch(`/ekom/backoffice/SkuProductPicker/${e}`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json"
      },
      body: JSON.stringify(t)
    });
    if (!i.ok)
      throw new Error(`Could not resolve selected products (${i.status}).`);
    return i.json();
  }
}
customElements.define("ekom-sku-product-picker", y);
export {
  y as EkomSkuProductPickerElement,
  y as default
};
