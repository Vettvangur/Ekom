var $ = Object.defineProperty;
var P = (s, l, e) => l in s ? $(s, l, { enumerable: !0, configurable: !0, writable: !0, value: e }) : s[l] = e;
var n = (s, l, e) => P(s, typeof l != "symbol" ? l + "" : l, e);
import { UMB_COLLECTION_CONTEXT as C } from "@umbraco-cms/backoffice/collection";
import { UmbElementMixin as E } from "@umbraco-cms/backoffice/element-api";
import "@umbraco-cms/backoffice/imaging";
const I = 16, u = "sortOrderAsc", g = "ekomCatalogNode", f = 14, T = 220, z = 4, O = "ekomCatalogParent:", v = "ekomCatalogSort", b = "ekomCatalog";
class A extends E(HTMLElement) {
  constructor() {
    super(...arguments);
    n(this, "nodeId", "");
    n(this, "query", "");
    n(this, "sort", y());
    n(this, "page", 1);
    n(this, "pageSize", I);
    n(this, "loading", !0);
    n(this, "error", "");
    n(this, "data");
    n(this, "revealTimer");
    n(this, "resizeTimer");
    n(this, "collectionPaginationObserver");
    n(this, "selection");
    n(this, "onPopState", () => this.restoreNodeFromHistory());
    n(this, "onResize", () => {
      this.resizeTimer != null && window.clearTimeout(this.resizeTimer), this.resizeTimer = window.setTimeout(() => {
        this.updatePageSizeForViewport() && this.nodeId && this.data != null && !this.loading && this.load(!1);
      }, 150);
    });
  }
  connectedCallback() {
    super.connectedCallback(), this.style.setProperty("display", "block", "important"), this.style.setProperty("visibility", "visible", "important"), window.addEventListener("popstate", this.onPopState), window.addEventListener("resize", this.onResize), this.revealCollectionHost(), this.revealTimer = window.setTimeout(() => this.revealCollectionHost(), 250), this.nodeId = this.getNodeIdFromUrl(), this.sort = y(), this.restorePageFromHistory(), this.consumeContext(C, (e) => {
      var i;
      const t = (i = e == null ? void 0 : e.getConfig()) == null ? void 0 : i.unique;
      this.getCatalogNodeFromUrl() == null && t != null && t !== this.nodeId && (this.nodeId = t, this.restorePageFromHistory(), this.load()), (e == null ? void 0 : e.selection) != null && (this.selection = e.selection, this.selection.setSelectable(!0), this.selection.setMultiple(!0), this.observe(e.selection.selection, () => this.render(), "ekomCatalogCollectionSelection")), this.observe(e == null ? void 0 : e.filter, (o) => this.setQueryFromCollectionFilter(o), "ekomCatalogCollectionFilter");
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
      if (t instanceof m && t.status === 404 && this.redirectToParent())
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
    var t;
    const e = this.getNodeIdFromUrl();
    e && (this.nodeId = e, this.query = "", this.page = 1, this.restorePageFromHistory(), (t = this.selection) == null || t.clearSelection(), this.load());
  }
  storeParentNode(e) {
    var i;
    const t = this.getParentStorageKey(e.current.key || String(e.current.id));
    (i = e.parent) != null && i.key ? window.sessionStorage.setItem(t, e.parent.key) : window.sessionStorage.removeItem(t);
  }
  redirectToParent() {
    var t, i;
    const e = ((i = (t = this.data) == null ? void 0 : t.parent) == null ? void 0 : i.key) ?? window.sessionStorage.getItem(this.getParentStorageKey(this.nodeId));
    return e ? (window.location.replace(this.getDocumentHref(e)), !0) : !1;
  }
  getParentStorageKey(e) {
    return `${O}${e}`;
  }
  restorePageFromHistory() {
    const e = history.state, t = e == null ? void 0 : e[b];
    if (t == null || typeof t != "object")
      return;
    const { nodeId: i, page: o } = t;
    i === this.nodeId && typeof o == "number" && Number.isInteger(o) && o > 0 && (this.page = o);
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
    const t = "filter" in (e ?? {}) ? e.filter : void 0, i = typeof t == "string" ? t : "";
    i !== this.query && (this.query = i, this.page = 1, this.storePageInHistory(), this.nodeId && this.load());
  }
  setSort(e) {
    if (w(e)) {
      if (e === this.sort) {
        x(e);
        return;
      }
      this.sort = e, x(e), this.page = 1, this.storePageInHistory(), this.load();
    }
  }
  setPage(e) {
    e < 1 || e === this.page || this.data != null && e > this.data.totalPages || (this.page = e, this.storePageInHistory(), this.load());
  }
  updatePageSizeForViewport() {
    const e = Math.max(0, this.clientWidth - 48);
    if (e === 0)
      return !1;
    const i = Math.max(1, Math.floor((e + f) / (T + f))) * z;
    return i === this.pageSize ? !1 : (this.pageSize = i, !0);
  }
  toggleProduct(e) {
    var t;
    (t = this.selection) == null || t.toggleSelect(e.key);
  }
  toggleCurrentPage() {
    var a;
    if (this.selection == null)
      return;
    const t = (((a = this.data) == null ? void 0 : a.products) ?? []).map((r) => r.key), i = t.length > 0 && t.every((r) => this.selection.isSelected(r)), o = this.selection.getSelection();
    this.selection.setSelection(i ? o.filter((r) => !t.includes(r)) : [.../* @__PURE__ */ new Set([...o, ...t])]);
  }
  revealCollectionHost() {
    for (const e of this.getComposedAncestors("umb-collection-default")) {
      const t = e.shadowRoot, i = t == null ? void 0 : t.querySelector("#router"), o = t == null ? void 0 : t.querySelector("umb-body-layout"), a = t == null ? void 0 : t.querySelector("#empty-state");
      i == null || i.style.setProperty("visibility", "visible", "important"), a == null || a.style.setProperty("display", "none", "important"), o == null || o.classList.add("has-items");
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
    let i = this;
    for (; i != null; )
      i instanceof HTMLElement && i.matches(e) && t.push(i), i = i.parentNode ?? (i.getRootNode() instanceof ShadowRoot ? i.getRootNode().host : null);
    return t;
  }
  getComposedShadowRoots() {
    const e = /* @__PURE__ */ new Set();
    let t = this;
    for (; t != null; ) {
      const i = t.getRootNode();
      i instanceof ShadowRoot && e.add(i), t = t.parentNode ?? (i instanceof ShadowRoot ? i.host : null);
    }
    return [...e];
  }
  render() {
    this.innerHTML = `
      <style>${q}</style>
      <div class="catalog-shell">
        ${this.renderContent()}
      </div>
    `, this.revealCollectionHost(), this.bindEvents();
  }
  renderContent() {
    return this.loading ? '<div class="surface state">Loading catalog…</div>' : this.error ? `<div class="surface state error"><strong>Catalog unavailable</strong><span>${d(this.error)}</span><button type="button" data-action="retry">Retry</button></div>` : this.data == null ? '<div class="surface state">No catalog data.</div>' : !this.query && this.data.productCount === 0 && this.data.subcategoryCount === 0 ? `
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
  renderBreadcrumbs(e) {
    return `<nav class="breadcrumbs">${e.breadcrumbs.map((t, i) => {
      const o = i === e.breadcrumbs.length - 1, a = d(t.title || t.name);
      return `${i > 0 ? '<span class="sep">/</span>' : ""}${o ? `<span>${a}</span>` : `<a href="${this.getDocumentHref(t.key)}">${a}</a>`}`;
    }).join("")}</nav>`;
  }
  renderHeader(e) {
    return `
      <header class="catalog-header">
        <button type="button" class="back" data-action="back" ${e.parent == null ? "disabled" : ""}>←</button>
        <div>
          <h1>${d(e.current.title || e.current.name)}</h1>
          <p>${e.productCount} products · ${e.subcategoryCount} subcategories</p>
        </div>
      </header>
    `;
  }
  renderToolbar(e) {
    const t = e.products.length > 0 && e.products.every((i) => {
      var o;
      return (o = this.selection) == null ? void 0 : o.isSelected(i.key);
    });
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
  renderSubcategories(e) {
    return `
      <section class="section">
        <h2>Subcategories</h2>
        <div class="chips">${e.map((t) => `
          <a class="chip" href="${this.getDocumentHref(t.key)}">
            <span class="tile">▦</span><span class="chip-text"><strong>${d(t.title || t.name)}</strong><small>${t.productCount} products · ${t.subcategoryCount} subcategories</small></span><span>›</span>
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
    const t = this.query ? `No products match &quot;${d(this.query)}&quot;` : "No products in this category yet";
    return `
      <section class="section">
        <h2>Products (${e.filteredProductCount})</h2>
        ${e.products.length === 0 ? `<div class="empty">${t}</div>` : `<div class="grid">${e.products.map((i) => this.renderProduct(i)).join("")}</div>`}
      </section>
    `;
  }
  renderProduct(e) {
    var o;
    const t = ((o = this.selection) == null ? void 0 : o.isSelected(e.key)) ?? !1, i = `/umbraco/section/content/workspace/document/edit/${e.key}`;
    return `
      <article class="card ${t ? "selected" : ""}">
        <button type="button" class="checkbox ${t ? "checked" : ""}" data-product-key="${e.key}">${t ? "✓" : ""}</button>
        <a class="image ${e.published ? "" : "dimmed"}" href="${i}">${e.image ? `<umb-imaging-thumbnail unique="${d(e.image)}" width="320" height="160" alt="${d(e.title || e.name)}"></umb-imaging-thumbnail>` : "<span>Product image</span>"}</a>
        <div class="card-body">
          <a class="title" href="${i}">${d(e.title || e.name)}</a>
          <div class="meta"><span>${e.sku ? `SKU ${d(e.sku)}` : "No SKU"}</span><strong>${d(e.price)}</strong></div>
          <div class="status-row"><span class="pill ${N(e.status)}"><i></i>${e.status}</span><strong class="availability ${e.available ? "ok" : "bad"}">${e.available ? "✓ Available" : "✕ Unavailable"}</strong></div>
        </div>
      </article>
    `;
  }
  renderPagination(e) {
    if (e.filteredProductCount === 0 || e.totalPages <= 1)
      return "";
    const t = (e.page - 1) * e.pageSize + 1, i = Math.min(e.page * e.pageSize, e.filteredProductCount);
    return `
      <footer class="pagination">
        <div>
          <button type="button" data-page="${e.page - 1}" ${e.page <= 1 ? "disabled" : ""}>‹ Prev</button>
          ${this.paginationItems(e.page, e.totalPages).map((o) => o === "…" ? "<span>…</span>" : `<button type="button" class="${o === e.page ? "current" : ""}" data-page="${o}">${o}</button>`).join("")}
          <button type="button" data-page="${e.page + 1}" ${e.page >= e.totalPages ? "disabled" : ""}>Next ›</button>
        </div>
        <span>${t}–${i} of ${e.filteredProductCount}</span>
      </footer>
    `;
  }
  paginationItems(e, t) {
    if (t <= 7)
      return Array.from({ length: t }, (r, c) => c + 1);
    const o = [.../* @__PURE__ */ new Set([1, t, e - 1, e, e + 1])].filter((r) => r >= 1 && r <= t).sort((r, c) => r - c), a = [];
    for (const r of o) {
      const c = a[a.length - 1];
      typeof c == "number" && r - c > 1 && a.push("…"), a.push(r);
    }
    return a;
  }
  bindEvents() {
    var e, t, i, o, a;
    (e = this.querySelector('[data-action="retry"]')) == null || e.addEventListener("click", () => void this.load()), (t = this.querySelector('[data-action="back"]')) == null || t.addEventListener("click", () => {
      var r;
      ((r = this.data) == null ? void 0 : r.parent) != null && this.drillTo(this.data.parent);
    }), (i = this.querySelector('[data-action="toggle-page"]')) == null || i.addEventListener("click", () => this.toggleCurrentPage()), (o = this.querySelector('[data-field="sort"]')) == null || o.addEventListener("input", (r) => this.setSort(r.target.value)), (a = this.querySelector('[data-field="sort"]')) == null || a.addEventListener("change", (r) => this.setSort(r.target.value)), this.querySelectorAll("[data-product-key]").forEach((r) => {
      r.addEventListener("click", (c) => {
        var h;
        c.preventDefault();
        const S = r.dataset.productKey, p = (h = this.data) == null ? void 0 : h.products.find((k) => k.key === S);
        p != null && this.toggleProduct(p);
      });
    }), this.querySelectorAll("[data-page]").forEach((r) => {
      r.addEventListener("click", () => this.setPage(Number(r.dataset.page)));
    });
  }
  getNodeIdFromUrl() {
    const e = this.getCatalogNodeFromUrl();
    if (e != null)
      return e;
    const t = window.location.href, i = t.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);
    if (i != null)
      return i[0];
    const o = t.match(/\/edit\/([^/?#]+)/i);
    return (o == null ? void 0 : o[1]) ?? "";
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
      throw new m(await t.text() || `Request failed (${t.status}).`, t.status);
    return await t.json();
  }
}
class m extends Error {
  constructor(l, e) {
    super(l), this.status = e;
  }
}
function d(s) {
  return s.replace(/[&<>"]/g, (l) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;" })[l] ?? l);
}
function N(s) {
  return s === "Published" ? "published" : s === "Pending changes" ? "pending" : "unpublished";
}
function w(s) {
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
  ].includes(s);
}
function y() {
  try {
    const s = window.localStorage.getItem(v);
    return s != null && w(s) ? s : u;
  } catch {
    return u;
  }
}
function x(s) {
  try {
    window.localStorage.setItem(v, s);
  } catch {
  }
}
const q = `
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
