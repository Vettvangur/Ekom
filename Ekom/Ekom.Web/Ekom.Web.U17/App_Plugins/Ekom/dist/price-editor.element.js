var f = Object.defineProperty;
var m = (d, u, e) => u in d ? f(d, u, { enumerable: !0, configurable: !0, writable: !0, value: e }) : d[u] = e;
var o = (d, u, e) => m(d, typeof u != "symbol" ? u + "" : u, e);
import { UMB_PROPERTY_CONTEXT as y } from "@umbraco-cms/backoffice/property";
import { UmbChangeEvent as g } from "@umbraco-cms/backoffice/event";
import { UmbElementMixin as b } from "@umbraco-cms/backoffice/element-api";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT as v } from "@umbraco-cms/backoffice/document";
class P extends b(HTMLElement) {
  constructor() {
    super(...arguments);
    o(this, "manifest");
    o(this, "name");
    o(this, "dataSourceAlias");
    o(this, "config");
    o(this, "mandatory");
    o(this, "mandatoryMessage");
    o(this, "editor");
    o(this, "status");
    o(this, "stores", []);
    o(this, "documentId", "");
    o(this, "requestId", 0);
    o(this, "databasePrices", !1);
    o(this, "saving", !1);
    o(this, "rawValue");
    o(this, "internalValue", {});
    o(this, "alias");
  }
  get value() {
    return this.internalValue;
  }
  set value(e) {
    if (this.databasePrices) return;
    this.rawValue = e;
    const t = this.normalizeValue(e);
    this.internalValue = this.stores.length > 0 ? this.ensurePriceStructure(t) : t, this.syncInputs();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncDisabledState();
  }
  connectedCallback() {
    super.connectedCallback(), this.renderShell(), this.setStatus("Loading prices..."), this.consumeContext(y, (e) => {
      e != null && this.observe(e.alias, (t) => {
        this.alias = t, this.loadStores();
      }, "ekomPriceAlias");
    }), this.consumeContext(v, (e) => {
      e != null && (this.updateDocumentId(e.getUnique()), this.observe(e.unique, (t) => this.updateDocumentId(t), "ekomPriceDocumentId"));
    });
  }
  disconnectedCallback() {
    this.requestId++, super.disconnectedCallback();
  }
  async loadStores() {
    var r;
    if (!this.alias) return;
    const e = this.documentId || this.getDocumentIdFromUrl();
    if (e.length === 0) {
      this.setStatus("Save the document before editing prices.");
      return;
    }
    const t = ++this.requestId;
    this.setStatus("Loading prices...");
    try {
      const i = await this.fetchJson("/ekom/backoffice/Config"), s = await this.fetchJson(`/ekom/backoffice/Stores/${encodeURIComponent(e)}`);
      if (t !== this.requestId)
        return;
      this.databasePrices = i.useDatabasePrices === !0, this.stores = s;
      const a = Object.keys(this.internalValue).length > 0 ? this.internalValue : this.normalizeValue(this.rawValue);
      if (this.internalValue = this.ensurePriceStructure(a), this.databasePrices) {
        const n = await this.fetchJson(this.priceUrl(e));
        if (t !== this.requestId) return;
        for (const c of n) {
          const l = Object.keys(this.internalValue).find((p) => p.toLowerCase() === c.storeAlias.toLowerCase()), h = l == null || (r = this.internalValue[l]) == null ? void 0 : r.find((p) => p.Currency.toLowerCase() === c.currency.toLowerCase());
          h != null && (h.Price = c.price);
        }
      }
      this.renderPrices(), this.setStatus(this.databasePrices ? "Prices are stored in the database. Changes save immediately without publishing." : "");
    } catch (i) {
      if (t !== this.requestId)
        return;
      const s = i instanceof Error ? i.message : "Could not load prices.";
      this.setStatus(s, !0);
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
    const e = document.createDocumentFragment(), t = this.stores.length > 1;
    for (const r of this.stores) {
      const i = r.alias;
      if (i == null)
        continue;
      const s = document.createElement(t ? "fieldset" : "div");
      if (t) {
        const a = document.createElement("legend");
        a.textContent = i, s.append(a);
      }
      for (const a of r.currencies ?? [])
        a.currencyValue != null && s.append(this.createPriceInput(i, a));
      e.append(s);
    }
    this.editor.replaceChildren(e), this.syncDisabledState();
  }
  createPriceInput(e, t) {
    const r = t.currencyValue ?? "", i = document.createElement("div");
    i.className = "ekom-price-row";
    const s = `price_${t.isoCurrencySymbol ?? r}_${this.name ?? "price"}_${e}`, a = document.createElement("label");
    a.htmlFor = s, a.textContent = t.isoCurrencySymbol ?? r;
    const n = document.createElement("input");
    n.type = "number", n.min = "0", n.step = "any", n.id = s, n.dataset.store = e, n.dataset.currency = r, n.value = String(this.getPrice(e, r)), n.addEventListener(this.databasePrices ? "change" : "input", () => {
      if (this.databasePrices) {
        if (!n.validity.valid || n.value === "") {
          this.setStatus("Enter a non-negative price.", !0);
          return;
        }
        this.savePrice(e, r, n.value);
      } else
        this.setPrice(e, r, n.value);
    });
    const c = document.createElement("span");
    return c.textContent = t.currencySymbol ?? "", i.append(a, n, c), i;
  }
  setPrice(e, t, r) {
    const i = this.parsePrice(r);
    let s = !1;
    const a = (this.internalValue[e] ?? []).map((n) => n.Currency !== t ? n : (s = !0, {
      ...n,
      Price: i
    }));
    s || a.push({
      Currency: t,
      Price: i
    }), this.internalValue = {
      ...this.internalValue,
      [e]: a
    }, this.rawValue = this.internalValue, this.databasePrices || this.emitChange();
  }
  priceUrl(e) {
    if (!this.alias) throw new Error("The price property alias is unavailable.");
    return `/ekom/backoffice/Prices/${encodeURIComponent(e)}/${encodeURIComponent(this.alias)}`;
  }
  async savePrice(e, t, r) {
    if (!(this.readonly || this.saving)) {
      this.saving = !0, this.syncDisabledState(), this.setStatus("Saving price...");
      try {
        const i = await fetch(this.priceUrl(this.documentId || this.getDocumentIdFromUrl()), {
          method: "PUT",
          credentials: "same-origin",
          headers: { "Content-Type": "application/json" },
          body: JSON.stringify({ storeAlias: e, currency: t, price: Number(r) })
        });
        if (!i.ok) throw new Error(`Could not save price (${i.status}).`);
        this.setPrice(e, t, r), this.setStatus("Price saved. The change is live without publishing.");
      } catch (i) {
        this.syncInputs(), this.setStatus(i instanceof Error ? i.message : "Could not save price.", !0);
      } finally {
        this.saving = !1, this.syncDisabledState();
      }
    }
  }
  getPrice(e, t) {
    var r, i;
    return ((i = (r = this.internalValue[e]) == null ? void 0 : r.find((s) => s.Currency === t)) == null ? void 0 : i.Price) ?? 0;
  }
  ensurePriceStructure(e) {
    const t = Object.fromEntries(Object.entries(e).map(([r, i]) => [
      r,
      i.map((s) => ({ ...s }))
    ]));
    for (const r of this.stores) {
      const i = r.alias;
      if (i == null)
        continue;
      const s = t[i] ?? [];
      for (const a of r.currencies ?? []) {
        const n = a.currencyValue;
        n != null && (s.some((c) => c.Currency === n) || s.push({
          Currency: n,
          Price: 0
        }));
      }
      t[i] = s;
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
        t[r] = i.map((s) => {
          if (!(!this.isRecord(s) || !("Currency" in s) || !("Price" in s)))
            return {
              Currency: String(s.Currency),
              Price: this.parsePrice(s.Price)
            };
        }).filter((s) => s != null);
      }
    return t;
  }
  transformLegacyValue(e) {
    var i, s, a;
    const t = {}, r = ((a = (s = (i = this.stores[0]) == null ? void 0 : i.currencies) == null ? void 0 : s[0]) == null ? void 0 : a.currencyValue) ?? "";
    for (const [n, c] of Object.entries(e))
      n === "undefined" || !this.isRecord(c) || (t[n] = Object.values(c).map((l) => {
        const h = this.isRecord(l) && "Price" in l ? l.Price : l;
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
      e.disabled = this.readonly || this.saving;
  }
  setStatus(e, t = !1) {
    this.status != null && (this.status.textContent = e, this.status.dataset.error = String(t));
  }
  emitChange() {
    this.dispatchEvent(new g());
  }
  updateDocumentId(e) {
    e == null || e === this.documentId || (this.documentId = e, this.loadStores());
  }
  getDocumentIdFromUrl() {
    const e = new URL(window.location.href), t = e.searchParams.get("id");
    return t != null && (/^\d+$/.test(t) || this.isGuid(t)) ? t : e.pathname.split("/").reverse().find((r) => /^\d+$/.test(r) || this.isGuid(r)) ?? "";
  }
  isGuid(e) {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(e);
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
customElements.define("ekom-price-editor", P);
export {
  P as EkomPriceEditorElement,
  P as default
};
