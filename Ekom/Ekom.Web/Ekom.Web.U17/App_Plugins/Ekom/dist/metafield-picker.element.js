var v = Object.defineProperty;
var k = (b, m, e) => m in b ? v(b, m, { enumerable: !0, configurable: !0, writable: !0, value: e }) : b[m] = e;
var p = (b, m, e) => k(b, typeof m != "symbol" ? m + "" : m, e);
import { UmbChangeEvent as w } from "@umbraco-cms/backoffice/event";
class S extends HTMLElement {
  constructor() {
    super(...arguments);
    p(this, "manifest");
    p(this, "name");
    p(this, "dataSourceAlias");
    p(this, "config");
    p(this, "mandatory");
    p(this, "mandatoryMessage");
    p(this, "editor");
    p(this, "status");
    p(this, "languages", []);
    p(this, "fields", []);
    p(this, "items", []);
    p(this, "handleDocumentClick", (e) => {
      const t = e.composedPath().find((o) => o instanceof Element && o.classList.contains("combobox"));
      (!(t instanceof Element) || !this.contains(t)) && this.closeDropdowns();
    });
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
    this.renderShell(), document.addEventListener("click", this.handleDocumentClick), this.loadData();
  }
  disconnectedCallback() {
    document.removeEventListener("click", this.handleDocumentClick);
  }
  async loadData() {
    this.setStatus("Loading metafields...");
    try {
      const [e, t] = await Promise.all([
        this.fetchJson("/ekom/backoffice/Languages"),
        this.fetchJson("/ekom/backoffice/Metafields")
      ]);
      this.languages = e, this.fields = t, this.ensureFieldValues(), this.renderFields(), this.setStatus("");
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
        input { box-sizing: border-box; width: 100%; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        .combobox { position: relative; }
        .combobox-control { display: flex; align-items: center; gap: var(--uui-size-space-2, 8px); box-sizing: border-box; width: 100%; min-height: 40px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-1, 4px) var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); cursor: pointer; }
        .combobox-control:focus { outline: 2px solid var(--uui-color-focus, #3544b1); outline-offset: 1px; }
        .combobox-control[aria-disabled='true'] { cursor: not-allowed; opacity: 0.55; }
        .combobox-value { display: flex; flex: 1; flex-wrap: wrap; gap: var(--uui-size-space-1, 4px); min-width: 0; }
        .placeholder { color: var(--uui-color-text-alt, #515054); }
        .combobox-arrow { flex: 0 0 auto; }
        .combobox-dropdown { position: absolute; z-index: 20; top: calc(100% + 4px); left: 0; right: 0; display: grid; gap: var(--uui-size-space-2, 8px); border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); box-shadow: 0 4px 12px rgb(0 0 0 / 18%); }
        .combobox-dropdown[hidden] { display: none; }
        .chip { display: inline-flex; align-items: center; gap: var(--uui-size-space-1, 4px); border-radius: 999px; padding: 3px 6px 3px 10px; background: var(--uui-color-surface-alt, #f3f3f5); color: var(--uui-color-text, #1b264f); }
        .chip button { display: grid; place-items: center; width: 20px; height: 20px; border: 0; border-radius: 50%; padding: 0; background: transparent; color: inherit; cursor: pointer; font: inherit; font-size: 18px; line-height: 1; }
        .chip button:hover { background: var(--uui-color-border, #d8d7d9); }
        .option-list { display: grid; max-height: 220px; overflow-y: auto; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); background: var(--uui-color-surface, #fff); }
        .option { display: flex; align-items: center; gap: var(--uui-size-space-2, 8px); border: 0; padding: var(--uui-size-space-2, 8px); background: transparent; color: inherit; font: inherit; font-weight: 400; text-align: left; cursor: pointer; }
        .option + .option { border-top: 1px solid var(--uui-color-border, #d8d7d9); }
        .option:hover, .option:focus { background: var(--uui-color-surface-alt, #f3f3f5); }
        .option-mark { width: 16px; text-align: center; }
        .empty-options { padding: var(--uui-size-space-3, 12px); color: var(--uui-color-text-alt, #515054); }
        .clear-button { border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-1, 4px) var(--uui-size-space-2, 8px); background: var(--uui-color-surface-alt, #f3f3f5); color: var(--uui-color-text, #1b264f); cursor: pointer; font: inherit; white-space: nowrap; }
        button:disabled, input:disabled { cursor: not-allowed; opacity: 0.55; }
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
    this.fields.forEach((t, o) => e.append(this.createField(t, o))), this.editor.replaceChildren(e), this.syncDisabledState();
  }
  createField(e, t) {
    const o = document.createElement("div");
    o.className = "field";
    const s = document.createElement("div");
    s.className = "label-row";
    const a = document.createElement("label");
    a.htmlFor = `metafield_${t}`, a.textContent = e.name ?? e.key ?? "";
    const r = `metafield_${t}_description`;
    if (!this.isEmpty(e.description)) {
      const d = document.createElement("small");
      d.id = r, d.textContent = e.description ?? "", a.append(d);
    }
    const l = document.createElement("button");
    l.type = "button", l.className = "clear-button", l.dataset.key = e.key ?? "", l.dataset.action = "clear", l.textContent = "Clear", l.addEventListener("click", (d) => this.clearField(d, e)), s.append(a, l), o.append(s);
    const c = e.values ?? [];
    return c.length > 0 ? o.append(e.enableMultipleChoice === !0 ? this.createMultiSelect(e, t, r) : this.createSelect(e, t, c, r)) : o.append(this.createTextInput(e, t, r)), o;
  }
  createSelect(e, t, o, s) {
    return this.createChoicePicker(e, t, s, !1);
  }
  createMultiSelect(e, t, o) {
    return this.createChoicePicker(e, t, o, !0);
  }
  createChoicePicker(e, t, o, s) {
    const a = document.createElement("div");
    a.className = `combobox ${s ? "multi-picker" : "single-picker"}`, a.dataset.key = e.key ?? "";
    const r = document.createElement("div");
    r.id = `metafield_${t}`, r.className = "combobox-control", r.dataset.key = e.key ?? "", r.dataset.control = "choice", r.tabIndex = 0, r.setAttribute("role", "combobox"), r.setAttribute("aria-haspopup", "listbox"), r.setAttribute("aria-expanded", "false"), r.setAttribute("aria-label", e.name ?? e.key ?? "Metafield"), this.setDescription(r, e, o);
    const l = document.createElement("div");
    l.className = "combobox-value", l.setAttribute("aria-label", s ? "Selected values" : "Selected value");
    const c = document.createElement("span");
    c.className = "combobox-arrow", c.setAttribute("aria-hidden", "true"), c.textContent = "▾", r.append(l, c);
    const d = document.createElement("div");
    d.className = "combobox-dropdown", d.hidden = !0;
    const n = document.createElement("input");
    n.type = "search", n.placeholder = "Search values", n.dataset.key = e.key ?? "", n.dataset.control = "search", n.setAttribute("aria-label", `Search ${e.name ?? e.key ?? "metafield"} values`), n.addEventListener("input", () => this.renderChoiceOptions(e, a, n.value, s)), n.addEventListener("keydown", (i) => {
      var h;
      i.key === "Escape" ? (i.preventDefault(), this.closeDropdown(a, !0)) : i.key === "ArrowDown" && (i.preventDefault(), (h = a.querySelector(".option")) == null || h.focus());
    });
    const u = document.createElement("div");
    return u.className = "option-list", u.setAttribute("role", "listbox"), u.setAttribute("aria-label", "Available values"), u.setAttribute("aria-multiselectable", String(s)), r.addEventListener("click", () => this.toggleDropdown(e, a, s)), r.addEventListener("keydown", (i) => {
      i.key === "Enter" || i.key === " " || i.key === "ArrowDown" ? (i.preventDefault(), this.openDropdown(e, a, s)) : i.key === "Escape" && (i.preventDefault(), this.closeDropdown(a));
    }), d.append(n, u), a.append(r, d), this.syncChoicePicker(e, a, s), a;
  }
  createTextInput(e, t, o) {
    const s = document.createElement("input");
    return s.id = `metafield_${t}`, s.type = "text", s.dataset.key = e.key ?? "", s.dataset.control = "text", s.value = String(this.getFieldValue(e) ?? ""), s.readOnly = e.readOnly === !0, this.setDescription(s, e, o), s.addEventListener("input", () => this.setMetafieldValue(e, s.value)), s;
  }
  clearField(e, t) {
    var s;
    if (e.preventDefault(), this.isFieldReadonly(t))
      return;
    const o = (((s = t.values) == null ? void 0 : s.length) ?? 0) > 0 && t.enableMultipleChoice === !0 ? [] : "";
    this.setMetafieldValue(t, o), this.syncInputs();
  }
  setMetafieldValue(e, t) {
    const o = e.key;
    o != null && (this.items = this.items.map((s) => s.key === o ? {
      key: o,
      values: t
    } : s), this.items.some((s) => s.key === o) || (this.items = [
      ...this.items,
      {
        key: o,
        values: t
      }
    ]), this.syncDisabledState(), this.emitChange());
  }
  ensureFieldValues() {
    var t;
    const e = [...this.items];
    for (const o of this.fields) {
      const s = o.key;
      s == null || e.some((a) => a.key === s) || e.push({
        key: s,
        values: (((t = o.values) == null ? void 0 : t.length) ?? 0) > 0 ? [] : ""
      });
    }
    this.items = e;
  }
  syncInputs() {
    if (!(this.editor == null || this.fields.length === 0)) {
      for (const e of this.fields) {
        const t = e.key;
        if (t == null)
          continue;
        const o = this.editor.querySelector(`input[data-control="text"][data-key="${CSS.escape(t)}"]`), s = this.editor.querySelector(`.multi-picker[data-key="${CSS.escape(t)}"]`), a = this.editor.querySelector(`.single-picker[data-key="${CSS.escape(t)}"]`);
        o != null && (o.value = String(this.getFieldValue(e) ?? "")), s != null && this.syncChoicePicker(e, s, !0), a != null && this.syncChoicePicker(e, a, !1);
      }
      this.syncDisabledState();
    }
  }
  getFieldValue(e) {
    var t;
    return (t = this.items.find((o) => o.key === e.key)) == null ? void 0 : t.values;
  }
  getSelectedIds(e) {
    const t = this.getFieldValue(e);
    return Array.isArray(t) ? t.map((o) => this.isRecord(o) ? String(o.id ?? "") : String(o)) : this.isRecord(t) ? [String(t.id ?? "")] : t == null || t === "" ? [] : [String(t)];
  }
  syncChoicePicker(e, t, o) {
    const s = new Set(this.getSelectedIds(e)), a = (e.values ?? []).filter((c) => s.has(c.id ?? "")), r = t.querySelector(".combobox-value"), l = t.querySelector('input[type="search"]');
    if (r != null)
      if (o) {
        const c = a.map((d) => {
          const n = this.getMetavalueLabel(d), u = document.createElement("span");
          u.className = "chip", u.append(document.createTextNode(n));
          const i = document.createElement("button");
          return i.type = "button", i.dataset.key = e.key ?? "", i.setAttribute("aria-label", `Remove ${n}`), i.textContent = "×", i.addEventListener("click", (h) => {
            h.preventDefault(), h.stopPropagation(), this.toggleMetavalue(e, d, !1, t);
          }), u.append(i), u;
        });
        c.length === 0 ? r.replaceChildren(this.createPlaceholder("Select values")) : r.replaceChildren(...c);
      } else {
        const c = a[0];
        r.replaceChildren(c == null ? this.createPlaceholder("Select value") : document.createTextNode(this.getMetavalueLabel(c)));
      }
    this.isDropdownClosed(t) || this.renderChoiceOptions(e, t, (l == null ? void 0 : l.value) ?? "", o), this.syncDisabledState();
  }
  createPlaceholder(e) {
    const t = document.createElement("span");
    return t.className = "placeholder", t.textContent = e, t;
  }
  toggleDropdown(e, t, o) {
    this.isDropdownClosed(t) ? this.openDropdown(e, t, o) : this.closeDropdown(t);
  }
  openDropdown(e, t, o) {
    if (this.isFieldReadonly(e))
      return;
    this.closeDropdowns(t);
    const s = t.querySelector(".combobox-control"), a = t.querySelector(".combobox-dropdown"), r = t.querySelector('input[type="search"]');
    s == null || a == null || r == null || (a.hidden = !1, s.setAttribute("aria-expanded", "true"), this.renderChoiceOptions(e, t, r.value, o), r.focus());
  }
  closeDropdown(e, t = !1) {
    const o = e.querySelector(".combobox-control"), s = e.querySelector(".combobox-dropdown"), a = e.querySelector('input[type="search"]'), r = e.querySelector(".option-list");
    s != null && (s.hidden = !0, o == null || o.setAttribute("aria-expanded", "false"), r == null || r.replaceChildren(), a != null && (a.value = ""), t && (o == null || o.focus()));
  }
  closeDropdowns(e) {
    for (const t of this.querySelectorAll(".combobox"))
      t !== e && this.closeDropdown(t);
  }
  isDropdownClosed(e) {
    var t;
    return ((t = e.querySelector(".combobox-dropdown")) == null ? void 0 : t.hidden) !== !1;
  }
  renderChoiceOptions(e, t, o, s) {
    const a = t.querySelector(".option-list");
    if (a == null || this.isDropdownClosed(t))
      return;
    const r = o.trim().toLocaleLowerCase(), l = new Set(this.getSelectedIds(e)), c = (e.values ?? []).filter((n) => this.getMetavalueLabel(n).toLocaleLowerCase().includes(r));
    if (c.length === 0) {
      const n = document.createElement("span");
      n.className = "empty-options", n.textContent = "No matching values", a.replaceChildren(n);
      return;
    }
    const d = c.map((n) => {
      const u = n.id ?? "", i = document.createElement("button"), h = l.has(u);
      i.type = "button", i.className = "option", i.dataset.key = e.key ?? "", i.dataset.valueId = u, i.setAttribute("role", "option"), i.setAttribute("aria-selected", String(h));
      const y = document.createElement("span");
      y.className = "option-mark", y.setAttribute("aria-hidden", "true"), y.textContent = h ? "✓" : "";
      const g = document.createElement("span");
      return g.textContent = this.getMetavalueLabel(n), i.append(y, g), i.addEventListener("click", (f) => {
        var x;
        f.preventDefault(), f.stopPropagation(), s ? (this.toggleMetavalue(e, n, !h, t), (x = t.querySelector(`button[data-value-id="${CSS.escape(u)}"]`)) == null || x.focus()) : this.selectMetavalue(e, n, t);
      }), i.addEventListener("keydown", (f) => this.handleOptionKeydown(f, t)), i;
    });
    a.replaceChildren(...d);
  }
  toggleMetavalue(e, t, o, s) {
    if (this.isFieldReadonly(e))
      return;
    const a = t.id ?? "", r = new Set(this.getSelectedIds(e));
    o ? r.add(a) : r.delete(a);
    const l = (e.values ?? []).filter((c) => r.has(c.id ?? ""));
    this.setMetafieldValue(e, l), this.syncChoicePicker(e, s, !0);
  }
  selectMetavalue(e, t, o) {
    this.isFieldReadonly(e) || (this.setMetafieldValue(e, t), this.syncChoicePicker(e, o, !1), this.closeDropdown(o, !0));
  }
  handleOptionKeydown(e, t) {
    var r;
    if (e.key === "Escape") {
      e.preventDefault(), this.closeDropdown(t, !0);
      return;
    }
    if (e.key !== "ArrowDown" && e.key !== "ArrowUp")
      return;
    e.preventDefault();
    const o = Array.from(t.querySelectorAll(".option")), s = o.indexOf(e.currentTarget), a = e.key === "ArrowDown" ? Math.min(s + 1, o.length - 1) : Math.max(s - 1, 0);
    (r = o[a]) == null || r.focus();
  }
  setDescription(e, t, o) {
    this.isEmpty(t.description) || e.setAttribute("aria-describedby", o);
  }
  getMetavalueLabel(e) {
    var o, s, a;
    const t = (o = this.languages[0]) == null ? void 0 : o.isoCode;
    return t != null && !this.isEmpty((s = e.values) == null ? void 0 : s[t]) ? ((a = e.values) == null ? void 0 : a[t]) ?? "" : Object.values(e.values ?? {}).find((r) => !this.isEmpty(r)) ?? e.id ?? "";
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
    for (const e of this.querySelectorAll("input, button")) {
      const t = e.dataset.key, o = this.fields.find((l) => l.key === t), s = e instanceof HTMLInputElement && e.dataset.control === "text", a = e.dataset.action === "clear" && (o == null || this.isFieldValueEmpty(o)), r = this.readonly || !s && (o == null ? void 0 : o.readOnly) === !0 || a;
      e.toggleAttribute("disabled", r);
    }
    for (const e of this.querySelectorAll('input[data-control="text"]')) {
      const t = e.dataset.key, o = this.fields.find((s) => s.key === t);
      e.readOnly = (o == null ? void 0 : o.readOnly) === !0;
    }
    for (const e of this.querySelectorAll(".combobox-control")) {
      const t = this.fields.find((s) => s.key === e.dataset.key), o = t == null || this.isFieldReadonly(t);
      if (e.setAttribute("aria-disabled", String(o)), e.tabIndex = o ? -1 : 0, o) {
        const s = e.closest(".combobox");
        s != null && this.closeDropdown(s);
      }
    }
  }
  isFieldReadonly(e) {
    return this.readonly || e.readOnly === !0;
  }
  isFieldValueEmpty(e) {
    const t = this.getFieldValue(e);
    return Array.isArray(t) ? t.length === 0 : t == null || t === "";
  }
  setStatus(e, t = !1) {
    this.status != null && (this.status.textContent = e, this.status.dataset.error = String(t));
  }
  emitChange() {
    this.dispatchEvent(new w());
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
customElements.define("ekom-metafield-picker", S);
export {
  S as EkomMetafieldPickerElement,
  S as default
};
