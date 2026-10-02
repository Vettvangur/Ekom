var p = Object.defineProperty;
var h = (l, u, e) => u in l ? p(l, u, { enumerable: !0, configurable: !0, writable: !0, value: e }) : l[u] = e;
var o = (l, u, e) => h(l, typeof u != "symbol" ? u + "" : u, e);
import { UmbChangeEvent as m } from "@umbraco-cms/backoffice/event";
import { UmbElementMixin as g } from "@umbraco-cms/backoffice/element-api";
import { UMB_PROPERTY_CONTEXT as f, UMB_PROPERTY_DATASET_CONTEXT as y } from "@umbraco-cms/backoffice/property";
class b extends g(HTMLElement) {
  constructor() {
    super();
    o(this, "manifest");
    o(this, "name");
    o(this, "dataSourceAlias");
    o(this, "config");
    o(this, "mandatory");
    o(this, "mandatoryMessage");
    o(this, "editor");
    o(this, "status");
    o(this, "stores", []);
    o(this, "rawValue");
    o(this, "internalValue", {});
    o(this, "propertyAlias", "");
    o(this, "propertyDatasetContext");
    o(this, "percentageDiscount", !1);
    o(this, "discountTypeRequestId", 0);
    this.consumeContext(f, (e) => {
      e != null && this.observe(e.alias, (t) => {
        this.propertyAlias = t ?? "", this.observeDiscountType();
      }, "ekomRangePropertyAlias");
    }), this.consumeContext(y, (e) => {
      this.propertyDatasetContext = e, this.observeDiscountType();
    });
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
    super.connectedCallback(), this.renderShell(), this.loadStores();
  }
  async observeDiscountType() {
    const e = ++this.discountTypeRequestId;
    if (this.removeUmbControllerByAlias("ekomRangeDiscountType"), this.percentageDiscount = !1, this.syncRangeLabels(), this.propertyAlias !== "discount" || this.propertyDatasetContext == null)
      return;
    const t = await this.propertyDatasetContext.propertyValueByAlias("type");
    e !== this.discountTypeRequestId || !this.isConnected || this.observe(t, (r) => {
      this.percentageDiscount = typeof r == "string" && r.trim().toLowerCase() === "percentage", this.syncRangeLabels();
    }, "ekomRangeDiscountType");
  }
  syncRangeLabels() {
    var e;
    for (const t of ((e = this.editor) == null ? void 0 : e.querySelectorAll("[data-percentage-suffix]")) ?? [])
      t.hidden = !this.percentageDiscount;
  }
  async loadStores() {
    this.setStatus("Loading ranges...");
    try {
      this.stores = await this.fetchJson(`/ekom/backoffice/Stores/${this.getNodeId()}`), this.internalValue = this.ensureRangeStructure(this.normalizeValue(this.rawValue)), this.renderRanges(), this.setStatus("");
    } catch (e) {
      const t = e instanceof Error ? e.message : "Could not load ranges.";
      this.setStatus(t, !0);
    }
  }
  renderShell() {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-range-editor {
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

        .ekom-range-row {
          display: flex;
          align-items: center;
          gap: var(--uui-size-space-2, 8px);
          margin-bottom: var(--uui-size-space-3, 12px);
        }

        .ekom-range-row:last-child {
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
      <div class="ekom-range-editor"></div>
      <p aria-live="polite"></p>
    `, this.editor = this.querySelector(".ekom-range-editor") ?? void 0, this.status = this.querySelector("p") ?? void 0;
  }
  renderRanges() {
    if (this.editor == null)
      return;
    const e = document.createDocumentFragment();
    for (const t of this.stores) {
      const r = t.alias;
      if (r == null)
        continue;
      const n = document.createElement("fieldset"), s = document.createElement("legend");
      s.textContent = r, n.append(s);
      for (const i of t.currencies ?? [])
        i.currencyValue != null && n.append(this.createRangeInput(r, i));
      e.append(n);
    }
    this.editor.replaceChildren(e), this.syncRangeLabels(), this.syncDisabledState();
  }
  createRangeInput(e, t) {
    const r = t.currencyValue ?? "", n = document.createElement("div");
    n.className = "ekom-range-row";
    const s = `range_${t.isoCurrencySymbol ?? r}_${this.name ?? "range"}_${e}`, i = document.createElement("label");
    i.htmlFor = s, i.dataset.currencyLabel = t.isoCurrencySymbol ?? r, i.textContent = t.isoCurrencySymbol ?? r;
    const a = document.createElement("input");
    a.type = "number", a.min = "0", a.step = "any", a.id = s, a.dataset.store = e, a.dataset.currency = r, a.value = String(this.getRange(e, r)), a.addEventListener("input", () => this.setRange(e, r, a.value));
    const c = document.createElement("span");
    return c.dataset.percentageSuffix = "", c.textContent = "%", c.hidden = !this.percentageDiscount, n.append(i, a, c), n;
  }
  setRange(e, t, r) {
    const n = this.parseRange(r), s = [...this.internalValue[e] ?? []], i = s.find((a) => a.currency === t);
    i == null ? s.push({
      currency: t,
      value: n
    }) : i.value = n, this.internalValue = {
      ...this.internalValue,
      [e]: s
    }, this.emitChange();
  }
  getRange(e, t) {
    var r, n;
    return ((n = (r = this.internalValue[e]) == null ? void 0 : r.find((s) => s.currency === t)) == null ? void 0 : n.value) ?? 0;
  }
  ensureRangeStructure(e) {
    var r, n;
    const t = {};
    for (const s of this.stores) {
      const i = s.alias;
      if (i != null) {
        t[i] = [];
        for (const a of s.currencies ?? []) {
          const c = a.currencyValue;
          c != null && t[i].push({
            currency: c,
            value: ((n = (r = e[i]) == null ? void 0 : r.find((d) => d.currency === c)) == null ? void 0 : n.value) ?? 0
          });
        }
      }
    }
    return t;
  }
  normalizeValue(e) {
    return e == null || e === "" ? {} : this.isRecord(e) && this.isRecord(e.values) ? this.normalizeWrappedValue(e.values) : this.isRecord(e) ? this.normalizeCurrentFormat(e) ?? {} : this.normalizePrimitiveValue(e);
  }
  normalizeWrappedValue(e) {
    const t = {};
    for (const [r, n] of Object.entries(e)) {
      const s = typeof n == "string" ? this.tryParseJson(n) : n;
      Array.isArray(s) && (t[r] = this.normalizeRangeArray(s));
    }
    return t;
  }
  normalizeCurrentFormat(e) {
    const t = {};
    for (const [r, n] of Object.entries(e))
      if (r !== "undefined") {
        if (!Array.isArray(n))
          return;
        t[r] = this.normalizeRangeArray(n);
      }
    return t;
  }
  normalizeRangeArray(e) {
    return e.map((t) => {
      if (this.isRecord(t))
        return {
          currency: String(t.currency ?? t.Currency ?? ""),
          value: this.parseRange(t.value ?? t.Value)
        };
    }).filter((t) => t != null);
  }
  normalizePrimitiveValue(e) {
    var n, s, i, a;
    const t = ((n = this.stores[0]) == null ? void 0 : n.alias) ?? "", r = ((a = (i = (s = this.stores[0]) == null ? void 0 : s.currencies) == null ? void 0 : i[0]) == null ? void 0 : a.currencyValue) ?? "";
    return t.length === 0 || r.length === 0 ? {} : {
      [t]: [
        {
          currency: r,
          value: this.parseRange(e)
        }
      ]
    };
  }
  syncInputs() {
    if (this.editor != null)
      for (const e of this.editor.querySelectorAll("input[data-store][data-currency]")) {
        const t = e.dataset.store, r = e.dataset.currency;
        t == null || r == null || (e.value = String(this.getRange(t, r)));
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
      const n = Number.parseInt(t, 10);
      if (!Number.isNaN(n))
        return n;
    }
    const r = e.pathname.split("/").reverse().find((n) => /^\d+$/.test(n));
    return r == null ? 0 : Number.parseInt(r, 10);
  }
  parseRange(e) {
    if (e == null || e === "")
      return 0;
    const t = Number(String(e).replace(",", "."));
    return Number.isFinite(t) ? t : 0;
  }
  tryParseJson(e) {
    try {
      return JSON.parse(e);
    } catch {
      return e;
    }
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
customElements.define("ekom-range-editor", b);
export {
  b as EkomRangeEditorElement,
  b as default
};
