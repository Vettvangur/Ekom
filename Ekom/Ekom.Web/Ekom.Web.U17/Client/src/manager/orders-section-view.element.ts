import { renderManagerContent } from './manager-shared';
import type { UmbThemeContext } from '@umbraco-cms/backoffice/themes';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_NOTIFICATION_CONTEXT, type UmbNotificationColor } from '@umbraco-cms/backoffice/notification';
import {
  CustomerInformationField,
  EkomManagerApi,
  GiftcardRecoveryAttempt,
  ManagerConfig,
  ManagerFilters,
  OrderAction,
  OrderActivityLog,
  OrderInfo,
  OrderLockStatus,
  OrderListItem,
  OrderSearchResult,
  ShippingProviderItem,
  decodeHtml,
  downloadBlob,
  escapeHtml,
  formatDate,
  getStatusValue,
  managerState,
  managerStyles,
  renderStatusBadge,
  renderManagerHeader, ensureManagerCurrency,
  renderDateRange,
  bindDateRange,
  orderViews,
  renderOrderRowsLoading,
  renderLoadingLine,
  renderDrawerLoading,
  type OrderReview,
  printOrderDocument,
} from './manager-shared';

type EditorFieldDefinition = {
  key: string;
  label: string;
  property: string;
};

const customerFields: EditorFieldDefinition[] = [
  { key: 'customerName', label: 'Name', property: 'name' },
  { key: 'customerEmail', label: 'Email', property: 'email' },
  { key: 'customerAddress', label: 'Address', property: 'address' },
  { key: 'customerApartment', label: 'Apartment', property: 'apartment' },
  { key: 'customerCity', label: 'City', property: 'city' },
  { key: 'customerCountry', label: 'Country', property: 'country' },
  { key: 'customerZipCode', label: 'Zipcode', property: 'zipCode' },
  { key: 'customerPhone', label: 'Phone', property: 'phone' },
];

const shippingFields: EditorFieldDefinition[] = [
  { key: 'shippingName', label: 'Name', property: 'name' },
  { key: 'shippingEmail', label: 'Email', property: 'email' },
  { key: 'shippingAddress', label: 'Address', property: 'address' },
  { key: 'shippingApartment', label: 'Apartment', property: 'apartment' },
  { key: 'shippingCity', label: 'City', property: 'city' },
  { key: 'shippingCountry', label: 'Country', property: 'country' },
  { key: 'shippingZipCode', label: 'Zipcode', property: 'zipCode' },
  { key: 'shippingPhone', label: 'Phone', property: 'phone' },
];

const activityLogPreviewCharacterLimit = 180;
const activityLogTypeSuccess = 1;
const activityLogTypeAlert = 2;

export class EkomOrdersSectionViewElement extends UmbElementMixin(HTMLElement) {
  private readonly api = new EkomManagerApi();
  private notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;
  private result: OrderSearchResult = { orders: [], count: 0, totalPages: 0 };
  private page = 1;
  private loading = true;
  private orderOpening = false;
  private pendingListKey = '';
  private shownListKey?: string;
  private error = '';
  private config?: ManagerConfig;
  private configError = '';
  private notifyOrderStatusOverride?: boolean;
  private searchTimer = 0;
  private pollingSeconds = 0;
  private pollingTimer = 0;
  private overlay = '';
  private reviews: OrderReview[] = [];
  private activeReview?: OrderReview;
  private reviewSaving = false;
  private reviewError = '';
  private reviewComment = '';
  private reviewResolved = true;
  private orderTab = 'order';
  private stateSaving = false;
  private orderListRequest = 0;
  private viewCounts: Record<string, number> = {};
  private returnFocusOrderId?: string;
  private selectedOrder?: OrderInfo;
  private orderLogs: OrderActivityLog[] = [];
  private orderLogsLoading = false;
  private orderLogsError = false;
  private activityLogExpandedIndexes = new Set<number>();
  private orderActions: OrderAction[] = [];
  private orderActionsLoading = false;
  private orderActionsError = false;
  private executingActionKey = '';
  private orderRequestId = 0;
  private lockRequestId = 0;
  private orderLockStatus?: OrderLockStatus;
  private releasingOrderLock = false;
  private orderLockWarning = '';
  private orderLockReleaseError = '';
  private orderLockLoading = false;
  private orderLockError = false;
  private recoveryAttempts?: GiftcardRecoveryAttempt[];
  private recoveryLoading = false;
  private recoveryError = false;
  private recoveryRequestId = 0;
  private recoveryOperation = '';
  private recoveryMessage = '';
  private recoveryActionError = '';
  private recoveryRefreshError = false;
  private recoveryScrollTop?: number;
  private customerEditorOpen = false;
  private customerSaving = false;
  private customerEditModel?: { customer: CustomerInformationField[]; shipping: CustomerInformationField[] };
  private orderLineEditorOpen = false;
  private orderLineSaving = false;
  private orderLineEditModel?: { productId: string; variantId: string; quantity: string };
  private shippingProviderEditorOpen = false;
  private shippingProviderSaving = false;
  private shippingProviders: ShippingProviderItem[] = [];
  private filterProvidersRequestId = 0;
  private filterProvidersLoading = false;
  private shippingProviderId = '';
  private shippingCustomFields: Array<{ key: string; value: string; existing: boolean }> = [];
  private removingOrderLineId = '';
  private exportIncludeOrderLines = false;
  private exporting = false;
  private readonly dismissFulfillmentMenus = (event: PointerEvent): void => {
    const path = event.composedPath();
    this.querySelectorAll<HTMLDetailsElement>('.ekom-row-fulfillment[open], .ekom-fulfillment-menu[open]').forEach(menu => {
      if (!path.includes(menu)) menu.open = false;
    });
  };
  private readonly handleKeyDown = (event: KeyboardEvent): void => {
    if (this.activeReview && event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); if (!this.reviewSaving) { this.activeReview = undefined; this.render(); } return; }
    if (this.activeReview && event.key.startsWith('Arrow')) return;
    if (event.key === 'Escape') {
      const menu = this.querySelector<HTMLDetailsElement>('.ekom-row-fulfillment[open], .ekom-fulfillment-menu[open]');
      if (menu) { event.preventDefault(); event.stopPropagation(); menu.open = false; menu.querySelector('summary')?.focus(); return; }
    }
    if (['ArrowLeft', 'ArrowRight', 'ArrowUp', 'ArrowDown'].includes(event.key) && this.overlay === 'order') {
      const target = event.composedPath()[0] as HTMLElement | undefined;
      if (event.defaultPrevented || event.repeat || event.altKey || event.ctrlKey || event.metaKey || event.shiftKey ||
          this.customerEditorOpen || this.orderLineEditorOpen || this.shippingProviderEditorOpen || this.stateSaving ||
          this.executingActionKey || this.releasingOrderLock || this.recoveryOperation ||
          target?.closest?.('input, select, textarea, [contenteditable], [role="tablist"], details') ||
          this.querySelector('details[open]')) return;
      const pending = this.querySelector<HTMLInputElement>('[data-field="fulfillmentState"]');
      if (pending && pending.value !== this.selectedOrder?.status?.fulfillmentStatus) return;
      const direction = ['ArrowLeft', 'ArrowUp'].includes(event.key) ? 'Previous order' : 'Next order';
      const button = this.querySelector<HTMLButtonElement>(`.ekom-order-navigation button[aria-label="${direction}"]`);
      if (button && !button.disabled) { event.preventDefault(); button.click(); }
      return;
    }
    if (event.key === 'Tab' && this.overlay) {
      const dialogs = this.querySelectorAll<HTMLElement>('.ekmCustomerInformationModal__panel, .ekmOverlay__panel');
      const dialog = dialogs.item(dialogs.length - 1);
      const targets = dialog ? Array.from(dialog.querySelectorAll<HTMLElement>('button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), a[href], summary, [tabindex="0"]')).filter(element => !element.closest('[hidden]') && element.getClientRects().length > 0) : [];
      const first = targets[0], last = targets[targets.length - 1];
      if (first && last && (!dialog.contains(document.activeElement) || (event.shiftKey ? document.activeElement === first : document.activeElement === last))) {
        event.preventDefault();
        (event.shiftKey ? last : first).focus();
      }
      return;
    }
    if (event.key !== 'Escape' || !this.overlay) {
      return;
    }

    if (this.customerEditorOpen) {
      if (this.customerSaving) {
        return;
      }

      this.customerEditorOpen = false;
      this.customerEditModel = undefined;
      this.render();
      return;
    }

    if (this.orderLineEditorOpen) {
      if (this.orderLineSaving) {
        return;
      }

      this.orderLineEditorOpen = false;
      this.orderLineEditModel = undefined;
      this.render();
      return;
    }

    if (this.shippingProviderEditorOpen) {
      if (this.shippingProviderSaving) {
        return;
      }

      this.shippingProviderEditorOpen = false;
      this.render();
      return;
    }

    if (this.exporting) {
      return;
    }

