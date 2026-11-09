var E = Object.defineProperty;
var O = (n, i, t) => i in n ? E(n, i, { enumerable: !0, configurable: !0, writable: !0, value: t }) : n[i] = t;
var h = (n, i, t) => O(n, typeof i != "symbol" ? i + "" : i, t);
import { UmbElementMixin as F } from "@umbraco-cms/backoffice/element-api";
import { E as q, m, f as D, a as L, e as c, g as P, p as R, r as U, d as N } from "./manager-shared.js";
const k = 864e5, S = (n) => n.toISOString().slice(0, 10), x = (n) => /* @__PURE__ */ new Date(`${n}T00:00:00Z`);
function V(n, i) {
  const t = x(n), e = x(i), s = Math.round((e.getTime() - t.getTime()) / k) + 1;
  return { dateFrom: S(new Date(t.getTime() - s * k)), dateTo: S(new Date(t.getTime() - k)) };
}
function M(n, i = /* @__PURE__ */ new Date()) {
  const t = new Date(Date.UTC(i.getFullYear(), i.getMonth(), i.getDate())), e = new Date(t);
  return n === "7" && e.setUTCDate(e.getUTCDate() - 6), n === "30" && e.setUTCDate(e.getUTCDate() - 29), n === "month" && e.setUTCDate(1), n === "year" && (e.setUTCMonth(0), e.setUTCDate(1)), { dateFrom: S(e), dateTo: S(t) };
}
function j(n, i, t, e = /* @__PURE__ */ new Date()) {
  if (t === "") return "";
  const s = ["today", "7", "30", "month", "year"];
  return t && s.includes(t) && s.unshift(t), s.find((a) => {
    const o = M(a, e);
    return o.dateFrom === n && o.dateTo === i;
  }) || "";
}
function z(n, i, t) {
  var o, d, r, g, l;
  const e = [], s = n.ordersChart.points;
  let a = 0;
  for (; a < s.length; ) {
    const y = a, p = s[a].x, u = x(p);
    t === "month" ? (u.setUTCDate(1), u.setUTCMonth(u.getUTCMonth() + 1)) : t === "week" ? u.setUTCDate(u.getUTCDate() + (7 - (u.getUTCDay() + 6) % 7)) : u.setUTCDate(u.getUTCDate() + 1);
    let b = 0, v = 0, C = 0, f = 0;
    do
      b += Number(((o = n.revenueChart.points[a]) == null ? void 0 : o.y) || 0), v += Number(s[a].y || 0), C += Number(((d = i == null ? void 0 : i.revenueChart.points[a]) == null ? void 0 : d.y) || 0), f += Number(((r = i == null ? void 0 : i.ordersChart.points[a]) == null ? void 0 : r.y) || 0), a++;
    while (a < s.length && x(s[a].x).getTime() < u.getTime());
    e.push({
      start: p,
      end: s[a - 1].x,
      amount: b,
      orders: v,
      average: v ? b / v : null,
      ...i ? {
        previousStart: (g = i.ordersChart.points[y]) == null ? void 0 : g.x,
        previousEnd: (l = i.ordersChart.points[a - 1]) == null ? void 0 : l.x,
        previousAmount: C,
        previousOrders: f,
        previousAverage: f ? C / f : null
      } : {}
    });
  }
  return e;
}
function A(n) {
  const i = n.revenueChart.points.reduce((e, s) => e + Number(s.y || 0), 0), t = n.ordersChart.points.reduce((e, s) => e + Number(s.y || 0), 0);
  return { amount: i, orders: t, average: t ? i / t : null };
}
function T(n, i) {
  if (n == null || i == null) return "No comparison available";
  if (i === 0) return n === 0 ? "No change" : "No prior value";
  const t = (n - i) / Math.abs(i) * 100;
  return `${t > 0 ? "+" : ""}${t.toLocaleString(void 0, { maximumFractionDigits: 1 })}%`;
}
class I extends F(HTMLElement) {
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
    h(this, "datePreset");
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
    var t, e, s;
    this.disposed = !0, this.chartRequest++, this.productRequest++, (t = this.chartAbort) == null || t.abort(), (e = this.productAbort) == null || e.abort(), (s = this.resizeObserver) == null || s.disconnect(), this.resizeObserver = void 0, super.disconnectedCallback();
  }
  get currency() {
    var t;
    return (t = m.stores.find((e) => {
      var s;
      return e.alias === (((s = this.chartFilters) == null ? void 0 : s.store) || m.filters.store);
    })) == null ? void 0 : t.currency;
  }
  async initialize() {
    var t;
    try {
      const [e, s] = await Promise.all([this.api.statusList(), this.api.stores()]);
      if (this.disposed) return;
      m.statusList = e || [], m.stores = s || [], m.stores.some((a) => a.alias === m.filters.store) || (m.filters.store = ((t = m.stores[0]) == null ? void 0 : t.alias) || ""), await this.loadAnalytics();
    } catch (e) {
      if (this.disposed) return;
      this.error = w(e, "Unable to load Analytics."), this.loading = !1, this.render();
    }
  }
  async loadAnalytics() {
    await Promise.all([this.loadCharts(), this.productsVisible ? this.loadMostSoldProducts() : Promise.resolve()]);
  }
  async loadCharts() {
    var a;
    const t = ++this.chartRequest;
    (a = this.chartAbort) == null || a.abort();
    const e = this.chartAbort = new AbortController(), s = { ...m.filters };
    if (this.chartData = void 0, this.previousData = void 0, this.error = "", this.loading = !!s.store, this.render(), !!s.store)
      try {
        const o = this.compare ? { ...s, ...V(s.dateFrom, s.dateTo) } : void 0, [d, r] = await Promise.all([
          this.api.charts(s, e.signal),
          o ? this.api.charts(o, e.signal) : Promise.resolve(void 0)
        ]);
        if (t !== this.chartRequest || this.disposed) return;
        this.chartData = d, this.previousData = r, this.chartFilters = s, d.mixedCurrencies && (this.metric = "orders");
      } catch (o) {
        if (t !== this.chartRequest || this.disposed) return;
        this.error = w(o, "Unable to load charts.");
      } finally {
        t === this.chartRequest && !this.disposed && (this.loading = !1, this.render());
      }
  }
  async loadMostSoldProducts() {
    var a;
    const t = ++this.productRequest;
    (a = this.productAbort) == null || a.abort();
    const e = this.productAbort = new AbortController(), s = { ...m.filters };
    if (this.mostSoldProducts = { products: [], count: 0, totalPages: 0, page: this.pageMostSoldProducts }, this.productError = "", this.loadingMostSoldProducts = !!s.store, this.render(), !!s.store)
      try {
        const o = await this.api.mostSoldProducts(s, this.pageMostSoldProducts, 20, e.signal);
        if (t !== this.productRequest || this.disposed) return;
        this.mostSoldProducts = { ...o, products: o.products || [] }, this.pageMostSoldProducts = o.page || this.pageMostSoldProducts;
      } catch (o) {
        t === this.productRequest && !this.disposed && (this.productError = w(o, "Unable to load products."));
      } finally {
        t === this.productRequest && !this.disposed && (this.loadingMostSoldProducts = !1, this.render());
      }
  }
  get buckets() {
    return this.chartData ? z(this.chartData, this.previousData, this.grouping) : [];
  }
  get comparisonMoneyAvailable() {
    var t, e;
    return !!this.previousData && !((t = this.chartData) != null && t.mixedCurrencies) && !this.previousData.mixedCurrencies && (!((e = this.chartData) != null && e.currency) || !this.previousData.currency || this.chartData.currency === this.previousData.currency);
  }
  amount(t) {
    var s, a, o, d, r;
    if (t == null || (s = this.chartData) != null && s.mixedCurrencies) return "—";
    const e = ((a = this.chartData) == null ? void 0 : a.currency) || ((o = this.previousData) == null ? void 0 : o.currency);
    return !e || e === ((d = this.currency) == null ? void 0 : d.currencyValue) || e === ((r = this.currency) == null ? void 0 : r.isoCurrencySymbol) ? D(t, this.currency) : /^[A-Z]{3}$/.test(e) ? D(t, { isoCurrencySymbol: e }) : `${t.toLocaleString(void 0, { maximumFractionDigits: 2 })} ${e}`;
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
    const t = m.filters, e = j(t.dateFrom, t.dateTo, this.datePreset);
    this.innerHTML = `<style>${L}
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
        <label>Store<select data-field="store">${m.stores.map((s) => `<option value="${c(s.alias)}" ${s.alias === t.store ? "selected" : ""}>${c(s.title)}</option>`).join("")}</select></label>
        <label>Order status<select data-field="orderStatus"><option value="CompletedOrders" ${t.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option><option value="AllOrders" ${t.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>${m.statusList.map((s) => `<option value="${c(P(s))}" ${P(s) === t.orderStatus ? "selected" : ""}>${c(s.label)}</option>`).join("")}</select></label>
        <label>Date From<input type="date" data-field="dateFrom" value="${c(t.dateFrom)}"></label>
        <label>Date To<input type="date" data-field="dateTo" value="${c(t.dateTo)}"></label>
        <label>Date preset<select data-preset><option value="">Custom range</option>${[["today", "Today"], ["7", "Last 7 days"], ["30", "Last 30 days"], ["month", "This month"], ["year", "This year"]].map(([s, a]) => `<option value="${s}" ${e === s ? "selected" : ""}>${a}</option>`).join("")}</select></label>
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
    var s, a, o, d;
    const t = A(this.chartData), e = this.previousData ? A(this.previousData) : void 0;
    return `<div class="analytics-summary">${["amount", "orders", "average"].map((r) => `<div class="card"><span>${this.label(r)}</span><strong>${c(this.value(t[r], r))}</strong><span class="analytics-muted">${e && (r === "orders" || this.comparisonMoneyAvailable) ? c(T(t[r], e[r])) + " vs previous period" : this.compare ? "No monetary comparison available" : "Selected period"}</span></div>`).join("")}</div>
      <p class="analytics-muted">Order amount is the recorded charged amount, after gift cards; it is not refund-adjusted revenue. Dates use ${this.chartData.dateBasis === "PaidDate" ? "payment date" : "order creation date"}, matching Orders.</p>
      ${this.chartData.mixedCurrencies ? '<p role="status">This period contains multiple currencies. Monetary totals are hidden to avoid combining different currencies.</p>' : ""}
      ${this.previousData && !this.comparisonMoneyAvailable ? '<p class="analytics-muted">Monetary comparison is unavailable across different or mixed currencies.</p>' : ""}
      <div class="card analytics-main"><div class="analytics-toolbar">
        ${["amount", "orders", "average"].map((r) => `<button data-metric="${r}" aria-pressed="${this.metric === r}" ${r !== "orders" && this.chartData.mixedCurrencies ? "disabled" : ""}>${this.label(r)}</button>`).join("")}
        <label>Group by<select data-grouping>${[["day", "Daily"], ["week", "Weekly"], ["month", "Monthly"]].map(([r, g]) => `<option value="${r}" ${this.grouping === r ? "selected" : ""}>${g}</option>`).join("")}</select></label>
        <button data-table aria-pressed="${this.table}">${this.table ? "Show chart" : "Show data table"}</button><button data-csv>Download CSV</button>
      </div>
      <p class="analytics-muted">${this.previousData ? `Solid: ${c(this.range((s = this.chartFilters) == null ? void 0 : s.dateFrom, (a = this.chartFilters) == null ? void 0 : a.dateTo))}. Dashed: ${c(this.range((o = this.previousData.ordersChart.points[0]) == null ? void 0 : o.x, (d = this.previousData.ordersChart.points.at(-1)) == null ? void 0 : d.x))}. Comparison buckets cover the same number of days.` : "Selected period."} Click a bucket to open its orders.</p>
      ${this.table ? this.renderTable() : `<div style="height:360px"><canvas id="analytics-chart" style="width:100%;height:100%" role="img" aria-label="${this.label(this.metric)} over time. Use Show data table for exact values and order links."></canvas></div>`}
      </div>`;
  }
  renderCustomerInsights() {
    var g, l;
    const t = (g = this.chartData) == null ? void 0 : g.customerInsights;
    if (!t) return "";
    const e = (l = this.previousData) == null ? void 0 : l.customerInsights, s = [
      ["Unique purchasing customers", t.uniqueCustomers, e == null ? void 0 : e.uniqueCustomers, !1],
      ["First-time buyers", t.newCustomers, e == null ? void 0 : e.newCustomers, !1],
      ["Returning customer rate", t.returningCustomerRate, e == null ? void 0 : e.returningCustomerRate, !0]
    ], a = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"], o = a.map((y, p) => t.orderTimes.filter((u) => u.weekday === p).reduce((u, b) => u + b.orders, 0)), d = Array.from({ length: 24 }, (y, p) => t.orderTimes.filter((u) => u.hour === p).reduce((u, b) => u + b.orders, 0)), r = (y, p) => y.map((u, b) => `<div class="analytics-time-row"><span>${u}</span><div class="analytics-time-track" aria-hidden="true"><span style="width:${100 * p[b] / Math.max(1, ...p)}%"></span></div><span>${p[b]}</span></div>`).join("");
    return `<div class="analytics-summary">${s.map(([y, p, u, b]) => `<div class="card"><span>${y}</span><strong>${p == null ? "—" : c(p.toLocaleString(void 0, { maximumFractionDigits: b ? 1 : 0 })) + (b ? "%" : "")}</strong><span class="analytics-muted">${e ? b ? p == null || u == null ? "No comparison available" : `${(p - u).toLocaleString(void 0, { maximumFractionDigits: 1, signDisplay: "always" })} percentage points vs previous period` : `${T(p, u ?? null)} vs previous period` : "Selected period"}</span></div>`).join("")}</div>
      <div class="card analytics-main"><h3>New and returning purchases</h3>
        <p class="analytics-muted">Based on qualifying purchase orders and ${t.identityField === "CustomerUsername" ? "username" : "email"}. A repeat purchase has an earlier qualifying order in this store, ordered by payment time (creation time when unpaid). A buyer can appear in both rows if they buy for the first time and return in this period. Returning rate is repeat buyers divided by unique identified buyers.</p>
        <div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Purchase type</th><th>Buyers</th><th>Orders</th><th>Order amount</th></tr></thead><tbody>
          <tr><td>First purchase</td><td>${t.newCustomers}</td><td>${t.firstOrders}</td><td>${c(this.amount(t.firstOrderAmount))}</td></tr>
          <tr><td>Repeat purchase</td><td>${t.returningCustomers}</td><td>${t.repeatOrders}</td><td>${c(this.amount(t.repeatOrderAmount))}</td></tr>
        </tbody></table></div>
        <p class="analytics-muted">${t.missingIdentifiers} qualifying orders have no customer identifier and are excluded from buyer counts and this breakdown. They remain included in the time charts below. Incomplete and cancelled orders are excluded from these customer insights, even with All Orders selected.</p>
      </div>
      <div class="analytics-insight-grid">
        <div class="card analytics-main"><h3>Orders by weekday</h3><p class="analytics-muted">Qualifying orders within the selected filters, grouped by recorded creation day.</p><div class="analytics-time-bars">${r(a, o)}</div></div>
        <div class="card analytics-main"><h3>Orders by hour</h3><p class="analytics-muted">Recorded creation time, 00–23. No timezone conversion. Totals are counts, not averages per hour.</p><div class="analytics-time-bars analytics-hour-bars">${r(d.map((y, p) => String(p).padStart(2, "0") + ":00"), d)}</div></div>
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
          ${R(this.pageMostSoldProducts, this.mostSoldProducts.totalPages).map((t) => {
      const e = Number(String(t).replace("...", ""));
      return `<li class="${e === this.pageMostSoldProducts ? "active" : ""}"><button type="button" data-action="set-most-sold-page" data-page="${e}" ${e === this.pageMostSoldProducts ? "disabled" : ""}>${c(t)}</button></li>`;
    }).join("")}
        </ul>
      </div>
    `;
  }
  renderCharts() {
    var d;
    const t = this.querySelector("#analytics-chart");
    if (!t || !this.chartData || this.table) return;
    const e = this.buckets, s = this.chartData.currency || ((d = this.previousData) == null ? void 0 : d.currency), a = s && /^[A-Z]{3}$/.test(s) ? { isoCurrencySymbol: s } : this.currency, o = this.previousData && (this.metric === "orders" || this.comparisonMoneyAvailable) ? { labels: [], points: e.map((r) => ({ x: this.range(r.previousStart, r.previousEnd), y: this.previousValue(r) })) } : void 0;
    U(t, { labels: [], points: e.map((r) => ({ x: this.range(r.start, r.end), y: r[this.metric] })) }, "#25765c", {
      currency: this.metric === "orders" ? void 0 : a,
      integer: this.metric === "orders",
      comparison: o,
      formatValue: (r) => this.value(r),
      columnHover: !0,
      onSelect: (r) => this.openOrders(r)
    });
  }
  changedFilters() {
    this.pageMostSoldProducts = 1, this.dispatchEvent(new CustomEvent("analytics-filters-changed", { bubbles: !0, detail: { ...m.filters } })), this.loadAnalytics();
  }
  openOrders(t) {
    const e = this.buckets[t];
    if (!e || !this.chartFilters) return;
    const s = { ...this.chartFilters, dateFrom: e.start, dateTo: e.end };
    for (const a of ["query", "paymentProvider", "shippingProvider", "couponCode", "giftcardCode", "productSku", "trackingSource", "trackingMedium", "trackingCampaign", "trackingTerm", "trackingContent", "trackingClickId"]) s[a] = "";
    if (Object.assign(m.filters, s), this.hasAttribute("classic")) this.dispatchEvent(new CustomEvent("analytics-open-orders", { bubbles: !0, detail: s }));
    else {
      const a = new URLSearchParams({ analyticsDrill: "1", store: s.store, start: s.dateFrom, end: s.dateTo, orderStatus: s.orderStatus }), o = document.createElement("a");
      o.href = location.pathname.replace(/\/analytics\/?$/, "/orders") + "?" + a, this.appendChild(o), o.click(), o.remove();
    }
  }
  exportCsv() {
    var s, a, o;
    const t = [["From", "To", "Orders", "Order amount", "Average order", "Currency", "Previous from", "Previous to", "Previous orders", "Previous amount", "Previous average"]];
    for (const d of this.buckets) t.push([d.start, d.end, String(d.orders), (s = this.chartData) != null && s.mixedCurrencies ? "" : String(d.amount), (a = this.chartData) != null && a.mixedCurrencies || d.average == null ? "" : String(d.average), ((o = this.chartData) == null ? void 0 : o.currency) || "", d.previousStart || "", d.previousEnd || "", d.previousOrders == null ? "" : String(d.previousOrders), this.comparisonMoneyAvailable ? String(d.previousAmount ?? "") : "", this.comparisonMoneyAvailable ? String(d.previousAverage ?? "") : ""]);
    const e = t.map((d) => d.map((r) => '"' + r.replaceAll('"', '""') + '"').join(",")).join(`\r
`);
    N(new Blob(["\uFEFF", e], { type: "text/csv;charset=utf-8" }), "analytics.csv");
  }
  bindEvents() {
    var t, e, s, a, o, d, r, g;
    this.querySelectorAll("[data-field]").forEach((l) => l.addEventListener("change", () => {
      const y = l.dataset.field;
      (y === "dateFrom" || y === "dateTo") && (this.datePreset = void 0), m.filters[y] = l.value, this.changedFilters();
    })), (t = this.querySelector("[data-preset]")) == null || t.addEventListener("change", (l) => {
      const y = l.target.value;
      this.datePreset = y, y ? (Object.assign(m.filters, M(y)), this.changedFilters()) : this.render();
    }), (e = this.querySelector("[data-compare]")) == null || e.addEventListener("change", () => {
      this.compare = !this.compare, this.loadCharts();
    }), (s = this.querySelector("[data-grouping]")) == null || s.addEventListener("change", (l) => {
      this.grouping = l.target.value, this.render();
    }), this.querySelectorAll("[data-metric]").forEach((l) => l.addEventListener("click", () => {
      this.metric = l.dataset.metric, this.render();
    })), (a = this.querySelector("[data-table]")) == null || a.addEventListener("click", () => {
      this.table = !this.table, this.render();
    }), (o = this.querySelector("[data-csv]")) == null || o.addEventListener("click", () => this.exportCsv()), (d = this.querySelector("[data-refresh]")) == null || d.addEventListener("click", () => void this.loadAnalytics()), this.querySelectorAll("[data-orders]").forEach((l) => l.addEventListener("click", () => this.openOrders(Number(l.dataset.orders)))), (r = this.querySelector("[data-products]")) == null || r.addEventListener("click", () => {
      var l;
      this.productsVisible = !this.productsVisible, this.productsVisible ? this.loadMostSoldProducts() : (this.productRequest++, (l = this.productAbort) == null || l.abort(), this.render());
    }), (g = this.querySelector("[data-product-retry]")) == null || g.addEventListener("click", () => void this.loadMostSoldProducts()), this.querySelectorAll('[data-action="set-most-sold-page"]').forEach((l) => l.addEventListener("click", () => {
      this.pageMostSoldProducts = Number(l.dataset.page), this.loadMostSoldProducts();
    }));
  }
}
function $(n, ...i) {
  for (const t of i) if (n[t] != null) return n[t];
  return "";
}
function w(n, i) {
  return n instanceof Error ? n.message : i;
}
customElements.define("ekom-analytics-section-view", I);
export {
  I as EkomAnalyticsSectionViewElement,
  I as default
};
