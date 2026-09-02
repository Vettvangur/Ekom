var D = Object.defineProperty;
var F = (i, o, t) => o in i ? D(i, o, { enumerable: !0, configurable: !0, writable: !0, value: t }) : i[o] = t;
var l = (i, o, t) => F(i, typeof o != "symbol" ? o + "" : o, t);
import { UmbElementMixin as M } from "@umbraco-cms/backoffice/element-api";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT as O, UMB_DOCUMENT_PUBLISHING_WORKSPACE_CONTEXT as W } from "@umbraco-cms/backoffice/document";
import "@umbraco-cms/backoffice/imaging";
import "@umbraco-cms/backoffice/media";
import { UMB_NOTIFICATION_CONTEXT as B } from "@umbraco-cms/backoffice/notification";
import { UMB_WORKSPACE_VIEW_CONTEXT as j } from "@umbraco-cms/backoffice/workspace";
const V = "00000000-0000-0000-0000-000000000000", m = "ekom-variant-count", R = "You have unsaved variant changes. Save & Publish will publish the product node and save/publish the variant changes. If you only want to save/publish variant changes, use the Save variant changes button at the top. Continue?";
class z extends M(HTMLElement) {
  constructor() {
    super(...arguments);
    l(this, "notificationContext");
    l(this, "workspaceViewContext");
    l(this, "product");
    l(this, "loading", !0);
    l(this, "saving", !1);
    l(this, "error", "");
    l(this, "productId", "");
    l(this, "activeLanguage", "");
    l(this, "nextDraftId", -1);
    l(this, "drawer");
    l(this, "expandedGroupIds", /* @__PURE__ */ new Set());
    l(this, "selectedGroupIds", /* @__PURE__ */ new Set());
    l(this, "selectedVariantIds", /* @__PURE__ */ new Set());
    l(this, "deletedNodeIds", /* @__PURE__ */ new Set());
    l(this, "groupSnapshots", /* @__PURE__ */ new Map());
    l(this, "variantSnapshots", /* @__PURE__ */ new Map());
    l(this, "draggedGroupId");
    l(this, "draggedVariant");
    l(this, "documentWorkspaceContext");
    l(this, "originalRequestSave");
    l(this, "originalRequestSubmit");
    l(this, "originalSaveAndPublish");
    l(this, "publishingWorkspaceContext");
    l(this, "originalPublishingSaveAndPublish");
    l(this, "onKeydown", (t) => {
      t.key !== "Escape" || this.drawer == null || (t.stopPropagation(), this.syncMediaPickers(), this.drawer = void 0, this.render());
    });
  }
  connectedCallback() {
    super.connectedCallback(), window.addEventListener("keydown", this.onKeydown), this.consumeContext(B, (t) => {
      this.notificationContext = t;
    }), this.consumeContext(j, (t) => {
      this.workspaceViewContext = t, this.updateWorkspaceViewBadge();
    }), this.consumeContext(O, (t) => {
      if (t == null)
        return;
      const e = t.getUnique();
      e != null && (this.productId = e), this.patchDocumentSave(t), this.observe(t.unique, (a) => {
        a == null || a === this.productId || (this.productId = a, this.load());
      }, "ekomVariantProductId");
    }), this.consumeContext(W, (t) => {
      t != null && this.patchPublishingSave(t);
    }), this.productId = this.getProductIdFromUrl(), this.render(), this.productId && this.load();
  }
  disconnectedCallback() {
    window.removeEventListener("keydown", this.onKeydown), this.restoreDocumentSave(), this.restorePublishingSave(), super.disconnectedCallback();
  }
  patchDocumentSave(t) {
    this.documentWorkspaceContext !== t && (this.restoreDocumentSave(), this.documentWorkspaceContext = t, t.requestSave != null && (this.originalRequestSave = t.requestSave.bind(t), t.requestSave = async () => {
      var e;
      await ((e = this.originalRequestSave) == null ? void 0 : e.call(this)), await this.saveAfterDocumentSave();
    }), t.requestSubmit != null && (this.originalRequestSubmit = t.requestSubmit.bind(t), t.requestSubmit = async () => {
      var e;
      this.confirmPublishWithVariantChanges() && (await ((e = this.originalRequestSubmit) == null ? void 0 : e.call(this)), await this.saveAfterDocumentSave());
    }), t.saveAndPublish != null && (this.originalSaveAndPublish = t.saveAndPublish.bind(t), t.saveAndPublish = async () => {
      var e;
      this.confirmPublishWithVariantChanges() && (await ((e = this.originalSaveAndPublish) == null ? void 0 : e.call(this)), await this.saveAfterDocumentSave());
    }));
  }
  restoreDocumentSave() {
    this.documentWorkspaceContext != null && (this.originalRequestSave != null && (this.documentWorkspaceContext.requestSave = this.originalRequestSave), this.originalRequestSubmit != null && (this.documentWorkspaceContext.requestSubmit = this.originalRequestSubmit), this.originalSaveAndPublish != null && (this.documentWorkspaceContext.saveAndPublish = this.originalSaveAndPublish), this.documentWorkspaceContext = void 0, this.originalRequestSave = void 0, this.originalRequestSubmit = void 0, this.originalSaveAndPublish = void 0);
  }
  patchPublishingSave(t) {
    this.publishingWorkspaceContext !== t && (this.restorePublishingSave(), this.publishingWorkspaceContext = t, t.saveAndPublish != null && (this.originalPublishingSaveAndPublish = t.saveAndPublish.bind(t), t.saveAndPublish = async () => {
      var e;
      this.confirmPublishWithVariantChanges() && (await ((e = this.originalPublishingSaveAndPublish) == null ? void 0 : e.call(this)), await this.saveAfterDocumentSave());
    }));
  }
  restorePublishingSave() {
    this.publishingWorkspaceContext != null && (this.originalPublishingSaveAndPublish != null && (this.publishingWorkspaceContext.saveAndPublish = this.originalPublishingSaveAndPublish), this.publishingWorkspaceContext = void 0, this.originalPublishingSaveAndPublish = void 0);
  }
  async saveAfterDocumentSave() {
    this.syncMediaPickers(), this.product != null && this.hasChanges() && await this.save();
  }
  confirmPublishWithVariantChanges() {
    return this.syncMediaPickers(), this.product == null || !this.hasChanges() ? !0 : window.confirm(R);
  }
  async load() {
    var e;
    const t = this.getProductId();
    if (!t) {
      this.loading = !1, this.error = "Could not determine the current product id.", this.showError(this.error), this.render();
      return;
    }
    this.loading = !0, this.error = "", this.render();
    try {
      this.product = await this.fetchJson(`/ekom/backoffice/Variants/${encodeURIComponent(t)}`), this.activeLanguage = ((e = this.product.languages[0]) == null ? void 0 : e.isoCode) ?? "", this.drawer = void 0, this.selectedGroupIds.clear(), this.selectedVariantIds.clear(), this.deletedNodeIds.clear(), this.expandedGroupIds.clear(), this.product.groups.length === 1 && this.expandedGroupIds.add(this.product.groups[0].id), this.resetSnapshots(), this.updateWorkspaceViewBadge();
    } catch (a) {
      this.product = void 0, this.updateWorkspaceViewBadge(), this.error = $(a, "Could not load variants."), this.showError(this.error);
    } finally {
      this.loading = !1, this.render();
    }
  }
  addGroup() {
    if (this.product == null)
      return;
    const t = this.nextDraftId--, e = {
      id: t,
      key: V,
      name: "New group",
      title: "",
      titleValues: this.createEmptyTitleValues(),
      color: "",
      images: "",
      sortOrder: this.product.groups.length,
      published: !1,
      customFields: T(this.product.variantGroupFields),
      variants: []
    };
    this.product.groups = [...this.product.groups, e], this.expandedGroupIds.add(t), this.drawer = { type: "group", groupId: t }, this.render();
  }
  addVariant(t) {
    var r;
    const e = this.getGroup(t);
    if (e == null)
      return;
    const a = this.nextDraftId--, s = {
      id: a,
      key: V,
      name: "New variant",
      title: "",
      titleValues: this.createEmptyTitleValues(),
      sku: "",
      images: "",
      priceValues: {},
      stockValues: this.createEmptyStockValues(),
      customFields: T(((r = this.product) == null ? void 0 : r.variantFields) ?? []),
      sortOrder: e.variants.length,
      published: !1
    };
    e.variants = [...e.variants, s], this.expandedGroupIds.add(t), this.drawer = { type: "variant", groupId: t, variantId: a }, this.render();
  }
  async save() {
    var e;
    if (this.syncMediaPickers(), !this.validateAllTitles() || !this.validateAllCustomFields())
      return;
    if (this.product == null || !this.hasChanges()) {
      this.showNotification("default", "Variants", "No changes to save.");
      return;
    }
    const t = this.product.groups.map((a) => this.getChangedGroupForSave(a)).filter((a) => a != null);
    this.saving = !0, this.error = "", this.render();
    try {
      for (const a of this.deletedNodeIds)
        await this.deleteJson(`/ekom/backoffice/Variants/${encodeURIComponent(String(a))}`);
      t.length > 0 ? this.product = await this.postJson("/ekom/backoffice/Variants/Save", {
        productId: this.getProductId(),
        publish: !0,
        groups: t
      }) : await this.load(), this.selectedGroupIds.clear(), this.selectedVariantIds.clear(), this.deletedNodeIds.clear(), this.drawer = void 0, this.expandedGroupIds.clear(), ((e = this.product) == null ? void 0 : e.groups.length) === 1 && this.expandedGroupIds.add(this.product.groups[0].id), this.resetSnapshots(), this.updateWorkspaceViewBadge(), this.showSuccess("Variant changes were saved.");
    } catch (a) {
      this.error = $(a, "Action failed."), this.showError(this.error);
    } finally {
      this.saving = !1, this.render();
    }
  }
  deleteSelected() {
    if (this.product == null)
      return;
    const t = this.selectedGroupIds.size + this.selectedVariantIds.size;
    if (!(t === 0 || !window.confirm(`Delete ${t} selected item${t === 1 ? "" : "s"}?`))) {
      for (const e of this.product.groups) {
        this.selectedGroupIds.has(e.id) && !p(e.id) && this.deletedNodeIds.add(e.id);
        for (const a of e.variants)
          this.selectedVariantIds.has(a.id) && !p(a.id) && this.deletedNodeIds.add(a.id);
      }
      this.product.groups = this.product.groups.filter((e) => !this.selectedGroupIds.has(e.id)).map((e) => ({
        ...e,
        variants: e.variants.filter((a) => !this.selectedVariantIds.has(a.id))
      })), this.selectedGroupIds.clear(), this.selectedVariantIds.clear(), this.drawer = void 0, this.render();
    }
  }
  deleteDrawerItem() {
    if (this.product == null || this.drawer == null)
      return;
    const t = this.drawer, e = t.type === "group", a = e ? "variant group" : "variant";
    if (window.confirm(`Delete this ${a}?`)) {
      if (e) {
        const s = this.getGroup(t.groupId);
        s != null && !p(s.id) && this.deletedNodeIds.add(s.id), this.product.groups = this.product.groups.filter((r) => r.id !== t.groupId);
      } else {
        const s = this.getGroup(t.groupId), r = this.getVariant(t.groupId, t.variantId);
        r != null && !p(r.id) && this.deletedNodeIds.add(r.id), s != null && (s.variants = s.variants.filter((n) => n.id !== t.variantId));
      }
      this.drawer = void 0, this.render();
    }
  }
  updateGroupTitle(t, e) {
    const a = this.getGroup(t);
    a != null && (a.titleValues = { ...a.titleValues, [this.activeLanguage]: e }, a.title = y(a.titleValues) || a.name, this.updateActiveLanguageTabMissingState(a.titleValues), this.updateSaveButtonState());
  }
  updateGroupImages(t, e) {
    const a = this.getGroup(t);
    a != null && (a.images = e, this.updateSaveButtonState());
  }
  updateVariantTitle(t, e, a) {
    const s = this.getVariant(t, e);
    s != null && (s.titleValues = { ...s.titleValues, [this.activeLanguage]: a }, s.title = y(s.titleValues) || s.name, this.updateActiveLanguageTabMissingState(s.titleValues), this.updateSaveButtonState());
  }
  updateVariant(t, e, a, s) {
    const r = this.getVariant(t, e);
    r != null && (r[a] = s, this.updateSaveButtonState());
  }
  updatePrice(t, e, a, s, r) {
    const n = this.getVariant(t, e);
    if (n == null)
      return;
    const d = [...n.priceValues[a] ?? []], c = Number(r) || 0, h = d.find((g) => v(g) === s);
    h != null ? (h.Price = c, h.price = c) : d.push({ Currency: s, Price: c }), n.priceValues = { ...n.priceValues, [a]: d }, this.updateSaveButtonState();
  }
  updateStock(t, e, a, s) {
    const r = this.getVariant(t, e);
    if (r == null)
      return;
    const n = Number(s) || 0;
    let d = !1;
    const c = r.stockValues.map((h) => h.storeAlias !== a ? h : (d = !0, { ...h, value: n }));
    d || c.push({ storeAlias: a, value: n }), r.stockValues = c, this.updateSaveButtonState();
  }
  hasChanges() {
    var t;
    return this.deletedNodeIds.size > 0 || (((t = this.product) == null ? void 0 : t.groups) ?? []).some((e) => this.isGroupChanged(e) || e.variants.some((a) => this.isVariantChanged(a)));
  }
  updateWorkspaceViewBadge() {
    var e;
    if (this.workspaceViewContext == null)
      return;
    this.workspaceViewContext.hints.has(m) && this.workspaceViewContext.hints.removeOne(m);
    const t = ((e = this.product) == null ? void 0 : e.variantCount) ?? 0;
    t > 0 && this.workspaceViewContext.hints.addOne({
      unique: m,
      text: String(t),
      color: "default"
    });
  }
  getChangedGroupForSave(t) {
    const e = t.variants.filter((a) => this.isVariantChanged(a)).map((a) => {
      var s;
      return {
        ...a,
        priceValues: Y(a.priceValues, ((s = this.product) == null ? void 0 : s.stores) ?? []),
        changed: !0
      };
    });
    return !this.isGroupChanged(t) && e.length === 0 ? null : {
      ...t,
      changed: this.isGroupChanged(t),
      variants: e
    };
  }
  isGroupChanged(t) {
    return p(t.id) || this.groupSnapshots.get(t.id) !== G(t);
  }
  isVariantChanged(t) {
    return p(t.id) || this.variantSnapshots.get(t.id) !== N(t);
  }
  getGroup(t) {
    var e;
    return (e = this.product) == null ? void 0 : e.groups.find((a) => a.id === t);
  }
  getVariant(t, e) {
    var a;
    return (a = this.getGroup(t)) == null ? void 0 : a.variants.find((s) => s.id === e);
  }
  resetSnapshots() {
    var t;
    this.groupSnapshots.clear(), this.variantSnapshots.clear();
    for (const e of ((t = this.product) == null ? void 0 : t.groups) ?? []) {
      this.groupSnapshots.set(e.id, G(e));
      for (const a of e.variants)
        this.variantSnapshots.set(a.id, N(a));
    }
  }
  createEmptyTitleValues() {
    var e;
    const t = {};
    for (const a of ((e = this.product) == null ? void 0 : e.languages) ?? [])
      t[a.isoCode ?? ""] = "";
    return t;
  }
  createEmptyStockValues() {
    var t;
    return (((t = this.product) == null ? void 0 : t.stores) ?? []).map((e) => ({ storeAlias: e.alias ?? "", value: 0 }));
  }
  showSuccess(t) {
    this.showNotification("positive", "Success", t);
  }
  showError(t) {
    this.showNotification("danger", "Error", t);
  }
  showNotification(t, e, a) {
    if (this.notificationContext) {
      this.notificationContext.peek(t, {
        data: {
          headline: e,
          message: a
        }
      });
      return;
    }
    t === "danger" && console.error(`${e}: ${a}`);
  }
  render() {
    this.innerHTML = `
      <style>${Q}</style>
      <section class="ekm-variant-editor">
        ${this.renderTopBar()}
        ${this.error ? `<p class="status status--error">${u(this.error)}</p>` : ""}
        ${this.loading ? "<uui-loader></uui-loader><p>Loading variants...</p>" : this.renderBody()}
        ${this.renderDrawer()}
      </section>
    `, this.bindEvents();
  }
  renderTopBar() {
    const t = this.selectedGroupIds.size + this.selectedVariantIds.size, e = this.hasChanges();
    return `
      <div class="top-bar">
        <div class="selection-actions">
          ${t > 0 ? `
            <span>${t} selected</span>
            <uui-button look="secondary" color="danger" data-action="delete-selected" ${this.saving ? "disabled" : ""}>Delete</uui-button>
          ` : ""}
        </div>
        <div class="main-actions">
          <uui-button look="primary" data-action="create-group" ${this.saving ? "disabled" : ""}>Add group</uui-button>
          <uui-button look="primary" color="positive" data-action="save" ${this.saving || !e ? "disabled" : ""}>Save variant changes</uui-button>
        </div>
      </div>
    `;
  }
  renderBody() {
    return this.product == null ? "" : this.product.groups.length === 0 ? `
        <uui-box headline="No variants yet">
          <p>Create a variant group to start adding product variants.</p>
          <uui-button look="primary" data-action="create-group">Create variant group</uui-button>
        </uui-box>
      ` : `
      <div class="group-list">
        ${this.product.groups.map((t) => this.renderGroup(t)).join("")}
      </div>
    `;
  }
  renderGroup(t) {
    const e = this.expandedGroupIds.has(t.id), a = this.getTitle(t, "New group"), s = _(t);
    return `
      <article class="group-card ${p(t.id) ? "is-draft" : ""}" draggable="true" data-drag-group-id="${t.id}">
        <div class="group-header">
          <span class="drag-handle" title="Drag to reorder" aria-hidden="true">⋮⋮</span>
          <input type="checkbox" data-select-group data-group-id="${t.id}" ${this.selectedGroupIds.has(t.id) ? "checked" : ""} aria-label="Select group">
          <button type="button" class="group-toggle" data-action="toggle-group" data-group-id="${t.id}" aria-label="Toggle group">${e ? "▼" : "►"}</button>
          <button type="button" class="thumb thumb--button" data-action="toggle-group" data-group-id="${t.id}">${E(s, a)}</button>
          <button type="button" class="group-title" data-action="toggle-group" data-group-id="${t.id}">${u(a)}</button>
          <span class="count">${t.variants.length} variants</span>
          ${p(t.id) ? '<span class="badge">draft</span>' : ""}
          <div class="group-header-actions">
            <uui-button compact look="secondary" data-action="edit-group" data-group-id="${t.id}">Edit group</uui-button>
            <uui-button compact look="secondary" data-action="create-variant" data-group-id="${t.id}">Add variant</uui-button>
          </div>
        </div>
        ${e ? this.renderVariants(t) : ""}
      </article>
    `;
  }
  renderVariants(t) {
    return `
      <div class="variant-table">
        <div class="variant-head">
          <span></span><span></span><span></span><span>Title</span><span>SKU</span><span class="variant-price-cell">Price</span><span class="variant-stock-cell">Stock</span><span></span>
        </div>
        ${t.variants.map((e) => this.renderVariant(t, e)).join("")}
      </div>
    `;
  }
  renderVariant(t, e) {
    return `
      <div class="variant-row ${p(e.id) ? "is-draft" : ""}" draggable="true" data-drag-group-id="${t.id}" data-drag-variant-id="${e.id}">
        <span class="drag-handle" title="Drag to reorder" aria-hidden="true">⋮⋮</span>
        <input type="checkbox" data-select-variant data-variant-id="${e.id}" ${this.selectedVariantIds.has(e.id) ? "checked" : ""} aria-label="Select variant">
        <span class="thumb">${E(k(e.images), this.getTitle(e, "New variant"))}</span>
        <strong>${u(this.getTitle(e, "New variant"))}${p(e.id) ? ' <span class="badge">draft</span>' : ""}</strong>
        <span>${u(e.sku)}</span>
        <span class="variant-price-cell">${u(this.getDefaultPrice(e))}</span>
        <span class="variant-stock-cell">${u(this.getTotalStock(e))}</span>
        <uui-button compact look="secondary" data-action="edit-variant" data-group-id="${t.id}" data-variant-id="${e.id}">Edit</uui-button>
      </div>
    `;
  }
  renderDrawer() {
    if (this.drawer == null || this.product == null)
      return "";
    const t = this.getGroup(this.drawer.groupId);
    if (t == null)
      return "";
    if (this.drawer.type === "group")
      return this.renderGroupDrawer(t);
    const e = this.getVariant(this.drawer.groupId, this.drawer.variantId);
    return e == null ? "" : this.renderVariantDrawer(t, e);
  }
  renderGroupDrawer(t) {
    var e;
    return `
      <div class="drawer-backdrop" data-action="close-drawer"></div>
      <aside class="drawer">
        ${this.renderDrawerHeader(this.getTitle(t, "New group"), "variant group", t)}
        <div class="drawer-body">
          ${this.renderTitleField("Group title", ((e = t.titleValues) == null ? void 0 : e[this.activeLanguage]) ?? "", "data-group-title", t.id, 0, t.titleValues)}
          ${this.renderCustomFields(t.customFields, t.id)}
          ${this.renderMediaPicker("Images", t.images, "data-group-images", t.id)}
          <p class="hint">Group images apply to all variants unless a variant has its own.</p>
        </div>
        ${this.renderDrawerFooter()}
      </aside>
    `;
  }
  renderVariantDrawer(t, e) {
    var a;
    return `
      <div class="drawer-backdrop" data-action="close-drawer"></div>
      <aside class="drawer">
        ${this.renderDrawerHeader(this.getTitle(e, "New variant"), `${this.getTitle(t, "Group")} / variant`, e)}
        <div class="drawer-body">
          ${this.renderTitleField("Title", ((a = e.titleValues) == null ? void 0 : a[this.activeLanguage]) ?? "", "data-variant-title", t.id, e.id, e.titleValues)}
          <label>SKU<input data-variant-field="sku" data-group-id="${t.id}" data-variant-id="${e.id}" value="${u(e.sku)}"></label>
          ${this.renderCustomFields(e.customFields, t.id, e.id)}
          ${this.renderPriceTable(t.id, e)}
          ${this.renderStockTable(t.id, e)}
          ${this.renderMediaPicker("Images", e.images, "data-variant-images", t.id, e.id)}
        </div>
        ${this.renderDrawerFooter()}
      </aside>
    `;
  }
  renderDrawerHeader(t, e, a) {
    const s = !p(a.id) && a.key ? `/umbraco/section/content/workspace/document/edit/${a.key}` : "";
    return `
      <header class="drawer-header">
        <div class="drawer-title">
          <h2>${u(t)}</h2>
          <p>${u(e)}</p>
        </div>
        <div class="drawer-header-actions">
          ${s ? `<uui-button compact look="secondary" href="${s}" target="_blank" rel="noopener" title="Open node in new tab" aria-label="Open node in new tab"><span class="edit-icon" aria-hidden="true">✎</span></uui-button>` : ""}
          <button type="button" class="close-button" data-action="close-drawer" aria-label="Close">×</button>
        </div>
      </header>
    `;
  }
  renderDrawerFooter() {
    return `
      <footer class="drawer-footer">
        <div class="drawer-footer-left">
          <button type="button" class="danger-button" data-action="delete-drawer-item">Delete</button>
        </div>
        <div class="drawer-footer-right">
          <uui-button look="secondary" data-action="close-drawer">Close</uui-button>
          <uui-button look="primary" color="positive" data-action="save-drawer">Save</uui-button>
        </div>
      </footer>
    `;
  }
  renderTitleField(t, e, a, s, r = 0, n = {}) {
    return `
      <label>${u(t)} *
        ${this.renderLanguageMiniTabs(n)}
        <input ${a} data-group-id="${s}" data-variant-id="${r}" value="${u(e)}" required>
      </label>
    `;
  }
  renderLanguageMiniTabs(t) {
    var a;
    const e = ((a = this.product) == null ? void 0 : a.languages) ?? [];
    return e.length <= 1 ? "" : `
      <div class="mini-tabs">
        ${e.map((s) => {
      var d, c;
      const r = s.isoCode ?? "", n = ["mini-tab"];
      return r === this.activeLanguage && n.push("active"), (d = t[r]) != null && d.trim() || n.push("is-missing"), `<button type="button" data-action="set-language" data-tab-value="${u(r)}" class="${n.join(" ")}" title="${(c = t[r]) != null && c.trim() ? "" : "Missing title value"}">${u(U(s))}</button>`;
    }).join("")}
      </div>
    `;
  }
  renderCustomFields(t, e, a = 0) {
    return t != null && t.length ? t.map((s) => `
      <label>${u(s.label)}${s.required ? " *" : ""}
        <input data-custom-field data-custom-field-alias="${u(s.alias)}" data-group-id="${e}" data-variant-id="${a}" value="${u(s.value ?? "")}" ${s.required ? "required" : ""}>
      </label>
    `).join("") : "";
  }
  renderPriceTable(t, e) {
    var a;
    return `
      <section>
        <h3>Prices</h3>
        <table>
          <thead><tr><th>Store</th><th>Currency</th><th>Price</th></tr></thead>
          <tbody>
            ${(((a = this.product) == null ? void 0 : a.stores) ?? []).flatMap((s) => (s.currencies ?? []).map((r) => {
      var h, g;
      const n = s.alias ?? "", d = r.currencyValue ?? "", c = S((g = (h = e.priceValues) == null ? void 0 : h[n]) == null ? void 0 : g.find((L) => v(L) === d));
      return `<tr><td>${u(s.title ?? n)}</td><td>${u(r.isoCurrencySymbol ?? d)}</td><td><input class="numeric" type="number" min="0" step="any" data-price data-price-store="${u(n)}" data-price-currency="${u(d)}" data-group-id="${t}" data-variant-id="${e.id}" value="${u(c)}"></td></tr>`;
    })).join("")}
          </tbody>
        </table>
      </section>
    `;
  }
  renderStockTable(t, e) {
    var s, r;
    const a = e.stockValues.length > 0 ? e.stockValues : this.createEmptyStockValues();
    if (a.length <= 1) {
      const n = ((s = a[0]) == null ? void 0 : s.value) ?? 0, d = ((r = a[0]) == null ? void 0 : r.storeAlias) ?? "";
      return `<label>Stock<input class="numeric" type="number" min="0" step="any" data-stock data-stock-store="${u(d)}" data-group-id="${t}" data-variant-id="${e.id}" value="${u(n)}"></label>`;
    }
    return `
      <section>
        <h3>Stock</h3>
        <table>
          <thead><tr><th>Store</th><th>Stock</th></tr></thead>
          <tbody>
            ${a.map((n) => `<tr><td>${u(this.getStoreTitle(n.storeAlias))}</td><td><input class="numeric" type="number" min="0" step="any" data-stock data-stock-store="${u(n.storeAlias)}" data-group-id="${t}" data-variant-id="${e.id}" value="${u(n.value)}"></td></tr>`).join("")}
          </tbody>
        </table>
      </section>
    `;
  }
  renderMediaPicker(t, e, a, s, r = 0) {
    return `
      <div class="media-field">
        <span class="field-label">${u(t)}</span>
        <umb-input-media ${a} max="100" value="${u(e ?? "")}" data-group-id="${s}" data-variant-id="${r}" data-value="${u(e ?? "")}"></umb-input-media>
      </div>
    `;
  }
  bindEvents() {
    var t, e, a, s;
    this.querySelectorAll('[data-action="create-group"]').forEach((r) => {
      r.addEventListener("click", () => this.addGroup());
    }), (t = this.querySelector('[data-action="save"]')) == null || t.addEventListener("click", () => void this.save()), (e = this.querySelector('[data-action="delete-selected"]')) == null || e.addEventListener("click", () => this.deleteSelected()), (a = this.querySelector('[data-action="delete-drawer-item"]')) == null || a.addEventListener("click", () => this.deleteDrawerItem()), (s = this.querySelector('[data-action="save-drawer"]')) == null || s.addEventListener("click", () => {
      this.syncMediaPickers(), !(!this.validateDrawerTitle() || !this.validateDrawerCustomFields()) && (this.drawer = void 0, this.render());
    }), this.querySelectorAll('[data-action="close-drawer"]').forEach((r) => {
      r.addEventListener("click", () => {
        this.syncMediaPickers(), this.drawer = void 0, this.render();
      });
    }), this.querySelectorAll('[data-action="set-language"]').forEach((r) => {
      r.addEventListener("click", (n) => {
        n.preventDefault(), n.stopPropagation(), this.setActiveLanguage(r.dataset.tabValue ?? "");
      });
    }), this.querySelectorAll('[data-action="toggle-group"]').forEach((r) => {
      r.addEventListener("click", () => this.toggleGroup(Number(r.dataset.groupId)));
    }), this.querySelectorAll('[data-action="edit-group"]').forEach((r) => {
      r.addEventListener("click", () => {
        this.drawer = { type: "group", groupId: Number(r.dataset.groupId) }, this.render();
      });
    }), this.querySelectorAll('[data-action="create-variant"]').forEach((r) => {
      r.addEventListener("click", () => this.addVariant(Number(r.dataset.groupId)));
    }), this.querySelectorAll('[data-action="edit-variant"]').forEach((r) => {
      r.addEventListener("click", () => {
        const n = r;
        this.drawer = { type: "variant", groupId: Number(n.dataset.groupId), variantId: Number(n.dataset.variantId) }, this.render();
      });
    }), this.querySelectorAll("[data-select-group]").forEach((r) => {
      r.addEventListener("change", () => {
        this.toggleSet(this.selectedGroupIds, Number(r.dataset.groupId), r.checked), this.render();
      });
    }), this.querySelectorAll("[data-select-variant]").forEach((r) => {
      r.addEventListener("change", () => {
        this.toggleSet(this.selectedVariantIds, Number(r.dataset.variantId), r.checked), this.render();
      });
    }), this.querySelectorAll("[data-group-title]").forEach((r) => {
      r.addEventListener("input", (n) => {
        n.stopPropagation(), this.updateGroupTitle(Number(r.dataset.groupId), r.value);
      });
    }), this.querySelectorAll("[data-variant-title]").forEach((r) => {
      r.addEventListener("input", (n) => {
        n.stopPropagation(), this.updateVariantTitle(Number(r.dataset.groupId), Number(r.dataset.variantId), r.value);
      });
    }), this.querySelectorAll("[data-variant-field]").forEach((r) => {
      r.addEventListener("input", (n) => {
        n.stopPropagation(), this.updateVariant(Number(r.dataset.groupId), Number(r.dataset.variantId), "sku", r.value);
      });
    }), this.querySelectorAll("[data-custom-field]").forEach((r) => {
      r.addEventListener("input", (n) => {
        n.stopPropagation(), this.updateCustomField(Number(r.dataset.groupId), Number(r.dataset.variantId), r.dataset.customFieldAlias ?? "", r.value);
      });
    }), this.querySelectorAll("[data-price]").forEach((r) => {
      r.addEventListener("input", (n) => {
        n.stopPropagation(), this.updatePrice(Number(r.dataset.groupId), Number(r.dataset.variantId), r.dataset.priceStore ?? "", r.dataset.priceCurrency ?? "", r.value);
      });
    }), this.querySelectorAll("[data-stock]").forEach((r) => {
      r.addEventListener("input", (n) => {
        n.stopPropagation(), this.updateStock(Number(r.dataset.groupId), Number(r.dataset.variantId), r.dataset.stockStore ?? "", r.value);
      });
    }), this.bindDragAndDrop(), this.bindMediaPickers();
  }
  bindDragAndDrop() {
    this.querySelectorAll(".group-card[data-drag-group-id]").forEach((t) => {
      t.addEventListener("dragstart", (e) => {
        var a, s;
        this.draggedGroupId = Number(t.dataset.dragGroupId), (a = e.dataTransfer) == null || a.setData("text/plain", `group:${this.draggedGroupId}`), (s = e.dataTransfer) == null || s.setDragImage(t, 20, 20);
      }), t.addEventListener("dragover", (e) => {
        this.draggedGroupId != null && e.preventDefault();
      }), t.addEventListener("drop", (e) => {
        e.preventDefault(), this.reorderGroup(this.draggedGroupId, Number(t.dataset.dragGroupId)), this.draggedGroupId = void 0;
      }), t.addEventListener("dragend", () => {
        this.draggedGroupId = void 0;
      });
    }), this.querySelectorAll(".variant-row[data-drag-variant-id]").forEach((t) => {
      t.addEventListener("dragstart", (e) => {
        var a;
        this.draggedVariant = {
          groupId: Number(t.dataset.dragGroupId),
          variantId: Number(t.dataset.dragVariantId)
        }, e.stopPropagation(), (a = e.dataTransfer) == null || a.setData("text/plain", `variant:${this.draggedVariant.groupId}:${this.draggedVariant.variantId}`);
      }), t.addEventListener("dragover", (e) => {
        var a;
        ((a = this.draggedVariant) == null ? void 0 : a.groupId) === Number(t.dataset.dragGroupId) && (e.preventDefault(), e.stopPropagation());
      }), t.addEventListener("drop", (e) => {
        e.preventDefault(), e.stopPropagation(), this.reorderVariant(this.draggedVariant, Number(t.dataset.dragGroupId), Number(t.dataset.dragVariantId)), this.draggedVariant = void 0;
      }), t.addEventListener("dragend", (e) => {
        e.stopPropagation(), this.draggedVariant = void 0;
      });
    });
  }
  bindMediaPickers() {
    this.querySelectorAll("[data-group-images], [data-variant-images]").forEach((t) => {
      const e = t, a = t.dataset.value ?? "", s = x(a);
      e.value = s.join(","), e.selection = s, H(e), t.addEventListener("change", async (r) => {
        const n = Number(t.dataset.groupId), d = Number(t.dataset.variantId);
        r.stopPropagation(), await Promise.resolve();
        const c = A(e);
        await f(e), d !== 0 ? this.updateVariant(n, d, "images", c) : this.updateGroupImages(n, c);
      });
    });
  }
  syncMediaPickers() {
    this.querySelectorAll("[data-group-images], [data-variant-images]").forEach((t) => {
      const e = t, a = Number(t.dataset.groupId), s = Number(t.dataset.variantId), r = A(e);
      if (s !== 0) {
        const d = this.getVariant(a, s);
        d != null && d.images !== r && (d.images = r);
        return;
      }
      const n = this.getGroup(a);
      n != null && n.images !== r && (n.images = r);
    }), this.updateSaveButtonState();
  }
  updateSaveButtonState() {
    const t = this.querySelector('[data-action="save"]');
    t != null && t.toggleAttribute("disabled", this.saving || !this.hasChanges());
  }
  updateActiveLanguageTabMissingState(t) {
    var a;
    const e = Array.from(this.querySelectorAll('[data-action="set-language"]')).find((s) => s.dataset.tabValue === this.activeLanguage);
    e == null || e.classList.toggle("is-missing", !((a = t[this.activeLanguage]) != null && a.trim()));
  }
  updateCustomField(t, e, a, s) {
    var d;
    const r = e !== 0 ? this.getVariant(t, e) : this.getGroup(t), n = (d = r == null ? void 0 : r.customFields) == null ? void 0 : d.find((c) => c.alias === a);
    n != null && (n.value = s), this.updateSaveButtonState();
  }
  validateDrawerCustomFields() {
    if (this.drawer == null)
      return !0;
    const t = this.drawer.type === "group" ? this.getGroup(this.drawer.groupId) : this.getVariant(this.drawer.groupId, this.drawer.variantId);
    return this.validateCustomFields(t == null ? void 0 : t.customFields);
  }
  validateDrawerTitle() {
    if (this.drawer == null)
      return !0;
    const t = this.drawer.type === "group" ? this.getGroup(this.drawer.groupId) : this.getVariant(this.drawer.groupId, this.drawer.variantId);
    return this.validateTitle(t);
  }
  validateAllTitles() {
    var t;
    for (const e of ((t = this.product) == null ? void 0 : t.groups) ?? []) {
      if (!this.validateTitle(e))
        return !1;
      for (const a of e.variants)
        if (!this.validateTitle(a))
          return !1;
    }
    return !0;
  }
  validateTitle(t) {
    return X(t == null ? void 0 : t.titleValues) ? !0 : (this.showError("Title is required."), !1);
  }
  validateAllCustomFields() {
    var t;
    for (const e of ((t = this.product) == null ? void 0 : t.groups) ?? []) {
      if (!this.validateCustomFields(e.customFields, !1))
        return !1;
      for (const a of e.variants)
        if (!this.validateCustomFields(a.customFields, !1))
          return !1;
    }
    return !0;
  }
  validateCustomFields(t, e = !0) {
    const a = t == null ? void 0 : t.find((s) => {
      var r;
      return s.required && !((r = s.value) != null && r.trim());
    });
    return a == null ? !0 : (this.showError(`${a.label} is required.`), e && this.render(), !1);
  }
  reorderGroup(t, e) {
    this.product == null || t == null || t === e || (this.product.groups = q(this.product.groups, t, e), this.product.groups.forEach((a, s) => {
      a.sortOrder = s;
    }), this.render());
  }
  reorderVariant(t, e, a) {
    if (t == null || t.groupId !== e || t.variantId === a)
      return;
    const s = this.getGroup(t.groupId);
    s != null && (s.variants = q(s.variants, t.variantId, a), s.variants.forEach((r, n) => {
      r.sortOrder = n;
    }), this.render());
  }
  setActiveLanguage(t) {
    var s, r;
    if (this.activeLanguage = t, this.querySelectorAll('[data-action="set-language"]').forEach((n) => {
      n.classList.toggle("active", n.dataset.tabValue === t);
    }), this.drawer == null)
      return;
    if (this.drawer.type === "group") {
      const n = this.getGroup(this.drawer.groupId), d = this.querySelector("[data-group-title]");
      n != null && d != null && (d.value = ((s = n.titleValues) == null ? void 0 : s[t]) ?? "");
      return;
    }
    const e = this.getVariant(this.drawer.groupId, this.drawer.variantId), a = this.querySelector("[data-variant-title]");
    e != null && a != null && (a.value = ((r = e.titleValues) == null ? void 0 : r[t]) ?? "");
  }
  toggleGroup(t) {
    this.expandedGroupIds.has(t) ? this.expandedGroupIds.delete(t) : this.expandedGroupIds.add(t), this.render();
  }
  toggleSet(t, e, a) {
    a ? t.add(e) : t.delete(e);
  }
  getTitle(t, e) {
    var a;
    return ((a = t.titleValues) == null ? void 0 : a[this.activeLanguage]) || y(t.titleValues) || t.title || t.name || e;
  }
  getDefaultPrice(t) {
    var n, d, c, h;
    const e = (n = this.product) == null ? void 0 : n.stores[0], a = (d = e == null ? void 0 : e.currencies) == null ? void 0 : d[0];
    if (e == null || a == null)
      return "—";
    const s = S((h = (c = t.priceValues) == null ? void 0 : c[e.alias ?? ""]) == null ? void 0 : h.find((g) => v(g) === (a.currencyValue ?? ""))), r = a.currencySymbol ?? a.isoCurrencySymbol ?? a.currencyValue ?? "";
    return `${C(s)} ${r}`.trim();
  }
  getTotalStock(t) {
    return t.stockValues.length === 0 ? "0" : C(t.stockValues.reduce((e, a) => e + (Number(a.value) || 0), 0));
  }
  getStoreTitle(t) {
    var e, a;
    return ((a = (e = this.product) == null ? void 0 : e.stores.find((s) => s.alias === t)) == null ? void 0 : a.title) ?? t;
  }
  getProductId() {
    return this.productId || this.getProductIdFromUrl();
  }
  getProductIdFromUrl() {
    const t = window.location.href, e = t.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);
    if (e != null)
      return e[0];
    const a = t.match(/\/edit\/([^/?#]+)/i);
    return (a == null ? void 0 : a[1]) ?? "";
  }
  async fetchJson(t) {
    const e = await fetch(t, { credentials: "same-origin", headers: { Accept: "application/json" } });
    return w(e);
  }
  async postJson(t, e) {
    const a = await fetch(t, {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json", "Content-Type": "application/json" },
      body: JSON.stringify(e)
    });
    return w(a);
  }
  async deleteJson(t) {
    const e = await fetch(t, { method: "DELETE", credentials: "same-origin", headers: { Accept: "application/json" } });
    await w(e);
  }
}
async function w(i) {
  if (!i.ok)
    throw new Error(await i.text() || "Request failed.");
  if (i.status !== 204)
    return i.json();
}
function $(i, o) {
  return i instanceof Error && i.message.length > 0 ? i.message : o;
}
function u(i) {
  return String(i ?? "").replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;").replace(/'/g, "&#39;");
}
function y(i) {
  return Object.values(i ?? {}).find((o) => o != null && o.trim().length > 0) ?? "";
}
function U(i) {
  return (i.isoCode ?? i.cultureName ?? "").split("-")[0].toUpperCase();
}
function v(i) {
  return i.Currency ?? i.currency ?? "";
}
function S(i) {
  return (i == null ? void 0 : i.Price) ?? (i == null ? void 0 : i.price) ?? 0;
}
function C(i) {
  return new Intl.NumberFormat().format(i);
}
function x(i) {
  const o = String(i ?? "").trim();
  if (o.length === 0)
    return [];
  if (o.startsWith("["))
    try {
      return JSON.parse(o).map((e) => String(e.mediaKey ?? e.key ?? "").trim()).filter(Boolean);
    } catch {
      return [];
    }
  return o.split(",").map((t) => I(t.trim())).filter(Boolean);
}
function k(i) {
  return x(i)[0] ?? "";
}
function _(i) {
  const o = k(i.images);
  if (o)
    return o;
  for (const t of i.variants) {
    const e = k(t.images);
    if (e)
      return e;
  }
  return "";
}
function E(i, o) {
  return i ? `<umb-imaging-thumbnail unique="${u(i)}" width="38" height="38" alt="${u(o)}"></umb-imaging-thumbnail>` : "-";
}
function I(i) {
  const o = i.match(/umb:\/\/media\/(.+)$/i);
  return (o == null ? void 0 : o[1]) ?? i;
}
function A(i) {
  return Array.isArray(i.selection) ? K(i.selection).join(",") : x(i.value ?? "").join(",");
}
const P = /* @__PURE__ */ new WeakSet();
async function H(i) {
  if (P.has(i)) {
    await f(i);
    return;
  }
  P.add(i), await f(i);
  const o = i.shadowRoot;
  if (o == null)
    return;
  const t = () => void J(i), e = () => window.setTimeout(() => void f(i));
  new MutationObserver(() => void f(i)).observe(o, { childList: !0, subtree: !0 }), o.addEventListener("pointerdown", t, { capture: !0 }), o.addEventListener("dragstart", t, { capture: !0 }), o.addEventListener("dragend", e, { capture: !0 }), o.addEventListener("drop", e, { capture: !0 });
}
async function J(i) {
  var o;
  await i.updateComplete, await new Promise((t) => window.requestAnimationFrame(t)), (o = i.shadowRoot) == null || o.querySelectorAll("uui-card-media[data-mark]").forEach((t) => {
    var a;
    const e = ((a = t.dataset.mark) == null ? void 0 : a.split(":").pop()) ?? "";
    e && t.setAttribute("detail", e);
  });
}
async function f(i) {
  var o;
  await i.updateComplete, await new Promise((t) => window.requestAnimationFrame(t)), (o = i.shadowRoot) == null || o.querySelectorAll("uui-card-media[detail]").forEach((t) => {
    t.removeAttribute("detail");
  });
}
function K(i) {
  return i.map((o) => {
    if (typeof o == "string")
      return I(o);
    if (o != null && typeof o == "object") {
      const t = o;
      return I(String(t.udi ?? t.key ?? t.unique ?? t.id ?? ""));
    }
    return "";
  }).filter(Boolean);
}
function p(i) {
  return i <= 0;
}
function G(i) {
  return b({
    titleValues: i.titleValues,
    images: i.images,
    customFields: i.customFields,
    sortOrder: i.sortOrder
  });
}
function N(i) {
  return b({
    titleValues: i.titleValues,
    sku: i.sku,
    images: i.images,
    priceValues: i.priceValues,
    stockValues: i.stockValues,
    customFields: i.customFields,
    sortOrder: i.sortOrder
  });
}
function T(i) {
  return (i ?? []).map((o) => ({
    ...o,
    value: ""
  }));
}
function X(i) {
  return Object.values(i ?? {}).some((o) => o.trim().length > 0);
}
function Y(i, o) {
  const t = {};
  for (const e of o) {
    const a = e.alias ?? "", s = /* @__PURE__ */ new Map();
    for (const [r, n] of Object.entries(i ?? {}))
      if (r.toLowerCase() === a.toLowerCase())
        for (const d of n) {
          const c = v(d);
          c !== "" && s.set(c.toLowerCase(), {
            Currency: c,
            Price: S(d)
          });
        }
    s.size > 0 && (t[a] = [...s.values()]);
  }
  return t;
}
function q(i, o, t) {
  const e = [...i], a = e.findIndex((n) => n.id === o), s = e.findIndex((n) => n.id === t);
  if (a < 0 || s < 0)
    return i;
  const [r] = e.splice(a, 1);
  return e.splice(s, 0, r), e;
}
function b(i) {
  if (Array.isArray(i))
    return `[${i.map(b).join(",")}]`;
  if (i != null && typeof i == "object") {
    const o = i;
    return `{${Object.keys(o).sort().map((t) => `${JSON.stringify(t)}:${b(o[t])}`).join(",")}}`;
  }
  return JSON.stringify(i);
}
const Q = `
  :host { display: block; padding: var(--uui-size-layout-1, 24px); background: #f4f3f5; color: #1b264f; font-family: Lato, Arial, sans-serif; }
  .ekm-variant-editor { display: grid; gap: 16px; }
  .top-bar, .selection-actions, .main-actions, .group-header, .group-header-actions { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
  .top-bar { background: #f4f3f5; border-bottom: 1px solid #e2e1e6; box-shadow: 0 4px 10px rgba(27, 38, 79, .06); justify-content: space-between; margin: -24px -24px 0; padding: 16px 24px; position: sticky; top: 0; z-index: 10; }
  .selection-actions { min-height: 32px; }
  .main-actions { margin-left: auto; justify-content: flex-end; }
  .group-list { display: grid; gap: 14px; }
  .group-card { background: #fff; border: 1px solid #e2e1e6; border-radius: 6px; box-shadow: 0 1px 3px rgba(27, 38, 79, .06); overflow: hidden; }
  .group-card.is-draft, .variant-row.is-draft { background: #f8fbf9; }
  .group-header { padding: 12px 14px; }
  .drag-handle { color: #8b8994; cursor: grab; font-weight: 900; letter-spacing: -2px; user-select: none; }
  [draggable="true"]:active .drag-handle { cursor: grabbing; }
  .group-toggle, .group-title, .thumb--button, .close-button, .mini-tab { border: 0; background: transparent; color: inherit; cursor: pointer; font: inherit; }
  .group-title { font-weight: 900; text-align: left; }
  .thumb { display: inline-flex; width: 38px; height: 38px; align-items: center; justify-content: center; border: 1px solid #e2e1e6; border-radius: 4px; background: #f4f3f5; color: #686570; font-size: 11px; text-transform: uppercase; }
  .thumb umb-imaging-thumbnail { width: 100%; height: 100%; }
  .count, .hint { color: #686570; }
  .badge { border-radius: 999px; padding: 2px 8px; background: #ecf7f1; color: #188a4f; font-size: 11px; font-weight: 700; text-transform: uppercase; }
  .group-header-actions { margin-left: auto; }
  .variant-table { border-top: 1px solid #e2e1e6; }
  .variant-head, .variant-row { display: grid; grid-template-columns: 18px 24px 44px minmax(140px, 1.3fr) minmax(100px, .8fr) minmax(100px, .8fr) minmax(80px, .6fr) auto; gap: 12px; align-items: center; padding: 10px 14px; }
  .variant-head { background: #f8f7fa; color: #8b8994; font-size: 11px; font-weight: 900; letter-spacing: .04em; text-transform: uppercase; }
  .variant-row { border-top: 1px solid #e2e1e6; }
  .variant-price-cell, .variant-stock-cell { text-align: center; }
  input { box-sizing: border-box; width: 100%; border: 1px solid #c4c2cb; border-radius: 3px; padding: 8px; font: inherit; }
  input:focus { border-color: #1b264f; outline: none; }
  input[type="checkbox"] { width: 16px; height: 16px; accent-color: #188a4f; }
  .status { padding: 12px; background: #fff; border-radius: 3px; }
  .status--error { color: #d42054; }
  .drawer-backdrop { position: fixed; inset: 0; background: rgba(27, 38, 79, .35); z-index: 1000; }
  .drawer { position: fixed; top: 0; right: 0; bottom: 0; z-index: 1001; width: min(460px, 100vw); display: grid; grid-template-rows: auto 1fr auto; background: #fff; box-shadow: -6px 0 24px rgba(27, 38, 79, .18); animation: slide-in .18s ease-out; }
  .drawer-header, .drawer-footer { display: flex; gap: 12px; align-items: center; justify-content: space-between; padding: 18px; border-bottom: 1px solid #e2e1e6; }
  .drawer-footer { border-top: 1px solid #e2e1e6; border-bottom: 0; }
  .drawer-title { min-width: 0; }
  .drawer-header-actions { display: flex; gap: 8px; align-items: center; flex-shrink: 0; }
  .drawer-footer-left, .drawer-footer-right { display: flex; gap: 12px; align-items: center; }
  .drawer-footer-left { margin-right: auto; }
  .danger-button { background: #d42054; border: 1px solid #d42054; border-radius: 3px; color: #fff; cursor: pointer; font: inherit; font-weight: 700; padding: 8px 14px; }
  .danger-button:hover { background: #b51b46; border-color: #b51b46; }
  .drawer-header h2 { margin: 0; font-size: 20px; }
  .drawer-header p { margin: 4px 0 0; color: #686570; }
  .edit-icon { display: inline-block; font-size: 15px; line-height: 1; transform: translateY(-1px); }
  .drawer-body { display: grid; gap: 18px; align-content: start; overflow: auto; padding: 18px; }
  label, .media-field { display: grid; gap: 8px; font-weight: 700; }
  .field-label, h3 { font-weight: 900; margin: 0; }
  .drawer-body section h3 { font-size: 13px; margin-bottom: 10px; }
  .mini-tabs { display: inline-flex; gap: 4px; }
  .mini-tab { padding: 4px 8px; border-bottom: 2px solid transparent; color: #686570; font-weight: 700; }
  .mini-tab.active { border-bottom-color: #1b264f; color: #1b264f; }
  .mini-tab.is-missing { color: #d42054; }
  .mini-tab.is-missing::after { content: ''; display: inline-block; width: 6px; height: 6px; margin-left: 5px; border-radius: 999px; background: #d42054; vertical-align: middle; }
  table { width: 100%; border-collapse: collapse; }
  th { background: #f8f7fa; color: #8b8994; font-size: 11px; letter-spacing: .04em; text-align: left; text-transform: uppercase; }
  th, td { border-bottom: 1px solid #e2e1e6; padding: 8px; }
  .numeric { text-align: right; }
  @keyframes slide-in { from { transform: translateX(100%); } to { transform: translateX(0); } }
`;
customElements.define("ekom-variants-workspace-view", z);
export {
  z as default
};