    this.closeOverlay();
  };

  override connectedCallback(): void {
    super.connectedCallback();
    if (!this.hasAttribute('classic')) this.consumeContext?.<UmbThemeContext>('umbThemeContext', context => {
      if (context) this.observe(context.theme, alias => {
        this.toggleAttribute('data-ekom-dark', alias === 'umb-dark-theme');
      }, 'ekom-manager-theme');
    });
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, context => {
      this.notificationContext = context;
    });
    document.addEventListener('keydown', this.handleKeyDown);
    document.addEventListener('pointerdown', this.dismissFulfillmentMenus, true);
    this.render();
    void this.initialize();
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    if (managerState.filters.orderStatus === 'NeedsReview') managerState.filters.orderStatus = 'AllOrders';
    document.removeEventListener('keydown', this.handleKeyDown);
    document.removeEventListener('pointerdown', this.dismissFulfillmentMenus, true);
    window.clearTimeout(this.searchTimer);
    window.clearInterval(this.pollingTimer);
    this.orderListRequest++;
  }

  private setPolling(seconds: number): void {
    if (![0, 2, 5, 10, 20, 30].includes(seconds)) return;
    window.clearInterval(this.pollingTimer);
    this.pollingSeconds = seconds;
    if (seconds) this.pollingTimer = window.setInterval(() => { void this.pollOrders(); }, seconds * 1000);
    this.render();
  }

  private async pollOrders(): Promise<void> {
    if (!this.pollingSeconds || !this.isConnected || document.hidden || this.loading || this.overlay || this.stateSaving ||
        this.contains(document.activeElement) || this.querySelector('details[open]')) return;
    const request = ++this.orderListRequest;
    const filters = { ...managerState.filters };
    if (!filters.store) return;
    this.loading = true;
    try {
      const result = await this.api.searchOrders(filters, this.page);
      if (request !== this.orderListRequest || !this.pollingSeconds || !this.isConnected || this.overlay || this.contains(document.activeElement)) return;
      const next = { ...result, orders: result.orders || [] };
      if (filters.orderStatus === 'NeedsReview' && result.count === 0) {
        managerState.filters.orderStatus = 'AllOrders'; this.page = 1;
        await this.loadOrders(); return;
      }
      {
        const counts: Record<string, number> = { [filters.orderStatus]: result.count };
        try { for (const item of await this.api.orderViewCounts(filters, this.otherViews(filters))) counts[item.view] = item.count; }
        catch { /* Leave unavailable counts blank until the next refresh. */ }
        if (request !== this.orderListRequest || !this.pollingSeconds || !this.isConnected || this.overlay || this.contains(document.activeElement)) return;
        if (JSON.stringify(next) !== JSON.stringify(this.result) || orderViews.some(view => counts[view.key] !== this.viewCounts[view.key])) {
          this.result = next;
          this.viewCounts = counts;
          this.loading = false;
          this.render();
        }
      }
      const control = this.querySelector('.ekom-polling summary');
      if (control) {
        control.classList.add('just-refreshed');
        control.addEventListener('animationend', () => control.classList.remove('just-refreshed'), { once: true });
      }
    } catch {
      // Retain the last successful list on a transient background failure.
    } finally {
      if (request === this.orderListRequest) this.loading = false;
    }
  }

  private async initialize(): Promise<void> {
    void this.api.printingEnabled().then(enabled => { managerState.printingEnabled = enabled; if (this.isConnected) this.render(); });
    const drill = new URLSearchParams(location.search);
    if (drill.get('analyticsDrill') === '1') {
      history.replaceState(history.state, '', location.pathname);
      managerState.filters.store = drill.get('store') || '';
      managerState.filters.currency = drill.get('currency') || '';
      managerState.filters.dateFrom = drill.get('start') || managerState.filters.dateFrom;
      managerState.filters.dateTo = drill.get('end') || managerState.filters.dateTo;
      managerState.filters.orderStatus = drill.get('orderStatus') || 'CompletedOrders';
      for (const key of ['query','paymentProvider','shippingProvider','couponCode','giftcardCode','productSku','trackingSource','trackingMedium','trackingCampaign','trackingTerm','trackingContent','trackingClickId'] as const) managerState.filters[key] = '';
    }
    try {
      const [statuses, stores] = await Promise.all([
        this.api.statusList(),
        this.api.stores(),
        this.loadConfig(),
      ]);

      managerState.statusList = statuses || [];
      managerState.stores = stores || [];

      if (!managerState.filters.store && managerState.stores.length) {
        managerState.filters.store = managerState.stores[0].alias;
      }

      if (!this.hasAttribute('classic')) ensureManagerCurrency();
      await this.loadFilterProviders();
      await this.loadOrders();
      managerState.pendingOrderId ||= new URLSearchParams(location.search).get('orderId') || undefined;
      if (managerState.pendingOrderId) {
        const orderId = managerState.pendingOrderId;
        managerState.pendingOrderId = undefined;
        await this.openOrder(orderId);
      }
    } catch (error) {
      this.error = getErrorMessage(error, 'Error loading Ekom Manager.');
      this.loading = false;
      this.render();
    }
  }

  private async loadConfig(): Promise<void> {
    this.config = undefined;
    this.configError = '';
    try {
      this.config = await this.api.config();
    } catch {
      this.configError = 'Unable to load Ekom configuration. Order status updates are disabled. Reload to try again.';
    }
    this.render();
  }

  private get notifyOrderStatus(): boolean {
    return this.notifyOrderStatusOverride ?? this.config?.managerOrderStatusFireEventsByDefault ?? false;
  }

  private async loadFilterProviders(resetSelection = false): Promise<void> {
    const requestId = ++this.filterProvidersRequestId;
    const store = managerState.filters.store;
    if (resetSelection || !store) {
      managerState.filters.paymentProvider = '';
      managerState.filters.shippingProvider = '';
    }

    managerState.paymentProviders = [];
    managerState.shippingFilterProviders = [];
    if (!store) {
      this.filterProvidersLoading = false;
      return;
    }

    this.filterProvidersLoading = true;
    this.render();
    const [paymentResult, shippingResult] = await Promise.allSettled([
      this.api.paymentProviders(store),
      this.api.shippingProviders(store),
    ]);
    if (requestId !== this.filterProvidersRequestId || store !== managerState.filters.store) {
      return;
    }

    const paymentProviders = paymentResult.status === 'fulfilled' ? paymentResult.value : [];
    const shippingProviders = shippingResult.status === 'fulfilled' ? shippingResult.value : [];
    managerState.paymentProviders = paymentProviders;
    managerState.shippingFilterProviders = shippingProviders;
    this.filterProvidersLoading = false;

    if (paymentResult.status === 'rejected' || shippingResult.status === 'rejected') {
      this.showNotification('warning', 'Filter options unavailable',
        'Some provider options could not be loaded. Orders can still be searched; reload to try again.');
    }

    if (managerState.filters.paymentProvider) {
      const selectedProviderStillExists = managerState.paymentProviders.some(provider => provider.key === managerState.filters.paymentProvider);

      if (!selectedProviderStillExists) {
        managerState.filters.paymentProvider = '';
      }
    }

    if (managerState.filters.shippingProvider
      && !shippingProviders.some(provider => provider.key === managerState.filters.shippingProvider)) {
      managerState.filters.shippingProvider = '';
    }
    this.render();
  }

  private otherViews(filters: ManagerFilters): string[] {
    return orderViews.map(view => view.key).filter(key => key !== filters.orderStatus);
  }

  private async loadOrders(): Promise<void> {
    if (!managerState.filters.store) {
      this.loading = false;
      this.result = { orders: [], count: 0, totalPages: 0 };
      if (!this.stateSaving) this.render();
      return;
    }

    const listRequest = ++this.orderListRequest;
    const filters = { ...managerState.filters };
    const listKey = this.pendingListKey = JSON.stringify([filters, this.page]);
    this.loading = true;
    this.error = '';
    if (!this.stateSaving) this.render();

    try {
      const result = await this.api.searchOrders(filters, this.page);
      if (listRequest !== this.orderListRequest) return;
      this.shownListKey = listKey;
      this.viewCounts[filters.orderStatus] = result.count;
      if (filters.orderStatus === 'NeedsReview' && result.count === 0) {
        managerState.filters.orderStatus = 'AllOrders'; this.page = 1;
        await this.loadOrders(); return;
      }
      const otherViews = this.otherViews(filters);
      void this.api.orderViewCounts(filters, otherViews).catch(() => []).then(items => {
        if (listRequest !== this.orderListRequest) return;
        otherViews.forEach(view => delete this.viewCounts[view]);
        items.forEach(item => { this.viewCounts[item.view] = item.count; });
      }).then(() => {
        if (listRequest !== this.orderListRequest || !this.isConnected) return;
        this.querySelectorAll<HTMLButtonElement>('[data-action="select-order-view"]').forEach(button => {
          const count = this.viewCounts[button.dataset.view || ''];
          if (button.dataset.view === 'NeedsReview') button.hidden = !(count && count > 0);
          let badge = button.querySelector('span');
          if (count === undefined) { badge?.remove(); return; }
          if (!badge) { badge = document.createElement('span'); button.appendChild(badge); }
          badge.textContent = String(count);
        });
      });
      this.result = {
        ...result,
        orders: result.orders || [],
      };
    } catch (error) {
      if (listRequest !== this.orderListRequest) return;
      this.error = getErrorMessage(error, 'Error searching orders.');
    } finally {
      if (listRequest === this.orderListRequest) { this.loading = false; if (!this.stateSaving) this.render(); }
    }
  }

  private render(): void {
    // While an order is opening, its loader is already on screen. Skip the renders the
    // individual requests trigger so the panel appears once, complete, instead of in pieces.
    if (this.orderOpening && this.querySelector<HTMLElement>('[data-order-opening]')?.dataset.orderOpening === this.returnFocusOrderId) return;
    const reviewInput = this.querySelector<HTMLTextAreaElement>('[data-review-comment]');
    const reviewFocus = reviewInput && this.ownerDocument?.activeElement === reviewInput
      ? { start: reviewInput.selectionStart, end: reviewInput.selectionEnd } : undefined;
    const scrollPositions = Array.from(this.querySelectorAll<HTMLElement>('.ekmOverlay, .ekmOverlay__content, .ekmCustomerInformationModal .ekmOverlay__content')).map(node => node.scrollTop);
    renderManagerContent(this, `
      <style>${managerStyles}</style>
      <link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css?v=20261110-order-tabs-1">
      <section class="ekmManager ekom-design">
        <div class="ekmManager__body">
          ${this.configError ? `<p class="status status--error" role="alert">${escapeHtml(this.configError)}</p>` : ''}
          ${renderManagerHeader('orders')}
          ${this.renderOrders()}
        </div>
      </section>
      ${this.renderOverlay()}
    `);

    this.bindEvents();
    if (reviewFocus) {
      const input = this.querySelector<HTMLTextAreaElement>('[data-review-comment]');
      input?.focus({ preventScroll: true });
      input?.setSelectionRange(reviewFocus.start, reviewFocus.end);
    }
    if (this.isConnected && this.overlay === 'order' && !this.contains(document.activeElement)) {
      this.querySelector<HTMLElement>('[data-action="close-overlay"]')?.focus({ preventScroll: true });
    }
    this.querySelectorAll<HTMLElement>('.ekmOverlay, .ekmOverlay__content, .ekmCustomerInformationModal .ekmOverlay__content').forEach((node, index) => {
      node.scrollTop = scrollPositions[index] ?? 0;
    });
    if (this.recoveryScrollTop !== undefined) {
      const overlay = this.querySelector<HTMLElement>('.ekmOverlay');
      if (overlay) overlay.scrollTop = this.recoveryScrollTop;
    }
  }

  private renderOrders(): string {
    const filters = managerState.filters;
    return `${this.renderToolbar()}<section class="ekom-order-list" aria-label="Orders" aria-busy="${this.loading}">
      <div class="ekom-list-toolbar"><nav class="ekom-order-views" aria-label="Order views">${orderViews.map(view => `<button type="button" data-action="select-order-view" data-view="${escapeHtml(view.key)}" ${view.key === 'NeedsReview' && !(this.viewCounts[view.key] > 0) ? 'hidden' : ''} aria-pressed="${filters.orderStatus === view.key}">${view.label}${this.viewCounts[view.key] === undefined ? '' : `<span>${this.viewCounts[view.key]}</span>`}</button>`).join('')}</nav><div class="ekom-list-search"><label class="ekom-search-field"><svg aria-hidden="true" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><circle cx="11" cy="11" r="7"></circle><path d="M20 20l-3.5-3.5"></path></svg><input type="search" data-field="query" value="${escapeHtml(filters.query)}" placeholder="Search orders" aria-label="Search orders"></label><button type="button" data-action="open-filter"><svg aria-hidden="true" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M4 6h16M7 12h10M10 18h4"></path></svg>Filter</button></div></div>
      <div class="ekom-inline-totals"><div><span>Orders</span><strong>${escapeHtml(this.result.count || 0)}</strong></div><div><span>Order amount</span><strong>${escapeHtml(this.result.grandTotal || '0')}</strong></div><div><span>Average</span><strong>${escapeHtml(this.result.averageAmount || '0')}</strong></div></div>
      ${this.error ? `<p class="status status--error" role="alert">${escapeHtml(this.error)}</p>` : ''}
      ${this.showOrdersLoading ? this.renderOrderTableHead() + renderOrderRowsLoading() + '</div>' : this.renderOrderTable()}
      ${this.renderPagination()}
    </section>`;
  }

  private renderToolbar(): string {
    return `<div class="ekom-shared-filters">${renderDateRange()}<div class="ekom-shared-filter-actions"><details class="ekom-polling${this.pollingSeconds ? ' is-active' : ''}"><summary aria-label="Order polling"><svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-3-6.7"></path><path d="M21 3v6h-6"></path></svg><span>${this.pollingSeconds ? `Polling every ${this.pollingSeconds}s` : 'Polling off'}</span><span aria-hidden="true">⌄</span></summary><div class="ekom-poll-options" role="group" aria-label="Polling interval">${[0, 2, 5, 10, 20, 30].map(seconds => `<button type="button" data-poll-seconds="${seconds}" aria-pressed="${seconds === this.pollingSeconds}"><span aria-hidden="true">•</span>${seconds ? `Every ${seconds} seconds` : 'Off'}</button>`).join('')}</div></details><button type="button" data-action="open-export">Export</button></div></div>`;
  }

  private renderOrderTable(): string {
    if (!this.result.orders.length) return '<div class="ekom-list-message"><h3>No orders match these filters</h3><p>Try a wider date range, another view, or clear the filters.</p><button type="button" data-action="clear-order-filters">Clear filters</button></div>';
    return `${this.renderOrderTableHead()}${this.result.orders.map(order => this.renderOrderRow(order)).join('')}</div>`;
  }

  /** Opens the orders table; the caller closes it after the rows. */
  private renderOrderTableHead(): string {
    return `<div class="ekom-list-table" role="table" aria-label="Orders"><div class="ekom-list-row ekom-list-head" role="row"><span role="columnheader">Order</span><span role="columnheader">Date</span><span role="columnheader">Customer</span><span role="columnheader" class="ekom-align-right">Total</span><span role="columnheader">Payment</span><span role="columnheader">Fulfillment</span><span role="columnheader" class="ekom-align-right">Items</span><span role="columnheader"><span class="ekom-sr-only">Open order</span></span></div>`;
  }

  /** Placeholder rows while a different list is on its way; a refresh of the list on screen keeps its rows. */
  private get showOrdersLoading(): boolean {
    return this.loading && this.pendingListKey !== this.shownListKey;
  }

  private renderOrderRow(order: OrderListItem): string {
    const state = order.status;
    return `<div class="ekom-list-row" role="row">
      <div role="cell" class="ekom-order-number"><span class="ekom-order-reference"><button type="button" data-action="view-order" data-order-id="${escapeHtml(order.uniqueId)}" aria-label="Open order ${escapeHtml(order.referenceId)}">#${escapeHtml(order.referenceId)}</button>${order.needsReview ? '<span class="ekom-review-icon" role="img" aria-label="Needs review" title="Needs review">!</span>' : ''}</span><small>${escapeHtml(state?.orderStatus || 'Needs review')}</small></div>
      <span role="cell" class="ekom-row-date">${escapeHtml(formatDate(order.createDate))}</span>
      <div role="cell" class="ekom-row-customer"><strong>${escapeHtml(order.customerName)}</strong><span>${escapeHtml(order.customerEmail)}</span></div>
      <strong role="cell" class="ekom-align-right">${escapeHtml(order.formattedTotal)}</strong>
      <div role="cell">${this.renderPaymentMenu(order)}</div>
      <div role="cell">${this.renderRowFulfillment(order)}</div>
      <span role="cell" class="ekom-align-right ekom-muted">${escapeHtml(order.itemQuantity ?? '—')}</span>
      <button role="cell" type="button" class="ekom-row-open" data-action="view-order" data-order-id="${escapeHtml(order.uniqueId)}" aria-label="Open order ${escapeHtml(order.referenceId)}">›</button>
    </div>`;
  }

  private renderPagination(): string {
    const count = this.result.count || 0;
    const pages = Math.max(1, this.result.totalPages);
    return `<footer class="ekom-list-footer"><span>${count ? (this.page - 1) * 20 + 1 : 0}–${Math.min(this.page * 20, count)} of ${count}</span><div><span>20 per page</span><button type="button" data-action="set-page" data-page="${this.page - 1}" ${this.page <= 1 || this.loading ? 'disabled' : ''} aria-label="Previous page">‹</button><strong>${this.page} / ${pages}</strong><button type="button" data-action="set-page" data-page="${this.page + 1}" ${this.page >= pages || this.loading ? 'disabled' : ''} aria-label="Next page">›</button></div></footer>`;
  }

  private renderOverlay(): string {
    if (this.overlay === 'filter') {
      return this.renderFilterOverlay();
    }

    if (this.overlay === 'export') {
      return this.renderExportOverlay();
    }

    if (this.overlay === 'order' && this.orderOpening) {
      return this.renderOrderOpening();
    }

    if (this.overlay === 'order' && this.selectedOrder) {
      return this.renderOrderOverlay(this.selectedOrder);
    }

    return '';
  }

  private renderFilterOverlay(): string {
    const filters = managerState.filters;
    return `
      <div class="ekmOverlay ekom-filter-overlay">
        <div class="ekmOverlay__panel ekom-filter-panel" role="dialog" aria-modal="true" aria-label="Filter orders">
          <div class="ekmOverlay__header"><div><h2>Filter orders</h2><p>Status filters also apply to Analytics and Customers</p></div><button class="btn-reset" type="button" data-action="close-overlay" aria-label="Close filters">&times;</button></div>
          <div class="ekmOverlay__content">
            <h3>Status</h3><div class="ekom-status-filter-grid">${[['Order', ['Draft', 'Open', 'Closed', 'Cancelled']], ['Payment', ['Pending', 'Authorized', 'Paid', 'PartiallyPaid', 'Failed', 'Voided']], ['Fulfillment', ['Unfulfilled', 'Picking', 'Picked', 'Packed', 'PartiallyFulfilled', 'Fulfilled']]].map(([label, values], index) => `<label class="control-group">${label}<select data-status-dimension="${index}"><option value="">Any</option>${(values as string[]).map(value => `<option value="${value}" ${filters.orderStatus.startsWith('states:') && filters.orderStatus.slice(7).split(';')[index] === value ? 'selected' : ''}>${value.replace(/([a-z])([A-Z])/g, '$1 $2')}</option>`).join('')}</select></label>`).join('')}</div>
            <h3>Payment &amp; shipping</h3><div class="ekom-filter-fields">
            <label class="control-group">Payment provider
              <select data-filter-field="paymentProvider" ${this.filterProvidersLoading ? 'disabled' : ''}>
                <option value="">Select payment provider</option>
                ${managerState.paymentProviders.map(provider => `<option value="${escapeHtml(provider.key)}" ${filters.paymentProvider === provider.key ? 'selected' : ''}>${escapeHtml(provider.title)}</option>`).join('')}
              </select>
            </label>
            <label class="control-group">Shipping provider
              <select data-filter-field="shippingProvider" ${this.filterProvidersLoading ? 'disabled' : ''}>
                <option value="">All shipping providers</option>
                ${managerState.shippingFilterProviders.map(provider => `<option value="${escapeHtml(provider.key)}" ${filters.shippingProvider === provider.key ? 'selected' : ''}>${escapeHtml(provider.title)}</option>`).join('')}
              </select>
            </label>
            ${this.renderFilterInput('giftcardCode', 'Gift card number:', 'Full gift card number')}</div><h3>Order contents <small>Exact match</small></h3><div class="ekom-filter-fields ekom-filter-two">${this.renderFilterInput('productSku', 'Product SKU', 'Exact SKU')}${this.renderFilterInput('couponCode', 'Coupon code', 'Exact order-level coupon code')}</div><h3>Tracking</h3><div class="ekom-filter-fields ekom-filter-two">
            ${this.renderFilterInput('trackingSource', 'Tracking source:', 'facebook')}
            ${this.renderFilterInput('trackingMedium', 'Tracking medium:', 'paid-social')}
            ${this.renderFilterInput('trackingCampaign', 'Tracking campaign:', 'summer_2026')}
            ${this.renderFilterInput('trackingTerm', 'Tracking term:', 'running shoes')}
            ${this.renderFilterInput('trackingContent', 'Tracking content:', 'hero_banner')}
            ${this.renderFilterInput('trackingClickId', 'Tracking click id:', 'gclid or fbclid')}
            </div></div><div class="ekom-filter-footer"><button type="button" class="btn-success" data-action="apply-filter" ${this.filterProvidersLoading ? 'disabled' : ''}>Apply</button><button type="button" class="btn-outline" data-action="close-overlay">Cancel</button></div>
        </div>
      </div>
    `;
  }

  private renderPaymentMenu(order: OrderListItem | OrderInfo, detail = false): string {
    const state = order.status;
    if (!this.config || this.configError || !state || state.requiresReview || state.orderStatus !== 'Open') return renderStatusBadge(state?.paymentStatus);
    const values = ['Pending', 'Authorized', 'Paid', 'PartiallyPaid', 'Failed', 'Voided'];
    return `<details class="ekom-row-fulfillment ekom-payment-menu" name="ekom-row-fulfillment"><summary aria-label="Set payment for order ${escapeHtml(order.referenceId)}">${renderStatusBadge(state.paymentStatus)}<span aria-hidden="true">▾</span></summary><div class="ekom-row-fulfillment-menu"><h4>Set payment</h4>${values.map(value => `<button type="button" data-action="set-payment" data-detail="${detail}" data-order-id="${escapeHtml(order.uniqueId)}" data-value="${value}" aria-pressed="${state.paymentStatus === value}" ${this.stateSaving ? 'disabled' : ''}>${this.renderStateDot(value)}${value.replace(/([a-z])([A-Z])/g, '$1 $2')}<span class="ekom-status-check">${state.paymentStatus === value ? '✓' : ''}</span></button>`).join('')}<p class="ekom-payment-note">Updates the recorded status immediately. Does not move money through the payment provider.</p>${detail ? '' : `<label><input type="checkbox" data-row-events ${this.config.managerOrderStatusFireEventsByDefault ?? true ? 'checked' : ''}><span><strong>Trigger status events</strong><small>Runs any handlers set up for this status</small></span></label>`}</div></details>`;
  }

  private async changePayment(button: HTMLButtonElement): Promise<void> {
    const detail = button.dataset.detail === 'true';
    const order = detail ? this.selectedOrder : this.result.orders.find(item => item.uniqueId === button.dataset.orderId);
    const value = button.dataset.value || '';
    if (button.disabled || this.stateSaving || !this.config || this.configError || !order?.status || order.status.requiresReview ||
        order.status.orderStatus !== 'Open' || !['Pending', 'Authorized', 'Paid', 'PartiallyPaid', 'Failed', 'Voided'].includes(value)) return;
    const menu = button.closest('details');
    if (order.status.paymentStatus === value) { menu?.removeAttribute('open'); return; }
    if (detail) {
      const pending = this.querySelector<HTMLInputElement>('[data-field="fulfillmentState"]');
      if (pending && pending.value !== order.status.fulfillmentStatus) {
        this.showError('Save your fulfillment change before updating payment.');
        return;
      }
      await this.changeState(`Payment${value}`);
      return;
    }
    const notify = menu?.querySelector<HTMLInputElement>('[data-row-events]')?.checked ?? this.config.managerOrderStatusFireEventsByDefault ?? true;
    this.stateSaving = true;
    menu?.querySelectorAll<HTMLButtonElement>('button').forEach(item => { item.disabled = true; });
    try {
      await this.api.changeOrderStates(order.uniqueId, order.status.revision, `Payment${value}`, notify);
      this.showSuccess('Payment status updated.');
    } catch (error) {
      this.showError(getErrorMessage(error, 'Error updating payment status.'));
    } finally {
      await this.loadOrders();
      this.stateSaving = false;
      this.render();
    }
  }

  private renderRowFulfillment(order: OrderListItem): string {
    const state = order.status;
    if (!this.config || this.configError || !state || state.requiresReview || state.orderStatus !== 'Open') return renderStatusBadge(state?.fulfillmentStatus);
    return `<details class="ekom-row-fulfillment" name="ekom-row-fulfillment"><summary aria-label="Set fulfillment for order ${escapeHtml(order.referenceId)}">${renderStatusBadge(state.fulfillmentStatus)}<span aria-hidden="true">▾</span></summary><div class="ekom-row-fulfillment-menu"><h4>Set fulfillment</h4>${['Unfulfilled', 'Picking', 'Picked', 'Packed', 'Fulfilled', 'PartiallyFulfilled'].map(value => `<button type="button" data-action="set-row-fulfillment" data-order-id="${escapeHtml(order.uniqueId)}" data-value="${value}" aria-pressed="${state.fulfillmentStatus === value}" ${this.stateSaving ? 'disabled' : ''}>${this.renderStateDot(value)}${value.replace(/([a-z])([A-Z])/g, '$1 $2')}<span class="ekom-status-check">${state.fulfillmentStatus === value ? '✓' : ''}</span></button>`).join('')}<label><input type="checkbox" data-row-events ${this.config.managerOrderStatusFireEventsByDefault ?? true ? 'checked' : ''}><span><strong>Trigger status events</strong><small>Runs any handlers set up for this status</small></span></label></div></details>`;
  }

  private async changeRowFulfillment(button: HTMLButtonElement): Promise<void> {
    const order = this.result.orders.find(item => item.uniqueId === button.dataset.orderId);
    const value = button.dataset.value || '';
    if (button.disabled || this.stateSaving || !this.config || this.configError || !order?.status || order.status.requiresReview ||
        order.status.orderStatus !== 'Open' || !['Unfulfilled', 'Picking', 'Picked', 'Packed', 'PartiallyFulfilled', 'Fulfilled'].includes(value)) return;
    const menu = button.closest('details');
    if (order.status.fulfillmentStatus === value) { menu?.removeAttribute('open'); return; }
    const fireEvents = menu?.querySelector<HTMLInputElement>('[data-row-events]')?.checked ?? this.config.managerOrderStatusFireEventsByDefault ?? true;
    this.stateSaving = true;
    menu?.querySelectorAll<HTMLButtonElement>('button').forEach(item => { item.disabled = true; });
    try {
      await this.api.changeOrderStates(order.uniqueId, order.status.revision, value === 'Fulfilled' ? 'Fulfill' : value, fireEvents);
      this.showSuccess('Fulfillment status updated.');
    } catch (error) {
      this.showError(getErrorMessage(error, 'Error updating fulfillment status.'));
    } finally {
      await this.loadOrders();
      this.stateSaving = false;
      this.render();
    }
  }

  private renderFilterInput(field: keyof ManagerFilters, label: string, placeholder: string): string {
    return `
      <label class="control-group">${escapeHtml(label)}
        <input type="text" data-filter-field="${escapeHtml(field)}" value="${escapeHtml(managerState.filters[field])}" placeholder="${escapeHtml(placeholder)}">
      </label>
    `;
  }

  private renderExportOverlay(): string {
    return `<div class="ekmOverlay ekom-export-overlay"><div class="ekmOverlay__panel ekom-export-panel" role="dialog" aria-modal="true" aria-label="Export orders">
      <div class="ekmOverlay__header"><h2>Export orders</h2><button class="btn-reset" type="button" data-action="close-overlay" aria-label="Close export" ${this.exporting ? 'disabled' : ''}>&times;</button></div>
      <div class="ekmOverlay__content"><p>Export the <strong>${this.result.count || 0}</strong> orders matching the current filters as CSV.</p>
        <label class="ekom-export-option"><input type="checkbox" data-field="includeOrderLines" ${this.exportIncludeOrderLines ? 'checked' : ''} ${this.exporting ? 'disabled' : ''}><span><strong>Include order lines</strong><small>Include product, variant, SKU, quantity and price details in the CSV.</small></span></label>
        ${this.exportIncludeOrderLines ? '<p class="ekom-export-note">Including order lines can take longer because each matching order needs to be loaded before the CSV is created.</p>' : ''}
        ${this.exporting ? '<p role="status">Exporting orders. This may take a while...</p>' : ''}
      </div><div class="ekom-filter-footer"><button type="button" class="btn-outline" data-action="close-overlay" ${this.exporting ? 'disabled' : ''}>Cancel</button><button type="button" class="btn-success" data-action="export-orders" ${this.exporting ? 'disabled' : ''}>Export</button></div>
    </div></div>`;
  }

  /** The order drawer while everything it shows is still being fetched. */
  private renderOrderOpening(): string {
    const reference = this.result.orders.find(order => order.uniqueId === this.returnFocusOrderId)?.referenceId;
    return `<div class="ekmOverlay ekom-order-drawer ekom-design-drawer" role="dialog" aria-modal="true" aria-label="Order details" aria-busy="true" data-order-opening="${escapeHtml(this.returnFocusOrderId)}"><div class="ekmOverlay__panel ekmOrderOverlay__panel"><header class="ekom-panel-heading"><div class="ekom-order-heading-copy"><h2>${reference ? `#${escapeHtml(reference)}` : 'Order'}</h2>${renderLoadingLine('Loading order…')}</div><button type="button" data-action="close-overlay" aria-label="Close order">×</button></header><div class="ekmOverlay__content ekmOrder">${renderDrawerLoading()}</div></div></div>`;
  }

  private renderOrderOverlay(order: OrderInfo): string {
    const index = this.result.orders.findIndex(item => item.uniqueId === order.uniqueId);
    const navigation = [-1, 1].map(direction => {
      const neighbor = index >= 0 ? this.result.orders[index + direction] : undefined;
      const label = direction < 0 ? 'Previous order' : 'Next order';
      return `<button type="button" data-action="view-order" data-order-id="${escapeHtml(neighbor?.uniqueId || '')}" aria-label="${label}" title="${label} on this page (${direction < 0 ? '← / ↑' : '→ / ↓'})" ${!neighbor || this.stateSaving || this.executingActionKey ? 'disabled' : ''}>${direction < 0 ? '‹' : '›'}</button>`;
    }).join('');
    return `<div class="ekmOverlay ekom-order-drawer ekom-design-drawer" role="dialog" aria-modal="true" aria-label="Order details"><div class="ekmOverlay__panel ekmOrderOverlay__panel"><header class="ekom-panel-heading"><nav class="ekom-order-navigation" aria-label="Order navigation">${navigation}</nav><div class="ekom-order-heading-copy"><h2>#${escapeHtml(order.referenceId)} ${renderStatusBadge(order.status?.paymentStatus)} ${renderStatusBadge(order.status?.fulfillmentStatus)} ${renderStatusBadge(order.status?.orderStatus)}</h2><p>${escapeHtml(order.customerInformation?.customer?.name)} · ${escapeHtml(formatDate(order.createDate))} · ${escapeHtml(order.storeInfo?.alias || order.storeAlias)} · <span class="ekom-heading-order-id"><span>${escapeHtml(order.uniqueId)}</span><button type="button" class="ekom-copy-order-id" data-action="copy-order-id" aria-label="Copy order ID" title="Copy order ID"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V4H4v12h4"/></svg></button></span></p></div><button type="button" data-action="close-overlay" aria-label="Close order">×</button></header><div class="ekmOverlay__content ekmOrder">${this.renderOrderDetails(order)}</div></div></div>${this.customerEditorOpen ? this.renderCustomerEditor() : ''}${this.orderLineEditorOpen ? this.renderOrderLineEditor() : ''}${this.shippingProviderEditorOpen ? this.renderShippingProviderEditor() : ''}${this.activeReview ? this.renderReviewDialog() : ''}`;
  }

  private renderOrderDetails(order: OrderInfo): string {
    const customer = order.customerInformation?.customer || {};
    const shipping = order.customerInformation?.shipping || {};
    const state = order.status;
    const tabs = [{ key: 'order', label: 'Order' }, { key: 'activity', label: 'Activity log' }, { key: 'tracking', label: 'Tracking & consent' }];
    const blocked = !this.config || !!this.configError || this.stateSaving || !state || state.requiresReview || this.orderLockStatus?.hasLock || this.orderLockLoading || this.orderLockError;
    const panel = (tab: string, content: string) => `<section id="ekom-order-panel-${tab}" role="tabpanel" aria-labelledby="ekom-order-tab-${tab}" ${this.orderTab === tab ? '' : 'hidden'}>${content}</section>`;
    const orderContent = `<div class="ekom-order-columns"><main class="ekom-order-main">
      <section class="ekom-detail-card">${this.renderDesignOrderLines(order)}</section>
      ${this.renderPaymentCard(order)}
      ${this.renderShipment(order)}
      ${Object.keys(order.customData || {}).length ? `<section class="ekom-detail-card"><h3>Notes</h3>${this.renderOrderCustomData(order)}</section>` : ''}
    </main><aside class="ekom-customer-sidebar"><section class="ekom-detail-card"><h3>Customer</h3><strong>${escapeHtml(customer.name)}</strong><div class="ekom-card-heading"><h4>Contact information</h4><button type="button" class="btn-reset" data-action="open-customer-editor">Edit</button></div><p>${escapeHtml(customer.email)}</p><p>${escapeHtml(customer.phone)}</p><h4>Shipping address</h4>${hasShippingInfo(shipping) ? this.renderAddress(shipping) : '<p>Same as billing address</p>'}<h4>Billing address</h4>${this.renderAddress(customer)}${this.renderExtraProperties(customer.properties, 'customer')}</section></aside></div>`;
    return `<div class="ekom-status-controls"><label>Order<details class="ekom-lifecycle-menu"><summary aria-label="Order status">${this.renderStateDot(state?.orderStatus)}${escapeHtml(state?.orderStatus || 'Needs review')} ▾</summary><div>${(state?.orderStatus === 'Closed' ? [['Reopen', 'Reopen order']] : [['Close', 'Close order'], ['Cancel', 'Cancel order']]).map(([action, label]) => `<button type="button" data-action="order-state-command" data-command="${action}" ${blocked ? 'disabled' : ''}>${label}</button>`).join('')}</div></details></label><div class="ekom-payment-control"><span>Payment</span>${this.renderPaymentMenu(order, true)}</div><div class="ekom-fulfillment-control"><span>Fulfillment</span><input type="hidden" data-field="fulfillmentState" value="${escapeHtml(state?.fulfillmentStatus || '')}"><details class="ekom-lifecycle-menu ekom-fulfillment-menu"><summary aria-label="Fulfillment status" ${blocked || state?.orderStatus !== 'Open' ? 'aria-disabled="true"' : ''}>${this.renderStateDot(state?.fulfillmentStatus)}<span data-fulfillment-label>${escapeHtml((state?.fulfillmentStatus || 'Needs review').replace(/([a-z])([A-Z])/g, '$1 $2'))}</span><span class="ekom-status-chevron">▾</span></summary><div>${['Unfulfilled', 'Picking', 'Picked', 'Packed', 'PartiallyFulfilled', 'Fulfilled'].map(value => `<button type="button" data-action="choose-fulfillment" data-value="${value}" aria-pressed="${state?.fulfillmentStatus === value}" ${blocked || state?.orderStatus !== 'Open' ? 'disabled' : ''}>${this.renderStateDot(value)}${value.replace(/([a-z])([A-Z])/g, '$1 $2')}<span class="ekom-status-check">${state?.fulfillmentStatus === value ? '✓' : ''}</span></button>`).join('')}</div></details></div><label class="ekmCheckboxLabel"><input type="checkbox" data-field="notifyOrderStatus" ${this.notifyOrderStatus ? 'checked' : ''}>Trigger status events</label><span class="ekom-events-help"><button type="button" class="ekom-events-help-button" aria-label="About trigger status events" aria-describedby="ekom-events-tooltip">i</button><span class="ekom-events-tooltip" id="ekom-events-tooltip" role="tooltip"><strong>Trigger status events ("Fire events")</strong><span><b>On</b> — saves the status and runs whatever handlers are set up for it. What happens depends on the status and your setup, for example an email or an ERP update.</span><span><b>Off</b> — saves the status only. Use for manual corrections so external actions don't repeat.</span><small>Either way the status is saved and logged. It doesn't release stock or replace the payment flow.</small></span></span><button type="button" data-action="save-fulfillment" class="btn-success" ${blocked || state?.orderStatus !== 'Open' ? 'disabled' : ''}>${this.stateSaving ? 'Saving...' : 'Save'}</button></div>
      ${state?.requiresReview ? '<p class="status" role="status">Historical payment or fulfillment needs review before updating this order.</p>' : ''}
      ${this.configError ? `<p class="status status--error" role="alert">${escapeHtml(this.configError)}</p>` : ''}
      <span class="ekom-sr-only">Payment: ${escapeHtml(order.chargedAmount?.currencyString)}</span>${this.renderOrderLockStatus()}${this.renderOrderActions()}${this.renderGiftcardRecovery()}${this.renderReviews()}
      <div class="ekom-order-tabs" role="tablist" aria-label="Order sections">${tabs.map(tab => `<button type="button" id="ekom-order-tab-${tab.key}" role="tab" aria-selected="${this.orderTab === tab.key}" aria-controls="ekom-order-panel-${tab.key}" tabindex="${this.orderTab === tab.key ? 0 : -1}" data-action="select-order-tab" data-tab="${tab.key}">${tab.label}</button>`).join('')}</div>
      ${panel('order', orderContent)}${panel('activity', this.renderActivityLogs())}${panel('tracking', this.renderTracking(order) + this.renderConsent(order))}`;
  }

  private selectOrderTab(tab: string): void {
    if (!['order', 'activity', 'tracking'].includes(tab)) return;
    this.orderTab = tab;
    this.querySelectorAll<HTMLElement>('[role="tabpanel"]').forEach(panel => { panel.hidden = panel.id !== `ekom-order-panel-${tab}`; });
    this.querySelectorAll<HTMLButtonElement>('[data-action="select-order-tab"]').forEach(button => {
      const selected = button.dataset.tab === tab;
      button.setAttribute('aria-selected', String(selected)); button.tabIndex = selected ? 0 : -1;
      if (selected) button.focus();
    });
  }

  private async changeState(action: string): Promise<void> {
    const order = this.selectedOrder;
    if (!order?.status || this.stateSaving || !this.config || this.configError) return;
    if (action === 'Cancel' && !window.confirm('Cancel this order? This does not refund payments or reverse fulfillment.')) return;
    this.stateSaving = true;
    this.render();
    try {
      const status = await this.api.changeOrderStates(order.uniqueId, order.status.revision, action, this.notifyOrderStatus);
      if (this.selectedOrder?.uniqueId !== order.uniqueId) return;
      order.status = status;
      this.selectedOrder = await this.api.orderInfo(order.uniqueId);
      await Promise.all([this.loadOrderLogs(order.uniqueId), this.loadOrderActions(order.uniqueId)]);
      await this.loadOrders();
    } catch (error) {
      this.showError(getErrorMessage(error, 'Could not update the order. Reload it before trying again.'));
    } finally { this.stateSaving = false; this.render(); }
  }

  private renderOrderCustomData(order: OrderInfo): string {
    const entries = Object.entries(order.customData || {})
      .filter(([key, value]) => key.trim() && typeof value === 'string' && value.trim());
    if (!entries.length) return '';

    return `<dl class="ekmOrderCustomData" aria-label="Order custom data">${entries.map(([key, value]) => `
      <div class="ekmOrderCustomData__item">
        <dt class="ekmOrderCustomData__key">${escapeHtml(formatOrderCustomDataKey(key))}</dt>
        <dd class="ekmOrderCustomData__value">${escapeHtml(value)}</dd>
      </div>`).join('')}</dl>`;
  }

  private renderPaymentCard(order: OrderInfo): string {
    const items = (order.orderLines || []).reduce((count: number, line: Record<string, any>) => count + (Number(line.quantity) || 0), 0);
    const shippingName = order.shippingProvider?.title || order.shippingProvider?.properties?.nodeName || '';
    const row = (label: string, detail: string, amount: string, total = false): string => `<div${total ? ' class="ekom-money-total"' : ''}><dt>${escapeHtml(label)}</dt><dd class="ekom-money-description">${escapeHtml(detail)}</dd><dd>${escapeHtml(amount || '—')}</dd></div>`;
    return `<section class="ekom-detail-card ekom-payment-card"><h3>Payment ${renderStatusBadge(order.status?.paymentStatus)}</h3><dl class="ekom-money-summary">${row('Subtotal', `${items} ${items === 1 ? 'item' : 'items'}`, order.subTotal?.withVat?.currencyString)}${row('Discount', order.coupon || '—', order.discountAmount?.currencyString ? `${Number(order.discountAmount.value) ? '-' : ''}${order.discountAmount.currencyString}` : '')}${row('Shipping', shippingName, order.shippingProvider?.price?.withVat?.currencyString)}${row('Tax', 'Included in total', order.chargedVat?.currencyString)}${row('Total', '', order.chargedAmount?.currencyString, true)}</dl>${order.giftcards?.length ? `<div class="ekom-payment-breakdown">${this.renderGiftcards(order)}</div>` : ''}${this.renderPaymentDetails(order)}</section>`;
  }

  private renderPaymentDetails(order: OrderInfo): string {
    const rows = (fields: Array<{ label: string; value: string }>): string => `<dl>${fields.map(field => `<div><dt>${escapeHtml(field.label)}</dt><dd><span>${escapeHtml(field.value)}</span>${field.label !== 'Currency' && field.value && field.value !== 'Not recorded' && field.value !== '—' ? `<button type="button" class="ekom-payment-copy" data-action="copy-provider-value" data-copy="${escapeHtml(field.value)}" aria-label="Copy ${escapeHtml(field.label)}" title="Copy ${escapeHtml(field.label)}"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V4H4v12h4"/></svg></button>` : ''}</dd></div>`).join('')}</dl>`;
    const providerFields = parseProperties(order.paymentProvider?.customData, 'custompayment').map(([key, value]) => ({ label: labelFromKey(cleanKey(key)), value }));
    providerFields.push({ label: 'Paid date', value: order.paidDate ? formatDate(order.paidDate) : 'Not recorded' }, { label: 'Currency', value: order.currency || order.storeInfo?.currency?.isoCurrencySymbol || 'Not recorded' });
    return `<section class="ekom-payment-details" aria-label="Payment details"><div class="ekom-payment-detail-grid"><section class="ekom-payment-detail-box"><header><span class="ekom-payment-kind">Payment</span><h4>${escapeHtml(order.paymentProvider?.title || order.paymentProvider?.properties?.nodeName || 'Not recorded')}</h4>${renderStatusBadge(order.status?.paymentStatus)}</header>${rows(providerFields)}</section>${(order.giftcards || []).map((card: Record<string, any>) => {
      const fields = giftcardDetails(card).filter(field => field.label !== giftcardFieldLabel('code')).map(field => field.label === giftcardFieldLabel('amount') ? { ...field, value: this.formatGiftcardAmount(card.amount, order) } : field);
      return `<section class="ekom-payment-detail-box"><header><span class="ekom-payment-kind ekom-payment-kind-gift">Gift card</span><h4>${escapeHtml(card.code)}</h4><span class="ekom-gift-state">${escapeHtml(giftcardSummary(card).status)}</span></header>${rows(fields)}</section>`;
    }).join('')}</div></section>`;
  }

  private renderShipment(order: OrderInfo): string {
    const provider = order.shippingProvider;
    const name = provider?.title || provider?.properties?.nodeName || 'No shipping provider';
    const fields = parseProperties(provider?.customData, 'customshipping');
    const value = (key: string): string => fields.find(([field]) => cleanKey(field).replace(/[_ -]/g, '').toLowerCase() === key.toLowerCase())?.[1] || '';
    const tracking = value('TrackingNumber');
    const shipmentState = value('ShipmentState');
    const trackingUrl = value('TrackingUrl');
    const safeTrackingUrl = /^https?:\/\//i.test(trackingUrl) ? trackingUrl : '';
    const customer = order.customerInformation?.customer || {};
    const shipping = order.customerInformation?.shipping || {};
    const address = hasShippingInfo(shipping) ? shipping : customer;
    const copy = (text: string, label: string): string => `<button type="button" class="ekom-shipment-copy" data-action="copy-provider-value" data-copy="${escapeHtml(text)}" aria-label="Copy ${escapeHtml(label)}" title="Copy ${escapeHtml(label)}"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V4H4v12h4"/></svg></button>`;
    return `<section class="ekom-detail-card ekom-shipment"><div class="ekom-shipment-grid"><section class="ekom-shipment-box"><div class="ekom-shipment-heading"><h4>Shipment</h4>${shipmentState ? `<span class="ekom-shipment-state">${escapeHtml(shipmentState)}</span>` : ''}</div><div class="ekom-shipment-tracking"><span>Tracking number</span><div><strong>${escapeHtml(tracking || 'Not available')}</strong>${tracking ? copy(tracking, 'tracking number') : ''}${provider ? `<button type="button" class="ekom-shipment-copy" data-action="open-shipping-provider-editor" aria-label="Edit shipping data" title="Edit shipping data"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><path d="M12 20h9M16 3l5 5L7 22l-5 1 1-5Z"/></svg></button>` : ''}${safeTrackingUrl ? `<a href="${escapeHtml(safeTrackingUrl)}" target="_blank" rel="noopener noreferrer">Track shipment ↗</a>` : ''}</div></div></section><section class="ekom-shipment-box"><div class="ekom-shipment-heading"><h4>Method</h4><strong>${escapeHtml(provider?.price?.withVat?.currencyString || '—')}</strong></div><h3>${escapeHtml(name)}</h3><div class="ekom-shipment-address"><span>Deliver to</span><strong>${escapeHtml([address.address, address.apartment].filter(Boolean).join(', ') || 'No address recorded')}</strong><span>${escapeHtml([address.zipCode, address.city].filter(Boolean).join(' '))}</span><span>${escapeHtml(address.country)}</span></div><div><button type="button" data-action="open-shipping-provider-editor">${provider ? 'Change shipping provider' : 'Add shipping provider'}</button></div></section></div>${fields.length ? `<details class="ekom-shipment-data" open><summary><strong>Provider data</strong><span>${fields.length} fields from ${escapeHtml(name)}</span><span class="ekom-shipment-hide">Hide ▴</span><span class="ekom-shipment-show">Show ▾</span></summary><dl>${fields.map(([key, text]) => `<div><dt>${escapeHtml(labelFromKey(cleanKey(key)))}</dt><dd><span>${escapeHtml(text || '—')}</span>${text ? copy(text, labelFromKey(cleanKey(key))) : ''}</dd></div>`).join('')}</dl></details>` : ''}</section>`;
  }

  private renderPrintableOrder(order: OrderInfo): string {
    const customer = order.customerInformation?.customer || {};
    const shipping = order.customerInformation?.shipping || {};
    const statusValue = this.getOrderStatusValue(order.orderStatus);
    const status = managerState.statusList.find(item => getStatusValue(item) === statusValue)?.label || statusValue;
    return `
      <main class="ekmOrder">
        <h1>Order number: ${escapeHtml(order.referenceId)}</h1>
        <p>Order status: ${escapeHtml(order.status?.orderStatus || status)}</p>
        ${order.status ? `<p>Payment status: ${escapeHtml(order.status.paymentStatus.replace(/([a-z])([A-Z])/g, '$1 $2'))} · Fulfillment: ${escapeHtml(order.status.fulfillmentStatus.replace(/([a-z])([A-Z])/g, '$1 $2'))}</p>` : ''}
        <p>UniqueId: ${escapeHtml(order.uniqueId)}</p>
        <p>Created date: ${escapeHtml(formatDate(order.createDate))}</p>
        <p>Paid date: ${escapeHtml(formatDate(order.paidDate))}</p>
        <p>Store: ${escapeHtml(order.storeInfo?.alias || order.storeAlias)}</p>
        <p>Payment: <strong>${escapeHtml(order.chargedAmount?.currencyString)}</strong></p>
        ${this.renderOrderCustomData(order)}
        <section class="ekmSplit">
          <div class="ekmSplit__column"><h4>Billing</h4>${this.renderAddress(customer)}${this.renderExtraProperties(customer.properties, 'customer')}</div>
          <div class="ekmSplit__column"><h4>Shipping</h4>${hasShippingInfo(shipping) ? `${this.renderAddress(shipping)}${this.renderExtraProperties(shipping.properties, 'shipping')}` : '<p>Same as billing address</p>'}</div>
        </section>
        <section class="ekmSplit">
          <div class="ekmSplit__column">${this.renderProvider('Payment Method', order.paymentProvider, 'custompayment')}</div>
          <div class="ekmSplit__column">${this.renderProvider('Shipping Method', order.shippingProvider, 'customshipping')}</div>
        </section>
        ${this.renderGiftcards(order)}
        ${this.renderOrderLines(order)}
      </main>`;
  }

  private renderAddress(info: Record<string, any>): string {
    return ['name', 'email', 'address', 'apartment', 'city', 'country', 'zipCode', 'phone']
      .filter(key => info[key])
      .map(key => `<p>${labelFromKey(key)}: ${escapeHtml(decodeHtml(info[key]))}</p>`)
      .join('');
  }

  private renderExtraProperties(properties: Record<string, unknown> | undefined, prefix: string): string {
    const entries = parseProperties(properties, prefix);

    if (!entries.length) {
      return '';
    }

    return `<h5 style="margin-top:20px; font-weight:bold;">${prefix === 'customshipping' ? 'Shipping Provider Custom' : `Extra ${prefix === 'shipping' ? 'Shipping' : 'Customer'}`} Data</h5><ul>${entries.map(([key, value]) => `<li><strong>${escapeHtml(cleanKey(key))}</strong>: ${escapeHtml(value)}</li>`).join('')}</ul>`;
  }

  private renderProvider(title: string, provider: Record<string, any> | undefined, prefix: string): string {
    if (!provider) {
      return '';
    }

    const providerTitle = typeof provider.title === 'string' && provider.title.trim()
      ? provider.title
      : provider.properties?.nodeName;

    return `<h4>${escapeHtml(title)}</h4><h4><strong>${escapeHtml(providerTitle)}</strong></h4>${provider.price ? `<p>Price: ${escapeHtml(provider.price.withVat?.currencyString)}</p>` : ''}${this.renderExtraProperties(provider.customData, prefix)}`;
  }

  private renderGiftcards(order: OrderInfo): string {
    const giftcards = Array.isArray(order.giftcards) ? order.giftcards : [];
    if (!giftcards.length) {
      return '';
    }

    return `<div class="ekmOrderGiftcards"><h4>Gift cards</h4><ul class="ekmOrderGiftcards__list">${giftcards.map((card: Record<string, any>) => {
      const summary = giftcardSummary(card);
      const fields = giftcardDetails(card);
      return `<li><div class="ekmOrderGiftcards__summary"><strong>${escapeHtml(card.code)}</strong><strong>${escapeHtml(this.formatGiftcardAmount(card.amount, order))}</strong><span>${escapeHtml(summary.status)}</span></div>
        <p>${escapeHtml(summary.expiryLabel)}: ${escapeHtml(summary.expiry)}</p>
        ${summary.warning ? `<p role="alert">${escapeHtml(summary.warning)}</p>` : ''}
        <div class="ekmOrderGiftcards__actions">${this.orderActions.filter(action => action.giftcardCode != null && action.giftcardCode === card.code).map(action => this.renderAction(action)).join('')}</div>
        <details><summary>More details</summary><dl>${fields.map(field => `<dt>${escapeHtml(field.label)}</dt><dd>${escapeHtml(field.value)}</dd>`).join('')}</dl></details></li>`;
    }).join('')}</ul></div>`;
  }

  private formatGiftcardAmount(amount: number, order: OrderInfo): string {
    const locale = order.storeInfo?.currency?.currencyValue || 'en-US';
    const currency = order.storeInfo?.currency?.isoCurrencySymbol || order.currency;
    try {
      return new Intl.NumberFormat(locale, { style: 'currency', currency }).format(amount);
    } catch {
      return String(amount);
    }
  }

  private get recoveryBusy(): boolean {
    return !!this.recoveryOperation || this.recoveryLoading || this.recoveryError || this.recoveryRefreshError
      || !!this.executingActionKey || this.releasingOrderLock || this.orderLockLoading || this.orderLockError
      || this.orderActionsLoading || this.orderActionsError;
  }

  private renderGiftcardRecovery(): string {
    if (this.recoveryAttempts?.length === 0 && !this.recoveryLoading && !this.recoveryError
      && !this.recoveryOperation && !this.recoveryMessage && !this.recoveryActionError
      && !this.recoveryRefreshError && !this.orderActionsError) {
      return '';
    }

    const field = (label: string, value: unknown): string => `<dt>${escapeHtml(label)}</dt><dd>${escapeHtml(value ?? 'Not recorded')}</dd>`;
    return `<section class="ekmOrderTracking ekmGiftcardRecovery"><h4>Failed gift-card preparation</h4>
      <p>Recovery history for failed checkout preparation, not accepted payment. These actions do not refund a payment or retry checkout.</p>
      ${this.recoveryMessage ? `<p role="status">${escapeHtml(this.recoveryMessage)}</p>` : ''}
      ${this.recoveryActionError ? `<p role="alert">${escapeHtml(this.recoveryActionError)}</p>` : ''}
      ${this.recoveryRefreshError ? '<p role="alert">We could not refresh all order details. Reopen the order before taking another recovery action.</p>' : ''}
      ${this.orderActionsError ? '<p role="alert">We could not refresh order actions. Recovery actions are unavailable. Reopen the order to try again.</p>' : ''}
      ${this.recoveryLoading ? '<p>Loading recovery history...</p>' : ''}
      ${this.recoveryError ? '<p role="alert">We could not load gift-card recovery history. Recovery actions are unavailable. Reopen the order to try again.</p>' : ''}
      ${this.recoveryAttempts === undefined && !this.recoveryLoading && !this.recoveryError ? '<p>Recovery history has not been checked. Recovery actions are unavailable.</p>' : ''}
      ${(this.recoveryAttempts || []).map(attempt => {
        const canReconcile = attempt.canReconcile && !!attempt.reconcileToken;
        return `<article><h5>Preparation attempt: ${escapeHtml(attempt.attemptId)}</h5>
          <dl>${field('Created', formatDate(attempt.createdUtc))}${field('Recovery state', attempt.state)}${field('Reason', attempt.reason)}</dl>
          ${attempt.hasUnknownEffects ? '<p role="alert">Unknown provider effects need reconciliation. A missing claim reference is not evidence that no hold exists. Do not release by card number or amount.</p>' : ''}
          ${attempt.claims.map(claim => {
            const canRelease = claim.canRelease && !!claim.releaseToken;
            return `<div class="ekmGiftcardRecovery__claim"><dl>${field('Masked card', claim.cardReference)}${field('Claim reference', claim.claimReference)}${field('Provider', claim.provider)}${field('Account', claim.account)}${field('Requested amount', `${claim.requestedAmount} ${claim.currency}`)}${field('Raw returned amount', claim.rawReturnedAmount)}${field('Returned amount type', claim.returnedAmountType)}${field('Raw expiry', claim.rawExpiry)}${field('Cleanup status', claim.cleanupState)}</dl>
              ${claim.claimReference ? `<button type="button" class="btn-outline" data-action="release-giftcard-recovery" data-attempt-id="${escapeHtml(attempt.attemptId)}" data-claim-id="${escapeHtml(claim.id)}" ${!canRelease || this.recoveryBusy ? 'disabled' : ''}>Release failed-preparation hold${this.recoveryOperation === `release:${attempt.attemptId}:${claim.id}` ? ' (Working...)' : ''}</button>` : '<p>Missing claim reference: needs reconciliation. No release action is available.</p>'}
              <p>${escapeHtml(canRelease && claim.claimReference ? 'Release allowed after confirmation.' : claim.unavailableReason || 'Release unavailable; check reconciliation requirements.')}</p></div>`;
          }).join('')}
          ${!attempt.claims.length && attempt.hasUnknownEffects ? '<p>No known claim to release. Reconcile unknown provider effects.</p>' : ''}
          <button type="button" class="btn-outline" data-action="reconcile-giftcard-recovery" data-attempt-id="${escapeHtml(attempt.attemptId)}" ${!canReconcile || this.recoveryBusy ? 'disabled' : ''}>Reconcile failed preparation${this.recoveryOperation === `reconcile:${attempt.attemptId}` ? ' (Working...)' : ''}</button>
          <p>${escapeHtml(canReconcile ? 'Reconciliation allowed after confirmation.' : attempt.unavailableReason || 'Reconciliation unavailable.')}</p>
        </article>`;
      }).join('')}</section>`;
  }

  private renderDesignOrderLines(order: OrderInfo): string {
    const lines = Array.isArray(order.orderLines) ? order.orderLines : [];
    const itemCount = lines.reduce((total: number, line: Record<string, any>) => total + (Number(line.quantity) || 0), 0);
    return `<div class="ekom-card-heading ekom-lines-heading"><h3>Order lines</h3><span>${lines.length} ${lines.length === 1 ? 'line' : 'lines'} · ${itemCount} ${itemCount === 1 ? 'item' : 'items'}</span><button type="button" data-action="open-order-line-editor">Add order line</button></div><div class="ekom-lines-scroll"><table class="ekom-design-lines"><thead><tr><th>Product</th><th>Quantity</th><th>Unit price</th><th>Discount</th><th>Total</th><th><span class="ekom-sr-only">Actions</span></th></tr></thead><tbody>${lines.map((line: Record<string, any>) => `<tr><td><div class="ekmOrderLineProduct">${renderOrderLineThumbnail(line)}<div>${renderOrderLineProductTitle(line.product)}<small>${escapeHtml(line.product?.sku)}</small>${line.variant ? `<small>${escapeHtml(line.variant.title)}</small>` : ''}${renderOrderLineProperties(line)}</div></div></td><td>${escapeHtml(line.quantity)}</td><td>${escapeHtml(line.product?.price?.withVat?.currencyString)}</td><td>${escapeHtml(line.amount?.discountAmount?.currencyString)}</td><td><strong>${escapeHtml(line.amount?.withVat?.currencyString)}</strong></td><td><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${escapeHtml(line.key)}" data-product-title="${escapeHtml(line.product?.title)}" ${this.removingOrderLineId === line.key ? 'disabled' : ''} aria-label="Remove ${escapeHtml(line.product?.title)}">×</button></td></tr>`).join('')}</tbody></table></div>`;
  }

  private renderOrderLines(order: OrderInfo): string {
    const lines = Array.isArray(order.orderLines) ? order.orderLines : [];
    const coupon = typeof order.coupon === 'string' ? order.coupon.trim() : '';
    const discountLabel = coupon ? `Discount (${coupon})` : 'Discount';
    return `
      <div style="align-items:center; display:flex; gap:10px; justify-content:space-between;"><h4>Order Details</h4><button type="button" class="btn-outline" data-action="open-order-line-editor">Add order line</button></div>
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed">Product</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Unit Price (inc VAT)</div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">Total (inc VAT)</div></div></div>
        <div class="umb-table-body">
          ${lines.map((line: Record<string, any>) => `<div class="umb-table-row"><div class="umb-table-cell"><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${escapeHtml(line.key)}" data-product-title="${escapeHtml(line.product?.title)}" ${this.removingOrderLineId === line.key ? 'disabled' : ''} aria-label="Remove ${escapeHtml(line.product?.title)}" title="Remove order line">&#128465;</button></div><div class="umb-table-cell not-fixed"><div class="ekmOrderLineProduct">${renderOrderLineThumbnail(line)}<div class="ekmOrderLineProduct__details"><span>${renderOrderLineProductTitle(line.product)}${line.product?.sku?.trim() ? ` (${escapeHtml(line.product.sku)})` : ''}</span>${line.variant ? `<small style="display:block; margin-top:3px;">${escapeHtml(line.variant.title)} ${line.variant.sku?.trim() ? `(${escapeHtml(line.variant.sku)})` : ''}</small>` : ''}${renderOrderLineProperties(line)}</div></div></div><div class="umb-table-cell">${escapeHtml(line.quantity)}</div><div class="umb-table-cell">${escapeHtml(line.product?.price?.withVat?.currencyString)}</div><div class="umb-table-cell">${escapeHtml(line.amount?.vat?.currencyString)}</div><div class="umb-table-cell">-${escapeHtml(line.amount?.discountAmount?.currencyString)}</div><div class="umb-table-cell"><strong>${escapeHtml(line.amount?.withVat?.currencyString)}</strong></div></div>`).join('')}
        </div>
        <div class="umb-table-footer">
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Sub Total (inc VAT)</div><div class="umb-table-cell">${escapeHtml(order.subTotal?.withVat?.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">${escapeHtml(discountLabel)}</div><div class="umb-table-cell">-${escapeHtml(order.discountAmount?.currencyString)}</div></div>
          ${order.shippingProvider ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Shipping Total</div><div class="umb-table-cell">${escapeHtml(order.shippingProvider.price?.withVat?.currencyString)}</div></div>` : ''}
          ${this.renderGiftcardTotal(order)}
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">${escapeHtml(order.chargedVat?.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Total</div><div class="umb-table-cell"><strong>${escapeHtml(order.chargedAmount?.currencyString)}</strong></div></div>
        </div>
      </div>
    `;
  }

  private renderGiftcardTotal(order: OrderInfo): string {
    const giftcards = Array.isArray(order.giftcards) ? order.giftcards : [];
    const eligibleValue = giftcards.reduce((total: number, card: Record<string, any>) =>
      total + (card.amount > 0 && (card.claimed || !card.validUntil || Date.parse(card.validUntil) > Date.now()) ? card.amount : 0), 0);
    if (!eligibleValue) {
      return '';
    }

    return `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell" title="Payable total already includes applicable gift cards; eligible value may exceed the order total.">Gift cards (eligible value)</div><div class="umb-table-cell">${escapeHtml(this.formatGiftcardAmount(eligibleValue, order))}</div></div>`;
  }

  private renderTracking(order: OrderInfo): string {
    const tracking = order.tracking;

    if (!hasTrackingData(tracking)) {
      return '<div class="ekmOrderTracking"><h4>Tracking</h4><p>No tracking data was captured for this order.</p></div>';
    }

    return `<div class="ekmOrderTracking"><h4>Tracking</h4>${renderTrackingDetails(tracking)}</div>`;
  }

  private renderConsent(order: OrderInfo): string {
    const consent = order.consent;

    if (!hasConsentData(consent)) {
      return '<div class="ekmOrderTracking"><h4>Consent</h4><p>No consent data was captured for this order.</p></div>';
    }

    return `<div class="ekmOrderTracking"><h4>Consent</h4>${renderOptional('Resolved', formatDate(consent.resolvedAtUtc))}${renderOptional('Source', consent.source)}${renderOptional('Analytics', consentLabel(consent.analytics))}${renderOptional('Marketing', consentLabel(consent.marketing))}</div>`;
  }

  private renderActivityLogs(): string {
    if (this.orderLogsLoading) {
      return '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Loading activity...</p></div>';
    }

    if (this.orderLogsError) {
      return '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Unable to load activity log.</p></div>';
    }

    if (!this.orderLogs.length) {
      return '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>No activity yet.</p></div>';
    }

    const activityLogs = this.orderLogs.map((log, index) => {
      const message = log.message ?? '';
      const isExpanded = this.activityLogExpandedIndexes.has(index);
      const toggle = this.canExpandActivityLog(message)
        ? `<button type="button" class="btn-reset ekmOrderActivityLog__toggle" data-action="toggle-activity-log" data-log-index="${index}">${isExpanded ? 'Show less' : 'Show more'}</button>`
        : '';

      return `<tr><td class="ekmOrderActivityLog__date">${escapeHtml(formatDate(log.date))}</td><td><span class="ekom-log-type ${this.getActivityLogTypeClass(log)}">${log.logType === activityLogTypeSuccess ? 'Success' : log.logType === activityLogTypeAlert ? 'Alert' : 'Info'}</span></td><td><div class="ekmOrderActivityLog__message${isExpanded ? ' ekmOrderActivityLog__message--expanded' : ''}">${escapeHtml(message)}</div>${toggle}</td></tr>`;
    }).join('');

    return `<div class="ekmOrderActivityLog"><h4>Activity log</h4><div class="ekom-log-scroll"><table class="ekom-log-table"><thead><tr><th>Date</th><th>Type</th><th>Message</th></tr></thead><tbody>${activityLogs}</tbody></table></div></div>`;
  }

  private getActivityLogTypeClass(log: OrderActivityLog): string {
    switch (log.logType) {
      case activityLogTypeSuccess:
        return 'ekmOrderActivityLog__icon--success';
      case activityLogTypeAlert:
        return 'ekmOrderActivityLog__icon--alert';
      default:
        return 'ekmOrderActivityLog__icon--info';
    }
  }

  private canExpandActivityLog(message: string): boolean {
    return message.length > activityLogPreviewCharacterLimit;
  }

  private renderStateDot(value?: string): string {
    return `<span class="ekom-state-dot" data-state="${escapeHtml(value || '')}" aria-hidden="true"></span>`;
  }

  private renderOrderActions(): string {
    const actions = this.orderActions.filter(action => action.giftcardCode == null);
    return `<div class="ekom-detail-actions"><button type="button" data-action="print-order" class="ekom-print-button" aria-label="Print order" title="Print order"><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 9V3h12v6"></path><rect x="3" y="9" width="18" height="8" rx="2"></rect><path d="M6 14h12v7H6z"></path></svg></button>${actions.length ? '<span class="ekom-action-divider" aria-hidden="true"></span>' : ''}${actions.map(action => this.renderAction(action)).join('')}${this.orderActionsLoading ? '<span role="status">Loading actions...</span>' : ''}</div>`;
  }

  private renderAction(action: OrderAction): string {
    const executing = this.executingActionKey === action.key;
    return `<span style="display:inline-block; margin:4px;"><button type="button" class="${action.look === 'primary' ? 'btn-success' : 'btn-outline'}" data-action="execute-order-action" data-action-key="${escapeHtml(action.key)}" ${action.enabled === false || this.executingActionKey || this.releasingOrderLock || this.recoveryOperation ? 'disabled' : ''}>${escapeHtml(action.label)}${executing ? ' (Working...)' : ''}</button>${action.enabled === false && action.disabledReason ? `<small style="display:block;">${escapeHtml(action.disabledReason)}</small>` : ''}</span>`;
  }

  private renderOrderLockStatus(): string {
    const status = this.orderLockStatus;
    const warning = this.orderLockWarning ? `<p role="alert">${escapeHtml(this.orderLockWarning)}</p>` : '';
    const error = this.orderLockError ? '<p role="alert">We could not check whether checkout is locked. Reopen the order to try again.</p>' : '';
    const releaseError = this.orderLockReleaseError ? `<p role="alert">${escapeHtml(this.orderLockReleaseError)}</p>` : '';
    if (!status?.hasLock) {
      return `${warning}${error}${releaseError}${this.orderLockLoading ? '<p>Checking checkout...</p>' : ''}`;
    }

    const unavailable = !status.canRelease || !status.releaseToken ? status.releaseUnavailableReason || 'This checkout lock cannot be released right now.' : '';
    return `<section class="ekmOrderTracking"><h4>Checkout is locked</h4>${warning}${error}${releaseError}<p>${status.reconciliationRequired && !status.paymentOperationLocked && !status.preparationLocked ? 'The previous payment needs checking before the customer can pay or edit their basket.' : 'A previous checkout did not finish. This prevents the customer from paying or editing their basket.'}</p><p>If you have checked that no payment is being processed, Release Lock resets checkout and lets the customer try again.</p><button type="button" class="btn-outline" data-action="release-order-lock" ${unavailable || this.orderLockLoading || this.orderLockError || this.releasingOrderLock || this.executingActionKey || this.recoveryOperation ? 'disabled' : ''}>Release Lock</button>${unavailable ? `<p>${escapeHtml(unavailable)}</p>` : ''}<details><summary>Technical details</summary><ul>${status.paymentOperationLocked ? '<li>Payment operation ownership retained.</li>' : ''}${status.preparationLocked ? '<li>Checkout preparation ownership retained.</li>' : ''}${status.reconciliationRequired ? '<li>Reconciliation required.</li>' : ''}</ul><p>Attempt ID: ${escapeHtml(status.attemptId || 'Not available')}</p><p>Attempt state: ${escapeHtml(status.attemptState || 'Not available')}</p></details></section>`;
  }

  private renderCustomerEditor(): string {
    const model = this.customerEditModel;

    if (!model) {
      return '';
    }

    return `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel ekom-editor-customer" role="dialog" aria-modal="true" aria-label="Edit customer information"><div class="ekmOverlay__header"><h3>Edit customer information</h3><button class="btn-reset" type="button" aria-label="Close edit customer information" data-action="close-customer-editor" ${this.customerSaving ? 'disabled' : ''}>&times;</button></div><div class="ekmOverlay__content"><div class="ekmSplit"><div class="ekmSplit__column"><h4>Billing</h4>${this.renderCustomerFields(model.customer, 'customer')}</div><div class="ekmSplit__column"><h4>Shipping</h4>${this.renderCustomerFields(model.shipping, 'shipping')}</div></div></div><div class="ekom-editor-footer"><button class="btn-outline" type="button" data-action="close-customer-editor" ${this.customerSaving ? 'disabled' : ''}>Cancel</button><button class="btn-success" type="button" data-action="save-customer-information" ${this.customerSaving ? 'disabled' : ''}>${this.customerSaving ? 'Saving...' : 'Save customer information'}</button></div></div></div>`;
  }

  private renderCustomerFields(fields: CustomerInformationField[], group: string): string {
    return fields.map(field => `<label class="control-group">${escapeHtml(field.label)} ${field.isExtra ? `<small>(${escapeHtml(field.key)})</small>` : ''}<input type="text" data-customer-group="${group}" data-customer-key="${escapeHtml(field.key)}" value="${escapeHtml(field.value)}" ${this.customerSaving ? 'disabled' : ''}></label>`).join('');
  }

  private renderOrderLineEditor(): string {
    const model = this.orderLineEditModel;

    if (!model) {
      return '';
    }

    return `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel ekom-editor-order-line" role="dialog" aria-modal="true" aria-label="Add order line"><div class="ekmOverlay__header"><h3>Add order line</h3><button class="btn-reset" type="button" aria-label="Close add order line" data-action="close-order-line-editor" ${this.orderLineSaving ? 'disabled' : ''}>&times;</button></div><div class="ekmOverlay__content"><label class="control-group">Product ID<input type="text" data-order-line-field="productId" value="${escapeHtml(model.productId)}" required ${this.orderLineSaving ? 'disabled' : ''}></label><label class="control-group">Variant ID<input type="text" data-order-line-field="variantId" value="${escapeHtml(model.variantId)}" ${this.orderLineSaving ? 'disabled' : ''}></label><label class="control-group" style="padding-bottom:20px;">Quantity<input type="number" min="0.000001" step="any" data-order-line-field="quantity" value="${escapeHtml(model.quantity)}" required ${this.orderLineSaving ? 'disabled' : ''}></label></div><div class="ekom-editor-footer"><button class="btn-outline" type="button" data-action="close-order-line-editor" ${this.orderLineSaving ? 'disabled' : ''}>Cancel</button><button class="btn-success" type="button" data-action="save-order-line" ${this.orderLineSaving ? 'disabled' : ''}>${this.orderLineSaving ? 'Adding...' : 'Add order line'}</button></div></div></div>`;
  }

  private renderShippingProviderEditor(): string {
    return `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel ekom-editor-shipping-provider" role="dialog" aria-modal="true" aria-label="Shipping provider"><div class="ekmOverlay__header"><h3>${this.selectedOrder?.shippingProvider ? 'Change shipping provider' : 'Add shipping provider'}</h3><button class="btn-reset" type="button" aria-label="Close shipping provider" data-action="close-shipping-provider-editor" ${this.shippingProviderSaving ? 'disabled' : ''}>&times;</button></div><div class="ekmOverlay__content"><label class="control-group">Shipping provider<select data-field="shippingProviderId" ${this.shippingProviderSaving ? 'disabled' : ''}><option value="">Select a provider</option>${this.shippingProviders.map(provider => `<option value="${escapeHtml(provider.key)}" ${this.shippingProviderId === provider.key ? 'selected' : ''}>${escapeHtml(provider.title)}</option>`).join('')}</select></label>${this.shippingProviders.length ? '' : '<p>No shipping providers available for this store.</p>'}<div class="ekom-provider-data-heading"><h4>Provider data</h4><p>Stored on the order. Keys are kept as-is; new keys are prefixed <code>customshipping</code>.</p></div><div class="ekom-provider-field-labels" aria-hidden="true"><span>Key</span><span>Value</span></div><div class="ekom-provider-fields">${this.shippingCustomFields.map((field, index) => `<div class="ekom-provider-field-row"><input aria-label="Key ${index + 1}" type="text" data-shipping-key="${index}" value="${escapeHtml(field.key)}" ${field.existing ? 'readonly' : ''} ${this.shippingProviderSaving ? 'disabled' : ''} placeholder="TrackingNumber" maxlength="100"><input aria-label="Value ${index + 1}" type="text" data-shipping-value="${index}" value="${escapeHtml(field.value)}" ${this.shippingProviderSaving ? 'disabled' : ''} maxlength="4096"></div>`).join('')}</div><button class="btn-outline" type="button" data-action="add-shipping-custom-field" ${this.shippingProviderSaving ? 'disabled' : ''}>Add custom field</button></div><div class="ekom-editor-footer"><p>Changes apply to this order only.</p><button class="btn-outline" type="button" data-action="close-shipping-provider-editor" ${this.shippingProviderSaving ? 'disabled' : ''}>Cancel</button><button class="btn-success" type="button" data-action="save-shipping-provider" ${this.shippingProviderSaving || !this.shippingProviders.length ? 'disabled' : ''}>${this.shippingProviderSaving ? 'Saving...' : 'Save shipping provider'}</button></div></div></div>`;
  }

  private readShippingCustomFields(): void {
    this.shippingCustomFields = this.shippingCustomFields.map((field, index) => ({
      key: field.existing ? field.key : this.querySelector<HTMLInputElement>(`[data-shipping-key="${index}"]`)?.value || '',
      value: this.querySelector<HTMLInputElement>(`[data-shipping-value="${index}"]`)?.value || '',
      existing: field.existing,
    }));
  }

  private bindEvents(): void {
    this.querySelector<HTMLTextAreaElement>('[data-review-comment]')?.addEventListener('input', event => {
      this.reviewComment = (event.target as HTMLTextAreaElement).value;
    });
    this.querySelector('.ekom-review-overlay')?.addEventListener('click', event => {
      if (event.target === event.currentTarget && !this.reviewSaving) { this.activeReview = undefined; this.render(); }
    });
    this.querySelector<HTMLInputElement>('[data-review-resolved]')?.addEventListener('change', event => {
      this.reviewResolved = (event.target as HTMLInputElement).checked;
      this.reviewComment = this.querySelector<HTMLTextAreaElement>('[data-review-comment]')?.value || '';
      const button = this.querySelector('[data-action="save-review"]');
      if (button) button.textContent = this.reviewResolved ? 'Mark as reviewed' : 'Save note';
    });
    this.querySelectorAll<HTMLDetailsElement>('.ekom-row-fulfillment').forEach(details => {
      details.addEventListener('toggle', () => {
        if (!details.open) return;
        const menu = details.querySelector<HTMLElement>('.ekom-row-fulfillment-menu')!;
        menu.style.top = ''; menu.style.bottom = ''; menu.style.maxHeight = ''; menu.style.overflowY = 'auto';
        const anchor = details.getBoundingClientRect();
        const below = window.innerHeight - anchor.bottom - 22;
        const above = anchor.top - 86;
        if (menu.offsetHeight > below && above > below) {
          menu.style.top = 'auto'; menu.style.bottom = 'calc(100% + 6px)';
          menu.style.maxHeight = `${Math.max(120, above)}px`;
        } else { menu.style.maxHeight = `${Math.max(120, below)}px`; }
      });
      details.addEventListener('keydown', event => {
        if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); details.open = false; details.querySelector('summary')?.focus(); }
      });
    });
    this.querySelectorAll<HTMLButtonElement>('[data-poll-seconds]').forEach(button => button.addEventListener('click', () => this.setPolling(Number(button.dataset.pollSeconds))));
    this.querySelector('.ekom-polling')?.addEventListener('keydown', event => {
      if ((event as KeyboardEvent).key !== 'Escape') return;
      const details = event.currentTarget as HTMLDetailsElement;
      details.open = false;
      details.querySelector('summary')?.focus();
      event.stopPropagation();
    });
    bindDateRange(this, () => { this.page = 1; void this.loadOrders(); });
    this.querySelectorAll<HTMLButtonElement>('[data-action="select-order-tab"]').forEach(button => {
      button.addEventListener('keydown', event => {
        if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
        event.preventDefault();
        const tabs = ['order', 'activity', 'tracking'];
        const index = tabs.indexOf(this.orderTab);
        const next = event.key === 'Home' ? 0 : event.key === 'End' ? tabs.length - 1 : (index + (event.key === 'ArrowRight' ? 1 : -1) + tabs.length) % tabs.length;
        this.selectOrderTab(tabs[next]);
      });
    });
    this.querySelectorAll<HTMLImageElement>('.ekmOrderLineThumbnail img').forEach(image => {
      const showPlaceholder = () => { image.hidden = true; };
      image.addEventListener('error', showPlaceholder, { once: true });
      if (image.complete && image.naturalWidth === 0) showPlaceholder();
    });
    if (this.overlay === 'order') {
      this.querySelector('.ekmOverlay')?.addEventListener('click', event => {
        if (event.target === event.currentTarget && !this.customerEditorOpen && !this.orderLineEditorOpen && !this.shippingProviderEditorOpen && !this.activeReview) {
          this.closeOverlay();
        }
      });
    }

    this.querySelectorAll('[data-action]').forEach(element => {
      if (element instanceof HTMLSelectElement) {
        element.addEventListener('change', event => void this.handleAction(event));
      } else {
        element.addEventListener('click', event => void this.handleAction(event));
      }
    });

    this.querySelectorAll('[data-field]').forEach(element => {
      element.addEventListener('change', event => void this.handleFieldChange(event));
      if ((element as HTMLElement).dataset.field === 'query') {
        element.addEventListener('input', event => this.handleSearchInput(event));
      }
    });
  }

  private async handleAction(event: Event): Promise<void> {
    const target = event.currentTarget as HTMLElement;
    const action = target.dataset.action;
    if (action === 'open-review') {
      this.activeReview = this.reviews.find(review => review.id === target.dataset.reviewId);
      this.reviewComment = ''; this.reviewResolved = true; this.reviewError = ''; this.render();
      this.querySelector<HTMLTextAreaElement>('[data-review-comment]')?.focus(); return;
    }
    if (action === 'close-review') { if (!this.reviewSaving) { this.activeReview = undefined; this.render(); } return; }
    if (action === 'save-review') { await this.saveReview(); return; }

    if (action === 'select-order-view') { managerState.filters.orderStatus = target.dataset.view || 'AllOrders'; this.page = 1; await this.loadOrders(); return; }
    if (action === 'clear-order-filters') { managerState.filters.orderStatus = 'AllOrders'; managerState.filters.query = ''; this.page = 1; await this.loadOrders(); return; }
    if (action === 'order-state-command') { await this.changeState(target.dataset.command || ''); return; }
    if (action === 'copy-provider-value') {
      try {
        await navigator.clipboard.writeText(target.dataset.copy || '');
        this.showSuccess('Copied to clipboard.');
      } catch { this.showError('Could not copy this value. Select the text to copy it manually.'); }
      return;
    }
    if (action === 'copy-order-id') {
      const orderId = this.selectedOrder?.uniqueId;
      if (!orderId) return;
      try {
        await navigator.clipboard.writeText(orderId);
        this.showNotification('positive', 'Copied', 'Order ID copied to clipboard.');
      } catch {
        this.showError('Could not copy the order ID. You can select and copy it from the heading.');
      }
      return;
    }
    if (action === 'set-payment') { await this.changePayment(target as HTMLButtonElement); return; }
    if (action === 'set-row-fulfillment') { await this.changeRowFulfillment(target as HTMLButtonElement); return; }
    if (action === 'choose-fulfillment') {
      if ((target as HTMLButtonElement).disabled) return;
      const menu = target.closest('details')!;
      const value = target.dataset.value || '';
      const input = this.querySelector<HTMLInputElement>('[data-field="fulfillmentState"]');
      if (input) input.value = value;
      menu.querySelector('[data-fulfillment-label]')!.textContent = value.replace(/([a-z])([A-Z])/g, '$1 $2');
      (menu.querySelector('summary .ekom-state-dot') as HTMLElement).dataset.state = value;
      menu.querySelectorAll<HTMLButtonElement>('button[data-value]').forEach(button => {
        const selected = button.dataset.value === value;
        button.setAttribute('aria-pressed', String(selected));
        button.querySelector('.ekom-status-check')!.textContent = selected ? '✓' : '';
      });
      menu.removeAttribute('open');
      menu.querySelector('summary')?.focus();
      return;
    }
    if (action === 'save-fulfillment') {
      const value = this.querySelector<HTMLSelectElement>('[data-field="fulfillmentState"]')?.value;
      if (value && value !== this.selectedOrder?.status?.fulfillmentStatus) await this.changeState(value === 'Fulfilled' ? 'Fulfill' : value);
      return;
    }


    if (action === 'select-order-tab') { this.selectOrderTab(target.dataset.tab || 'order'); return; }

    if (action === 'set-page') {
      this.page = Number(target.dataset.page || 1);
      await this.loadOrders();
      return;
    }

    if (action === 'open-filter' || action === 'open-export') {
      this.overlay = action === 'open-filter' ? 'filter' : 'export';
      this.render();
      return;
    }

    if (action === 'close-overlay') {
      this.closeOverlay();
      return;
    }

    if (action === 'apply-filter') {
      this.applyFilterOverlay();
      this.overlay = '';
      this.page = 1;
      await this.loadOrders();
      return;
    }

    if (action === 'export-orders') {
      await this.exportOrders();
      return;
    }

    if (action === 'view-order') {
      await this.openOrder(target.dataset.orderId || '');
      return;
    }

    if (action === 'save-overlay-status') {
      await this.saveOverlayStatus();
      return;
    }

    if (action === 'change-row-status') {
      await this.changeRowStatus(target as HTMLSelectElement);
      return;
    }

    if (action === 'print-order') {
      if (this.selectedOrder) {
        try {
          await printOrderDocument(this.renderPrintableOrder(this.selectedOrder), `Order ${this.selectedOrder.referenceId}`);
        } catch {
          this.showError('Unable to prepare the order for printing. Please try again.');
        }
      }
      return;
    }

    if (action === 'toggle-activity-log') {
      const index = Number(target.dataset.logIndex);
      if (this.activityLogExpandedIndexes.has(index)) {
        this.activityLogExpandedIndexes.delete(index);
      } else {
        this.activityLogExpandedIndexes.add(index);
      }
      this.renderPreservingOverlayScroll();
      return;
    }

    if (action === 'release-order-lock') {
      await this.releaseOrderLock();
      return;
    }

    if (action === 'release-giftcard-recovery' || action === 'reconcile-giftcard-recovery') {
      await this.executeGiftcardRecovery(target.dataset.attemptId || '', action === 'release-giftcard-recovery' ? target.dataset.claimId || '' : undefined);
      return;
    }

    if (action === 'execute-order-action') {
      await this.executeOrderAction(target.dataset.actionKey || '');
      return;
    }

    if (action === 'open-customer-editor') {
      this.openCustomerEditor();
      return;
    }

    if (action === 'close-customer-editor') {
      this.customerEditorOpen = false;
      this.customerEditModel = undefined;
      this.render();
      return;
    }

    if (action === 'save-customer-information') {
      await this.saveCustomerInformation();
      return;
    }

    if (action === 'open-order-line-editor') {
      this.orderLineEditModel = { productId: '', variantId: '', quantity: '1' };
      this.orderLineEditorOpen = true;
      this.render();
      return;
    }

    if (action === 'close-order-line-editor') {
      if (!this.orderLineSaving) {
        this.orderLineEditorOpen = false;
        this.orderLineEditModel = undefined;
        this.render();
      }
      return;
    }

    if (action === 'save-order-line') {
      await this.saveOrderLine();
      return;
    }

    if (action === 'open-shipping-provider-editor') {
      await this.openShippingProviderEditor();
      return;
    }

    if (action === 'close-shipping-provider-editor') {
      if (!this.shippingProviderSaving) {
        this.shippingProviderEditorOpen = false;
        this.render();
      }
      return;
    }

    if (action === 'save-shipping-provider') {
      await this.saveShippingProvider();
      return;
    }

    if (action === 'add-shipping-custom-field') {
      this.readShippingCustomFields();
      this.shippingProviderId = this.querySelector<HTMLSelectElement>('[data-field="shippingProviderId"]')?.value || '';
      this.shippingCustomFields.push({ key: '', value: '', existing: false });
      this.render();
      return;
    }

    if (action === 'remove-order-line') {
      await this.removeOrderLine(target.dataset.orderLineId || '', target.dataset.productTitle || 'this order line');
    }
  }

  private async handleFieldChange(event: Event): Promise<void> {
    const target = event.currentTarget as HTMLInputElement | HTMLSelectElement;
    const field = target.dataset.field;

    if (field === 'includeOrderLines') {
      this.exportIncludeOrderLines = (target as HTMLInputElement).checked;
      this.render();
      return;
    }

    if (field === 'notifyOrderStatus') {
      this.notifyOrderStatusOverride = (target as HTMLInputElement).checked;
      return;
    }

    if (field === 'orderStatusOverlay' || field === 'query') {
      return;
    }

    if (!field || !(field in managerState.filters)) {
      return;
    }

    (managerState.filters as Record<string, string>)[field] = target.value;
    this.page = 1;

    if (field === 'store') {
      await this.loadFilterProviders(true);
    }

    await this.loadOrders();
  }

  private handleSearchInput(event: Event): void {
    const target = event.currentTarget as HTMLInputElement;
    managerState.filters.query = target.value;
    this.page = 1;
    window.clearTimeout(this.searchTimer);
    this.searchTimer = window.setTimeout(() => void this.loadOrders(), 700);
  }

  private applyFilterOverlay(): void {
    const dimensions = Array.from(this.querySelectorAll<HTMLSelectElement>('[data-status-dimension]')).map(select => select.value);
    if (dimensions.length === 3) managerState.filters.orderStatus = dimensions.some(Boolean) ? `states:${dimensions.join(';')}` : 'AllOrders';
    this.querySelectorAll<HTMLInputElement | HTMLSelectElement>('[data-filter-field]').forEach(input => {
      const field = input.dataset.filterField;

      if (field && field in managerState.filters) {
        (managerState.filters as Record<string, string>)[field] = field === 'giftcardCode' ? input.value.trim() : input.value;
      }
    });
  }

  private async exportOrders(): Promise<void> {
    if (!this.result.count) {
      return;
    }

    this.exporting = true;
    this.render();

    try {
      const blob = await this.api.exportOrders(managerState.filters, this.result.count, this.exportIncludeOrderLines);
      downloadBlob(blob, this.exportIncludeOrderLines ? 'orders-with-orderlines.csv' : 'orders.csv');
      this.overlay = '';
    } catch (error) {
      this.showError(getErrorMessage(error, 'Error exporting orders.'));
    } finally {
      this.exporting = false;
      this.render();
    }
  }

  private async openOrder(orderId: string): Promise<void> {
    if (!orderId) {
      return;
    }

    this.returnFocusOrderId = orderId;
    this.orderTab = 'order';
    const requestId = ++this.orderRequestId;
    ++this.lockRequestId;
    ++this.recoveryRequestId;
    this.recoveryAttempts = undefined;
    this.recoveryLoading = false;
    this.recoveryError = false;
    this.recoveryOperation = '';
    this.recoveryMessage = '';
    this.recoveryActionError = '';
    this.recoveryRefreshError = false;
    this.recoveryScrollTop = undefined;
    this.notifyOrderStatusOverride = undefined;
    this.selectedOrder = undefined;
    this.orderActions = [];
    this.orderActionsError = false;
    this.executingActionKey = '';
    this.orderLockStatus = undefined;
    this.releasingOrderLock = false;
    this.orderLockWarning = '';
    this.orderLockReleaseError = '';
    this.orderLockError = false;
    this.orderLockLoading = false;
    // Open the drawer on a loader and hold it until the order and everything shown with it
    // (activity, actions, checkout lock, gift-card recovery, reviews) has arrived.
    const previousOverlay = this.overlay;
    this.orderOpening = true;
    this.overlay = 'order';
    this.render();
    try {
      const order = await this.api.orderInfo(orderId);
      if (requestId !== this.orderRequestId) return;
      this.selectedOrder = order;
      this.reviews = []; this.activeReview = undefined; this.reviewError = '';
      this.orderLogs = [];
      this.orderLogsError = false;
      this.activityLogExpandedIndexes.clear();
      await Promise.all([this.loadOrderLogs(orderId), this.loadOrderActions(orderId), this.loadOrderLockStatus(orderId), this.loadGiftcardRecovery(orderId), this.loadOrderReviews(orderId)]);
    } catch (error) {
      if (requestId !== this.orderRequestId) return;
      if (!this.selectedOrder) this.overlay = previousOverlay === 'order' ? '' : previousOverlay;
      this.showError(getErrorMessage(error, 'Error on getting orderInfo.'));
    } finally {
      if (requestId === this.orderRequestId) {
        this.orderOpening = false;
        this.render();
      }
    }
  }

  private async loadOrderReviews(orderId: string): Promise<void> {
    try {
      const reviews = await this.api.orderReviews(orderId);
      if (this.selectedOrder?.uniqueId !== orderId) return;
      this.reviews = reviews; this.render();
    } catch (error) {
      if (this.selectedOrder?.uniqueId !== orderId) return;
      this.reviewError = `Could not load order reviews: ${String(error)}`; this.render();
    }
  }

  private renderReviewAlert(review: OrderReview, button = false): string {
    return `<div class="ekom-review-alert"><span class="ekom-review-icon" aria-hidden="true">!</span><div><strong>${escapeHtml(review.title)}</strong><small>${escapeHtml(review.source)} · ${escapeHtml(formatDate(review.createdUtc))}</small><p>${escapeHtml(review.message)}</p></div>${button ? `<button type="button" data-action="open-review" data-review-id="${escapeHtml(review.id)}">Review</button>` : ''}</div>`;
  }

  private renderReviews(): string {
    return `${this.reviewError && !this.activeReview ? `<p role="alert">${escapeHtml(this.reviewError)}</p>` : ''}${this.reviews.map(review => this.renderReviewAlert(review, true)).join('')}`;
  }

  private renderReviewDialog(): string {
    return `<div class="ekmCustomerInformationModal ekom-review-overlay"><section class="ekmCustomerInformationModal__panel ekom-review-panel" role="dialog" aria-modal="true" aria-label="Review alert">
      <header><h2>Review alert · Order #${escapeHtml(this.selectedOrder?.referenceId)}</h2><button type="button" data-action="close-review" aria-label="Close review" ${this.reviewSaving ? 'disabled' : ''}>×</button></header>
      <div class="ekom-review-body">${this.renderReviewAlert(this.activeReview!)}
      <label for="ekom-review-comment">What was done</label><textarea id="ekom-review-comment" data-review-comment maxlength="4000" rows="4" placeholder="Describe how this was resolved or what was checked." ${this.reviewSaving ? 'disabled' : ''}>${escapeHtml(this.reviewComment)}</textarea>
      <p class="ekom-muted">Saved to the activity log with your name and the time.</p>
      <label class="ekom-review-checkbox"><input type="checkbox" data-review-resolved ${this.reviewResolved ? 'checked' : ''} ${this.reviewSaving ? 'disabled' : ''}><span>Problem is resolved <small>— untick to log a note but keep the alert open</small></span></label>
      ${this.reviewError ? `<p role="alert">${escapeHtml(this.reviewError)}</p>` : ''}</div>
      <footer><button type="button" data-action="close-review" ${this.reviewSaving ? 'disabled' : ''}>Cancel</button><button class="btn-success" type="button" data-action="save-review" ${this.reviewSaving ? 'disabled' : ''}>${this.reviewSaving ? 'Saving...' : this.reviewResolved ? 'Mark as reviewed' : 'Save note'}</button></footer>
    </section></div>`;
  }

  private async saveReview(): Promise<void> {
    if (!this.activeReview || !this.selectedOrder || this.reviewSaving) return;
    const orderId = this.selectedOrder.uniqueId;
    this.reviewComment = this.querySelector<HTMLTextAreaElement>('[data-review-comment]')?.value || '';
    this.reviewResolved = this.querySelector<HTMLInputElement>('[data-review-resolved]')?.checked ?? true;
    if (!this.reviewResolved && !this.reviewComment.trim()) { this.reviewError = 'Describe what was done before saving a note.'; this.render(); return; }
    this.reviewSaving = true; this.reviewError = ''; this.render();
    try {
      await this.api.reviewOrder(orderId, this.activeReview.id, this.reviewComment, this.reviewResolved);
      this.activeReview = undefined;
      await Promise.all([this.loadOrderReviews(orderId), this.loadOrderLogs(orderId), this.loadOrders()]);
    } catch (error) { this.reviewError = String(error); }
    finally { this.reviewSaving = false; this.render(); }
  }

  private async loadOrderLogs(orderId: string): Promise<void> {
    const requestId = this.orderRequestId;
    this.orderLogsLoading = true;
    this.orderLogsError = false;
    if (!this.stateSaving) this.render();

    try {
      const logs = await this.api.orderLogs(orderId);
      if (requestId !== this.orderRequestId) return;
      this.orderLogs = logs;
    } catch {
      if (requestId !== this.orderRequestId) return;
      this.orderLogs = [];
      this.orderLogsError = true;
    } finally {
      if (requestId !== this.orderRequestId) return;
      this.orderLogsLoading = false;
      if (!this.stateSaving) this.render();
    }
  }

  private async loadOrderActions(orderId: string): Promise<void> {
    const requestId = this.orderRequestId;
    this.orderActionsLoading = true;
    this.orderActionsError = false;
    if (!this.stateSaving) this.render();

    try {
      const actions = await this.api.orderActions(orderId);
      if (requestId !== this.orderRequestId) return;
      this.orderActions = actions;
    } catch {
      if (requestId !== this.orderRequestId) return;
      this.orderActions = [];
      this.orderActionsError = true;
    } finally {
      if (requestId !== this.orderRequestId) return;
      this.orderActionsLoading = false;
      if (!this.stateSaving) this.render();
    }
  }

  private async loadOrderLockStatus(orderId: string): Promise<void> {
    const requestId = ++this.lockRequestId;
    const orderRequestId = this.orderRequestId;
    const isCurrent = (): boolean => requestId === this.lockRequestId && orderRequestId === this.orderRequestId && orderId === this.selectedOrder?.uniqueId;
    this.orderLockLoading = true;
    this.orderLockError = false;
    this.render();
    try {
      const status = await this.api.orderLockStatus(orderId);
      if (isCurrent()) this.orderLockStatus = status;
    } catch {
      if (isCurrent()) this.orderLockError = true;
    } finally {
      if (isCurrent()) {
        this.orderLockLoading = false;
        this.render();
      }
    }
  }

  private async loadGiftcardRecovery(orderId: string): Promise<void> {
    const requestId = ++this.recoveryRequestId;
    const orderRequestId = this.orderRequestId;
    const isCurrent = (): boolean => requestId === this.recoveryRequestId && orderRequestId === this.orderRequestId && orderId === this.selectedOrder?.uniqueId;
    this.recoveryLoading = true;
    this.recoveryError = false;
    this.render();
    try {
      const attempts = await this.api.giftcardRecovery(orderId);
      if (!Array.isArray(attempts) || attempts.some(attempt => !Array.isArray(attempt.claims))) {
        throw new Error('Invalid recovery history response.');
      }
      if (isCurrent()) this.recoveryAttempts = attempts;
    } catch {
      if (isCurrent()) this.recoveryError = true;
    } finally {
      if (isCurrent()) {
        this.recoveryLoading = false;
        this.render();
      }
    }
  }

  private async executeGiftcardRecovery(attemptId: string, claimId?: string): Promise<void> {
    const orderId = this.selectedOrder?.uniqueId;
    const attempt = this.recoveryAttempts?.find(item => item.attemptId === attemptId);
    if (!orderId || !attempt || this.recoveryBusy) return;
    const release = claimId !== undefined;
    const claim = release ? attempt.claims.find(item => item.id === claimId) : undefined;
    const token = release ? claim?.releaseToken : attempt.reconcileToken;
    if (!token || (release ? !claim?.canRelease || !claim.claimReference : !attempt.canReconcile)) return;
    const confirmation = release
      ? `Release the failed-preparation hold for masked card ${claim!.cardReference}, claim ${claim!.claimReference}? This calls the provider through audited recovery. It does not refund an accepted payment or retry payment or checkout. Unknown effects may still need reconciliation.`
      : 'Reconcile this failed gift-card preparation? The audited backend will check known claims and unknown provider effects. This does not refund an accepted payment or retry payment or checkout. Unresolved effects may remain blocked.';
    if (!window.confirm(confirmation)) return;

    const requestId = this.orderRequestId;
    const isCurrent = (): boolean => requestId === this.orderRequestId && orderId === this.selectedOrder?.uniqueId;
    this.recoveryOperation = release ? `release:${attemptId}:${claimId}` : `reconcile:${attemptId}`;
    this.recoveryScrollTop = this.querySelector<HTMLElement>('.ekmOverlay')?.scrollTop;
    this.recoveryMessage = '';
    this.recoveryActionError = '';
    this.render();
    try {
      const result = release
        ? await this.api.releaseGiftcardRecovery(orderId, attemptId, claim!.id, token)
        : await this.api.reconcileGiftcardRecovery(orderId, attemptId, token);
      if (!isCurrent()) return;
      this.recoveryMessage = `${result.message} ${result.resolved ? 'Failed preparation resolved.' : 'Recovery remains unresolved; check the updated history.'}`;
    } catch (error) {
      if (!isCurrent()) return;
      this.recoveryActionError = getErrorMessage(error, 'Recovery could not be confirmed. Check the updated history before trying again.');
    } finally {
      if (isCurrent()) {
        // Even a failed POST may have reached the provider. Refresh tokens and
        // evidence, never automatically retry the operation.
        await Promise.all([
          this.loadGiftcardRecovery(orderId), this.loadOrderLockStatus(orderId),
          this.loadOrderActions(orderId), this.loadOrderLogs(orderId),
          this.api.orderInfo(orderId).then(order => {
            if (isCurrent()) this.selectedOrder = order;
          }).catch(() => { if (isCurrent()) this.recoveryRefreshError = true; }),
        ]);
        if (isCurrent()) {
          this.recoveryOperation = '';
          this.render();
          this.recoveryScrollTop = undefined;
        }
      }
    }
  }

  private async releaseOrderLock(): Promise<void> {
    const orderId = this.selectedOrder?.uniqueId;
    const status = this.orderLockStatus;
    if (!orderId || !status?.hasLock || !status.canRelease || !status.releaseToken || this.orderLockLoading || this.orderLockError || this.releasingOrderLock || this.executingActionKey || this.recoveryOperation) return;
    if (!window.confirm("Release this checkout lock? Confirm that no payment is currently being processed and the previous checkout has stopped. The customer's basket will be kept, and they can try checkout again. This does not cancel or refund a payment. Any gift-card hold that cannot be released must be checked separately.")) return;

    const requestId = this.orderRequestId;
    const isCurrent = (): boolean => requestId === this.orderRequestId && orderId === this.selectedOrder?.uniqueId;
    this.releasingOrderLock = true;
    this.orderLockReleaseError = '';
    let released = false;
    this.render();
    try {
      const result = await this.api.releaseOrderLock(orderId, status.releaseToken);
      if (!isCurrent()) return;
      released = true;
      this.orderLockWarning = result.warning || '';
      this.showSuccess(result.message);
      const [order] = await Promise.all([this.api.orderInfo(orderId), this.loadOrderLogs(orderId), this.loadOrderActions(orderId), this.loadOrderLockStatus(orderId), this.loadGiftcardRecovery(orderId), this.loadOrderReviews(orderId)]);
      if (isCurrent()) this.selectedOrder = order;
    } catch (error) {
      if (!isCurrent()) return;
      this.orderLockReleaseError = released
        ? 'Checkout was unlocked, but we could not refresh all order details. Reopen the order to check them.'
        : getErrorMessage(error, 'We could not confirm the checkout reset. Check the updated order before trying again.');
      // A failed response can still have acquired a recovery owner. Fetch a fresh
      // confirmation token, but never automatically repeat the editor's POST.
      await Promise.all([this.loadOrderLogs(orderId), this.loadOrderActions(orderId), this.loadOrderLockStatus(orderId), this.loadGiftcardRecovery(orderId), this.loadOrderReviews(orderId),
        this.api.orderInfo(orderId).then(order => { if (isCurrent()) this.selectedOrder = order; }).catch(() => {})]);
    } finally {
      if (isCurrent()) {
        this.releasingOrderLock = false;
        this.render();
      }
    }
  }

  private async saveOverlayStatus(): Promise<void> {
    if (!this.config || !this.selectedOrder?.uniqueId) {
      return;
    }

    const select = this.querySelector<HTMLSelectElement>('[data-field="orderStatusOverlay"]');
    const status = select?.value || '';
    const notify = this.notifyOrderStatus;

    try {
      await this.api.changeOrderStatus(this.selectedOrder.uniqueId, status, notify);
      this.selectedOrder.orderStatus = status;
      this.showSuccess('Order status updated.');
      await this.loadOrders();
      await this.loadOrderLogs(this.selectedOrder.uniqueId);
    } catch (error) {
      if (select) select.value = this.getOrderStatusValue(this.selectedOrder?.orderStatus);
      this.showError(getErrorMessage(error, 'Error updating order status.'));
    }
  }

  private showSuccess(message: string): void {
    this.showNotification('positive', 'Success', message);
  }

  private showError(message: string): void {
    this.showNotification('danger', 'Error', message);
  }

  private showNotification(color: UmbNotificationColor, headline: string, message: string): void {
    if (this.notificationContext) {
      this.notificationContext.peek(color, {
        data: {
          headline,
          message,
        },
      });
      return;
    }

    if (color === 'danger') {
      console.error(`${headline}: ${message}`);
    }
  }

  private renderPreservingOverlayScroll(): void {
    const overlay = this.querySelector<HTMLElement>('.ekmOverlay');
    const scrollTop = overlay?.scrollTop ?? 0;

    this.render();

    requestAnimationFrame(() => {
      const newOverlay = this.querySelector<HTMLElement>('.ekmOverlay');

      if (newOverlay) {
        newOverlay.scrollTop = scrollTop;
      }
    });
  }

  private closeOverlay(): void {
    this.orderOpening = false;
    ++this.orderRequestId;
    ++this.lockRequestId;
    ++this.recoveryRequestId;
    this.recoveryOperation = '';
    this.recoveryScrollTop = undefined;
    this.overlay = '';
    this.selectedOrder = undefined;
    this.customerEditorOpen = false;
    this.customerEditModel = undefined;
    this.orderLineEditorOpen = false;
    this.orderLineEditModel = undefined;
    this.shippingProviderEditorOpen = false;
    this.render();
    const trigger = Array.from(this.querySelectorAll<HTMLElement>('[data-action="view-order"]')).find(button => button.dataset.orderId === this.returnFocusOrderId);
    trigger?.focus();
    this.returnFocusOrderId = undefined;
  }

  private getOrderStatusValue(orderStatus: unknown): string {
    const stringValue = String(orderStatus ?? '');
    const status = managerState.statusList.find(item => {
      return String(item.value ?? '') === stringValue
        || String(item.enumValue ?? '') === stringValue;
    });

    return status ? getStatusValue(status) : stringValue;
  }

  private async changeRowStatus(select: HTMLSelectElement): Promise<void> {
    const orderId = select.dataset.orderId || '';

    if (!this.config || !orderId) {
      return;
    }

    try {
      await this.api.changeOrderStatus(orderId, select.value, this.config.managerOrderStatusFireEventsByDefault ?? true);
      const order = this.result.orders.find(item => item.uniqueId === orderId);

      if (order) {
        order.orderStatusCol = select.value;
      }

      this.showSuccess('Order status updated.');
    } catch (error) {
      this.showError(getErrorMessage(error, 'Error updating order status.'));
      await this.loadOrders();
    }
  }

  private async executeOrderAction(actionKey: string): Promise<void> {
    if (!this.selectedOrder?.uniqueId || !actionKey || this.executingActionKey || this.releasingOrderLock || this.recoveryOperation) {
      return;
    }

    const action = this.orderActions.find(item => item.key === actionKey);

    if (!action || action.enabled === false) return;
    const orderId = this.selectedOrder.uniqueId;
    const requestId = this.orderRequestId;

    if (action?.confirmMessage && !window.confirm(action.confirmMessage)) {
      return;
    }

    this.executingActionKey = actionKey;
    this.render();

    try {
      const response = await this.api.executeOrderAction(orderId, actionKey);
      const blob = await response.blob();
      const contentDisposition = response.headers.get('content-disposition') || '';
      const contentType = response.headers.get('content-type') || '';

      if (contentDisposition.toLowerCase().includes('filename=') || contentType.startsWith('application/pdf') || contentType.startsWith('application/octet-stream') || contentType.startsWith('image/')) {
        window.open(URL.createObjectURL(blob), '_blank');
      } else {
        const message = await blob.text();
        this.showSuccess(tryParseMessage(message));
      }

      if (requestId !== this.orderRequestId) return;
      const [order] = await Promise.all([this.api.orderInfo(orderId), this.loadOrderLockStatus(orderId), this.loadGiftcardRecovery(orderId)]);
      if (requestId !== this.orderRequestId) return;
      this.selectedOrder = order;
      await Promise.all([this.loadOrderLogs(orderId), this.loadOrderActions(orderId)]);
    } catch (error) {
      if (requestId !== this.orderRequestId) return;
      this.showError(getErrorMessage(error, 'Order action failed.'));
    } finally {
      if (requestId !== this.orderRequestId) return;
      this.executingActionKey = '';
      this.render();
    }
  }

  private openCustomerEditor(): void {
    const order = this.selectedOrder;

    if (!order) {
      return;
    }

    const customer = order.customerInformation?.customer || {};
    const shipping = order.customerInformation?.shipping || {};
    this.customerEditModel = {
      customer: mapStandardFields(customer, customerFields).concat(mapExtraFields(customer.properties, 'customer')),
      shipping: mapStandardFields(shipping, shippingFields).concat(mapExtraFields(shipping.properties, 'shipping')),
    };
    this.customerEditorOpen = true;
    this.render();
  }

  private async saveCustomerInformation(): Promise<void> {
    if (!this.selectedOrder?.uniqueId || !this.customerEditModel || this.customerSaving) {
      return;
    }

    this.querySelectorAll<HTMLInputElement>('[data-customer-group]').forEach(input => {
      const group = input.dataset.customerGroup as 'customer' | 'shipping';
      const key = input.dataset.customerKey;
      const field = this.customerEditModel?.[group].find(item => item.key === key);

      if (field) {
        field.value = input.value;
      }
    });

    this.customerSaving = true;
    this.render();

    try {
      this.selectedOrder = await this.api.updateCustomerInformation(
        this.selectedOrder.uniqueId,
        buildPayload(this.customerEditModel.customer),
        buildPayload(this.customerEditModel.shipping));
      this.customerEditorOpen = false;
      this.customerEditModel = undefined;
      this.showSuccess('Customer information updated.');
      await this.loadOrders();
    } catch (error) {
      this.showError(getErrorMessage(error, 'Error updating customer information.'));
    } finally {
      this.customerSaving = false;
      this.render();
    }
  }

  private async saveOrderLine(): Promise<void> {
    if (!this.selectedOrder?.uniqueId || !this.orderLineEditModel || this.orderLineSaving) {
      return;
    }

    this.querySelectorAll<HTMLInputElement>('[data-order-line-field]').forEach(input => {
      const field = input.dataset.orderLineField as keyof typeof this.orderLineEditModel;

      if (field) {
        this.orderLineEditModel![field] = input.value;
      }
    });

    const { productId, variantId, quantity } = this.orderLineEditModel;
    const parsedQuantity = Number(quantity);

    if (!productId.trim() || !Number.isFinite(parsedQuantity) || parsedQuantity <= 0) {
      this.showError('Product ID and a positive quantity are required.');
      return;
    }

    this.orderLineSaving = true;
    this.render();

    try {
      this.selectedOrder = await this.api.addOrderLine(this.selectedOrder.uniqueId, productId.trim(), variantId.trim() || undefined, parsedQuantity);
      this.orderLineEditorOpen = false;
      this.orderLineEditModel = undefined;
      this.showSuccess('Order line added.');
      await this.refreshOrderAfterLineChange();
    } catch (error) {
      this.showError(getErrorMessage(error, 'Error adding order line.'));
    } finally {
      this.orderLineSaving = false;
      this.render();
    }
  }

  private async openShippingProviderEditor(): Promise<void> {
    if (!this.selectedOrder) {
      return;
    }

    try {
      const storeAlias = this.selectedOrder.storeInfo?.alias || this.selectedOrder.storeAlias;
      this.shippingProviders = await this.api.shippingProviders(storeAlias);
      this.shippingProviderId = this.selectedOrder.shippingProvider?.key || '';
      this.shippingCustomFields = Object.entries(this.selectedOrder.shippingProvider?.customData || {})
        .filter(([key]) => key.toLowerCase().startsWith('customshipping'))
        .map(([key, value]) => ({ key, value: decodeHtml(value), existing: true }));
      this.shippingProviderEditorOpen = true;
      this.render();
    } catch (error) {
      this.showError(getErrorMessage(error, 'Error loading shipping providers.'));
    }
  }

  private async saveShippingProvider(): Promise<void> {
    if (!this.selectedOrder?.uniqueId || this.shippingProviderSaving) {
      return;
    }

    const providerId = this.querySelector<HTMLSelectElement>('[data-field="shippingProviderId"]')?.value || '';
    if (!this.shippingProviders.some(provider => provider.key === providerId)) {
      this.showError('Select a shipping provider.');
      return;
    }

    this.readShippingCustomFields();
    const customData: Record<string, string> = {};
    const seen = new Set<string>();
    for (const field of this.shippingCustomFields) {
      const key = field.existing ? field.key : `customshipping${field.key.trim()}`;
      if (!key.slice('customshipping'.length).trim() || key.length > 100 || field.value.length > 4096 || seen.has(key.toLowerCase())) {
        this.showError('Enter unique shipping custom data keys and valid values.');
        return;
      }
      seen.add(key.toLowerCase());
      customData[key] = field.value;
    }

    this.shippingProviderSaving = true;
    this.render();
    try {
      this.selectedOrder = await this.api.updateShippingProvider(this.selectedOrder.uniqueId, providerId, customData);
      this.shippingProviderEditorOpen = false;
      this.showSuccess('Shipping provider updated.');
      await this.refreshOrderAfterLineChange();
    } catch (error) {
      this.showError(getErrorMessage(error, 'Error updating shipping provider.'));
    } finally {
      this.shippingProviderSaving = false;
      this.render();
    }
  }

  private async removeOrderLine(lineId: string, productTitle: string): Promise<void> {
    if (!this.selectedOrder?.uniqueId || !lineId || this.removingOrderLineId || !window.confirm(`Remove ${productTitle} from this order?`)) {
      return;
    }

    this.removingOrderLineId = lineId;
    this.render();

    try {
      this.selectedOrder = await this.api.removeOrderLine(this.selectedOrder.uniqueId, lineId);
      this.showSuccess('Order line removed.');
      await this.refreshOrderAfterLineChange();
    } catch (error) {
      this.showError(getErrorMessage(error, 'Error removing order line.'));
    } finally {
      this.removingOrderLineId = '';
      this.render();
    }
  }

  private async refreshOrderAfterLineChange(): Promise<void> {
    if (!this.selectedOrder?.uniqueId) {
      return;
    }

    const orderId = this.selectedOrder.uniqueId;
    await Promise.all([this.loadOrders(), this.loadOrderLogs(orderId), this.loadOrderActions(orderId)]);
  }
}

