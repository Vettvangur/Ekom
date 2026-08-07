import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_NOTIFICATION_CONTEXT, type UmbNotificationColor } from '@umbraco-cms/backoffice/notification';
import {
  CustomerInformationField,
  EkomManagerApi,
  ManagerFilters,
  OrderAction,
  OrderActivityLog,
  OrderInfo,
  OrderListItem,
  OrderSearchResult,
  decodeHtml,
  downloadBlob,
  escapeHtml,
  formatDate,
  getStatusValue,
  managerState,
  managerStyles,
  pageRange,
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

export class EkomOrdersSectionViewElement extends UmbElementMixin(HTMLElement) {
  private readonly api = new EkomManagerApi();
  private notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;
  private result: OrderSearchResult = { orders: [], count: 0, totalPages: 0 };
  private page = 1;
  private loading = true;
  private error = '';
  private searchTimer = 0;
  private overlay = '';
  private selectedOrder?: OrderInfo;
  private orderLogs: OrderActivityLog[] = [];
  private orderLogsLoading = false;
  private orderActions: OrderAction[] = [];
  private orderActionsLoading = false;
  private executingActionKey = '';
  private trackingExpanded = false;
  private consentExpanded = false;
  private customerEditorOpen = false;
  private customerSaving = false;
  private customerEditModel?: { customer: CustomerInformationField[]; shipping: CustomerInformationField[] };
  private orderLineEditorOpen = false;
  private orderLineSaving = false;
  private orderLineEditModel?: { productId: string; variantId: string; quantity: string };
  private removingOrderLineId = '';
  private exportIncludeOrderLines = false;
  private exporting = false;
  private readonly handleKeyDown = (event: KeyboardEvent): void => {
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

    if (this.exporting) {
      return;
    }

    this.closeOverlay();
  };

  override connectedCallback(): void {
    super.connectedCallback();
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, context => {
      this.notificationContext = context;
    });
    document.addEventListener('keydown', this.handleKeyDown);
    this.render();
    void this.initialize();
  }

  override disconnectedCallback(): void {
    super.disconnectedCallback();
    document.removeEventListener('keydown', this.handleKeyDown);
    window.clearTimeout(this.searchTimer);
  }

  private async initialize(): Promise<void> {
    try {
      const [statuses, stores] = await Promise.all([
        this.api.statusList(),
        this.api.stores(),
      ]);

      managerState.statusList = statuses || [];
      managerState.stores = stores || [];

      if (!managerState.filters.store && managerState.stores.length) {
        managerState.filters.store = managerState.stores[0].alias;
      }

      await this.loadPaymentProviders();
      await this.loadOrders();
    } catch (error) {
      this.error = getErrorMessage(error, 'Error loading Ekom Manager.');
      this.loading = false;
      this.render();
    }
  }

  private async loadPaymentProviders(resetSelection = false): Promise<void> {
    if (!managerState.filters.store) {
      managerState.paymentProviders = [];
      return;
    }

    managerState.paymentProviders = await this.api.paymentProviders(managerState.filters.store);

    if (resetSelection) {
      managerState.filters.paymentProvider = '';
    }

    if (managerState.filters.paymentProvider) {
      const selectedProviderStillExists = managerState.paymentProviders.some(provider => provider.key === managerState.filters.paymentProvider);

      if (!selectedProviderStillExists) {
        managerState.filters.paymentProvider = '';
      }
    }
  }

  private async loadOrders(): Promise<void> {
    if (!managerState.filters.store) {
      this.loading = false;
      this.result = { orders: [], count: 0, totalPages: 0 };
      this.render();
      return;
    }

    this.loading = true;
    this.error = '';
    this.render();

    try {
      const result = await this.api.searchOrders(managerState.filters, this.page);
      this.result = {
        ...result,
        orders: result.orders || [],
      };
    } catch (error) {
      this.error = getErrorMessage(error, 'Error searching orders.');
    } finally {
      this.loading = false;
      this.render();
    }
  }

  private render(): void {
    this.innerHTML = `
      <style>${managerStyles}</style>
      <section class="ekmManager">
        <div class="ekmManager__body">
          ${this.renderOrders()}
        </div>
      </section>
      ${this.renderOverlay()}
    `;

    this.bindEvents();
  }

  private renderOrders(): string {
    return `
      <div class="cards">
        <div class="card ekmSummaryCard">
          <span class="ekmSummaryCard__label">Orders</span>
          <strong class="ekmSummaryCard__value">${escapeHtml(this.result.count || 0)}</strong>
        </div>
        <div class="card ekmSummaryCard">
          <span class="ekmSummaryCard__label">Payments total</span>
          <strong class="ekmSummaryCard__value">${escapeHtml(this.result.grandTotal || 0)}</strong>
        </div>
        <div class="card ekmSummaryCard">
          <span class="ekmSummaryCard__label">Average order amount</span>
          <strong class="ekmSummaryCard__value">${escapeHtml(this.result.averageAmount || 0)}</strong>
        </div>
      </div>
      ${this.renderToolbar()}
      ${this.error ? `<p class="status status--error">${escapeHtml(this.error)}</p>` : ''}
      ${this.loading ? '<p>Hang tight! Fetching your order details... This might take a moment.</p>' : this.renderOrderTable()}
      ${!this.loading && this.result.totalPages > 1 ? this.renderPagination() : ''}
    `;
  }

  private renderToolbar(): string {
    const filters = managerState.filters;
    return `
      <div class="umb-sub-header">
        <div class="ekmManager__filters">
          <label class="ekmManager__filter">Order Status:
            <select data-field="orderStatus">
              <option value="CompletedOrders" ${filters.orderStatus === 'CompletedOrders' ? 'selected' : ''}>Completed Orders</option>
              <option value="AllOrders" ${filters.orderStatus === 'AllOrders' ? 'selected' : ''}>All Orders</option>
              ${managerState.statusList.map(status => {
                const value = getStatusValue(status);
                return `<option value="${escapeHtml(value)}" ${filters.orderStatus === value ? 'selected' : ''}>${escapeHtml(status.label)}</option>`;
              }).join('')}
            </select>
          </label>
          <label class="ekmManager__filter">Date From:
            <input type="date" data-field="dateFrom" value="${escapeHtml(filters.dateFrom)}">
          </label>
          <label class="ekmManager__filter">Date To:
            <input type="date" data-field="dateTo" value="${escapeHtml(filters.dateTo)}">
          </label>
          <label class="ekmManager__filter">Store:
            <select data-field="store">
              ${managerState.stores.map(store => `<option value="${escapeHtml(store.alias)}" ${filters.store === store.alias ? 'selected' : ''}>${escapeHtml(store.title)}</option>`).join('')}
            </select>
          </label>
          <div class="ekmManager__search">
            <button type="button" class="btn-outline" data-action="open-export">Export</button>
            <button type="button" class="btn-primary" data-action="open-filter">Filter</button>
            <div class="form-search"><input type="text" data-field="query" value="${escapeHtml(filters.query)}" placeholder="Type to search..."></div>
          </div>
        </div>
      </div>
    `;
  }

  private renderOrderTable(): string {
    if (!this.result.orders.length) {
      return '<div class="umb-table"><div class="umb-table-row"><div class="umb-table-cell">No orders found</div></div></div>';
    }

    return `
      <div class="umb-table">
        <div class="umb-table-head">
          <div class="umb-table-row">
            <div class="umb-table-cell not-fixed"></div>
            <div class="umb-table-cell">Order Number</div>
            <div class="umb-table-cell">Status</div>
            <div class="umb-table-cell">Name</div>
            <div class="umb-table-cell">Store</div>
            <div class="umb-table-cell">Created</div>
            <div class="umb-table-cell">Payment</div>
          </div>
        </div>
        <div class="umb-table-body">
          ${this.result.orders.map(order => this.renderOrderRow(order)).join('')}
        </div>
      </div>
    `;
  }

  private renderOrderRow(order: OrderListItem): string {
    return `
      <div class="umb-table-row">
        <div class="umb-table-cell not-fixed" data-label="Action"><button class="btn-success" type="button" data-action="view-order" data-order-id="${escapeHtml(order.uniqueId)}">View</button></div>
        <div class="umb-table-cell" data-label="Order Number" title="${escapeHtml(order.uniqueId)}">${escapeHtml(order.referenceId)}</div>
        <div class="umb-table-cell" data-label="Status">
          <select data-action="change-row-status" data-order-id="${escapeHtml(order.uniqueId)}">
            ${managerState.statusList.map(status => {
              const value = getStatusValue(status);
              return `<option value="${escapeHtml(value)}" ${order.orderStatusCol === value ? 'selected' : ''}>${escapeHtml(status.label)}</option>`;
            }).join('')}
          </select>
        </div>
        <div class="umb-table-cell" data-label="Name">${escapeHtml(order.customerName)}</div>
        <div class="umb-table-cell" data-label="Store">${escapeHtml(order.storeAlias)}</div>
        <div class="umb-table-cell" data-label="Created">${escapeHtml(formatDate(order.createDate))}</div>
        <div class="umb-table-cell" data-label="Payment">${escapeHtml(order.formattedTotal)}</div>
      </div>
    `;
  }

  private renderPagination(): string {
    return `
      <div class="pagination">
        <ul>
          ${pageRange(this.page, this.result.totalPages).map(page => {
            const pageNumber = Number(String(page).replace('...', ''));
            return `<li class="${pageNumber === this.page ? 'active' : ''}"><button type="button" data-action="set-page" data-page="${pageNumber}" ${pageNumber === this.page ? 'disabled' : ''}>${escapeHtml(page)}</button></li>`;
          }).join('')}
        </ul>
      </div>
    `;
  }

  private renderOverlay(): string {
    if (this.overlay === 'filter') {
      return this.renderFilterOverlay();
    }

    if (this.overlay === 'export') {
      return this.renderExportOverlay();
    }

    if (this.overlay === 'order' && this.selectedOrder) {
      return this.renderOrderOverlay(this.selectedOrder);
    }

    return '';
  }

  private renderFilterOverlay(): string {
    const filters = managerState.filters;
    return `
      <div class="ekmOverlay">
        <div class="ekmOverlay__panel ekmOverlay__panel--small">
          <div class="ekmOverlay__header"><h2>Filter</h2><button class="btn-reset" type="button" data-action="close-overlay">&times;</button></div>
          <div class="ekmOverlay__content">
            <label class="control-group">Payment provider:
              <select data-filter-field="paymentProvider">
                <option value="">Select payment provider</option>
                ${managerState.paymentProviders.map(provider => `<option value="${escapeHtml(provider.key)}" ${filters.paymentProvider === provider.key ? 'selected' : ''}>${escapeHtml(provider.title)}</option>`).join('')}
              </select>
            </label>
            ${this.renderFilterInput('productSku', 'Product SKU:', 'Exact SKU')}
            ${this.renderFilterInput('trackingSource', 'Tracking source:', 'facebook')}
            ${this.renderFilterInput('trackingMedium', 'Tracking medium:', 'paid-social')}
            ${this.renderFilterInput('trackingCampaign', 'Tracking campaign:', 'summer_2026')}
            ${this.renderFilterInput('trackingTerm', 'Tracking term:', 'running shoes')}
            ${this.renderFilterInput('trackingContent', 'Tracking content:', 'hero_banner')}
            ${this.renderFilterInput('trackingClickId', 'Tracking click id:', 'gclid or fbclid')}
            <div style="margin-top:25px; display:flex; gap:10px;"><button type="button" class="btn-success" data-action="apply-filter">Apply</button><button type="button" class="btn-outline" data-action="close-overlay">Cancel</button></div>
          </div>
        </div>
      </div>
    `;
  }

  private renderFilterInput(field: keyof ManagerFilters, label: string, placeholder: string): string {
    return `
      <label class="control-group">${escapeHtml(label)}
        <input type="text" data-filter-field="${escapeHtml(field)}" value="${escapeHtml(managerState.filters[field])}" placeholder="${escapeHtml(placeholder)}">
      </label>
    `;
  }

  private renderExportOverlay(): string {
    return `
      <div class="ekmOverlay">
        <div class="ekmOverlay__panel ekmOverlay__panel--small">
          <div class="ekmOverlay__header"><h2>Export orders</h2><button class="btn-reset" type="button" data-action="close-overlay" ${this.exporting ? 'disabled' : ''}>&times;</button></div>
          <div class="ekmOverlay__content">
            <p>Choose how to export the orders matching the current filters.</p>
            <label style="display:block; margin-top:15px;"><input type="checkbox" data-field="includeOrderLines" ${this.exportIncludeOrderLines ? 'checked' : ''} ${this.exporting ? 'disabled' : ''}> Include order lines</label>
            ${this.exportIncludeOrderLines ? '<p style="margin-top:10px;">Including order lines can take longer because each matching order needs to be loaded before the CSV is created.</p>' : ''}
            ${this.exporting ? '<p style="margin-top:15px;">Exporting orders. This may take a while...</p>' : ''}
            <div style="margin-top:25px; display:flex; gap:10px;"><button type="button" class="btn-success" data-action="export-orders" ${this.exporting ? 'disabled' : ''}>Export</button><button type="button" class="btn-outline" data-action="close-overlay" ${this.exporting ? 'disabled' : ''}>Cancel</button></div>
          </div>
        </div>
      </div>
    `;
  }

  private renderOrderOverlay(order: OrderInfo): string {
    return `
      <div class="ekmOverlay">
        <div class="ekmOverlay__panel ekmOrderOverlay__panel">
          <div class="ekmOverlay__header"><h2>View Order</h2><button class="btn-reset" type="button" data-action="close-overlay">&times;</button></div>
          <div class="ekmOverlay__content ekmOrder">
            ${this.renderOrderDetails(order)}
          </div>
        </div>
      </div>
      ${this.customerEditorOpen ? this.renderCustomerEditor() : ''}
      ${this.orderLineEditorOpen ? this.renderOrderLineEditor() : ''}
    `;
  }

  private renderOrderDetails(order: OrderInfo): string {
    const customer = order.customerInformation?.customer || {};
    const shipping = order.customerInformation?.shipping || {};
    const orderStatus = this.getOrderStatusValue(order.orderStatus);
    return `
      <div class="ekmOrder__header">
        <h1>Order number: ${escapeHtml(order.referenceId)}</h1>
        <div class="ekmOrderStatusBar">
          <label class="ekmOrderStatusBar__status">Order Status:
            <select data-field="orderStatusOverlay">
              ${managerState.statusList.map(status => {
                const value = getStatusValue(status);
                return `<option value="${escapeHtml(value)}" ${orderStatus === value ? 'selected' : ''}>${escapeHtml(status.label)}</option>`;
              }).join('')}
            </select>
          </label>
          <label class="ekmCheckboxLabel"><input type="checkbox" data-field="notifyOrderStatus"> Fire events?</label>
          <button type="button" class="btn-success" data-action="save-overlay-status">Save</button>
          <button type="button" class="btn-outline ekmOrderStatusBar__print" data-action="print-order">Print</button>
        </div>
        <p>UniqueId: ${escapeHtml(order.uniqueId)}</p>
        <p>Created date: ${escapeHtml(formatDate(order.createDate))}</p>
        <p>Paid date: ${escapeHtml(formatDate(order.paidDate))}</p>
        <p>Store: ${escapeHtml(order.storeInfo?.alias || order.storeAlias)}</p>
        <p>Payment: <strong>${escapeHtml(order.chargedAmount?.currencyString)}</strong></p>
        ${this.renderOrderActions()}
      </div>
      <div class="ekmSplit">
        <div class="ekmSplit__column"><h4>Billing</h4><button type="button" class="btn-outline" data-action="open-customer-editor" style="margin-bottom:10px;">Edit customer information</button>${this.renderAddress(customer)}${this.renderExtraProperties(customer.properties, 'customer')}</div>
        <div class="ekmSplit__column"><h4>Shipping</h4>${hasShippingInfo(shipping) ? `${this.renderAddress(shipping)}${this.renderExtraProperties(shipping.properties, 'shipping')}` : '<p style="font-weight:bold">Same as billing address</p>'}</div>
      </div>
      <div class="ekmSplit">
        <div class="ekmSplit__column">${this.renderProvider('Payment Method', order.paymentProvider, 'custompayment')}</div>
        <div class="ekmSplit__column">${this.renderProvider('Shipping Method', order.shippingProvider, 'customshipping')}</div>
      </div>
      ${this.renderOrderLines(order)}
      ${this.renderTracking(order)}
      ${this.renderConsent(order)}
      ${this.renderActivityLogs()}
    `;
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

    return `<h5 style="margin-top:20px; font-weight:bold;">Extra ${prefix === 'shipping' ? 'Shipping' : 'Customer'} Data</h5><ul>${entries.map(([key, value]) => `<li><strong>${escapeHtml(cleanKey(key))}</strong>: ${escapeHtml(value)}</li>`).join('')}</ul>`;
  }

  private renderProvider(title: string, provider: Record<string, any> | undefined, prefix: string): string {
    if (!provider) {
      return '';
    }

    return `<h4>${escapeHtml(title)}</h4><h4><strong>${escapeHtml(provider.title)}</strong></h4>${provider.price ? `<p>Price: ${escapeHtml(provider.price.withVat?.currencyString)}</p>` : ''}${this.renderExtraProperties(provider.customData, prefix)}`;
  }

  private renderOrderLines(order: OrderInfo): string {
    const lines = Array.isArray(order.orderLines) ? order.orderLines : [];
    return `
      <div style="align-items:center; display:flex; gap:10px; justify-content:space-between;"><h4>Order Details</h4><button type="button" class="btn-outline" data-action="open-order-line-editor">Add order line</button></div>
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed">Product</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Unit Price (inc VAT)</div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">Total (inc VAT)</div></div></div>
        <div class="umb-table-body">
          ${lines.map((line: Record<string, any>) => `<div class="umb-table-row"><div class="umb-table-cell"><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${escapeHtml(line.key)}" data-product-title="${escapeHtml(line.product?.title)}" ${this.removingOrderLineId === line.key ? 'disabled' : ''} aria-label="Remove ${escapeHtml(line.product?.title)}" title="Remove order line">&#128465;</button></div><div class="umb-table-cell not-fixed">${escapeHtml(line.product?.title)} (${escapeHtml(line.product?.sku)})${line.variant ? `<small style="display:block; margin-top:3px;">${escapeHtml(line.variant.title)} ${line.variant.sku ? `(${escapeHtml(line.variant.sku)})` : ''}</small>` : ''}${renderOrderLineProperties(line)}</div><div class="umb-table-cell">${escapeHtml(line.quantity)}</div><div class="umb-table-cell">${escapeHtml(line.product?.price?.withVat?.currencyString)}</div><div class="umb-table-cell">${escapeHtml(line.amount?.vat?.currencyString)}</div><div class="umb-table-cell">-${escapeHtml(line.amount?.discountAmount?.currencyString)}</div><div class="umb-table-cell"><strong>${escapeHtml(line.amount?.withVat?.currencyString)}</strong></div></div>`).join('')}
        </div>
        <div class="umb-table-footer">
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Sub Total (inc VAT)</div><div class="umb-table-cell">${escapeHtml(order.subTotal?.withVat?.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">-${escapeHtml(order.discountAmount?.currencyString)}</div></div>
          ${order.shippingProvider ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Shipping Total</div><div class="umb-table-cell">${escapeHtml(order.shippingProvider.price?.withVat?.currencyString)}</div></div>` : ''}
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">${escapeHtml(order.chargedVat?.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Total</div><div class="umb-table-cell"><strong>${escapeHtml(order.chargedAmount?.currencyString)}</strong></div></div>
        </div>
      </div>
    `;
  }

  private renderTracking(order: OrderInfo): string {
    const tracking = order.tracking;

    if (!hasTrackingData(tracking)) {
      return '<div class="ekmOrderTracking"><h4>Tracking</h4><p>No tracking data was captured for this order.</p></div>';
    }

    return `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Tracking</h4><button class="btn-reset" type="button" data-action="toggle-tracking">${this.trackingExpanded ? 'Hide' : 'Show'}</button></div>${this.trackingExpanded ? renderTrackingDetails(tracking) : ''}</div>`;
  }

  private renderConsent(order: OrderInfo): string {
    const consent = order.consent;

    if (!hasConsentData(consent)) {
      return '<div class="ekmOrderTracking"><h4>Consent</h4><p>No consent data was captured for this order.</p></div>';
    }

    return `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Consent</h4><button class="btn-reset" type="button" data-action="toggle-consent">${this.consentExpanded ? 'Hide' : 'Show'}</button></div>${this.consentExpanded ? `<p>Resolved: ${escapeHtml(formatDate(consent.resolvedAtUtc))}</p><p>Source: ${escapeHtml(consent.source)}</p><p>Analytics: ${consentLabel(consent.analytics)}</p><p>Marketing: ${consentLabel(consent.marketing)}</p>` : ''}</div>`;
  }

  private renderActivityLogs(): string {
    if (this.orderLogsLoading) {
      return '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Loading activity...</p></div>';
    }

    if (!this.orderLogs.length) {
      return '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>No activity yet.</p></div>';
    }

    return `<div class="ekmOrderActivityLog"><h4>Activity log</h4>${this.orderLogs.map(log => `<div class="ekmOrderActivityLog__item"><div class="ekmOrderActivityLog__date">${escapeHtml(formatDate(log.date))}</div><div>${escapeHtml(log.message)}</div></div>`).join('')}</div>`;
  }

  private renderOrderActions(): string {
    if (!this.orderActions.length && !this.orderActionsLoading) {
      return '';
    }

    return `<div style="margin-top:15px;"><h4>Order Actions</h4><div style="display:flex; flex-wrap:wrap; gap:10px;">${this.orderActions.map(action => `<button type="button" class="${action.look === 'primary' ? 'btn-success' : 'btn-outline'}" data-action="execute-order-action" data-action-key="${escapeHtml(action.key)}" ${action.enabled === false || this.executingActionKey === action.key ? 'disabled' : ''}>${escapeHtml(action.label)}</button>`).join('')}</div>${this.orderActionsLoading ? '<p>Loading actions...</p>' : ''}</div>`;
  }

  private renderCustomerEditor(): string {
    const model = this.customerEditModel;

    if (!model) {
      return '';
    }

    return `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel"><div class="ekmOverlay__header"><h3>Edit customer information</h3><button class="btn-reset" type="button" data-action="close-customer-editor" ${this.customerSaving ? 'disabled' : ''}>&times;</button></div><div class="ekmOverlay__content"><div class="ekmSplit"><div class="ekmSplit__column"><h4>Billing</h4>${this.renderCustomerFields(model.customer, 'customer')}</div><div class="ekmSplit__column"><h4>Shipping</h4>${this.renderCustomerFields(model.shipping, 'shipping')}</div></div><div style="display:flex; justify-content:flex-end; gap:10px; padding-top:20px; border-top:1px solid #d8d7d9;"><button class="btn-outline" type="button" data-action="close-customer-editor" ${this.customerSaving ? 'disabled' : ''}>Cancel</button><button class="btn-success" type="button" data-action="save-customer-information" ${this.customerSaving ? 'disabled' : ''}>${this.customerSaving ? 'Saving...' : 'Save customer information'}</button></div></div></div></div>`;
  }

  private renderCustomerFields(fields: CustomerInformationField[], group: string): string {
    return fields.map(field => `<label class="control-group">${escapeHtml(field.label)} ${field.isExtra ? `<small>(${escapeHtml(field.key)})</small>` : ''}<input type="text" data-customer-group="${group}" data-customer-key="${escapeHtml(field.key)}" value="${escapeHtml(field.value)}" ${this.customerSaving ? 'disabled' : ''}></label>`).join('');
  }

  private renderOrderLineEditor(): string {
    const model = this.orderLineEditModel;

    if (!model) {
      return '';
    }

    return `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel"><div class="ekmOverlay__header"><h3>Add order line</h3><button class="btn-reset" type="button" data-action="close-order-line-editor" ${this.orderLineSaving ? 'disabled' : ''}>&times;</button></div><div class="ekmOverlay__content"><label class="control-group">Product ID<input type="text" data-order-line-field="productId" value="${escapeHtml(model.productId)}" required ${this.orderLineSaving ? 'disabled' : ''}></label><label class="control-group">Variant ID<input type="text" data-order-line-field="variantId" value="${escapeHtml(model.variantId)}" ${this.orderLineSaving ? 'disabled' : ''}></label><label class="control-group" style="padding-bottom:20px;">Quantity<input type="number" min="0.000001" step="any" data-order-line-field="quantity" value="${escapeHtml(model.quantity)}" required ${this.orderLineSaving ? 'disabled' : ''}></label><div style="display:flex; justify-content:flex-end; gap:10px; padding-top:20px; border-top:1px solid #d8d7d9;"><button class="btn-outline" type="button" data-action="close-order-line-editor" ${this.orderLineSaving ? 'disabled' : ''}>Cancel</button><button class="btn-success" type="button" data-action="save-order-line" ${this.orderLineSaving ? 'disabled' : ''}>${this.orderLineSaving ? 'Adding...' : 'Add order line'}</button></div></div></div></div>`;
  }

  private bindEvents(): void {
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
      window.print();
      return;
    }

    if (action === 'toggle-tracking') {
      this.trackingExpanded = !this.trackingExpanded;
      this.renderPreservingOverlayScroll();
      return;
    }

    if (action === 'toggle-consent') {
      this.consentExpanded = !this.consentExpanded;
      this.renderPreservingOverlayScroll();
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

    if (field === 'orderStatusOverlay' || field === 'notifyOrderStatus' || field === 'query') {
      return;
    }

    if (!field || !(field in managerState.filters)) {
      return;
    }

    (managerState.filters as Record<string, string>)[field] = target.value;
    this.page = 1;

    if (field === 'store') {
      await this.loadPaymentProviders(true);
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
    this.querySelectorAll<HTMLInputElement | HTMLSelectElement>('[data-filter-field]').forEach(input => {
      const field = input.dataset.filterField;

      if (field && field in managerState.filters) {
        (managerState.filters as Record<string, string>)[field] = input.value;
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

    try {
      this.selectedOrder = await this.api.orderInfo(orderId);
      this.overlay = 'order';
      this.trackingExpanded = false;
      this.consentExpanded = false;
      this.render();
      await Promise.all([this.loadOrderLogs(orderId), this.loadOrderActions(orderId)]);
    } catch (error) {
      this.showError(getErrorMessage(error, 'Error on getting orderInfo.'));
    }
  }

  private async loadOrderLogs(orderId: string): Promise<void> {
    this.orderLogsLoading = true;
    this.render();

    try {
      this.orderLogs = await this.api.orderLogs(orderId);
    } catch {
      this.orderLogs = [];
    } finally {
      this.orderLogsLoading = false;
      this.render();
    }
  }

  private async loadOrderActions(orderId: string): Promise<void> {
    this.orderActionsLoading = true;
    this.render();

    try {
      this.orderActions = await this.api.orderActions(orderId);
    } catch {
      this.orderActions = [];
    } finally {
      this.orderActionsLoading = false;
      this.render();
    }
  }

  private async saveOverlayStatus(): Promise<void> {
    if (!this.selectedOrder?.uniqueId) {
      return;
    }

    const status = this.querySelector<HTMLSelectElement>('[data-field="orderStatusOverlay"]')?.value || '';
    const notify = this.querySelector<HTMLInputElement>('[data-field="notifyOrderStatus"]')?.checked || false;

    try {
      await this.api.changeOrderStatus(this.selectedOrder.uniqueId, status, notify);
      this.selectedOrder.orderStatus = status;
      this.showSuccess('Order status updated.');
      await this.loadOrders();
      await this.loadOrderLogs(this.selectedOrder.uniqueId);
    } catch (error) {
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
    this.overlay = '';
    this.selectedOrder = undefined;
    this.customerEditorOpen = false;
    this.customerEditModel = undefined;
    this.orderLineEditorOpen = false;
    this.orderLineEditModel = undefined;
    this.render();
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

    if (!orderId) {
      return;
    }

    try {
      await this.api.changeOrderStatus(orderId, select.value, true);
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
    if (!this.selectedOrder?.uniqueId || !actionKey || this.executingActionKey) {
      return;
    }

    const action = this.orderActions.find(item => item.key === actionKey);

    if (action?.confirmMessage && !window.confirm(action.confirmMessage)) {
      return;
    }

    this.executingActionKey = actionKey;
    this.render();

    try {
      const response = await this.api.executeOrderAction(this.selectedOrder.uniqueId, actionKey);
      const blob = await response.blob();
      const contentDisposition = response.headers.get('content-disposition') || '';
      const contentType = response.headers.get('content-type') || '';

      if (contentDisposition.toLowerCase().includes('filename=') || contentType.startsWith('application/pdf') || contentType.startsWith('application/octet-stream') || contentType.startsWith('image/')) {
        window.open(URL.createObjectURL(blob), '_blank');
      } else {
        const message = await blob.text();
        this.showSuccess(tryParseMessage(message));
      }

      this.selectedOrder = await this.api.orderInfo(this.selectedOrder.uniqueId);
      await this.loadOrderLogs(this.selectedOrder.uniqueId);
      await this.loadOrderActions(this.selectedOrder.uniqueId);
    } catch (error) {
      this.showError(getErrorMessage(error, 'Order action failed.'));
    } finally {
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
    .filter(([key, value]) => Boolean(value) && key.toLowerCase().startsWith(prefix) && !isDefaultKey(key))
    .map(([key, value]) => [key, decodeHtml(value)]);
}

function hasShippingInfo(shipping: Record<string, any>): boolean {
  return Boolean(shipping?.name || shipping?.email || shipping?.address || shipping?.apartment || shipping?.city || shipping?.country || shipping?.zipCode || shipping?.phone);
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
  return Boolean(tracking && (tracking.source || tracking.medium || tracking.campaign || tracking.term || tracking.content || tracking.clickId || tracking.clickIdType || tracking.landingUrl || tracking.referrer || tracking.captureMethod || tracking.capturedAtUtc || tracking.hasCookieSupport !== null && tracking.hasCookieSupport !== undefined || tracking.ga4?.clientId || tracking.ga4?.sessionId || tracking.meta?.fbp || tracking.meta?.fbc));
}

function renderTrackingDetails(tracking: Record<string, any>): string {
  const ga4Data = Object.entries(tracking.ga4?.data || {});
  const metaData = Object.entries(tracking.meta?.data || {});
  return `<div class="ekmSplit"><div class="ekmSplit__column">${renderOptional('Captured', formatDate(tracking.capturedAtUtc))}${renderOptional('Capture method', tracking.captureMethod)}${tracking.hasCookieSupport !== null && tracking.hasCookieSupport !== undefined ? `<p>Cookie support: ${tracking.hasCookieSupport ? 'Yes' : 'No'}</p>` : ''}${renderOptional('Source', tracking.source)}${renderOptional('Medium', tracking.medium)}${renderOptional('Campaign', tracking.campaign)}${renderOptional('Term', tracking.term)}${renderOptional('Content', tracking.content)}${renderOptional('Click ID', tracking.clickId)}${renderOptional('Click ID Type', tracking.clickIdType)}${renderOptional('Landing URL', tracking.landingUrl)}${renderOptional('Referrer', tracking.referrer)}</div><div class="ekmSplit__column"><h5>GA4</h5>${renderOptional('Client ID', tracking.ga4?.clientId)}${renderOptional('Session ID', tracking.ga4?.sessionId)}${renderPairs(ga4Data)}<h5>Meta</h5>${renderOptional('FBP', tracking.meta?.fbp)}${renderOptional('FBC', tracking.meta?.fbc)}${renderPairs(metaData)}</div></div>`;
}

function renderOptional(label: string, value: unknown): string {
  return value ? `<p class="ekmOrderTracking__wrap">${escapeHtml(label)}: ${escapeHtml(value)}</p>` : '';
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
