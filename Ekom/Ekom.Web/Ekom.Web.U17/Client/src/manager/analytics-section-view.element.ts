import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { ChartData, EkomManagerApi, ManagerFilters, MostSoldProductsResult, escapeHtml, formatAnalyticsAmount, getStatusValue, managerState, managerStyles, pageRange, renderLineChart, downloadBlob } from './manager-shared';
import { AnalyticsBucket, AnalyticsMetric, analyticsSummary, groupAnalytics, percentageChange, presetRange, previousPeriod } from './analytics-data';

export class EkomAnalyticsSectionViewElement extends UmbElementMixin(HTMLElement) {
  private readonly api = new EkomManagerApi();
  private loading = true;
  private loadingMostSoldProducts = false;
  private error = '';
  private productError = '';
  private chartData?: ChartData;
  private previousData?: ChartData;
  private chartFilters?: ManagerFilters;
  private mostSoldProducts: MostSoldProductsResult = { products: [], count: 0, totalPages: 0, page: 1 };
  private pageMostSoldProducts = 1;
  private resizeObserver?: ResizeObserver;
  private chartRequest = 0;
  private productRequest = 0;
  private chartAbort?: AbortController;
  private productAbort?: AbortController;
  private disposed = false;
  private productsVisible = false;
  private metric: AnalyticsMetric = 'amount';
  private grouping = 'day';
  private compare = true;
  private table = false;

  override connectedCallback(): void {
    super.connectedCallback();
    this.disposed = false;
    if (this.hasAttribute('classic')) {
      for (const field of ['store', 'dateFrom', 'dateTo', 'orderStatus'] as const) {
        const value = this.getAttribute(field.toLowerCase());
        if (value) managerState.filters[field] = value;
      }
    }
    this.render();
    this.resizeObserver = new ResizeObserver(() => this.renderCharts());
    this.resizeObserver.observe(this);
    void this.initialize();
  }

  override disconnectedCallback(): void {
    this.disposed = true;
    this.chartRequest++;
    this.productRequest++;
    this.chartAbort?.abort();
    this.productAbort?.abort();
    this.resizeObserver?.disconnect();
    this.resizeObserver = undefined;
    super.disconnectedCallback();
  }

  private get currency() {
    return managerState.stores.find(store => store.alias === (this.chartFilters?.store || managerState.filters.store))?.currency;
  }

  private async initialize(): Promise<void> {
    try {
      const [statuses, stores] = await Promise.all([this.api.statusList(), this.api.stores()]);
      if (this.disposed) return;
      managerState.statusList = statuses || [];
      managerState.stores = stores || [];
      if (!managerState.stores.some(store => store.alias === managerState.filters.store)) managerState.filters.store = managerState.stores[0]?.alias || '';
      await this.loadAnalytics();
    } catch (error) {
      if (this.disposed) return;
      this.error = getErrorMessage(error, 'Unable to load Analytics.');
      this.loading = false;
      this.render();
    }
  }

  private async loadAnalytics(): Promise<void> {
    await Promise.all([this.loadCharts(), this.productsVisible ? this.loadMostSoldProducts() : Promise.resolve()]);
  }

  private async loadCharts(): Promise<void> {
    const request = ++this.chartRequest;
    this.chartAbort?.abort();
    const abort = this.chartAbort = new AbortController();
    const filters = { ...managerState.filters };
    this.chartData = undefined;
    this.previousData = undefined;
    this.error = '';
    this.loading = !!filters.store;
    this.render();
    if (!filters.store) return;
    try {
      const comparisonFilters = this.compare ? { ...filters, ...previousPeriod(filters.dateFrom, filters.dateTo) } : undefined;
      const [current, previous] = await Promise.all([
        this.api.charts(filters, abort.signal),
        comparisonFilters ? this.api.charts(comparisonFilters, abort.signal) : Promise.resolve(undefined),
      ]);
      if (request !== this.chartRequest || this.disposed) return;
      this.chartData = current;
      this.previousData = previous;
      this.chartFilters = filters;
      if (current.mixedCurrencies) this.metric = 'orders';
    } catch (error) {
      if (request !== this.chartRequest || this.disposed) return;
      this.error = getErrorMessage(error, 'Unable to load charts.');
    } finally {
      if (request === this.chartRequest && !this.disposed) { this.loading = false; this.render(); }
    }
  }

