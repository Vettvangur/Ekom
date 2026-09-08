var l = Object.defineProperty;
var h = (u, i, e) => i in u ? l(u, i, { enumerable: !0, configurable: !0, writable: !0, value: e }) : u[i] = e;
var n = (u, i, e) => h(u, typeof i != "symbol" ? i + "" : i, e);
import { UmbChangeEvent as p } from "@umbraco-cms/backoffice/event";
class f extends HTMLElement {
  constructor() {
    super(...arguments);
    n(this, "manifest");
    n(this, "name");
    n(this, "dataSourceAlias");
    n(this, "config");
    n(this, "editor");
    n(this, "status");
    n(this, "warehouseStock", { sku: "", items: [] });
  }
  get value() {
    return this.warehouseStock;
  }
  set value(e) {
    this.warehouseStock = this.normalizeValue(e), this.syncInputs();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncDisabledState();
  }
  connectedCallback() {
    this.renderShell(), this.loadWarehouseStock();
  }
  async loadWarehouseStock() {
    const e = this.getContentKey();
    if (e == null) {
      this.setStatus("Save the product or variant with a SKU before editing warehouse stock.");
      return;
    }
    this.setStatus("Loading warehouse stock...");
    try {
      this.warehouseStock = await this.fetchJson(`/ekom/backoffice/WarehouseStock/${e}`), this.renderStock(), this.warehouseStock.sku.length === 0 ? this.setStatus("Save a SKU before editing warehouse stock.") : this.warehouseStock.items.length === 0 ? this.setStatus("No warehouses are configured for this product or variant.") : this.setStatus("");
    } catch (t) {
      const s = t instanceof Error ? t.message : "Could not load warehouse stock.";
      this.setStatus(s, !0);
    }
  }
  renderShell() {
    this.innerHTML = `
      <style>
        :host { display: block; }
        .editor { display: grid; gap: var(--uui-size-space-4, 16px); width: 100%; max-width: 600px; }
        fieldset { border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); margin: 0; padding: var(--uui-size-space-4, 16px); background: var(--uui-color-surface, #fff); }
        legend { padding: 0 var(--uui-size-space-2, 8px); color: var(--uui-color-text, #1b264f); font-size: 16px; font-weight: 700; }
        .header, .row { display: grid; grid-template-columns: 100px minmax(150px, 1fr) 110px; gap: var(--uui-size-space-3, 12px); align-items: center; }
        .header { padding: 0 var(--uui-size-space-2, 8px) var(--uui-size-space-2, 8px); color: var(--uui-color-text-alt, #515054); font-size: 12px; font-weight: 700; text-transform: uppercase; }
        .header > :last-child { text-align: center; }
        .row { min-height: 40px; padding: var(--uui-size-space-2, 8px); border-top: 1px solid var(--uui-color-border, #d8d7d9); }
        input { box-sizing: border-box; width: 100%; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; text-align: right; }
        input:disabled { cursor: not-allowed; opacity: 0.55; }
        .hidden { color: var(--uui-color-text-alt, #515054); font-size: 12px; }
        p { margin: 0; color: var(--uui-color-text-alt, #515054); line-height: 1.4; }
        p[data-error='true'] { color: var(--uui-color-danger, #d42054); }
        @media (max-width: 600px) {
          .header { display: none; }
          .row { grid-template-columns: minmax(80px, 1fr) minmax(120px, 2fr) 90px; gap: var(--uui-size-space-2, 8px); }
        }
      </style>
      <div class="editor"></div>
      <p aria-live="polite"></p>
    `, this.editor = this.querySelector(".editor") ?? void 0, this.status = this.querySelector("p") ?? void 0;
  }
  renderStock() {
    if (this.editor == null)
      return;
    const e = document.createDocumentFragment(), t = [...new Set(this.warehouseStock.items.map((s) => s.storeAlias))];
    for (const s of t) {
      const a = document.createElement("fieldset"), r = document.createElement("legend");
      r.textContent = s;
      const o = document.createElement("div");
      o.className = "header";
      for (const d of ["Code", "Warehouse", "Balance"]) {
        const c = document.createElement("div");
        c.textContent = d, o.append(c);
      }
      a.append(r, o);
      for (const d of this.warehouseStock.items.filter((c) => c.storeAlias === s))
        a.append(this.createRow(d));
      e.append(a);
    }
    this.editor.replaceChildren(e), this.syncDisabledState();
  }
  createRow(e) {
    const t = document.createElement("div");
    t.className = "row";
    const s = document.createElement("div");
    s.textContent = e.code;
    const a = document.createElement("div");
    if (a.textContent = e.name, !e.visible) {
      const o = document.createElement("span");
      o.className = "hidden", o.textContent = " (Hidden)", a.append(o);
    }
    const r = document.createElement("input");
    return r.type = "number", r.min = "0", r.step = "any", r.placeholder = "Not set", r.dataset.store = e.storeAlias, r.dataset.warehouse = e.warehouseKey, r.value = e.balance == null ? "" : String(e.balance), r.addEventListener("input", () => this.setBalance(e.storeAlias, e.warehouseKey, r.value)), t.append(s, a, r), t;
  }
  setBalance(e, t, s) {
    const a = s === "" ? null : Number(s), r = a == null || Number.isFinite(a) && a >= 0 ? a : null;
    this.warehouseStock = {
      ...this.warehouseStock,
      items: this.warehouseStock.items.map((o) => o.storeAlias === e && o.warehouseKey === t ? { ...o, balance: r } : o)
    }, this.dispatchEvent(new p());
  }
  normalizeValue(e) {
    const t = typeof e == "string" ? this.tryParseJson(e) : e;
    if (t == null || typeof t != "object" || Array.isArray(t))
      return { sku: "", items: [] };
    const s = t;
    return {
      sku: typeof s.sku == "string" ? s.sku : "",
      items: Array.isArray(s.items) ? s.items : []
    };
  }
  syncInputs() {
    for (const e of this.querySelectorAll("input[data-warehouse]")) {
      const t = this.warehouseStock.items.find((s) => s.storeAlias === e.dataset.store && s.warehouseKey === e.dataset.warehouse);
      e.value = (t == null ? void 0 : t.balance) == null ? "" : String(t.balance);
    }
  }
  syncDisabledState() {
    for (const e of this.querySelectorAll("input"))
      e.disabled = this.readonly;
  }
  setStatus(e, t = !1) {
    this.status != null && (this.status.textContent = e, this.status.dataset.error = String(t));
  }
  getContentKey() {
    return window.location.pathname.split("/").find((e) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(e));
  }
  tryParseJson(e) {
    try {
      return JSON.parse(e);
    } catch {
      return;
    }
  }
  async fetchJson(e) {
    const t = await fetch(e, {
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw new Error(`Request to ${e} failed with status ${t.status}.`);
    return await t.json();
  }
}
customElements.define("ekom-warehouse-stock-editor", f);
export {
  f as EkomWarehouseStockEditorElement,
  f as default
};
