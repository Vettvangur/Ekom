var p = Object.defineProperty;
var f = (l, u, e) => u in l ? p(l, u, { enumerable: !0, configurable: !0, writable: !0, value: e }) : l[u] = e;
var a = (l, u, e) => f(l, typeof u != "symbol" ? u + "" : u, e);
import { UmbChangeEvent as m } from "@umbraco-cms/backoffice/event";
class y extends HTMLElement {
  constructor() {
    super(...arguments);
    a(this, "manifest");
    a(this, "name");
    a(this, "dataSourceAlias");
    a(this, "config");
    a(this, "mandatory");
    a(this, "mandatoryMessage");
    a(this, "editor");
    a(this, "status");
    a(this, "stores", []);
    a(this, "showStoreFieldsets", !0);
    a(this, "rawValue");
    a(this, "internalValue", {});
  }
  get value() {
    return this.internalValue;
  }
  set value(e) {
    this.rawValue = e, this.internalValue = this.normalizeValue(e), this.syncInputs();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncDisabledState();
  }
  connectedCallback() {
    this.renderShell(), this.loadStores();
  }
  async loadStores() {
    this.setStatus("Loading prices...");
    try {
      const [e, t] = await Promise.all([
        this.fetchJson("/ekom/backoffice/Config"),
        this.fetchJson(`/ekom/backoffice/Stores/${this.getNodeId()}`)
      ]);
      this.showStoreFieldsets = e.perStoreStock !== !1, this.stores = t, this.internalValue = this.ensurePriceStructure(this.normalizeValue(this.rawValue)), this.renderPrices(), this.setStatus(""), this.emitChange();
    } catch (e) {
      const t = e instanceof Error ? e.message : "Could not load prices.";
      this.setStatus(t, !0);
    }
  }
  renderShell() {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-price-editor {
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

        .ekom-price-row {
          display: flex;
          align-items: center;
          gap: var(--uui-size-space-2, 8px);
          margin-bottom: var(--uui-size-space-3, 12px);
        }

        .ekom-price-row:last-child {
          margin-bottom: 0;
        }

        label {
          min-width: 45px;
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
      <div class="ekom-price-editor"></div>
      <p aria-live="polite"></p>
    `, this.editor = this.querySelector(".ekom-price-editor") ?? void 0, this.status = this.querySelector("p") ?? void 0;
  }
  renderPrices() {
    if (this.editor == null)
      return;
    const e = document.createDocumentFragment();
    for (const t of this.stores) {
      const r = t.alias;
      if (r == null)
        continue;
      const i = this.showStoreFieldsets ? document.createElement("fieldset") : document.createDocumentFragment();
      if (i instanceof HTMLFieldSetElement) {
        const n = document.createElement("legend");
        n.textContent = r, i.append(n);
      }
      for (const n of t.currencies ?? [])
        n.currencyValue != null && i.append(this.createPriceInput(r, n));
      e.append(i);
    }
    this.editor.replaceChildren(e), this.syncDisabledState();
  }
  createPriceInput(e, t) {
    const r = t.currencyValue ?? "", i = document.createElement("div");
    i.className = "ekom-price-row";
    const n = `price_${t.isoCurrencySymbol ?? r}_${this.name ?? "price"}_${e}`, o = document.createElement("label");
    o.htmlFor = n, o.textContent = t.isoCurrencySymbol ?? r;
    const s = document.createElement("input");
    s.type = "number", s.min = "0", s.step = "any", s.id = n, s.dataset.store = e, s.dataset.currency = r, s.value = String(this.getPrice(e, r)), s.addEventListener("input", () => this.setPrice(e, r, s.value));
    const c = document.createElement("span");
    return c.textContent = t.currencySymbol ?? "", i.append(o, s, c), i;
  }
  setPrice(e, t, r) {
    const i = this.parsePrice(r);
    let n = !1;
    const o = (this.internalValue[e] ?? []).map((s) => s.Currency !== t ? s : (n = !0, {
      ...s,
      Price: i
    }));
    n || o.push({
      Currency: t,
      Price: i
    }), this.internalValue = {
      ...this.internalValue,
      [e]: o
    }, this.emitChange();
  }
  getPrice(e, t) {
    var r, i;
    return ((i = (r = this.internalValue[e]) == null ? void 0 : r.find((n) => n.Currency === t)) == null ? void 0 : i.Price) ?? 0;
  }
  ensurePriceStructure(e) {
    var r, i;
    const t = {};
    for (const n of this.stores) {
      const o = n.alias;
      if (o != null) {
        t[o] = [];
        for (const s of n.currencies ?? []) {
          const c = s.currencyValue;
          c != null && t[o].push({
            Currency: c,
            Price: ((i = (r = e[o]) == null ? void 0 : r.find((d) => d.Currency === c)) == null ? void 0 : i.Price) ?? 0
          });
        }
      }
    }
    return t;
  }
  normalizeValue(e) {
    if (e == null || e === "")
      return {};
    if (!this.isRecord(e))
      return {};
    const t = this.normalizeCurrentFormat(e);
    return t ?? this.transformLegacyValue(e);
  }
  normalizeCurrentFormat(e) {
    const t = {};
    for (const [r, i] of Object.entries(e))
      if (r !== "undefined") {
        if (!Array.isArray(i))
          return;
        t[r] = i.map((n) => {
          if (!(!this.isRecord(n) || !("Currency" in n) || !("Price" in n)))
            return {
              Currency: String(n.Currency),
              Price: this.parsePrice(n.Price)
            };
        }).filter((n) => n != null);
      }
    return t;
  }
  transformLegacyValue(e) {
    var i, n, o;
    const t = {}, r = ((o = (n = (i = this.stores[0]) == null ? void 0 : i.currencies) == null ? void 0 : n[0]) == null ? void 0 : o.currencyValue) ?? "";
    for (const [s, c] of Object.entries(e))
      s === "undefined" || !this.isRecord(c) || (t[s] = Object.values(c).map((d) => {
        const h = this.isRecord(d) && "Price" in d ? d.Price : d;
        return {
          Currency: r,
          Price: this.parsePrice(h)
        };
      }));
    return t;
  }
  syncInputs() {
    if (this.editor != null)
      for (const e of this.editor.querySelectorAll("input[data-store][data-currency]")) {
        const t = e.dataset.store, r = e.dataset.currency;
        t == null || r == null || (e.value = String(this.getPrice(t, r)));
      }
  }
  syncDisabledState() {
    for (const e of this.querySelectorAll("input"))
      e.disabled = this.readonly;
  }
  setStatus(e, t = !1) {
    this.status != null && (this.status.textContent = e, this.status.dataset.error = String(t));
  }
  emitChange() {
    this.dispatchEvent(new m());
  }
  getNodeId() {
    const e = new URL(window.location.href), t = e.searchParams.get("id");
    if (t != null) {
      const i = Number.parseInt(t, 10);
      if (!Number.isNaN(i))
        return i;
    }
    const r = e.pathname.split("/").reverse().find((i) => /^\d+$/.test(i));
    return r == null ? 0 : Number.parseInt(r, 10);
  }
  parsePrice(e) {
    if (e == null || e === "")
      return 0;
    const t = Number(e);
    return Number.isFinite(t) ? t : 0;
  }
  isRecord(e) {
    return e != null && typeof e == "object" && !Array.isArray(e);
  }
  async fetchJson(e) {
    const t = await fetch(e, {
      credentials: "same-origin",
      headers: {
        Accept: "application/json"
      }
    });
    if (!t.ok)
      throw new Error(`Request to ${e} failed with status ${t.status}.`);
    return await t.json();
  }
}
customElements.define("ekom-price-editor", y);
export {
  y as EkomPriceEditorElement,
  y as default
};
