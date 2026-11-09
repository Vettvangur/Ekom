var M = Object.defineProperty;
var w = (o, i, t) => i in o ? M(o, i, { enumerable: !0, configurable: !0, writable: !0, value: t }) : o[i] = t;
var c = (o, i, t) => w(o, typeof i != "symbol" ? i + "" : i, t);
import { UmbElementMixin as E } from "@umbraco-cms/backoffice/element-api";
import { E as O, m as h, f as x, a as q, e as u, g as A, p as F, r as L, d as U } from "./manager-shared.js";
const D = 864e5, f = (o) => o.toISOString().slice(0, 10), S = (o) => /* @__PURE__ */ new Date(`${o}T00:00:00Z`);
function R(o, i) {
  const t = S(o), e = S(i), s = Math.round((e.getTime() - t.getTime()) / D) + 1;
  return { dateFrom: f(new Date(t.getTime() - s * D)), dateTo: f(new Date(t.getTime() - D)) };
}
function N(o, i = /* @__PURE__ */ new Date()) {
  const t = new Date(Date.UTC(i.getFullYear(), i.getMonth(), i.getDate())), e = new Date(t);
  return o === "7" && e.setUTCDate(e.getUTCDate() - 6), o === "30" && e.setUTCDate(e.getUTCDate() - 29), o === "month" && e.setUTCDate(1), o === "year" && (e.setUTCMonth(0), e.setUTCDate(1)), { dateFrom: f(e), dateTo: f(t) };
}
function V(o, i, t) {
  var n, d, a, m, l;
  const e = [], s = o.ordersChart.points;
  let r = 0;
  for (; r < s.length; ) {
    const b = r, P = s[r].x, p = S(P);
    t === "month" ? (p.setUTCDate(1), p.setUTCMonth(p.getUTCMonth() + 1)) : t === "week" ? p.setUTCDate(p.getUTCDate() + (7 - (p.getUTCDay() + 6) % 7)) : p.setUTCDate(p.getUTCDate() + 1);
    let $ = 0, v = 0, C = 0, g = 0;
    do
      $ += Number(((n = o.revenueChart.points[r]) == null ? void 0 : n.y) || 0), v += Number(s[r].y || 0), C += Number(((d = i == null ? void 0 : i.revenueChart.points[r]) == null ? void 0 : d.y) || 0), g += Number(((a = i == null ? void 0 : i.ordersChart.points[r]) == null ? void 0 : a.y) || 0), r++;
    while (r < s.length && S(s[r].x).getTime() < p.getTime());
    e.push({
      start: P,
      end: s[r - 1].x,
      amount: $,
      orders: v,
      average: v ? $ / v : null,
      ...i ? {
        previousStart: (m = i.ordersChart.points[b]) == null ? void 0 : m.x,
        previousEnd: (l = i.ordersChart.points[r - 1]) == null ? void 0 : l.x,
        previousAmount: C,
        previousOrders: g,
        previousAverage: g ? C / g : null
      } : {}
    });
  }
  return e;
}
function T(o) {
  const i = o.revenueChart.points.reduce((e, s) => e + Number(s.y || 0), 0), t = o.ordersChart.points.reduce((e, s) => e + Number(s.y || 0), 0);
  return { amount: i, orders: t, average: t ? i / t : null };
}
function j(o, i) {
  if (o == null || i == null) return "No comparison available";
  if (i === 0) return o === 0 ? "No change" : "No prior value";
  const t = (o - i) / Math.abs(i) * 100;
  return `${t > 0 ? "+" : ""}${t.toLocaleString(void 0, { maximumFractionDigits: 1 })}%`;
}
class z extends E(HTMLElement) {
  constructor() {
    super(...arguments);
    c(this, "api", new O());
    c(this, "loading", !0);
    c(this, "loadingMostSoldProducts", !1);
    c(this, "error", "");
    c(this, "productError", "");
    c(this, "chartData");
    c(this, "previousData");
    c(this, "chartFilters");
    c(this, "mostSoldProducts", { products: [], count: 0, totalPages: 0, page: 1 });
    c(this, "pageMostSoldProducts", 1);
    c(this, "resizeObserver");
    c(this, "chartRequest", 0);
    c(this, "productRequest", 0);
    c(this, "chartAbort");
    c(this, "productAbort");
    c(this, "disposed", !1);
    c(this, "productsVisible", !1);
    c(this, "metric", "amount");
    c(this, "grouping", "day");
    c(this, "compare", !0);
    c(this, "table", !1);
  }
  connectedCallback() {
    if (super.connectedCallback(), this.disposed = !1, this.hasAttribute("classic"))
      for (const t of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const e = this.getAttribute(t.toLowerCase());
        e && (h.filters[t] = e);
      }
    this.render(), this.resizeObserver = new ResizeObserver(() => this.renderCharts()), this.resizeObserver.observe(this), this.initialize();
  }
  disconnectedCallback() {
    var t, e, s;
    this.disposed = !0, this.chartRequest++, this.productRequest++, (t = this.chartAbort) == null || t.abort(), (e = this.productAbort) == null || e.abort(), (s = this.resizeObserver) == null || s.disconnect(), this.resizeObserver = void 0, super.disconnectedCallback();
  }
  get currency() {
    var t;
    return (t = h.stores.find((e) => {
      var s;
      return e.alias === (((s = this.chartFilters) == null ? void 0 : s.store) || h.filters.store);
    })) == null ? void 0 : t.currency;
  }
  async initialize() {
    var t;
    try {
      const [e, s] = await Promise.all([this.api.statusList(), this.api.stores()]);
      if (this.disposed) return;
      h.statusList = e || [], h.stores = s || [], h.stores.some((r) => r.alias === h.filters.store) || (h.filters.store = ((t = h.stores[0]) == null ? void 0 : t.alias) || ""), await this.loadAnalytics();
    } catch (e) {
      if (this.disposed) return;
      this.error = k(e, "Unable to load Analytics."), this.loading = !1, this.render();
    }
  }
  async loadAnalytics() {
    await Promise.all([this.loadCharts(), this.productsVisible ? this.loadMostSoldProducts() : Promise.resolve()]);
  }
  async loadCharts() {
    var r;
    const t = ++this.chartRequest;
    (r = this.chartAbort) == null || r.abort();
    const e = this.chartAbort = new AbortController(), s = { ...h.filters };
    if (this.chartData = void 0, this.previousData = void 0, this.error = "", this.loading = !!s.store, this.render(), !!s.store)
      try {
        const n = this.compare ? { ...s, ...R(s.dateFrom, s.dateTo) } : void 0, [d, a] = await Promise.all([
          this.api.charts(s, e.signal),
          n ? this.api.charts(n, e.signal) : Promise.resolve(void 0)
        ]);
        if (t !== this.chartRequest || this.disposed) return;
        this.chartData = d, this.previousData = a, this.chartFilters = s, d.mixedCurrencies && (this.metric = "orders");
      } catch (n) {
        if (t !== this.chartRequest || this.disposed) return;
        this.error = k(n, "Unable to load charts.");
      } finally {
        t === this.chartRequest && !this.disposed && (this.loading = !1, this.render());
      }
  }
  async loadMostSoldProducts() {
    var r;
    const t = ++this.productRequest;
    (r = this.productAbort) == null || r.abort();
    const e = this.productAbort = new AbortController(), s = { ...h.filters };
    if (this.mostSoldProducts = { products: [], count: 0, totalPages: 0, page: this.pageMostSoldProducts }, this.productError = "", this.loadingMostSoldProducts = !!s.store, this.render(), !!s.store)
      try {
        const n = await this.api.mostSoldProducts(s, this.pageMostSoldProducts, 20, e.signal);
        if (t !== this.productRequest || this.disposed) return;
        this.mostSoldProducts = { ...n, products: n.products || [] }, this.pageMostSoldProducts = n.page || this.pageMostSoldProducts;
      } catch (n) {
        t === this.productRequest && !this.disposed && (this.productError = k(n, "Unable to load products."));
      } finally {
        t === this.productRequest && !this.disposed && (this.loadingMostSoldProducts = !1, this.render());
      }
  }
  get buckets() {
    return this.chartData ? V(this.chartData, this.previousData, this.grouping) : [];
  }
  get comparisonMoneyAvailable() {
    var t, e;
    return !!this.previousData && !((t = this.chartData) != null && t.mixedCurrencies) && !this.previousData.mixedCurrencies && (!((e = this.chartData) != null && e.currency) || !this.previousData.currency || this.chartData.currency === this.previousData.currency);
  }
  amount(t) {
    var s, r, n, d, a;
    if (t == null || (s = this.chartData) != null && s.mixedCurrencies) return "—";
    const e = ((r = this.chartData) == null ? void 0 : r.currency) || ((n = this.previousData) == null ? void 0 : n.currency);
    return !e || e === ((d = this.currency) == null ? void 0 : d.currencyValue) || e === ((a = this.currency) == null ? void 0 : a.isoCurrencySymbol) ? x(t, this.currency) : /^[A-Z]{3}$/.test(e) ? x(t, { isoCurrencySymbol: e }) : `${t.toLocaleString(void 0, { maximumFractionDigits: 2 })} ${e}`;
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
    const t = h.filters;
    this.innerHTML = `<style>${q}
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
        <label>Store<select data-field="store">${h.stores.map((e) => `<option value="${u(e.alias)}" ${e.alias === t.store ? "selected" : ""}>${u(e.title)}</option>`).join("")}</select></label>
        <label>Order status<select data-field="orderStatus"><option value="CompletedOrders" ${t.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option><option value="AllOrders" ${t.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>${h.statusList.map((e) => `<option value="${u(A(e))}" ${A(e) === t.orderStatus ? "selected" : ""}>${u(e.label)}</option>`).join("")}</select></label>
        <label>Date From<input type="date" data-field="dateFrom" value="${u(t.dateFrom)}"></label>
        <label>Date To<input type="date" data-field="dateTo" value="${u(t.dateTo)}"></label>
        <label>Date preset<select data-preset><option value="">Custom range</option>${[["today", "Today"], ["7", "Last 7 days"], ["30", "Last 30 days"], ["month", "This month"], ["year", "This year"]].map(([e, s]) => `<option value="${e}">${s}</option>`).join("")}</select></label>
        <label><span>Comparison</span><span><input type="checkbox" data-compare ${this.compare ? "checked" : ""}> Previous period</span></label>
        <button data-refresh>Refresh</button>
      </div>
      ${this.error ? `<p role="alert">${u(this.error)}</p>` : ""}
      ${this.loading ? '<p role="status">Loading analytics…</p>' : t.store ? "" : "<p>No accessible stores.</p>"}
      ${this.chartData ? this.renderAnalytics() : ""}
      <div class="card analytics-main"><div class="analytics-toolbar"><h3>Most Sold Products</h3><button data-products>${this.productsVisible ? "Hide products" : "Load product breakdown"}</button></div>
        <p class="analytics-muted">Fetch the most sold products for the selected store and dates. This may take a minute for larger date ranges. Totals include VAT and discounts, excluding shipping and payment fees.</p>
        ${this.productsVisible ? `${this.productError ? `<p role="alert">${u(this.productError)}</p><button data-product-retry>Retry products</button>` : ""}${this.loadingMostSoldProducts ? '<p role="status"><span class="analytics-spinner" aria-hidden="true"></span>Fetching product breakdown… This may take a minute. You can continue using Analytics.</p>' : this.renderMostSoldProducts()}` : ""}
      </div>
    </div></section>`, this.bindEvents(), this.renderCharts();
  }
  renderAnalytics() {
    var s, r, n, d;
    const t = T(this.chartData), e = this.previousData ? T(this.previousData) : void 0;
    return `<div class="analytics-summary">${["amount", "orders", "average"].map((a) => `<div class="card"><span>${this.label(a)}</span><strong>${u(this.value(t[a], a))}</strong><span class="analytics-muted">${e && (a === "orders" || this.comparisonMoneyAvailable) ? u(j(t[a], e[a])) + " vs previous period" : this.compare ? "No monetary comparison available" : "Selected period"}</span></div>`).join("")}</div>
      <p class="analytics-muted">Order amount is the recorded charged amount, after gift cards; it is not refund-adjusted revenue. Dates use ${this.chartData.dateBasis === "PaidDate" ? "payment date" : "order creation date"}, matching Orders.</p>
      ${this.chartData.mixedCurrencies ? '<p role="status">This period contains multiple currencies. Monetary totals are hidden to avoid combining different currencies.</p>' : ""}
      ${this.previousData && !this.comparisonMoneyAvailable ? '<p class="analytics-muted">Monetary comparison is unavailable across different or mixed currencies.</p>' : ""}
      <div class="card analytics-main"><div class="analytics-toolbar">
        ${["amount", "orders", "average"].map((a) => `<button data-metric="${a}" aria-pressed="${this.metric === a}" ${a !== "orders" && this.chartData.mixedCurrencies ? "disabled" : ""}>${this.label(a)}</button>`).join("")}
        <label>Group by<select data-grouping>${[["day", "Daily"], ["week", "Weekly"], ["month", "Monthly"]].map(([a, m]) => `<option value="${a}" ${this.grouping === a ? "selected" : ""}>${m}</option>`).join("")}</select></label>
        <button data-table aria-pressed="${this.table}">${this.table ? "Show chart" : "Show data table"}</button><button data-csv>Download CSV</button>
      </div>
      <p class="analytics-muted">${this.previousData ? `Solid: ${u(this.range((s = this.chartFilters) == null ? void 0 : s.dateFrom, (r = this.chartFilters) == null ? void 0 : r.dateTo))}. Dashed: ${u(this.range((n = this.previousData.ordersChart.points[0]) == null ? void 0 : n.x, (d = this.previousData.ordersChart.points.at(-1)) == null ? void 0 : d.x))}. Comparison buckets cover the same number of days.` : "Selected period."} Click a bucket to open its orders.</p>
      ${this.table ? this.renderTable() : `<div style="height:360px"><canvas id="analytics-chart" style="width:100%;height:100%" role="img" aria-label="${this.label(this.metric)} over time. Use Show data table for exact values and order links."></canvas></div>`}
      </div>`;
  }
  renderTable() {
    return `<div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Period</th><th>Orders</th><th>Order amount</th><th>Average order</th>${this.previousData ? `<th>Previous period</th><th>Previous ${this.label(this.metric).toLowerCase()}</th>` : ""}<th>Details</th></tr></thead><tbody>${this.buckets.map((t, e) => `<tr><td>${u(this.range(t.start, t.end))}</td><td>${t.orders}</td><td>${u(this.amount(t.amount))}</td><td>${u(this.amount(t.average))}</td>${this.previousData ? `<td>${u(this.range(t.previousStart, t.previousEnd))}</td><td>${u(this.metric === "orders" || this.comparisonMoneyAvailable ? this.value(this.previousValue(t)) : "—")}</td>` : ""}<td><button data-orders="${e}">Open orders</button></td></tr>`).join("")}</tbody></table></div>`;
  }
  renderMostSoldProducts() {
    return this.mostSoldProducts.products.length ? `
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell">Product</div><div class="umb-table-cell">Sku</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Total</div></div></div>
        <div class="umb-table-body">
          ${this.mostSoldProducts.products.map((t) => `<div class="umb-table-row"><div class="umb-table-cell">${u(y(t, "title", "productTitle", "name"))}</div><div class="umb-table-cell">${u(y(t, "sku", "productSku"))}</div><div class="umb-table-cell">${u(y(t, "productCount", "quantity", "count"))}</div><div class="umb-table-cell">${u(x(Number(y(t, "total")), this.currency))}</div></div>`).join("")}
        </div>
      </div>
      ${this.mostSoldProducts.totalPages > 1 ? this.renderMostSoldProductsPagination() : ""}
    ` : "<p>No products found.</p>";
  }
  renderMostSoldProductsPagination() {
    return `
      <div class="pagination">
        <ul>
          ${F(this.pageMostSoldProducts, this.mostSoldProducts.totalPages).map((t) => {
      const e = Number(String(t).replace("...", ""));
      return `<li class="${e === this.pageMostSoldProducts ? "active" : ""}"><button type="button" data-action="set-most-sold-page" data-page="${e}" ${e === this.pageMostSoldProducts ? "disabled" : ""}>${u(t)}</button></li>`;
    }).join("")}
        </ul>
      </div>
    `;
  }
  renderCharts() {
    var d;
    const t = this.querySelector("#analytics-chart");
    if (!t || !this.chartData || this.table) return;
    const e = this.buckets, s = this.chartData.currency || ((d = this.previousData) == null ? void 0 : d.currency), r = s && /^[A-Z]{3}$/.test(s) ? { isoCurrencySymbol: s } : this.currency, n = this.previousData && (this.metric === "orders" || this.comparisonMoneyAvailable) ? { labels: [], points: e.map((a) => ({ x: this.range(a.previousStart, a.previousEnd), y: this.previousValue(a) })) } : void 0;
    L(t, { labels: [], points: e.map((a) => ({ x: this.range(a.start, a.end), y: a[this.metric] })) }, "#25765c", {
      currency: this.metric === "orders" ? void 0 : r,
      integer: this.metric === "orders",
      comparison: n,
      formatValue: (a) => this.value(a),
      columnHover: !0,
      onSelect: (a) => this.openOrders(a)
    });
  }
  changedFilters() {
    this.pageMostSoldProducts = 1, this.dispatchEvent(new CustomEvent("analytics-filters-changed", { bubbles: !0, detail: { ...h.filters } })), this.loadAnalytics();
  }
  openOrders(t) {
    const e = this.buckets[t];
    if (!e || !this.chartFilters) return;
    const s = { ...this.chartFilters, dateFrom: e.start, dateTo: e.end };
    for (const r of ["query", "paymentProvider", "shippingProvider", "couponCode", "giftcardCode", "productSku", "trackingSource", "trackingMedium", "trackingCampaign", "trackingTerm", "trackingContent", "trackingClickId"]) s[r] = "";
    if (Object.assign(h.filters, s), this.hasAttribute("classic")) this.dispatchEvent(new CustomEvent("analytics-open-orders", { bubbles: !0, detail: s }));
    else {
      const r = new URLSearchParams({ analyticsDrill: "1", store: s.store, start: s.dateFrom, end: s.dateTo, orderStatus: s.orderStatus }), n = document.createElement("a");
      n.href = location.pathname.replace(/\/analytics\/?$/, "/orders") + "?" + r, this.appendChild(n), n.click(), n.remove();
    }
  }
  exportCsv() {
    var s, r, n;
    const t = [["From", "To", "Orders", "Order amount", "Average order", "Currency", "Previous from", "Previous to", "Previous orders", "Previous amount", "Previous average"]];
    for (const d of this.buckets) t.push([d.start, d.end, String(d.orders), (s = this.chartData) != null && s.mixedCurrencies ? "" : String(d.amount), (r = this.chartData) != null && r.mixedCurrencies || d.average == null ? "" : String(d.average), ((n = this.chartData) == null ? void 0 : n.currency) || "", d.previousStart || "", d.previousEnd || "", d.previousOrders == null ? "" : String(d.previousOrders), this.comparisonMoneyAvailable ? String(d.previousAmount ?? "") : "", this.comparisonMoneyAvailable ? String(d.previousAverage ?? "") : ""]);
    const e = t.map((d) => d.map((a) => '"' + a.replaceAll('"', '""') + '"').join(",")).join(`\r
`);
    U(new Blob(["\uFEFF", e], { type: "text/csv;charset=utf-8" }), "analytics.csv");
  }
  bindEvents() {
    var t, e, s, r, n, d, a, m;
    this.querySelectorAll("[data-field]").forEach((l) => l.addEventListener("change", () => {
      const b = l.dataset.field;
      h.filters[b] = l.value, this.changedFilters();
    })), (t = this.querySelector("[data-preset]")) == null || t.addEventListener("change", (l) => {
      const b = l.target.value;
      b && (Object.assign(h.filters, N(b)), this.changedFilters());
    }), (e = this.querySelector("[data-compare]")) == null || e.addEventListener("change", () => {
      this.compare = !this.compare, this.loadCharts();
    }), (s = this.querySelector("[data-grouping]")) == null || s.addEventListener("change", (l) => {
      this.grouping = l.target.value, this.render();
    }), this.querySelectorAll("[data-metric]").forEach((l) => l.addEventListener("click", () => {
      this.metric = l.dataset.metric, this.render();
    })), (r = this.querySelector("[data-table]")) == null || r.addEventListener("click", () => {
      this.table = !this.table, this.render();
    }), (n = this.querySelector("[data-csv]")) == null || n.addEventListener("click", () => this.exportCsv()), (d = this.querySelector("[data-refresh]")) == null || d.addEventListener("click", () => void this.loadAnalytics()), this.querySelectorAll("[data-orders]").forEach((l) => l.addEventListener("click", () => this.openOrders(Number(l.dataset.orders)))), (a = this.querySelector("[data-products]")) == null || a.addEventListener("click", () => {
      var l;
      this.productsVisible = !this.productsVisible, this.productsVisible ? this.loadMostSoldProducts() : (this.productRequest++, (l = this.productAbort) == null || l.abort(), this.render());
    }), (m = this.querySelector("[data-product-retry]")) == null || m.addEventListener("click", () => void this.loadMostSoldProducts()), this.querySelectorAll('[data-action="set-most-sold-page"]').forEach((l) => l.addEventListener("click", () => {
      this.pageMostSoldProducts = Number(l.dataset.page), this.loadMostSoldProducts();
    }));
  }
}
function y(o, ...i) {
  for (const t of i) if (o[t] != null) return o[t];
  return "";
}
function k(o, i) {
  return o instanceof Error ? o.message : i;
}
customElements.define("ekom-analytics-section-view", z);
export {
  z as EkomAnalyticsSectionViewElement,
  z as default
};
