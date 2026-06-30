var r = Object.defineProperty;
var l = (n, i, e) => i in n ? r(n, i, { enumerable: !0, configurable: !0, writable: !0, value: e }) : n[i] = e;
var s = (n, i, e) => l(n, typeof i != "symbol" ? i + "" : i, e);
import { UmbChangeEvent as d } from "@umbraco-cms/backoffice/event";
class c extends HTMLElement {
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
    s(this, "options", []);
    s(this, "internalValue");
    s(this, "loaded", !1);
  }
  get value() {
    return this.internalValue;
  }
  set value(e) {
    this.internalValue = this.normalizeValue(e), this.syncSelection();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncDisabledState();
  }
  connectedCallback() {
    this.render(), this.loadDataTypes();
  }
  async loadDataTypes() {
    if (!this.loaded) {
      this.setStatus("Loading data types...");
      try {
        const e = await fetch("/ekom/backoffice/GetNonEkomDataTypes", {
          credentials: "same-origin",
          headers: {
            Accept: "application/json"
          }
        });
        if (!e.ok)
          throw new Error(`Data type request failed with status ${e.status}.`);
        this.options = await e.json(), this.options.sort((t, a) => t.name.localeCompare(a.name)), this.loaded = !0, this.renderOptions(), this.syncSelection(), this.setStatus("");
      } catch (e) {
        const t = e instanceof Error ? e.message : "Could not load data types.";
        this.setStatus(t, !0);
      }
    }
  }
  onChange() {
    var t;
    const e = (t = this.select) == null ? void 0 : t.value;
    this.internalValue = this.options.find((a) => a.guid === e), this.dispatchEvent(new d());
  }
  render() {
    var e;
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-data-type-picker {
          display: grid;
          gap: var(--uui-size-space-2, 8px);
        }

        select {
          box-sizing: border-box;
          width: 100%;
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
      <div class="ekom-data-type-picker">
        <select></select>
        <p aria-live="polite"></p>
      </div>
    `, this.select = this.querySelector("select") ?? void 0, this.status = this.querySelector("p") ?? void 0, (e = this.select) == null || e.addEventListener("change", () => this.onChange()), this.renderOptions(), this.syncSelection(), this.syncDisabledState();
  }
  renderOptions() {
    if (this.select == null)
      return;
    const e = document.createDocumentFragment(), t = document.createElement("option");
    t.value = "", t.textContent = "Select a data type", e.append(t);
    for (const a of this.options) {
      const o = document.createElement("option");
      o.value = a.guid, o.textContent = `${a.name} (${a.editorAlias})`, e.append(o);
    }
    this.select.replaceChildren(e);
  }
  syncSelection() {
    var e;
    this.select != null && (this.select.value = ((e = this.internalValue) == null ? void 0 : e.guid) ?? "");
  }
  syncDisabledState() {
    this.select != null && (this.select.disabled = this.readonly);
  }
  setStatus(e, t = !1) {
    this.status != null && (this.status.textContent = e, this.status.dataset.error = String(t));
  }
  normalizeValue(e) {
    if (e != null && typeof e == "object" && "guid" in e) {
      const t = e;
      if (typeof t.guid == "string")
        return {
          guid: t.guid,
          name: t.name ?? t.guid,
          editorAlias: t.editorAlias ?? ""
        };
    }
  }
}
customElements.define("ekom-data-type-picker", c);
export {
  c as EkomDataTypePickerElement,
  c as default
};