function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

function hasGiftcardValue(value: unknown): boolean {
  return value != null && (typeof value !== 'string' || value.trim() !== '');
}

function giftcardFieldLabel(key: string): string {
  if (key === 'code') return 'Gift-card number';
  if (key === 'claimId') return 'Reservation reference';
  if (key === 'transactionId') return 'Transaction reference';
  if (key === 'usedDate') return 'Used on';
  if (key === 'validUntil') return 'Saved expiry';
  if (key === 'claimDate') return 'Payment claim date';
  const label = key.replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/[_-]+/g, ' ');
  return label.charAt(0).toUpperCase() + label.slice(1).toLowerCase().replace(/\bid\b/g, 'ID');
}

function giftcardFieldValue(value: unknown): string {
  if (!hasGiftcardValue(value)) return 'Not recorded';
  if (typeof value === 'boolean') return value ? 'Yes' : 'No';
  if (Array.isArray(value)) return value.length ? value.map(giftcardFieldValue).join('; ') : 'Empty list';
  if (typeof value === 'object') {
    const entries = Object.entries(value as Record<string, unknown>);
    return entries.length ? entries.map(([key, item]) => `${giftcardFieldLabel(key)}: ${giftcardFieldValue(item)}`).join('; ') : 'Empty object';
  }
  return String(value);
}

