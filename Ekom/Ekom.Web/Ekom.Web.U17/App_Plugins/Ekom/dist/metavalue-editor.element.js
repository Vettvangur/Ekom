var l = Object.defineProperty;
var c = (n, o, e) => o in n ? l(n, o, { enumerable: !0, configurable: !0, writable: !0, value: e }) : n[o] = e;
var r = (n, o, e) => c(n, typeof o != "symbol" ? o + "" : o, e);
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
  set value(e) {
    this.items = this.normalizeValue(e), this.renderRows();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncDisabledState();
  }
  connectedCallback() {
    this.renderShell(), this.loadLanguages();
  }
  async loadLanguages() {
    this.setStatus("Loading languages...");
    try {
      this.languages = await this.fetchJson("/ekom/backoffice/Languages"), this.renderRows(), this.setStatus("");
    } catch (e) {
      const t = e instanceof Error ? e.message : "Could not load languages.";
      this.setStatus(t, !0);
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
    const e = document.createDocumentFragment();
    if (this.languages.length > 0 && this.items.length > 0) {
      const a = document.createElement("div");
      a.className = "header";
      for (const s of this.languages) {
        const i = document.createElement("div");
        i.textContent = s.cultureName ?? s.isoCode ?? "", a.append(i);
      }
      a.append(document.createElement("div")), e.append(a);
    }
    this.items.forEach((a, s) => e.append(this.createRow(a, s)));
    const t = document.createElement("button");
    t.type = "button", t.textContent = "Add", t.addEventListener("click", (a) => this.addItem(a)), e.append(t), this.editor.replaceChildren(e), this.syncDisabledState();
  }
  createRow(e, t) {
    const a = document.createElement("div");
    a.className = "row";
    for (const i of this.languages) {
      const u = i.isoCode ?? "", d = document.createElement("input");
      d.type = "text", d.value = e.values[u] ?? "", d.addEventListener("input", () => this.setLanguageValue(t, u, d.value)), a.append(d);
    }
    const s = document.createElement("div");
    return s.className = "actions", s.append(
      this.createActionButton("↑", () => this.moveItem(t, -1), "secondary", t === 0),
      this.createActionButton("↓", () => this.moveItem(t, 1), "secondary", t === this.items.length - 1),
      this.createActionButton("Remove", () => this.removeItem(t), "danger")
    ), a.append(s), a;
  }
  createActionButton(e, t, a, s = !1) {
    const i = document.createElement("button");
    return i.type = "button", i.textContent = e, a != null && (i.dataset.kind = a), i.dataset.disabledWhenEnabled = String(s), i.disabled = s, i.addEventListener("click", (u) => {
      u.preventDefault(), this.readonly || t();
    }), i;
  }
  addItem(e) {
    if (e.preventDefault(), this.readonly)
      return;
    const t = {};
    for (const a of this.languages)
      a.isoCode != null && (t[a.isoCode] = "");
    this.items = [
      ...this.items,
      {
        id: Math.random().toString(16).slice(2),
        values: t
      }
    ], this.renderRows(), this.emitChange();
  }
  removeItem(e) {
    this.items = this.items.filter((t, a) => a !== e), this.renderRows(), this.emitChange();
  }
  moveItem(e, t) {
    const a = e + t;
    if (a < 0 || a >= this.items.length)
      return;
    const s = [...this.items], [i] = s.splice(e, 1);
    s.splice(a, 0, i), this.items = s, this.renderRows(), this.emitChange();
  }
  setLanguageValue(e, t, a) {
    this.items[e] != null && (this.items = this.items.map((i, u) => u === e ? {
      ...i,
      values: {
        ...i.values,
        [t]: a
      }
    } : i), this.emitChange());
  }
  normalizeValue(e) {
    return Array.isArray(e) ? e.map((t) => {
      if (this.isRecord(t))
        return {
          id: String(t.id ?? Math.random().toString(16).slice(2)),
          values: this.isRecord(t.values) ? this.normalizeValues(t.values) : {}
        };
    }).filter((t) => t != null) : [];
  }
  normalizeValues(e) {
    const t = {};
    for (const [a, s] of Object.entries(e))
      t[a] = s == null ? "" : String(s);
    return t;
  }
  syncDisabledState() {
    for (const e of this.querySelectorAll("input"))
      e.disabled = this.readonly;
    for (const e of this.querySelectorAll("button"))
      e.disabled = this.readonly || e.dataset.disabledWhenEnabled === "true";
  }
  setStatus(e, t = !1) {
    this.status != null && (this.status.textContent = e, this.status.dataset.error = String(t));
  }
  emitChange() {
    this.dispatchEvent(new h());
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
customElements.define("ekom-metavalue-editor", m);
export {
  m as EkomMetavalueEditorElement,
  m as default
};