  private async loadMostSoldProducts(): Promise<void> {
    const request = ++this.productRequest;
    this.productAbort?.abort();
    const abort = this.productAbort = new AbortController();
    const filters = { ...managerState.filters };
    this.mostSoldProducts = { products: [], count: 0, totalPages: 0, page: this.pageMostSoldProducts };
    this.productError = '';
    this.loadingMostSoldProducts = !!filters.store;
    this.render();
    if (!filters.store) return;
    try {
      const result = await this.api.mostSoldProducts(filters, this.pageMostSoldProducts, 20, abort.signal);
      if (request !== this.productRequest || this.disposed) return;
      this.mostSoldProducts = { ...result, products: result.products || [] };
      this.pageMostSoldProducts = result.page || this.pageMostSoldProducts;
    } catch (error) {
      if (request === this.productRequest && !this.disposed) this.productError = getErrorMessage(error, 'Unable to load products.');
    } finally {
      if (request === this.productRequest && !this.disposed) { this.loadingMostSoldProducts = false; this.render(); }
    }
  }

  private get buckets(): AnalyticsBucket[] { return this.chartData ? groupAnalytics(this.chartData, this.previousData, this.grouping) : []; }
  private get comparisonMoneyAvailable(): boolean {
    return !!this.previousData && !this.chartData?.mixedCurrencies && !this.previousData.mixedCurrencies &&
      (!this.chartData?.currency || !this.previousData.currency || this.chartData.currency === this.previousData.currency);
  }
  private amount(value: number | null | undefined): string {
    if (value == null || this.chartData?.mixedCurrencies) return '—';
    const code = this.chartData?.currency || this.previousData?.currency;
    if (!code || code === this.currency?.currencyValue || code === this.currency?.isoCurrencySymbol) return formatAnalyticsAmount(value, this.currency);
    if (/^[A-Z]{3}$/.test(code)) return formatAnalyticsAmount(value, { isoCurrencySymbol: code });
    return `${value.toLocaleString(undefined, { maximumFractionDigits: 2 })} ${code}`;
  }
  private label(metric: AnalyticsMetric): string { return metric === 'orders' ? 'Orders' : metric === 'average' ? 'Average order value' : 'Order amount'; }
  private value(value: number | null | undefined, metric = this.metric): string { return metric === 'orders' ? (value == null ? '—' : value.toLocaleString()) : this.amount(value); }
  private previousValue(bucket: AnalyticsBucket): number | null | undefined { return this.metric === 'orders' ? bucket.previousOrders : this.metric === 'average' ? bucket.previousAverage : bucket.previousAmount; }
  private range(start?: string, end?: string): string { return start === end ? start || '' : `${start || ''} – ${end || ''}`; }