function giftcardDetails(card: Record<string, unknown>): { label: string; value: string }[] {
  const known = ['code', 'amount', 'claimId', 'claimDate', 'transactionId', 'usedDate', 'claimed', 'validUntil'];
  return [...known, ...Object.keys(card).filter(key => !known.includes(key))]
    .map(key => ({ label: giftcardFieldLabel(key), value: giftcardFieldValue(card[key]) }));
}

function giftcardSummary(card: Record<string, unknown>, now = Date.now()) {
  const hasReference = hasGiftcardValue(card.claimId);
  const hasExpiry = hasGiftcardValue(card.validUntil);
  const expiryTime = hasExpiry ? Date.parse(String(card.validUntil)) : NaN;
  const future = expiryTime > now;
  const used = card.claimed === true || hasGiftcardValue(card.transactionId) || hasGiftcardValue(card.claimDate) || hasGiftcardValue(card.usedDate);
  const status = used ? 'Used' : hasReference && future ? 'Reserved'
    : hasReference && expiryTime <= now ? 'Reservation expired'
    : hasReference || hasExpiry ? 'Needs checking' : 'Selected';
  let warning = '';
  if (!used && !hasReference && hasExpiry) {
    warning = 'An expiry time is saved, but the reservation reference is missing. Check the gift-card provider if an amount is still held.';
  } else if (!used && hasReference && expiryTime <= now) {
    warning = 'The saved reservation expiry has passed. This does not confirm that the provider has released the hold.';
  } else if (!used && hasReference && !hasExpiry) {
    warning = 'No expiry time is saved. Check the gift-card provider if an amount is still held.';
  }
  if (hasExpiry && !Number.isFinite(expiryTime)) {
    warning += `${warning ? ' ' : ''}The saved expiry time could not be read. Check the gift-card provider.`;
  }
  let expiry = 'Not recorded';
  if (hasExpiry) {
    expiry = Number.isFinite(expiryTime)
      ? new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'long' }).format(new Date(expiryTime))
      : String(card.validUntil);
    if (future) {
      const minutes = Math.ceil((expiryTime - now) / 60000);
      const value = minutes < 60 ? minutes : minutes < 1440 ? Math.ceil(minutes / 60) : Math.ceil(minutes / 1440);
      const unit = minutes < 60 ? 'minute' : minutes < 1440 ? 'hour' : 'day';
      expiry += ` (in about ${value} ${unit}${value === 1 ? '' : 's'})`;
    }
  }
  return { status, warning, expiryLabel: !used && hasReference ? 'Reservation ends' : 'Saved expiry', expiry };
}

