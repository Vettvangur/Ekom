var d = Object.defineProperty;
var c = (u, o, t) => o in u ? d(u, o, { enumerable: !0, configurable: !0, writable: !0, value: t }) : u[o] = t;
var r = (u, o, t) => c(u, typeof o != "symbol" ? o + "" : o, t);
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
    r(this, "items", []);
  }
  get value() {
    return this.items;
  }
  set value(t) {
    const e = this.normalizeValue(t), a = this.hasMatchingRows(e);
    this.items = e, a ? this.syncInputs() : this.renderRows();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(t) {
    this.toggleAttribute("readonly", t), this.syncDisabledState();
  }
  connectedCallback() {
    this.renderShell(), this.loadLanguages();
  }
  async loadLanguages() {
    this.setStatus("Loading languages...");
    try {
      this.languages = await this.fetchJson("/ekom/backoffice/Languages"), this.renderRows(), this.setStatus("");
    } catch (t) {
      const e = t instanceof Error ? t.message : "Could not load languages.";
      this.setStatus(e, !0);
    }
  }
  renderShell() {
    this.innerHTML = `
      <style>
        :host { display: block; }
        .ekom-metavalue-editor { display: grid; gap: var(--uui-size-space-4, 16px); max-width: 900px; }
        .header, .row { display: grid; grid-template-columns: repeat(var(--language-count, 1), minmax(160px, 1fr)) auto; gap: var(--uui-size-space-2, 8px); align-items: center; }
        .header { font-weight: 700; }
        input { box-sizing: border-box; width: 100%; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        .actions { display: flex; gap: var(--uui-size-space-1, 4px); }
        button { border: 0; border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px) var(--uui-size-space-3, 12px); background: var(--uui-color-interactive, #3544b1); color: var(--uui-color-interactive-contrast, #fff); cursor: pointer; font: inherit; font-weight: 600; }
        button[data-kind='danger'] { background: var(--uui-color-danger, #d42054); color: var(--uui-color-danger-contrast, #fff); }
        button[data-kind='secondary'] { background: var(--uui-color-surface-alt, #f3f3f5); color: var(--uui-color-text, #1b264f); border: 1px solid var(--uui-color-border, #d8d7d9); }
        button:disabled, input:disabled { cursor: not-allowed; opacity: 0.55; }
        p { margin: 0; color: var(--uui-color-text-alt, #515054); }
        p[data-error='true'] { color: var(--uui-color-danger, #d42054); }
      </style>
      <div class="ekom-metavalue-editor"></div>
      <p aria-live="polite"></p>
    `, this.editor = this.querySelector(".ekom-metavalue-editor") ?? void 0, this.status = this.querySelector("p") ?? void 0;
  }
  renderRows() {
    if (this.editor == null)
      return;
    this.editor.style.setProperty("--language-count", String(Math.max(this.languages.length, 1)));
    const t = document.createDocumentFragment();
    if (this.languages.length > 0 && this.items.length > 0) {
      const a = document.createElement("div");
      a.className = "header";
      for (const i of this.languages) {
        const s = document.createElement("div");
        s.textContent = i.cultureName ?? i.isoCode ?? "", a.append(s);
      }
      a.append(document.createElement("div")), t.append(a);
    }
    this.items.forEach((a, i) => t.append(this.createRow(a, i)));
    const e = document.createElement("button");
    e.type = "button", e.textContent = "Add", e.addEventListener("click", (a) => this.addItem(a)), t.append(e), this.editor.replaceChildren(t), this.syncDisabledState();
  }
  createRow(t, e) {
    const a = document.createElement("div");
    a.className = "row", a.dataset.itemId = t.id;
    for (const s of this.languages) {
      const n = s.isoCode ?? "", l = document.createElement("input");
      l.type = "text", l.dataset.language = n, l.value = t.values[n] ?? "", l.addEventListener("input", () => this.setLanguageValue(e, n, l.value)), a.append(l);
    }
    const i = document.createElement("div");
    return i.className = "actions", i.append(
      this.createActionButton("↑", () => this.moveItem(e, -1), "secondary", e === 0),
      this.createActionButton("↓", () => this.moveItem(e, 1), "secondary", e === this.items.length - 1),
      this.createActionButton("Remove", () => this.removeItem(e), "danger")
    ), a.append(i), a;
  }
  createActionButton(t, e, a, i = !1) {
    const s = document.createElement("button");
    return s.type = "button", s.textContent = t, a != null && (s.dataset.kind = a), s.dataset.disabledWhenEnabled = String(i), s.disabled = i, s.addEventListener("click", (n) => {
      n.preventDefault(), this.readonly || e();
    }), s;
  }
  addItem(t) {
    if (t.preventDefault(), this.readonly)
      return;
    const e = {};
    for (const a of this.languages)
      a.isoCode != null && (e[a.isoCode] = "");
    this.items = [
      ...this.items,
      {
        id: Math.random().toString(16).slice(2),
        values: e
      }
    ], this.renderRows(), this.emitChange();
  }
  removeItem(t) {
    this.items = this.items.filter((e, a) => a !== t), this.renderRows(), this.emitChange();
  }
  moveItem(t, e) {
    const a = t + e;
    if (a < 0 || a >= this.items.length)
      return;
    const i = [...this.items], [s] = i.splice(t, 1);
    i.splice(a, 0, s), this.items = i, this.renderRows(), this.emitChange();
  }
  setLanguageValue(t, e, a) {
    this.items[t] != null && (this.items = this.items.map((s, n) => n === t ? {
      ...s,
      values: {
        ...s.values,
        [e]: a
      }
    } : s), this.emitChange());
  }
  hasMatchingRows(t) {
    if (this.editor == null || this.editor.childElementCount === 0)
      return !1;
    const e = this.editor.querySelectorAll(".row");
    return e.length === t.length && t.every((a, i) => {
      var s;
      return ((s = e[i]) == null ? void 0 : s.dataset.itemId) === a.id;
    });
  }
  syncInputs() {
    if (this.editor == null)
      return;
    this.editor.querySelectorAll(".row").forEach((e, a) => {
      const i = this.items[a];
      if (i != null)
        for (const s of e.querySelectorAll("input[data-language]")) {
          const n = i.values[s.dataset.language ?? ""] ?? "";
          s.value !== n && (s.value = n);
        }
    });
  }
  normalizeValue(t) {
    return Array.isArray(t) ? t.map((e) => {
      if (this.isRecord(e))
        return {
          id: String(e.id ?? Math.random().toString(16).slice(2)),
          values: this.isRecord(e.values) ? this.normalizeValues(e.values) : {}
        };
    }).filter((e) => e != null) : [];
  }
  normalizeValues(t) {
    const e = {};
    for (const [a, i] of Object.entries(t))
      e[a] = i == null ? "" : String(i);
    return e;
  }
  syncDisabledState() {
    for (const t of this.querySelectorAll("input"))
      t.disabled = this.readonly;
    for (const t of this.querySelectorAll("button"))
      t.disabled = this.readonly || t.dataset.disabledWhenEnabled === "true";
  }
  setStatus(t, e = !1) {
    this.status != null && (this.status.textContent = t, this.status.dataset.error = String(e));
  }
  emitChange() {
    this.dispatchEvent(new h());
  }
  isRecord(t) {
    return t != null && typeof t == "object" && !Array.isArray(t);
  }
  async fetchJson(t) {
    const e = await fetch(t, {
      credentials: "same-origin",
      headers: {
        Accept: "application/json"
      }
    });
    if (!e.ok)
      throw new Error(`Request to ${t} failed with status ${e.status}.`);
    return await e.json();
  }
}
customElements.define("ekom-metavalue-editor", m);
export {
  m as EkomMetavalueEditorElement,
  m as default
};
