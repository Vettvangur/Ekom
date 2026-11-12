import { UMB_COLLECTION_CONTEXT } from '@umbraco-cms/backoffice/collection';
import { UmbEntityContext } from '@umbraco-cms/backoffice/entity';
import { UmbExtensionElementInitializer } from '@umbraco-cms/backoffice/extension-api';
import { umbExtensionsRegistry } from '@umbraco-cms/backoffice/extension-registry';
import { html, nothing, repeat, svg } from '@umbraco-cms/backoffice/external/lit';
import type { TemplateResult } from '@umbraco-cms/backoffice/external/lit';
import { UmbImagingRepository } from '@umbraco-cms/backoffice/imaging';
import type { UmbImagingCropMode } from '@umbraco-cms/backoffice/imaging';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import { UMB_THEME_CONTEXT } from '@umbraco-cms/backoffice/themes';
import { isCatalogPicker } from './catalog-picker-context';
import { catalogStyles } from './catalog-styles';
import {
  DEFAULT_SORT,
  SORT_OPTIONS,
  STATUS_CHIPS,
  healthLabel,
  isSortOption,
  metaLine,
  paginationItems,
  plural,
  publishLabel,
  sortLabel,
  stockLabel,
  themeFromAlias,
  toggleAll,
} from './catalog-view-model';
import type { CatalogBreadcrumb, CatalogNode, CatalogProduct, CatalogResponse, CatalogStatus } from './catalog-view-model';

type CatalogSelection = {
  clearSelection(): void;
  getSelection(): string[];
  setMultiple(value: boolean): void;
  setSelectable(value: boolean): void;
  setSelection(selection: string[]): void;
  toggleSelect(key: string): void;
};

const DEFAULT_PAGE_SIZE = 16;
const CATALOG_NODE_QUERY = 'ekomCatalogNode';
const MAX_PRODUCT_ROWS = 4;
const NARROW_QUERY = '(max-width: 1100px)';
const PARENT_STORAGE_PREFIX = 'ekomCatalogParent:';
const SORT_STORAGE_KEY = 'ekomCatalogSort';
const HISTORY_STATE_KEY = 'ekomCatalog';
const THUMBNAIL_SIZE = 480;
const DOCUMENT_ENTITY_TYPE = 'document';

const folderIcon = svg`<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 7a2 2 0 0 1 2-2h4l2 2.5h8a2 2 0 0 1 2 2V17a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/></svg>`;
const imageIcon = svg`<svg width="44" height="44" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="2.5"/><circle cx="9" cy="9" r="1.8"/><path d="m21 15-4.6-4.6a1.5 1.5 0 0 0-2.1 0L5 20"/></svg>`;

/** Gives the card's entity actions the document they act on. */
class EkomCatalogEntityElement extends UmbLitElement {
  private readonly entityContext = new UmbEntityContext(this);

  set unique(value: string) {
    this.entityContext.setEntityType(DOCUMENT_ENTITY_TYPE);
    this.entityContext.setUnique(value);
  }

  override render(): TemplateResult {
    return html`<slot></slot>`;
  }
}

class EkomCatalogCollectionViewElement extends UmbLitElement {
  static override styles = [catalogStyles];

  private pickerMode = false;
  private initialized = false;
  private pickerView?: UmbExtensionElementInitializer;
  private nodeId = '';
  private query = '';
  private sort = getStoredSort();
  private status: CatalogStatus = 'all';
  private page = 1;
  private pageSize = DEFAULT_PAGE_SIZE;
  private loading = true;
  private busy = false;
  private error = '';
  private data?: CatalogResponse;
  private openMenu?: string;
  private selected = new Set<string>();
  private requestId = 0;
  private request?: AbortController;
  private revealTimer?: number;
  private resizeTimer?: number;
  private collectionPaginationObserver?: MutationObserver;
  private selection?: CatalogSelection;
  private readonly imaging = new UmbImagingRepository(this);
  private readonly thumbnails = new Map<string, string>();
  private readonly onPopState = (): void => this.restoreNodeFromHistory();
  private readonly onDocumentClick = (): void => this.setOpenMenu(undefined);
  private readonly onKeyDown = (event: KeyboardEvent): void => {
    if (event.key === 'Escape') {
      this.setOpenMenu(undefined);
    }
  };
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

