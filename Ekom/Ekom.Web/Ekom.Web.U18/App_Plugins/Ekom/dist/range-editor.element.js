var h = Object.defineProperty;
var p = (l, u, e) => u in l ? h(l, u, { enumerable: !0, configurable: !0, writable: !0, value: e }) : l[u] = e;
var o = (l, u, e) => p(l, typeof u != "symbol" ? u + "" : u, e);
import { UmbChangeEvent as m } from "@umbraco-cms/backoffice/event";
class g extends HTMLElement {
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
    o(this, "rawValue");
    o(this, "internalValue", {});
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
      for (const a of t.currencies ?? [])
        a.currencyValue != null && n.append(this.createRangeInput(r, a));
      e.append(n);
    }
    this.editor.replaceChildren(e), this.syncDisabledState();
  }
  createRangeInput(e, t) {
    const r = t.currencyValue ?? "", n = document.createElement("div");
    n.className = "ekom-range-row";
    const s = `range_${t.isoCurrencySymbol ?? r}_${this.name ?? "range"}_${e}`, a = document.createElement("label");
    a.htmlFor = s, a.textContent = t.isoCurrencySymbol ?? r;
    const i = document.createElement("input");
    return i.type = "number", i.min = "0", i.step = "any", i.id = s, i.dataset.store = e, i.dataset.currency = r, i.value = String(this.getRange(e, r)), i.addEventListener("input", () => this.setRange(e, r, i.value)), n.append(a, i), n;
  }
  setRange(e, t, r) {
    const n = this.parseRange(r), s = [...this.internalValue[e] ?? []], a = s.find((i) => i.currency === t);
    a == null ? s.push({
      currency: t,
      value: n
    }) : a.value = n, this.internalValue = {
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
      const a = s.alias;
      if (a != null) {
        t[a] = [];
        for (const i of s.currencies ?? []) {
          const c = i.currencyValue;
          c != null && t[a].push({
            currency: c,
            value: ((n = (r = e[a]) == null ? void 0 : r.find((d) => d.currency === c)) == null ? void 0 : n.value) ?? 0
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
    var n, s, a, i;
    const t = ((n = this.stores[0]) == null ? void 0 : n.alias) ?? "", r = ((i = (a = (s = this.stores[0]) == null ? void 0 : s.currencies) == null ? void 0 : a[0]) == null ? void 0 : i.currencyValue) ?? "";
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
customElements.define("ekom-range-editor", g);
export {
  g as EkomRangeEditorElement,
  g as default
};
