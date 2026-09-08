var l = Object.defineProperty;
var h = (d, n, e) => n in d ? l(d, n, { enumerable: !0, configurable: !0, writable: !0, value: e }) : d[n] = e;
var u = (d, n, e) => h(d, typeof n != "symbol" ? n + "" : n, e);
import { UmbChangeEvent as p } from "@umbraco-cms/backoffice/event";
class m extends HTMLElement {
  constructor() {
    super(...arguments);
    u(this, "manifest");
    u(this, "name");
    u(this, "dataSourceAlias");
    u(this, "config");
    u(this, "editor");
    u(this, "status");
    u(this, "warehouseStock", { sku: "", items: [] });
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
      const t = await this.fetchJson(`/ekom/backoffice/WarehouseStock/${e}`);
      this.warehouseStock = this.mergePendingChanges(t, this.warehouseStock), this.renderStock(), this.warehouseStock.sku.length === 0 ? this.setStatus("Save a SKU before editing warehouse stock.") : this.warehouseStock.items.length === 0 ? this.setStatus("No warehouses are configured for this product or variant.") : this.setStatus("");
    } catch (t) {
      const r = t instanceof Error ? t.message : "Could not load warehouse stock.";
      this.setStatus(r, !0);
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
    const e = document.createDocumentFragment(), t = [...new Set(this.warehouseStock.items.map((r) => r.storeAlias))];
    for (const r of t) {
      const a = document.createElement("fieldset"), s = document.createElement("legend");
      s.textContent = r;
      const o = document.createElement("div");
      o.className = "header";
      for (const i of ["Code", "Warehouse", "Balance"]) {
        const c = document.createElement("div");
        c.textContent = i, o.append(c);
      }
      a.append(s, o);
      for (const i of this.warehouseStock.items.filter((c) => c.storeAlias === r))
        a.append(this.createRow(i));
      e.append(a);
    }
    this.editor.replaceChildren(e), this.syncDisabledState();
  }
  createRow(e) {
    const t = document.createElement("div");
    t.className = "row";
    const r = document.createElement("div");
    r.textContent = e.code;
    const a = document.createElement("div");
    if (a.textContent = e.name, !e.visible) {
      const o = document.createElement("span");
      o.className = "hidden", o.textContent = " (Hidden)", a.append(o);
    }
    const s = document.createElement("input");
    return s.type = "number", s.min = "0", s.step = "any", s.placeholder = "Not set", s.dataset.store = e.storeAlias, s.dataset.warehouse = e.warehouseKey, s.value = e.balance == null ? "" : String(e.balance), s.addEventListener("input", () => this.setBalance(e.storeAlias, e.warehouseKey, s)), t.append(r, a, s), t;
  }
  setBalance(e, t, r) {
    const a = r.value, s = a === "" ? null : Number(a);
    if (s != null && (!Number.isFinite(s) || s < 0)) {
      r.setCustomValidity("Balance must be a non-negative number.");
      return;
    }
    r.setCustomValidity("");
    const o = s;
    this.warehouseStock = {
      ...this.warehouseStock,
      items: this.warehouseStock.items.map((i) => i.storeAlias === e && i.warehouseKey === t ? { ...i, balance: o, isDirty: !0 } : i)
    }, this.dispatchEvent(new p());
  }
  normalizeValue(e) {
    const t = typeof e == "string" ? this.tryParseJson(e) : e;
    if (t == null || typeof t != "object" || Array.isArray(t))
      return { sku: "", items: [] };
    const r = t;
    return {
      sku: typeof r.sku == "string" ? r.sku : "",
      items: Array.isArray(r.items) ? r.items.filter((a) => a != null && typeof a == "object").map((a) => ({ ...a, isDirty: a.isDirty === !0 })) : []
    };
  }
  mergePendingChanges(e, t) {
    const r = t.items.filter((s) => s.isDirty), a = new Set(e.items.map((s) => this.getIdentity(s)));
    return {
      ...e,
      items: [
        ...e.items.map((s) => {
          const o = r.find((i) => this.getIdentity(i) === this.getIdentity(s));
          return o == null ? { ...s, isDirty: !1 } : { ...s, balance: o.balance, isDirty: !0 };
        }),
        ...r.filter((s) => !a.has(this.getIdentity(s)))
      ]
    };
  }
  getIdentity(e) {
    return `${e.storeAlias.trim().toUpperCase()}|${e.warehouseKey.toUpperCase()}`;
  }
  syncInputs() {
    for (const e of this.querySelectorAll("input[data-warehouse]")) {
      const t = this.warehouseStock.items.find((r) => r.storeAlias === e.dataset.store && r.warehouseKey === e.dataset.warehouse);
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
customElements.define("ekom-warehouse-stock-editor", m);
export {
  m as EkomWarehouseStockEditorElement,
  m as default
};
