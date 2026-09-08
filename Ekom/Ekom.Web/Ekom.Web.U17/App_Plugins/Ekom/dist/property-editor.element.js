var m = Object.defineProperty;
var b = (d, s, t) => s in d ? m(d, s, { enumerable: !0, configurable: !0, writable: !0, value: t }) : d[s] = t;
var a = (d, s, t) => b(d, typeof s != "symbol" ? s + "" : s, t);
import { UmbChangeEvent as y } from "@umbraco-cms/backoffice/event";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT as C } from "@umbraco-cms/backoffice/document";
import { createExtensionElement as v } from "@umbraco-cms/backoffice/extension-api";
import { umbExtensionsRegistry as T } from "@umbraco-cms/backoffice/extension-registry";
import { UmbLitElement as w } from "@umbraco-cms/backoffice/lit-element";
import { UMB_PROPERTY_CONTEXT as E, UMB_PROPERTY_DATASET_CONTEXT as N } from "@umbraco-cms/backoffice/property";
import { UmbPropertyEditorConfigCollection as S } from "@umbraco-cms/backoffice/property-editor";
const c = "00000000-0000-0000-0000-000000000000", f = "ekomCurrentTab", p = "ekom-property-title-changed", g = "ekom-property-tab-changed";
class k extends w {
  constructor() {
    super(...arguments);
    a(this, "manifest");
    a(this, "name");
    a(this, "dataSourceAlias");
    a(this, "mandatory");
    a(this, "mandatoryMessage");
    a(this, "editor");
    a(this, "editorContainer");
    a(this, "status");
    a(this, "tabsContainer");
    a(this, "propertyAlias", "");
    a(this, "propertyContext");
    a(this, "propertyDatasetContext");
    a(this, "rawConfig");
    a(this, "wrappedDataType");
    a(this, "tabs", []);
    a(this, "currentTab");
    a(this, "documentId", "");
    a(this, "loadRequestId", 0);
    a(this, "loading", !0);
    a(this, "failed", !1);
    a(this, "errorMessage", "");
    a(this, "lastAutofilledNodeName");
    a(this, "manuallyEditedSlugTabs", /* @__PURE__ */ new Set());
    a(this, "onTitleChanged", (t) => this.handleTitleChanged(t));
    a(this, "onTabChanged", (t) => this.handleTabChanged(t));
    a(this, "internalValue", {
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
    super.connectedCallback(), this.consumeContext(C, (t) => {
      t != null && (this.updateDocumentId(t.getUnique()), this.observe(t.unique, (e) => this.updateDocumentId(e), "ekomPropertyDocumentId"));
    }), this.consumeContext(E, (t) => {
      t != null && (this.propertyContext = t, this.observe(t.alias, (e) => {
        const r = e ?? "";
        r !== this.propertyAlias && (this.propertyAlias = r, this.load());
      }, "ekomPropertyAlias"));
    }), this.consumeContext(N, (t) => {
      t != null && (this.propertyDatasetContext = t, this.observe(t.name, (e) => this.tryAutofillFromNodeName(e), "ekomPropertyNodeName"));
    }), this.renderShell(), window.addEventListener(p, this.onTitleChanged), window.addEventListener(g, this.onTabChanged), this.load();
  }
  destroy() {
    var t, e;
    window.removeEventListener(p, this.onTitleChanged), window.removeEventListener(g, this.onTabChanged), (e = (t = this.editor) == null ? void 0 : t.destroy) == null || e.call(t);
  }
  async load() {
    var e;
    if (!this.isConnected || this.rawConfig == null)
      return;
    const t = ++this.loadRequestId;
    this.setLoading();
    try {
      const r = this.getConfigObject(), i = this.extractGuid(r.dataType);
      if (i == null)
        throw new Error("No wrapped data type has been configured for this Ekom property.");
      const l = await this.fetchJson(`/ekom/backoffice/DataType/${i}`), n = !!r.useLanguages, u = this.getContentKey(), o = n ? await this.loadLanguageTabs(u) : await this.loadStoreTabs();
      if (t !== this.loadRequestId)
        return;
      this.wrappedDataType = l, this.internalValue.type = n ? "Language" : "Store", this.tabs = o, this.currentTab = this.getStoredTab() ?? this.tabs[0], this.loading = !1, this.failed = !1, this.syncStatus(), this.renderTabs(), this.tryAutofillFromNodeName((e = this.propertyDatasetContext) == null ? void 0 : e.getName()), await this.renderCurrentEditor();
    } catch (r) {
      if (t !== this.loadRequestId)
        return;
      this.loading = !1, this.failed = !0, this.errorMessage = r instanceof Error ? r.message : "Could not render the property.", this.syncStatus();
    }
  }
  async loadLanguageTabs(t) {
    const e = t != null ? `/ekom/backoffice/Languages/${encodeURIComponent(t)}` : "/ekom/backoffice/Languages";
    return (await this.fetchJson(e)).filter((i) => i.isoCode != null).map((i) => ({
      value: i.isoCode ?? "",
      text: i.cultureName ?? i.isoCode ?? ""
    }));
  }
  async loadStoreTabs() {
    const t = this.propertyAlias === "disable" ? "1" : this.getDocumentId();
    return (await this.fetchJson(`/ekom/backoffice/Stores/${encodeURIComponent(t)}`)).filter((r) => r.alias != null).map((r) => ({
      value: r.alias ?? "",
      text: r.title ?? r.alias ?? ""
    }));
  }
  async renderCurrentEditor() {
    var i, l, n, u;
    if (this.editorContainer == null || this.loading || this.failed || this.currentTab == null || this.wrappedDataType == null)
      return;
    const t = this.wrappedDataType.view;
    if (t == null || t.length === 0)
      throw new Error("The wrapped data type does not expose a property editor UI alias.");
    const e = T.getByAlias(t);
    if (e == null)
      throw new Error(`Could not find property editor UI "${t}".`);
    (l = (i = this.editor) == null ? void 0 : i.destroy) == null || l.call(i), this.editorContainer.replaceChildren();
    const r = await v(e);
    if (r == null)
      throw new Error(`Could not create property editor UI "${t}".`);
    r.manifest = e, r.name = `${this.name ?? this.propertyAlias}.${this.currentTab.value}`, r.value = (n = this.internalValue.values) == null ? void 0 : n[this.currentTab.value], r.config = new S(this.getWrappedConfig()), r.readonly = this.readonly, r.mandatory = !1, this.stringIsNullOrWhiteSpace(this.mandatoryMessage) || (r.mandatoryMessage = this.mandatoryMessage), r.toggleAttribute("readonly", this.readonly), r.addEventListener("change", (o) => this.onWrappedEditorChange(o)), r.addEventListener("property-value-change", (o) => this.onWrappedEditorChange(o)), this.editor = r, this.editorContainer.append(r), this.tryAutofillFromNodeName((u = this.propertyDatasetContext) == null ? void 0 : u.getName());
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
    var o;
    if (!this.isCreateMode() || this.tabs.length === 0)
      return;
    if (this.stringIsNullOrWhiteSpace(t)) {
      this.lastAutofilledNodeName = void 0;
      return;
    }
    const e = this.propertyAlias === "title", r = this.propertyAlias === "slug";
    if (!e && !r || t === this.lastAutofilledNodeName)
      return;
    const i = e ? t : this.slugify(t);
    let l = !1, n = !1;
    const u = { ...this.internalValue.values };
    for (const h of this.tabs)
      r && this.manuallyEditedSlugTabs.has(h.value) || (u[h.value] = i, n = !0, h.value === ((o = this.currentTab) == null ? void 0 : o.value) && (l = !0));
    if (!n) {
      this.lastAutofilledNodeName = t;
      return;
    }
    this.internalValue = {
      ...this.internalValue,
      values: u
    }, l && this.editor != null && (this.editor.value = i), this.lastAutofilledNodeName = t, this.emitChange();
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
    var e, r, i;
    const t = (r = (e = window.Umbraco) == null ? void 0 : e.Sys) == null ? void 0 : r.ServerVariables;
    return ((i = t == null ? void 0 : t.ekom) == null ? void 0 : i.charCollections) ?? [];
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
      const i = document.createElement("button");
      i.type = "button", i.className = "ekom-tab", i.textContent = r.text, i.setAttribute("role", "tab"), i.setAttribute("aria-selected", String(r.value === ((e = this.currentTab) == null ? void 0 : e.value))), i.addEventListener("click", () => this.setCurrentTab(r)), t.append(i);
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
    return Array.isArray(t) ? t : t == null ? [] : Object.entries(t).map(([r, i]) => ({
      alias: r,
      value: i
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
      const i = Number.parseInt(e, 10);
      if (!Number.isNaN(i))
        return i;
    }
    const r = t.pathname.split("/").reverse().find((i) => /^\d+$/.test(i));
    return r == null ? 0 : Number.parseInt(r, 10);
  }
  getDocumentId() {
    return this.documentId || this.getContentKey() || String(this.getNodeId());
  }
  updateDocumentId(t) {
    t == null || t === this.documentId || (this.documentId = t, this.load());
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