function labelFromKey(key: string): string {
  return key === 'zipCode' ? 'Zipcode' : key.charAt(0).toUpperCase() + key.slice(1);
}

function isDefaultKey(key: string): boolean {
  return new Set(['shippingname', 'shippingaddress', 'shippingcity', 'shippingcountry', 'shippingemail', 'shippingapartment', 'shippingzipcode', 'shippingphone', 'customeremail', 'customername', 'customeraddress', 'customerapartment', 'customercity', 'customercountry', 'customerzipcode', 'customerphone']).has(key.toLowerCase());
}

function cleanKey(key: string): string {
  return key.replace(/^customshipping/i, '').replace(/^custompayment/i, '').replace(/^shipping/i, '').replace(/^customer/i, '');
}

function parseProperties(properties: Record<string, unknown> | undefined, prefix: string): Array<[string, string]> {
  return Object.entries(properties || {})
    .filter(([key, value]) => (Boolean(value) || (prefix === 'customshipping' && value === '')) && key.toLowerCase().startsWith(prefix) && !isDefaultKey(key))
    .map(([key, value]) => [key, decodeHtml(value)]);
}

function hasShippingInfo(shipping: Record<string, any>): boolean {
  return Boolean(shipping?.name || shipping?.email || shipping?.address || shipping?.apartment || shipping?.city || shipping?.country || shipping?.zipCode || shipping?.phone);
}