  private render(): void {
    const f = managerState.filters;
    this.innerHTML = `<style>${managerStyles}
      ekom-analytics-section-view { display:block;color:var(--uui-color-text,#20232e) }
      .analytics {background:var(--uui-color-background,#f3f4f7);min-height:80vh}
      .analytics .card {background:var(--uui-color-surface,#fff);border:1px solid #dfe2e8;border-radius:10px;padding:18px;text-align:left;box-shadow:none}
      .analytics .analytics-main {margin-top:18px}
      .analytics h3 {margin:0;font-size:16px}
      .analytics button:disabled {opacity:.5;cursor:default}
      .analytics-toolbar {display:flex;flex-wrap:wrap;gap:12px;align-items:end;margin-bottom:18px}
      .analytics-toolbar label {display:grid;gap:5px;font-size:12px}
      .analytics-toolbar input,.analytics-toolbar select,.analytics-toolbar button,.analytics-table button {font:inherit;padding:8px 12px;border:1px solid #dfe2e8;border-radius:6px;background:var(--uui-color-surface,#fff);color:inherit}
      .analytics-toolbar button {cursor:pointer}
      .analytics-toolbar button[aria-pressed=true] {background:#e7f1ed;border-color:#26795c}
      .analytics-summary {display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px;margin:18px 0}
      .analytics-summary .card > span:first-child {display:block;color:#646b7c;font-size:12px;margin-bottom:8px}
      .analytics-summary strong {display:block;font-size:23px;margin:0 0 8px}
      .analytics-muted {color:#70778a;font-size:12px;line-height:1.5}
      .analytics-table-wrap {overflow:auto;max-height:520px}
      .analytics-table {width:100%;border-collapse:collapse}
      .analytics-table th,.analytics-table td {text-align:left;padding:15px 10px;border-bottom:1px solid #e8e9ed;white-space:nowrap}
      .analytics-table th {position:sticky;top:0;background:var(--uui-color-surface,#fff);font-size:11px;text-transform:uppercase;letter-spacing:.07em;color:#70778a}
      .analytics-main {padding:22px}
      .analytics-spinner {display:inline-block;width:14px;height:14px;margin-right:8px;border:2px solid #dfe2e8;border-top-color:#25765c;border-radius:50%;animation:analytics-spin 1s linear infinite}
      @keyframes analytics-spin {to {transform:rotate(360deg)}}
      @media (prefers-reduced-motion:reduce) {.analytics-spinner {animation:none}}
    </style><section class="ekmManager analytics"><div class="ekmManager__body">
      <div class="analytics-toolbar">
        <label>Store<select data-field="store">${managerState.stores.map(s => `<option value="${escapeHtml(s.alias)}" ${s.alias === f.store ? 'selected' : ''}>${escapeHtml(s.title)}</option>`).join('')}</select></label>
        <label>Order status<select data-field="orderStatus"><option value="CompletedOrders" ${f.orderStatus === 'CompletedOrders' ? 'selected' : ''}>Completed Orders</option><option value="AllOrders" ${f.orderStatus === 'AllOrders' ? 'selected' : ''}>All Orders</option>${managerState.statusList.map(s => `<option value="${escapeHtml(getStatusValue(s))}" ${getStatusValue(s) === f.orderStatus ? 'selected' : ''}>${escapeHtml(s.label)}</option>`).join('')}</select></label>
        <label>Date From<input type="date" data-field="dateFrom" value="${escapeHtml(f.dateFrom)}"></label>
        <label>Date To<input type="date" data-field="dateTo" value="${escapeHtml(f.dateTo)}"></label>
        <label>Date preset<select data-preset><option value="">Custom range</option>${[['today','Today'],['7','Last 7 days'],['30','Last 30 days'],['month','This month'],['year','This year']].map(([v,l])=>`<option value="${v}">${l}</option>`).join('')}</select></label>
        <label><span>Comparison</span><span><input type="checkbox" data-compare ${this.compare ? 'checked' : ''}> Previous period</span></label>
        <button data-refresh>Refresh</button>
      </div>
      ${this.error ? `<p role="alert">${escapeHtml(this.error)}</p>` : ''}
      ${this.loading ? '<p role="status">Loading analytics…</p>' : !f.store ? '<p>No accessible stores.</p>' : ''}
      ${this.chartData ? this.renderAnalytics() : ''}
      <div class="card analytics-main"><div class="analytics-toolbar"><h3>Most Sold Products</h3><button data-products>${this.productsVisible ? 'Hide products' : 'Load product breakdown'}</button></div>
        <p class="analytics-muted">Fetch the most sold products for the selected store and dates. This may take a minute for larger date ranges. Totals include VAT and discounts, excluding shipping and payment fees.</p>
        ${this.productsVisible ? `${this.productError ? `<p role="alert">${escapeHtml(this.productError)}</p><button data-product-retry>Retry products</button>` : ''}${this.loadingMostSoldProducts ? '<p role="status"><span class="analytics-spinner" aria-hidden="true"></span>Fetching product breakdown… This may take a minute. You can continue using Analytics.</p>' : this.renderMostSoldProducts()}` : ''}
      </div>
    </div></section>`;
    this.bindEvents();
    this.renderCharts();
  }