  constructor() {
    super();
    this.consumeContext(UMB_THEME_CONTEXT, context => {
      this.observe(context?.theme, alias => this.setAttribute('data-theme', themeFromAlias(alias)), 'ekomCatalogTheme');
    });
  }

  override connectedCallback(): void {
    super.connectedCallback();
    this.style.setProperty('display', 'block', 'important');
    this.style.setProperty('visibility', 'visible', 'important');
    // Wait for the local collection before inspecting the URL. In a picker the URL
    // belongs to the document behind the modal, not the category being browsed.
    this.consumeContext(UMB_COLLECTION_CONTEXT, context => {
      if (context == null) return;

      this.pickerMode = isCatalogPicker(context.getConfig());
      if (this.pickerMode) {
        this.initializePickerView();
        this.requestUpdate();
        return;
      }

      if (!this.initialized) {
        this.initialized = true;
        window.addEventListener('popstate', this.onPopState);
        window.addEventListener('resize', this.onResize);
        document.addEventListener('click', this.onDocumentClick);
        document.addEventListener('keydown', this.onKeyDown);
        this.revealCollectionHost();
        this.revealTimer = window.setTimeout(() => this.revealCollectionHost(), 250);
        this.nodeId = this.getNodeIdFromUrl();
        this.sort = getStoredSort();
        this.restorePageFromHistory();
      }

      const unique = context?.getConfig()?.unique;
      if (this.getCatalogNodeFromUrl() == null && unique != null && unique !== this.nodeId) {
        this.nodeId = unique;
        this.restorePageFromHistory();
      }

      if (context?.selection != null) {
        // Selection belongs to Umbraco's collection: its selection bar and bulk actions
        // react to whatever is written here.
        this.selection = context.selection as unknown as CatalogSelection;
        this.selection.setSelectable(true);
        this.selection.setMultiple(true);
        this.observe(context.selection.selection, keys => {
          this.selected = new Set((keys ?? []).filter((key): key is string => typeof key === 'string'));
          this.requestUpdate();
        }, 'ekomCatalogCollectionSelection');
      }

      this.observe(context?.filter, filter => this.setQueryFromCollectionFilter(filter), 'ekomCatalogCollectionFilter');
      this.requestUpdate();
      if (this.nodeId) {
        void this.load();
      }
    });
  }

  private initializePickerView(): void {
    if (this.pickerView != null) return;

    // Use the host collection's native data, selection constraints and navigation.
    // Do not use the catalog endpoint or change the picker's selection manager.
    this.pickerView = new UmbExtensionElementInitializer(
      this,
      umbExtensionsRegistry,
      'Umb.CollectionView.Document.Table',
      (permitted, controller) => {
        if (permitted && controller.component != null) {
          this.replaceChildren(controller.component);
        } else {
          this.textContent = 'The Umbraco document table view is unavailable.';
        }
      },
    );
  }

  override disconnectedCallback(): void {
    this.pickerView?.destroy();
    this.pickerView = undefined;
    this.initialized = false;
    this.request?.abort();
    window.removeEventListener('popstate', this.onPopState);
    window.removeEventListener('resize', this.onResize);
    document.removeEventListener('click', this.onDocumentClick);
    document.removeEventListener('keydown', this.onKeyDown);
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
    if (this.pickerMode) return;
    if (!this.nodeId) {
      this.loading = false;
      this.error = 'Unable to determine the current catalog node.';
      this.requestUpdate();
      return;
    }

    // Only the newest request may update the view; older ones are cancelled.
    this.request?.abort();
    const request = new AbortController();
    const requestId = ++this.requestId;
    this.request = request;
    this.loading = showLoading;
    this.busy = !showLoading;
    this.error = '';
    this.updatePageSizeForViewport();
    this.requestUpdate();

    try {
      const params = new URLSearchParams({
        query: this.query,
        sort: this.sort,
        status: this.status,
        page: String(this.page),
        pageSize: String(this.pageSize),
      });
      const data = await this.fetchJson<CatalogResponse>(`/ekom/backoffice/CatalogCollection/${encodeURIComponent(this.nodeId)}?${params}`, request.signal);
      if (requestId !== this.requestId) return;

      this.data = data;
      this.page = data.page;
      this.pageSize = data.pageSize;
      this.storeParentNode(data);
      void this.loadThumbnails(data.products);
    } catch (error) {
      if (requestId !== this.requestId || request.signal.aborted) return;
      if (error instanceof CatalogRequestError && error.status === 404 && this.redirectToParent()) {
        return;
      }

      this.error = error instanceof Error ? error.message : 'Unable to load catalog.';
    }

    this.loading = false;
    this.busy = false;
    this.requestUpdate();
  }

