var $ = Object.defineProperty;
var x = (u, c, e) => c in u ? $(u, c, { enumerable: !0, configurable: !0, writable: !0, value: e }) : u[c] = e;
var i = (u, c, e) => x(u, typeof c != "symbol" ? c + "" : c, e);
import { UmbElementMixin as w } from "@umbraco-cms/backoffice/element-api";
import { E as S, m as o, e as a, a as k } from "./manager-shared.js";
class E extends w(HTMLElement) {
  constructor() {
    super(...arguments);
    i(this, "api", new S());
    i(this, "result");
    i(this, "detail");
    i(this, "query", "");
    i(this, "segment", "all");
    i(this, "sort", "newest");
    i(this, "page", 1);
    i(this, "busy", !1);
    i(this, "error", "");
    i(this, "request", 0);
    i(this, "detailRequest", 0);
    i(this, "other", !1);
    i(this, "classic", !1);
    i(this, "opener");
    i(this, "dismissOnEscape", (e) => {
      !this.detail || e.key !== "Escape" || (e.preventDefault(), e.stopPropagation(), this.close());
    });
  }
  connectedCallback() {
    var e;
    if (super.connectedCallback(), (e = this.ownerDocument) == null || e.addEventListener("keydown", this.dismissOnEscape, !0), this.classic = this.hasAttribute("classic"), this.classic)
      for (const t of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const r = this.getAttribute(t.toLowerCase());
        r && (o.filters[t] = r);
      }
    this.initialize();
  }
  disconnectedCallback() {
    var e;
    (e = this.ownerDocument) == null || e.removeEventListener("keydown", this.dismissOnEscape, !0), this.request++, this.detailRequest++, super.disconnectedCallback();
  }
  async initialize() {
    var e;
    this.busy = !0, this.render();
    try {
      o.stores = await this.api.stores(), o.stores.some((t) => t.alias === o.filters.store) || (o.filters.store = ((e = o.stores[0]) == null ? void 0 : e.alias) || ""), await this.load();
    } catch {
      this.error = "Unable to load customers. Please try again.", this.busy = !1, this.render();
    }
  }
  params() {
    const e = o.filters;
    return new URLSearchParams({ store: e.store, start: e.dateFrom, end: e.dateTo, orderStatus: e.orderStatus, query: this.query, segment: this.segment, sort: this.sort, page: String(this.page) });
  }
  async get(e, t) {
    const r = await fetch(`/ekom/manager/customers${e}?${t}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!r.ok) throw new Error("Unable to load customer data. Check the filters and try again.");
    return r.json();
  }
  async load() {
    const e = ++this.request;
    this.detailRequest++, this.detail = void 0, this.result = void 0, this.error = "", this.busy = !0, this.render();
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
    var d;
    const r = ++this.detailRequest;
    this.opener = e, this.error = "";
    try {
      const n = await this.get("/detail", new URLSearchParams({ store: o.filters.store, identity: e, page: String(t), other: String(this.other) }));
      if (r !== this.detailRequest || !this.isConnected) return;
      this.detail = n, this.render(), (d = this.querySelector("[data-close]")) == null || d.focus();
    } catch (n) {
      r === this.detailRequest && (this.error = String(n), this.render());
    }
  }
  close() {
    var e;
    this.detailRequest++, this.detail = void 0, this.render(), (e = Array.from(this.querySelectorAll("[data-customer]")).find((t) => t.dataset.customer === this.opener)) == null || e.focus();
  }
  async export() {
    this.busy = !0, this.render();
    try {
      const e = await fetch(`/ekom/manager/customers/export?${this.params()}`, { credentials: "same-origin" });
      if (!e.ok) throw new Error("Customer export failed.");
      const t = URL.createObjectURL(await e.blob()), r = document.createElement("a");
      r.href = t, r.download = "customers.csv", r.click(), setTimeout(() => URL.revokeObjectURL(t), 1e3);
    } catch (e) {
      this.error = String(e);
    } finally {
      this.busy = !1, this.render();
    }
  }
  amount(e, t) {
    return e.map((r) => {
      var h;
      const d = (h = o.stores.find((m) => m.alias === o.filters.store)) == null ? void 0 : h.currency;
      let n = r.currency;
      !/^[A-Z]{3}$/.test(n) && (d == null ? void 0 : d.currencyValue) === n && (n = d.isoCurrencySymbol || n);
      try {
        return a(new Intl.NumberFormat(void 0, { style: "currency", currency: n }).format(r[t]));
      } catch {
        return `${a(r[t].toLocaleString(void 0, { maximumFractionDigits: 2 }))} ${a(r.currency)}`;
      }
    }).join("<br>") || "—";
  }
  number(e) {
    return e == null ? "—" : a(e.toLocaleString(void 0, { maximumFractionDigits: 1 }));
  }
  date(e) {
    return e ? a(new Date(e).toLocaleDateString()) : "—";
  }
  card(e, t) {
    return `<div class="customer-card"><small>${e}</small><strong>${t}</strong></div>`;
  }
  render() {
    const e = o.filters, t = this.result;
    this.innerHTML = `<style>${k}
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
    </style><section class="customers" aria-busy="${this.busy}">
      <div class="customer-toolbar">
        <label>Store<select data-filter="store">${o.stores.map((r) => `<option value="${a(r.alias)}" ${r.alias === e.store ? "selected" : ""}>${a(r.title)}</option>`).join("")}</select></label>
        <label>Date From<input type="date" data-filter="dateFrom" value="${a(e.dateFrom)}"></label>
        <label>Date To<input type="date" data-filter="dateTo" value="${a(e.dateTo)}"></label>
        <button data-reload ${this.busy ? "disabled" : ""}>Refresh</button>
      </div>
      <p class="customer-muted">Customers who ordered in the selected period. Lifetime metrics include all qualifying orders in this store. Status filter: ${a(e.orderStatus.replace(/([a-z])([A-Z])/g, "$1 $2"))}.</p>
      ${this.error ? `<p role="alert">${a(this.error)}</p>` : ""}
      ${this.busy && t ? '<p role="status">Preparing export...</p>' : ""}
      ${t ? `<div class="customer-cards">${this.card("Customers", this.number(t.totals.customers))}${this.card("Returning", this.number(t.totals.returningPercent) + "%")}${this.card("Average lifetime spend", this.amount(t.spend, "averageLifetime"))}${this.card("Average orders per customer", this.number(t.totals.averageOrders))}${this.card("Average days between orders", this.number(t.totals.averageReorderDays))}</div>
      <p class="customer-muted">${t.missingIdentifiers} qualifying orders in this period have no ${t.identityField === "CustomerUsername" ? "username" : "email"} and are excluded. Spend uses recorded charged amounts, without refund adjustment.</p>` : ""}
      <div class="customer-panel"><div class="customer-toolbar" role="search">
        <label>Search<input name="query" value="${a(this.query)}" placeholder="Name, email or username"></label>
        <button type="button" data-search>Search</button>
        <label>Customers<select data-segment>${["all", "new", "returning"].map((r) => `<option value="${r}" ${r === this.segment ? "selected" : ""}>${r[0].toUpperCase() + r.slice(1)}</option>`).join("")}</select></label>
        <label>Sort by<select data-sort>${[["newest", "Newest first order"], ["oldest", "Oldest first order"], ["lastOrder", "Latest order"], ["orders", "Most orders"], ["name", "Name"]].map(([r, d]) => `<option value="${r}" ${r === this.sort ? "selected" : ""}>${d}</option>`).join("")}</select></label>
        <button type="button" data-export ${this.busy || !(t != null && t.totals.customers) ? "disabled" : ""}>Export</button>
      </div><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Customer</th><th>First order</th><th>Orders</th><th>Lifetime spend</th><th>Average order</th><th>Last order</th><th>Days since last order</th></tr></thead><tbody>
      ${((t == null ? void 0 : t.customers) || []).map((r) => `<tr><td><button data-customer="${a(r.identity)}">${a(r.name || r.email || r.username || r.identity)}<span class="customer-badge">${r.orders > 1 ? "Returning" : "New"}</span><span class="customer-muted">${a(r.email || r.username || r.identity)}</span></button></td><td>${this.date(r.firstOrder)}</td><td>${r.orders}</td><td>${this.amount(r.spend, "total")}</td><td>${this.amount(r.spend, "averageOrder")}</td><td>${this.date(r.lastOrder)}<span class="customer-muted">${a(r.lastStatus || "")}</span></td><td>${r.daysSinceLastOrder}</td></tr>`).join("") || `<tr><td colspan="7">${this.busy ? '<div class="customer-loading" role="status"><span class="customer-spinner" aria-hidden="true"></span><strong>Loading customers...</strong><span class="customer-muted">Large datasets may take a moment.</span></div>' : t ? "No customers found." : this.error ? "Customer results could not be loaded." : "Select a store to see customers."}</td></tr>`}
      </tbody></table></div><div class="customer-pagination"><button data-page="${this.page - 1}" ${this.page <= 1 || this.busy ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${Math.max(1, (t == null ? void 0 : t.totalPages) || 0)}</span><button data-page="${this.page + 1}" ${this.page >= ((t == null ? void 0 : t.totalPages) || 0) || this.busy ? "disabled" : ""}>Next</button></div></div>
      ${this.detail ? this.renderDetail(this.detail) : ""}</section>`, this.bind();
  }
  renderDetail(e) {
    const t = e.customer;
    return `<div class="customer-overlay"><section role="dialog" aria-modal="true" aria-label="Customer details" class="customer-dialog"><header class="customer-heading"><div><h2>${a(t.name || t.identity)} <span class="customer-badge">${t.orders > 1 ? "Returning" : "New"}</span></h2><p>${a([t.email, t.username].filter(Boolean).join(" · "))}</p></div><button data-close aria-label="Close customer details">Close</button></header>
      <div class="customer-cards">${this.card("Lifetime spend", this.amount(t.spend, "total"))}${this.card("Orders", String(t.orders))}${this.card("Average order", this.amount(t.spend, "averageOrder"))}${this.card("First order", this.date(t.firstOrder))}${this.card("Days since last order", String(t.daysSinceLastOrder))}${this.card("Average reorder interval (days)", this.number(t.averageReorderDays))}</div>
      <div class="customer-detail-grid"><div><h3>Orders <small>(${e.count})</small></h3><label><input type="checkbox" data-other ${this.other ? "checked" : ""}> Show other orders (excluded from lifetime metrics)</label><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Order</th><th>Status</th><th>Created</th><th>Paid</th><th>Total</th></tr></thead><tbody>${e.orders.map((r) => `<tr><td><button data-order="${a(r.uniqueId)}">#${a(r.orderNumber || String(r.referenceId))}</button></td><td>${a(r.orderStatusCol)}</td><td>${this.date(r.createDate)}</td><td>${this.date(r.paidDate)}</td><td>${a(r.formattedTotal)}</td></tr>`).join("") || '<tr><td colspan="5">No orders.</td></tr>'}</tbody></table></div><div class="customer-pagination"><button data-detail-page="${e.page - 1}" ${e.page <= 1 ? "disabled" : ""}>Previous</button><span>${e.page} / ${Math.max(1, e.totalPages)}</span><button data-detail-page="${e.page + 1}" ${e.page >= e.totalPages ? "disabled" : ""}>Next</button></div></div>
      <aside class="customer-snapshot"><h3>Latest order snapshot</h3><p class="customer-muted">Recorded on order #${a(e.snapshot.orderNumber)} · ${this.date(e.snapshot.date)}. Historical details, not a current customer profile.</p>${e.snapshot.unavailable ? "<p>Saved details could not be read.</p>" : ""}<dl>${Object.entries(e.snapshot.fields).map(([r, d]) => `<dt>${a(r)}</dt><dd>${a(d)}</dd>`).join("") || "<dd>No additional details recorded.</dd>"}</dl></aside></div></section></div>`;
  }
  bind() {
    var t, r, d, n, h, m, f, b, y, v;
    this.querySelectorAll("[data-filter]").forEach((s) => s.addEventListener("change", () => {
      const l = s.dataset.filter;
      o.filters[l] = s.value, this.dispatchEvent(new CustomEvent("customer-filters-changed", { bubbles: !0, detail: { ...o.filters } })), this.page = 1, this.load();
    }));
    const e = () => {
      var s;
      this.query = ((s = this.querySelector("[name=query]")) == null ? void 0 : s.value.trim()) || "", this.page = 1, this.load();
    };
    (t = this.querySelector("[data-search]")) == null || t.addEventListener("click", e), (r = this.querySelector("[name=query]")) == null || r.addEventListener("keydown", (s) => {
      s.key !== "Enter" || s.isComposing || (s.preventDefault(), s.stopPropagation(), e());
    }), (d = this.querySelector("[data-segment]")) == null || d.addEventListener("change", (s) => {
      this.segment = s.target.value, this.page = 1, this.load();
    }), (n = this.querySelector("[data-sort]")) == null || n.addEventListener("change", (s) => {
      this.sort = s.target.value, this.page = 1, this.load();
    }), (h = this.querySelector("[data-reload]")) == null || h.addEventListener("click", () => void this.load()), (m = this.querySelector("[data-export]")) == null || m.addEventListener("click", () => void this.export()), (f = this.querySelector("[data-close]")) == null || f.addEventListener("click", () => this.close()), (b = this.querySelector(".customer-overlay")) == null || b.addEventListener("click", (s) => {
      s.target === s.currentTarget && this.close();
    }), (y = this.querySelector("[data-other]")) == null || y.addEventListener("change", () => {
      this.other = !this.other, this.detail && this.open(this.detail.customer.identity);
    }), this.querySelectorAll("[data-customer]").forEach((s) => s.addEventListener("click", () => {
      this.other = !1, this.open(s.dataset.customer);
    })), this.querySelectorAll("[data-page]").forEach((s) => s.addEventListener("click", () => {
      this.page = Number(s.dataset.page), this.load();
    })), this.querySelectorAll("[data-detail-page]").forEach((s) => s.addEventListener("click", () => {
      this.detail && this.open(this.detail.customer.identity, Number(s.dataset.detailPage));
    })), this.querySelectorAll("[data-order]").forEach((s) => s.addEventListener("click", () => {
      if (this.classic) this.dispatchEvent(new CustomEvent("customer-open-order", { bubbles: !0, detail: { orderId: s.dataset.order } }));
      else {
        o.pendingOrderId = s.dataset.order;
        const l = document.createElement("a");
        l.href = location.pathname.replace(/\/customers\/?$/, "/orders") + "?orderId=" + encodeURIComponent(s.dataset.order || ""), this.appendChild(l), l.click(), l.remove();
      }
    })), (v = this.querySelector("[role=dialog]")) == null || v.addEventListener("keydown", (s) => {
      if (s.key === "Escape" && (s.preventDefault(), this.close()), s.key !== "Tab") return;
      const l = Array.from(this.querySelectorAll("[role=dialog] button:not(:disabled),[role=dialog] input")), p = l[0], g = l[l.length - 1];
      s.shiftKey && document.activeElement === p ? (s.preventDefault(), g == null || g.focus()) : !s.shiftKey && document.activeElement === g && (s.preventDefault(), p == null || p.focus());
    });
  }
}
customElements.define("ekom-customers-section-view", E);
export {
  E as EkomCustomersSectionViewElement,
  E as default
};
