var m = Object.defineProperty;
var f = (l, u, e) => u in l ? m(l, u, { enumerable: !0, configurable: !0, writable: !0, value: e }) : l[u] = e;
var a = (l, u, e) => f(l, typeof u != "symbol" ? u + "" : u, e);
import { UMB_PROPERTY_CONTEXT as p } from "@umbraco-cms/backoffice/property";
import { UmbChangeEvent as y } from "@umbraco-cms/backoffice/event";
import { UmbElementMixin as b } from "@umbraco-cms/backoffice/element-api";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT as g } from "@umbraco-cms/backoffice/document";
class v extends b(HTMLElement) {
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
    a(this, "documentId", "");
    a(this, "hasDocumentContext", !1);
    a(this, "requestId", 0);
    a(this, "documentRevision", 0);
    a(this, "databasePrices", !1);
    a(this, "loading", !1);
    a(this, "overrides", []);
    a(this, "rawValue");
    a(this, "internalValue", {});
    a(this, "alias");
  }
  get value() {
    return this.databasePrices ? this.normalizeValue(this.rawValue) : this.internalValue;
  }
  set value(e) {
    const t = Object.values(this.normalizeValue(this.rawValue)).some((s) => s.some((i) => i.EditId));
    this.rawValue = e, this.rebuildValue();
    const r = Object.values(this.normalizeValue(e)).some((s) => s.some((i) => i.EditId));
    this.databasePrices && t && !r && this.documentId && this.loadStores();
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
    }), this.consumeContext(g, (e) => {
      e != null && (this.updateDocumentId(e.getUnique()), this.observe(e.unique, (t) => this.updateDocumentId(t), "ekomPriceDocumentId"));
    });
  }
  disconnectedCallback() {
    this.requestId++, this.documentRevision++, super.disconnectedCallback();
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
      const r = await this.fetchJson("/ekom/backoffice/Config"), s = await this.fetchJson(`/ekom/backoffice/Stores/${encodeURIComponent(e)}`);
      if (t !== this.requestId) return;
      const i = r.useDatabasePrices === !0, o = i ? await this.fetchJson(this.priceUrl(e)) : { prices: [] };
      if (t !== this.requestId) return;
      this.databasePrices = i, this.stores = s, this.overrides = o.prices, this.rebuildValue(), this.renderPrices(), this.setStatus(i ? "Save the document to update prices. Saving makes price changes live." : "");
    } catch (r) {
      if (t !== this.requestId) return;
      this.setStatus(r instanceof Error ? r.message : "Could not load prices.", !0);
    } finally {
      t === this.requestId && (this.loading = !1, this.syncDisabledState());
    }
  }
  rebuildValue() {
    var e;
    if (this.internalValue = this.ensurePriceStructure(this.normalizeValue(this.rawValue)), this.databasePrices)
      for (const t of this.overrides) {
        const r = Object.keys(this.internalValue).find((i) => i.toLowerCase() === t.storeAlias.toLowerCase()), s = r == null || (e = this.internalValue[r]) == null ? void 0 : e.find((i) => i.Currency.toLowerCase() === t.currency.toLowerCase());
        s != null && !s.EditId && (s.Price = t.price);
      }
    this.syncInputs();
  }
  resetDocumentState() {
    this.requestId++, this.documentRevision++, this.rawValue = void 0, this.internalValue = {}, this.overrides = [], this.renderPrices();
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
      const s = r.alias;
      if (s == null)
        continue;
      const i = document.createElement(t ? "fieldset" : "div");
      if (t) {
        const o = document.createElement("legend");
        o.textContent = s, i.append(o);
      }
      for (const o of r.currencies ?? [])
        o.currencyValue != null && i.append(this.createPriceInput(s, o));
      e.append(i);
    }
    this.editor.replaceChildren(e), this.syncDisabledState();
  }
  createPriceInput(e, t) {
    const r = t.currencyValue ?? "", s = document.createElement("div");
    s.className = "ekom-price-row";
    const i = `price_${t.isoCurrencySymbol ?? r}_${this.name ?? "price"}_${e}`, o = document.createElement("label");
    o.htmlFor = i, o.textContent = t.isoCurrencySymbol ?? r;
    const n = document.createElement("input");
    n.type = "number", n.min = "0", n.step = "any", n.id = i, n.dataset.store = e, n.dataset.currency = r, n.value = String(this.getPrice(e, r)), n.addEventListener("input", () => {
      if (!n.validity.valid || n.value === "") {
        this.setStatus("Enter a non-negative price.", !0);
        return;
      }
      this.setPrice(e, r, n.value);
    });
    const c = document.createElement("span");
    return c.textContent = t.currencySymbol ?? "", s.append(o, n, c), s;
  }
  setPrice(e, t, r) {
    if (this.readonly || this.loading || this.hasDocumentContext && !this.documentId) return;
    const s = this.parsePrice(r);
    let i = !1;
    const o = (this.internalValue[e] ?? []).map((n) => n.Currency !== t ? n : (i = !0, {
      ...n,
      Price: s
    }));
    if (i || o.push({
      Currency: t,
      Price: s
    }), this.internalValue = {
      ...this.internalValue,
      [e]: o
    }, this.databasePrices) {
      const n = this.normalizeValue(this.rawValue), c = n[e] ?? (n[e] = []), d = c.find((h) => h.Currency === t);
      d ? (d.Price = s, d.EditId = crypto.randomUUID()) : c.push({ Currency: t, Price: s, EditId: crypto.randomUUID() }), this.rawValue = n, this.rebuildValue(), this.setStatus("Price changed. Save the document to make it live.");
    } else
      this.rawValue = this.internalValue;
    this.emitChange();
  }
  priceUrl(e) {
    if (!this.alias) throw new Error("The price property alias is unavailable.");
    return `/ekom/backoffice/PriceEditor/${encodeURIComponent(e)}/${encodeURIComponent(this.alias)}`;
  }
  getPrice(e, t) {
    var r, s;
    return ((s = (r = this.internalValue[e]) == null ? void 0 : r.find((i) => i.Currency === t)) == null ? void 0 : s.Price) ?? 0;
  }
  ensurePriceStructure(e) {
    const t = Object.fromEntries(Object.entries(e).map(([r, s]) => [
      r,
      s.map((i) => ({ ...i }))
    ]));
    for (const r of this.stores) {
      const s = r.alias;
      if (s == null)
        continue;
      const i = t[s] ?? [];
      for (const o of r.currencies ?? []) {
        const n = o.currencyValue;
        n != null && (i.some((c) => c.Currency === n) || i.push({
          Currency: n,
          Price: 0
        }));
      }
      t[s] = i;
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
    for (const [r, s] of Object.entries(e))
      if (r !== "undefined") {
        if (!Array.isArray(s))
          return;
        t[r] = s.map((i) => {
          if (!(!this.isRecord(i) || !("Currency" in i) || !("Price" in i)))
            return {
              Currency: String(i.Currency),
              Price: this.parsePrice(i.Price),
              EditId: typeof i.EditId == "string" ? i.EditId : void 0
            };
        }).filter((i) => i != null);
      }
    return t;
  }
  transformLegacyValue(e) {
    var s, i, o;
    const t = {}, r = ((o = (i = (s = this.stores[0]) == null ? void 0 : s.currencies) == null ? void 0 : i[0]) == null ? void 0 : o.currencyValue) ?? "";
    for (const [n, c] of Object.entries(e))
      n === "undefined" || !this.isRecord(c) || (t[n] = Object.values(c).map((d) => {
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
      e.disabled = this.readonly || this.loading || this.hasDocumentContext && !this.documentId;
  }
  setStatus(e, t = !1) {
    this.status != null && (this.status.textContent = e, this.status.dataset.error = String(t));
  }
  emitChange() {
    this.dispatchEvent(new y());
  }
  get activeDocumentId() {
    return this.hasDocumentContext ? this.documentId : this.documentId || this.getDocumentIdFromUrl();
  }
  updateDocumentId(e) {
    const t = e ?? "", r = t !== this.documentId;
    if (this.hasDocumentContext = !0, !r) {
      this.syncDisabledState();
      return;
    }
    this.documentId ? this.resetDocumentState() : (this.requestId++, this.documentRevision++, this.overrides = []), this.loading = !1, this.documentId = t, this.syncDisabledState(), this.loadStores();
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
customElements.define("ekom-price-editor", v);
export {
  v as EkomPriceEditorElement,
  v as default
};