function formatOrderCustomDataKey(key: string): string {
  return key
    .replace(/([a-z0-9])([A-Z])/g, '$1 $2')
    .replace(/([A-Z])([A-Z][a-z])/g, '$1 $2')
    .replace(/[_-]+/g, ' ')
    .trim()
    .replace(/\b[a-z]/g, letter => letter.toUpperCase());
}

function resizeOrderLineImage(url: string, width: number): string {
  const hashIndex = url.indexOf('#');
  const hash = hashIndex >= 0 ? url.slice(hashIndex) : '';
  const pathAndQuery = hashIndex >= 0 ? url.slice(0, hashIndex) : url;
  const queryIndex = pathAndQuery.indexOf('?');
  const path = queryIndex >= 0 ? pathAndQuery.slice(0, queryIndex) : pathAndQuery;
  const query = new URLSearchParams(queryIndex >= 0 ? pathAndQuery.slice(queryIndex + 1) : '');
  query.set('width', String(width));
  query.set('format', 'webp');
  query.set('rmode', 'pad');
  query.set('rcolor', 'ffffff');
  return `${path}?${query}${hash}`.replace(/,/g, '%2C').replace(/ /g, '%20');
}

function renderOrderLineThumbnail(line: Record<string, any>): string {
  const images = [line.variant?.images, line.product?.images].flatMap(images => Array.isArray(images) ? images : []);
  const url = images.map(image => {
    if (typeof image?.url !== 'string' || !image.url.trim()) return '';
    try {
      const url = image.url.trim();
      return ['http:', 'https:'].includes(new URL(url, 'https://ekom.invalid/').protocol) ? url : '';
    } catch {
      return '';
    }
  }).find(Boolean);
  return `<span class="ekmOrderLineThumbnail" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8" cy="8" r="1.5"/><path d="m3 17 6-6 4 4 3-3 5 5"/></svg>${url ? `<picture><source srcset="${escapeHtml(resizeOrderLineImage(url, 56))} 1x, ${escapeHtml(resizeOrderLineImage(url, 112))} 2x"><img src="${escapeHtml(resizeOrderLineImage(url, 56))}" width="56" height="56" alt="" loading="lazy" decoding="async"></picture>` : ''}</span>`;
}

