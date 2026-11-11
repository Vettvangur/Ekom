var x = Object.defineProperty;
var k = (h, c, t) => c in h ? x(h, c, { enumerable: !0, configurable: !0, writable: !0, value: t }) : h[c] = t;
var d = (h, c, t) => k(h, typeof c != "symbol" ? c + "" : c, t);
import { E as S, m as o, e as w, c as a, r as E, a as q, b as L, d as C, g as A, h as O, l as f, k as D } from "./manager-shared.js";
import { UmbElementMixin as R } from "@umbraco-cms/backoffice/element-api";
class P extends R(HTMLElement) {
  constructor() {
    super(...arguments);
    d(this, "api", new S());
    d(this, "result");
    d(this, "detail");
    d(this, "query", "");
    d(this, "segment", "all");
    d(this, "sort", "newest");
    d(this, "page", 1);
    d(this, "busy", !1);
    d(this, "error", "");
    d(this, "request", 0);
    d(this, "detailRequest", 0);
    d(this, "other", !1);
    d(this, "classic", !1);
    d(this, "opener");
    d(this, "dismissOnEscape", (t) => {
      !this.detail || t.key !== "Escape" || (t.preventDefault(), t.stopPropagation(), this.close());
    });
  }
  connectedCallback() {
    var t, s;
    if (super.connectedCallback(), this.hasAttribute("classic") || (t = this.consumeContext) == null || t.call(this, "umbThemeContext", (e) => {
      e && this.observe(e.theme, (i) => {
        this.toggleAttribute("data-ekom-dark", i === "umb-dark-theme");
      }, "ekom-manager-theme");
    }), (s = this.ownerDocument) == null || s.addEventListener("keydown", this.dismissOnEscape, !0), this.classic = this.hasAttribute("classic"), this.classic)
      for (const e of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const i = this.getAttribute(e.toLowerCase());
        i && (o.filters[e] = i);
      }
    this.initialize();
  }
  disconnectedCallback() {
    var t;
    (t = this.ownerDocument) == null || t.removeEventListener("keydown", this.dismissOnEscape, !0), this.request++, this.detailRequest++, super.disconnectedCallback();
  }
  async initialize() {
    var t;
    this.busy = !0, this.render();
    try {
      const [s, e] = await Promise.all([this.api.stores(), this.classic ? Promise.resolve(!1) : this.api.printingEnabled()]);
      o.stores = s, o.printingEnabled = e, o.stores.some((i) => i.alias === o.filters.store) || (o.filters.store = ((t = o.stores[0]) == null ? void 0 : t.alias) || ""), this.classic || w(), await this.load();
    } catch {
      this.error = "Unable to load customers. Please try again.", this.busy = !1, this.render();
    }
  }
  params() {
    const t = o.filters;
    return new URLSearchParams({ store: t.store, currency: t.currency || "", start: t.dateFrom, end: t.dateTo, orderStatus: t.orderStatus, query: this.query, segment: this.segment, sort: this.sort, page: String(this.page) });
  }
  async get(t, s) {
    const e = await fetch(`/ekom/manager/customers${t}?${s}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!e.ok) throw new Error("Unable to load customer data. Check the filters and try again.");
    return e.json();
  }
  async load() {
    const t = ++this.request;
    this.detailRequest++, this.detail = void 0, this.result = void 0, this.error = "", this.busy = !0;
    const s = this.querySelector(".customers");
    s && o.filters.store ? s.setAttribute("aria-busy", "true") : this.render();
    try {
      if (!o.filters.store) {
        this.result = void 0;
        return;
      }
      const e = await this.get("", this.params());
      t === this.request && (this.result = e);
    } catch (e) {
      t === this.request && (this.result = void 0, this.error = String(e));
    } finally {
      t === this.request && (this.busy = !1, this.render());
    }
  }
  async open(t, s = 1) {
    var i;
    const e = ++this.detailRequest;
    this.opener = t, this.error = "";
    try {
      const n = await this.get("/detail", new URLSearchParams({ store: o.filters.store, currency: o.filters.currency || "", identity: t, orderStatus: o.filters.orderStatus, page: String(s), other: String(this.other) }));
      if (e !== this.detailRequest || !this.isConnected) return;
      this.detail = n, this.render(), (i = this.querySelector("[data-close]")) == null || i.focus();
    } catch (n) {
      e === this.detailRequest && (this.error = String(n), this.render());
    }
  }
  close() {
    var t;
    this.detailRequest++, this.detail = void 0, this.render(), (t = Array.from(this.querySelectorAll("[data-customer]")).find((s) => s.dataset.customer === this.opener)) == null || t.focus();
  }
  async export() {
    this.busy = !0, this.render();
    try {
      const t = await fetch(`/ekom/manager/customers/export?${this.params()}`, { credentials: "same-origin" });
      if (!t.ok) throw new Error("Customer export failed.");
      const s = URL.createObjectURL(await t.blob()), e = document.createElement("a");
      e.href = s, e.download = "customers.csv", e.click(), setTimeout(() => URL.revokeObjectURL(s), 1e3);
    } catch (t) {
      this.error = String(t);
    } finally {
      this.busy = !1, this.render();
    }
  }
  amount(t, s) {
    return t.map((e) => {
      var u;
      const i = (u = o.stores.find((m) => m.alias === o.filters.store)) == null ? void 0 : u.currency;
      let n = e.currency;
      !/^[A-Z]{3}$/.test(n) && (i == null ? void 0 : i.currencyValue) === n && (n = i.isoCurrencySymbol || n);
      try {
        return a(new Intl.NumberFormat(void 0, { style: "currency", currency: n }).format(e[s]));
      } catch {
        return `${a(e[s].toLocaleString(void 0, { maximumFractionDigits: 2 }))} ${a(e.currency)}`;
      }
    }).join("<br>") || "—";
  }
  number(t) {
    return t == null ? "—" : a(t.toLocaleString(void 0, { maximumFractionDigits: 1 }));
  }
  date(t) {
    return t ? a(new Date(t).toLocaleDateString()) : "—";
  }
  avatar(t) {
    const s = t.trim().split(/\s+/).slice(0, 2).map((e) => Array.from(e)[0] || "").join("").toUpperCase();
    return this.classic ? "" : `<span class="customer-avatar" aria-hidden="true">${a(s)}</span>`;
  }
  card(t, s) {
    return `<div class="customer-card"><small>${t}</small><strong>${s}</strong></div>`;
  }
  render() {
    const t = o.filters, s = this.result;
    E(this, `<style>${q}${this.classic ? `
      ekom-customers-section-view { display:block; color:var(--uui-color-text,#20232e) }
      .customers { padding:24px; background:var(--uui-color-background,#f3f4f7); min-height:80vh }
      .customer-cards { display:grid;grid-template-columns:repeat(auto-fit,minmax(155px,1fr));gap:12px;margin:18px 0 }
      .customer-card,.customer-panel { background:var(--uui-color-surface,#fff);border:1px solid #dfe2e8;border-radius:10px;padding:18px }
      .customer-card small { display:block;color:#646b7c;font-size:12px;margin-bottom:8px }
      .customer-card strong { font-size:23px }
      .customer-toolbar { display:flex;flex-wrap:wrap;align-items:end;gap:12px }
      .customer-toolbar label { display:grid;gap:5px;font-size:12px }
      .customers input,.customers select,.customers button,.customer-dialog button { font:inherit;padding:8px 12px;border:1px solid #dfe2e8;border-radius:6px;background:var(--uui-color-surface,#fff);color:inherit }
      .customers button,.customer-dialog button { cursor:pointer }
      .customers button:disabled { opacity:.5;cursor:default }
      .customer-table-wrap { overflow:auto }
      .customer-table { border-collapse:collapse;width:100%;font-size:14px }
      .customer-table th,.customer-table td { text-align:left;padding:15px 10px;border-bottom:1px solid #e8e9ed;white-space:nowrap }
      .customer-table th { font-size:11px;text-transform:uppercase;letter-spacing:.07em;color:#70778a }
      .customer-table button[data-customer] { border:0;padding:0;text-align:left;background:none;font-weight:600 }
      .customer-muted { display:block;color:#70778a;font-size:12px;margin-top:5px }
      .customer-badge { background:#edf2f0;color:#237053;border-radius:10px;padding:3px 8px;font-size:11px;margin-left:8px }
      .customer-loading { display:flex;flex-direction:column;align-items:center;gap:12px;padding:48px 20px;text-align:center;white-space:normal }
      .customer-spinner { width:28px;height:28px;border:3px solid #dfe2e8;border-top-color:#237053;border-radius:50%;animation:customer-spin .8s linear infinite }
      @keyframes customer-spin { to { transform:rotate(360deg) } }
      @media(prefers-reduced-motion:reduce) { .customer-spinner { animation:none } }
      .customer-pagination { display:flex;gap:16px;align-items:center;justify-content:flex-end;padding-top:18px }
      .customer-overlay { position:fixed;inset:0;background:#0006;z-index:10000;display:flex;justify-content:flex-end }
      .customer-dialog { background:var(--uui-color-surface,#fff);width:min(1200px,95vw);height:100%;overflow:auto;padding:28px;box-sizing:border-box }
      .customer-heading { display:flex;justify-content:space-between;gap:16px;align-items:start }
      .customer-heading h2 { margin:0 0 8px }
      .customer-detail-grid { display:grid;grid-template-columns:minmax(0,2fr) minmax(230px,1fr);gap:24px }
      .customer-snapshot { background:var(--uui-color-background,#f3f4f7);padding:20px;border-radius:8px }
      .customer-snapshot dt { color:#70778a;font-size:12px;margin-top:15px }
      .customer-snapshot dd { margin:4px 0;overflow-wrap:anywhere }
      @media(max-width:800px) { .customer-detail-grid { grid-template-columns:1fr } .customers { padding:12px } }
    ` : ""}</style>${this.classic ? "" : '<link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css"><link rel="stylesheet" href="/App_Plugins/Ekom/dist/customers-layout.css?v=20261111-background">'}<section class="customers ekom-design" aria-busy="${this.busy}">
      ${this.classic ? "" : L("customers", "data-filter")}
      <div class="customer-toolbar ekom-shared-filters">
        ${this.classic ? `<label>Store<select data-filter="store">${o.stores.map((e) => `<option value="${a(e.alias)}" ${e.alias === t.store ? "selected" : ""}>${a(e.title)}</option>`).join("")}</select></label>` : ""}
        ${this.classic ? `<label>Order view<select data-filter="orderStatus">${C(t.orderStatus)}</select></label>` : A()}
        ${this.classic ? `        <label>Date From<input type="date" data-filter="dateFrom" value="${a(t.dateFrom)}"></label>
        <label>Date To<input type="date" data-filter="dateTo" value="${a(t.dateTo)}"></label>
` : O()}
        <button data-reload ${this.busy ? "disabled" : ""}>Refresh</button>
      </div>
      <p class="customer-muted">Customers who ordered in the selected period. Lifetime metrics include all qualifying orders in the selected store and currency. </p>
      ${this.error ? `<p role="alert">${a(this.error)}</p>` : ""}
      ${this.busy && s ? '<p role="status">Preparing export...</p>' : ""}
      ${s ? `<div class="customer-cards">${this.card("Customers", this.number(s.totals.customers))}${this.card("Returning", this.number(s.totals.returningPercent) + "%")}${this.card("Average lifetime spend", this.amount(s.spend, "averageLifetime"))}${this.card("Average orders per customer", this.number(s.totals.averageOrders))}${this.card("Average days between orders", this.number(s.totals.averageReorderDays))}</div>
      <p class="customer-muted">${s.missingIdentifiers} qualifying orders in this period have no ${s.identityField === "CustomerUsername" ? "username" : "email"} and are excluded. Spend uses recorded charged amounts, without refund adjustment.</p>` : ""}
      <div class="customer-panel"><div class="customer-toolbar" role="search">
        <label>Search<input name="query" value="${a(this.query)}" placeholder="Name, email or username"></label>
        <button type="button" data-search>Search</button>
        <nav class="ekom-customer-segments" aria-label="Customer segment">${["all", "new", "returning"].map((e) => `<button type="button" data-segment-value="${e}" aria-pressed="${e === this.segment}">${e[0].toUpperCase() + e.slice(1)}</button>`).join("")}</nav>
        <label>Sort by<select data-sort>${[["newest", "Newest first order"], ["oldest", "Oldest first order"], ["lastOrder", "Latest order"], ["orders", "Most orders"], ["name", "Name"]].map(([e, i]) => `<option value="${e}" ${e === this.sort ? "selected" : ""}>${i}</option>`).join("")}</select></label>
        <button type="button" data-export ${this.busy || !(s != null && s.totals.customers) ? "disabled" : ""}>Export</button>
      </div><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Customer</th><th>First order</th><th>Orders</th><th>Lifetime spend</th><th>Average order</th><th>Last order</th><th>Days since last order</th></tr></thead><tbody>
      ${((s == null ? void 0 : s.customers) || []).map((e) => `<tr><td><div class="customer-identity">${this.avatar(e.name || e.email || e.username || e.identity)}<button data-customer="${a(e.identity)}">${a(e.name || e.email || e.username || e.identity)}<span class="customer-badge">${e.orders > 1 ? "Returning" : "New"}</span><span class="customer-muted">${a(e.email || e.username || e.identity)}</span></button></div></td><td>${this.date(e.firstOrder)}</td><td>${e.orders}</td><td>${this.amount(e.spend, "total")}</td><td>${this.amount(e.spend, "averageOrder")}</td><td>${this.date(e.lastOrder)}${this.classic ? `<span class="customer-muted">${a(e.lastStatus || "")}</span>` : ""}</td><td>${e.daysSinceLastOrder}</td></tr>`).join("") || `<tr><td colspan="7">${this.busy ? '<div class="customer-loading" role="status"><span class="customer-spinner" aria-hidden="true"></span><strong>Loading customers...</strong><span class="customer-muted">Large datasets may take a moment.</span></div>' : s ? "No customers found." : this.error ? "Customer results could not be loaded." : "Select a store to see customers."}</td></tr>`}
      </tbody></table></div><div class="customer-pagination"><button data-page="${this.page - 1}" ${this.page <= 1 || this.busy ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${Math.max(1, (s == null ? void 0 : s.totalPages) || 0)}</span><button data-page="${this.page + 1}" ${this.page >= ((s == null ? void 0 : s.totalPages) || 0) || this.busy ? "disabled" : ""}>Next</button></div></div>
      ${this.detail ? this.renderDetail(this.detail) : ""}</section>`), this.bind();
  }
  renderDetail(t) {
    const s = t.customer;
    return `<div class="customer-overlay"><section role="dialog" aria-modal="true" aria-label="Customer details" class="customer-dialog"><header class="customer-heading">${this.avatar(s.name || s.identity)}<div><h2>${a(s.name || s.identity)} <span class="customer-badge">${s.orders > 1 ? "Returning" : "New"}</span></h2><p>${a([s.email, s.username].filter(Boolean).join(" · "))}</p></div><button data-close aria-label="Close customer details">${this.classic ? "Close" : "×"}</button></header>
      <div class="customer-cards">${this.card("Lifetime spend", this.amount(s.spend, "total"))}${this.card("Orders", String(s.orders))}${this.card("Average order", this.amount(s.spend, "averageOrder"))}${this.card("First order", this.date(s.firstOrder))}${this.card("Days since last order", String(s.daysSinceLastOrder))}${this.card("Average reorder interval (days)", this.number(s.averageReorderDays))}</div>
      <div class="customer-detail-grid"><div><h3>Orders <small>(${t.count})</small></h3><label><input type="checkbox" data-other ${this.other ? "checked" : ""}> Show other orders (excluded from lifetime metrics)</label><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Order</th>${this.classic ? "<th>Status</th>" : "<th>Order status</th><th>Payment</th><th>Fulfillment</th>"}<th>Created</th><th>Paid</th><th>Total</th></tr></thead><tbody>${t.orders.map((e) => {
      var i, n, u;
      return `<tr><td><button data-order="${a(e.uniqueId)}">#${a(e.orderNumber || String(e.referenceId))}</button></td>${this.classic ? `<td>${a(e.orderStatusCol)}</td>` : `<td>${f((i = e.status) == null ? void 0 : i.orderStatus)}</td><td>${f((n = e.status) == null ? void 0 : n.paymentStatus)}</td><td>${f((u = e.status) == null ? void 0 : u.fulfillmentStatus)}</td>`}<td>${this.date(e.createDate)}</td><td>${this.date(e.paidDate)}</td><td>${a(e.formattedTotal)}</td></tr>`;
    }).join("") || `<tr><td colspan="${this.classic ? 5 : 7}">No orders.</td></tr>`}</tbody></table></div><div class="customer-pagination"><button data-detail-page="${t.page - 1}" ${t.page <= 1 ? "disabled" : ""}>Previous</button><span>${t.page} / ${Math.max(1, t.totalPages)}</span><button data-detail-page="${t.page + 1}" ${t.page >= t.totalPages ? "disabled" : ""}>Next</button></div></div>
      <aside class="customer-snapshot"><h3>Latest order snapshot</h3><p class="customer-muted">Recorded on order #${a(t.snapshot.orderNumber)} · ${this.date(t.snapshot.date)}. Historical details, not a current customer profile.</p>${t.snapshot.unavailable ? "<p>Saved details could not be read.</p>" : ""}<dl>${Object.entries(t.snapshot.fields).map(([e, i]) => `<dt>${a(e)}</dt><dd>${a(i)}</dd>`).join("") || "<dd>No additional details recorded.</dd>"}</dl></aside></div></section></div>`;
  }
  bind() {
    var s, e, i, n, u, m, b, y, v, $;
    this.classic || D(this, () => {
      this.page = 1, this.dispatchEvent(new CustomEvent("customer-filters-changed", { bubbles: !0, detail: { ...o.filters } })), this.load();
    }), this.querySelectorAll("[data-filter]").forEach((r) => r.addEventListener("change", () => {
      const l = r.dataset.filter;
      o.filters[l] = r.value, this.dispatchEvent(new CustomEvent("customer-filters-changed", { bubbles: !0, detail: { ...o.filters } })), this.page = 1, this.load();
    }));
    const t = () => {
      var r;
      this.query = ((r = this.querySelector("[name=query]")) == null ? void 0 : r.value.trim()) || "", this.page = 1, this.load();
    };
    (s = this.querySelector("[data-search]")) == null || s.addEventListener("click", t), (e = this.querySelector("[name=query]")) == null || e.addEventListener("keydown", (r) => {
      r.key !== "Enter" || r.isComposing || (r.preventDefault(), r.stopPropagation(), t());
    }), this.querySelectorAll("[data-segment-value]").forEach((r) => r.addEventListener("click", () => {
      this.segment = r.dataset.segmentValue, this.page = 1, this.load();
    })), (i = this.querySelector("[data-segment]")) == null || i.addEventListener("change", (r) => {
      this.segment = r.target.value, this.page = 1, this.load();
    }), (n = this.querySelector("[data-sort]")) == null || n.addEventListener("change", (r) => {
      this.sort = r.target.value, this.page = 1, this.load();
    }), (u = this.querySelector("[data-reload]")) == null || u.addEventListener("click", () => void this.load()), (m = this.querySelector("[data-export]")) == null || m.addEventListener("click", () => void this.export()), (b = this.querySelector("[data-close]")) == null || b.addEventListener("click", () => this.close()), (y = this.querySelector(".customer-overlay")) == null || y.addEventListener("click", (r) => {
      r.target === r.currentTarget && this.close();
    }), (v = this.querySelector("[data-other]")) == null || v.addEventListener("change", () => {
      this.other = !this.other, this.detail && this.open(this.detail.customer.identity);
    }), this.querySelectorAll("[data-customer]").forEach((r) => r.addEventListener("click", () => {
      this.other = !1, this.open(r.dataset.customer);
    })), this.querySelectorAll("[data-page]").forEach((r) => r.addEventListener("click", () => {
      this.page = Number(r.dataset.page), this.load();
    })), this.querySelectorAll("[data-detail-page]").forEach((r) => r.addEventListener("click", () => {
      this.detail && this.open(this.detail.customer.identity, Number(r.dataset.detailPage));
    })), this.querySelectorAll("[data-order]").forEach((r) => r.addEventListener("click", () => {
      if (this.classic) this.dispatchEvent(new CustomEvent("customer-open-order", { bubbles: !0, detail: { orderId: r.dataset.order } }));
      else {
        o.pendingOrderId = r.dataset.order;
        const l = document.createElement("a");
        l.href = location.pathname.replace(/\/customers\/?$/, "/orders") + "?orderId=" + encodeURIComponent(r.dataset.order || ""), this.appendChild(l), l.click(), l.remove();
      }
    })), ($ = this.querySelector("[role=dialog]")) == null || $.addEventListener("keydown", (r) => {
      if (r.key === "Escape" && (r.preventDefault(), this.close()), r.key !== "Tab") return;
      const l = Array.from(this.querySelectorAll("[role=dialog] button:not(:disabled),[role=dialog] input")), p = l[0], g = l[l.length - 1];
      r.shiftKey && document.activeElement === p ? (r.preventDefault(), g == null || g.focus()) : !r.shiftKey && document.activeElement === g && (r.preventDefault(), p == null || p.focus());
    });
  }
}
customElements.define("ekom-customers-section-view", P);
export {
  P as EkomCustomersSectionViewElement,
  P as default
};