  /** One request for every image on the page instead of one per card. */
  private async loadThumbnails(products: CatalogProduct[]): Promise<void> {
    const missing = [...new Set(products.map(product => product.image).filter(key => key && !this.thumbnails.has(key)))];
    if (missing.length === 0) return;

    try {
      const { data } = await this.imaging.requestResizedItems(missing, {
        width: THUMBNAIL_SIZE,
        height: THUMBNAIL_SIZE,
        mode: 'Max' as UmbImagingCropMode,
      });
      for (const item of data ?? []) {
        if (item.url) {
          this.thumbnails.set(item.unique, item.url);
        }
      }
    } catch {
      // Cards fall back to the placeholder when thumbnails cannot be resolved.
    }

    this.requestUpdate();
  }

  private drillTo(node: CatalogNode): void {
    const nodeId = node.key || String(node.id);
    const state = history.state != null && typeof history.state === 'object'
      ? history.state as Record<string, unknown>
      : {};
    history.pushState({
      ...state,
      [HISTORY_STATE_KEY]: {
        nodeId,
        page: 1,
      },
    }, '', this.getDocumentHref(nodeId));

    this.nodeId = nodeId;
    this.query = '';
    this.status = 'all';
    this.page = 1;
    this.selection?.clearSelection();
    void this.load();
  }