function renderOrderLineProductTitle(product: Record<string, unknown> | undefined): string {
  const title = escapeHtml(product?.title);
  const url = typeof product?.url === 'string' ? product.url.trim() : '';
  if (!url) return title;

  try {
    const protocol = new URL(url, 'https://ekom.invalid/').protocol;
    if (protocol !== 'http:' && protocol !== 'https:') return title;
  } catch {
    return title;
  }

  return `<a href="${escapeHtml(url)}" target="_blank" rel="noopener noreferrer">${title}</a>`;
}

function renderOrderLineProperties(orderLine: Record<string, any>): string {
  const properties = orderLine.orderLineInfo?.properties || {};
  return Object.entries(properties)
    .filter(([, value]) => Boolean(value))
    .map(([key, value]) => `<small style="display:block; margin-top:3px;"><strong>${escapeHtml(formatOrderLinePropertyKey(key))}</strong>: ${escapeHtml(decodeHtml(value))}</small>`)
    .join('');
}

function formatOrderLinePropertyKey(key: string): string {
  const value = key.replace(/^orderline/i, '').replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/[_-]+/g, ' ').trim();
  return value ? value.charAt(0).toUpperCase() + value.slice(1) : key;
}

function hasTrackingData(tracking: Record<string, any> | undefined): boolean {
  return Boolean(tracking && (tracking.source || tracking.medium || tracking.campaign || tracking.term || tracking.content || tracking.clickId || tracking.clickIdType || tracking.landingUrl || tracking.referrer || tracking.captureMethod || tracking.capturedAtUtc || tracking.hasCookieSupport !== null && tracking.hasCookieSupport !== undefined || tracking.ga4?.clientId || tracking.ga4?.sessionId || tracking.meta?.fbp || tracking.meta?.fbc || hasAlgoliaTrackingData(tracking.algolia)));
}

