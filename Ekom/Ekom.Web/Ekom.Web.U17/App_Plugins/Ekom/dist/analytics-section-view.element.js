var E = Object.defineProperty;
var F = (l, n, t) => n in l ? E(l, n, { enumerable: !0, configurable: !0, writable: !0, value: t }) : l[n] = t;
var h = (l, n, t) => F(l, typeof n != "symbol" ? n + "" : n, t);
import { E as O, m, s as q, e as L, f as D, r as R, a as N, b as U, c as p, d as V, g as j, h as z, p as I, i as B, j as Z, k as _ } from "./manager-shared.js";
import { UmbElementMixin as H } from "@umbraco-cms/backoffice/element-api";
const C = 864e5, $ = (l) => l.toISOString().slice(0, 10), x = (l) => /* @__PURE__ */ new Date(`${l}T00:00:00Z`);
function w(l, n) {
  const t = x(l), e = x(n), s = Math.round((e.getTime() - t.getTime()) / C) + 1;
  return { dateFrom: $(new Date(t.getTime() - s * C)), dateTo: $(new Date(t.getTime() - C)) };
}
function M(l, n = /* @__PURE__ */ new Date()) {
  const t = new Date(Date.UTC(n.getFullYear(), n.getMonth(), n.getDate())), e = new Date(t);
  return l === "7" && e.setUTCDate(e.getUTCDate() - 6), l === "30" && e.setUTCDate(e.getUTCDate() - 29), l === "month" && e.setUTCDate(1), l === "year" && (e.setUTCMonth(0), e.setUTCDate(1)), { dateFrom: $(e), dateTo: $(t) };
}
function G(l, n, t, e = /* @__PURE__ */ new Date()) {
  if (t === "") return "";
  const s = ["today", "7", "30", "month", "year"];
  return t && s.includes(t) && s.unshift(t), s.find((a) => {
    const o = M(a, e);
    return o.dateFrom === l && o.dateTo === n;
  }) || "";
}
function J(l, n, t) {
  var o, i, r, b, v;
  const e = [], s = l.ordersChart.points;
  let a = 0;
  for (; a < s.length; ) {
    const u = a, c = s[a].x, d = x(c);
    t === "month" ? (d.setUTCDate(1), d.setUTCMonth(d.getUTCMonth() + 1)) : t === "week" ? d.setUTCDate(d.getUTCDate() + (7 - (d.getUTCDay() + 6) % 7)) : d.setUTCDate(d.getUTCDate() + 1);
    let g = 0, y = 0, k = 0, f = 0;
    do
      g += Number(((o = l.revenueChart.points[a]) == null ? void 0 : o.y) || 0), y += Number(s[a].y || 0), k += Number(((i = n == null ? void 0 : n.revenueChart.points[a]) == null ? void 0 : i.y) || 0), f += Number(((r = n == null ? void 0 : n.ordersChart.points[a]) == null ? void 0 : r.y) || 0), a++;
    while (a < s.length && x(s[a].x).getTime() < d.getTime());
    e.push({
      start: c,
      end: s[a - 1].x,
      amount: g,
      orders: y,
      average: y ? g / y : null,
      ...n ? {
        previousStart: (b = n.ordersChart.points[u]) == null ? void 0 : b.x,
        previousEnd: (v = n.ordersChart.points[a - 1]) == null ? void 0 : v.x,
        previousAmount: k,
        previousOrders: f,
        previousAverage: f ? k / f : null
      } : {}
    });
  }
  return e;
}
function A(l) {
  const n = l.revenueChart.points.reduce((e, s) => e + Number(s.y || 0), 0), t = l.ordersChart.points.reduce((e, s) => e + Number(s.y || 0), 0);
  return { amount: n, orders: t, average: t ? n / t : null };
}
function T(l, n) {
  if (l == null || n == null) return "No comparison available";
  if (n === 0) return l === 0 ? "No change" : "No prior value";
  const t = (l - n) / Math.abs(n) * 100;
  return `${t > 0 ? "+" : ""}${t.toLocaleString(void 0, { maximumFractionDigits: 1 })}%`;
}
class W extends H(HTMLElement) {
  constructor() {
    super(...arguments);
    h(this, "api", new O());
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
    h(this, "themeObserver");
    h(this, "themeLoaded", () => this.renderCharts());
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
    h(this, "pollingSeconds", 0);
    h(this, "pollingTimer", 0);
    h(this, "polling", !1);
  }
  setPolling(t) {
    [0, 2, 5, 10, 20, 30].includes(t) && (window.clearInterval(this.pollingTimer), this.pollingSeconds = t, t && (this.pollingTimer = window.setInterval(() => void this.pollAnalytics(), t * 1e3)), this.render());
  }
  async pollAnalytics() {
    if (!this.pollingSeconds || this.polling || this.disposed || document.hidden || this.loading || this.loadingMostSoldProducts || this.contains(document.activeElement) || this.querySelector('details[open], dialog[open], [role="dialog"]')) return;
    const t = { ...m.filters };
    if (!t.store) return;
    const e = this.chartRequest, s = this.productRequest, a = this.compare, o = this.productsVisible;
    this.polling = !0;
    try {
      const [i, r, b] = await Promise.all([
        this.api.charts(t),
        a ? this.api.charts({ ...t, ...w(t.dateFrom, t.dateTo) }) : Promise.resolve(void 0),
        o ? this.api.mostSoldProducts(t, this.pageMostSoldProducts, 20) : Promise.resolve(void 0)
      ]);
      if (this.disposed || !this.pollingSeconds || e !== this.chartRequest || s !== this.productRequest || a !== this.compare || o !== this.productsVisible || JSON.stringify(t) !== JSON.stringify(m.filters) || this.contains(document.activeElement) || this.querySelector('details[open], dialog[open], [role="dialog"]')) return;
      const v = b ? { ...b, products: b.products || [] } : this.mostSoldProducts, u = this.error || this.productError || JSON.stringify([i, r, v]) !== JSON.stringify([this.chartData, this.previousData, this.mostSoldProducts]);
      this.chartData = i, this.previousData = r, this.chartFilters = t, this.mostSoldProducts = v, this.error = "", this.productError = "", i.mixedCurrencies && (this.metric = "orders"), u && this.render();
      const c = this.querySelector(".ekom-polling summary");
      c == null || c.classList.add("just-refreshed"), c == null || c.addEventListener("animationend", (d) => {
        d.target === c && c.classList.remove("just-refreshed");
      }, { once: !0 });
    } catch {
    } finally {
      this.polling = !1;
    }
  }
  connectedCallback() {
    var t;
    if (super.connectedCallback(), this.hasAttribute("classic") || (t = this.consumeContext) == null || t.call(this, "umbThemeContext", (e) => {
      e && this.observe(e.theme, (s) => {
        this.toggleAttribute("data-ekom-dark", s === "umb-dark-theme");
      }, "ekom-manager-theme");
    }), this.disposed = !1, this.hasAttribute("classic"))
      for (const e of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const s = this.getAttribute(e.toLowerCase());
        s && (m.filters[e] = s);
      }
    this.render(), this.resizeObserver = new ResizeObserver(() => this.renderCharts()), this.resizeObserver.observe(this), typeof MutationObserver < "u" && (this.themeObserver = new MutationObserver(() => this.renderCharts()), this.themeObserver.observe(document.head, { childList: !0, subtree: !0, attributes: !0, attributeFilter: ["href"] }), document.head.addEventListener("load", this.themeLoaded, !0)), this.initialize();
  }
  disconnectedCallback() {
    var t, e, s, a;
    this.disposed = !0, window.clearInterval(this.pollingTimer), this.pollingSeconds = 0, this.chartRequest++, this.productRequest++, (t = this.chartAbort) == null || t.abort(), (e = this.productAbort) == null || e.abort(), (s = this.resizeObserver) == null || s.disconnect(), this.resizeObserver = void 0, (a = this.themeObserver) == null || a.disconnect(), typeof document < "u" && document.head.removeEventListener("load", this.themeLoaded, !0), super.disconnectedCallback();
  }
  get currency() {
    var t;
    return this.currencyFor(((t = this.chartFilters) == null ? void 0 : t.currency) || m.filters.currency);
  }
  currencyFor(t) {
    return q(m.stores.find((e) => {
      var s;
      return e.alias === (((s = this.chartFilters) == null ? void 0 : s.store) || m.filters.store);
    }), t);
  }
  async initialize() {
    var t;
    try {
      const [e, s, a] = await Promise.all([this.api.statusList(), this.api.stores(), this.hasAttribute("classic") ? Promise.resolve(!1) : this.api.printingEnabled()]);
      if (m.printingEnabled = a, this.disposed) return;
      m.statusList = e || [], m.stores = s || [], m.stores.some((o) => o.alias === m.filters.store) || (m.filters.store = ((t = m.stores[0]) == null ? void 0 : t.alias) || ""), this.hasAttribute("classic") || L(), await this.loadAnalytics();
    } catch (e) {
      if (this.disposed) return;
      this.error = P(e, "Unable to load Analytics."), this.loading = !1, this.render();
    }
  }
  async loadAnalytics() {
    await Promise.all([this.loadCharts(), this.productsVisible ? this.loadMostSoldProducts() : Promise.resolve()]);
  }
  async loadCharts() {
    var o;
    const t = ++this.chartRequest;
    (o = this.chartAbort) == null || o.abort();
    const e = this.chartAbort = new AbortController(), s = { ...m.filters };
    s.store || (this.chartData = void 0, this.previousData = void 0), this.error = "", this.loading = !!s.store;
    const a = this.querySelector(".analytics");
    if (a && s.store ? a.setAttribute("aria-busy", "true") : this.render(), !!s.store)
      try {
        const i = this.compare ? { ...s, ...w(s.dateFrom, s.dateTo) } : void 0, [r, b] = await Promise.all([
          this.api.charts(s, e.signal),
          i ? this.api.charts(i, e.signal) : Promise.resolve(void 0)
        ]);
        if (t !== this.chartRequest || this.disposed) return;
        this.chartData = r, this.previousData = b, this.chartFilters = s, r.mixedCurrencies && (this.metric = "orders");
      } catch (i) {
        if (t !== this.chartRequest || this.disposed) return;
        this.chartData = void 0, this.previousData = void 0, this.error = P(i, "Unable to load charts.");
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
        t === this.productRequest && !this.disposed && (this.productError = P(o, "Unable to load products."));
      } finally {
        t === this.productRequest && !this.disposed && (this.loadingMostSoldProducts = !1, this.render());
      }
  }
  get buckets() {
    return this.chartData ? J(this.chartData, this.previousData, this.grouping) : [];
  }
  get comparisonMoneyAvailable() {
    var t, e;
    return !!this.previousData && !((t = this.chartData) != null && t.mixedCurrencies) && !this.previousData.mixedCurrencies && (!((e = this.chartData) != null && e.currency) || !this.previousData.currency || this.chartData.currency === this.previousData.currency);
  }
  amount(t) {
    var s, a, o, i, r;
    if (t == null || (s = this.chartData) != null && s.mixedCurrencies) return "—";
    const e = ((a = this.chartData) == null ? void 0 : a.currency) || ((o = this.previousData) == null ? void 0 : o.currency);
    return !e || e === ((i = this.currency) == null ? void 0 : i.currencyValue) || e === ((r = this.currency) == null ? void 0 : r.isoCurrencySymbol) ? D(t, this.currency) : /^[A-Z]{3}$/.test(e) ? D(t, this.currencyFor(e)) : `${t.toLocaleString(void 0, { maximumFractionDigits: 2 })} ${e}`;
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
    const t = m.filters, e = G(t.dateFrom, t.dateTo, this.datePreset);
    R(this, `<style>${N}${this.hasAttribute("classic") ? `
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
    ` : ""}</style>${this.hasAttribute("classic") ? "" : '<link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css"><link rel="stylesheet" href="/App_Plugins/Ekom/dist/analytics-layout.css">'}<section class="ekmManager analytics ekom-design" aria-busy="${this.loading}"><div class="ekmManager__body">${this.hasAttribute("classic") ? "" : U("analytics")}
      <div class="analytics-toolbar ekom-shared-filters">
        ${this.hasAttribute("classic") ? `<label>Store<select data-field="store">${m.stores.map((s) => `<option value="${p(s.alias)}" ${s.alias === t.store ? "selected" : ""}>${p(s.title)}</option>`).join("")}</select></label>` : ""}
        ${this.hasAttribute("classic") ? `<label>Order view<select data-field="orderStatus">${V(t.orderStatus)}</select></label>` : j()}
        ${this.hasAttribute("classic") ? `        <label>Date From<input type="date" data-field="dateFrom" value="${p(t.dateFrom)}"></label>
        <label>Date To<input type="date" data-field="dateTo" value="${p(t.dateTo)}"></label>
        <label>Date preset<select data-preset><option value="">Custom range</option>${[["today", "Today"], ["7", "Last 7 days"], ["30", "Last 30 days"], ["month", "This month"], ["year", "This year"]].map(([s, a]) => `<option value="${s}" ${e === s ? "selected" : ""}>${a}</option>`).join("")}</select></label>
` : z()}
        ${this.hasAttribute("classic") ? `<label><span>Comparison</span><span><input type="checkbox" data-compare ${this.compare ? "checked" : ""}> Previous period</span></label><button data-refresh>Refresh</button>` : ""}
      </div>
      ${this.hasAttribute("classic") ? "" : `<div class="analytics-comparison-actions">
        <label class="analytics-compare"><input type="checkbox" data-compare ${this.compare ? "checked" : ""}><span>Compare to previous period</span></label>
        <details class="ekom-polling${this.pollingSeconds ? " is-active" : ""}"><summary aria-label="Analytics polling"><svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-3-6.7"></path><path d="M21 3v6h-6"></path></svg><span>${this.pollingSeconds ? `Polling every ${this.pollingSeconds}s` : "Polling off"}</span><span aria-hidden="true">⌄</span></summary><div class="ekom-poll-options" role="group" aria-label="Polling interval">${[0, 2, 5, 10, 20, 30].map((s) => `<button type="button" data-poll-seconds="${s}" aria-pressed="${s === this.pollingSeconds}"><span aria-hidden="true">•</span>${s ? `Every ${s} seconds` : "Off"}</button>`).join("")}</div></details>
      </div>`}
      ${this.error ? `<p role="alert">${p(this.error)}</p>` : ""}
      ${this.loading ? '<p role="status">Loading analytics…</p>' : t.store ? "" : "<p>No accessible stores.</p>"}
      ${this.chartData ? this.renderAnalytics() + this.renderCustomerInsights() : ""}
      <div class="card analytics-main analytics-products"><div class="analytics-toolbar"><h3>Most Sold Products</h3><button data-products>${this.productsVisible ? "Hide products" : "Load product breakdown"}</button></div>
        <p class="analytics-muted">Fetch the most sold products for the selected store and dates. This may take a minute for larger date ranges. Totals include VAT and discounts, excluding shipping and payment fees.</p>
        ${this.productsVisible ? `${this.productError ? `<p role="alert">${p(this.productError)}</p><button data-product-retry>Retry products</button>` : ""}${this.loadingMostSoldProducts ? '<p role="status"><span class="analytics-spinner" aria-hidden="true"></span>Fetching product breakdown… This may take a minute. You can continue using Analytics.</p>' : this.renderMostSoldProducts()}` : ""}
      </div>
    </div></section>`), this.bindEvents(), this.renderCharts();
  }
  displayRange(t, e) {
    return !t || !e ? "" : new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" }).formatRange(/* @__PURE__ */ new Date(`${t.slice(0, 10)}T00:00:00Z`), /* @__PURE__ */ new Date(`${e.slice(0, 10)}T00:00:00Z`));
  }
  renderAnalytics() {
    var s, a, o, i;
    const t = A(this.chartData), e = this.previousData ? A(this.previousData) : void 0;
    return `<div class="analytics-summary">${["amount", "orders", "average"].map((r) => `<div class="card"><span>${this.label(r)}</span><strong>${p(this.value(t[r], r))}</strong><span class="analytics-muted">${e && (r === "orders" || this.comparisonMoneyAvailable) ? p(T(t[r], e[r])) + " vs previous period" : this.compare ? "No monetary comparison available" : "Selected period"}</span></div>`).join("")}</div>
      <p class="analytics-muted">Order amount is the recorded charged amount, after gift cards; it is not refund-adjusted revenue. Dates use ${this.chartData.dateBasis === "PaidDate" ? "payment date" : "order creation date"}, matching Orders.</p>
      ${this.chartData.mixedCurrencies ? '<p role="status">This period contains multiple currencies. Monetary totals are hidden to avoid combining different currencies.</p>' : ""}
      ${this.previousData && !this.comparisonMoneyAvailable ? '<p class="analytics-muted">Monetary comparison is unavailable across different or mixed currencies.</p>' : ""}
      <div class="card analytics-main"><div class="analytics-toolbar">
        <div class="analytics-metrics">${["amount", "orders", "average"].map((r) => `<button data-metric="${r}" aria-pressed="${this.metric === r}" ${r !== "orders" && this.chartData.mixedCurrencies ? "disabled" : ""}>${this.label(r)}</button>`).join("")}</div>
        <label class="analytics-grouping">Group by<select data-grouping>${[["day", "Daily"], ["week", "Weekly"], ["month", "Monthly"]].map(([r, b]) => `<option value="${r}" ${this.grouping === r ? "selected" : ""}>${b}</option>`).join("")}</select></label>
        <button data-table aria-pressed="${this.table}">${this.table ? "Show chart" : "Show data table"}</button><button data-csv>Download CSV</button>
      </div>
      <p class="analytics-muted">${this.previousData ? `Solid: ${p(this.displayRange((s = this.chartFilters) == null ? void 0 : s.dateFrom, (a = this.chartFilters) == null ? void 0 : a.dateTo))}. Dashed: ${p(this.displayRange((o = this.previousData.ordersChart.points[0]) == null ? void 0 : o.x, (i = this.previousData.ordersChart.points.at(-1)) == null ? void 0 : i.x))}. Comparison buckets cover the same number of days.` : "Selected period."} Grouped ${this.grouping === "month" ? "monthly" : this.grouping === "week" ? "weekly" : "daily"}.</p>
      ${this.table ? this.renderTable() : `<div class="analytics-chart-frame" style="height:${this.hasAttribute("classic") ? 360 : 266}px"><canvas id="analytics-chart" style="width:100%;height:100%" role="img" aria-label="${this.label(this.metric)} over time. Use Show data table for exact values and order links."></canvas></div><div class="analytics-legend"><span><i></i>This period</span>${this.previousData ? '<span><i class="previous"></i>Previous period</span>' : ""}<span>Click a bucket to open its orders</span></div>`}
      </div>`;
  }
  renderCustomerInsights() {
    var b, v, u;
    const t = (b = this.chartData) == null ? void 0 : b.customerInsights;
    if (!t) return "";
    const e = (v = this.previousData) == null ? void 0 : v.customerInsights, s = [
      ["Unique purchasing customers", t.uniqueCustomers, e == null ? void 0 : e.uniqueCustomers, !1],
      ["First-time buyers", t.newCustomers, e == null ? void 0 : e.newCustomers, !1],
      ["Returning customer rate", t.returningCustomerRate, e == null ? void 0 : e.returningCustomerRate, !0]
    ], a = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"], o = a.map((c, d) => t.orderTimes.filter((g) => g.weekday === d).reduce((g, y) => g + y.orders, 0)), i = Array.from({ length: 24 }, (c, d) => t.orderTimes.filter((g) => g.hour === d).reduce((g, y) => g + y.orders, 0)), r = (c, d) => c.map((g, y) => `<div class="analytics-time-row" tabindex="0" data-tooltip="${g}: ${d[y]} orders" aria-label="${g}: ${d[y]} orders"><span>${g}</span><div class="analytics-time-track" aria-hidden="true"><span style="width:${100 * d[y] / Math.max(1, ...d)}%"></span></div><span>${d[y]}</span></div>`).join("");
    return `<div class="analytics-summary">${s.map(([c, d, g, y]) => `<div class="card"><span>${c}</span><strong>${d == null ? "—" : p(d.toLocaleString(void 0, { maximumFractionDigits: y ? 1 : 0 })) + (y ? "%" : "")}</strong><span class="analytics-muted">${e ? y ? d == null || g == null ? "No comparison available" : `${(d - g).toLocaleString(void 0, { maximumFractionDigits: 1, signDisplay: "always" })} percentage points vs previous period` : `${T(d, g ?? null)} vs previous period` : "Selected period"}</span></div>`).join("")}</div>
      <div class="card analytics-main analytics-purchases"><h3>New and returning purchases</h3>
        <div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Purchase type</th><th>Buyers</th><th>Orders</th><th>Order amount</th></tr></thead><tbody>
          <tr><td>First purchase</td><td>${t.newCustomers}</td><td>${t.firstOrders}</td><td>${p(this.amount(t.firstOrderAmount))}</td></tr>
          <tr><td>Repeat purchase</td><td>${t.returningCustomers}</td><td>${t.repeatOrders}</td><td>${p(this.amount(t.repeatOrderAmount))}</td></tr>
        </tbody></table></div>
      </div>
      <div class="analytics-insight-grid">
        <div class="card analytics-main"><div class="analytics-chart-heading"><h3>Orders by weekday</h3><span>By creation day</span></div><div class="analytics-time-bars">${r(a, o)}</div></div>
        ${(u = this.hasAttribute) != null && u.call(this, "classic") ? `<div class="card analytics-main"><h3>Orders by hour</h3><p class="analytics-muted">Recorded creation time, 00–23. No timezone conversion. Totals are counts, not averages per hour.</p><div class="analytics-time-bars analytics-hour-bars">${r(i.map((c, d) => String(d).padStart(2, "0") + ":00"), i)}</div></div>` : `<div class="card analytics-main"><div class="analytics-chart-heading"><h3>Orders by hour</h3><span>${Math.max(...i) > 0 ? `Peak ${String(i.indexOf(Math.max(...i))).padStart(2, "0")}:00` : "No orders"}</span></div><div class="analytics-hour-columns">${i.map((c, d) => `<div class="analytics-hour-column" tabindex="0" data-tooltip="${String(d).padStart(2, "0")}:00: ${c} orders" aria-label="${String(d).padStart(2, "0")}:00: ${c} orders"><span class="analytics-hour-value">${c || ""}</span><span class="analytics-hour-bar" style="height:${130 * c / Math.max(1, ...i)}px"></span><span class="analytics-hour-label">${d % 3 === 0 ? String(d).padStart(2, "0") : ""}</span></div>`).join("")}</div></div>`}

      </div>`;
  }
  renderTable() {
    return `<div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Period</th><th>Orders</th><th>Order amount</th><th>Average order</th>${this.previousData ? `<th>Previous period</th><th>Previous ${this.label(this.metric).toLowerCase()}</th>` : ""}<th>Details</th></tr></thead><tbody>${this.buckets.map((t, e) => `<tr><td>${p(this.range(t.start, t.end))}</td><td>${t.orders}</td><td>${p(this.amount(t.amount))}</td><td>${p(this.amount(t.average))}</td>${this.previousData ? `<td>${p(this.range(t.previousStart, t.previousEnd))}</td><td>${p(this.metric === "orders" || this.comparisonMoneyAvailable ? this.value(this.previousValue(t)) : "—")}</td>` : ""}<td><button data-orders="${e}">Open orders</button></td></tr>`).join("")}</tbody></table></div>`;
  }
  renderMostSoldProducts() {
    return this.mostSoldProducts.products.length ? `
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell">Product</div><div class="umb-table-cell">Sku</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Total</div></div></div>
        <div class="umb-table-body">
          ${this.mostSoldProducts.products.map((t) => `<div class="umb-table-row"><div class="umb-table-cell">${p(S(t, "title", "productTitle", "name"))}</div><div class="umb-table-cell">${p(S(t, "sku", "productSku"))}</div><div class="umb-table-cell">${p(S(t, "productCount", "quantity", "count"))}</div><div class="umb-table-cell">${p(D(Number(S(t, "total")), this.currency))}</div></div>`).join("")}
        </div>
      </div>
      ${this.mostSoldProducts.totalPages > 1 ? this.renderMostSoldProductsPagination() : ""}
    ` : "<p>No products found.</p>";
  }
  renderMostSoldProductsPagination() {
    return `
      <div class="pagination">
        <ul>
          ${I(this.pageMostSoldProducts, this.mostSoldProducts.totalPages).map((t) => {
      const e = Number(String(t).replace("...", ""));
      return `<li class="${e === this.pageMostSoldProducts ? "active" : ""}"><button type="button" data-action="set-most-sold-page" data-page="${e}" ${e === this.pageMostSoldProducts ? "disabled" : ""}>${p(t)}</button></li>`;
    }).join("")}
        </ul>
      </div>
    `;
  }
  renderCharts() {
    var i;
    const t = this.querySelector("#analytics-chart");
    if (!t || !this.chartData || this.table) return;
    const e = this.buckets, s = this.chartData.currency || ((i = this.previousData) == null ? void 0 : i.currency), a = s && /^[A-Z]{3}$/.test(s) ? this.currencyFor(s) : this.currency, o = this.previousData && (this.metric === "orders" || this.comparisonMoneyAvailable) ? { labels: [], points: e.map((r) => ({ x: this.range(r.previousStart, r.previousEnd), y: this.previousValue(r) })) } : void 0;
    B(t, { labels: [], points: e.map((r) => ({ x: this.range(r.start, r.end), y: r[this.metric] })) }, getComputedStyle(t).getPropertyValue("--ekom-accent").trim() || "#10694a", {
      templateStyle: !this.hasAttribute("classic"),
      formatAxisValue: (r) => r.toLocaleString(void 0, { notation: "compact", maximumFractionDigits: 1 }).replace("K", "k"),
      formatLabel: (r) => (/* @__PURE__ */ new Date(`${e[r].start}T00:00:00Z`)).toLocaleDateString("en-GB", { timeZone: "UTC", month: "short", ...this.grouping === "month" ? {} : { day: "numeric" } }),
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
      const a = new URLSearchParams({ analyticsDrill: "1", store: s.store, currency: s.currency || "", start: s.dateFrom, end: s.dateTo, orderStatus: s.orderStatus }), o = document.createElement("a");
      o.href = location.pathname.replace(/\/analytics\/?$/, "/orders") + "?" + a, this.appendChild(o), o.click(), o.remove();
    }
  }
  exportCsv() {
    var s, a, o;
    const t = [["From", "To", "Orders", "Order amount", "Average order", "Currency", "Previous from", "Previous to", "Previous orders", "Previous amount", "Previous average"]];
    for (const i of this.buckets) t.push([i.start, i.end, String(i.orders), (s = this.chartData) != null && s.mixedCurrencies ? "" : String(i.amount), (a = this.chartData) != null && a.mixedCurrencies || i.average == null ? "" : String(i.average), ((o = this.chartData) == null ? void 0 : o.currency) || "", i.previousStart || "", i.previousEnd || "", i.previousOrders == null ? "" : String(i.previousOrders), this.comparisonMoneyAvailable ? String(i.previousAmount ?? "") : "", this.comparisonMoneyAvailable ? String(i.previousAverage ?? "") : ""]);
    const e = t.map((i) => i.map((r) => '"' + r.replaceAll('"', '""') + '"').join(",")).join(`\r
`);
    Z(new Blob(["\uFEFF", e], { type: "text/csv;charset=utf-8" }), "analytics.csv");
  }
  bindEvents() {
    var t, e, s, a, o, i, r, b, v;
    this.hasAttribute("classic") || _(this, () => {
      this.datePreset = void 0, this.changedFilters();
    }), this.querySelectorAll("[data-field]").forEach((u) => u.addEventListener("change", () => {
      const c = u.dataset.field;
      (c === "dateFrom" || c === "dateTo") && (this.datePreset = void 0), m.filters[c] = u.value, this.changedFilters();
    })), (t = this.querySelector("[data-preset]")) == null || t.addEventListener("change", (u) => {
      const c = u.target.value;
      this.datePreset = c, c ? (Object.assign(m.filters, M(c)), this.changedFilters()) : this.render();
    }), (e = this.querySelector("[data-compare]")) == null || e.addEventListener("change", () => {
      this.compare = !this.compare, this.loadCharts();
    }), (s = this.querySelector("[data-grouping]")) == null || s.addEventListener("change", (u) => {
      this.grouping = u.target.value, this.render();
    }), this.querySelectorAll("[data-metric]").forEach((u) => u.addEventListener("click", () => {
      this.metric = u.dataset.metric, this.render();
    })), (a = this.querySelector("[data-table]")) == null || a.addEventListener("click", () => {
      this.table = !this.table, this.render();
    }), (o = this.querySelector("[data-csv]")) == null || o.addEventListener("click", () => this.exportCsv()), this.querySelectorAll("[data-poll-seconds]").forEach((u) => u.addEventListener("click", () => this.setPolling(Number(u.dataset.pollSeconds)))), (i = this.querySelector(".ekom-polling")) == null || i.addEventListener("keydown", (u) => {
      var d;
      if (u.key !== "Escape") return;
      const c = u.currentTarget;
      c.open = !1, (d = c.querySelector("summary")) == null || d.focus(), u.stopPropagation();
    }), (r = this.querySelector("[data-refresh]")) == null || r.addEventListener("click", () => void this.loadAnalytics()), this.querySelectorAll("[data-orders]").forEach((u) => u.addEventListener("click", () => this.openOrders(Number(u.dataset.orders)))), (b = this.querySelector("[data-products]")) == null || b.addEventListener("click", () => {
      var u;
      this.productsVisible = !this.productsVisible, this.productsVisible ? this.loadMostSoldProducts() : (this.productRequest++, (u = this.productAbort) == null || u.abort(), this.render());
    }), (v = this.querySelector("[data-product-retry]")) == null || v.addEventListener("click", () => void this.loadMostSoldProducts()), this.querySelectorAll('[data-action="set-most-sold-page"]').forEach((u) => u.addEventListener("click", () => {
      this.pageMostSoldProducts = Number(u.dataset.page), this.loadMostSoldProducts();
    }));
  }
}
function S(l, ...n) {
  for (const t of n) if (l[t] != null) return l[t];
  return "";
}
function P(l, n) {
  return l instanceof Error ? l.message : n;
}
customElements.define("ekom-analytics-section-view", W);
export {
  W as EkomAnalyticsSectionViewElement,
  W as default
};
