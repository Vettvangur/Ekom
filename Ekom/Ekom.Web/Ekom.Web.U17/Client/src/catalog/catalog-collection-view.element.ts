import { UMB_COLLECTION_CONTEXT } from '@umbraco-cms/backoffice/collection';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import '@umbraco-cms/backoffice/imaging';

type CatalogNode = {
  id: number;
  key: string;
  name: string;
  title: string;
  contentTypeAlias: string;
  productCount: number;
  subcategoryCount: number;
};

type CatalogProduct = CatalogNode & {
  sku: string;
  price: string;
  status: 'Published' | 'Pending changes' | 'Unpublished';
  published: boolean;
  pendingChanges: boolean;
  available: boolean;
  image: string;
};

type CatalogResponse = {
  current: CatalogNode;
  parent?: CatalogNode;
  breadcrumbs: CatalogNode[];
  subcategories: CatalogNode[];
  products: CatalogProduct[];
  productCount: number;
  subcategoryCount: number;
  filteredProductCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

const DEFAULT_PAGE_SIZE = 16;
const DEFAULT_SORT = 'sortOrderAsc';
const CATALOG_NODE_QUERY = 'ekomCatalogNode';
const GRID_GAP = 14;
const GRID_MIN_CARD_WIDTH = 220;
const MAX_PRODUCT_ROWS = 4;
const PARENT_STORAGE_PREFIX = 'ekomCatalogParent:';
const SORT_STORAGE_KEY = 'ekomCatalogSort';

class EkomCatalogCollectionViewElement extends UmbElementMixin(HTMLElement) {
  private nodeId = '';
  private query = '';
  private sort = getStoredSort();
  private page = 1;
  private pageSize = DEFAULT_PAGE_SIZE;
  private loading = true;
  private error = '';
  private data?: CatalogResponse;
  private revealTimer?: number;
  private resizeTimer?: number;
  private collectionPaginationObserver?: MutationObserver;
  private readonly selectedProductKeys = new Set<string>();
  private readonly onPopState = (): void => this.restoreNodeFromHistory();
  private readonly onResize = (): void => {
    if (this.resizeTimer != null) {
      window.clearTimeout(this.resizeTimer);
    }

    this.resizeTimer = window.setTimeout(() => {
      if (this.updatePageSizeForViewport() && this.nodeId && this.data != null && !this.loading) {
        void this.load(false);
      }
    }, 150);
  };
  override connectedCallback(): void {
    super.connectedCallback();
    this.style.setProperty('display', 'block', 'important');
    this.style.setProperty('visibility', 'visible', 'important');
    window.addEventListener('popstate', this.onPopState);
    window.addEventListener('resize', this.onResize);
    this.revealCollectionHost();
    this.revealTimer = window.setTimeout(() => this.revealCollectionHost(), 250);

    this.nodeId = this.getNodeIdFromUrl();
    this.sort = getStoredSort();

    this.consumeContext(UMB_COLLECTION_CONTEXT, context => {
      const unique = context?.getConfig()?.unique;
      if (this.getCatalogNodeFromUrl() == null && unique != null && unique !== this.nodeId) {
        this.nodeId = unique;
        void this.load();
      }

      this.observe(context?.filter, filter => this.setQueryFromCollectionFilter(filter), 'ekomCatalogCollectionFilter');
    });

    this.render();

    if (this.nodeId) {
      void this.load();
    }
  }

  override disconnectedCallback(): void {
    window.removeEventListener('popstate', this.onPopState);
    window.removeEventListener('resize', this.onResize);
    if (this.revealTimer != null) {
      window.clearTimeout(this.revealTimer);
    }
    if (this.resizeTimer != null) {
      window.clearTimeout(this.resizeTimer);
    }
    this.collectionPaginationObserver?.disconnect();
    this.collectionPaginationObserver = undefined;

    super.disconnectedCallback();
  }

  private async load(showLoading = true): Promise<void> {
    if (!this.nodeId) {
      this.loading = false;
      this.error = 'Unable to determine the current catalog node.';
      this.render();
      return;
    }

    this.loading = showLoading;
    this.error = '';
    this.updatePageSizeForViewport();
    if (showLoading) {
      this.render();
    }

    try {
      const params = new URLSearchParams({
        query: this.query,
        sort: this.sort,
        page: String(this.page),
        pageSize: String(this.pageSize),
      });
      this.data = await this.fetchJson<CatalogResponse>(`/ekom/backoffice/CatalogCollection/${encodeURIComponent(this.nodeId)}?${params}`);
      this.page = this.data.page;
      this.pageSize = this.data.pageSize;
      this.storeParentNode(this.data);
    } catch (error) {
      if (error instanceof CatalogRequestError && error.status === 404 && this.redirectToParent()) {
        return;
      }

      this.error = error instanceof Error ? error.message : 'Unable to load catalog.';
    } finally {
      this.loading = false;
      this.render();
    }
  }

  private drillTo(node: CatalogNode): void {
    window.location.assign(this.getDocumentHref(node.key || String(node.id)));
  }

  private restoreNodeFromHistory(): void {
    const nodeId = this.getNodeIdFromUrl();
    if (!nodeId || nodeId === this.nodeId) {
      return;
    }

    this.nodeId = nodeId;
    this.query = '';
    this.page = 1;
    this.selectedProductKeys.clear();
    void this.load();
  }

  private storeParentNode(data: CatalogResponse): void {
    const key = this.getParentStorageKey(data.current.key || String(data.current.id));
    if (data.parent?.key) {
      window.sessionStorage.setItem(key, data.parent.key);
    } else {
      window.sessionStorage.removeItem(key);
    }
  }

  private redirectToParent(): boolean {
    const parentId = this.data?.parent?.key ?? window.sessionStorage.getItem(this.getParentStorageKey(this.nodeId));
    if (!parentId) {
      return false;
    }

    window.location.replace(this.getDocumentHref(parentId));
    return true;
  }

  private getParentStorageKey(nodeId: string): string {
    return `${PARENT_STORAGE_PREFIX}${nodeId}`;
  }

  private setQueryFromCollectionFilter(value: object | undefined): void {
    const filter = 'filter' in (value ?? {}) ? (value as { filter?: unknown }).filter : undefined;
    const query = typeof filter === 'string' ? filter : '';
    if (query === this.query) {
      return;
    }

    this.query = query;
    this.page = 1;
    if (this.nodeId) {
      void this.load();
    }
  }

  private setSort(value: string): void {
    if (!isSortOption(value)) {
      return;
    }

    if (value === this.sort) {
      setStoredSort(value);
      return;
    }

    this.sort = value;
    setStoredSort(value);
    this.page = 1;
    void this.load();
  }

  private setPage(page: number): void {
    if (page < 1 || page === this.page || (this.data != null && page > this.data.totalPages)) {
      return;
    }

    this.page = page;
    void this.load();
  }

  private updatePageSizeForViewport(): boolean {
    const width = Math.max(0, this.clientWidth - 48);
    if (width === 0) {
      return false;
    }

    const columns = Math.max(1, Math.floor((width + GRID_GAP) / (GRID_MIN_CARD_WIDTH + GRID_GAP)));
    const pageSize = columns * MAX_PRODUCT_ROWS;
    if (pageSize === this.pageSize) {
      return false;
    }

    this.pageSize = pageSize;
    return true;
  }

  private toggleProduct(product: CatalogProduct): void {
    if (this.selectedProductKeys.has(product.key)) {
      this.selectedProductKeys.delete(product.key);
    } else {
      this.selectedProductKeys.add(product.key);
    }

    this.render();
  }

  private toggleCurrentPage(): void {
    const products = this.data?.products ?? [];
    const allSelected = products.length > 0 && products.every(product => this.selectedProductKeys.has(product.key));

    for (const product of products) {
      if (allSelected) {
        this.selectedProductKeys.delete(product.key);
      } else {
        this.selectedProductKeys.add(product.key);
      }
    }

    this.render();
  }

  private clearSelection(): void {
    this.selectedProductKeys.clear();
    this.render();
  }

  private revealCollectionHost(): void {
    for (const collection of this.getComposedAncestors('umb-collection-default')) {
      const collectionRoot = collection.shadowRoot;
      const router = collectionRoot?.querySelector<HTMLElement>('#router');
      const bodyLayout = collectionRoot?.querySelector<HTMLElement>('umb-body-layout');
      const emptyState = collectionRoot?.querySelector<HTMLElement>('#empty-state');

      router?.style.setProperty('visibility', 'visible', 'important');
      emptyState?.style.setProperty('display', 'none', 'important');
      bodyLayout?.classList.add('has-items');
    }

    for (const collectionRoot of this.getComposedShadowRoots()) {
      this.hideDefaultCollectionPagination(collectionRoot);
      this.observeDefaultCollectionPagination(collectionRoot);
    }
  }

  private hideDefaultCollectionPagination(collectionRoot: ShadowRoot): void {
    collectionRoot.querySelector<HTMLElement>('umb-collection-pagination')?.style.setProperty('display', 'none', 'important');
  }

  private observeDefaultCollectionPagination(collectionRoot: ShadowRoot): void {
    if (this.collectionPaginationObserver == null) {
      this.collectionPaginationObserver = new MutationObserver(() => this.revealCollectionHost());
    }

    this.collectionPaginationObserver.observe(collectionRoot, { childList: true, subtree: true });
  }

  private getComposedAncestors(selector: string): HTMLElement[] {
    const ancestors: HTMLElement[] = [];
    let node: Node | null = this;

    while (node != null) {
      if (node instanceof HTMLElement && node.matches(selector)) {
        ancestors.push(node);
      }

      node = node.parentNode ?? (node.getRootNode() instanceof ShadowRoot ? (node.getRootNode() as ShadowRoot).host : null);
    }

    return ancestors;
  }

  private getComposedShadowRoots(): ShadowRoot[] {
    const roots = new Set<ShadowRoot>();
    let node: Node | null = this;

    while (node != null) {
      const root = node.getRootNode();
      if (root instanceof ShadowRoot) {
        roots.add(root);
      }

      node = node.parentNode ?? (root instanceof ShadowRoot ? root.host : null);
    }

    return [...roots];
  }

  private render(): void {
    this.innerHTML = `
      <style>${styles}</style>
      <div class="catalog-shell">
        ${this.renderContent()}
      </div>
    `;
    this.revealCollectionHost();
    this.bindEvents();
  }

  private renderContent(): string {
    if (this.loading) {
      return '<div class="surface state">Loading catalog…</div>';
    }

    if (this.error) {
      return `<div class="surface state error"><strong>Catalog unavailable</strong><span>${escapeHtml(this.error)}</span><button type="button" data-action="retry">Retry</button></div>`;
    }

    if (this.data == null) {
      return '<div class="surface state">No catalog data.</div>';
    }

    const selectedCount = this.selectedProductKeys.size;
    const isEmptyCategory = !this.query && this.data.productCount === 0 && this.data.subcategoryCount === 0;

    if (isEmptyCategory) {
      return `
        ${this.renderBreadcrumbs(this.data)}
        ${this.renderHeader(this.data)}
        ${this.renderEmptyCategory()}
      `;
    }

    return `
      ${this.renderBreadcrumbs(this.data)}
      ${this.renderHeader(this.data)}
      ${this.renderToolbar(this.data)}
      ${selectedCount > 0 ? this.renderBulkBar(selectedCount) : ''}
      ${!this.query && this.data.subcategories.length > 0 ? this.renderSubcategories(this.data.subcategories) : ''}
      ${this.renderProducts(this.data)}
      ${this.renderPagination(this.data)}
    `;
  }

  private renderBreadcrumbs(data: CatalogResponse): string {
    return `<nav class="breadcrumbs">${data.breadcrumbs.map((item, index) => {
      const isCurrent = index === data.breadcrumbs.length - 1;
      const label = escapeHtml(item.title || item.name);
      return `${index > 0 ? '<span class="sep">/</span>' : ''}${isCurrent ? `<span>${label}</span>` : `<a href="${this.getDocumentHref(item.key)}">${label}</a>`}`;
    }).join('')}</nav>`;
  }

  private renderHeader(data: CatalogResponse): string {
    return `
      <header class="catalog-header">
        <button type="button" class="back" data-action="back" ${data.parent == null ? 'disabled' : ''}>←</button>
        <div>
          <h1>${escapeHtml(data.current.title || data.current.name)}</h1>
          <p>${data.productCount} products · ${data.subcategoryCount} subcategories</p>
        </div>
      </header>
    `;
  }

  private renderToolbar(data: CatalogResponse): string {
    const currentPageSelected = data.products.length > 0 && data.products.every(product => this.selectedProductKeys.has(product.key));

    return `
      <div class="toolbar">
        <select data-field="sort">
          ${this.renderSortOption('sortOrderAsc', 'Sort order asc')}
          ${this.renderSortOption('sortOrderDesc', 'Sort order desc')}
          ${this.renderSortOption('nameAsc', 'Name asc')}
          ${this.renderSortOption('nameDesc', 'Name desc')}
          ${this.renderSortOption('priceAsc', 'Price asc')}
          ${this.renderSortOption('priceDesc', 'Price desc')}
          ${this.renderSortOption('skuAsc', 'SKU asc')}
          ${this.renderSortOption('skuDesc', 'SKU desc')}
          ${this.renderSortOption('createdAsc', 'Created asc')}
          ${this.renderSortOption('createdDesc', 'Created desc')}
          ${this.renderSortOption('updatedAsc', 'Updated asc')}
          ${this.renderSortOption('updatedDesc', 'Updated desc')}
        </select>
        <button type="button" class="select-all" data-action="toggle-page"><span class="fake-checkbox ${currentPageSelected ? 'checked' : ''}"></span>Select all</button>
      </div>
    `;
  }

  private renderSortOption(value: string, label: string): string {
    return `<option value="${value}" ${this.sort === value ? 'selected' : ''}>${label}</option>`;
  }

  private renderBulkBar(selectedCount: number): string {
    return `
      <div class="bulk-bar">
        <strong>${selectedCount} selected</strong>
        <div>
          <button type="button" disabled>Publish</button>
          <button type="button" disabled>Unpublish</button>
          <button type="button" disabled>Move</button>
          <button type="button" class="clear" data-action="clear-selection">Clear ✕</button>
        </div>
      </div>
    `;
  }

  private renderSubcategories(subcategories: CatalogNode[]): string {
    return `
      <section class="section">
        <h2>Subcategories</h2>
        <div class="chips">${subcategories.map(category => `
          <a class="chip" href="${this.getDocumentHref(category.key)}">
            <span class="tile">▦</span><span class="chip-text"><strong>${escapeHtml(category.title || category.name)}</strong><small>${category.productCount} products · ${category.subcategoryCount} subcategories</small></span><span>›</span>
          </a>
        `).join('')}</div>
      </section>
    `;
  }

  private renderEmptyCategory(): string {
    return `
      <section class="section">
        <div class="empty empty-category">
          <strong>No catalog items in this category yet</strong>
          <span>This category does not contain any direct products or subcategories.</span>
        </div>
      </section>
    `;
  }

  private renderProducts(data: CatalogResponse): string {
    const empty = this.query ? `No products match &quot;${escapeHtml(this.query)}&quot;` : 'No products in this category yet';

    return `
      <section class="section">
        <h2>Products (${data.filteredProductCount})</h2>
        ${data.products.length === 0 ? `<div class="empty">${empty}</div>` : `<div class="grid">${data.products.map(product => this.renderProduct(product)).join('')}</div>`}
      </section>
    `;
  }

  private renderProduct(product: CatalogProduct): string {
    const selected = this.selectedProductKeys.has(product.key);
    const href = `/umbraco/section/content/workspace/document/edit/${product.key}`;

    return `
      <article class="card ${selected ? 'selected' : ''}">
        <button type="button" class="checkbox ${selected ? 'checked' : ''}" data-product-key="${product.key}">${selected ? '✓' : ''}</button>
        <a class="image ${!product.published ? 'dimmed' : ''}" href="${href}">${product.image ? `<umb-imaging-thumbnail unique="${escapeHtml(product.image)}" width="320" height="160" alt="${escapeHtml(product.title || product.name)}"></umb-imaging-thumbnail>` : '<span>Product image</span>'}</a>
        <div class="card-body">
          <a class="title" href="${href}">${escapeHtml(product.title || product.name)}</a>
          <div class="meta"><span>${product.sku ? `SKU ${escapeHtml(product.sku)}` : 'No SKU'}</span><strong>${escapeHtml(product.price)}</strong></div>
          <div class="status-row"><span class="pill ${statusClass(product.status)}"><i></i>${product.status}</span><strong class="availability ${product.available ? 'ok' : 'bad'}">${product.available ? '✓ Available' : '✕ Unavailable'}</strong></div>
        </div>
      </article>
    `;
  }

  private renderPagination(data: CatalogResponse): string {
    if (data.filteredProductCount === 0 || data.totalPages <= 1) {
      return '';
    }

    const from = (data.page - 1) * data.pageSize + 1;
    const to = Math.min(data.page * data.pageSize, data.filteredProductCount);

    return `
      <footer class="pagination">
        <div>
          <button type="button" data-page="${data.page - 1}" ${data.page <= 1 ? 'disabled' : ''}>‹ Prev</button>
          ${this.paginationItems(data.page, data.totalPages).map(item => item === '…' ? '<span>…</span>' : `<button type="button" class="${item === data.page ? 'current' : ''}" data-page="${item}">${item}</button>`).join('')}
          <button type="button" data-page="${data.page + 1}" ${data.page >= data.totalPages ? 'disabled' : ''}>Next ›</button>
        </div>
        <span>${from}–${to} of ${data.filteredProductCount}</span>
      </footer>
    `;
  }

  private paginationItems(page: number, total: number): Array<number | '…'> {
    if (total <= 7) {
      return Array.from({ length: total }, (_, index) => index + 1);
    }

    const items = new Set([1, total, page - 1, page, page + 1]);
    const sorted = [...items].filter(item => item >= 1 && item <= total).sort((a, b) => a - b);
    const result: Array<number | '…'> = [];

    for (const item of sorted) {
      const previous = result[result.length - 1];
      if (typeof previous === 'number' && item - previous > 1) {
        result.push('…');
      }

      result.push(item);
    }

    return result;
  }

  private bindEvents(): void {
    this.querySelector('[data-action="retry"]')?.addEventListener('click', () => void this.load());
    this.querySelector('[data-action="back"]')?.addEventListener('click', () => {
      if (this.data?.parent != null) {
        this.drillTo(this.data.parent);
      }
    });
    this.querySelector('[data-action="toggle-page"]')?.addEventListener('click', () => this.toggleCurrentPage());
    this.querySelector('[data-action="clear-selection"]')?.addEventListener('click', () => this.clearSelection());
    this.querySelector('[data-field="sort"]')?.addEventListener('input', event => this.setSort((event.target as HTMLSelectElement).value));
    this.querySelector('[data-field="sort"]')?.addEventListener('change', event => this.setSort((event.target as HTMLSelectElement).value));
    this.querySelectorAll<HTMLElement>('[data-product-key]').forEach(element => {
      element.addEventListener('click', event => {
        event.preventDefault();
        const key = element.dataset.productKey;
        const product = this.data?.products.find(item => item.key === key);
        if (product != null) {
          this.toggleProduct(product);
        }
      });
    });
    this.querySelectorAll<HTMLElement>('[data-page]').forEach(element => {
      element.addEventListener('click', () => this.setPage(Number(element.dataset.page)));
    });
  }

  private getNodeIdFromUrl(): string {
    const catalogNode = this.getCatalogNodeFromUrl();
    if (catalogNode != null) {
      return catalogNode;
    }

    const href = window.location.href;
    const guidMatch = href.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);

    if (guidMatch != null) {
      return guidMatch[0];
    }

    const editSegmentMatch = href.match(/\/edit\/([^/?#]+)/i);
    return editSegmentMatch?.[1] ?? '';
  }

  private getDocumentHref(nodeId: string): string {
    const url = new URL(window.location.href);
    url.searchParams.delete(CATALOG_NODE_QUERY);

    if (url.pathname.match(/\/edit\/[^/]+/i) != null) {
      url.pathname = url.pathname.replace(/\/edit\/[^/]+/i, `/edit/${nodeId}`);
      return `${url.pathname}${url.search}${url.hash}`;
    }

    return `/umbraco/section/content/workspace/document/edit/${nodeId}`;
  }

  private getCatalogNodeFromUrl(): string | null {
    const value = new URL(window.location.href).searchParams.get(CATALOG_NODE_QUERY);
    return value && value.trim().length > 0 ? value : null;
  }

  private async fetchJson<T>(url: string): Promise<T> {
    const response = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
    if (!response.ok) {
      throw new CatalogRequestError(await response.text() || `Request failed (${response.status}).`, response.status);
    }

    return await response.json() as T;
  }
}

class CatalogRequestError extends Error {
  constructor(message: string, public readonly status: number) {
    super(message);
  }
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"]/g, char => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[char] ?? char);
}

function statusClass(status: CatalogProduct['status']): string {
  if (status === 'Published') {
    return 'published';
  }

  return status === 'Pending changes' ? 'pending' : 'unpublished';
}

function isSortOption(value: string): boolean {
  return [
    'sortOrderAsc',
    'sortOrderDesc',
    'nameAsc',
    'nameDesc',
    'priceAsc',
    'priceDesc',
    'skuAsc',
    'skuDesc',
    'createdAsc',
    'createdDesc',
    'updatedAsc',
    'updatedDesc',
  ].includes(value);
}

function getStoredSort(): string {
  try {
    const value = window.localStorage.getItem(SORT_STORAGE_KEY);
    return value != null && isSortOption(value) ? value : DEFAULT_SORT;
  } catch {
    return DEFAULT_SORT;
  }
}

function setStoredSort(value: string): void {
  try {
    window.localStorage.setItem(SORT_STORAGE_KEY, value);
  } catch {
    // Ignore storage failures and keep the in-memory value for this view.
  }
}

const styles = `
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

customElements.define('ekom-catalog-collection-view', EkomCatalogCollectionViewElement);

export default EkomCatalogCollectionViewElement;