function renderTrackingDetails(tracking: Record<string, any>): string {
  const ga4Data = Object.entries(tracking.ga4?.data || {});
  const metaData = Object.entries(tracking.meta?.data || {});
  const algolia = tracking.algolia;
  const algoliaLines = Array.isArray(algolia?.lines) ? algolia.lines : [];
  const algoliaTracking = hasAlgoliaTrackingData(algolia)
    ? `<div class="ekmOrderTracking__provider"><h5>Algolia</h5>${renderOptional('User token', algolia.userToken)}${algoliaLines.length ? `<ul>${algoliaLines.map((line: Record<string, any>) => `<li class="ekmOrderTracking__wrap"><strong>Order line key</strong>: ${escapeHtml(line.orderLineKey)}<br><strong>Query ID</strong>: ${escapeHtml(line.queryId)}</li>`).join('')}</ul>` : ''}</div>`
    : '';
  return `<div class="ekmSplit"><div class="ekmSplit__column">${renderOptional('Captured', formatDate(tracking.capturedAtUtc))}${renderOptional('Capture method', tracking.captureMethod)}${tracking.hasCookieSupport !== null && tracking.hasCookieSupport !== undefined ? `<p>Cookie support: ${tracking.hasCookieSupport ? 'Yes' : 'No'}</p>` : ''}${renderOptional('Source', tracking.source)}${renderOptional('Medium', tracking.medium)}${renderOptional('Campaign', tracking.campaign)}${renderOptional('Term', tracking.term)}${renderOptional('Content', tracking.content)}${renderOptional('Click ID', tracking.clickId)}${renderOptional('Click ID Type', tracking.clickIdType)}${renderOptional('Landing URL', tracking.landingUrl)}${renderOptional('Referrer', tracking.referrer)}</div><div class="ekmSplit__column"><h5>GA4</h5>${renderOptional('Client ID', tracking.ga4?.clientId)}${renderOptional('Session ID', tracking.ga4?.sessionId)}${renderPairs(ga4Data)}<h5>Meta</h5>${renderOptional('FBP', tracking.meta?.fbp)}${renderOptional('FBC', tracking.meta?.fbc)}${renderPairs(metaData)}${algoliaTracking}</div></div>`;
}

function hasAlgoliaTrackingData(algolia: Record<string, any> | undefined): boolean {
  return Boolean(algolia && (algolia.userToken || Array.isArray(algolia.lines) && algolia.lines.length));
}

function renderOptional(label: string, value: unknown): string {
  return value ? `<p class="ekmOrderTracking__wrap ekom-tracking-field"><span>${escapeHtml(label)}</span><strong>${escapeHtml(value)}</strong></p>` : '';
}

function renderPairs(entries: Array<[string, unknown]>): string {
  return entries.length ? `<ul>${entries.map(([key, value]) => `<li><strong>${escapeHtml(key)}</strong>: ${escapeHtml(value)}</li>`).join('')}</ul>` : '';
}

function hasConsentData(consent: Record<string, any> | undefined): boolean {
  return Boolean(consent && (consent.resolvedAtUtc || consent.source || consent.analytics !== null && consent.analytics !== undefined || consent.marketing !== null && consent.marketing !== undefined));
}

function consentLabel(value: unknown): string {
  if (value === true) {
    return 'Yes';
  }

  if (value === false) {
    return 'No';
  }

  return 'Unknown';
}

function mapStandardFields(info: Record<string, any>, fields: EditorFieldDefinition[]): CustomerInformationField[] {
  return fields.map(field => ({
    key: field.key,
    label: field.label,
    value: decodeHtml(info[field.property] || info.properties?.[field.key] || ''),
    isExtra: false,
  }));
}

function mapExtraFields(properties: Record<string, unknown> | undefined, prefix: string): CustomerInformationField[] {
  return parseProperties(properties, prefix).map(([key, value]) => ({
    key,
    label: cleanKey(key).replace(/([a-z0-9])([A-Z])/g, '$1 $2').replace(/[_-]+/g, ' ').trim() || key,
    value,
    isExtra: true,
  }));
}

function buildPayload(fields: CustomerInformationField[]): Record<string, string> {
  return Object.fromEntries(fields.map(field => [field.key, field.value || '']));
}

function tryParseMessage(text: string): string {
  try {
    const data = JSON.parse(text) as { message?: string };
    return data.message || text;
  } catch {
    return text;
  }
}

customElements.define('ekom-orders-section-view', EkomOrdersSectionViewElement);

export default EkomOrdersSectionViewElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-orders-section-view': EkomOrdersSectionViewElement;
  }
}