  private renderAnalytics(): string {
    const current = analyticsSummary(this.chartData!);
    const previous = this.previousData ? analyticsSummary(this.previousData) : undefined;
    return `<div class="analytics-summary">${(['amount','orders','average'] as AnalyticsMetric[]).map(metric => `<div class="card"><span>${this.label(metric)}</span><strong>${escapeHtml(this.value(current[metric], metric))}</strong><span class="analytics-muted">${previous && (metric === 'orders' || this.comparisonMoneyAvailable) ? escapeHtml(percentageChange(current[metric], previous[metric])) + ' vs previous period' : this.compare ? 'No monetary comparison available' : 'Selected period'}</span></div>`).join('')}</div>
      <p class="analytics-muted">Order amount is the recorded charged amount, after gift cards; it is not refund-adjusted revenue. Dates use ${this.chartData!.dateBasis === 'PaidDate' ? 'payment date' : 'order creation date'}, matching Orders.</p>
      ${this.chartData!.mixedCurrencies ? '<p role="status">This period contains multiple currencies. Monetary totals are hidden to avoid combining different currencies.</p>' : ''}
      ${this.previousData && !this.comparisonMoneyAvailable ? '<p class="analytics-muted">Monetary comparison is unavailable across different or mixed currencies.</p>' : ''}
      <div class="card analytics-main"><div class="analytics-toolbar">
        ${(['amount','orders','average'] as AnalyticsMetric[]).map(metric => `<button data-metric="${metric}" aria-pressed="${this.metric === metric}" ${metric !== 'orders' && this.chartData!.mixedCurrencies ? 'disabled' : ''}>${this.label(metric)}</button>`).join('')}
        <label>Group by<select data-grouping>${[['day','Daily'],['week','Weekly'],['month','Monthly']].map(([v,l])=>`<option value="${v}" ${this.grouping === v ? 'selected' : ''}>${l}</option>`).join('')}</select></label>
        <button data-table aria-pressed="${this.table}">${this.table ? 'Show chart' : 'Show data table'}</button><button data-csv>Download CSV</button>
      </div>
      <p class="analytics-muted">${this.previousData ? `Solid: ${escapeHtml(this.range(this.chartFilters?.dateFrom,this.chartFilters?.dateTo))}. Dashed: ${escapeHtml(this.range(this.previousData.ordersChart.points[0]?.x,this.previousData.ordersChart.points.at(-1)?.x))}. Comparison buckets cover the same number of days.` : 'Selected period.'} Click a bucket to open its orders.</p>
      ${this.table ? this.renderTable() : `<div style="height:360px"><canvas id="analytics-chart" style="width:100%;height:100%" role="img" aria-label="${this.label(this.metric)} over time. Use Show data table for exact values and order links."></canvas></div>`}
      </div>`;
  }

  private renderTable(): string {
    return `<div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Period</th><th>Orders</th><th>Order amount</th><th>Average order</th>${this.previousData ? `<th>Previous period</th><th>Previous ${this.label(this.metric).toLowerCase()}</th>` : ''}<th>Details</th></tr></thead><tbody>${this.buckets.map((b,i)=>`<tr><td>${escapeHtml(this.range(b.start,b.end))}</td><td>${b.orders}</td><td>${escapeHtml(this.amount(b.amount))}</td><td>${escapeHtml(this.amount(b.average))}</td>${this.previousData ? `<td>${escapeHtml(this.range(b.previousStart,b.previousEnd))}</td><td>${escapeHtml(this.metric === 'orders' || this.comparisonMoneyAvailable ? this.value(this.previousValue(b)) : '—')}</td>` : ''}<td><button data-orders="${i}">Open orders</button></td></tr>`).join('')}</tbody></table></div>`;
  }

