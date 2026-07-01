var c = Object.defineProperty;
var l = (s, u, e) => u in s ? c(s, u, { enumerable: !0, configurable: !0, writable: !0, value: e }) : s[u] = e;
var i = (s, u, e) => l(s, typeof u != "symbol" ? u + "" : u, e);
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
    i(this, "cultureInput");
    i(this, "formatInput");
    i(this, "addButton");
    i(this, "removeButton");
    i(this, "select");
    i(this, "currencies", []);
  }
  get value() {
    return this.currencies;
  }
  set value(e) {
    this.currencies = this.normalizeValue(e), this.renderOptions();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncDisabledState();
  }
  connectedCallback() {
    this.render();
  }
  addCurrency(e) {
    if (e.preventDefault(), this.readonly || this.cultureInput == null || this.formatInput == null)
      return;
    const r = this.cultureInput.value.trim(), t = this.formatInput.value.trim();
    r.length === 0 || t.length === 0 || (this.currencies = [
      ...this.currencies,
      {
        currencyFormat: t,
        currencyValue: r,
        sort: this.currencies.length
      }
    ], this.cultureInput.value = "", this.formatInput.value = "", this.renderOptions(), this.emitChange());
  }
  removeCurrency(e) {
    if (e.preventDefault(), this.readonly || this.select == null || this.select.value.length === 0)
      return;
    const r = Number.parseInt(this.select.value, 10);
    Number.isNaN(r) || (this.currencies = this.currencies.filter((t, n) => n !== r).map((t, n) => ({
      ...t,
      sort: n
    })), this.renderOptions(), this.emitChange());
  }
  render() {
    var e, r;
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-currency-picker {
          display: grid;
          gap: var(--uui-size-space-4, 16px);
          justify-items: start;
        }

        .ekom-currency-form {
          display: flex;
          flex-wrap: wrap;
          align-items: end;
          gap: var(--uui-size-space-4, 16px);
        }

        label {
          display: grid;
          gap: var(--uui-size-space-1, 4px);
          font-weight: 600;
        }

        input,
        select {
          box-sizing: border-box;
          min-height: 32px;
          border: 1px solid var(--uui-color-border, #d8d7d9);
          border-radius: var(--uui-border-radius, 3px);
          padding: var(--uui-size-space-2, 8px);
          background: var(--uui-color-surface, #fff);
          color: var(--uui-color-text, #1b264f);
          font: inherit;
        }

        select {
          min-width: 320px;
        }

        button {
          border: 0;
          border-radius: var(--uui-border-radius, 3px);
          padding: var(--uui-size-space-2, 8px) var(--uui-size-space-4, 16px);
          background: var(--uui-color-interactive, #3544b1);
          color: var(--uui-color-interactive-contrast, #fff);
          cursor: pointer;
          font: inherit;
          font-weight: 600;
        }

        button[data-kind='danger'] {
          background: var(--uui-color-danger, #d42054);
          color: var(--uui-color-danger-contrast, #fff);
        }

        button:disabled,
        input:disabled,
        select:disabled {
          cursor: not-allowed;
          opacity: 0.55;
        }
      </style>
      <div class="ekom-currency-picker">
        <div class="ekom-currency-form">
          <label>
            Currency Culture:
            <input type="text" name="currencyCulture" autocomplete="off" />
          </label>
          <label>
            Currency Format:
            <input type="text" name="currencyFormat" autocomplete="off" />
          </label>
          <button type="button">Add</button>
        </div>
        <label>
          Current Currencies:
          <select size="7"></select>
        </label>
        <button type="button" data-kind="danger">Remove</button>
      </div>
    `, this.cultureInput = this.querySelector('input[name="currencyCulture"]') ?? void 0, this.formatInput = this.querySelector('input[name="currencyFormat"]') ?? void 0, this.addButton = this.querySelector("button:not([data-kind])") ?? void 0, this.removeButton = this.querySelector('button[data-kind="danger"]') ?? void 0, this.select = this.querySelector("select") ?? void 0, (e = this.addButton) == null || e.addEventListener("click", (t) => this.addCurrency(t)), (r = this.removeButton) == null || r.addEventListener("click", (t) => this.removeCurrency(t)), this.renderOptions(), this.syncDisabledState();
  }
  renderOptions() {
    if (this.select == null)
      return;
    const e = document.createDocumentFragment(), r = [...this.currencies].sort((t, n) => t.sort - n.sort);
    for (const t of r) {
      const n = document.createElement("option");
      n.value = String(this.currencies.indexOf(t)), n.textContent = this.combine(t), e.append(n);
    }
    this.select.replaceChildren(e);
  }
  combine(e) {
    return `Culture: ${e.currencyValue} Format: ${e.currencyFormat}`;
  }
  normalizeValue(e) {
    return Array.isArray(e) ? e.filter((r) => r != null && typeof r == "object").map((r, t) => {
      const n = r;
      return {
        currencyFormat: n.currencyFormat ?? "",
        currencyValue: n.currencyValue ?? "",
        sort: n.sort ?? n.Sort ?? t
      };
    }) : [];
  }
  syncDisabledState() {
    var r, t, n, o, a;
    const e = this.readonly;
    (r = this.cultureInput) == null || r.toggleAttribute("disabled", e), (t = this.formatInput) == null || t.toggleAttribute("disabled", e), (n = this.select) == null || n.toggleAttribute("disabled", e), (o = this.addButton) == null || o.toggleAttribute("disabled", e), (a = this.removeButton) == null || a.toggleAttribute("disabled", e);
  }
  emitChange() {
    this.dispatchEvent(new d());
  }
}
customElements.define("ekom-currency-picker", h);
export {
  h as EkomCurrencyPickerElement,
  h as default
};
