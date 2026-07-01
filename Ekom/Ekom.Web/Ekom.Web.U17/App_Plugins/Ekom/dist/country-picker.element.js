var l = Object.defineProperty;
var u = (r, i, e) => i in r ? l(r, i, { enumerable: !0, configurable: !0, writable: !0, value: e }) : r[i] = e;
var s = (r, i, e) => u(r, typeof i != "symbol" ? i + "" : i, e);
import { UmbChangeEvent as c } from "@umbraco-cms/backoffice/event";
class d extends HTMLElement {
  constructor() {
    super(...arguments);
    s(this, "manifest");
    s(this, "name");
    s(this, "dataSourceAlias");
    s(this, "config");
    s(this, "mandatory");
    s(this, "mandatoryMessage");
    s(this, "select");
    s(this, "status");
    s(this, "countries", []);
    s(this, "internalValue", "");
  }
  get value() {
    return this.internalValue;
  }
  set value(e) {
    this.internalValue = typeof e == "string" ? e : "", this.syncValue();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncDisabledState();
  }
  connectedCallback() {
    this.renderShell(), this.loadCountries();
  }
  async loadCountries() {
    this.setStatus("Loading countries...");
    try {
      this.countries = await this.fetchJson("/ekom/api/countries"), this.renderOptions(), this.setStatus("");
    } catch (e) {
      const t = e instanceof Error ? e.message : "Could not load countries.";
      this.setStatus(t, !0);
    }
  }
  renderShell() {
    var e;
    this.innerHTML = `
      <style>
        :host { display: block; }
        .ekom-country-picker { display: grid; gap: var(--uui-size-space-2, 8px); justify-items: start; }
        select { box-sizing: border-box; min-width: 260px; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        select:disabled { cursor: not-allowed; opacity: 0.55; }
        p { margin: 0; color: var(--uui-color-text-alt, #515054); }
        p[data-error='true'] { color: var(--uui-color-danger, #d42054); }
      </style>
      <div class="ekom-country-picker">
        <select></select>
        <p aria-live="polite"></p>
      </div>
    `, this.select = this.querySelector("select") ?? void 0, this.status = this.querySelector("p") ?? void 0, (e = this.select) == null || e.addEventListener("change", () => this.setCountryValue()), this.syncDisabledState();
  }
  renderOptions() {
    var t;
    if (this.select == null)
      return;
    const e = document.createDocumentFragment();
    for (const n of this.countries) {
      const a = this.getCountryCode(n);
      if (a.length === 0)
        continue;
      const o = document.createElement("option");
      o.value = a, o.textContent = this.getCountryLabel(n, a), e.append(o);
    }
    this.select.replaceChildren(e), this.internalValue.length === 0 && this.select.options.length > 0 && (this.internalValue = ((t = this.select.options[0]) == null ? void 0 : t.value) ?? "", this.emitChange()), this.syncValue();
  }
  setCountryValue() {
    this.readonly || this.select == null || (this.internalValue = this.select.value, this.emitChange());
  }
  syncValue() {
    this.select != null && (this.select.value = this.internalValue);
  }
  syncDisabledState() {
    var e;
    (e = this.select) == null || e.toggleAttribute("disabled", this.readonly);
  }
  getCountryCode(e) {
    return e.code ?? e.Code ?? "";
  }
  getCountryLabel(e, t) {
    return `${e.name ?? e.Name ?? t} (${t})`;
  }
  setStatus(e, t = !1) {
    this.status != null && (this.status.textContent = e, this.status.dataset.error = String(t));
  }
  async fetchJson(e) {
    const t = await fetch(e, {
      credentials: "same-origin"
    });
    if (!t.ok)
      throw new Error(`Request failed: ${t.status}`);
    return await t.json();
  }
  emitChange() {
    this.dispatchEvent(new c());
  }
}
customElements.define("ekom-country-picker", d);
export {
  d as EkomCountryPickerElement,
  d as default
};
