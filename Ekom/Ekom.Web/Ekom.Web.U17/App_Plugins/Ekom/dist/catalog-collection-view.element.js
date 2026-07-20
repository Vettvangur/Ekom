var $ = Object.defineProperty;
var P = (s, c, e) => c in s ? $(s, c, { enumerable: !0, configurable: !0, writable: !0, value: e }) : s[c] = e;
var l = (s, c, e) => P(s, typeof c != "symbol" ? c + "" : c, e);
import { UMB_COLLECTION_CONTEXT as C } from "@umbraco-cms/backoffice/collection";
import { UmbElementMixin as E } from "@umbraco-cms/backoffice/element-api";
import "@umbraco-cms/backoffice/imaging";
const T = 16, m = "sortOrderAsc", f = "ekomCatalogNode", y = 14, I = 220, z = 4, q = "ekomCatalogParent:", w = "ekomCatalogSort", b = "ekomCatalog";
class A extends E(HTMLElement) {
  constructor() {
    super(...arguments);
    l(this, "nodeId", "");
    l(this, "query", "");
    l(this, "sort", k());
    l(this, "page", 1);
    l(this, "pageSize", T);
    l(this, "loading", !0);
    l(this, "error", "");
    l(this, "data");
    l(this, "revealTimer");
    l(this, "resizeTimer");
    l(this, "collectionPaginationObserver");
    l(this, "selection");
    l(this, "onPopState", () => this.restoreNodeFromHistory());
    l(this, "onResize", () => {
      this.resizeTimer != null && window.clearTimeout(this.resizeTimer), this.resizeTimer = window.setTimeout(() => {
        this.updatePageSizeForViewport() && this.nodeId && this.data != null && !this.loading && this.load(!1);
      }, 150);
    });
  }
  connectedCallback() {
    super.connectedCallback(), this.style.setProperty("display", "block", "important"), this.style.setProperty("visibility", "visible", "important"), window.addEventListener("popstate", this.onPopState), window.addEventListener("resize", this.onResize), this.revealCollectionHost(), this.revealTimer = window.setTimeout(() => this.revealCollectionHost(), 250), this.nodeId = this.getNodeIdFromUrl(), this.sort = k(), this.restorePageFromHistory(), this.consumeContext(C, (e) => {
      var o;
      const t = (o = e == null ? void 0 : e.getConfig()) == null ? void 0 : o.unique;
      this.getCatalogNodeFromUrl() == null && t != null && t !== this.nodeId && (this.nodeId = t, this.restorePageFromHistory(), this.load()), (e == null ? void 0 : e.selection) != null && (this.selection = e.selection, this.selection.setSelectable(!0), this.selection.setMultiple(!0), this.observe(e.selection.selection, () => this.render(), "ekomCatalogCollectionSelection")), this.observe(e == null ? void 0 : e.filter, (r) => this.setQueryFromCollectionFilter(r), "ekomCatalogCollectionFilter");
    }), this.render(), this.nodeId && this.load();
  }
  disconnectedCallback() {
    var e;
    window.removeEventListener("popstate", this.onPopState), window.removeEventListener("resize", this.onResize), this.revealTimer != null && window.clearTimeout(this.revealTimer), this.resizeTimer != null && window.clearTimeout(this.resizeTimer), (e = this.collectionPaginationObserver) == null || e.disconnect(), this.collectionPaginationObserver = void 0, super.disconnectedCallback();
  }
  async load(e = !0) {
    if (!this.nodeId) {
      this.loading = !1, this.error = "Unable to determine the current catalog node.", this.render();
      return;
    }
    this.loading = e, this.error = "", this.updatePageSizeForViewport(), e && this.render();
    try {
      const t = new URLSearchParams({
        query: this.query,
        sort: this.sort,
        page: String(this.page),
        pageSize: String(this.pageSize)
      });
      this.data = await this.fetchJson(`/ekom/backoffice/CatalogCollection/${encodeURIComponent(this.nodeId)}?${t}`), this.page = this.data.page, this.pageSize = this.data.pageSize, this.storeParentNode(this.data);
    } catch (t) {
      if (t instanceof x && t.status === 404 && this.redirectToParent())
        return;
      this.error = t instanceof Error ? t.message : "Unable to load catalog.";
    } finally {
      this.loading = !1, this.render();
    }
  }
  drillTo(e) {
    var r;
    const t = e.key || String(e.id), o = history.state != null && typeof history.state == "object" ? history.state : {};
    history.pushState({
      ...o,
      [b]: {
        nodeId: t,
        page: 1
      }
    }, "", this.getDocumentHref(t)), this.nodeId = t, this.query = "", this.page = 1, (r = this.selection) == null || r.clearSelection(), this.load();
  }
  restoreNodeFromHistory() {
    var t;
    const e = this.getNodeIdFromUrl();
    e && (this.nodeId = e, this.query = "", this.page = 1, this.restorePageFromHistory(), (t = this.selection) == null || t.clearSelection(), this.load());
  }
  storeParentNode(e) {
    var o;
    const t = this.getParentStorageKey(e.current.key || String(e.current.id));
    (o = e.parent) != null && o.key ? window.sessionStorage.setItem(t, e.parent.key) : window.sessionStorage.removeItem(t);
  }
  redirectToParent() {
    var t, o;
    const e = ((o = (t = this.data) == null ? void 0 : t.parent) == null ? void 0 : o.key) ?? window.sessionStorage.getItem(this.getParentStorageKey(this.nodeId));
    return e ? (window.location.replace(this.getDocumentHref(e)), !0) : !1;
  }
  getParentStorageKey(e) {
    return `${q}${e}`;
  }
  restorePageFromHistory() {
    const e = history.state, t = e == null ? void 0 : e[b];
    if (t == null || typeof t != "object")
      return;
    const { nodeId: o, page: r } = t;
    o === this.nodeId && typeof r == "number" && Number.isInteger(r) && r > 0 && (this.page = r);
  }
  storePageInHistory() {
    if (!this.nodeId)
      return;
    const e = history.state != null && typeof history.state == "object" ? history.state : {};
    history.replaceState({
      ...e,
      [b]: {
        nodeId: this.nodeId,
        page: this.page
      }
    }, "", window.location.href);
  }
  setQueryFromCollectionFilter(e) {
    const t = "filter" in (e ?? {}) ? e.filter : void 0, o = typeof t == "string" ? t : "";
    o !== this.query && (this.query = o, this.page = 1, this.storePageInHistory(), this.nodeId && this.load());
  }
  setSort(e) {
    if (S(e)) {
      if (e === this.sort) {
        v(e);
        return;
      }
      this.sort = e, v(e), this.page = 1, this.storePageInHistory(), this.load();
    }
  }
  setPage(e) {
    e < 1 || e === this.page || this.data != null && e > this.data.totalPages || (this.page = e, this.storePageInHistory(), this.load());
  }
  updatePageSizeForViewport() {
    const e = Math.max(0, this.clientWidth - 48);
    if (e === 0)
      return !1;
    const o = Math.max(1, Math.floor((e + y) / (I + y))) * z;
    return o === this.pageSize ? !1 : (this.pageSize = o, !0);
  }
  toggleProduct(e) {
    var t;
    (t = this.selection) == null || t.toggleSelect(e.key);
  }
  toggleCurrentPage() {
    var a;
    if (this.selection == null)
      return;
    const t = (((a = this.data) == null ? void 0 : a.products) ?? []).map((i) => i.key), o = t.length > 0 && t.every((i) => this.selection.isSelected(i)), r = this.selection.getSelection();
    this.selection.setSelection(o ? r.filter((i) => !t.includes(i)) : [.../* @__PURE__ */ new Set([...r, ...t])]);
  }
  revealCollectionHost() {
    for (const e of this.getComposedAncestors("umb-collection-default")) {
      const t = e.shadowRoot, o = t == null ? void 0 : t.querySelector("#router"), r = t == null ? void 0 : t.querySelector("umb-body-layout"), a = t == null ? void 0 : t.querySelector("#empty-state");
      o == null || o.style.setProperty("visibility", "visible", "important"), a == null || a.style.setProperty("display", "none", "important"), r == null || r.classList.add("has-items");
    }
    for (const e of this.getComposedShadowRoots())
      this.hideDefaultCollectionPagination(e), this.observeDefaultCollectionPagination(e);
  }
  hideDefaultCollectionPagination(e) {
    var t;
    (t = e.querySelector("umb-collection-pagination")) == null || t.style.setProperty("display", "none", "important");
  }
  observeDefaultCollectionPagination(e) {
    this.collectionPaginationObserver == null && (this.collectionPaginationObserver = new MutationObserver(() => this.revealCollectionHost())), this.collectionPaginationObserver.observe(e, { childList: !0, subtree: !0 });
  }
  getComposedAncestors(e) {
    const t = [];
    let o = this;
    for (; o != null; )
      o instanceof HTMLElement && o.matches(e) && t.push(o), o = o.parentNode ?? (o.getRootNode() instanceof ShadowRoot ? o.getRootNode().host : null);
    return t;
  }
  getComposedShadowRoots() {
    const e = /* @__PURE__ */ new Set();
    let t = this;
    for (; t != null; ) {
      const o = t.getRootNode();
      o instanceof ShadowRoot && e.add(o), t = t.parentNode ?? (o instanceof ShadowRoot ? o.host : null);
    }
    return [...e];
  }
  render() {
    this.innerHTML = `
      <style>${L}</style>
      <div class="catalog-shell">
        ${this.renderContent()}
      </div>
    `, this.revealCollectionHost(), this.bindEvents();
  }
  renderContent() {
    return this.loading ? this.renderLoading() : this.error ? `<div class="surface state error"><strong>Catalog unavailable</strong><span>${p(this.error)}</span><button type="button" data-action="retry">Retry</button></div>` : this.data == null ? '<div class="surface state">No catalog data.</div>' : !this.query && this.data.productCount === 0 && this.data.subcategoryCount === 0 ? `
        ${this.renderBreadcrumbs(this.data)}
        ${this.renderHeader(this.data)}
        ${this.renderEmptyCategory()}
      ` : `
      ${this.renderBreadcrumbs(this.data)}
      ${this.renderHeader(this.data)}
      ${this.renderToolbar(this.data)}
      ${!this.query && this.data.subcategories.length > 0 ? this.renderSubcategories(this.data.subcategories) : ""}
      ${this.renderProducts(this.data)}
      ${this.renderPagination(this.data)}
    `;
  }
  renderLoading() {
    return `
      <div class="catalog-loading" aria-busy="true" aria-label="Loading catalog" role="status">
        <div class="loading-breadcrumbs">
          <span class="skeleton skeleton-breadcrumb"></span>
          <span class="skeleton skeleton-breadcrumb"></span>
          <span class="skeleton skeleton-breadcrumb current"></span>
        </div>
        <div class="loading-header">
          <span class="skeleton skeleton-back"></span>
          <div>
            <span class="skeleton skeleton-title"></span>
            <span class="skeleton skeleton-summary"></span>
          </div>
        </div>
        <div class="loading-toolbar">
          <span class="skeleton skeleton-control"></span>
          <span class="skeleton skeleton-control"></span>
        </div>
        <section class="loading-section">
          <span class="skeleton skeleton-section-title"></span>
          <div class="loading-chips">
            <span class="skeleton skeleton-chip"></span>
            <span class="skeleton skeleton-chip"></span>
            <span class="skeleton skeleton-chip"></span>
          </div>
        </section>
        <section class="loading-section">
          <span class="skeleton skeleton-section-title"></span>
          <div class="loading-grid">
            ${Array.from({ length: 8 }, () => '<article class="loading-card"><span class="skeleton skeleton-image"></span><div class="loading-card-body"><span class="skeleton skeleton-product-title"></span><span class="skeleton skeleton-product-meta"></span><span class="skeleton skeleton-product-status"></span></div></article>').join("")}
          </div>
        </section>
        <span class="visually-hidden">Loading catalog…</span>
      </div>
    `;
  }
  renderBreadcrumbs(e) {
    return `<nav class="breadcrumbs">${e.breadcrumbs.map((t, o) => {
      const r = o === e.breadcrumbs.length - 1, a = p(t.title || t.name), i = t.siblings.filter((d) => d.key !== t.key), h = t.contentTypeAlias === "ekmCategory" && i.length > 0 ? `<div class="breadcrumb-category"><a class="breadcrumb-link" href="${this.getDocumentHref(t.key)}" data-breadcrumb-link-key="${p(t.key)}" ${r ? 'aria-current="page"' : ""}>${a}</a><details class="breadcrumb-menu"><summary aria-label="Show sibling categories for ${a}"><span aria-hidden="true"></span></summary><div class="breadcrumb-menu-list" role="menu">${i.map((d) => {
        const u = p(d.title || d.name);
        return `<button type="button" data-breadcrumb-category-key="${p(d.key)}" role="menuitem">${u}</button>`;
      }).join("")}</div></details></div>` : r ? `<span aria-current="page">${a}</span>` : `<a href="${this.getDocumentHref(t.key)}">${a}</a>`;
      return `${o > 0 ? '<span class="sep">/</span>' : ""}${h}`;
    }).join("")}</nav>`;
  }
  renderHeader(e) {
    return `
      <header class="catalog-header">
        <button type="button" class="back" data-action="back" ${e.parent == null ? "disabled" : ""}>←</button>
        <div>
          <h1>${p(e.current.title || e.current.name)}</h1>
          <p>${e.productCount} products · ${e.subcategoryCount} subcategories</p>
        </div>
      </header>
    `;
  }
  renderToolbar(e) {
    const t = e.products.length > 0 && e.products.every((o) => {
      var r;
      return (r = this.selection) == null ? void 0 : r.isSelected(o.key);
    });
    return `
      <div class="toolbar">
        <select data-field="sort">
          ${this.renderSortOption("sortOrderAsc", "Sort order asc")}
          ${this.renderSortOption("sortOrderDesc", "Sort order desc")}
          ${this.renderSortOption("nameAsc", "Name asc")}
          ${this.renderSortOption("nameDesc", "Name desc")}
          ${this.renderSortOption("createdAsc", "Created asc")}
          ${this.renderSortOption("createdDesc", "Created desc")}
          ${this.renderSortOption("updatedAsc", "Updated asc")}
          ${this.renderSortOption("updatedDesc", "Updated desc")}
        </select>
        <button type="button" class="select-all" data-action="toggle-page"><span class="fake-checkbox ${t ? "checked" : ""}"></span>Select all</button>
      </div>
    `;
  }
  renderSortOption(e, t) {
    return `<option value="${e}" ${this.sort === e ? "selected" : ""}>${t}</option>`;
  }
  renderSubcategories(e) {
    return `
      <section class="section">
        <h2>Subcategories</h2>
        <div class="chips">${e.map((t) => `
          <a class="chip" href="${this.getDocumentHref(t.key)}">
            <span class="tile">▦</span><span class="chip-text"><strong>${p(t.title || t.name)}</strong><small>${t.productCount} products · ${t.subcategoryCount} subcategories</small></span><span>›</span>
          </a>
        `).join("")}</div>
      </section>
    `;
  }
  renderEmptyCategory() {
    return `
      <section class="section">
        <div class="empty empty-category">
          <strong>No catalog items in this category yet</strong>
          <span>This category does not contain any direct products or subcategories.</span>
        </div>
      </section>
    `;
  }
  renderProducts(e) {
    const t = this.query ? `No products match &quot;${p(this.query)}&quot;` : "No products in this category yet";
    return `
      <section class="section">
        <h2>Products (${e.filteredProductCount})</h2>
        ${e.products.length === 0 ? `<div class="empty">${t}</div>` : `<div class="grid">${e.products.map((o) => this.renderProduct(o)).join("")}</div>`}
      </section>
    `;
  }
  renderProduct(e) {
    var r;
    const t = ((r = this.selection) == null ? void 0 : r.isSelected(e.key)) ?? !1, o = `/umbraco/section/content/workspace/document/edit/${e.key}`;
    return `
      <article class="card ${t ? "selected" : ""}">
        <button type="button" class="checkbox ${t ? "checked" : ""}" data-product-key="${e.key}">${t ? "✓" : ""}</button>
        <a class="image ${e.published ? "" : "dimmed"}" href="${o}">${e.image ? `<umb-imaging-thumbnail unique="${p(e.image)}" width="320" height="160" alt="${p(e.title || e.name)}"></umb-imaging-thumbnail>` : "<span>Product image</span>"}</a>
        <div class="card-body">
          <a class="title" href="${o}">${p(e.title || e.name)}</a>
          <div class="meta"><span>${e.sku ? `SKU ${p(e.sku)}` : "No SKU"}</span><strong>${p(e.price)}</strong></div>
          <div class="status-row"><span class="pill ${O(e.status)}"><i></i>${e.status}</span><strong class="availability ${e.available ? "ok" : "bad"}">${e.available ? "✓ Available" : "✕ Unavailable"}</strong></div>
        </div>
      </article>
    `;
  }
  renderPagination(e) {
    if (e.filteredProductCount === 0 || e.totalPages <= 1)
      return "";
    const t = (e.page - 1) * e.pageSize + 1, o = Math.min(e.page * e.pageSize, e.filteredProductCount);
    return `
      <footer class="pagination">
        <div>
          <button type="button" data-page="${e.page - 1}" ${e.page <= 1 ? "disabled" : ""}>‹ Prev</button>
          ${this.paginationItems(e.page, e.totalPages).map((r) => r === "…" ? "<span>…</span>" : `<button type="button" class="${r === e.page ? "current" : ""}" data-page="${r}">${r}</button>`).join("")}
          <button type="button" data-page="${e.page + 1}" ${e.page >= e.totalPages ? "disabled" : ""}>Next ›</button>
        </div>
        <span>${t}–${o} of ${e.filteredProductCount}</span>
      </footer>
    `;
  }
  paginationItems(e, t) {
    if (t <= 7)
      return Array.from({ length: t }, (i, n) => n + 1);
    const r = [.../* @__PURE__ */ new Set([1, t, e - 1, e, e + 1])].filter((i) => i >= 1 && i <= t).sort((i, n) => i - n), a = [];
    for (const i of r) {
      const n = a[a.length - 1];
      typeof n == "number" && i - n > 1 && a.push("…"), a.push(i);
    }
    return a;
  }
  bindEvents() {
    var e, t, o, r, a;
    (e = this.querySelector('[data-action="retry"]')) == null || e.addEventListener("click", () => void this.load()), (t = this.querySelector('[data-action="back"]')) == null || t.addEventListener("click", () => {
      var i;
      ((i = this.data) == null ? void 0 : i.parent) != null && this.drillTo(this.data.parent);
    }), (o = this.querySelector('[data-action="toggle-page"]')) == null || o.addEventListener("click", () => this.toggleCurrentPage()), (r = this.querySelector('[data-field="sort"]')) == null || r.addEventListener("input", (i) => this.setSort(i.target.value)), (a = this.querySelector('[data-field="sort"]')) == null || a.addEventListener("change", (i) => this.setSort(i.target.value)), this.querySelectorAll("[data-product-key]").forEach((i) => {
      i.addEventListener("click", (n) => {
        var u;
        n.preventDefault();
        const h = i.dataset.productKey, d = (u = this.data) == null ? void 0 : u.products.find((g) => g.key === h);
        d != null && this.toggleProduct(d);
      });
    }), this.querySelectorAll("[data-page]").forEach((i) => {
      i.addEventListener("click", () => this.setPage(Number(i.dataset.page)));
    }), this.querySelectorAll("[data-breadcrumb-link-key]").forEach((i) => {
      i.addEventListener("click", (n) => {
        var u;
        n.preventDefault();
        const h = i.dataset.breadcrumbLinkKey, d = (u = this.data) == null ? void 0 : u.breadcrumbs.find((g) => g.key === h);
        d != null && this.drillTo(d);
      });
    }), this.querySelectorAll("[data-breadcrumb-category-key]").forEach((i) => {
      i.addEventListener("click", () => {
        var d;
        const n = i.dataset.breadcrumbCategoryKey, h = (d = this.data) == null ? void 0 : d.breadcrumbs.flatMap((u) => u.siblings).find((u) => u.key === n);
        h != null && this.drillTo(h);
      });
    }), this.querySelectorAll(".breadcrumb-menu").forEach((i) => {
      i.addEventListener("toggle", () => {
        i.open && this.querySelectorAll(".breadcrumb-menu").forEach((n) => {
          n !== i && (n.open = !1);
        });
      });
    });
  }
  getNodeIdFromUrl() {
    const e = this.getCatalogNodeFromUrl();
    if (e != null)
      return e;
    const t = window.location.href, o = t.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);
    if (o != null)
      return o[0];
    const r = t.match(/\/edit\/([^/?#]+)/i);
    return (r == null ? void 0 : r[1]) ?? "";
  }
  getDocumentHref(e) {
    const t = new URL(window.location.href);
    return t.searchParams.delete(f), t.pathname.match(/\/edit\/[^/]+/i) != null ? (t.pathname = t.pathname.replace(/\/edit\/[^/]+/i, `/edit/${e}`), `${t.pathname}${t.search}${t.hash}`) : `/umbraco/section/content/workspace/document/edit/${e}`;
  }
  getCatalogNodeFromUrl() {
    const e = new URL(window.location.href).searchParams.get(f);
    return e && e.trim().length > 0 ? e : null;
  }
  async fetchJson(e) {
    const t = await fetch(e, { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!t.ok)
      throw new x(await t.text() || `Request failed (${t.status}).`, t.status);
    return await t.json();
  }
}
class x extends Error {
  constructor(c, e) {
    super(c), this.status = e;
  }
}
function p(s) {
  return s.replace(/[&<>"]/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[c] ?? c);
}
function O(s) {
  return s === "Published" ? "published" : s === "Pending changes" ? "pending" : "unpublished";
}
function S(s) {
  return [
    "sortOrderAsc",
    "sortOrderDesc",
    "nameAsc",
    "nameDesc",
    "createdAsc",
    "createdDesc",
    "updatedAsc",
    "updatedDesc"
  ].includes(s);
}
function k() {
  try {
    const s = window.localStorage.getItem(w);
    return s != null && S(s) ? s : m;
  } catch {
    return m;
  }
}
function v(s) {
  try {
    window.localStorage.setItem(w, s);
  } catch {
  }
}
const L = `
  ekom-catalog-collection-view { color: #1f1f1f; display: block !important; font-family: Lato, sans-serif; visibility: visible !important; }
  .catalog-shell { background: #f6f4f4; min-height: 100%; padding: 24px; }
  .surface, .empty { background: #fff; border: 1px solid #d8d7d9; border-radius: 3px; }
  .state { padding: 24px; display: grid; gap: 10px; }
  .error { color: #d42054; }
  .catalog-loading { animation: loading-enter .18s ease-out; }
  .skeleton { animation: skeleton-shimmer 1.4s ease-in-out infinite; background: linear-gradient(100deg, #e9e9eb 35%, #f8f8f9 50%, #e9e9eb 65%); background-size: 200% 100%; border-radius: 3px; display: block; }
  .loading-breadcrumbs { display: flex; gap: 8px; margin-bottom: 18px; }
  .skeleton-breadcrumb { height: 13px; width: 58px; }
  .skeleton-breadcrumb.current { width: 82px; }
  .loading-header { align-items: center; display: flex; gap: 14px; margin-bottom: 18px; }
  .skeleton-back { border-radius: 50%; height: 36px; width: 36px; }
  .loading-header div { display: grid; gap: 8px; }
  .skeleton-title { height: 28px; width: 176px; }
  .skeleton-summary { height: 13px; width: 132px; }
  .loading-toolbar { display: flex; gap: 10px; justify-content: flex-end; margin-bottom: 14px; }
  .skeleton-control { height: 36px; width: 118px; }
  .loading-section { margin-top: 22px; }
  .skeleton-section-title { height: 11px; margin-bottom: 10px; width: 96px; }
  .loading-chips { display: flex; flex-wrap: wrap; gap: 10px; }
  .skeleton-chip { border-radius: 999px; height: 42px; width: 172px; }
  .loading-grid { display: grid; gap: 14px; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); }
  .loading-card { background: #fff; border: 1px solid #e9e9eb; border-radius: 4px; overflow: hidden; }
  .skeleton-image { border-radius: 0; height: 160px; }
  .loading-card-body { display: grid; gap: 12px; padding: 12px; }
  .skeleton-product-title { height: 16px; width: 76%; }
  .skeleton-product-meta { height: 13px; width: 58%; }
  .skeleton-product-status { height: 24px; width: 88%; }
  .visually-hidden { height: 1px; margin: -1px; overflow: hidden; position: absolute; width: 1px; clip: rect(0, 0, 0, 0); }
  @keyframes loading-enter { from { opacity: 0; } to { opacity: 1; } }
  @keyframes skeleton-shimmer { from { background-position: 100% 0; } to { background-position: -100% 0; } }
  @media (prefers-reduced-motion: reduce) { .catalog-loading, .skeleton { animation: none; } }
  button, input, select { font: inherit; }
  button { cursor: pointer; }
  button:disabled { cursor: default; opacity: .45; }
  .breadcrumbs { align-items: center; color: #777; display: flex; flex-wrap: wrap; font-size: 13px; gap: 8px; margin-bottom: 18px; }
  .breadcrumbs a { color: #2152a3; text-decoration: none; }
  .breadcrumbs a:hover { text-decoration: underline; }
  .breadcrumb-category { align-items: center; display: flex; gap: 2px; }
  .breadcrumb-menu { position: relative; }
  .breadcrumb-menu summary { align-items: center; background: #fff; border: 1px solid #b8c7df; border-radius: 3px; box-sizing: border-box; color: #2152a3; cursor: pointer; display: flex; height: 22px; justify-content: center; list-style: none; padding: 0; width: 22px; }
  .breadcrumb-menu summary::-webkit-details-marker { display: none; }
  .breadcrumb-menu summary:hover { background: #e6edf9; border-color: #2152a3; }
  .breadcrumb-menu summary:focus-visible { border-radius: 2px; outline: 2px solid #2152a3; outline-offset: 2px; }
  .breadcrumb-menu summary span { border: solid currentColor; border-width: 0 1.5px 1.5px 0; display: block; height: 5px; transform: translateY(-1px) rotate(45deg); width: 5px; }
  .breadcrumb-menu[open] summary span { transform: translateY(1px) rotate(225deg); }
  .breadcrumb-menu-list { background: #fff; border: 1px solid #d8d7d9; border-radius: 3px; box-shadow: 0 6px 16px rgba(0,0,0,.12); display: grid; left: 0; max-height: 320px; min-width: 220px; overflow-y: auto; padding: 4px; position: absolute; top: calc(100% + 4px); z-index: 2; }
  .breadcrumb-menu-list button { background: transparent; border: 0; color: #1f1f1f; padding: 8px 10px; text-align: left; white-space: nowrap; }
  .breadcrumb-menu-list button:hover, .breadcrumb-menu-list button:focus-visible { background: #f0f4fc; border-radius: 2px; outline: none; }
  .sep { color: #a5a5a5; }
  .catalog-header { align-items: center; display: flex; gap: 14px; margin-bottom: 18px; }
  .back { background: #fff; border: 1px solid #d8d7d9; border-radius: 3px; height: 36px; width: 36px; }
  h1 { font-size: 24px; line-height: 1.2; margin: 0; }
  p { color: #777; margin: 4px 0 0; }
  .toolbar { display: flex; gap: 10px; justify-content: flex-end; margin-bottom: 14px; }
  select, .select-all { background: #fff; border: 1px solid #d8d7d9; border-radius: 3px; height: 36px; padding: 0 10px; }
  select { appearance: none; background-image: linear-gradient(45deg, transparent 50%, #777 50%), linear-gradient(135deg, #777 50%, transparent 50%); background-position: calc(100% - 16px) 15px, calc(100% - 11px) 15px; background-repeat: no-repeat; background-size: 5px 5px, 5px 5px; padding-right: 34px; }
  .select-all { align-items: center; display: flex; gap: 8px; }
  .fake-checkbox { border: 1px solid #b8b8b8; border-radius: 2px; height: 14px; width: 14px; }
  .fake-checkbox.checked { background: #2152a3; border-color: #2152a3; }
  .section { margin-top: 22px; }
  h2 { color: #777; font-size: 11px; letter-spacing: .08em; margin: 0 0 10px; text-transform: uppercase; }
  .chips { display: flex; flex-wrap: wrap; gap: 10px; }
  .chip { align-items: center; background: #fff; border: 1px solid #d8d7d9; border-radius: 999px; color: inherit; display: flex; gap: 10px; padding: 6px 11px 6px 6px; text-align: left; text-decoration: none; }
  .chip-text { display: grid; gap: 1px; }
  .chip-text small { color: #777; font-size: 11px; }
  .tile { align-items: center; background: #f6f4f4; border-radius: 50%; color: #1b264f; display: inline-flex; height: 30px; justify-content: center; width: 30px; }
  .grid { display: grid; gap: 14px; grid-template-columns: repeat(auto-fill, minmax(220px, 1fr)); }
  .card { background: #fff; border: 1px solid #e9e9eb; border-radius: 4px; overflow: hidden; position: relative; transition: box-shadow .15s, border-color .15s; }
  .card:hover { box-shadow: 0 6px 16px rgba(0,0,0,.08); }
  .card.selected { border-color: #2152a3; box-shadow: 0 0 0 1px #2152a3; }
   .checkbox { background: transparent; border: 0; color: transparent; height: 50px; left: -5px; padding: 0; position: absolute; top: -5px; width: 50px; z-index: 1; }
   .checkbox::after { background: rgba(255,255,255,.76); border: 1px solid #d8d7d9; border-radius: 3px; box-sizing: border-box; color: #fff; content: ''; font-size: 14px; height: 20px; left: 15px; line-height: 18px; opacity: .6; position: absolute; top: 15px; width: 20px; }
   .card:hover .checkbox::after, .checkbox.checked::after { opacity: 1; }
   .checkbox.checked::after { background: #2152a3; border-color: #2152a3; content: '✓'; }
  .image { align-items: center; background: linear-gradient(135deg, #f4f4f6, #e9e9eb); color: #888; display: flex; height: 160px; justify-content: center; text-decoration: none; }
  .image.dimmed { opacity: .45; }
  .image umb-imaging-thumbnail { height: 100%; width: 100%; }
  .card-body { display: grid; gap: 10px; padding: 12px; }
  .title { color: #1f1f1f; display: -webkit-box; font-size: 14px; font-weight: 700; line-clamp: 2; -webkit-line-clamp: 2; -webkit-box-orient: vertical; overflow: hidden; text-decoration: none; }
  .title:hover { text-decoration: underline; }
  .meta, .status-row { align-items: center; display: flex; justify-content: space-between; gap: 10px; }
  .meta span { color: #777; font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .meta strong { font-size: 14px; white-space: nowrap; }
  .pill { align-items: center; background: #f4f4f6; border-radius: 999px; color: #555; display: inline-flex; font-size: 12px; gap: 6px; padding: 4px 8px; }
  .pill i { border-radius: 50%; display: inline-block; height: 7px; width: 7px; }
  .published i { background: #2bc37c; }
  .pending i { background: #fbd142; }
  .unpublished i { background: #c4c4c4; }
  .availability { font-size: 12px; white-space: nowrap; }
  .availability.ok { color: #1c7d3c; }
  .availability.bad { color: #d42054; }
  .empty { border-style: dashed; color: #777; padding: 30px; text-align: center; }
  .empty-category { display: grid; gap: 6px; }
  .empty-category strong { color: #1f1f1f; font-size: 15px; }
  .pagination { align-items: center; display: flex; gap: 18px; justify-content: center; margin-top: 24px; }
  .pagination div { display: flex; gap: 6px; }
  .pagination button { background: #fff; border: 1px solid #d8d7d9; border-radius: 3px; min-width: 34px; padding: 7px 10px; }
  .pagination .current { background: #1b264f; border-color: #1b264f; color: #fff; }
`;
customElements.define("ekom-catalog-collection-view", A);
export {
  A as default
};
