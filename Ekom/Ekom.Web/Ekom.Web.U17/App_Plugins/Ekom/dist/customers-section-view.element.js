var k = Object.defineProperty;
var S = (m, h, e) => h in m ? k(m, h, { enumerable: !0, configurable: !0, writable: !0, value: e }) : m[h] = e;
var n = (m, h, e) => S(m, typeof h != "symbol" ? h + "" : h, e);
import { E as w, m as o, e as E, u as q, c as i, l as L, r as C, a as O, b as D, d as A, g as R, h as P, i as j, v as N, w as x, x as U, y, q as F } from "./manager-shared.js";
import { UmbElementMixin as z } from "@umbraco-cms/backoffice/element-api";
class T extends z(HTMLElement) {
  constructor() {
    super(...arguments);
    n(this, "api", new w());
    n(this, "result");
    n(this, "detail");
    n(this, "query", "");
    n(this, "segment", "all");
    n(this, "sort", "newest");
    n(this, "page", 1);
    n(this, "busy", !1);
    n(this, "error", "");
    n(this, "request", 0);
    n(this, "detailRequest", 0);
    n(this, "detailOpening");
    n(this, "other", !1);
    n(this, "classic", !1);
    n(this, "opener");
    n(this, "dismissOnEscape", (e) => {
      !this.detail && !this.detailOpening || e.key !== "Escape" || (e.preventDefault(), e.stopPropagation(), this.close());
    });
  }
  connectedCallback() {
    var e, t;
    if (super.connectedCallback(), this.hasAttribute("classic") || (e = this.consumeContext) == null || e.call(this, "umbThemeContext", (s) => {
      s && this.observe(s.theme, (a) => {
        this.toggleAttribute("data-ekom-dark", a === "umb-dark-theme");
      }, "ekom-manager-theme");
    }), (t = this.ownerDocument) == null || t.addEventListener("keydown", this.dismissOnEscape, !0), this.classic = this.hasAttribute("classic"), this.classic)
      for (const s of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const a = this.getAttribute(s.toLowerCase());
        a && (o.filters[s] = a);
      }
    this.initialize();
  }
  disconnectedCallback() {
    var e;
    (e = this.ownerDocument) == null || e.removeEventListener("keydown", this.dismissOnEscape, !0), this.request++, this.detailRequest++, super.disconnectedCallback();
  }
  async initialize() {
    var t, s, a;
    const e = new URLSearchParams(this.classic || typeof location > "u" ? "" : location.search);
    e.get("customer") && e.get("store") && (o.filters.store = e.get("store")), this.busy = !0, this.render();
    try {
      const [d, l] = await Promise.all([this.api.stores(), this.classic ? Promise.resolve(!1) : this.api.printingEnabled()]);
      o.stores = d, o.printingEnabled = l, o.stores.some((g) => g.alias === o.filters.store) || (o.filters.store = ((t = o.stores[0]) == null ? void 0 : t.alias) || ""), this.classic || E(), await this.load();
      const c = o.pendingCustomer;
      o.pendingCustomer = void 0;
      const p = ((a = ((s = this.result) == null ? void 0 : s.identityField) === "CustomerUsername" ? c == null ? void 0 : c.username : c == null ? void 0 : c.email) == null ? void 0 : a.trim()) || e.get("customer");
      p && await this.open(p);
    } catch {
      this.error = "Unable to load customers. Please try again.", this.busy = !1, this.render();
    }
  }
  params() {
    const e = o.filters;
    return new URLSearchParams({ store: e.store, currency: e.currency || "", start: e.dateFrom, end: e.dateTo, orderStatus: e.orderStatus, query: this.query, segment: this.segment, sort: this.sort, page: String(this.page) });
  }
  async get(e, t) {
    const s = await fetch(`/ekom/manager/customers${e}?${t}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!s.ok) throw new Error("Unable to load customer data. Check the filters and try again.");
    return s.json();
  }
  async load() {
    const e = ++this.request;
    this.detailRequest++, this.detail = void 0, this.link(), this.result = void 0, this.error = "", this.busy = !0, this.render();
    try {
      if (!o.filters.store) {
        this.result = void 0;
        return;
      }
      const t = await this.get("", this.params());
      e === this.request && (this.result = t);
    } catch (t) {
      e === this.request && (this.result = void 0, this.error = String(t));
    } finally {
      e === this.request && (this.busy = !1, this.render());
    }
  }
  async open(e, t = 1) {
    var a, d;
    const s = ++this.detailRequest;
    this.opener = e, this.link(e), this.error = "", this.detail || (this.detailOpening = e, this.render(), (a = this.querySelector("[data-close]")) == null || a.focus());
    try {
      const l = await this.get("/detail", new URLSearchParams({ store: o.filters.store, currency: o.filters.currency || "", identity: e, orderStatus: o.filters.orderStatus, page: String(t), other: String(this.other) }));
      if (s !== this.detailRequest || !this.isConnected) return;
      this.detail = l, this.detailOpening = void 0, this.render(), (d = this.querySelector("[data-close]")) == null || d.focus();
    } catch (l) {
      s === this.detailRequest && (this.detailOpening = void 0, this.error = String(l), this.render());
    }
  }
  close() {
    var e;
    this.detailRequest++, this.detailOpening = void 0, this.detail = void 0, this.link(), this.render(), (e = Array.from(this.querySelectorAll("[data-customer]")).find((t) => t.dataset.customer === this.opener)) == null || e.focus();
  }
  /** Puts the open customer in the address bar, or takes it out when none is open. */
  link(e) {
    this.classic || q({ customer: e, store: e ? o.filters.store : void 0 });
  }
  async export() {
    this.busy = !0, this.render();
    try {
      const e = await fetch(`/ekom/manager/customers/export?${this.params()}`, { credentials: "same-origin" });
      if (!e.ok) throw new Error("Customer export failed.");
      const t = URL.createObjectURL(await e.blob()), s = document.createElement("a");
      s.href = t, s.download = "customers.csv", s.click(), setTimeout(() => URL.revokeObjectURL(t), 1e3);
    } catch (e) {
      this.error = String(e);
    } finally {
      this.busy = !1, this.render();
    }
  }
  amount(e, t) {
    return e.map((s) => {
      var l;
      const a = (l = o.stores.find((c) => c.alias === o.filters.store)) == null ? void 0 : l.currency;
      let d = s.currency;
      !/^[A-Z]{3}$/.test(d) && (a == null ? void 0 : a.currencyValue) === d && (d = a.isoCurrencySymbol || d);
      try {
        return i(new Intl.NumberFormat(void 0, { style: "currency", currency: d }).format(s[t]));
      } catch {
        return `${i(s[t].toLocaleString(void 0, { maximumFractionDigits: 2 }))} ${i(s.currency)}`;
      }
    }).join("<br>") || "—";
  }
  number(e) {
    return e == null ? "—" : i(e.toLocaleString(void 0, { maximumFractionDigits: 1 }));
  }
  date(e) {
    return e ? i(L(new Date(e))) : "—";
  }
  avatar(e) {
    const t = e.trim().split(/\s+/).slice(0, 2).map((s) => Array.from(s)[0] || "").join("").toUpperCase();
    return this.classic ? "" : `<span class="customer-avatar" aria-hidden="true">${i(t)}</span>`;
  }
  card(e, t) {
    return `<div class="customer-card"><small>${e}</small><strong>${t}</strong></div>`;
  }
  render() {
    const e = o.filters, t = this.result;
    C(this, `<style>${O}${this.classic ? `
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
    ` : ""}</style>${this.classic ? "" : '<link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css"><link rel="stylesheet" href="/App_Plugins/Ekom/dist/customers-layout.css?v=20261111-esc-close">'}<section class="customers ekom-design" aria-busy="${this.busy}">
      ${this.classic ? "" : D("customers", "data-filter")}
      <div class="customer-toolbar ekom-shared-filters">
        ${this.classic ? `<label>Store<select data-filter="store">${o.stores.map((s) => `<option value="${i(s.alias)}" ${s.alias === e.store ? "selected" : ""}>${i(s.title)}</option>`).join("")}</select></label>` : ""}
        ${this.classic ? `<label>Order view<select data-filter="orderStatus">${A(e.orderStatus)}</select></label>` : R()}
        ${this.classic ? `        <label>Date From<input type="date" data-filter="dateFrom" value="${i(e.dateFrom)}"></label>
        <label>Date To<input type="date" data-filter="dateTo" value="${i(e.dateTo)}"></label>
` : P()}
        <button data-reload ${this.busy ? "disabled" : ""}>Refresh</button>
      </div>
      <p class="customer-muted">Customers who ordered in the selected period. Lifetime metrics include all qualifying orders in the selected store and currency. </p>
      ${this.error ? `<p role="alert">${i(this.error)}</p>` : ""}
      ${this.busy && t ? '<p role="status">Preparing export...</p>' : ""}
      ${t ? `<div class="customer-cards">${this.card("Customers", this.number(t.totals.customers))}${this.card("Returning", this.number(t.totals.returningPercent) + "%")}${this.card("Average lifetime spend", this.amount(t.spend, "averageLifetime"))}${this.card("Average orders per customer", this.number(t.totals.averageOrders))}${this.card("Average days between orders", this.number(t.totals.averageReorderDays))}</div>
      <p class="customer-muted">${t.missingIdentifiers} qualifying orders in this period have no ${t.identityField === "CustomerUsername" ? "username" : "email"} and are excluded. Spend uses recorded charged amounts, without refund adjustment.</p>` : ""}
      ${this.busy && !t ? j("Customers", 5, 220) : ""}
      <div class="customer-panel" ${this.busy && !t ? "hidden" : ""}><div class="customer-toolbar" role="search">
        <label>Search<input name="query" value="${i(this.query)}" placeholder="Name, email or username"></label>
        <button type="button" data-search>Search</button>
        <nav class="ekom-customer-segments" aria-label="Customer segment">${["all", "new", "returning"].map((s) => `<button type="button" data-segment-value="${s}" aria-pressed="${s === this.segment}">${s[0].toUpperCase() + s.slice(1)}</button>`).join("")}</nav>
        <label>Sort by<select data-sort>${[["newest", "Newest first order"], ["oldest", "Oldest first order"], ["lastOrder", "Latest order"], ["orders", "Most orders"], ["name", "Name"]].map(([s, a]) => `<option value="${s}" ${s === this.sort ? "selected" : ""}>${a}</option>`).join("")}</select></label>
        <button type="button" data-export ${this.busy || !(t != null && t.totals.customers) ? "disabled" : ""}>Export</button>
      </div><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Customer</th><th>First order</th><th>Orders</th><th>Lifetime spend</th><th>Average order</th><th>Last order</th><th>Days since last order</th></tr></thead><tbody>
      ${((t == null ? void 0 : t.customers) || []).map((s) => `<tr><td><div class="customer-identity">${this.avatar(s.name || s.email || s.username || s.identity)}<button data-customer="${i(s.identity)}">${i(s.name || s.email || s.username || s.identity)}<span class="customer-badge">${s.orders > 1 ? "Returning" : "New"}</span><span class="customer-muted">${i(s.email || s.username || s.identity)}</span></button></div></td><td>${this.date(s.firstOrder)}</td><td>${s.orders}</td><td>${this.amount(s.spend, "total")}</td><td>${this.amount(s.spend, "averageOrder")}</td><td>${this.date(s.lastOrder)}${this.classic ? `<span class="customer-muted">${i(s.lastStatus || "")}</span>` : ""}</td><td>${s.daysSinceLastOrder}</td></tr>`).join("") || `<tr><td colspan="7">${t ? "No customers found." : this.error ? "Customer results could not be loaded." : "Select a store to see customers."}</td></tr>`}
      </tbody></table></div><div class="customer-pagination"><button data-page="${this.page - 1}" ${this.page <= 1 || this.busy ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${Math.max(1, (t == null ? void 0 : t.totalPages) || 0)}</span><button data-page="${this.page + 1}" ${this.page >= ((t == null ? void 0 : t.totalPages) || 0) || this.busy ? "disabled" : ""}>Next</button></div></div>
      ${this.detail ? this.renderDetail(this.detail) : this.detailOpening ? this.renderDetailOpening(this.detailOpening) : ""}</section>`), this.bind();
  }
  /** The customer drawer while the customer is still being fetched. */
  renderDetailOpening(e) {
    var a;
    const t = (a = this.result) == null ? void 0 : a.customers.find((d) => d.identity === e), s = (t == null ? void 0 : t.name) || (t == null ? void 0 : t.email) || (t == null ? void 0 : t.username) || e;
    return `<div class="customer-overlay"><section role="dialog" aria-modal="true" aria-label="Customer details" aria-busy="true" class="customer-dialog"><header class="customer-heading">${this.avatar(s)}<div><h2>${i(s)}</h2>${N("Loading customer…")}</div>${this.classic ? '<button data-close aria-label="Close customer details">Close</button>' : x('<button data-close aria-label="Close customer details">×</button>')}</header>
      <div class="customer-opening">${U()}</div></section></div>`;
  }
  renderDetail(e) {
    const t = e.customer;
    return `<div class="customer-overlay"><section role="dialog" aria-modal="true" aria-label="Customer details" class="customer-dialog"><header class="customer-heading">${this.avatar(t.name || t.identity)}<div><h2>${i(t.name || t.identity)} <span class="customer-badge">${t.orders > 1 ? "Returning" : "New"}</span></h2><p>${i([t.email, t.username].filter(Boolean).join(" · "))}</p></div>${this.classic ? '<button data-close aria-label="Close customer details">Close</button>' : x('<button data-close aria-label="Close customer details">×</button>')}</header>
      <div class="customer-cards">${this.card("Lifetime spend", this.amount(t.spend, "total"))}${this.card("Orders", String(t.orders))}${this.card("Average order", this.amount(t.spend, "averageOrder"))}${this.card("First order", this.date(t.firstOrder))}${this.card("Days since last order", String(t.daysSinceLastOrder))}${this.card("Average reorder interval (days)", this.number(t.averageReorderDays))}</div>
      <div class="customer-detail-grid"><div><h3>Orders <small>(${e.count})</small></h3><label><input type="checkbox" data-other ${this.other ? "checked" : ""}> Show other orders (excluded from lifetime metrics)</label><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Order</th>${this.classic ? "<th>Status</th>" : "<th>Order status</th><th>Payment</th><th>Fulfillment</th>"}<th>Created</th><th>Paid</th><th>Total</th></tr></thead><tbody>${e.orders.map((s) => {
      var a, d, l;
      return `<tr><td><button data-order="${i(s.uniqueId)}">#${i(s.orderNumber || String(s.referenceId))}</button></td>${this.classic ? `<td>${i(s.orderStatusCol)}</td>` : `<td>${y((a = s.status) == null ? void 0 : a.orderStatus)}</td><td>${y((d = s.status) == null ? void 0 : d.paymentStatus)}</td><td>${y((l = s.status) == null ? void 0 : l.fulfillmentStatus)}</td>`}<td>${this.date(s.createDate)}</td><td>${this.date(s.paidDate)}</td><td>${i(s.formattedTotal)}</td></tr>`;
    }).join("") || `<tr><td colspan="${this.classic ? 5 : 7}">No orders.</td></tr>`}</tbody></table></div><div class="customer-pagination"><button data-detail-page="${e.page - 1}" ${e.page <= 1 ? "disabled" : ""}>Previous</button><span>${e.page} / ${Math.max(1, e.totalPages)}</span><button data-detail-page="${e.page + 1}" ${e.page >= e.totalPages ? "disabled" : ""}>Next</button></div></div>
      <aside class="customer-snapshot"><h3>Latest order snapshot</h3><p class="customer-muted">Recorded on order #${i(e.snapshot.orderNumber)} · ${this.date(e.snapshot.date)}. Historical details, not a current customer profile.</p>${e.snapshot.unavailable ? "<p>Saved details could not be read.</p>" : ""}<dl>${Object.entries(e.snapshot.fields).map(([s, a]) => `<dt>${i(s)}</dt><dd>${i(a)}</dd>`).join("") || "<dd>No additional details recorded.</dd>"}</dl></aside></div></section></div>`;
  }
  bind() {
    var t, s, a, d, l, c, p, g, v, $;
    this.classic || F(this, () => {
      this.page = 1, this.dispatchEvent(new CustomEvent("customer-filters-changed", { bubbles: !0, detail: { ...o.filters } })), this.load();
    }), this.querySelectorAll("[data-filter]").forEach((r) => r.addEventListener("change", () => {
      const u = r.dataset.filter;
      o.filters[u] = r.value, this.dispatchEvent(new CustomEvent("customer-filters-changed", { bubbles: !0, detail: { ...o.filters } })), this.page = 1, this.load();
    }));
    const e = () => {
      var r;
      this.query = ((r = this.querySelector("[name=query]")) == null ? void 0 : r.value.trim()) || "", this.page = 1, this.load();
    };
    (t = this.querySelector("[data-search]")) == null || t.addEventListener("click", e), (s = this.querySelector("[name=query]")) == null || s.addEventListener("keydown", (r) => {
      r.key !== "Enter" || r.isComposing || (r.preventDefault(), r.stopPropagation(), e());
    }), this.querySelectorAll("[data-segment-value]").forEach((r) => r.addEventListener("click", () => {
      this.segment = r.dataset.segmentValue, this.page = 1, this.load();
    })), (a = this.querySelector("[data-segment]")) == null || a.addEventListener("change", (r) => {
      this.segment = r.target.value, this.page = 1, this.load();
    }), (d = this.querySelector("[data-sort]")) == null || d.addEventListener("change", (r) => {
      this.sort = r.target.value, this.page = 1, this.load();
    }), (l = this.querySelector("[data-reload]")) == null || l.addEventListener("click", () => void this.load()), (c = this.querySelector("[data-export]")) == null || c.addEventListener("click", () => void this.export()), (p = this.querySelector("[data-close]")) == null || p.addEventListener("click", () => this.close()), (g = this.querySelector(".customer-overlay")) == null || g.addEventListener("click", (r) => {
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
        const u = document.createElement("a");
        u.href = location.pathname.replace(/\/customers\/?$/, "/orders") + "?orderId=" + encodeURIComponent(r.dataset.order || ""), this.appendChild(u), u.click(), u.remove();
      }
    })), ($ = this.querySelector("[role=dialog]")) == null || $.addEventListener("keydown", (r) => {
      if (r.key === "Escape" && (r.preventDefault(), this.close()), r.key !== "Tab") return;
      const u = Array.from(this.querySelectorAll("[role=dialog] button:not(:disabled),[role=dialog] input")), f = u[0], b = u[u.length - 1];
      r.shiftKey && document.activeElement === f ? (r.preventDefault(), b == null || b.focus()) : !r.shiftKey && document.activeElement === b && (r.preventDefault(), f == null || f.focus());
    });
  }
}
customElements.define("ekom-customers-section-view", T);
export {
  T as EkomCustomersSectionViewElement,
  T as default
};
