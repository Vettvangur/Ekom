var M = Object.defineProperty;
var E = (d, i, t) => i in d ? M(d, i, { enumerable: !0, configurable: !0, writable: !0, value: t }) : d[i] = t;
var h = (d, i, t) => E(d, typeof i != "symbol" ? i + "" : i, t);
import { UmbElementMixin as O } from "@umbraco-cms/backoffice/element-api";
import { E as q, m, f as D, a as F, e as c, g as A, p as L, r as R, d as U } from "./manager-shared.js";
const k = 864e5, S = (d) => d.toISOString().slice(0, 10), x = (d) => /* @__PURE__ */ new Date(`${d}T00:00:00Z`);
function N(d, i) {
  const t = x(d), e = x(i), a = Math.round((e.getTime() - t.getTime()) / k) + 1;
  return { dateFrom: S(new Date(t.getTime() - a * k)), dateTo: S(new Date(t.getTime() - k)) };
}
function V(d, i = /* @__PURE__ */ new Date()) {
  const t = new Date(Date.UTC(i.getFullYear(), i.getMonth(), i.getDate())), e = new Date(t);
  return d === "7" && e.setUTCDate(e.getUTCDate() - 6), d === "30" && e.setUTCDate(e.getUTCDate() - 29), d === "month" && e.setUTCDate(1), d === "year" && (e.setUTCMonth(0), e.setUTCDate(1)), { dateFrom: S(e), dateTo: S(t) };
}
function j(d, i, t) {
  var n, o, s, g, l;
  const e = [], a = d.ordersChart.points;
  let r = 0;
  for (; r < a.length; ) {
    const b = r, p = a[r].x, u = x(p);
    t === "month" ? (u.setUTCDate(1), u.setUTCMonth(u.getUTCMonth() + 1)) : t === "week" ? u.setUTCDate(u.getUTCDate() + (7 - (u.getUTCDay() + 6) % 7)) : u.setUTCDate(u.getUTCDate() + 1);
    let y = 0, v = 0, C = 0, f = 0;
    do
      y += Number(((n = d.revenueChart.points[r]) == null ? void 0 : n.y) || 0), v += Number(a[r].y || 0), C += Number(((o = i == null ? void 0 : i.revenueChart.points[r]) == null ? void 0 : o.y) || 0), f += Number(((s = i == null ? void 0 : i.ordersChart.points[r]) == null ? void 0 : s.y) || 0), r++;
    while (r < a.length && x(a[r].x).getTime() < u.getTime());
    e.push({
      start: p,
      end: a[r - 1].x,
      amount: y,
      orders: v,
      average: v ? y / v : null,
      ...i ? {
        previousStart: (g = i.ordersChart.points[b]) == null ? void 0 : g.x,
        previousEnd: (l = i.ordersChart.points[r - 1]) == null ? void 0 : l.x,
        previousAmount: C,
        previousOrders: f,
        previousAverage: f ? C / f : null
      } : {}
    });
  }
  return e;
}
function P(d) {
  const i = d.revenueChart.points.reduce((e, a) => e + Number(a.y || 0), 0), t = d.ordersChart.points.reduce((e, a) => e + Number(a.y || 0), 0);
  return { amount: i, orders: t, average: t ? i / t : null };
}
function T(d, i) {
  if (d == null || i == null) return "No comparison available";
  if (i === 0) return d === 0 ? "No change" : "No prior value";
  const t = (d - i) / Math.abs(i) * 100;
  return `${t > 0 ? "+" : ""}${t.toLocaleString(void 0, { maximumFractionDigits: 1 })}%`;
}
class z extends O(HTMLElement) {
  constructor() {
    super(...arguments);
    h(this, "api", new q());
    h(this, "loading", !0);
    h(this, "loadingMostSoldProducts", !1);
    h(this, "error", "");
    h(this, "productError", "");
    h(this, "chartData");
    h(this, "previousData");
    h(this, "chartFilters");
    h(this, "mostSoldProducts", { products: [], count: 0, totalPages: 0, page: 1 });
    h(this, "pageMostSoldProducts", 1);
    h(this, "resizeObserver");
    h(this, "chartRequest", 0);
    h(this, "productRequest", 0);
    h(this, "chartAbort");
    h(this, "productAbort");
    h(this, "disposed", !1);
    h(this, "productsVisible", !1);
    h(this, "metric", "amount");
    h(this, "grouping", "day");
    h(this, "compare", !0);
    h(this, "table", !1);
  }
  connectedCallback() {
    if (super.connectedCallback(), this.disposed = !1, this.hasAttribute("classic"))
      for (const t of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const e = this.getAttribute(t.toLowerCase());
        e && (m.filters[t] = e);
      }
    this.render(), this.resizeObserver = new ResizeObserver(() => this.renderCharts()), this.resizeObserver.observe(this), this.initialize();
  }
  disconnectedCallback() {
    var t, e, a;
    this.disposed = !0, this.chartRequest++, this.productRequest++, (t = this.chartAbort) == null || t.abort(), (e = this.productAbort) == null || e.abort(), (a = this.resizeObserver) == null || a.disconnect(), this.resizeObserver = void 0, super.disconnectedCallback();
  }
  get currency() {
    var t;
    return (t = m.stores.find((e) => {
      var a;
      return e.alias === (((a = this.chartFilters) == null ? void 0 : a.store) || m.filters.store);
    })) == null ? void 0 : t.currency;
  }
  async initialize() {
    var t;
    try {
      const [e, a] = await Promise.all([this.api.statusList(), this.api.stores()]);
      if (this.disposed) return;
      m.statusList = e || [], m.stores = a || [], m.stores.some((r) => r.alias === m.filters.store) || (m.filters.store = ((t = m.stores[0]) == null ? void 0 : t.alias) || ""), await this.loadAnalytics();
    } catch (e) {
      if (this.disposed) return;
      this.error = w(e, "Unable to load Analytics."), this.loading = !1, this.render();
    }
  }
  async loadAnalytics() {
    await Promise.all([this.loadCharts(), this.productsVisible ? this.loadMostSoldProducts() : Promise.resolve()]);
  }
  async loadCharts() {
    var r;
    const t = ++this.chartRequest;
    (r = this.chartAbort) == null || r.abort();
    const e = this.chartAbort = new AbortController(), a = { ...m.filters };
    if (this.chartData = void 0, this.previousData = void 0, this.error = "", this.loading = !!a.store, this.render(), !!a.store)
      try {
        const n = this.compare ? { ...a, ...N(a.dateFrom, a.dateTo) } : void 0, [o, s] = await Promise.all([
          this.api.charts(a, e.signal),
          n ? this.api.charts(n, e.signal) : Promise.resolve(void 0)
        ]);
        if (t !== this.chartRequest || this.disposed) return;
        this.chartData = o, this.previousData = s, this.chartFilters = a, o.mixedCurrencies && (this.metric = "orders");
      } catch (n) {
        if (t !== this.chartRequest || this.disposed) return;
        this.error = w(n, "Unable to load charts.");
      } finally {
        t === this.chartRequest && !this.disposed && (this.loading = !1, this.render());
      }
  }
  async loadMostSoldProducts() {
    var r;
    const t = ++this.productRequest;
    (r = this.productAbort) == null || r.abort();
    const e = this.productAbort = new AbortController(), a = { ...m.filters };
    if (this.mostSoldProducts = { products: [], count: 0, totalPages: 0, page: this.pageMostSoldProducts }, this.productError = "", this.loadingMostSoldProducts = !!a.store, this.render(), !!a.store)
      try {
        const n = await this.api.mostSoldProducts(a, this.pageMostSoldProducts, 20, e.signal);
        if (t !== this.productRequest || this.disposed) return;
        this.mostSoldProducts = { ...n, products: n.products || [] }, this.pageMostSoldProducts = n.page || this.pageMostSoldProducts;
      } catch (n) {
        t === this.productRequest && !this.disposed && (this.productError = w(n, "Unable to load products."));
      } finally {
        t === this.productRequest && !this.disposed && (this.loadingMostSoldProducts = !1, this.render());
      }
  }
  get buckets() {
    return this.chartData ? j(this.chartData, this.previousData, this.grouping) : [];
  }
  get comparisonMoneyAvailable() {
    var t, e;
    return !!this.previousData && !((t = this.chartData) != null && t.mixedCurrencies) && !this.previousData.mixedCurrencies && (!((e = this.chartData) != null && e.currency) || !this.previousData.currency || this.chartData.currency === this.previousData.currency);
  }
  amount(t) {
    var a, r, n, o, s;
    if (t == null || (a = this.chartData) != null && a.mixedCurrencies) return "—";
    const e = ((r = this.chartData) == null ? void 0 : r.currency) || ((n = this.previousData) == null ? void 0 : n.currency);
    return !e || e === ((o = this.currency) == null ? void 0 : o.currencyValue) || e === ((s = this.currency) == null ? void 0 : s.isoCurrencySymbol) ? D(t, this.currency) : /^[A-Z]{3}$/.test(e) ? D(t, { isoCurrencySymbol: e }) : `${t.toLocaleString(void 0, { maximumFractionDigits: 2 })} ${e}`;
  }
  label(t) {
    return t === "orders" ? "Orders" : t === "average" ? "Average order value" : "Order amount";
  }
  value(t, e = this.metric) {
    return e === "orders" ? t == null ? "—" : t.toLocaleString() : this.amount(t);
  }
  previousValue(t) {
    return this.metric === "orders" ? t.previousOrders : this.metric === "average" ? t.previousAverage : t.previousAmount;
  }
  range(t, e) {
    return t === e ? t || "" : `${t || ""} – ${e || ""}`;
  }
  render() {
    const t = m.filters;
    this.innerHTML = `<style>${F}
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
      .analytics-insight-grid {display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:18px}
      .analytics-time-bars {display:grid;gap:8px;margin-top:16px}
      .analytics-hour-bars {grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:10px 18px}
      .analytics-time-row {display:grid;grid-template-columns:64px 1fr 32px;align-items:center;gap:8px;font-size:12px}
      .analytics-time-track {height:8px;background:#edf2f0;border-radius:4px;overflow:hidden}
      .analytics-time-track span {display:block;height:100%;background:#25765c;border-radius:4px}
      .analytics-spinner {display:inline-block;width:14px;height:14px;margin-right:8px;border:2px solid #dfe2e8;border-top-color:#25765c;border-radius:50%;animation:analytics-spin 1s linear infinite}
      @keyframes analytics-spin {to {transform:rotate(360deg)}}
      @media (prefers-reduced-motion:reduce) {.analytics-spinner {animation:none}}
    </style><section class="ekmManager analytics"><div class="ekmManager__body">
      <div class="analytics-toolbar">
        <label>Store<select data-field="store">${m.stores.map((e) => `<option value="${c(e.alias)}" ${e.alias === t.store ? "selected" : ""}>${c(e.title)}</option>`).join("")}</select></label>
        <label>Order status<select data-field="orderStatus"><option value="CompletedOrders" ${t.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option><option value="AllOrders" ${t.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>${m.statusList.map((e) => `<option value="${c(A(e))}" ${A(e) === t.orderStatus ? "selected" : ""}>${c(e.label)}</option>`).join("")}</select></label>
        <label>Date From<input type="date" data-field="dateFrom" value="${c(t.dateFrom)}"></label>
        <label>Date To<input type="date" data-field="dateTo" value="${c(t.dateTo)}"></label>
        <label>Date preset<select data-preset><option value="">Custom range</option>${[["today", "Today"], ["7", "Last 7 days"], ["30", "Last 30 days"], ["month", "This month"], ["year", "This year"]].map(([e, a]) => `<option value="${e}">${a}</option>`).join("")}</select></label>
        <label><span>Comparison</span><span><input type="checkbox" data-compare ${this.compare ? "checked" : ""}> Previous period</span></label>
        <button data-refresh>Refresh</button>
      </div>
      ${this.error ? `<p role="alert">${c(this.error)}</p>` : ""}
      ${this.loading ? '<p role="status">Loading analytics…</p>' : t.store ? "" : "<p>No accessible stores.</p>"}
      ${this.chartData ? this.renderAnalytics() + this.renderCustomerInsights() : ""}
      <div class="card analytics-main"><div class="analytics-toolbar"><h3>Most Sold Products</h3><button data-products>${this.productsVisible ? "Hide products" : "Load product breakdown"}</button></div>
        <p class="analytics-muted">Fetch the most sold products for the selected store and dates. This may take a minute for larger date ranges. Totals include VAT and discounts, excluding shipping and payment fees.</p>
        ${this.productsVisible ? `${this.productError ? `<p role="alert">${c(this.productError)}</p><button data-product-retry>Retry products</button>` : ""}${this.loadingMostSoldProducts ? '<p role="status"><span class="analytics-spinner" aria-hidden="true"></span>Fetching product breakdown… This may take a minute. You can continue using Analytics.</p>' : this.renderMostSoldProducts()}` : ""}
      </div>
    </div></section>`, this.bindEvents(), this.renderCharts();
  }
  renderAnalytics() {
    var a, r, n, o;
    const t = P(this.chartData), e = this.previousData ? P(this.previousData) : void 0;
    return `<div class="analytics-summary">${["amount", "orders", "average"].map((s) => `<div class="card"><span>${this.label(s)}</span><strong>${c(this.value(t[s], s))}</strong><span class="analytics-muted">${e && (s === "orders" || this.comparisonMoneyAvailable) ? c(T(t[s], e[s])) + " vs previous period" : this.compare ? "No monetary comparison available" : "Selected period"}</span></div>`).join("")}</div>
      <p class="analytics-muted">Order amount is the recorded charged amount, after gift cards; it is not refund-adjusted revenue. Dates use ${this.chartData.dateBasis === "PaidDate" ? "payment date" : "order creation date"}, matching Orders.</p>
      ${this.chartData.mixedCurrencies ? '<p role="status">This period contains multiple currencies. Monetary totals are hidden to avoid combining different currencies.</p>' : ""}
      ${this.previousData && !this.comparisonMoneyAvailable ? '<p class="analytics-muted">Monetary comparison is unavailable across different or mixed currencies.</p>' : ""}
      <div class="card analytics-main"><div class="analytics-toolbar">
        ${["amount", "orders", "average"].map((s) => `<button data-metric="${s}" aria-pressed="${this.metric === s}" ${s !== "orders" && this.chartData.mixedCurrencies ? "disabled" : ""}>${this.label(s)}</button>`).join("")}
        <label>Group by<select data-grouping>${[["day", "Daily"], ["week", "Weekly"], ["month", "Monthly"]].map(([s, g]) => `<option value="${s}" ${this.grouping === s ? "selected" : ""}>${g}</option>`).join("")}</select></label>
        <button data-table aria-pressed="${this.table}">${this.table ? "Show chart" : "Show data table"}</button><button data-csv>Download CSV</button>
      </div>
      <p class="analytics-muted">${this.previousData ? `Solid: ${c(this.range((a = this.chartFilters) == null ? void 0 : a.dateFrom, (r = this.chartFilters) == null ? void 0 : r.dateTo))}. Dashed: ${c(this.range((n = this.previousData.ordersChart.points[0]) == null ? void 0 : n.x, (o = this.previousData.ordersChart.points.at(-1)) == null ? void 0 : o.x))}. Comparison buckets cover the same number of days.` : "Selected period."} Click a bucket to open its orders.</p>
      ${this.table ? this.renderTable() : `<div style="height:360px"><canvas id="analytics-chart" style="width:100%;height:100%" role="img" aria-label="${this.label(this.metric)} over time. Use Show data table for exact values and order links."></canvas></div>`}
      </div>`;
  }
  renderCustomerInsights() {
    var g, l;
    const t = (g = this.chartData) == null ? void 0 : g.customerInsights;
    if (!t) return "";
    const e = (l = this.previousData) == null ? void 0 : l.customerInsights, a = [
      ["Unique purchasing customers", t.uniqueCustomers, e == null ? void 0 : e.uniqueCustomers, !1],
      ["First-time buyers", t.newCustomers, e == null ? void 0 : e.newCustomers, !1],
      ["Returning customer rate", t.returningCustomerRate, e == null ? void 0 : e.returningCustomerRate, !0]
    ], r = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"], n = r.map((b, p) => t.orderTimes.filter((u) => u.weekday === p).reduce((u, y) => u + y.orders, 0)), o = Array.from({ length: 24 }, (b, p) => t.orderTimes.filter((u) => u.hour === p).reduce((u, y) => u + y.orders, 0)), s = (b, p) => b.map((u, y) => `<div class="analytics-time-row"><span>${u}</span><div class="analytics-time-track" aria-hidden="true"><span style="width:${100 * p[y] / Math.max(1, ...p)}%"></span></div><span>${p[y]}</span></div>`).join("");
    return `<div class="analytics-summary">${a.map(([b, p, u, y]) => `<div class="card"><span>${b}</span><strong>${p == null ? "—" : c(p.toLocaleString(void 0, { maximumFractionDigits: y ? 1 : 0 })) + (y ? "%" : "")}</strong><span class="analytics-muted">${e ? y ? p == null || u == null ? "No comparison available" : `${(p - u).toLocaleString(void 0, { maximumFractionDigits: 1, signDisplay: "always" })} percentage points vs previous period` : `${T(p, u ?? null)} vs previous period` : "Selected period"}</span></div>`).join("")}</div>
      <div class="card analytics-main"><h3>New and returning purchases</h3>
        <p class="analytics-muted">Based on qualifying purchase orders and ${t.identityField === "CustomerUsername" ? "username" : "email"}. A repeat purchase has an earlier qualifying order in this store, ordered by payment time (creation time when unpaid). A buyer can appear in both rows if they buy for the first time and return in this period. Returning rate is repeat buyers divided by unique identified buyers.</p>
        <div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Purchase type</th><th>Buyers</th><th>Orders</th><th>Order amount</th></tr></thead><tbody>
          <tr><td>First purchase</td><td>${t.newCustomers}</td><td>${t.firstOrders}</td><td>${c(this.amount(t.firstOrderAmount))}</td></tr>
          <tr><td>Repeat purchase</td><td>${t.returningCustomers}</td><td>${t.repeatOrders}</td><td>${c(this.amount(t.repeatOrderAmount))}</td></tr>
        </tbody></table></div>
        <p class="analytics-muted">${t.missingIdentifiers} qualifying orders have no customer identifier and are excluded from buyer counts and this breakdown. They remain included in the time charts below. Incomplete and cancelled orders are excluded from these customer insights, even with All Orders selected.</p>
      </div>
      <div class="analytics-insight-grid">
        <div class="card analytics-main"><h3>Orders by weekday</h3><p class="analytics-muted">Qualifying orders within the selected filters, grouped by recorded creation day.</p><div class="analytics-time-bars">${s(r, n)}</div></div>
        <div class="card analytics-main"><h3>Orders by hour</h3><p class="analytics-muted">Recorded creation time, 00–23. No timezone conversion. Totals are counts, not averages per hour.</p><div class="analytics-time-bars analytics-hour-bars">${s(o.map((b, p) => String(p).padStart(2, "0") + ":00"), o)}</div></div>
      </div>`;
  }
  renderTable() {
    return `<div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Period</th><th>Orders</th><th>Order amount</th><th>Average order</th>${this.previousData ? `<th>Previous period</th><th>Previous ${this.label(this.metric).toLowerCase()}</th>` : ""}<th>Details</th></tr></thead><tbody>${this.buckets.map((t, e) => `<tr><td>${c(this.range(t.start, t.end))}</td><td>${t.orders}</td><td>${c(this.amount(t.amount))}</td><td>${c(this.amount(t.average))}</td>${this.previousData ? `<td>${c(this.range(t.previousStart, t.previousEnd))}</td><td>${c(this.metric === "orders" || this.comparisonMoneyAvailable ? this.value(this.previousValue(t)) : "—")}</td>` : ""}<td><button data-orders="${e}">Open orders</button></td></tr>`).join("")}</tbody></table></div>`;
  }
  renderMostSoldProducts() {
    return this.mostSoldProducts.products.length ? `
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell">Product</div><div class="umb-table-cell">Sku</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Total</div></div></div>
        <div class="umb-table-body">
          ${this.mostSoldProducts.products.map((t) => `<div class="umb-table-row"><div class="umb-table-cell">${c($(t, "title", "productTitle", "name"))}</div><div class="umb-table-cell">${c($(t, "sku", "productSku"))}</div><div class="umb-table-cell">${c($(t, "productCount", "quantity", "count"))}</div><div class="umb-table-cell">${c(D(Number($(t, "total")), this.currency))}</div></div>`).join("")}
        </div>
      </div>
      ${this.mostSoldProducts.totalPages > 1 ? this.renderMostSoldProductsPagination() : ""}
    ` : "<p>No products found.</p>";
  }
  renderMostSoldProductsPagination() {
    return `
      <div class="pagination">
        <ul>
          ${L(this.pageMostSoldProducts, this.mostSoldProducts.totalPages).map((t) => {
      const e = Number(String(t).replace("...", ""));
      return `<li class="${e === this.pageMostSoldProducts ? "active" : ""}"><button type="button" data-action="set-most-sold-page" data-page="${e}" ${e === this.pageMostSoldProducts ? "disabled" : ""}>${c(t)}</button></li>`;
    }).join("")}
        </ul>
      </div>
    `;
  }
  renderCharts() {
    var o;
    const t = this.querySelector("#analytics-chart");
    if (!t || !this.chartData || this.table) return;
    const e = this.buckets, a = this.chartData.currency || ((o = this.previousData) == null ? void 0 : o.currency), r = a && /^[A-Z]{3}$/.test(a) ? { isoCurrencySymbol: a } : this.currency, n = this.previousData && (this.metric === "orders" || this.comparisonMoneyAvailable) ? { labels: [], points: e.map((s) => ({ x: this.range(s.previousStart, s.previousEnd), y: this.previousValue(s) })) } : void 0;
    R(t, { labels: [], points: e.map((s) => ({ x: this.range(s.start, s.end), y: s[this.metric] })) }, "#25765c", {
      currency: this.metric === "orders" ? void 0 : r,
      integer: this.metric === "orders",
      comparison: n,
      formatValue: (s) => this.value(s),
      columnHover: !0,
      onSelect: (s) => this.openOrders(s)
    });
  }
  changedFilters() {
    this.pageMostSoldProducts = 1, this.dispatchEvent(new CustomEvent("analytics-filters-changed", { bubbles: !0, detail: { ...m.filters } })), this.loadAnalytics();
  }
  openOrders(t) {
    const e = this.buckets[t];
    if (!e || !this.chartFilters) return;
    const a = { ...this.chartFilters, dateFrom: e.start, dateTo: e.end };
    for (const r of ["query", "paymentProvider", "shippingProvider", "couponCode", "giftcardCode", "productSku", "trackingSource", "trackingMedium", "trackingCampaign", "trackingTerm", "trackingContent", "trackingClickId"]) a[r] = "";
    if (Object.assign(m.filters, a), this.hasAttribute("classic")) this.dispatchEvent(new CustomEvent("analytics-open-orders", { bubbles: !0, detail: a }));
    else {
      const r = new URLSearchParams({ analyticsDrill: "1", store: a.store, start: a.dateFrom, end: a.dateTo, orderStatus: a.orderStatus }), n = document.createElement("a");
      n.href = location.pathname.replace(/\/analytics\/?$/, "/orders") + "?" + r, this.appendChild(n), n.click(), n.remove();
    }
  }
  exportCsv() {
    var a, r, n;
    const t = [["From", "To", "Orders", "Order amount", "Average order", "Currency", "Previous from", "Previous to", "Previous orders", "Previous amount", "Previous average"]];
    for (const o of this.buckets) t.push([o.start, o.end, String(o.orders), (a = this.chartData) != null && a.mixedCurrencies ? "" : String(o.amount), (r = this.chartData) != null && r.mixedCurrencies || o.average == null ? "" : String(o.average), ((n = this.chartData) == null ? void 0 : n.currency) || "", o.previousStart || "", o.previousEnd || "", o.previousOrders == null ? "" : String(o.previousOrders), this.comparisonMoneyAvailable ? String(o.previousAmount ?? "") : "", this.comparisonMoneyAvailable ? String(o.previousAverage ?? "") : ""]);
    const e = t.map((o) => o.map((s) => '"' + s.replaceAll('"', '""') + '"').join(",")).join(`\r
`);
    U(new Blob(["\uFEFF", e], { type: "text/csv;charset=utf-8" }), "analytics.csv");
  }
  bindEvents() {
    var t, e, a, r, n, o, s, g;
    this.querySelectorAll("[data-field]").forEach((l) => l.addEventListener("change", () => {
      const b = l.dataset.field;
      m.filters[b] = l.value, this.changedFilters();
    })), (t = this.querySelector("[data-preset]")) == null || t.addEventListener("change", (l) => {
      const b = l.target.value;
      b && (Object.assign(m.filters, V(b)), this.changedFilters());
    }), (e = this.querySelector("[data-compare]")) == null || e.addEventListener("change", () => {
      this.compare = !this.compare, this.loadCharts();
    }), (a = this.querySelector("[data-grouping]")) == null || a.addEventListener("change", (l) => {
      this.grouping = l.target.value, this.render();
    }), this.querySelectorAll("[data-metric]").forEach((l) => l.addEventListener("click", () => {
      this.metric = l.dataset.metric, this.render();
    })), (r = this.querySelector("[data-table]")) == null || r.addEventListener("click", () => {
      this.table = !this.table, this.render();
    }), (n = this.querySelector("[data-csv]")) == null || n.addEventListener("click", () => this.exportCsv()), (o = this.querySelector("[data-refresh]")) == null || o.addEventListener("click", () => void this.loadAnalytics()), this.querySelectorAll("[data-orders]").forEach((l) => l.addEventListener("click", () => this.openOrders(Number(l.dataset.orders)))), (s = this.querySelector("[data-products]")) == null || s.addEventListener("click", () => {
      var l;
      this.productsVisible = !this.productsVisible, this.productsVisible ? this.loadMostSoldProducts() : (this.productRequest++, (l = this.productAbort) == null || l.abort(), this.render());
    }), (g = this.querySelector("[data-product-retry]")) == null || g.addEventListener("click", () => void this.loadMostSoldProducts()), this.querySelectorAll('[data-action="set-most-sold-page"]').forEach((l) => l.addEventListener("click", () => {
      this.pageMostSoldProducts = Number(l.dataset.page), this.loadMostSoldProducts();
    }));
  }
}
function $(d, ...i) {
  for (const t of i) if (d[t] != null) return d[t];
  return "";
}
function w(d, i) {
  return d instanceof Error ? d.message : i;
}
customElements.define("ekom-analytics-section-view", z);
export {
  z as EkomAnalyticsSectionViewElement,
  z as default
};
