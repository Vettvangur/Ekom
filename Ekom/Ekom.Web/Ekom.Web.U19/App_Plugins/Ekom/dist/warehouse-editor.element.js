var c = Object.defineProperty;
var p = (u, d, e) => d in u ? c(u, d, { enumerable: !0, configurable: !0, writable: !0, value: e }) : u[d] = e;
var i = (u, d, e) => p(u, typeof d != "symbol" ? d + "" : d, e);
import { UmbChangeEvent as g } from "@umbraco-cms/backoffice/event";
class f extends HTMLElement {
  constructor() {
    super(...arguments);
    i(this, "manifest");
    i(this, "name");
    i(this, "dataSourceAlias");
    i(this, "config");
    i(this, "editor");
    i(this, "warehouses", []);
    i(this, "draggedWarehouseKey");
    i(this, "onFocusOut", (e) => {
      e.relatedTarget instanceof Node && this.contains(e.relatedTarget) || this.emitChange();
    });
  }
  get value() {
    return this.warehouses;
  }
  set value(e) {
    this.warehouses = this.normalizeValue(e), this.renderRows();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(e) {
    this.toggleAttribute("readonly", e), this.syncDisabledState();
  }
  connectedCallback() {
    this.addEventListener("focusout", this.onFocusOut), this.renderShell(), this.renderRows();
  }
  disconnectedCallback() {
    this.removeEventListener("focusout", this.onFocusOut);
  }
  renderShell() {
    this.innerHTML = `
      <style>
        :host { display: block; }
        .warehouse-editor { display: grid; gap: var(--uui-size-space-3, 12px); max-width: 1000px; }
        .header, .row { display: grid; grid-template-columns: 56px minmax(120px, 1fr) minmax(180px, 2fr) 90px 80px; gap: var(--uui-size-space-2, 8px); align-items: center; }
        .header { font-weight: 700; }
        .header > :nth-child(4) { text-align: center; }
        input { box-sizing: border-box; width: 100%; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        input[type='checkbox'] { width: auto; min-height: auto; justify-self: center; }
        button { border: 0; border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px) var(--uui-size-space-3, 12px); background: var(--uui-color-interactive, #3544b1); color: var(--uui-color-interactive-contrast, #fff); cursor: pointer; font: inherit; font-weight: 600; }
        button[data-kind='danger'] { background: var(--uui-color-danger, #d42054); color: var(--uui-color-danger-contrast, #fff); }
        button:disabled, input:disabled { cursor: not-allowed; opacity: 0.55; }
        .drag-handle { min-width: 32px; cursor: grab; }
        .drag-handle:active { cursor: grabbing; }
        @media (max-width: 700px) { .header { display: none; } .row { grid-template-columns: 56px 1fr 1fr; padding: var(--uui-size-space-3, 12px); border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); } .row input[data-field='name'] { grid-column: span 2; } }
      </style>
      <div class="warehouse-editor"></div>
    `, this.editor = this.querySelector(".warehouse-editor") ?? void 0;
  }
  renderRows() {
    if (this.editor == null)
      return;
    const e = document.createDocumentFragment();
    if (this.warehouses.length > 0) {
      const t = document.createElement("div");
      t.className = "header";
      for (const a of ["", "Code", "Name", "Visible", "Actions"]) {
        const s = document.createElement("div");
        s.textContent = a, t.append(s);
      }
      e.append(t);
    }
    this.warehouses.forEach((t, a) => e.append(this.createRow(t, a)));
    const r = document.createElement("button");
    r.type = "button", r.textContent = "Add warehouse", r.addEventListener("click", () => this.addWarehouse()), e.append(r), this.editor.replaceChildren(e), this.syncDisabledState();
  }
  createRow(e, r) {
    const t = document.createElement("div");
    t.className = "row", t.dataset.key = e.key, t.addEventListener("dragover", (o) => this.onDragOver(o)), t.addEventListener("drop", (o) => this.onDrop(o, e.key));
    const a = document.createElement("button");
    a.type = "button", a.className = "drag-handle", a.textContent = "↕", a.title = "Drag to reorder", a.setAttribute("aria-label", `Drag ${e.name || e.code || "warehouse"} to reorder`), a.draggable = !this.readonly, a.addEventListener("dragstart", (o) => this.onDragStart(o, e.key)), a.addEventListener("dragend", () => this.clearDragState()), t.append(a), t.append(
      this.createInput("code", "text", e.code, (o) => o),
      this.createInput("name", "text", e.name, (o) => o)
    );
    const s = document.createElement("input");
    s.type = "checkbox", s.checked = e.visible, s.dataset.field = "visible", s.addEventListener("change", () => this.updateWarehouse(r, "visible", s.checked)), t.append(s);
    const n = document.createElement("button");
    return n.type = "button", n.textContent = "Remove", n.dataset.kind = "danger", n.addEventListener("click", () => this.removeWarehouse(r)), t.append(n), t;
  }
  createInput(e, r, t, a) {
    const s = document.createElement("input");
    return s.type = r, s.value = t, s.dataset.field = e, s.addEventListener("input", () => {
      const n = this.warehouses.findIndex((o) => {
        var h;
        return o.key === ((h = s.closest(".row")) == null ? void 0 : h.dataset.key);
      });
      n !== -1 && this.updateWarehouse(n, e, a(s.value), !1);
    }), s;
  }
  addWarehouse() {
    this.readonly || (this.warehouses = [...this.warehouses, {
      key: crypto.randomUUID(),
      code: "",
      name: "",
      sortOrder: this.warehouses.length,
      visible: !0
    }], this.renumberWarehouses(), this.renderRows(), this.emitChange());
  }
  removeWarehouse(e) {
    this.readonly || (this.warehouses = this.warehouses.filter((r, t) => t !== e), this.renumberWarehouses(), this.renderRows(), this.emitChange());
  }
  updateWarehouse(e, r, t, a = !0) {
    this.readonly || this.warehouses[e] == null || (this.warehouses = this.warehouses.map((s, n) => n === e ? { ...s, [r]: t } : s), a && this.emitChange());
  }
  normalizeValue(e) {
    const r = typeof e == "string" ? this.tryParseJson(e) : e;
    return Array.isArray(r) ? r.flatMap((t) => this.isRecord(t) ? [{
      key: typeof t.key == "string" && t.key.length > 0 ? t.key : crypto.randomUUID(),
      code: t.code == null ? "" : String(t.code),
      name: t.name == null ? "" : String(t.name),
      sortOrder: this.parseSortOrder(t.sortOrder),
      visible: typeof t.visible == "boolean" ? t.visible : typeof t.enabled == "boolean" ? t.enabled : !0
    }] : []).sort((t, a) => t.sortOrder - a.sortOrder || t.name.localeCompare(a.name)) : [];
  }
  syncDisabledState() {
    for (const e of this.querySelectorAll("input"))
      e.disabled = this.readonly;
    for (const e of this.querySelectorAll("button"))
      e.disabled = this.readonly, e.classList.contains("drag-handle") && (e.draggable = !this.readonly);
    this.readonly && this.clearDragState();
  }
  onDragStart(e, r) {
    if (this.readonly || e.dataTransfer == null) {
      e.preventDefault();
      return;
    }
    this.draggedWarehouseKey = r, e.dataTransfer.effectAllowed = "move", e.dataTransfer.setData("text/plain", r);
  }
  onDragOver(e) {
    this.readonly || this.draggedWarehouseKey == null || (e.preventDefault(), e.dataTransfer.dropEffect = "move");
  }
  onDrop(e, r) {
    var h;
    e.preventDefault();
    const t = this.draggedWarehouseKey ?? ((h = e.dataTransfer) == null ? void 0 : h.getData("text/plain"));
    if (this.clearDragState(), this.readonly || t == null || t === r)
      return;
    const a = this.warehouses.findIndex((l) => l.key === t), s = this.warehouses.findIndex((l) => l.key === r);
    if (a < 0 || s < 0)
      return;
    const n = [...this.warehouses], [o] = n.splice(a, 1);
    n.splice(s, 0, o), this.warehouses = n, this.renumberWarehouses(), this.renderRows(), this.emitChange();
  }
  clearDragState() {
    this.draggedWarehouseKey = void 0;
  }
  renumberWarehouses() {
    this.warehouses = this.warehouses.map((e, r) => ({ ...e, sortOrder: r }));
  }
  parseSortOrder(e) {
    const r = Number(e);
    return Number.isFinite(r) ? Math.trunc(r) : 0;
  }
  tryParseJson(e) {
    try {
      return JSON.parse(e);
    } catch {
      return [];
    }
  }
  isRecord(e) {
    return e != null && typeof e == "object" && !Array.isArray(e);
  }
  emitChange() {
    this.dispatchEvent(new g());
  }
}
customElements.define("ekom-warehouse-editor", f);
export {
  f as EkomWarehouseEditorElement,
  f as default
};