  private restoreNodeFromHistory(): void {
    const nodeId = this.getNodeIdFromUrl();
    if (!nodeId) {
      return;
    }

    this.nodeId = nodeId;
    this.query = '';
    this.status = 'all';
    this.page = 1;
    this.restorePageFromHistory();
    this.selection?.clearSelection();
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

  private restorePageFromHistory(): void {
    const state = history.state as Record<string, unknown> | null;
    const catalogState = state?.[HISTORY_STATE_KEY];
    if (catalogState == null || typeof catalogState !== 'object') {
      return;
    }

    const { nodeId, page } = catalogState as { nodeId?: unknown; page?: unknown };
    if (nodeId === this.nodeId && typeof page === 'number' && Number.isInteger(page) && page > 0) {
      this.page = page;
    }
  }

  private storePageInHistory(): void {
    if (!this.nodeId) {
      return;
    }

    const state = history.state != null && typeof history.state === 'object'
      ? history.state as Record<string, unknown>
      : {};
    history.replaceState({
      ...state,
      [HISTORY_STATE_KEY]: {
        nodeId: this.nodeId,
        page: this.page,
      },
    }, '', window.location.href);
  }

  private setQueryFromCollectionFilter(value: object | undefined): void {
    const filter = 'filter' in (value ?? {}) ? (value as { filter?: unknown }).filter : undefined;
    const query = typeof filter === 'string' ? filter : '';
    if (query === this.query) {
      return;
    }

    this.query = query;
    this.page = 1;
    this.storePageInHistory();
    if (this.nodeId) {
      void this.load(this.data == null);
    }
  }

  private setSort(value: string): void {
    this.setOpenMenu(undefined);
    if (!isSortOption(value)) {
      return;
    }

    setStoredSort(value);
    if (value === this.sort) {
      return;
    }

    this.sort = value;
    this.page = 1;
    this.storePageInHistory();
    void this.load(false);
  }

  private setStatus(value: CatalogStatus): void {
    if (value === this.status) {
      return;
    }

    this.status = value;
    this.page = 1;
    this.storePageInHistory();
    void this.load(false);
  }

  private setPage(page: number): void {
    if (page < 1 || page === this.page || (this.data != null && page > this.data.totalPages)) {
      return;
    }

    this.page = page;
    this.storePageInHistory();
    void this.load(false);
  }

  private setOpenMenu(menu: string | undefined): void {
    if (menu === this.openMenu) {
      return;
    }

    this.openMenu = menu;
    this.requestUpdate();
  }

  private toggleMenu(event: Event, menu: string): void {
    event.stopPropagation();
    this.setOpenMenu(this.openMenu === menu ? undefined : menu);
  }

  private updatePageSizeForViewport(): boolean {
    const narrow = window.matchMedia(NARROW_QUERY).matches;
    const gap = narrow ? 10 : 14;
    const minCardWidth = narrow ? 170 : 220;
    const width = Math.max(0, this.clientWidth - (narrow ? 32 : 48));
    if (width === 0) {
      return false;
    }

    const columns = Math.max(1, Math.floor((width + gap) / (minCardWidth + gap)));
    const pageSize = columns * MAX_PRODUCT_ROWS;
    if (pageSize === this.pageSize) {
      return false;
    }

    this.pageSize = pageSize;
    return true;
  }

  private visibleKeys(): string[] {
    const data = this.data;
    if (data == null) {
      return [];
    }

    const subcategories = this.query ? [] : data.subcategories;
    return [...subcategories.map(category => category.key), ...data.products.map(product => product.key)];
  }

  private toggleKey(event: MouseEvent, key: string): void {
    event.preventDefault();
    event.stopPropagation();
    this.selection?.toggleSelect(key);
  }

  private toggleVisible(): void {
    if (this.selection == null) {
      return;
    }

    this.selection.setSelection(toggleAll(this.visibleKeys(), this.selection.getSelection()));
  }

  private clearFilter(): void {
    this.setStatus('all');
  }

  private revealCollectionHost(): void {
    if (this.pickerMode) return;
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

  protected override updated(): void {
    if (!this.pickerMode) {
      this.revealCollectionHost();
    }
  }

  override render(): TemplateResult {
    if (this.pickerMode) {
      return html`<slot></slot>`;
    }

    return html`<div class="shell ${this.busy ? 'busy' : ''}" aria-busy=${this.loading || this.busy ? 'true' : 'false'}>${this.renderContent()}</div>`;
  }

  private renderContent(): TemplateResult {
    if (this.loading) {
      return this.renderLoading();
    }

    const data = this.data;
    if (this.error || data == null) {
      return html`
        <div class="empty error" role="alert">
          <strong>Could not load the catalog</strong>
          <span>${this.error || 'No catalog data.'}</span>
          <div class="actions">
            ${data?.parent != null ? html`<button type="button" class="control" @click=${() => this.drillTo(data.parent!)}>Back</button>` : nothing}
            <button type="button" class="control" @click=${() => void this.load()}>Retry</button>
          </div>
        </div>
      `;
    }

    const showSubcategories = !this.query && data.subcategories.length > 0;

    return html`
      ${this.renderBreadcrumbs(data)}
      ${this.renderHeader(data)}
      <div class="content">
        ${data.productCount > 0 ? this.renderFilters(data) : nothing}
        ${showSubcategories ? this.renderSubcategories(data.subcategories) : nothing}
        ${this.renderProducts(data)}
        ${this.renderPagination(data)}
      </div>
    `;
  }

  private renderLoading(): TemplateResult {
    return html`
      <div role="status" aria-label="Loading catalog">
        <span class="skeleton" style="height:14px;width:220px;margin-bottom:18px"></span>
        <div class="header">
          <span class="skeleton" style="height:44px;width:44px;border-radius:8px"></span>
          <div class="heading">
            <span class="skeleton" style="height:28px;width:200px"></span>
            <span class="skeleton" style="height:14px;width:160px;margin-top:8px"></span>
          </div>
        </div>
        <section class="section">
          <span class="skeleton" style="height:11px;width:120px;margin-bottom:10px"></span>
          <div class="subcats">
            ${[0, 1, 2, 3].map(() => html`<span class="skeleton" style="height:56px;width:230px;border-radius:10px"></span>`)}
          </div>
        </section>
        <section class="section">
          <span class="skeleton" style="height:11px;width:100px;margin-bottom:10px"></span>
          <div class="grid">
            ${Array.from({ length: 12 }, () => html`
              <div class="skeleton-card">
                <span class="skeleton image"></span>
                <div class="lines">
                  <span class="skeleton" style="height:16px;width:76%"></span>
                  <span class="skeleton" style="height:12px;width:58%"></span>
                  <span class="skeleton" style="height:22px;width:88%"></span>
                </div>
              </div>
            `)}
          </div>
        </section>
        <span class="visually-hidden">Loading catalog…</span>
      </div>
    `;
  }

  private renderBreadcrumbs(data: CatalogResponse): TemplateResult | typeof nothing {
    if (data.breadcrumbs.length <= 1) {
      return nothing;
    }

    const last = data.breadcrumbs.length - 1;
    return html`
      <nav class="crumbs" aria-label="Catalog path">
        ${data.breadcrumbs.map((item, index) => html`
          ${index > 0 ? html`<span class="sep" aria-hidden="true">/</span>` : nothing}
          ${this.renderBreadcrumb(item, index, last, data.breadcrumbs[index - 1])}
        `)}
      </nav>
    `;
  }

  private renderBreadcrumb(item: CatalogBreadcrumb, index: number, last: number, parent?: CatalogBreadcrumb): TemplateResult {
    const isCurrent = index === last;
    const menu = `crumb:${item.key}`;
    const open = this.openMenu === menu;
    const hasSiblings = item.contentTypeAlias === 'ekmCategory' && item.siblings.length > 1;

    return html`
      <span class="crumb">
        <button
          type="button"
          class="crumb-link ${isCurrent ? 'current' : ''} ${index === 0 ? 'root' : ''}"
          aria-current=${isCurrent ? 'page' : nothing}
          @click=${() => (isCurrent ? undefined : this.drillTo(item))}>${item.name}</button>
        ${hasSiblings ? html`
          <button
            type="button"
            class="crumb-caret"
            aria-haspopup="menu"
            aria-expanded=${open ? 'true' : 'false'}
            aria-label="Show sibling categories for ${item.name}"
            @click=${(event: Event) => this.toggleMenu(event, menu)}>${open ? '▲' : '▼'}</button>
          ${open ? html`
            <div class="menu crumb-menu" role="menu">
              ${parent != null ? html`<div class="menu-head">In ${parent.name}</div>` : nothing}
              ${item.siblings.map(sibling => html`
                <button
                  type="button"
                  role="menuitem"
                  class="menu-item ${sibling.key === item.key ? 'active' : ''}"
                  @click=${() => (sibling.key === item.key ? this.setOpenMenu(undefined) : this.drillTo(sibling))}>
                  ${folderIcon}
                  <span class="grow">${sibling.name}</span>
                  <span class="count">${sibling.totalProductCount}</span>
                  <span class="tick">${sibling.key === item.key ? '✓' : ''}</span>
                </button>
              `)}
            </div>
          ` : nothing}
        ` : nothing}
      </span>
    `;
  }

  private renderHeader(data: CatalogResponse): TemplateResult {
    const visible = this.visibleKeys();
    const allSelected = visible.length > 0 && visible.every(key => this.selected.has(key));
    const sortOpen = this.openMenu === 'sort';

    return html`
      <header class="header">
        ${data.parent != null ? html`<button type="button" class="back" aria-label="Back to ${data.parent.name}" @click=${() => this.drillTo(data.parent!)}>←</button>` : nothing}
        <div class="heading">
          <h1>${data.current.name}</h1>
          <p class="meta">${metaLine(data.current.totalProductCount, data.subcategoryCount)}</p>
        </div>
        <div class="tools">
          <div class="sort">
            <button
              type="button"
              class="control ${sortOpen ? 'open' : ''}"
              aria-haspopup="menu"
              aria-expanded=${sortOpen ? 'true' : 'false'}
              @click=${(event: Event) => this.toggleMenu(event, 'sort')}>${sortLabel(this.sort)}<span class="caret" aria-hidden="true">▼</span></button>
            ${sortOpen ? html`
              <div class="menu sort-menu" role="menu">
                ${SORT_OPTIONS.map(option => html`
                  <button type="button" role="menuitemradio" aria-checked=${option.value === this.sort ? 'true' : 'false'} class="menu-item ${option.value === this.sort ? 'active' : ''}" @click=${() => this.setSort(option.value)}>
                    <span class="grow">${option.label}</span>
                    <span class="tick">${option.value === this.sort ? '✓' : ''}</span>
                  </button>
                `)}
              </div>
            ` : nothing}
          </div>
          <button type="button" class="control" role="checkbox" aria-checked=${allSelected ? 'true' : 'false'} ?disabled=${visible.length === 0} @click=${() => this.toggleVisible()}>
            <span class="box ${allSelected ? 'on' : ''}" aria-hidden="true">${allSelected ? '✓' : ''}</span>Select all
          </button>
        </div>
      </header>
    `;
  }

  private renderFilters(data: CatalogResponse): TemplateResult {
    return html`
      <div class="filters">
        ${STATUS_CHIPS.map(chip => html`
          <button type="button" class="chip ${this.status === chip.value ? 'on' : ''}" title=${chip.title} aria-pressed=${this.status === chip.value ? 'true' : 'false'} @click=${() => this.setStatus(chip.value)}>
            ${chip.label}<span class="count">${data.statusCounts[chip.count]}</span>
          </button>
        `)}
        ${this.status !== 'all' ? html`<span class="showing">Showing ${data.filteredProductCount} of ${data.statusCounts.all}</span>` : nothing}
      </div>
    `;
  }

  private renderSubcategories(subcategories: CatalogNode[]): TemplateResult {
    return html`
      <section class="section">
        <h2>Subcategories<span>${subcategories.length}</span></h2>
        <div class="subcats">
          ${repeat(subcategories, category => category.key, category => this.renderSubcategory(category))}
        </div>
      </section>
    `;
  }

  private renderSubcategory(category: CatalogNode): TemplateResult {
    const selected = this.selected.has(category.key);
    const health = healthLabel(category);

    return html`
      <a class="subcat ${selected ? 'selected' : ''}" href=${this.getDocumentHref(category.key)} @click=${(event: Event) => { event.preventDefault(); this.drillTo(category); }}>
        <button type="button" class="box ${selected ? 'on' : ''}" role="checkbox" aria-checked=${selected ? 'true' : 'false'} aria-label="Select ${category.name}" @click=${(event: MouseEvent) => this.toggleKey(event, category.key)}>${selected ? '✓' : ''}</button>
        <span class="subcat-icon">${folderIcon}</span>
        <span class="subcat-text">
          <span class="subcat-name">${category.name}<span class="dot ${category.published ? 'green' : 'grey'}" title=${category.published ? 'Category is published' : 'Category is not published'}></span></span>
          <span class="subcat-meta">${metaLine(category.totalProductCount, category.subcategoryCount)} · <span class=${health.tone}>${health.text}</span></span>
        </span>
        <span class="chevron" aria-hidden="true">›</span>
      </a>
    `;
  }

  private renderProducts(data: CatalogResponse): TemplateResult {
    return html`
      <section class="section">
        <h2>Products<span>${data.filteredProductCount}</span></h2>
        ${data.products.length === 0
          ? this.renderEmptyProducts(data)
          : html`<div class="grid">${repeat(data.products, product => product.key, product => this.renderProduct(product))}</div>`}
      </section>
    `;
  }

  private renderEmptyProducts(data: CatalogResponse): TemplateResult {
    if (this.query) {
      return html`<div class="empty"><strong>No products match “${this.query}”</strong><span>Try another name or SKU, or clear the search above.</span></div>`;
    }

    if (this.status !== 'all' && data.productCount > 0) {
      const chip = STATUS_CHIPS.find(item => item.value === this.status);
      return html`
        <div class="empty">
          <strong>No ${chip?.label.toLowerCase()} products here</strong>
          <span>None of the ${plural(data.productCount, 'product', 'products')} in this category match the filter.</span>
          <div class="actions"><button type="button" class="control" @click=${() => this.clearFilter()}>Clear filter</button></div>
        </div>
      `;
    }

    return html`
      <div class="empty">
        <strong>No products in this category yet</strong>
        <span>Use Create to add a product here, or move products in from another category.</span>
      </div>
    `;
  }

  private renderProduct(product: CatalogProduct): TemplateResult {
    const selected = this.selected.has(product.key);
    const href = `/umbraco/section/content/workspace/document/edit/${product.key}`;
    const thumbnail = product.image ? this.thumbnails.get(product.image) : undefined;
    const publish = publishLabel(product);
    const stock = stockLabel(product.stock);

    return html`
      <article class="card ${selected ? 'selected' : ''}">
        <button type="button" class="box ${selected ? 'on' : ''}" role="checkbox" aria-checked=${selected ? 'true' : 'false'} aria-label="Select ${product.name}" @click=${(event: MouseEvent) => this.toggleKey(event, product.key)}>${selected ? '✓' : ''}</button>
        <ekom-catalog-entity class="card-actions" .unique=${product.key}>
          <umb-entity-actions-dropdown compact label="Actions for ${product.name}">
            <uui-symbol-more slot="label"></uui-symbol-more>
          </umb-entity-actions-dropdown>
        </ekom-catalog-entity>
        <a class="tile" href=${href} tabindex="-1" aria-hidden="true">
          ${thumbnail ? html`<img src=${thumbnail} alt="" loading="lazy" decoding="async" />` : imageIcon}
        </a>
        <div class="card-body">
          <a class="name" href=${href} title=${product.name}>${product.name}</a>
          <div class="row">
            <span class="sku" title=${product.sku}>${product.sku || 'No SKU'}</span>
            <span class="prices">
              ${product.originalPrice ? html`<span class="was">${product.originalPrice}</span>` : nothing}
              <span class="price">${product.price}</span>
            </span>
          </div>
          <span class="variants">${product.variantCount > 0 ? plural(product.variantCount, 'variant', 'variants') : 'No variants'}</span>
          <div class="foot">
            <span class="badges">
              <span class="badge ${publish.tone}"><i></i>${publish.text}</span>
              ${product.pendingChanges ? html`<span class="badge amber edits" title="Published with unpublished edits">Edits</span>` : nothing}
              ${product.linked ? html`<span class="badge grey edits" title="Lives in another category and is shown here through its categories picker">Linked</span>` : nothing}
            </span>
            <span class="stock ${stock.tone}">${stock.text}</span>
          </div>
        </div>
      </article>
    `;
  }

  private renderPagination(data: CatalogResponse): TemplateResult | typeof nothing {
    if (data.filteredProductCount === 0 || data.totalPages <= 1) {
      return nothing;
    }

    const from = (data.page - 1) * data.pageSize + 1;
    const to = Math.min(data.page * data.pageSize, data.filteredProductCount);

    return html`
      <footer class="pagination">
        <div>
          <button type="button" ?disabled=${data.page <= 1} @click=${() => this.setPage(data.page - 1)}>‹ Prev</button>
          ${paginationItems(data.page, data.totalPages).map(item => item === '…'
            ? html`<span>…</span>`
            : html`<button type="button" class=${item === data.page ? 'current' : ''} aria-current=${item === data.page ? 'page' : nothing} @click=${() => this.setPage(item)}>${item}</button>`)}
          <button type="button" ?disabled=${data.page >= data.totalPages} @click=${() => this.setPage(data.page + 1)}>Next ›</button>
        </div>
        <span>${from}–${to} of ${data.filteredProductCount}</span>
      </footer>
    `;
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

  private async fetchJson<T>(url: string, signal: AbortSignal): Promise<T> {
    const response = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' }, signal });
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

customElements.define('ekom-catalog-entity', EkomCatalogEntityElement);
customElements.define('ekom-catalog-collection-view', EkomCatalogCollectionViewElement);

export default EkomCatalogCollectionViewElement;
