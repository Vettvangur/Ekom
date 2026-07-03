var u = Object.defineProperty;
var l = (a, o, t) => o in a ? u(a, o, { enumerable: !0, configurable: !0, writable: !0, value: t }) : a[o] = t;
var i = (a, o, t) => l(a, typeof o != "symbol" ? o + "" : o, t);
import { UmbChangeEvent as d } from "@umbraco-cms/backoffice/event";
class h extends HTMLElement {
  constructor() {
    super(...arguments);
    i(this, "manifest");
    i(this, "name");
    i(this, "dataSourceAlias");
    i(this, "config");
    i(this, "mandatory");
    i(this, "mandatoryMessage");
    i(this, "editor");
    i(this, "status");
    i(this, "stocks", []);
  }
  get value() {
    return this.stocks;
  }
  set value(t) {
    this.stocks = this.normalizeValue(t), this.syncInputs();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(t) {
    this.toggleAttribute("readonly", t), this.syncDisabledState();
  }
  connectedCallback() {
    this.renderShell(), this.loadStock();
  }
  async loadStock() {
    this.setStatus("Loading stock...");
    try {
      const t = await this.fetchJson("/ekom/backoffice/Config"), e = this.getContentKey();
      this.stocks = t.perStoreStock ? await this.loadPerStoreStock(e) : [await this.loadStockItem(e, "")], this.renderStock(), this.setStatus(""), this.emitChange();
    } catch (t) {
      const e = t instanceof Error ? t.message : "Could not load stock.";
      this.setStatus(e, !0);
    }
  }
  async loadPerStoreStock(t) {
    const e = await this.fetchJson(`/ekom/backoffice/Stores/${this.getNodeId()}`);
    if (e.length <= 1)
      return [await this.loadStockItem(t, "")];
    const s = [];
    for (const r of e)
      r.alias != null && s.push(await this.loadStockItem(t, r.alias));
    return s;
  }
  async loadStockItem(t, e) {
    if (t == null)
      return {
        storeAlias: e,
        value: this.getExistingValue(e)
      };
    const s = e.length > 0 ? `/ekom/backoffice/Stock/${t}/StoreAlias/${e}` : `/ekom/backoffice/Stock/${t}`;
    try {
      const r = await this.fetchJson(s);
      return {
        storeAlias: e,
        value: this.parseStock(r)
      };
    } catch {
      return {
        storeAlias: e,
        value: this.getExistingValue(e)
      };
    }
  }
  renderShell() {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-stock-editor {
          display: grid;
          gap: var(--uui-size-space-5, 20px);
        }

        fieldset {
          border: 1px solid var(--uui-color-border, #d8d7d9);
          border-radius: var(--uui-border-radius, 3px);
          margin: 0;
          padding: var(--uui-size-space-4, 16px);
        }

        legend {
          padding: 0 var(--uui-size-space-2, 8px);
          font-size: 18px;
          font-weight: 700;
        }

        input {
          box-sizing: border-box;
          min-height: 32px;
          border: 1px solid var(--uui-color-border, #d8d7d9);
          border-radius: var(--uui-border-radius, 3px);
          padding: var(--uui-size-space-2, 8px);
          background: var(--uui-color-surface, #fff);
          color: var(--uui-color-text, #1b264f);
          font: inherit;
        }

        p {
          margin: 0;
          color: var(--uui-color-text-alt, #515054);
          line-height: 1.4;
        }

        p[data-error='true'] {
          color: var(--uui-color-danger, #d42054);
        }
      </style>
      <div class="ekom-stock-editor"></div>
      <p aria-live="polite"></p>
    `, this.editor = this.querySelector(".ekom-stock-editor") ?? void 0, this.status = this.querySelector("p") ?? void 0;
  }
  renderStock() {
    if (this.editor == null)
      return;
    const t = document.createDocumentFragment();
    for (const e of this.stocks) {
      if (e.storeAlias.length === 0) {
        t.append(this.createStockInput(e));
        continue;
      }
      const s = document.createElement("fieldset"), r = document.createElement("legend");
      r.textContent = e.storeAlias, s.append(r, this.createStockInput(e)), t.append(s);
    }
    this.editor.replaceChildren(t), this.syncDisabledState();
  }
  createStockInput(t) {
    const e = document.createElement("input");
    return e.type = "number", e.min = "0", e.step = "any", e.id = `stock_${t.storeAlias}`, e.dataset.store = t.storeAlias, e.value = String(t.value), e.addEventListener("input", () => this.setStock(t.storeAlias, e.value)), e;
  }
  setStock(t, e) {
    const s = this.parseStock(e);
    let r = !1;
    const c = this.stocks.map((n) => n.storeAlias !== t ? n : (r = !0, {
      ...n,
      value: s
    }));
    r || c.push({
      storeAlias: t,
      value: s
    }), this.stocks = c, this.emitChange();
  }
  getExistingValue(t) {
    var e;
    return ((e = this.stocks.find((s) => s.storeAlias === t)) == null ? void 0 : e.value) ?? 0;
  }
  normalizeValue(t) {
    return Array.isArray(t) ? t.filter((e) => e != null && typeof e == "object").map((e) => {
      const s = e;
      return {
        storeAlias: s.storeAlias ?? "",
        value: this.parseStock(s.value)
      };
    }) : [];
  }
  syncInputs() {
    if (this.editor != null)
      for (const t of this.editor.querySelectorAll("input[data-store]"))
        t.value = String(this.getExistingValue(t.dataset.store ?? ""));
  }
  syncDisabledState() {
    for (const t of this.querySelectorAll("input"))
      t.disabled = this.readonly;
  }
  setStatus(t, e = !1) {
    this.status != null && (this.status.textContent = t, this.status.dataset.error = String(e));
  }
  emitChange() {
    this.dispatchEvent(new d());
  }
  getContentKey() {
    return window.location.pathname.split("/").find((t) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(t));
  }
  getNodeId() {
    const t = new URL(window.location.href), e = t.searchParams.get("id");
    if (e != null) {
      const r = Number.parseInt(e, 10);
      if (!Number.isNaN(r))
        return r;
    }
    const s = t.pathname.split("/").reverse().find((r) => /^\d+$/.test(r));
    return s == null ? 0 : Number.parseInt(s, 10);
  }
  parseStock(t) {
    if (t == null || t === "")
      return 0;
    const e = Number(t);
    return Number.isFinite(e) ? e : 0;
  }
  async fetchJson(t) {
    const e = await fetch(t, {
      credentials: "same-origin",
      headers: {
        Accept: "application/json"
      }
    });
    if (!e.ok)
      throw new Error(`Request to ${t} failed with status ${e.status}.`);
    return await e.json();
  }
}
customElements.define("ekom-stock-editor", h);
export {
  h as EkomStockEditorElement,
  h as default
};
