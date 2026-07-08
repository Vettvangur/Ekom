import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import {
  ChartData,
  EkomManagerApi,
  MostSoldProductsResult,
  escapeHtml,
  getStatusValue,
  managerState,
  managerStyles,
  pageRange,
  renderLineChart,
} from './manager-shared';

export class EkomAnalyticsSectionViewElement extends UmbElementMixin(HTMLElement) {
  private readonly api = new EkomManagerApi();
  private loading = true;
  private loadingMostSoldProducts = true;
  private error = '';
  private chartData?: ChartData;
  private mostSoldProducts: MostSoldProductsResult = { products: [], count: 0, totalPages: 0, page: 1 };
  private pageMostSoldProducts = 1;

  override connectedCallback(): void {
    super.connectedCallback();
    this.render();
    void this.initialize();
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

      await this.loadAnalytics();
    } catch (error) {
      this.error = getErrorMessage(error, 'Error loading Ekom analytics.');
      this.loading = false;
      this.loadingMostSoldProducts = false;
      this.render();
    }
  }

  private async loadAnalytics(): Promise<void> {
    await Promise.all([
      this.loadCharts(),
      this.loadMostSoldProducts(),
    ]);
  }

  private async loadCharts(): Promise<void> {
    if (!managerState.filters.store) {
      this.loading = false;
      return;
    }

    this.loading = true;
    this.error = '';
    this.render();

    try {
      this.chartData = await this.api.charts(managerState.filters);
    } catch (error) {
      this.error = getErrorMessage(error, 'Error on chart data.');
    } finally {
      this.loading = false;
      this.render();
      this.renderCharts();
    }
  }

  private async loadMostSoldProducts(): Promise<void> {
    if (!managerState.filters.store) {
      this.loadingMostSoldProducts = false;
      return;
    }

    this.loadingMostSoldProducts = true;
    this.render();

    try {
      this.mostSoldProducts = await this.api.mostSoldProducts(managerState.filters, this.pageMostSoldProducts);
      this.mostSoldProducts.products = this.mostSoldProducts.products || [];
      this.pageMostSoldProducts = this.mostSoldProducts.page || this.pageMostSoldProducts;
    } catch (error) {
      this.error = getErrorMessage(error, 'Error on most sold products data.');
    } finally {
      this.loadingMostSoldProducts = false;
      this.render();
      this.renderCharts();
    }
  }

  private render(): void {
    this.innerHTML = `
      <style>${managerStyles}</style>
      <section class="ekmManager">
        <div class="ekmManager__body">
          ${this.renderToolbar()}
          ${this.error ? `<p class="status status--error">${escapeHtml(this.error)}</p>` : ''}
          ${this.renderAnalytics()}
        </div>
      </section>
    `;

    this.bindEvents();
    this.renderCharts();
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
        </div>
      </div>
    `;
  }

  private renderAnalytics(): string {
    return `
      <div class="ekmGrid">
        <div class="card ekmChartCard"><h3>Sales Revenue</h3><div class="ekmChartCard__canvas">${this.loading ? '<p>Loading chart...</p>' : '<canvas id="chartRevenue"></canvas>'}</div></div>
        <div class="card ekmChartCard"><h3>Total Orders</h3><div class="ekmChartCard__canvas">${this.loading ? '<p>Loading chart...</p>' : '<canvas id="chartOrders"></canvas>'}</div></div>
        <div class="card ekmChartCard"><h3>Average Order Value</h3><div class="ekmChartCard__canvas">${this.loading ? '<p>Loading chart...</p>' : '<canvas id="chartAvarage"></canvas>'}</div></div>
      </div>
      <div class="card ekmChartCard">
        <h3>Most Sold Products</h3>
        ${this.loadingMostSoldProducts ? '<p>Loading most sold products...</p>' : this.renderMostSoldProducts()}
      </div>
    `;
  }

  private renderMostSoldProducts(): string {
    if (!this.mostSoldProducts.products.length) {
      return '<p>No products found.</p>';
    }

    return `
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell">Product</div><div class="umb-table-cell">Sku</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Total</div></div></div>
        <div class="umb-table-body">
          ${this.mostSoldProducts.products.map(product => `<div class="umb-table-row"><div class="umb-table-cell">${escapeHtml(readProductValue(product, 'title', 'productTitle', 'name'))}</div><div class="umb-table-cell">${escapeHtml(readProductValue(product, 'sku', 'productSku'))}</div><div class="umb-table-cell">${escapeHtml(readProductValue(product, 'quantity', 'count'))}</div><div class="umb-table-cell">${escapeHtml(readProductValue(product, 'formattedTotal', 'total'))}</div></div>`).join('')}
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
    if (!this.chartData) {
      return;
    }

    const revenue = this.querySelector<HTMLCanvasElement>('#chartRevenue');
    const orders = this.querySelector<HTMLCanvasElement>('#chartOrders');
    const average = this.querySelector<HTMLCanvasElement>('#chartAvarage');

    if (revenue) {
      renderLineChart(revenue, this.chartData.revenueChart, 'rgba(30, 64, 175, 1)');
    }

    if (orders) {
      renderLineChart(orders, this.chartData.ordersChart, 'rgba(8, 145, 178, 1)');
    }

    if (average) {
      renderLineChart(average, this.chartData.avarageChart, 'rgba(217, 119, 6, 1)');
    }
  }

  private bindEvents(): void {
    this.querySelectorAll('[data-action]').forEach(element => {
      element.addEventListener('click', event => void this.handleAction(event));
    });

    this.querySelectorAll('[data-field]').forEach(element => {
      element.addEventListener('change', event => void this.handleFieldChange(event));
    });
  }

  private async handleAction(event: Event): Promise<void> {
    const target = event.currentTarget as HTMLElement;

    if (target.dataset.action === 'set-most-sold-page') {
      this.pageMostSoldProducts = Number(target.dataset.page || 1);
      await this.loadMostSoldProducts();
    }
  }

  private async handleFieldChange(event: Event): Promise<void> {
    const target = event.currentTarget as HTMLInputElement | HTMLSelectElement;
    const field = target.dataset.field;

    if (!field || !(field in managerState.filters)) {
      return;
    }

    (managerState.filters as Record<string, string>)[field] = target.value;
    this.pageMostSoldProducts = 1;
    await this.loadAnalytics();
  }
}

function readProductValue(product: Record<string, unknown>, ...keys: string[]): unknown {
  for (const key of keys) {
    if (product[key] != null) {
      return product[key];
    }
  }

  return '';
}

function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof Error ? error.message : fallback;
}

customElements.define('ekom-analytics-section-view', EkomAnalyticsSectionViewElement);

export default EkomAnalyticsSectionViewElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-analytics-section-view': EkomAnalyticsSectionViewElement;
  }
}
