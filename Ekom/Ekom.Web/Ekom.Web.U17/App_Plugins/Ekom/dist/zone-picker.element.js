var d = Object.defineProperty;
var c = (o, a, e) => a in o ? d(o, a, { enumerable: !0, configurable: !0, writable: !0, value: e }) : o[a] = e;
var i = (o, a, e) => c(o, typeof a != "symbol" ? a + "" : a, e);
import { UmbChangeEvent as u } from "@umbraco-cms/backoffice/event";
class h extends HTMLElement {
  constructor() {
    super(...arguments);
    i(this, "manifest");
    i(this, "name");
    i(this, "dataSourceAlias");
    i(this, "config");
    i(this, "mandatory");
    i(this, "mandatoryMessage");
    i(this, "availableSelect");
    i(this, "selectedSelect");
    i(this, "addButton");
    i(this, "removeButton");
    i(this, "status");
    i(this, "countries", []);
    i(this, "selectedCodes", []);
  }
  get value() {
    return this.selectedCodes.join(",");
  }
  set value(e) {
    this.selectedCodes = typeof e == "string" ? e.split(",").map((t) => t.trim()).filter((t) => t.length > 0) : [], this.renderOptions();
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
    var e, t;
    this.innerHTML = `
      <style>
        :host { display: block; }
        .ekom-zone-picker { display: flex; flex-wrap: wrap; align-items: center; gap: var(--uui-size-space-3, 12px); }
        .buttons { display: grid; gap: var(--uui-size-space-2, 8px); }
        label { display: grid; gap: var(--uui-size-space-1, 4px); font-weight: 600; }
        select { box-sizing: border-box; min-width: 240px; min-height: 180px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        button { border: 0; border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px) var(--uui-size-space-4, 16px); background: var(--uui-color-interactive, #3544b1); color: var(--uui-color-interactive-contrast, #fff); cursor: pointer; font: inherit; font-weight: 600; }
        button:disabled, select:disabled { cursor: not-allowed; opacity: 0.55; }
        p { flex-basis: 100%; margin: 0; color: var(--uui-color-text-alt, #515054); }
        p[data-error='true'] { color: var(--uui-color-danger, #d42054); }
      </style>
      <div class="ekom-zone-picker">
        <label>
          Available Countries
          <select data-list="available" size="10" multiple></select>
        </label>
        <div class="buttons">
          <button type="button" data-action="add">Add</button>
          <button type="button" data-action="remove">Remove</button>
        </div>
        <label>
          Selected Countries
          <select data-list="selected" size="10" multiple></select>
        </label>
        <p aria-live="polite"></p>
      </div>
    `, this.availableSelect = this.querySelector('select[data-list="available"]') ?? void 0, this.selectedSelect = this.querySelector('select[data-list="selected"]') ?? void 0, this.addButton = this.querySelector('button[data-action="add"]') ?? void 0, this.removeButton = this.querySelector('button[data-action="remove"]') ?? void 0, this.status = this.querySelector("p") ?? void 0, (e = this.addButton) == null || e.addEventListener("click", (s) => this.moveSelected(s, this.availableSelect, !0)), (t = this.removeButton) == null || t.addEventListener("click", (s) => this.moveSelected(s, this.selectedSelect, !1)), this.syncDisabledState();
  }
  renderOptions() {
    if (this.availableSelect == null || this.selectedSelect == null)
      return;
    const e = new Set(this.selectedCodes);
    this.availableSelect.replaceChildren(...this.createOptions(this.countries.filter((t) => !e.has(this.getCountryCode(t))))), this.selectedSelect.replaceChildren(...this.createOptions(this.countries.filter((t) => e.has(this.getCountryCode(t)))));
  }
  createOptions(e) {
    return e.slice().sort((t, s) => this.getCountryLabel(t).localeCompare(this.getCountryLabel(s))).map((t) => {
      const s = document.createElement("option");
      return s.value = this.getCountryCode(t), s.textContent = this.getCountryLabel(t), s;
    });
  }
  moveSelected(e, t, s) {
    if (e.preventDefault(), this.readonly || t == null)
      return;
    const r = Array.from(t.selectedOptions).map((l) => l.value);
    if (r.length !== 0) {
      if (s)
        this.selectedCodes = Array.from(/* @__PURE__ */ new Set([...this.selectedCodes, ...r]));
      else {
        const l = new Set(r);
        this.selectedCodes = this.selectedCodes.filter((n) => !l.has(n));
      }
      this.renderOptions(), this.emitChange();
    }
  }
  syncDisabledState() {
    var t, s, r, l;
    const e = this.readonly;
    (t = this.availableSelect) == null || t.toggleAttribute("disabled", e), (s = this.selectedSelect) == null || s.toggleAttribute("disabled", e), (r = this.addButton) == null || r.toggleAttribute("disabled", e), (l = this.removeButton) == null || l.toggleAttribute("disabled", e);
  }
  getCountryCode(e) {
    return e.code ?? e.Code ?? "";
  }
  getCountryLabel(e) {
    return e.name ?? e.Name ?? this.getCountryCode(e);
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
    this.dispatchEvent(new u());
  }
}
customElements.define("ekom-zone-picker", h);
export {
  h as EkomZonePickerElement,
  h as default
};
