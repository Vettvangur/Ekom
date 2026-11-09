var T = Object.defineProperty;
var M = (o, i, t) => i in o ? T(o, i, { enumerable: !0, configurable: !0, writable: !0, value: t }) : o[i] = t;
var u = (o, i, t) => M(o, typeof i != "symbol" ? i + "" : i, t);
import { UmbElementMixin as E } from "@umbraco-cms/backoffice/element-api";
import { E as O, m as h, f as C, a as q, e as l, g as A, p as L, r as F, d as U } from "./manager-shared.js";
const D = 864e5, f = (o) => o.toISOString().slice(0, 10), x = (o) => /* @__PURE__ */ new Date(`${o}T00:00:00Z`);
function R(o, i) {
  const t = x(o), e = x(i), a = Math.round((e.getTime() - t.getTime()) / D) + 1;
  return { dateFrom: f(new Date(t.getTime() - a * D)), dateTo: f(new Date(t.getTime() - D)) };
}
function N(o, i = /* @__PURE__ */ new Date()) {
  const t = new Date(Date.UTC(i.getFullYear(), i.getMonth(), i.getDate())), e = new Date(t);
  return o === "7" && e.setUTCDate(e.getUTCDate() - 6), o === "30" && e.setUTCDate(e.getUTCDate() - 29), o === "month" && e.setUTCDate(1), o === "year" && (e.setUTCMonth(0), e.setUTCDate(1)), { dateFrom: f(e), dateTo: f(t) };
}
function V(o, i, t) {
  var n, d, r, m, c;
  const e = [], a = o.ordersChart.points;
  let s = 0;
  for (; s < a.length; ) {
    const b = s, P = a[s].x, p = x(P);
    t === "month" ? (p.setUTCDate(1), p.setUTCMonth(p.getUTCMonth() + 1)) : t === "week" ? p.setUTCDate(p.getUTCDate() + (7 - (p.getUTCDay() + 6) % 7)) : p.setUTCDate(p.getUTCDate() + 1);
    let S = 0, v = 0, $ = 0, y = 0;
    do
      S += Number(((n = o.revenueChart.points[s]) == null ? void 0 : n.y) || 0), v += Number(a[s].y || 0), $ += Number(((d = i == null ? void 0 : i.revenueChart.points[s]) == null ? void 0 : d.y) || 0), y += Number(((r = i == null ? void 0 : i.ordersChart.points[s]) == null ? void 0 : r.y) || 0), s++;
    while (s < a.length && x(a[s].x).getTime() < p.getTime());
    e.push({
      start: P,
      end: a[s - 1].x,
      amount: S,
      orders: v,
      average: v ? S / v : null,
      ...i ? {
        previousStart: (m = i.ordersChart.points[b]) == null ? void 0 : m.x,
        previousEnd: (c = i.ordersChart.points[s - 1]) == null ? void 0 : c.x,
        previousAmount: $,
        previousOrders: y,
        previousAverage: y ? $ / y : null
      } : {}
    });
  }
  return e;
}
function w(o) {
  const i = o.revenueChart.points.reduce((e, a) => e + Number(a.y || 0), 0), t = o.ordersChart.points.reduce((e, a) => e + Number(a.y || 0), 0);
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
    u(this, "api", new O());
    u(this, "loading", !0);
    u(this, "loadingMostSoldProducts", !1);
    u(this, "error", "");
    u(this, "productError", "");
    u(this, "chartData");
    u(this, "previousData");
    u(this, "chartFilters");
    u(this, "mostSoldProducts", { products: [], count: 0, totalPages: 0, page: 1 });
    u(this, "pageMostSoldProducts", 1);
    u(this, "resizeObserver");
    u(this, "chartRequest", 0);
    u(this, "productRequest", 0);
    u(this, "chartAbort");
    u(this, "productAbort");
    u(this, "disposed", !1);
    u(this, "productsVisible", !1);
    u(this, "metric", "amount");
    u(this, "grouping", "day");
    u(this, "compare", !0);
    u(this, "table", !1);
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
    var t, e, a;
    this.disposed = !0, this.chartRequest++, this.productRequest++, (t = this.chartAbort) == null || t.abort(), (e = this.productAbort) == null || e.abort(), (a = this.resizeObserver) == null || a.disconnect(), this.resizeObserver = void 0, super.disconnectedCallback();
  }
  get currency() {
    var t;
    return (t = h.stores.find((e) => {
      var a;
      return e.alias === (((a = this.chartFilters) == null ? void 0 : a.store) || h.filters.store);
    })) == null ? void 0 : t.currency;
  }
  async initialize() {
    var t;
    try {
      const [e, a] = await Promise.all([this.api.statusList(), this.api.stores()]);
      if (this.disposed) return;
      h.statusList = e || [], h.stores = a || [], h.stores.some((s) => s.alias === h.filters.store) || (h.filters.store = ((t = h.stores[0]) == null ? void 0 : t.alias) || ""), await this.loadAnalytics();
    } catch (e) {
      if (this.disposed) return;
      this.error = k(e, "Unable to load Analytics."), this.loading = !1, this.render();
    }
  }
  async loadAnalytics() {
    await Promise.all([this.loadCharts(), this.productsVisible ? this.loadMostSoldProducts() : Promise.resolve()]);
  }
  async loadCharts() {
    var s;
    const t = ++this.chartRequest;
    (s = this.chartAbort) == null || s.abort();
    const e = this.chartAbort = new AbortController(), a = { ...h.filters };
    if (this.chartData = void 0, this.previousData = void 0, this.error = "", this.loading = !!a.store, this.render(), !!a.store)
      try {
        const n = this.compare ? { ...a, ...R(a.dateFrom, a.dateTo) } : void 0, [d, r] = await Promise.all([
          this.api.charts(a, e.signal),
          n ? this.api.charts(n, e.signal) : Promise.resolve(void 0)
        ]);
        if (t !== this.chartRequest || this.disposed) return;
        this.chartData = d, this.previousData = r, this.chartFilters = a, d.mixedCurrencies && (this.metric = "orders");
      } catch (n) {
        if (t !== this.chartRequest || this.disposed) return;
        this.error = k(n, "Unable to load charts.");
      } finally {
        t === this.chartRequest && !this.disposed && (this.loading = !1, this.render());
      }
  }
  async loadMostSoldProducts() {
    var s;
    const t = ++this.productRequest;
    (s = this.productAbort) == null || s.abort();
    const e = this.productAbort = new AbortController(), a = { ...h.filters };
    if (this.mostSoldProducts = { products: [], count: 0, totalPages: 0, page: this.pageMostSoldProducts }, this.productError = "", this.loadingMostSoldProducts = !!a.store, this.render(), !!a.store)
      try {
        const n = await this.api.mostSoldProducts(a, this.pageMostSoldProducts, 20, e.signal);
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
    var a, s, n, d, r;
    if (t == null || (a = this.chartData) != null && a.mixedCurrencies) return "—";
    const e = ((s = this.chartData) == null ? void 0 : s.currency) || ((n = this.previousData) == null ? void 0 : n.currency);
    return !e || e === ((d = this.currency) == null ? void 0 : d.currencyValue) || e === ((r = this.currency) == null ? void 0 : r.isoCurrencySymbol) ? C(t, this.currency) : /^[A-Z]{3}$/.test(e) ? C(t, { isoCurrencySymbol: e }) : `${t.toLocaleString(void 0, { maximumFractionDigits: 2 })} ${e}`;
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
      .analytics-product-prompt {display:flex;flex-wrap:wrap;align-items:center;gap:20px;padding:24px;margin-top:20px;background:var(--uui-color-background,#f3f4f7);border:1px solid #e4e7ec;border-radius:8px}
      .analytics-product-prompt > div {flex:1;min-width:200px}
      .analytics-product-prompt h4 {margin:0 0 8px;font-size:15px}
      .analytics-product-prompt p {margin:4px 0;line-height:1.5}
      .analytics-products button {font:inherit;padding:10px 16px;border:1px solid #dfe2e8;border-radius:6px;background:var(--uui-color-surface,#fff);color:inherit;cursor:pointer}
      .analytics-products .analytics-product-primary {background:#25765c;border-color:#25765c;color:#fff;font-weight:600}
      .analytics-products button:focus-visible {outline:2px solid #25765c;outline-offset:3px}
      .analytics-product-footer {display:flex;justify-content:flex-end;margin-top:18px}
      .analytics-variant-title {color:#646b7c;margin-left:8px}
      .analytics-spinner {display:inline-block;width:14px;height:14px;margin-right:8px;border:2px solid #dfe2e8;border-top-color:#25765c;border-radius:50%;animation:analytics-spin 1s linear infinite}
      @keyframes analytics-spin {to {transform:rotate(360deg)}}
      @media (prefers-reduced-motion:reduce) {.analytics-spinner {animation:none}}
    </style><section class="ekmManager analytics"><div class="ekmManager__body">
      <div class="analytics-toolbar">
        <label>Store<select data-field="store">${h.stores.map((e) => `<option value="${l(e.alias)}" ${e.alias === t.store ? "selected" : ""}>${l(e.title)}</option>`).join("")}</select></label>
        <label>Order status<select data-field="orderStatus"><option value="CompletedOrders" ${t.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option><option value="AllOrders" ${t.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>${h.statusList.map((e) => `<option value="${l(A(e))}" ${A(e) === t.orderStatus ? "selected" : ""}>${l(e.label)}</option>`).join("")}</select></label>
        <label>Date From<input type="date" data-field="dateFrom" value="${l(t.dateFrom)}"></label>
        <label>Date To<input type="date" data-field="dateTo" value="${l(t.dateTo)}"></label>
        <label>Date preset<select data-preset><option value="">Custom range</option>${[["today", "Today"], ["7", "Last 7 days"], ["30", "Last 30 days"], ["month", "This month"], ["year", "This year"]].map(([e, a]) => `<option value="${e}">${a}</option>`).join("")}</select></label>
        <label><span>Comparison</span><span><input type="checkbox" data-compare ${this.compare ? "checked" : ""}> Previous period</span></label>
        <button data-refresh>Refresh</button>
      </div>
      ${this.error ? `<p role="alert">${l(this.error)}</p>` : ""}
      ${this.loading ? '<p role="status">Loading analytics…</p>' : t.store ? "" : "<p>No accessible stores.</p>"}
      ${this.chartData ? this.renderAnalytics() : ""}
      <div class="card analytics-main analytics-products"><h3>Most Sold Products</h3>
        <p class="analytics-muted">Product and variant sales for the selected store and dates. Totals include VAT and discounts, excluding shipping and payment fees.</p>
        ${this.productsVisible ? this.loadingMostSoldProducts ? '<div class="analytics-product-prompt" role="status"><span class="analytics-spinner" aria-hidden="true"></span><div><h4>Loading product breakdown</h4><p>This may take a minute. You can continue using Analytics.</p></div></div>' : this.productError ? `<div class="analytics-product-prompt"><p role="alert">${l(this.productError)}</p><button type="button" class="analytics-product-primary" data-product-retry>Try again</button></div>` : this.renderMostSoldProducts() : `<div class="analytics-product-prompt"><div><h4>Explore your best sellers</h4><p>See quantities sold and order totals for each product and variant.</p><p class="analytics-muted">Larger date ranges may take a minute to load.</p></div><button type="button" class="analytics-product-primary" data-products ${t.store ? "" : "disabled"}>Load product breakdown</button></div>`}
        ${this.productsVisible ? '<div class="analytics-product-footer"><button type="button" data-products>Hide product breakdown</button></div>' : ""}
      </div>
    </div></section>`, this.bindEvents(), this.renderCharts();
  }
  renderAnalytics() {
    var a, s, n, d;
    const t = w(this.chartData), e = this.previousData ? w(this.previousData) : void 0;
    return `<div class="analytics-summary">${["amount", "orders", "average"].map((r) => `<div class="card"><span>${this.label(r)}</span><strong>${l(this.value(t[r], r))}</strong><span class="analytics-muted">${e && (r === "orders" || this.comparisonMoneyAvailable) ? l(j(t[r], e[r])) + " vs previous period" : this.compare ? "No monetary comparison available" : "Selected period"}</span></div>`).join("")}</div>
      <p class="analytics-muted">Order amount is the recorded charged amount, after gift cards; it is not refund-adjusted revenue. Dates use ${this.chartData.dateBasis === "PaidDate" ? "payment date" : "order creation date"}, matching Orders.</p>
      ${this.chartData.mixedCurrencies ? '<p role="status">This period contains multiple currencies. Monetary totals are hidden to avoid combining different currencies.</p>' : ""}
      ${this.previousData && !this.comparisonMoneyAvailable ? '<p class="analytics-muted">Monetary comparison is unavailable across different or mixed currencies.</p>' : ""}
      <div class="card analytics-main"><div class="analytics-toolbar">
        ${["amount", "orders", "average"].map((r) => `<button data-metric="${r}" aria-pressed="${this.metric === r}" ${r !== "orders" && this.chartData.mixedCurrencies ? "disabled" : ""}>${this.label(r)}</button>`).join("")}
        <label>Group by<select data-grouping>${[["day", "Daily"], ["week", "Weekly"], ["month", "Monthly"]].map(([r, m]) => `<option value="${r}" ${this.grouping === r ? "selected" : ""}>${m}</option>`).join("")}</select></label>
        <button data-table aria-pressed="${this.table}">${this.table ? "Show chart" : "Show data table"}</button><button data-csv>Download CSV</button>
      </div>
      <p class="analytics-muted">${this.previousData ? `Solid: ${l(this.range((a = this.chartFilters) == null ? void 0 : a.dateFrom, (s = this.chartFilters) == null ? void 0 : s.dateTo))}. Dashed: ${l(this.range((n = this.previousData.ordersChart.points[0]) == null ? void 0 : n.x, (d = this.previousData.ordersChart.points.at(-1)) == null ? void 0 : d.x))}. Comparison buckets cover the same number of days.` : "Selected period."} Click a bucket to open its orders.</p>
      ${this.table ? this.renderTable() : `<div style="height:360px"><canvas id="analytics-chart" style="width:100%;height:100%" role="img" aria-label="${this.label(this.metric)} over time. Use Show data table for exact values and order links."></canvas></div>`}
      </div>`;
  }
  renderTable() {
    return `<div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Period</th><th>Orders</th><th>Order amount</th><th>Average order</th>${this.previousData ? `<th>Previous period</th><th>Previous ${this.label(this.metric).toLowerCase()}</th>` : ""}<th>Details</th></tr></thead><tbody>${this.buckets.map((t, e) => `<tr><td>${l(this.range(t.start, t.end))}</td><td>${t.orders}</td><td>${l(this.amount(t.amount))}</td><td>${l(this.amount(t.average))}</td>${this.previousData ? `<td>${l(this.range(t.previousStart, t.previousEnd))}</td><td>${l(this.metric === "orders" || this.comparisonMoneyAvailable ? this.value(this.previousValue(t)) : "—")}</td>` : ""}<td><button data-orders="${e}">Open orders</button></td></tr>`).join("")}</tbody></table></div>`;
  }
  renderMostSoldProducts() {
    return this.mostSoldProducts.products.length ? `
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell">Product</div><div class="umb-table-cell">Sku</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Total</div></div></div>
        <div class="umb-table-body">
          ${this.mostSoldProducts.products.map((t) => `<div class="umb-table-row"><div class="umb-table-cell">${l(g(t, "title", "productTitle", "name"))}${t.variantTitle ? `<span class="analytics-variant-title">— ${l(t.variantTitle)}</span>` : ""}</div><div class="umb-table-cell">${l(g(t, "sku", "productSku"))}</div><div class="umb-table-cell">${l(g(t, "productCount", "quantity", "count"))}</div><div class="umb-table-cell">${l(C(Number(g(t, "total")), this.currency))}</div></div>`).join("")}
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
      return `<li class="${e === this.pageMostSoldProducts ? "active" : ""}"><button type="button" data-action="set-most-sold-page" data-page="${e}" ${e === this.pageMostSoldProducts ? "disabled" : ""}>${l(t)}</button></li>`;
    }).join("")}
        </ul>
      </div>
    `;
  }
  renderCharts() {
    var d;
    const t = this.querySelector("#analytics-chart");
    if (!t || !this.chartData || this.table) return;
    const e = this.buckets, a = this.chartData.currency || ((d = this.previousData) == null ? void 0 : d.currency), s = a && /^[A-Z]{3}$/.test(a) ? { isoCurrencySymbol: a } : this.currency, n = this.previousData && (this.metric === "orders" || this.comparisonMoneyAvailable) ? { labels: [], points: e.map((r) => ({ x: this.range(r.previousStart, r.previousEnd), y: this.previousValue(r) })) } : void 0;
    F(t, { labels: [], points: e.map((r) => ({ x: this.range(r.start, r.end), y: r[this.metric] })) }, "#25765c", {
      currency: this.metric === "orders" ? void 0 : s,
      integer: this.metric === "orders",
      comparison: n,
      formatValue: (r) => this.value(r),
      columnHover: !0,
      onSelect: (r) => this.openOrders(r)
    });
  }
  changedFilters() {
    this.pageMostSoldProducts = 1, this.dispatchEvent(new CustomEvent("analytics-filters-changed", { bubbles: !0, detail: { ...h.filters } })), this.loadAnalytics();
  }
  openOrders(t) {
    const e = this.buckets[t];
    if (!e || !this.chartFilters) return;
    const a = { ...this.chartFilters, dateFrom: e.start, dateTo: e.end };
    for (const s of ["query", "paymentProvider", "shippingProvider", "couponCode", "giftcardCode", "productSku", "trackingSource", "trackingMedium", "trackingCampaign", "trackingTerm", "trackingContent", "trackingClickId"]) a[s] = "";
    if (Object.assign(h.filters, a), this.hasAttribute("classic")) this.dispatchEvent(new CustomEvent("analytics-open-orders", { bubbles: !0, detail: a }));
    else {
      const s = new URLSearchParams({ analyticsDrill: "1", store: a.store, start: a.dateFrom, end: a.dateTo, orderStatus: a.orderStatus }), n = document.createElement("a");
      n.href = location.pathname.replace(/\/analytics\/?$/, "/orders") + "?" + s, this.appendChild(n), n.click(), n.remove();
    }
  }
  exportCsv() {
    var a, s, n;
    const t = [["From", "To", "Orders", "Order amount", "Average order", "Currency", "Previous from", "Previous to", "Previous orders", "Previous amount", "Previous average"]];
    for (const d of this.buckets) t.push([d.start, d.end, String(d.orders), (a = this.chartData) != null && a.mixedCurrencies ? "" : String(d.amount), (s = this.chartData) != null && s.mixedCurrencies || d.average == null ? "" : String(d.average), ((n = this.chartData) == null ? void 0 : n.currency) || "", d.previousStart || "", d.previousEnd || "", d.previousOrders == null ? "" : String(d.previousOrders), this.comparisonMoneyAvailable ? String(d.previousAmount ?? "") : "", this.comparisonMoneyAvailable ? String(d.previousAverage ?? "") : ""]);
    const e = t.map((d) => d.map((r) => '"' + r.replaceAll('"', '""') + '"').join(",")).join(`\r
`);
    U(new Blob(["\uFEFF", e], { type: "text/csv;charset=utf-8" }), "analytics.csv");
  }
  bindEvents() {
    var t, e, a, s, n, d, r, m;
    this.querySelectorAll("[data-field]").forEach((c) => c.addEventListener("change", () => {
      const b = c.dataset.field;
      h.filters[b] = c.value, this.changedFilters();
    })), (t = this.querySelector("[data-preset]")) == null || t.addEventListener("change", (c) => {
      const b = c.target.value;
      b && (Object.assign(h.filters, N(b)), this.changedFilters());
    }), (e = this.querySelector("[data-compare]")) == null || e.addEventListener("change", () => {
      this.compare = !this.compare, this.loadCharts();
    }), (a = this.querySelector("[data-grouping]")) == null || a.addEventListener("change", (c) => {
      this.grouping = c.target.value, this.render();
    }), this.querySelectorAll("[data-metric]").forEach((c) => c.addEventListener("click", () => {
      this.metric = c.dataset.metric, this.render();
    })), (s = this.querySelector("[data-table]")) == null || s.addEventListener("click", () => {
      this.table = !this.table, this.render();
    }), (n = this.querySelector("[data-csv]")) == null || n.addEventListener("click", () => this.exportCsv()), (d = this.querySelector("[data-refresh]")) == null || d.addEventListener("click", () => void this.loadAnalytics()), this.querySelectorAll("[data-orders]").forEach((c) => c.addEventListener("click", () => this.openOrders(Number(c.dataset.orders)))), (r = this.querySelector("[data-products]")) == null || r.addEventListener("click", () => {
      var c;
      this.productsVisible = !this.productsVisible, this.productsVisible ? this.loadMostSoldProducts() : (this.productRequest++, (c = this.productAbort) == null || c.abort(), this.render());
    }), (m = this.querySelector("[data-product-retry]")) == null || m.addEventListener("click", () => void this.loadMostSoldProducts()), this.querySelectorAll('[data-action="set-most-sold-page"]').forEach((c) => c.addEventListener("click", () => {
      this.pageMostSoldProducts = Number(c.dataset.page), this.loadMostSoldProducts();
    }));
  }
}
function g(o, ...i) {
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