  private renderMostSoldProducts(): string {
    if (!this.mostSoldProducts.products.length) {
      return '<p>No products found.</p>';
    }

    return `
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell">Product</div><div class="umb-table-cell">Sku</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Total</div></div></div>
        <div class="umb-table-body">
          ${this.mostSoldProducts.products.map(product => `<div class="umb-table-row"><div class="umb-table-cell">${escapeHtml(readProductValue(product, 'title', 'productTitle', 'name'))}</div><div class="umb-table-cell">${escapeHtml(readProductValue(product, 'sku', 'productSku'))}</div><div class="umb-table-cell">${escapeHtml(readProductValue(product, 'productCount', 'quantity', 'count'))}</div><div class="umb-table-cell">${escapeHtml(formatAnalyticsAmount(Number(readProductValue(product, 'total')), this.currency))}</div></div>`).join('')}
        </div>
      </div>
      ${this.mostSoldProducts.totalPages > 1 ? this.renderMostSoldProductsPagination() : ''}
    `;
  }

  private renderMostSoldProductsPagination(): string {
    return `
      <div class="pagination">
        <ul>
          ${pageRange(this.pageMostSoldProducts, this.mostSoldProducts.totalPages).map(page => {
            const pageNumber = Number(String(page).replace('...', ''));
            return `<li class="${pageNumber === this.pageMostSoldProducts ? 'active' : ''}"><button type="button" data-action="set-most-sold-page" data-page="${pageNumber}" ${pageNumber === this.pageMostSoldProducts ? 'disabled' : ''}>${escapeHtml(page)}</button></li>`;
          }).join('')}
        </ul>
      </div>
    `;
  }

  private renderCharts(): void {
    const canvas = this.querySelector<HTMLCanvasElement>('#analytics-chart');
    if (!canvas || !this.chartData || this.table) return;
    const buckets = this.buckets;
    const code = this.chartData.currency || this.previousData?.currency;
    const currency = code && /^[A-Z]{3}$/.test(code) ? { isoCurrencySymbol: code } : this.currency;
    const comparison = this.previousData && (this.metric === 'orders' || this.comparisonMoneyAvailable)
      ? { labels: [], points: buckets.map(b => ({ x: this.range(b.previousStart,b.previousEnd), y: this.previousValue(b) })) } : undefined;
    renderLineChart(canvas, { labels: [], points: buckets.map(b => ({ x: this.range(b.start,b.end), y: b[this.metric] })) }, '#25765c', {
      currency: this.metric === 'orders' ? undefined : currency, integer: this.metric === 'orders', comparison,
      formatValue: value => this.value(value), columnHover: true, onSelect: index => this.openOrders(index),
    });
  }

  private changedFilters(): void {
    this.pageMostSoldProducts = 1;
    this.dispatchEvent(new CustomEvent('analytics-filters-changed', { bubbles:true, detail: { ...managerState.filters } }));
    void this.loadAnalytics();
  }

  private openOrders(index: number): void {
    const bucket = this.buckets[index];
    if (!bucket || !this.chartFilters) return;
    const filters = { ...this.chartFilters, dateFrom: bucket.start, dateTo: bucket.end };
    for (const key of ['query','paymentProvider','shippingProvider','couponCode','giftcardCode','productSku','trackingSource','trackingMedium','trackingCampaign','trackingTerm','trackingContent','trackingClickId'] as const) filters[key] = '';
    Object.assign(managerState.filters, filters);
    if (this.hasAttribute('classic')) this.dispatchEvent(new CustomEvent('analytics-open-orders', { bubbles:true, detail:filters }));
    else {
      const params = new URLSearchParams({ analyticsDrill: '1', store:filters.store, start:filters.dateFrom, end:filters.dateTo, orderStatus:filters.orderStatus });
      const link = document.createElement('a');
      link.href = location.pathname.replace(/\/analytics\/?$/, '/orders') + '?' + params;
      this.appendChild(link); link.click(); link.remove();
    }
  }

