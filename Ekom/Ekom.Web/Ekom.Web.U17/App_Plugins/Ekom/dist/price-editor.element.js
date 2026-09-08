var p = Object.defineProperty;
var m = (l, c, e) => c in l ? p(l, c, { enumerable: !0, configurable: !0, writable: !0, value: e }) : l[c] = e;
var a = (l, c, e) => m(l, typeof c != "symbol" ? c + "" : c, e);
import { UmbChangeEvent as f } from "@umbraco-cms/backoffice/event";
import { UmbElementMixin as y } from "@umbraco-cms/backoffice/element-api";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT as g } from "@umbraco-cms/backoffice/document";
class b extends y(HTMLElement) {
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
    a(this, "requestId", 0);
    a(this, "rawValue");
    a(this, "internalValue", {});
  }
  get value() {
    return this.internalValue;
  }
  set value(e) {
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
    super.connectedCallback(), this.renderShell(), this.setStatus("Loading prices..."), this.loadStores(), this.consumeContext(g, (e) => {
      e != null && (this.updateDocumentId(e.getUnique()), this.observe(e.unique, (t) => this.updateDocumentId(t), "ekomPriceDocumentId"));
    });
  }
  disconnectedCallback() {
    this.requestId++, super.disconnectedCallback();
  }
  async loadStores() {
    const e = this.documentId || this.getDocumentIdFromUrl();
    if (e.length === 0) {
      this.setStatus("Save the document before editing prices.");
      return;
    }
    const t = ++this.requestId;
    this.setStatus("Loading prices...");
    try {
      const r = await this.fetchJson(`/ekom/backoffice/Stores/${encodeURIComponent(e)}`);
      if (t !== this.requestId)
        return;
      this.stores = r;
      const i = Object.keys(this.internalValue).length > 0 ? this.internalValue : this.normalizeValue(this.rawValue);
      this.internalValue = this.ensurePriceStructure(i), this.renderPrices(), this.setStatus("");
    } catch (r) {
      if (t !== this.requestId)
        return;
      const i = r instanceof Error ? r.message : "Could not load prices.";
      this.setStatus(i, !0);
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
      const n = document.createElement(t ? "fieldset" : "div");
      if (t) {
        const o = document.createElement("legend");
        o.textContent = i, n.append(o);
      }
      for (const o of r.currencies ?? [])
        o.currencyValue != null && n.append(this.createPriceInput(i, o));
      e.append(n);
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
    const u = document.createElement("span");
    return u.textContent = t.currencySymbol ?? "", i.append(o, s, u), i;
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
    }, this.rawValue = this.internalValue, this.emitChange();
  }
  getPrice(e, t) {
    var r, i;
    return ((i = (r = this.internalValue[e]) == null ? void 0 : r.find((n) => n.Currency === t)) == null ? void 0 : i.Price) ?? 0;
  }
  ensurePriceStructure(e) {
    const t = Object.fromEntries(Object.entries(e).map(([r, i]) => [
      r,
      i.map((n) => ({ ...n }))
    ]));
    for (const r of this.stores) {
      const i = r.alias;
      if (i == null)
        continue;
      const n = t[i] ?? [];
      for (const o of r.currencies ?? []) {
        const s = o.currencyValue;
        s != null && (n.some((u) => u.Currency === s) || n.push({
          Currency: s,
          Price: 0
        }));
      }
      t[i] = n;
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
    for (const [s, u] of Object.entries(e))
      s === "undefined" || !this.isRecord(u) || (t[s] = Object.values(u).map((d) => {
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
    this.dispatchEvent(new f());
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
customElements.define("ekom-price-editor", b);
export {
  b as EkomPriceEditorElement,
  b as default
};
