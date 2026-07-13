var $ = Object.defineProperty;
var P = (o, d, e) => d in o ? $(o, d, { enumerable: !0, configurable: !0, writable: !0, value: e }) : o[d] = e;
var a = (o, d, e) => P(o, typeof d != "symbol" ? d + "" : d, e);
import { UMB_COLLECTION_CONTEXT as C } from "@umbraco-cms/backoffice/collection";
import { UmbElementMixin as E } from "@umbraco-cms/backoffice/element-api";
import "@umbraco-cms/backoffice/imaging";
const z = 16, h = "sortOrderAsc", g = "ekomCatalogNode", b = 14, T = 220, O = 4, I = "ekomCatalogParent:", x = "ekomCatalogSort";
class q extends E(HTMLElement) {
  constructor() {
    super(...arguments);
    a(this, "nodeId", "");
    a(this, "query", "");
    a(this, "sort", m());
    a(this, "page", 1);
    a(this, "pageSize", z);
    a(this, "loading", !0);
    a(this, "error", "");
    a(this, "data");
    a(this, "revealTimer");
    a(this, "resizeTimer");
    a(this, "collectionPaginationObserver");
    a(this, "selectedProductKeys", /* @__PURE__ */ new Set());
    a(this, "onPopState", () => this.restoreNodeFromHistory());
    a(this, "onResize", () => {
      this.resizeTimer != null && window.clearTimeout(this.resizeTimer), this.resizeTimer = window.setTimeout(() => {
        this.updatePageSizeForViewport() && this.nodeId && this.data != null && !this.loading && this.load(!1);
      }, 150);
    });
    a(this, "onWindowFocus", () => {
      document.visibilityState !== "hidden" && this.nodeId && !this.loading && this.load(!1);
    });
  }
  connectedCallback() {
    super.connectedCallback(), this.style.setProperty("display", "block", "important"), this.style.setProperty("visibility", "visible", "important"), window.addEventListener("popstate", this.onPopState), window.addEventListener("resize", this.onResize), window.addEventListener("focus", this.onWindowFocus), document.addEventListener("visibilitychange", this.onWindowFocus), this.revealCollectionHost(), this.revealTimer = window.setTimeout(() => this.revealCollectionHost(), 250), this.nodeId = this.getNodeIdFromUrl(), this.sort = m(), this.consumeContext(C, (e) => {
      var r;
      const t = (r = e == null ? void 0 : e.getConfig()) == null ? void 0 : r.unique;
      this.getCatalogNodeFromUrl() == null && t != null && t !== this.nodeId && (this.nodeId = t, this.load()), this.observe(e == null ? void 0 : e.filter, (i) => this.setQueryFromCollectionFilter(i), "ekomCatalogCollectionFilter");
    }), this.render(), this.nodeId && this.load();
  }
  disconnectedCallback() {
    var e;
    window.removeEventListener("popstate", this.onPopState), window.removeEventListener("resize", this.onResize), window.removeEventListener("focus", this.onWindowFocus), document.removeEventListener("visibilitychange", this.onWindowFocus), this.revealTimer != null && window.clearTimeout(this.revealTimer), this.resizeTimer != null && window.clearTimeout(this.resizeTimer), (e = this.collectionPaginationObserver) == null || e.disconnect(), this.collectionPaginationObserver = void 0, super.disconnectedCallback();
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
      if (t instanceof f && t.status === 404 && this.redirectToParent())
        return;
      this.error = t instanceof Error ? t.message : "Unable to load catalog.";
    } finally {
      this.loading = !1, this.render();
    }
  }
  drillTo(e) {
    window.location.assign(this.getDocumentHref(e.key || String(e.id)));
  }
  restoreNodeFromHistory() {
    const e = this.getNodeIdFromUrl();
    !e || e === this.nodeId || (this.nodeId = e, this.query = "", this.page = 1, this.selectedProductKeys.clear(), this.load());
  }
  storeParentNode(e) {
    var r;
    const t = this.getParentStorageKey(e.current.key || String(e.current.id));
    (r = e.parent) != null && r.key ? window.sessionStorage.setItem(t, e.parent.key) : window.sessionStorage.removeItem(t);
  }
  redirectToParent() {
    var t, r;
    const e = ((r = (t = this.data) == null ? void 0 : t.parent) == null ? void 0 : r.key) ?? window.sessionStorage.getItem(this.getParentStorageKey(this.nodeId));
    return e ? (window.location.replace(this.getDocumentHref(e)), !0) : !1;
  }
  getParentStorageKey(e) {
    return `${I}${e}`;
  }
  setQueryFromCollectionFilter(e) {
    const t = "filter" in (e ?? {}) ? e.filter : void 0, r = typeof t == "string" ? t : "";
    r !== this.query && (this.query = r, this.page = 1, this.nodeId && this.load());
  }
  setSort(e) {
    if (v(e)) {
      if (e === this.sort) {
        y(e);
        return;
      }
      this.sort = e, y(e), this.page = 1, this.load();
    }
  }
  setPage(e) {
    e < 1 || e === this.page || this.data != null && e > this.data.totalPages || (this.page = e, this.load());
  }
  updatePageSizeForViewport() {
    const e = Math.max(0, this.clientWidth - 48);
    if (e === 0)
      return !1;
    const r = Math.max(1, Math.floor((e + b) / (T + b))) * O;
    return r === this.pageSize ? !1 : (this.pageSize = r, !0);
  }
  toggleProduct(e) {
    this.selectedProductKeys.has(e.key) ? this.selectedProductKeys.delete(e.key) : this.selectedProductKeys.add(e.key), this.render();
  }
  toggleCurrentPage() {
    var r;
    const e = ((r = this.data) == null ? void 0 : r.products) ?? [], t = e.length > 0 && e.every((i) => this.selectedProductKeys.has(i.key));
    for (const i of e)
      t ? this.selectedProductKeys.delete(i.key) : this.selectedProductKeys.add(i.key);
    this.render();
  }
  clearSelection() {
    this.selectedProductKeys.clear(), this.render();
  }
  revealCollectionHost() {
    for (const e of this.getComposedAncestors("umb-collection-default")) {
      const t = e.shadowRoot, r = t == null ? void 0 : t.querySelector("#router"), i = t == null ? void 0 : t.querySelector("umb-body-layout"), n = t == null ? void 0 : t.querySelector("#empty-state");
      r == null || r.style.setProperty("visibility", "visible", "important"), n == null || n.style.setProperty("display", "none", "important"), i == null || i.classList.add("has-items");
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
    let r = this;
    for (; r != null; )
      r instanceof HTMLElement && r.matches(e) && t.push(r), r = r.parentNode ?? (r.getRootNode() instanceof ShadowRoot ? r.getRootNode().host : null);
    return t;
  }
  getComposedShadowRoots() {
    const e = /* @__PURE__ */ new Set();
    let t = this;
    for (; t != null; ) {
      const r = t.getRootNode();
      r instanceof ShadowRoot && e.add(r), t = t.parentNode ?? (r instanceof ShadowRoot ? r.host : null);
    }
    return [...e];
  }
  render() {
    this.innerHTML = `
      <style>${N}</style>
      <div class="catalog-shell">
        ${this.renderContent()}
      </div>
    `, this.revealCollectionHost(), this.bindEvents();
  }
  renderContent() {
    if (this.loading)
      return '<div class="surface state">Loading catalog…</div>';
    if (this.error)
      return `<div class="surface state error"><strong>Catalog unavailable</strong><span>${l(this.error)}</span><button type="button" data-action="retry">Retry</button></div>`;
    if (this.data == null)
      return '<div class="surface state">No catalog data.</div>';
    const e = this.selectedProductKeys.size;
    return !this.query && this.data.productCount === 0 && this.data.subcategoryCount === 0 ? `
        ${this.renderBreadcrumbs(this.data)}
        ${this.renderHeader(this.data)}
        ${this.renderEmptyCategory()}
      ` : `
      ${this.renderBreadcrumbs(this.data)}
      ${this.renderHeader(this.data)}
      ${this.renderToolbar(this.data)}
      ${e > 0 ? this.renderBulkBar(e) : ""}
      ${!this.query && this.data.subcategories.length > 0 ? this.renderSubcategories(this.data.subcategories) : ""}
      ${this.renderProducts(this.data)}
      ${this.renderPagination(this.data)}
    `;
  }
  renderBreadcrumbs(e) {
    return `<nav class="breadcrumbs">${e.breadcrumbs.map((t, r) => {
      const i = r === e.breadcrumbs.length - 1, n = l(t.title || t.name);
      return `${r > 0 ? '<span class="sep">/</span>' : ""}${i ? `<span>${n}</span>` : `<a href="${this.getDocumentHref(t.key)}">${n}</a>`}`;
    }).join("")}</nav>`;
  }
  renderHeader(e) {
    return `
      <header class="catalog-header">
        <button type="button" class="back" data-action="back" ${e.parent == null ? "disabled" : ""}>←</button>
        <div>
          <h1>${l(e.current.title || e.current.name)}</h1>
          <p>${e.productCount} products · ${e.subcategoryCount} subcategories</p>
        </div>
      </header>
    `;
  }
  renderToolbar(e) {
    const t = e.products.length > 0 && e.products.every((r) => this.selectedProductKeys.has(r.key));
    return `
      <div class="toolbar">
        <select data-field="sort">
          ${this.renderSortOption("sortOrderAsc", "Sort order asc")}
          ${this.renderSortOption("sortOrderDesc", "Sort order desc")}
          ${this.renderSortOption("nameAsc", "Name asc")}
          ${this.renderSortOption("nameDesc", "Name desc")}
          ${this.renderSortOption("priceAsc", "Price asc")}
          ${this.renderSortOption("priceDesc", "Price desc")}
          ${this.renderSortOption("skuAsc", "SKU asc")}
          ${this.renderSortOption("skuDesc", "SKU desc")}
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
  renderBulkBar(e) {
    return `
      <div class="bulk-bar">
        <strong>${e} selected</strong>
        <div>
          <button type="button" disabled>Publish</button>
          <button type="button" disabled>Unpublish</button>
          <button type="button" disabled>Move</button>
          <button type="button" class="clear" data-action="clear-selection">Clear ✕</button>
        </div>
      </div>
    `;
  }
  renderSubcategories(e) {
    return `
      <section class="section">
        <h2>Subcategories</h2>
        <div class="chips">${e.map((t) => `
          <a class="chip" href="${this.getDocumentHref(t.key)}">
            <span class="tile">▦</span><span class="chip-text"><strong>${l(t.title || t.name)}</strong><small>${t.productCount} products · ${t.subcategoryCount} subcategories</small></span><span>›</span>
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
    const t = this.query ? `No products match &quot;${l(this.query)}&quot;` : "No products in this category yet";
    return `
      <section class="section">
        <h2>Products (${e.filteredProductCount})</h2>
        ${e.products.length === 0 ? `<div class="empty">${t}</div>` : `<div class="grid">${e.products.map((r) => this.renderProduct(r)).join("")}</div>`}
      </section>
    `;
  }
  renderProduct(e) {
    const t = this.selectedProductKeys.has(e.key), r = `/umbraco/section/content/workspace/document/edit/${e.key}`;
    return `
      <article class="card ${t ? "selected" : ""}">
        <button type="button" class="checkbox ${t ? "checked" : ""}" data-product-key="${e.key}">${t ? "✓" : ""}</button>
        <a class="image ${e.published ? "" : "dimmed"}" href="${r}">${e.image ? `<umb-imaging-thumbnail unique="${l(e.image)}" width="320" height="160" alt="${l(e.title || e.name)}"></umb-imaging-thumbnail>` : "<span>Product image</span>"}</a>
        <div class="card-body">
          <a class="title" href="${r}">${l(e.title || e.name)}</a>
          <div class="meta"><span>${e.sku ? `SKU ${l(e.sku)}` : "No SKU"}</span><strong>${l(e.price)}</strong></div>
          <div class="status-row"><span class="pill ${A(e.status)}"><i></i>${e.status}</span><strong class="availability ${e.available ? "ok" : "bad"}">${e.available ? "✓ Available" : "✕ Unavailable"}</strong></div>
        </div>
      </article>
    `;
  }
  renderPagination(e) {
    if (e.filteredProductCount === 0 || e.totalPages <= 1)
      return "";
    const t = (e.page - 1) * e.pageSize + 1, r = Math.min(e.page * e.pageSize, e.filteredProductCount);
    return `
      <footer class="pagination">
        <div>
          <button type="button" data-page="${e.page - 1}" ${e.page <= 1 ? "disabled" : ""}>‹ Prev</button>
          ${this.paginationItems(e.page, e.totalPages).map((i) => i === "…" ? "<span>…</span>" : `<button type="button" class="${i === e.page ? "current" : ""}" data-page="${i}">${i}</button>`).join("")}
          <button type="button" data-page="${e.page + 1}" ${e.page >= e.totalPages ? "disabled" : ""}>Next ›</button>
        </div>
        <span>${t}–${r} of ${e.filteredProductCount}</span>
      </footer>
    `;
  }
  paginationItems(e, t) {
    if (t <= 7)
      return Array.from({ length: t }, (c, s) => s + 1);
    const i = [.../* @__PURE__ */ new Set([1, t, e - 1, e, e + 1])].filter((c) => c >= 1 && c <= t).sort((c, s) => c - s), n = [];
    for (const c of i) {
      const s = n[n.length - 1];
      typeof s == "number" && c - s > 1 && n.push("…"), n.push(c);
    }
    return n;
  }
  bindEvents() {
    var e, t, r, i, n, c;
    (e = this.querySelector('[data-action="retry"]')) == null || e.addEventListener("click", () => void this.load()), (t = this.querySelector('[data-action="back"]')) == null || t.addEventListener("click", () => {
      var s;
      ((s = this.data) == null ? void 0 : s.parent) != null && this.drillTo(this.data.parent);
    }), (r = this.querySelector('[data-action="toggle-page"]')) == null || r.addEventListener("click", () => this.toggleCurrentPage()), (i = this.querySelector('[data-action="clear-selection"]')) == null || i.addEventListener("click", () => this.clearSelection()), (n = this.querySelector('[data-field="sort"]')) == null || n.addEventListener("input", (s) => this.setSort(s.target.value)), (c = this.querySelector('[data-field="sort"]')) == null || c.addEventListener("change", (s) => this.setSort(s.target.value)), this.querySelectorAll("[data-product-key]").forEach((s) => {
      s.addEventListener("click", (w) => {
        var u;
        w.preventDefault();
        const k = s.dataset.productKey, p = (u = this.data) == null ? void 0 : u.products.find((S) => S.key === k);
        p != null && this.toggleProduct(p);
      });
    }), this.querySelectorAll("[data-page]").forEach((s) => {
      s.addEventListener("click", () => this.setPage(Number(s.dataset.page)));
    });
  }
  getNodeIdFromUrl() {
    const e = this.getCatalogNodeFromUrl();
    if (e != null)
      return e;
    const t = window.location.href, r = t.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);
    if (r != null)
      return r[0];
    const i = t.match(/\/edit\/([^/?#]+)/i);
    return (i == null ? void 0 : i[1]) ?? "";
  }
  getDocumentHref(e) {
    const t = new URL(window.location.href);
    return t.searchParams.delete(g), t.pathname.match(/\/edit\/[^/]+/i) != null ? (t.pathname = t.pathname.replace(/\/edit\/[^/]+/i, `/edit/${e}`), `${t.pathname}${t.search}${t.hash}`) : `/umbraco/section/content/workspace/document/edit/${e}`;
  }
  getCatalogNodeFromUrl() {
    const e = new URL(window.location.href).searchParams.get(g);
    return e && e.trim().length > 0 ? e : null;
  }
  async fetchJson(e) {
    const t = await fetch(e, { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!t.ok)
      throw new f(await t.text() || `Request failed (${t.status}).`, t.status);
    return await t.json();
  }
}
class f extends Error {
  constructor(d, e) {
    super(d), this.status = e;
  }
}
function l(o) {
  return o.replace(/[&<>"]/g, (d) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[d] ?? d);
}
function A(o) {
  return o === "Published" ? "published" : o === "Pending changes" ? "pending" : "unpublished";
}
function v(o) {
  return [
    "sortOrderAsc",
    "sortOrderDesc",
    "nameAsc",
    "nameDesc",
    "priceAsc",
    "priceDesc",
    "skuAsc",
    "skuDesc",
    "createdAsc",
    "createdDesc",
    "updatedAsc",
    "updatedDesc"
  ].includes(o);
}
function m() {
  try {
    const o = window.localStorage.getItem(x);
    return o != null && v(o) ? o : h;
  } catch {
    return h;
  }
}
function y(o) {
  try {
    window.localStorage.setItem(x, o);
  } catch {
  }
}
const N = `
  ekom-catalog-collection-view { color: #1f1f1f; display: block !important; font-family: Lato, sans-serif; visibility: visible !important; }
  .catalog-shell { background: #f6f4f4; min-height: 100%; padding: 24px; }
  .surface, .empty { background: #fff; border: 1px solid #d8d7d9; border-radius: 3px; }
  .state { padding: 24px; display: grid; gap: 10px; }
  .error { color: #d42054; }
  button, input, select { font: inherit; }
  button { cursor: pointer; }
  button:disabled { cursor: default; opacity: .45; }
  .breadcrumbs { align-items: center; color: #777; display: flex; flex-wrap: wrap; font-size: 13px; gap: 8px; margin-bottom: 18px; }
  .breadcrumbs a { color: #2152a3; text-decoration: none; }
  .breadcrumbs a:hover { text-decoration: underline; }
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
  .bulk-bar { align-items: center; background: #1b264f; border-radius: 3px; color: #fff; display: flex; justify-content: space-between; margin-bottom: 18px; padding: 12px 14px; }
  .bulk-bar button { background: transparent; border: 1px solid rgba(255,255,255,.75); border-radius: 3px; color: #fff; margin-left: 8px; padding: 6px 10px; }
  .bulk-bar .clear { border: 0; }
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
customElements.define("ekom-catalog-collection-view", q);
export {
  q as default
};