  private exportCsv(): void {
    const rows = [['From','To','Orders','Order amount','Average order','Currency','Previous from','Previous to','Previous orders','Previous amount','Previous average']];
    for (const b of this.buckets) rows.push([b.start,b.end,String(b.orders),this.chartData?.mixedCurrencies ? '' : String(b.amount),this.chartData?.mixedCurrencies || b.average == null ? '' : String(b.average),this.chartData?.currency || '',b.previousStart || '',b.previousEnd || '',b.previousOrders == null ? '' : String(b.previousOrders),this.comparisonMoneyAvailable ? String(b.previousAmount ?? '') : '',this.comparisonMoneyAvailable ? String(b.previousAverage ?? '') : '']);
    const csv = rows.map(row => row.map(value => '"' + value.replaceAll('"','""') + '"').join(',')).join('\r\n');
    downloadBlob(new Blob(['\uFEFF',csv], {type:'text/csv;charset=utf-8'}), 'analytics.csv');
  }

  private bindEvents(): void {
    this.querySelectorAll<HTMLInputElement | HTMLSelectElement>('[data-field]').forEach(input => input.addEventListener('change', () => {
      const field = input.dataset.field as keyof ManagerFilters;
      managerState.filters[field] = input.value; this.changedFilters();
    }));
    this.querySelector<HTMLSelectElement>('[data-preset]')?.addEventListener('change', event => {
      const preset = (event.target as HTMLSelectElement).value;
      if (preset) { Object.assign(managerState.filters, presetRange(preset)); this.changedFilters(); }
    });
    this.querySelector('[data-compare]')?.addEventListener('change', () => { this.compare = !this.compare; void this.loadCharts(); });
    this.querySelector<HTMLSelectElement>('[data-grouping]')?.addEventListener('change', event => { this.grouping = (event.target as HTMLSelectElement).value; this.render(); });
    this.querySelectorAll<HTMLButtonElement>('[data-metric]').forEach(b => b.addEventListener('click', () => { this.metric = b.dataset.metric as AnalyticsMetric; this.render(); }));
    this.querySelector('[data-table]')?.addEventListener('click', () => { this.table = !this.table; this.render(); } );
    this.querySelector('[data-csv]')?.addEventListener('click', () => this.exportCsv());
    this.querySelector('[data-refresh]')?.addEventListener('click', () => void this.loadAnalytics());
    this.querySelectorAll<HTMLButtonElement>('[data-orders]').forEach(b => b.addEventListener('click', () => this.openOrders(Number(b.dataset.orders))));
    this.querySelector('[data-products]')?.addEventListener('click', () => {
      this.productsVisible = !this.productsVisible;
      if (this.productsVisible) void this.loadMostSoldProducts();
      else { this.productRequest++; this.productAbort?.abort(); this.render(); }
    });
    this.querySelector('[data-product-retry]')?.addEventListener('click', () => void this.loadMostSoldProducts());
    this.querySelectorAll<HTMLButtonElement>('[data-action="set-most-sold-page"]').forEach(b => b.addEventListener('click', () => { this.pageMostSoldProducts = Number(b.dataset.page); void this.loadMostSoldProducts(); }));
  }
}

function readProductValue(product: Record<string, unknown>, ...keys: string[]): unknown {
  for (const key of keys) if (product[key] != null) return product[key];
  return '';
}
function getErrorMessage(error: unknown, fallback: string): string { return error instanceof Error ? error.message : fallback; }
customElements.define('ekom-analytics-section-view', EkomAnalyticsSectionViewElement);
export default EkomAnalyticsSectionViewElement;
declare global { interface HTMLElementTagNameMap { 'ekom-analytics-section-view': EkomAnalyticsSectionViewElement; } }
