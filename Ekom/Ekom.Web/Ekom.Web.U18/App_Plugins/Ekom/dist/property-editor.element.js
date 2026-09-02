var m = Object.defineProperty;
var b = (o, s, t) => s in o ? m(o, s, { enumerable: !0, configurable: !0, writable: !0, value: t }) : o[s] = t;
var i = (o, s, t) => b(o, typeof s != "symbol" ? s + "" : s, t);
import { UmbChangeEvent as y } from "@umbraco-cms/backoffice/event";
import { createExtensionElement as C } from "@umbraco-cms/backoffice/extension-api";
import { umbExtensionsRegistry as v } from "@umbraco-cms/backoffice/extension-registry";
import { UmbLitElement as T } from "@umbraco-cms/backoffice/lit-element";
import { UMB_PROPERTY_CONTEXT as w, UMB_PROPERTY_DATASET_CONTEXT as E } from "@umbraco-cms/backoffice/property";
import { UmbPropertyEditorConfigCollection as N } from "@umbraco-cms/backoffice/property-editor";
const c = "00000000-0000-0000-0000-000000000000", f = "ekomCurrentTab", p = "ekom-property-title-changed", g = "ekom-property-tab-changed";
class k extends T {
  constructor() {
    super(...arguments);
    i(this, "manifest");
    i(this, "name");
    i(this, "dataSourceAlias");
    i(this, "mandatory");
    i(this, "mandatoryMessage");
    i(this, "editor");
    i(this, "editorContainer");
    i(this, "status");
    i(this, "tabsContainer");
    i(this, "propertyAlias", "");
    i(this, "propertyContext");
    i(this, "propertyDatasetContext");
    i(this, "rawConfig");
    i(this, "wrappedDataType");
    i(this, "tabs", []);
    i(this, "currentTab");
    i(this, "loading", !0);
    i(this, "failed", !1);
    i(this, "errorMessage", "");
    i(this, "lastAutofilledNodeName");
    i(this, "manuallyEditedSlugTabs", /* @__PURE__ */ new Set());
    i(this, "onTitleChanged", (t) => this.handleTitleChanged(t));
    i(this, "onTabChanged", (t) => this.handleTabChanged(t));
    i(this, "internalValue", {
      values: {},
      dtdGuid: c,
      type: "Language"
    });
  }
  get value() {
    return this.internalValue;
  }
  set value(t) {
    this.internalValue = this.normalizeValue(t), this.syncCurrentEditorValue();
  }
  get config() {
    return this.rawConfig;
  }
  set config(t) {
    this.rawConfig = t, this.load();
  }
  get readonly() {
    return this.hasAttribute("readonly");
  }
  set readonly(t) {
    this.toggleAttribute("readonly", t), this.editor != null && (this.editor.readonly = t, this.editor.toggleAttribute("readonly", t));
  }
  connectedCallback() {
    super.connectedCallback(), this.consumeContext(w, (t) => {
      t != null && (this.propertyContext = t, this.observe(t.alias, (e) => {
        this.propertyAlias = e ?? "";
      }, "ekomPropertyAlias"));
    }), this.consumeContext(E, (t) => {
      t != null && (this.propertyDatasetContext = t, this.observe(t.name, (e) => this.tryAutofillFromNodeName(e), "ekomPropertyNodeName"));
    }), this.renderShell(), window.addEventListener(p, this.onTitleChanged), window.addEventListener(g, this.onTabChanged), this.load();
  }
  destroy() {
    var t, e;
    window.removeEventListener(p, this.onTitleChanged), window.removeEventListener(g, this.onTabChanged), (e = (t = this.editor) == null ? void 0 : t.destroy) == null || e.call(t);
  }
  async load() {
    var t;
    if (!(!this.isConnected || this.rawConfig == null)) {
      this.setLoading();
      try {
        const e = this.getConfigObject(), r = this.extractGuid(e.dataType);
        if (r == null)
          throw new Error("No wrapped data type has been configured for this Ekom property.");
        this.wrappedDataType = await this.fetchJson(`/ekom/backoffice/DataType/${r}`);
        const a = !!e.useLanguages;
        this.internalValue.type = a ? "Language" : "Store";
        const n = this.getContentKey();
        this.tabs = a ? await this.loadLanguageTabs(n) : await this.loadStoreTabs(this.getNodeId()), this.currentTab = this.getStoredTab() ?? this.tabs[0], this.loading = !1, this.failed = !1, this.syncStatus(), this.renderTabs(), this.tryAutofillFromNodeName((t = this.propertyDatasetContext) == null ? void 0 : t.getName()), await this.renderCurrentEditor();
      } catch (e) {
        this.loading = !1, this.failed = !0, this.errorMessage = e instanceof Error ? e.message : "Could not render the property.", this.syncStatus();
      }
    }
  }
  async loadLanguageTabs(t) {
    const e = t != null ? `/ekom/backoffice/Languages/${encodeURIComponent(t)}` : "/ekom/backoffice/Languages";
    return (await this.fetchJson(e)).filter((a) => a.isoCode != null).map((a) => ({
      value: a.isoCode ?? "",
      text: a.cultureName ?? a.isoCode ?? ""
    }));
  }
  async loadStoreTabs(t) {
    const e = this.propertyAlias === "disable" ? 1 : t;
    return (await this.fetchJson(`/ekom/backoffice/Stores/${e}`)).filter((a) => a.alias != null).map((a) => ({
      value: a.alias ?? "",
      text: a.title ?? a.alias ?? ""
    }));
  }
  async renderCurrentEditor() {
    var a, n, u, d;
    if (this.editorContainer == null || this.loading || this.failed || this.currentTab == null || this.wrappedDataType == null)
      return;
    const t = this.wrappedDataType.view;
    if (t == null || t.length === 0)
      throw new Error("The wrapped data type does not expose a property editor UI alias.");
    const e = v.getByAlias(t);
    if (e == null)
      throw new Error(`Could not find property editor UI "${t}".`);
    (n = (a = this.editor) == null ? void 0 : a.destroy) == null || n.call(a), this.editorContainer.replaceChildren();
    const r = await C(e);
    if (r == null)
      throw new Error(`Could not create property editor UI "${t}".`);
    r.manifest = e, r.name = `${this.name ?? this.propertyAlias}.${this.currentTab.value}`, r.value = (u = this.internalValue.values) == null ? void 0 : u[this.currentTab.value], r.config = new N(this.getWrappedConfig()), r.readonly = this.readonly, r.mandatory = !1, this.stringIsNullOrWhiteSpace(this.mandatoryMessage) || (r.mandatoryMessage = this.mandatoryMessage), r.toggleAttribute("readonly", this.readonly), r.addEventListener("change", (l) => this.onWrappedEditorChange(l)), r.addEventListener("property-value-change", (l) => this.onWrappedEditorChange(l)), this.editor = r, this.editorContainer.append(r), this.tryAutofillFromNodeName((d = this.propertyDatasetContext) == null ? void 0 : d.getName());
  }
  onWrappedEditorChange(t) {
    t.stopPropagation(), !(this.currentTab == null || this.editor == null) && (this.propertyAlias === "slug" && this.manuallyEditedSlugTabs.add(this.currentTab.value), this.internalValue = {
      ...this.internalValue,
      values: {
        ...this.internalValue.values,
        [this.currentTab.value]: this.editor.value
      }
    }, this.emitChange(), this.emitTitleChanged());
  }
  handleTitleChanged(t) {
    if (!this.isCreateMode() || this.propertyAlias !== "slug" || this.tabs.length === 0)
      return;
    const e = t.detail;
    e == null || this.stringIsNullOrWhiteSpace(e.tab) || this.manuallyEditedSlugTabs.has(e.tab) || this.setTabValue(e.tab, e.slug);
  }
  emitTitleChanged() {
    var e;
    if (!this.isCreateMode() || this.propertyAlias !== "title" || this.currentTab == null)
      return;
    const t = (e = this.editor) == null ? void 0 : e.value;
    typeof t == "string" && window.dispatchEvent(new CustomEvent(p, {
      detail: {
        tab: this.currentTab.value,
        title: t,
        slug: this.slugify(t)
      }
    }));
  }
  setCurrentTab(t) {
    localStorage.setItem(f, JSON.stringify(t.value)), this.selectTab(t.value), window.dispatchEvent(new CustomEvent(g, {
      detail: t.value
    }));
  }
  handleTabChanged(t) {
    var r;
    const e = t.detail;
    this.stringIsNullOrWhiteSpace(e) || e === ((r = this.currentTab) == null ? void 0 : r.value) || this.selectTab(e);
  }
  selectTab(t) {
    const e = this.tabs.find((r) => r.value === t);
    e != null && (this.currentTab = e, this.renderTabs(), this.renderCurrentEditor());
  }
  emitChange() {
    var t;
    (t = this.propertyContext) == null || t.setValue(this.internalValue), this.dispatchEvent(new y());
  }
  tryAutofillFromNodeName(t) {
    var l;
    if (!this.isCreateMode() || this.tabs.length === 0)
      return;
    if (this.stringIsNullOrWhiteSpace(t)) {
      this.lastAutofilledNodeName = void 0;
      return;
    }
    const e = this.propertyAlias === "title", r = this.propertyAlias === "slug";
    if (!e && !r || t === this.lastAutofilledNodeName)
      return;
    const a = e ? t : this.slugify(t);
    let n = !1, u = !1;
    const d = { ...this.internalValue.values };
    for (const h of this.tabs)
      r && this.manuallyEditedSlugTabs.has(h.value) || (d[h.value] = a, u = !0, h.value === ((l = this.currentTab) == null ? void 0 : l.value) && (n = !0));
    if (!u) {
      this.lastAutofilledNodeName = t;
      return;
    }
    this.internalValue = {
      ...this.internalValue,
      values: d
    }, n && this.editor != null && (this.editor.value = a), this.lastAutofilledNodeName = t, this.emitChange();
  }
  syncCurrentEditorValue() {
    var t;
    this.editor == null || this.currentTab == null || (this.editor.value = (t = this.internalValue.values) == null ? void 0 : t[this.currentTab.value]);
  }
  setTabValue(t, e) {
    var r;
    this.internalValue = {
      ...this.internalValue,
      values: {
        ...this.internalValue.values,
        [t]: e
      }
    }, t === ((r = this.currentTab) == null ? void 0 : r.value) && this.editor != null && (this.editor.value = e), this.emitChange();
  }
  isCreateMode() {
    return window.location.pathname.includes("/workspace/document/create/");
  }
  slugify(t) {
    let e = t;
    for (const r of this.getCharReplacements())
      this.stringIsNullOrWhiteSpace(r.Char) || (e = e.replaceAll(r.Char, r.Replacement ?? ""));
    return e.normalize("NFKD").toLowerCase().trim().replace(/\s+/g, "-").replace(/[^\w-]+/g, "").replace(/--+/g, "-");
  }
  getCharReplacements() {
    var e, r, a;
    const t = (r = (e = window.Umbraco) == null ? void 0 : e.Sys) == null ? void 0 : r.ServerVariables;
    return ((a = t == null ? void 0 : t.ekom) == null ? void 0 : a.charCollections) ?? [];
  }
  setLoading() {
    this.loading = !0, this.failed = !1, this.errorMessage = "", this.syncStatus();
  }
  renderShell() {
    const t = document.createElement("template");
    t.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-property-editor {
          display: grid;
          gap: var(--uui-size-space-4, 16px);
        }

        .ekom-tabs {
          display: flex;
          flex-wrap: wrap;
          gap: var(--uui-size-space-1, 4px);
          margin: 0;
          padding: 0;
          border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
          list-style: none;
        }

        .ekom-tab {
          border: 0;
          border-bottom: 3px solid transparent;
          padding: var(--uui-size-space-3, 12px) var(--uui-size-space-4, 16px);
          background: transparent;
          color: var(--uui-color-text, #1b264f);
          cursor: pointer;
          font: inherit;
        }

        .ekom-tab[aria-selected='true'] {
          border-bottom-color: var(--uui-color-interactive, #3544b1);
          font-weight: 700;
        }

        .ekom-status {
          color: var(--uui-color-text-alt, #515054);
        }

        .ekom-status[data-state='error'] {
          color: var(--uui-color-danger, #d42054);
        }
      </style>
      <div class="ekom-property-editor">
        <div class="ekom-tabs" role="tablist"></div>
        <div class="ekom-status" aria-live="polite"></div>
        <div class="ekom-editor"></div>
      </div>
    `, this.renderRoot.replaceChildren(t.content.cloneNode(!0)), this.tabsContainer = this.renderRoot.querySelector(".ekom-tabs") ?? void 0, this.status = this.renderRoot.querySelector(".ekom-status") ?? void 0, this.editorContainer = this.renderRoot.querySelector(".ekom-editor") ?? void 0, this.syncStatus();
  }
  renderTabs() {
    var e;
    if (this.tabsContainer == null)
      return;
    const t = document.createDocumentFragment();
    for (const r of this.tabs) {
      const a = document.createElement("button");
      a.type = "button", a.className = "ekom-tab", a.textContent = r.text, a.setAttribute("role", "tab"), a.setAttribute("aria-selected", String(r.value === ((e = this.currentTab) == null ? void 0 : e.value))), a.addEventListener("click", () => this.setCurrentTab(r)), t.append(a);
    }
    this.tabsContainer.replaceChildren(t);
  }
  syncStatus() {
    this.status != null && (this.status.dataset.state = this.failed ? "error" : this.loading ? "loading" : "idle", this.status.textContent = this.failed ? this.errorMessage : this.loading ? "Loading..." : this.tabs.length === 0 ? "No tabs are available for this property." : "");
  }
  getConfigObject() {
    var t;
    return ((t = this.rawConfig) == null ? void 0 : t.toObject()) ?? {};
  }
  getWrappedConfig() {
    var e;
    const t = (e = this.wrappedDataType) == null ? void 0 : e.preValues;
    return Array.isArray(t) ? t : t == null ? [] : Object.entries(t).map(([r, a]) => ({
      alias: r,
      value: a
    }));
  }
  getStoredTab() {
    const t = localStorage.getItem(f);
    if (t != null)
      try {
        const e = JSON.parse(t);
        return this.tabs.find((r) => r.value === e);
      } catch {
        return;
      }
  }
  getNodeId() {
    const t = new URL(window.location.href), e = t.searchParams.get("id");
    if (e != null) {
      const a = Number.parseInt(e, 10);
      if (!Number.isNaN(a))
        return a;
    }
    const r = t.pathname.split("/").reverse().find((a) => /^\d+$/.test(a));
    return r == null ? 0 : Number.parseInt(r, 10);
  }
  getContentKey() {
    return window.location.pathname.split("/").find((t) => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(t));
  }
  extractGuid(t) {
    if (typeof t == "string" && t.length > 0)
      return t;
    if (t != null && typeof t == "object" && "guid" in t) {
      const e = t.guid;
      return typeof e == "string" ? e : void 0;
    }
  }
  stringIsNullOrWhiteSpace(t) {
    return t == null || t.trim().length === 0;
  }
  normalizeValue(t) {
    if (t != null && typeof t == "object" && "values" in t) {
      const e = t;
      return {
        values: {
          ...e.values
        },
        dtdGuid: e.dtdGuid ?? c,
        type: e.type ?? "Language"
      };
    }
    return {
      values: {},
      dtdGuid: c,
      type: "Language"
    };
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
customElements.define("ekom-property-editor", k);
export {
  k as EkomPropertyEditorElement,
  k as default
};
