var u = Object.defineProperty;
var h = (s, r, t) => r in s ? u(s, r, { enumerable: !0, configurable: !0, writable: !0, value: t }) : s[r] = t;
var d = (s, r, t) => h(s, typeof r != "symbol" ? r + "" : r, t);
import { UmbElementMixin as v } from "@umbraco-cms/backoffice/element-api";
import { E as m, m as e, a as g, e as o, g as p, p as b, r as n } from "./manager-shared.js";
class f extends v(HTMLElement) {
  constructor() {
    super(...arguments);
    d(this, "api", new m());
    d(this, "loading", !0);
    d(this, "loadingMostSoldProducts", !0);
    d(this, "error", "");
    d(this, "chartData");
    d(this, "mostSoldProducts", { products: [], count: 0, totalPages: 0, page: 1 });
    d(this, "pageMostSoldProducts", 1);
  }
  connectedCallback() {
    super.connectedCallback(), this.render(), this.initialize();
  }
  async initialize() {
    try {
      const [t, a] = await Promise.all([
        this.api.statusList(),
        this.api.stores()
      ]);
      e.statusList = t || [], e.stores = a || [], !e.filters.store && e.stores.length && (e.filters.store = e.stores[0].alias), await this.loadAnalytics();
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
    if (!e.filters.store) {
      this.loading = !1;
      return;
    }
    this.loading = !0, this.error = "", this.render();
    try {
      this.chartData = await this.api.charts(e.filters);
    } catch (t) {
      this.error = c(t, "Error on chart data.");
    } finally {
      this.loading = !1, this.render(), this.renderCharts();
    }
  }
  async loadMostSoldProducts() {
    if (!e.filters.store) {
      this.loadingMostSoldProducts = !1;
      return;
    }
    this.loadingMostSoldProducts = !0, this.render();
    try {
      this.mostSoldProducts = await this.api.mostSoldProducts(e.filters, this.pageMostSoldProducts), this.mostSoldProducts.products = this.mostSoldProducts.products || [], this.pageMostSoldProducts = this.mostSoldProducts.page || this.pageMostSoldProducts;
    } catch (t) {
      this.error = c(t, "Error on most sold products data.");
    } finally {
      this.loadingMostSoldProducts = !1, this.render(), this.renderCharts();
    }
  }
  render() {
    this.innerHTML = `
      <style>${g}</style>
      <section class="ekmManager">
        <div class="ekmManager__body">
          ${this.renderToolbar()}
          ${this.error ? `<p class="status status--error">${o(this.error)}</p>` : ""}
          ${this.renderAnalytics()}
        </div>
      </section>
    `, this.bindEvents(), this.renderCharts();
  }
  renderToolbar() {
    const t = e.filters;
    return `
      <div class="umb-sub-header">
        <div class="ekmManager__filters">
          <label class="ekmManager__filter">Order Status:
            <select data-field="orderStatus">
              <option value="CompletedOrders" ${t.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option>
              <option value="AllOrders" ${t.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>
              ${e.statusList.map((a) => {
      const i = p(a);
      return `<option value="${o(i)}" ${t.orderStatus === i ? "selected" : ""}>${o(a.label)}</option>`;
    }).join("")}
            </select>
          </label>
          <label class="ekmManager__filter">Date From:
            <input type="date" data-field="dateFrom" value="${o(t.dateFrom)}">
          </label>
          <label class="ekmManager__filter">Date To:
            <input type="date" data-field="dateTo" value="${o(t.dateTo)}">
          </label>
          <label class="ekmManager__filter">Store:
            <select data-field="store">
              ${e.stores.map((a) => `<option value="${o(a.alias)}" ${t.store === a.alias ? "selected" : ""}>${o(a.title)}</option>`).join("")}
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
        ${this.loadingMostSoldProducts ? "<p>Loading most sold products...</p>" : this.renderMostSoldProducts()}
      </div>
    `;
  }
  renderMostSoldProducts() {
    return this.mostSoldProducts.products.length ? `
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell">Product</div><div class="umb-table-cell">Sku</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Total</div></div></div>
        <div class="umb-table-body">
          ${this.mostSoldProducts.products.map((t) => `<div class="umb-table-row"><div class="umb-table-cell">${o(l(t, "title", "productTitle", "name"))}</div><div class="umb-table-cell">${o(l(t, "sku", "productSku"))}</div><div class="umb-table-cell">${o(l(t, "quantity", "count"))}</div><div class="umb-table-cell">${o(l(t, "formattedTotal", "total"))}</div></div>`).join("")}
        </div>
      </div>
      ${this.mostSoldProducts.totalPages > 1 ? this.renderMostSoldProductsPagination() : ""}
    ` : "<p>No products found.</p>";
  }
  renderMostSoldProductsPagination() {
    return `
      <div class="pagination">
        <ul>
          ${b(this.pageMostSoldProducts, this.mostSoldProducts.totalPages).map((t) => {
      const a = Number(String(t).replace("...", ""));
      return `<li class="${a === this.pageMostSoldProducts ? "active" : ""}"><button type="button" data-action="set-most-sold-page" data-page="${a}" ${a === this.pageMostSoldProducts ? "disabled" : ""}>${o(t)}</button></li>`;
    }).join("")}
        </ul>
      </div>
    `;
  }
  renderCharts() {
    if (!this.chartData)
      return;
    const t = this.querySelector("#chartRevenue"), a = this.querySelector("#chartOrders"), i = this.querySelector("#chartAvarage");
    t && n(t, this.chartData.revenueChart, "rgba(30, 64, 175, 1)"), a && n(a, this.chartData.ordersChart, "rgba(8, 145, 178, 1)"), i && n(i, this.chartData.avarageChart, "rgba(217, 119, 6, 1)");
  }
  bindEvents() {
    this.querySelectorAll("[data-action]").forEach((t) => {
      t.addEventListener("click", (a) => void this.handleAction(a));
    }), this.querySelectorAll("[data-field]").forEach((t) => {
      t.addEventListener("change", (a) => void this.handleFieldChange(a));
    });
  }
  async handleAction(t) {
    const a = t.currentTarget;
    a.dataset.action === "set-most-sold-page" && (this.pageMostSoldProducts = Number(a.dataset.page || 1), await this.loadMostSoldProducts());
  }
  async handleFieldChange(t) {
    const a = t.currentTarget, i = a.dataset.field;
    !i || !(i in e.filters) || (e.filters[i] = a.value, this.pageMostSoldProducts = 1, await this.loadAnalytics());
  }
}
function l(s, ...r) {
  for (const t of r)
    if (s[t] != null)
      return s[t];
  return "";
}
function c(s, r) {
  return s instanceof Error ? s.message : r;
}
customElements.define("ekom-analytics-section-view", f);
export {
  f as EkomAnalyticsSectionViewElement,
  f as default
};
