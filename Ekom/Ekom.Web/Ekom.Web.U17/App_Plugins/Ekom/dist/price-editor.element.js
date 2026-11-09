var f = Object.defineProperty;
var m = (l, c, e) => c in l ? f(l, c, { enumerable: !0, configurable: !0, writable: !0, value: e }) : l[c] = e;
var o = (l, c, e) => m(l, typeof c != "symbol" ? c + "" : c, e);
import { UMB_PROPERTY_CONTEXT as p } from "@umbraco-cms/backoffice/property";
import { UmbChangeEvent as g } from "@umbraco-cms/backoffice/event";
import { UmbElementMixin as y } from "@umbraco-cms/backoffice/element-api";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT as b } from "@umbraco-cms/backoffice/document";
class v extends y(HTMLElement) {
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
    o(this, "hasDocumentContext", !1);
    o(this, "requestId", 0);
    o(this, "documentRevision", 0);
    o(this, "databasePrices", !1);
    o(this, "saving", !1);
    o(this, "loading", !1);
    o(this, "overrides", []);
    o(this, "rawValue");
    o(this, "internalValue", {});
    o(this, "alias");
  }
  get value() {
    return this.internalValue;
  }
  set value(e) {
    this.rawValue = e, this.rebuildValue();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncDisabledState();
  }
  connectedCallback() {
    super.connectedCallback(), this.renderShell(), this.setStatus("Loading prices..."), this.consumeContext(p, (e) => {
      e != null && this.observe(e.alias, (t) => {
        this.alias && this.alias !== t && this.resetDocumentState(), this.alias = t, this.loadStores();
      }, "ekomPriceAlias");
    }), this.consumeContext(b, (e) => {
      e != null && (this.updateDocumentId(e.getUnique()), this.observe(e.unique, (t) => this.updateDocumentId(t), "ekomPriceDocumentId"));
    });
  }
  disconnectedCallback() {
    this.requestId++, this.documentRevision++, this.saving = !1, super.disconnectedCallback();
  }
  async loadStores() {
    if (!this.alias) return;
    const e = this.activeDocumentId;
    if (e.length === 0) {
      this.setStatus("Save the document before editing prices.");
      return;
    }
    const t = ++this.requestId;
    this.loading = !0, this.syncDisabledState(), this.setStatus("Loading prices...");
    try {
      const i = await this.fetchJson("/ekom/backoffice/Config"), r = await this.fetchJson(`/ekom/backoffice/Stores/${encodeURIComponent(e)}`);
      if (t !== this.requestId) return;
      const s = i.useDatabasePrices === !0, a = s ? await this.fetchJson(this.priceUrl(e)) : [];
      if (t !== this.requestId) return;
      this.databasePrices = s, this.stores = r, this.overrides = a, this.rebuildValue(), this.renderPrices(), this.setStatus(s ? "Prices are stored in the database. Changes save immediately without publishing." : "");
    } catch (i) {
      if (t !== this.requestId) return;
      this.setStatus(i instanceof Error ? i.message : "Could not load prices.", !0);
    } finally {
      t === this.requestId && (this.loading = !1, this.syncDisabledState());
    }
  }
  rebuildValue() {
    var e;
    if (this.internalValue = this.ensurePriceStructure(this.normalizeValue(this.rawValue)), this.databasePrices)
      for (const t of this.overrides) {
        const i = Object.keys(this.internalValue).find((s) => s.toLowerCase() === t.storeAlias.toLowerCase()), r = i == null || (e = this.internalValue[i]) == null ? void 0 : e.find((s) => s.Currency.toLowerCase() === t.currency.toLowerCase());
        r != null && (r.Price = t.price);
      }
    this.syncInputs();
  }
  resetDocumentState() {
    this.requestId++, this.documentRevision++, this.rawValue = void 0, this.internalValue = {}, this.overrides = [], this.saving = !1, this.renderPrices();
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
    for (const i of this.stores) {
      const r = i.alias;
      if (r == null)
        continue;
      const s = document.createElement(t ? "fieldset" : "div");
      if (t) {
        const a = document.createElement("legend");
        a.textContent = r, s.append(a);
      }
      for (const a of i.currencies ?? [])
        a.currencyValue != null && s.append(this.createPriceInput(r, a));
      e.append(s);
    }
    this.editor.replaceChildren(e), this.syncDisabledState();
  }
  createPriceInput(e, t) {
    const i = t.currencyValue ?? "", r = document.createElement("div");
    r.className = "ekom-price-row";
    const s = `price_${t.isoCurrencySymbol ?? i}_${this.name ?? "price"}_${e}`, a = document.createElement("label");
    a.htmlFor = s, a.textContent = t.isoCurrencySymbol ?? i;
    const n = document.createElement("input");
    n.type = "number", n.min = "0", n.step = "any", n.id = s, n.dataset.store = e, n.dataset.currency = i, n.value = String(this.getPrice(e, i)), n.addEventListener(this.databasePrices ? "change" : "input", () => {
      if (this.databasePrices) {
        if (!n.validity.valid || n.value === "") {
          this.setStatus("Enter a non-negative price.", !0);
          return;
        }
        this.savePrice(e, i, n.value);
      } else
        this.setPrice(e, i, n.value);
    });
    const u = document.createElement("span");
    return u.textContent = t.currencySymbol ?? "", r.append(a, n, u), r;
  }
  setPrice(e, t, i) {
    const r = this.parsePrice(i);
    let s = !1;
    const a = (this.internalValue[e] ?? []).map((n) => n.Currency !== t ? n : (s = !0, {
      ...n,
      Price: r
    }));
    s || a.push({
      Currency: t,
      Price: r
    }), this.internalValue = {
      ...this.internalValue,
      [e]: a
    }, this.databasePrices ? (this.overrides = this.overrides.filter((n) => n.storeAlias.toLowerCase() !== e.toLowerCase() || n.currency.toLowerCase() !== t.toLowerCase()), this.overrides.push({ storeAlias: e, currency: t, price: r })) : this.rawValue = this.internalValue, this.databasePrices || this.emitChange();
  }
  priceUrl(e) {
    if (!this.alias) throw new Error("The price property alias is unavailable.");
    return `/ekom/backoffice/Prices/${encodeURIComponent(e)}/${encodeURIComponent(this.alias)}`;
  }
  async savePrice(e, t, i) {
    if (this.readonly || this.saving || this.loading) return;
    const r = this.activeDocumentId;
    if (!r) {
      this.setStatus("Save the document before editing prices.");
      return;
    }
    const s = this.documentRevision;
    this.saving = !0, this.syncDisabledState(), this.setStatus("Saving price...");
    try {
      const a = await fetch(this.priceUrl(r), {
        method: "PUT",
        credentials: "same-origin",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ storeAlias: e, currency: t, price: Number(i) })
      });
      if (s !== this.documentRevision) return;
      if (!a.ok) throw new Error(`Could not save price (${a.status}).`);
      this.setPrice(e, t, i), this.setStatus("Price saved. The change is live without publishing.");
    } catch (a) {
      if (s !== this.documentRevision) return;
      this.syncInputs(), this.setStatus(a instanceof Error ? a.message : "Could not save price.", !0);
    } finally {
      s === this.documentRevision && (this.saving = !1, this.syncDisabledState());
    }
  }
  getPrice(e, t) {
    var i, r;
    return ((r = (i = this.internalValue[e]) == null ? void 0 : i.find((s) => s.Currency === t)) == null ? void 0 : r.Price) ?? 0;
  }
  ensurePriceStructure(e) {
    const t = Object.fromEntries(Object.entries(e).map(([i, r]) => [
      i,
      r.map((s) => ({ ...s }))
    ]));
    for (const i of this.stores) {
      const r = i.alias;
      if (r == null)
        continue;
      const s = t[r] ?? [];
      for (const a of i.currencies ?? []) {
        const n = a.currencyValue;
        n != null && (s.some((u) => u.Currency === n) || s.push({
          Currency: n,
          Price: 0
        }));
      }
      t[r] = s;
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
    for (const [i, r] of Object.entries(e))
      if (i !== "undefined") {
        if (!Array.isArray(r))
          return;
        t[i] = r.map((s) => {
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
    var r, s, a;
    const t = {}, i = ((a = (s = (r = this.stores[0]) == null ? void 0 : r.currencies) == null ? void 0 : s[0]) == null ? void 0 : a.currencyValue) ?? "";
    for (const [n, u] of Object.entries(e))
      n === "undefined" || !this.isRecord(u) || (t[n] = Object.values(u).map((d) => {
        const h = this.isRecord(d) && "Price" in d ? d.Price : d;
        return {
          Currency: i,
          Price: this.parsePrice(h)
        };
      }));
    return t;
  }
  syncInputs() {
    if (this.editor != null)
      for (const e of this.editor.querySelectorAll("input[data-store][data-currency]")) {
        const t = e.dataset.store, i = e.dataset.currency;
        t == null || i == null || (e.value = String(this.getPrice(t, i)));
      }
  }
  syncDisabledState() {
    for (const e of this.querySelectorAll("input"))
      e.disabled = this.readonly || this.saving || this.loading || this.hasDocumentContext && !this.documentId;
  }
  setStatus(e, t = !1) {
    this.status != null && (this.status.textContent = e, this.status.dataset.error = String(t));
  }
  emitChange() {
    this.dispatchEvent(new g());
  }
  get activeDocumentId() {
    return this.hasDocumentContext ? this.documentId : this.documentId || this.getDocumentIdFromUrl();
  }
  updateDocumentId(e) {
    const t = e ?? "", i = t !== this.documentId;
    if (this.hasDocumentContext = !0, !i) {
      this.syncDisabledState();
      return;
    }
    this.documentId ? this.resetDocumentState() : (this.requestId++, this.documentRevision++, this.overrides = [], this.saving = !1), this.loading = !1, this.documentId = t, this.syncDisabledState(), this.loadStores();
  }
  getDocumentIdFromUrl() {
    const e = new URL(window.location.href), t = e.searchParams.get("id");
    return t != null && (/^\d+$/.test(t) || this.isGuid(t)) ? t : e.pathname.split("/").reverse().find((i) => /^\d+$/.test(i) || this.isGuid(i)) ?? "";
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
customElements.define("ekom-price-editor", v);
export {
  v as EkomPriceEditorElement,
  v as default
};
