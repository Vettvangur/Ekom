var c = Object.defineProperty;
var p = (o, l, e) => l in o ? c(o, l, { enumerable: !0, configurable: !0, writable: !0, value: e }) : o[l] = e;
var r = (o, l, e) => p(o, typeof l != "symbol" ? l + "" : l, e);
import { UmbChangeEvent as h } from "@umbraco-cms/backoffice/event";
class m extends HTMLElement {
  constructor() {
    super(...arguments);
    r(this, "manifest");
    r(this, "name");
    r(this, "dataSourceAlias");
    r(this, "config");
    r(this, "mandatory");
    r(this, "mandatoryMessage");
    r(this, "editor");
    r(this, "status");
    r(this, "languages", []);
    r(this, "fields", []);
    r(this, "items", []);
  }
  get value() {
    return this.items;
  }
  set value(e) {
    this.items = this.normalizeValue(e), this.syncInputs();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncDisabledState();
  }
  connectedCallback() {
    this.renderShell(), this.loadData();
  }
  async loadData() {
    this.setStatus("Loading metafields...");
    try {
      const [e, t] = await Promise.all([
        this.fetchJson("/ekom/backoffice/Languages"),
        this.fetchJson("/ekom/backoffice/Metafields")
      ]);
      this.languages = e, this.fields = t, this.ensureFieldValues(), this.renderFields(), this.setStatus(""), this.emitChange();
    } catch (e) {
      const t = e instanceof Error ? e.message : "Could not load metafields.";
      this.setStatus(t, !0);
    }
  }
  renderShell() {
    this.innerHTML = `
      <style>
        :host { display: block; }
        .ekom-metafield-picker { display: grid; gap: var(--uui-size-space-5, 20px); }
        .field { display: grid; gap: var(--uui-size-space-2, 8px); max-width: 680px; }
        .label-row { display: flex; align-items: start; justify-content: space-between; gap: var(--uui-size-space-4, 16px); }
        label { display: grid; gap: var(--uui-size-space-1, 4px); font-weight: 700; }
        small { display: block; color: var(--uui-color-text-alt, #515054); font-weight: 400; }
        input, select { box-sizing: border-box; width: 100%; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        select[multiple] { min-height: 130px; }
        button { border: 0; border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px) var(--uui-size-space-3, 12px); background: var(--uui-color-surface-alt, #f3f3f5); color: var(--uui-color-text, #1b264f); border: 1px solid var(--uui-color-border, #d8d7d9); cursor: pointer; font: inherit; font-weight: 600; white-space: nowrap; }
        button:disabled, input:disabled, select:disabled { cursor: not-allowed; opacity: 0.55; }
        p { margin: 0; color: var(--uui-color-text-alt, #515054); }
        p[data-error='true'] { color: var(--uui-color-danger, #d42054); }
      </style>
      <div class="ekom-metafield-picker"></div>
      <p aria-live="polite"></p>
    `, this.editor = this.querySelector(".ekom-metafield-picker") ?? void 0, this.status = this.querySelector("p") ?? void 0;
  }
  renderFields() {
    if (this.editor == null)
      return;
    const e = document.createDocumentFragment();
    if (this.fields.length === 0) {
      const t = document.createElement("p");
      t.textContent = "No metafields exist. You can create them under Metafields in Ekom", e.append(t);
    }
    this.fields.forEach((t, s) => e.append(this.createField(t, s))), this.editor.replaceChildren(e), this.syncDisabledState();
  }
  createField(e, t) {
    const s = document.createElement("div");
    s.className = "field";
    const i = document.createElement("div");
    i.className = "label-row";
    const a = document.createElement("label");
    if (a.htmlFor = `metafield_${t}`, a.textContent = e.name ?? e.key ?? "", !this.isEmpty(e.description)) {
      const u = document.createElement("small");
      u.textContent = e.description ?? "", a.append(u);
    }
    const n = document.createElement("button");
    n.type = "button", n.textContent = "Clear", n.addEventListener("click", (u) => this.clearField(u, e)), i.append(a, n), s.append(i);
    const d = e.values ?? [];
    return d.length > 0 ? s.append(this.createSelect(e, t, d)) : s.append(this.createTextInput(e, t)), s;
  }
  createSelect(e, t, s) {
    const i = document.createElement("select");
    if (i.id = `metafield_${t}`, i.dataset.key = e.key ?? "", i.multiple = e.enableMultipleChoice === !0, !i.multiple) {
      const a = document.createElement("option");
      a.value = "", a.textContent = "Select value", i.append(a);
    }
    for (const a of s) {
      const n = document.createElement("option");
      n.value = a.id ?? "", n.textContent = this.getMetavalueLabel(a), i.append(n);
    }
    return this.setSelectValue(i, e), i.addEventListener("change", () => this.setMetafieldSelectValue(e, i)), i;
  }
  createTextInput(e, t) {
    const s = document.createElement("input");
    return s.id = `metafield_${t}`, s.type = "text", s.dataset.key = e.key ?? "", s.value = String(this.getFieldValue(e) ?? ""), s.readOnly = e.readOnly === !0, s.addEventListener("input", () => this.setMetafieldValue(e, s.value)), s;
  }
  setSelectValue(e, t) {
    const s = new Set(this.getSelectedIds(t));
    for (const i of e.options)
      i.selected = s.has(i.value);
  }
  setMetafieldSelectValue(e, t) {
    if (this.readonly)
      return;
    const s = Array.from(t.selectedOptions).map((i) => (e.values ?? []).find((a) => a.id === i.value)).filter((i) => i != null);
    this.setMetafieldValue(e, t.multiple ? s : s[0] ?? "");
  }
  clearField(e, t) {
    var i;
    if (e.preventDefault(), this.readonly)
      return;
    const s = (((i = t.values) == null ? void 0 : i.length) ?? 0) > 0 && t.enableMultipleChoice === !0 ? [] : "";
    this.setMetafieldValue(t, s), this.syncInputs();
  }
  setMetafieldValue(e, t) {
    const s = e.key;
    s != null && (this.items = this.items.map((i) => i.key === s ? {
      key: s,
      values: t
    } : i), this.items.some((i) => i.key === s) || (this.items = [
      ...this.items,
      {
        key: s,
        values: t
      }
    ]), this.emitChange());
  }
  ensureFieldValues() {
    var t;
    const e = [...this.items];
    for (const s of this.fields) {
      const i = s.key;
      i == null || e.some((a) => a.key === i) || e.push({
        key: i,
        values: (((t = s.values) == null ? void 0 : t.length) ?? 0) > 0 ? [] : ""
      });
    }
    this.items = e;
  }
  syncInputs() {
    if (!(this.editor == null || this.fields.length === 0))
      for (const e of this.fields) {
        const t = e.key;
        if (t == null)
          continue;
        const s = this.editor.querySelector(`input[data-key="${CSS.escape(t)}"]`), i = this.editor.querySelector(`select[data-key="${CSS.escape(t)}"]`);
        s != null && (s.value = String(this.getFieldValue(e) ?? "")), i != null && this.setSelectValue(i, e);
      }
  }
  getFieldValue(e) {
    var t;
    return (t = this.items.find((s) => s.key === e.key)) == null ? void 0 : t.values;
  }
  getSelectedIds(e) {
    const t = this.getFieldValue(e);
    return Array.isArray(t) ? t.map((s) => this.isRecord(s) ? String(s.id ?? "") : String(s)) : this.isRecord(t) ? [String(t.id ?? "")] : t == null || t === "" ? [] : [String(t)];
  }
  getMetavalueLabel(e) {
    var s, i, a;
    const t = (s = this.languages[0]) == null ? void 0 : s.isoCode;
    return t != null && !this.isEmpty((i = e.values) == null ? void 0 : i[t]) ? ((a = e.values) == null ? void 0 : a[t]) ?? "" : Object.values(e.values ?? {}).find((n) => !this.isEmpty(n)) ?? e.id ?? "";
  }
  normalizeValue(e) {
    return Array.isArray(e) ? e.map((t) => {
      if (!(!this.isRecord(t) || typeof t.key != "string"))
        return {
          key: t.key,
          values: t.values ?? ""
        };
    }).filter((t) => t != null) : [];
  }
  syncDisabledState() {
    for (const e of this.querySelectorAll("input, select, button"))
      e.disabled = this.readonly;
    for (const e of this.querySelectorAll("input")) {
      const t = e.dataset.key, s = this.fields.find((i) => i.key === t);
      e.readOnly = (s == null ? void 0 : s.readOnly) === !0;
    }
  }
  setStatus(e, t = !1) {
    this.status != null && (this.status.textContent = e, this.status.dataset.error = String(t));
  }
  emitChange() {
    this.dispatchEvent(new h());
  }
  isEmpty(e) {
    return e == null || String(e).length === 0;
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
customElements.define("ekom-metafield-picker", m);
export {
  m as EkomMetafieldPickerElement,
  m as default
};
