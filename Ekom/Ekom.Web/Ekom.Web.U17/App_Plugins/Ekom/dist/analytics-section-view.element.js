var u = Object.defineProperty;
var h = (a, r, t) => r in a ? u(a, r, { enumerable: !0, configurable: !0, writable: !0, value: t }) : a[r] = t;
var d = (a, r, t) => h(a, typeof r != "symbol" ? r + "" : r, t);
import { UmbElementMixin as v } from "@umbraco-cms/backoffice/element-api";
import { E as m, m as s, a as g, e as i, g as p, f as b, p as f, r as n } from "./manager-shared.js";
class S extends v(HTMLElement) {
  constructor() {
    super(...arguments);
    d(this, "api", new m());
    d(this, "loading", !0);
    d(this, "loadingMostSoldProducts", !0);
    d(this, "error", "");
    d(this, "chartData");
    d(this, "mostSoldProducts", { products: [], count: 0, totalPages: 0, page: 1 });
    d(this, "pageMostSoldProducts", 1);
    d(this, "resizeObserver");
  }
  connectedCallback() {
    super.connectedCallback(), this.render(), this.resizeObserver = new ResizeObserver(() => this.renderCharts()), this.resizeObserver.observe(this), this.initialize();
  }
  disconnectedCallback() {
    var t;
    (t = this.resizeObserver) == null || t.disconnect(), this.resizeObserver = void 0, super.disconnectedCallback();
  }
  get currency() {
    var t;
    return (t = s.stores.find((e) => e.alias === s.filters.store)) == null ? void 0 : t.currency;
  }
  async initialize() {
    try {
      const [t, e] = await Promise.all([
        this.api.statusList(),
        this.api.stores()
      ]);
      s.statusList = t || [], s.stores = e || [], !s.filters.store && s.stores.length && (s.filters.store = s.stores[0].alias), await this.loadAnalytics();
    } catch (t) {
      this.error = c(t, "Error loading Ekom analytics."), this.loading = !1, this.loadingMostSoldProducts = !1, this.render();
    }
  }
  async loadAnalytics() {
    await Promise.all([
      this.loadCharts(),
      this.loadMostSoldProducts()
    ]);
  }
  async loadCharts() {
    if (!s.filters.store) {
      this.loading = !1;
      return;
    }
    this.loading = !0, this.error = "", this.render();
    try {
      this.chartData = await this.api.charts(s.filters);
    } catch (t) {
      this.error = c(t, "Error on chart data.");
    } finally {
      this.loading = !1, this.render(), this.renderCharts();
    }
  }
  async loadMostSoldProducts() {
    if (!s.filters.store) {
      this.loadingMostSoldProducts = !1;
      return;
    }
    this.loadingMostSoldProducts = !0, this.render();
    try {
      this.mostSoldProducts = await this.api.mostSoldProducts(s.filters, this.pageMostSoldProducts), this.mostSoldProducts.products = this.mostSoldProducts.products || [], this.pageMostSoldProducts = this.mostSoldProducts.page || this.pageMostSoldProducts;
    } catch (t) {
      this.error = c(t, "Error on most sold products data.");
    } finally {
      this.loadingMostSoldProducts = !1, this.render(), this.renderCharts();
    }
  }
  render() {
    this.innerHTML = `
      <style>${g}
        ekom-analytics-section-view { display: block; }
      </style>
      <section class="ekmManager">
        <div class="ekmManager__body">
          ${this.renderToolbar()}
          ${this.error ? `<p class="status status--error">${i(this.error)}</p>` : ""}
          ${this.renderAnalytics()}
        </div>
      </section>
    `, this.bindEvents(), this.renderCharts();
  }
  renderToolbar() {
    const t = s.filters;
    return `
      <div class="umb-sub-header">
        <div class="ekmManager__filters">
          <label class="ekmManager__filter">Order Status:
            <select data-field="orderStatus">
              <option value="CompletedOrders" ${t.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option>
              <option value="AllOrders" ${t.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>
              ${s.statusList.map((e) => {
      const o = p(e);
      return `<option value="${i(o)}" ${t.orderStatus === o ? "selected" : ""}>${i(e.label)}</option>`;
    }).join("")}
            </select>
          </label>
          <label class="ekmManager__filter">Date From:
            <input type="date" data-field="dateFrom" value="${i(t.dateFrom)}">
          </label>
          <label class="ekmManager__filter">Date To:
            <input type="date" data-field="dateTo" value="${i(t.dateTo)}">
          </label>
          <label class="ekmManager__filter">Store:
            <select data-field="store">
              ${s.stores.map((e) => `<option value="${i(e.alias)}" ${t.store === e.alias ? "selected" : ""}>${i(e.title)}</option>`).join("")}
            </select>
          </label>
        </div>
      </div>
    `;
  }
  renderAnalytics() {
    return `
      <div class="ekmGrid">
        <div class="card ekmChartCard"><h3>Sales Revenue</h3><div class="ekmChartCard__canvas">${this.loading ? "<p>Loading chart...</p>" : '<canvas id="chartRevenue"></canvas>'}</div></div>
        <div class="card ekmChartCard"><h3>Total Orders</h3><div class="ekmChartCard__canvas">${this.loading ? "<p>Loading chart...</p>" : '<canvas id="chartOrders"></canvas>'}</div></div>
        <div class="card ekmChartCard"><h3>Average Order Value</h3><div class="ekmChartCard__canvas">${this.loading ? "<p>Loading chart...</p>" : '<canvas id="chartAvarage"></canvas>'}</div></div>
      </div>
      <div class="card ekmChartCard">
        <h3>Most Sold Products</h3>
        <p>Totals include VAT and discounts, excluding shipping and payment fees.</p>
        ${this.loadingMostSoldProducts ? "<p>Loading most sold products...</p>" : this.renderMostSoldProducts()}
      </div>
    `;
  }
  renderMostSoldProducts() {
    return this.mostSoldProducts.products.length ? `
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell">Product</div><div class="umb-table-cell">Sku</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Total</div></div></div>
        <div class="umb-table-body">
          ${this.mostSoldProducts.products.map((t) => `<div class="umb-table-row"><div class="umb-table-cell">${i(l(t, "title", "productTitle", "name"))}</div><div class="umb-table-cell">${i(l(t, "sku", "productSku"))}</div><div class="umb-table-cell">${i(l(t, "productCount", "quantity", "count"))}</div><div class="umb-table-cell">${i(b(Number(l(t, "total")), this.currency))}</div></div>`).join("")}
        </div>
      </div>
      ${this.mostSoldProducts.totalPages > 1 ? this.renderMostSoldProductsPagination() : ""}
    ` : "<p>No products found.</p>";
  }
  renderMostSoldProductsPagination() {
    return `
      <div class="pagination">
        <ul>
          ${f(this.pageMostSoldProducts, this.mostSoldProducts.totalPages).map((t) => {
      const e = Number(String(t).replace("...", ""));
      return `<li class="${e === this.pageMostSoldProducts ? "active" : ""}"><button type="button" data-action="set-most-sold-page" data-page="${e}" ${e === this.pageMostSoldProducts ? "disabled" : ""}>${i(t)}</button></li>`;
    }).join("")}
        </ul>
      </div>
    `;
  }
  renderCharts() {
    if (!this.chartData)
      return;
    const t = this.querySelector("#chartRevenue"), e = this.querySelector("#chartOrders"), o = this.querySelector("#chartAvarage");
    t && n(t, this.chartData.revenueChart, "rgba(30, 64, 175, 1)", { currency: this.currency }), e && n(e, this.chartData.ordersChart, "rgba(8, 145, 178, 1)", { integer: !0 }), o && n(o, this.chartData.avarageChart, "rgba(217, 119, 6, 1)", { currency: this.currency });
  }
  bindEvents() {
    this.querySelectorAll("[data-action]").forEach((t) => {
      t.addEventListener("click", (e) => void this.handleAction(e));
    }), this.querySelectorAll("[data-field]").forEach((t) => {
      t.addEventListener("change", (e) => void this.handleFieldChange(e));
    });
  }
  async handleAction(t) {
    const e = t.currentTarget;
    e.dataset.action === "set-most-sold-page" && (this.pageMostSoldProducts = Number(e.dataset.page || 1), await this.loadMostSoldProducts());
  }
  async handleFieldChange(t) {
    const e = t.currentTarget, o = e.dataset.field;
    !o || !(o in s.filters) || (s.filters[o] = e.value, this.pageMostSoldProducts = 1, await this.loadAnalytics());
  }
}
function l(a, ...r) {
  for (const t of r)
    if (a[t] != null)
      return a[t];
  return "";
}
function c(a, r) {
  return a instanceof Error ? a.message : r;
}
customElements.define("ekom-analytics-section-view", S);
export {
  S as EkomAnalyticsSectionViewElement,
  S as default
};
