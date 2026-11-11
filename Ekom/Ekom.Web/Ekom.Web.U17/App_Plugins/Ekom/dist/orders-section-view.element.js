var ae = Object.defineProperty;
var oe = (a, h, e) => h in a ? ae(a, h, { enumerable: !0, configurable: !0, writable: !0, value: e }) : a[h] = e;
var c = (a, h, e) => oe(a, typeof h != "symbol" ? h + "" : h, e);
import { E as ne, m as v, o as x, e as de, r as le, a as ce, c as n, b as ue, h as he, n as O, l as E, q as K, s as q, k as pe, t as me, j as ve } from "./manager-shared.js";
import { UmbElementMixin as fe } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as ge } from "@umbraco-cms/backoffice/notification";
const ye = [
  { key: "customerName", label: "Name", property: "name" },
  { key: "customerEmail", label: "Email", property: "email" },
  { key: "customerAddress", label: "Address", property: "address" },
  { key: "customerApartment", label: "Apartment", property: "apartment" },
  { key: "customerCity", label: "City", property: "city" },
  { key: "customerCountry", label: "Country", property: "country" },
  { key: "customerZipCode", label: "Zipcode", property: "zipCode" },
  { key: "customerPhone", label: "Phone", property: "phone" }
], be = [
  { key: "shippingName", label: "Name", property: "name" },
  { key: "shippingEmail", label: "Email", property: "email" },
  { key: "shippingAddress", label: "Address", property: "address" },
  { key: "shippingApartment", label: "Apartment", property: "apartment" },
  { key: "shippingCity", label: "City", property: "city" },
  { key: "shippingCountry", label: "Country", property: "country" },
  { key: "shippingZipCode", label: "Zipcode", property: "zipCode" },
  { key: "shippingPhone", label: "Phone", property: "phone" }
], ke = 180, z = 1, W = 2;
class $e extends fe(HTMLElement) {
  constructor() {
    super(...arguments);
    c(this, "api", new ne());
    c(this, "notificationContext");
    c(this, "result", { orders: [], count: 0, totalPages: 0 });
    c(this, "page", 1);
    c(this, "loading", !0);
    c(this, "error", "");
    c(this, "config");
    c(this, "configError", "");
    c(this, "notifyOrderStatusOverride");
    c(this, "searchTimer", 0);
    c(this, "pollingSeconds", 0);
    c(this, "pollingTimer", 0);
    c(this, "overlay", "");
    c(this, "reviews", []);
    c(this, "activeReview");
    c(this, "reviewSaving", !1);
    c(this, "reviewError", "");
    c(this, "reviewComment", "");
    c(this, "reviewResolved", !0);
    c(this, "orderTab", "order");
    c(this, "stateSaving", !1);
    c(this, "orderListRequest", 0);
    c(this, "viewCounts", {});
    c(this, "returnFocusOrderId");
    c(this, "selectedOrder");
    c(this, "orderLogs", []);
    c(this, "orderLogsLoading", !1);
    c(this, "orderLogsError", !1);
    c(this, "activityLogExpandedIndexes", /* @__PURE__ */ new Set());
    c(this, "orderActions", []);
    c(this, "orderActionsLoading", !1);
    c(this, "orderActionsError", !1);
    c(this, "executingActionKey", "");
    c(this, "orderRequestId", 0);
    c(this, "lockRequestId", 0);
    c(this, "orderLockStatus");
    c(this, "releasingOrderLock", !1);
    c(this, "orderLockWarning", "");
    c(this, "orderLockReleaseError", "");
    c(this, "orderLockLoading", !1);
    c(this, "orderLockError", !1);
    c(this, "recoveryAttempts");
    c(this, "recoveryLoading", !1);
    c(this, "recoveryError", !1);
    c(this, "recoveryRequestId", 0);
    c(this, "recoveryOperation", "");
    c(this, "recoveryMessage", "");
    c(this, "recoveryActionError", "");
    c(this, "recoveryRefreshError", !1);
    c(this, "recoveryScrollTop");
    c(this, "customerEditorOpen", !1);
    c(this, "customerSaving", !1);
    c(this, "customerEditModel");
    c(this, "orderLineEditorOpen", !1);
    c(this, "orderLineSaving", !1);
    c(this, "orderLineEditModel");
    c(this, "shippingProviderEditorOpen", !1);
    c(this, "shippingProviderSaving", !1);
    c(this, "shippingProviders", []);
    c(this, "filterProvidersRequestId", 0);
    c(this, "filterProvidersLoading", !1);
    c(this, "shippingProviderId", "");
    c(this, "shippingCustomFields", []);
    c(this, "removingOrderLineId", "");
    c(this, "exportIncludeOrderLines", !1);
    c(this, "exporting", !1);
    c(this, "dismissFulfillmentMenus", (e) => {
      const t = e.composedPath();
      this.querySelectorAll(".ekom-row-fulfillment[open], .ekom-fulfillment-menu[open]").forEach((r) => {
        t.includes(r) || (r.open = !1);
      });
    });
    c(this, "handleKeyDown", (e) => {
      var t, r, i, o;
      if (this.activeReview && e.key === "Escape") {
        e.preventDefault(), e.stopPropagation(), this.reviewSaving || (this.activeReview = void 0, this.render());
        return;
      }
      if (!(this.activeReview && e.key.startsWith("Arrow"))) {
        if (e.key === "Escape") {
          const s = this.querySelector(".ekom-row-fulfillment[open], .ekom-fulfillment-menu[open]");
          if (s) {
            e.preventDefault(), e.stopPropagation(), s.open = !1, (t = s.querySelector("summary")) == null || t.focus();
            return;
          }
        }
        if (["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].includes(e.key) && this.overlay === "order") {
          const s = e.composedPath()[0];
          if (e.defaultPrevented || e.repeat || e.altKey || e.ctrlKey || e.metaKey || e.shiftKey || this.customerEditorOpen || this.orderLineEditorOpen || this.shippingProviderEditorOpen || this.stateSaving || this.executingActionKey || this.releasingOrderLock || this.recoveryOperation || (r = s == null ? void 0 : s.closest) != null && r.call(s, 'input, select, textarea, [contenteditable], [role="tablist"], details') || this.querySelector("details[open]")) return;
          const d = this.querySelector('[data-field="fulfillmentState"]');
          if (d && d.value !== ((o = (i = this.selectedOrder) == null ? void 0 : i.status) == null ? void 0 : o.fulfillmentStatus)) return;
          const l = ["ArrowLeft", "ArrowUp"].includes(e.key) ? "Previous order" : "Next order", m = this.querySelector(`.ekom-order-navigation button[aria-label="${l}"]`);
          m && !m.disabled && (e.preventDefault(), m.click());
          return;
        }
        if (e.key === "Tab" && this.overlay) {
          const s = this.querySelectorAll(".ekmCustomerInformationModal__panel, .ekmOverlay__panel"), d = s.item(s.length - 1), l = d ? Array.from(d.querySelectorAll('button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), a[href], summary, [tabindex="0"]')).filter((u) => !u.closest("[hidden]") && u.getClientRects().length > 0) : [], m = l[0], f = l[l.length - 1];
          m && f && (!d.contains(document.activeElement) || (e.shiftKey ? document.activeElement === m : document.activeElement === f)) && (e.preventDefault(), (e.shiftKey ? f : m).focus());
          return;
        }
        if (!(e.key !== "Escape" || !this.overlay)) {
          if (this.customerEditorOpen) {
            if (this.customerSaving)
              return;
            this.customerEditorOpen = !1, this.customerEditModel = void 0, this.render();
            return;
          }
          if (this.orderLineEditorOpen) {
            if (this.orderLineSaving)
              return;
            this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.render();
            return;
          }
          if (this.shippingProviderEditorOpen) {
            if (this.shippingProviderSaving)
              return;
            this.shippingProviderEditorOpen = !1, this.render();
            return;
          }
          this.exporting || this.closeOverlay();
        }
      }
    });
  }
  connectedCallback() {
    var e;
    super.connectedCallback(), this.hasAttribute("classic") || (e = this.consumeContext) == null || e.call(this, "umbThemeContext", (t) => {
      t && this.observe(t.theme, (r) => {
        this.toggleAttribute("data-ekom-dark", r === "umb-dark-theme");
      }, "ekom-manager-theme");
    }), this.consumeContext(ge, (t) => {
      this.notificationContext = t;
    }), document.addEventListener("keydown", this.handleKeyDown), document.addEventListener("pointerdown", this.dismissFulfillmentMenus, !0), this.render(), this.initialize();
  }
  disconnectedCallback() {
    super.disconnectedCallback(), v.filters.orderStatus === "NeedsReview" && (v.filters.orderStatus = "AllOrders"), document.removeEventListener("keydown", this.handleKeyDown), document.removeEventListener("pointerdown", this.dismissFulfillmentMenus, !0), window.clearTimeout(this.searchTimer), window.clearInterval(this.pollingTimer), this.orderListRequest++;
  }
  setPolling(e) {
    [0, 2, 5, 10, 20, 30].includes(e) && (window.clearInterval(this.pollingTimer), this.pollingSeconds = e, e && (this.pollingTimer = window.setInterval(() => {
      this.pollOrders();
    }, e * 1e3)), this.render());
  }
  async pollOrders() {
    if (!this.pollingSeconds || !this.isConnected || document.hidden || this.loading || this.overlay || this.stateSaving || this.contains(document.activeElement) || this.querySelector("details[open]")) return;
    const e = ++this.orderListRequest, t = { ...v.filters };
    if (t.store) {
      this.loading = !0;
      try {
        const r = await this.api.searchOrders(t, this.page);
        if (e !== this.orderListRequest || !this.pollingSeconds || !this.isConnected || this.overlay || this.contains(document.activeElement)) return;
        const i = { ...r, orders: r.orders || [] };
        if (t.orderStatus === "NeedsReview" && r.count === 0) {
          v.filters.orderStatus = "AllOrders", this.page = 1, await this.loadOrders();
          return;
        }
        {
          const s = { [t.orderStatus]: r.count };
          if (await Promise.all(x.filter((d) => d.key !== t.orderStatus).map(async (d) => {
            try {
              s[d.key] = (await this.api.searchOrders({ ...t, orderStatus: d.key }, 1, 1)).count;
            } catch {
            }
          })), e !== this.orderListRequest || !this.pollingSeconds || !this.isConnected || this.overlay || this.contains(document.activeElement)) return;
          (JSON.stringify(i) !== JSON.stringify(this.result) || x.some((d) => s[d.key] !== this.viewCounts[d.key])) && (this.result = i, this.viewCounts = s, this.loading = !1, this.render());
        }
        const o = this.querySelector(".ekom-polling summary");
        o && (o.classList.add("just-refreshed"), o.addEventListener("animationend", () => o.classList.remove("just-refreshed"), { once: !0 }));
      } catch {
      } finally {
        e === this.orderListRequest && (this.loading = !1);
      }
    }
  }
  async initialize() {
    var t;
    this.api.printingEnabled().then((r) => {
      v.printingEnabled = r, this.isConnected && this.render();
    });
    const e = new URLSearchParams(location.search);
    if (e.get("analyticsDrill") === "1") {
      history.replaceState(history.state, "", location.pathname), v.filters.store = e.get("store") || "", v.filters.currency = e.get("currency") || "", v.filters.dateFrom = e.get("start") || v.filters.dateFrom, v.filters.dateTo = e.get("end") || v.filters.dateTo, v.filters.orderStatus = e.get("orderStatus") || "CompletedOrders";
      for (const r of ["query", "paymentProvider", "shippingProvider", "couponCode", "giftcardCode", "productSku", "trackingSource", "trackingMedium", "trackingCampaign", "trackingTerm", "trackingContent", "trackingClickId"]) v.filters[r] = "";
    }
    try {
      const [r, i] = await Promise.all([
        this.api.statusList(),
        this.api.stores(),
        this.loadConfig()
      ]);
      if (v.statusList = r || [], v.stores = i || [], !v.filters.store && v.stores.length && (v.filters.store = v.stores[0].alias), this.hasAttribute("classic") || de(), await this.loadFilterProviders(), await this.loadOrders(), (t = v).pendingOrderId || (t.pendingOrderId = new URLSearchParams(location.search).get("orderId") || void 0), v.pendingOrderId) {
        const o = v.pendingOrderId;
        v.pendingOrderId = void 0, await this.openOrder(o);
      }
    } catch (r) {
      this.error = k(r, "Error loading Ekom Manager."), this.loading = !1, this.render();
    }
  }
  async loadConfig() {
    this.config = void 0, this.configError = "";
    try {
      this.config = await this.api.config();
    } catch {
      this.configError = "Unable to load Ekom configuration. Order status updates are disabled. Reload to try again.";
    }
    this.render();
  }
  get notifyOrderStatus() {
    var e;
    return this.notifyOrderStatusOverride ?? ((e = this.config) == null ? void 0 : e.managerOrderStatusFireEventsByDefault) ?? !1;
  }
  async loadFilterProviders(e = !1) {
    const t = ++this.filterProvidersRequestId, r = v.filters.store;
    if ((e || !r) && (v.filters.paymentProvider = "", v.filters.shippingProvider = ""), v.paymentProviders = [], v.shippingFilterProviders = [], !r) {
      this.filterProvidersLoading = !1;
      return;
    }
    this.filterProvidersLoading = !0, this.render();
    const [i, o] = await Promise.allSettled([
      this.api.paymentProviders(r),
      this.api.shippingProviders(r)
    ]);
    if (t !== this.filterProvidersRequestId || r !== v.filters.store)
      return;
    const s = i.status === "fulfilled" ? i.value : [], d = o.status === "fulfilled" ? o.value : [];
    v.paymentProviders = s, v.shippingFilterProviders = d, this.filterProvidersLoading = !1, (i.status === "rejected" || o.status === "rejected") && this.showNotification(
      "warning",
      "Filter options unavailable",
      "Some provider options could not be loaded. Orders can still be searched; reload to try again."
    ), v.filters.paymentProvider && (v.paymentProviders.some((m) => m.key === v.filters.paymentProvider) || (v.filters.paymentProvider = "")), v.filters.shippingProvider && !d.some((l) => l.key === v.filters.shippingProvider) && (v.filters.shippingProvider = ""), this.render();
  }
  async loadOrders() {
    if (!v.filters.store) {
      this.loading = !1, this.result = { orders: [], count: 0, totalPages: 0 }, this.stateSaving || this.render();
      return;
    }
    const e = ++this.orderListRequest, t = { ...v.filters };
    this.loading = !0, this.error = "", this.stateSaving || this.render();
    try {
      const r = await this.api.searchOrders(t, this.page);
      if (e !== this.orderListRequest) return;
      if (this.viewCounts[t.orderStatus] = r.count, t.orderStatus === "NeedsReview" && r.count === 0) {
        v.filters.orderStatus = "AllOrders", this.page = 1, await this.loadOrders();
        return;
      }
      Promise.all(x.filter((i) => i.key !== t.orderStatus).map(async (i) => {
        try {
          const o = await this.api.searchOrders({ ...t, orderStatus: i.key }, 1, 1);
          e === this.orderListRequest && (this.viewCounts[i.key] = o.count);
        } catch {
          e === this.orderListRequest && delete this.viewCounts[i.key];
        }
      })).then(() => {
        e !== this.orderListRequest || !this.isConnected || this.querySelectorAll('[data-action="select-order-view"]').forEach((i) => {
          const o = this.viewCounts[i.dataset.view || ""];
          i.dataset.view === "NeedsReview" && (i.hidden = !(o && o > 0));
          let s = i.querySelector("span");
          if (o === void 0) {
            s == null || s.remove();
            return;
          }
          s || (s = document.createElement("span"), i.appendChild(s)), s.textContent = String(o);
        });
      }), this.result = {
        ...r,
        orders: r.orders || []
      };
    } catch (r) {
      if (e !== this.orderListRequest) return;
      this.error = k(r, "Error searching orders.");
    } finally {
      e === this.orderListRequest && (this.loading = !1, this.stateSaving || this.render());
    }
  }
  render() {
    var i, o;
    const e = this.querySelector("[data-review-comment]"), t = e && ((i = this.ownerDocument) == null ? void 0 : i.activeElement) === e ? { start: e.selectionStart, end: e.selectionEnd } : void 0, r = Array.from(this.querySelectorAll(".ekmOverlay, .ekmOverlay__content, .ekmCustomerInformationModal .ekmOverlay__content")).map((s) => s.scrollTop);
    if (le(this, `
      <style>${ce}</style>
      <link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css?v=20261110-order-tabs-1">
      <section class="ekmManager ekom-design">
        <div class="ekmManager__body">
          ${this.configError ? `<p class="status status--error" role="alert">${n(this.configError)}</p>` : ""}
          ${ue("orders")}
          ${this.renderOrders()}
        </div>
      </section>
      ${this.renderOverlay()}
    `), this.bindEvents(), t) {
      const s = this.querySelector("[data-review-comment]");
      s == null || s.focus({ preventScroll: !0 }), s == null || s.setSelectionRange(t.start, t.end);
    }
    if (this.isConnected && this.overlay === "order" && !this.contains(document.activeElement) && ((o = this.querySelector('[data-action="close-overlay"]')) == null || o.focus({ preventScroll: !0 })), this.querySelectorAll(".ekmOverlay, .ekmOverlay__content, .ekmCustomerInformationModal .ekmOverlay__content").forEach((s, d) => {
      s.scrollTop = r[d] ?? 0;
    }), this.recoveryScrollTop !== void 0) {
      const s = this.querySelector(".ekmOverlay");
      s && (s.scrollTop = this.recoveryScrollTop);
    }
  }
  renderOrders() {
    const e = v.filters;
    return `${this.renderToolbar()}<section class="ekom-order-list" aria-label="Orders" aria-busy="${this.loading}">
      <div class="ekom-list-toolbar"><nav class="ekom-order-views" aria-label="Order views">${x.map((t) => `<button type="button" data-action="select-order-view" data-view="${n(t.key)}" ${t.key === "NeedsReview" && !(this.viewCounts[t.key] > 0) ? "hidden" : ""} aria-pressed="${e.orderStatus === t.key}">${t.label}${this.viewCounts[t.key] === void 0 ? "" : `<span>${this.viewCounts[t.key]}</span>`}</button>`).join("")}</nav><div class="ekom-list-search"><label class="ekom-search-field"><svg aria-hidden="true" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><circle cx="11" cy="11" r="7"></circle><path d="M20 20l-3.5-3.5"></path></svg><input type="search" data-field="query" value="${n(e.query)}" placeholder="Search orders" aria-label="Search orders"></label><button type="button" data-action="open-filter"><svg aria-hidden="true" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M4 6h16M7 12h10M10 18h4"></path></svg>Filter</button></div></div>
      <div class="ekom-inline-totals"><div><span>Orders</span><strong>${n(this.result.count || 0)}</strong></div><div><span>Order amount</span><strong>${n(this.result.grandTotal || "0")}</strong></div><div><span>Average</span><strong>${n(this.result.averageAmount || "0")}</strong></div></div>
      ${this.error ? `<p class="status status--error" role="alert">${n(this.error)}</p>` : ""}
      ${this.loading && !this.result.orders.length ? '<div class="ekom-list-message" role="status">Loading orders...</div>' : this.renderOrderTable()}
      ${this.renderPagination()}
    </section>`;
  }
  renderToolbar() {
    return `<div class="ekom-shared-filters">${he()}<div class="ekom-shared-filter-actions"><details class="ekom-polling${this.pollingSeconds ? " is-active" : ""}"><summary aria-label="Order polling"><svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-3-6.7"></path><path d="M21 3v6h-6"></path></svg><span>${this.pollingSeconds ? `Polling every ${this.pollingSeconds}s` : "Polling off"}</span><span aria-hidden="true">⌄</span></summary><div class="ekom-poll-options" role="group" aria-label="Polling interval">${[0, 2, 5, 10, 20, 30].map((e) => `<button type="button" data-poll-seconds="${e}" aria-pressed="${e === this.pollingSeconds}"><span aria-hidden="true">•</span>${e ? `Every ${e} seconds` : "Off"}</button>`).join("")}</div></details><button type="button" data-action="open-export">Export</button></div></div>`;
  }
  renderOrderTable() {
    return this.result.orders.length ? `<div class="ekom-list-table" role="table" aria-label="Orders"><div class="ekom-list-row ekom-list-head" role="row"><span role="columnheader">Order</span><span role="columnheader">Date</span><span role="columnheader">Customer</span><span role="columnheader" class="ekom-align-right">Total</span><span role="columnheader">Payment</span><span role="columnheader">Fulfillment</span><span role="columnheader" class="ekom-align-right">Items</span><span role="columnheader"><span class="ekom-sr-only">Open order</span></span></div>${this.result.orders.map((e) => this.renderOrderRow(e)).join("")}</div>` : '<div class="ekom-list-message"><h3>No orders match these filters</h3><p>Try a wider date range, another view, or clear the filters.</p><button type="button" data-action="clear-order-filters">Clear filters</button></div>';
  }
  renderOrderRow(e) {
    const t = e.status;
    return `<div class="ekom-list-row" role="row">
      <div role="cell" class="ekom-order-number"><span class="ekom-order-reference"><button type="button" data-action="view-order" data-order-id="${n(e.uniqueId)}" aria-label="Open order ${n(e.referenceId)}">#${n(e.referenceId)}</button>${e.needsReview ? '<span class="ekom-review-icon" role="img" aria-label="Needs review" title="Needs review">!</span>' : ""}</span><small>${n((t == null ? void 0 : t.orderStatus) || "Needs review")}</small></div>
      <span role="cell" class="ekom-row-date">${n(O(e.createDate))}</span>
      <div role="cell" class="ekom-row-customer"><strong>${n(e.customerName)}</strong><span>${n(e.customerEmail)}</span></div>
      <strong role="cell" class="ekom-align-right">${n(e.formattedTotal)}</strong>
      <div role="cell">${this.renderPaymentMenu(e)}</div>
      <div role="cell">${this.renderRowFulfillment(e)}</div>
      <span role="cell" class="ekom-align-right ekom-muted">${n(e.itemQuantity ?? "—")}</span>
      <button role="cell" type="button" class="ekom-row-open" data-action="view-order" data-order-id="${n(e.uniqueId)}" aria-label="Open order ${n(e.referenceId)}">›</button>
    </div>`;
  }
  renderPagination() {
    const e = this.result.count || 0, t = Math.max(1, this.result.totalPages);
    return `<footer class="ekom-list-footer"><span>${e ? (this.page - 1) * 20 + 1 : 0}–${Math.min(this.page * 20, e)} of ${e}</span><div><span>20 per page</span><button type="button" data-action="set-page" data-page="${this.page - 1}" ${this.page <= 1 || this.loading ? "disabled" : ""} aria-label="Previous page">‹</button><strong>${this.page} / ${t}</strong><button type="button" data-action="set-page" data-page="${this.page + 1}" ${this.page >= t || this.loading ? "disabled" : ""} aria-label="Next page">›</button></div></footer>`;
  }
  renderOverlay() {
    return this.overlay === "filter" ? this.renderFilterOverlay() : this.overlay === "export" ? this.renderExportOverlay() : this.overlay === "order" && this.selectedOrder ? this.renderOrderOverlay(this.selectedOrder) : "";
  }
  renderFilterOverlay() {
    const e = v.filters;
    return `
      <div class="ekmOverlay ekom-filter-overlay">
        <div class="ekmOverlay__panel ekom-filter-panel" role="dialog" aria-modal="true" aria-label="Filter orders">
          <div class="ekmOverlay__header"><div><h2>Filter orders</h2><p>Status filters also apply to Analytics and Customers</p></div><button class="btn-reset" type="button" data-action="close-overlay" aria-label="Close filters">&times;</button></div>
          <div class="ekmOverlay__content">
            <h3>Status</h3><div class="ekom-status-filter-grid">${[["Order", ["Draft", "Open", "Closed", "Cancelled"]], ["Payment", ["Pending", "Authorized", "Paid", "PartiallyPaid", "Failed", "Voided"]], ["Fulfillment", ["Unfulfilled", "Picking", "Picked", "Packed", "PartiallyFulfilled", "Fulfilled"]]].map(([t, r], i) => `<label class="control-group">${t}<select data-status-dimension="${i}"><option value="">Any</option>${r.map((o) => `<option value="${o}" ${e.orderStatus.startsWith("states:") && e.orderStatus.slice(7).split(";")[i] === o ? "selected" : ""}>${o.replace(/([a-z])([A-Z])/g, "$1 $2")}</option>`).join("")}</select></label>`).join("")}</div>
            <h3>Payment &amp; shipping</h3><div class="ekom-filter-fields">
            <label class="control-group">Payment provider
              <select data-filter-field="paymentProvider" ${this.filterProvidersLoading ? "disabled" : ""}>
                <option value="">Select payment provider</option>
                ${v.paymentProviders.map((t) => `<option value="${n(t.key)}" ${e.paymentProvider === t.key ? "selected" : ""}>${n(t.title)}</option>`).join("")}
              </select>
            </label>
            <label class="control-group">Shipping provider
              <select data-filter-field="shippingProvider" ${this.filterProvidersLoading ? "disabled" : ""}>
                <option value="">All shipping providers</option>
                ${v.shippingFilterProviders.map((t) => `<option value="${n(t.key)}" ${e.shippingProvider === t.key ? "selected" : ""}>${n(t.title)}</option>`).join("")}
              </select>
            </label>
            ${this.renderFilterInput("giftcardCode", "Gift card number:", "Full gift card number")}</div><h3>Order contents <small>Exact match</small></h3><div class="ekom-filter-fields ekom-filter-two">${this.renderFilterInput("productSku", "Product SKU", "Exact SKU")}${this.renderFilterInput("couponCode", "Coupon code", "Exact order-level coupon code")}</div><h3>Tracking</h3><div class="ekom-filter-fields ekom-filter-two">
            ${this.renderFilterInput("trackingSource", "Tracking source:", "facebook")}
            ${this.renderFilterInput("trackingMedium", "Tracking medium:", "paid-social")}
            ${this.renderFilterInput("trackingCampaign", "Tracking campaign:", "summer_2026")}
            ${this.renderFilterInput("trackingTerm", "Tracking term:", "running shoes")}
            ${this.renderFilterInput("trackingContent", "Tracking content:", "hero_banner")}
            ${this.renderFilterInput("trackingClickId", "Tracking click id:", "gclid or fbclid")}
            </div></div><div class="ekom-filter-footer"><button type="button" class="btn-success" data-action="apply-filter" ${this.filterProvidersLoading ? "disabled" : ""}>Apply</button><button type="button" class="btn-outline" data-action="close-overlay">Cancel</button></div>
        </div>
      </div>
    `;
  }
  renderPaymentMenu(e, t = !1) {
    const r = e.status;
    if (!this.config || this.configError || !r || r.requiresReview || r.orderStatus !== "Open") return E(r == null ? void 0 : r.paymentStatus);
    const i = ["Pending", "Authorized", "Paid", "PartiallyPaid", "Failed", "Voided"];
    return `<details class="ekom-row-fulfillment ekom-payment-menu" name="ekom-row-fulfillment"><summary aria-label="Set payment for order ${n(e.referenceId)}">${E(r.paymentStatus)}<span aria-hidden="true">▾</span></summary><div class="ekom-row-fulfillment-menu"><h4>Set payment</h4>${i.map((o) => `<button type="button" data-action="set-payment" data-detail="${t}" data-order-id="${n(e.uniqueId)}" data-value="${o}" aria-pressed="${r.paymentStatus === o}" ${this.stateSaving ? "disabled" : ""}>${this.renderStateDot(o)}${o.replace(/([a-z])([A-Z])/g, "$1 $2")}<span class="ekom-status-check">${r.paymentStatus === o ? "✓" : ""}</span></button>`).join("")}<p class="ekom-payment-note">Updates the recorded status immediately. Does not move money through the payment provider.</p>${t ? "" : `<label><input type="checkbox" data-row-events ${this.config.managerOrderStatusFireEventsByDefault ?? !0 ? "checked" : ""}><span><strong>Trigger status events</strong><small>Runs any handlers set up for this status</small></span></label>`}</div></details>`;
  }
  async changePayment(e) {
    var d;
    const t = e.dataset.detail === "true", r = t ? this.selectedOrder : this.result.orders.find((l) => l.uniqueId === e.dataset.orderId), i = e.dataset.value || "";
    if (e.disabled || this.stateSaving || !this.config || this.configError || !(r != null && r.status) || r.status.requiresReview || r.status.orderStatus !== "Open" || !["Pending", "Authorized", "Paid", "PartiallyPaid", "Failed", "Voided"].includes(i)) return;
    const o = e.closest("details");
    if (r.status.paymentStatus === i) {
      o == null || o.removeAttribute("open");
      return;
    }
    if (t) {
      const l = this.querySelector('[data-field="fulfillmentState"]');
      if (l && l.value !== r.status.fulfillmentStatus) {
        this.showError("Save your fulfillment change before updating payment.");
        return;
      }
      await this.changeState(`Payment${i}`);
      return;
    }
    const s = ((d = o == null ? void 0 : o.querySelector("[data-row-events]")) == null ? void 0 : d.checked) ?? this.config.managerOrderStatusFireEventsByDefault ?? !0;
    this.stateSaving = !0, o == null || o.querySelectorAll("button").forEach((l) => {
      l.disabled = !0;
    });
    try {
      await this.api.changeOrderStates(r.uniqueId, r.status.revision, `Payment${i}`, s), this.showSuccess("Payment status updated.");
    } catch (l) {
      this.showError(k(l, "Error updating payment status."));
    } finally {
      await this.loadOrders(), this.stateSaving = !1, this.render();
    }
  }
  renderRowFulfillment(e) {
    const t = e.status;
    return !this.config || this.configError || !t || t.requiresReview || t.orderStatus !== "Open" ? E(t == null ? void 0 : t.fulfillmentStatus) : `<details class="ekom-row-fulfillment" name="ekom-row-fulfillment"><summary aria-label="Set fulfillment for order ${n(e.referenceId)}">${E(t.fulfillmentStatus)}<span aria-hidden="true">▾</span></summary><div class="ekom-row-fulfillment-menu"><h4>Set fulfillment</h4>${["Unfulfilled", "Picking", "Picked", "Packed", "Fulfilled", "PartiallyFulfilled"].map((r) => `<button type="button" data-action="set-row-fulfillment" data-order-id="${n(e.uniqueId)}" data-value="${r}" aria-pressed="${t.fulfillmentStatus === r}" ${this.stateSaving ? "disabled" : ""}>${this.renderStateDot(r)}${r.replace(/([a-z])([A-Z])/g, "$1 $2")}<span class="ekom-status-check">${t.fulfillmentStatus === r ? "✓" : ""}</span></button>`).join("")}<label><input type="checkbox" data-row-events ${this.config.managerOrderStatusFireEventsByDefault ?? !0 ? "checked" : ""}><span><strong>Trigger status events</strong><small>Runs any handlers set up for this status</small></span></label></div></details>`;
  }
  async changeRowFulfillment(e) {
    var s;
    const t = this.result.orders.find((d) => d.uniqueId === e.dataset.orderId), r = e.dataset.value || "";
    if (e.disabled || this.stateSaving || !this.config || this.configError || !(t != null && t.status) || t.status.requiresReview || t.status.orderStatus !== "Open" || !["Unfulfilled", "Picking", "Picked", "Packed", "PartiallyFulfilled", "Fulfilled"].includes(r)) return;
    const i = e.closest("details");
    if (t.status.fulfillmentStatus === r) {
      i == null || i.removeAttribute("open");
      return;
    }
    const o = ((s = i == null ? void 0 : i.querySelector("[data-row-events]")) == null ? void 0 : s.checked) ?? this.config.managerOrderStatusFireEventsByDefault ?? !0;
    this.stateSaving = !0, i == null || i.querySelectorAll("button").forEach((d) => {
      d.disabled = !0;
    });
    try {
      await this.api.changeOrderStates(t.uniqueId, t.status.revision, r === "Fulfilled" ? "Fulfill" : r, o), this.showSuccess("Fulfillment status updated.");
    } catch (d) {
      this.showError(k(d, "Error updating fulfillment status."));
    } finally {
      await this.loadOrders(), this.stateSaving = !1, this.render();
    }
  }
  renderFilterInput(e, t, r) {
    return `
      <label class="control-group">${n(t)}
        <input type="text" data-filter-field="${n(e)}" value="${n(v.filters[e])}" placeholder="${n(r)}">
      </label>
    `;
  }
  renderExportOverlay() {
    return `<div class="ekmOverlay ekom-export-overlay"><div class="ekmOverlay__panel ekom-export-panel" role="dialog" aria-modal="true" aria-label="Export orders">
      <div class="ekmOverlay__header"><h2>Export orders</h2><button class="btn-reset" type="button" data-action="close-overlay" aria-label="Close export" ${this.exporting ? "disabled" : ""}>&times;</button></div>
      <div class="ekmOverlay__content"><p>Export the <strong>${this.result.count || 0}</strong> orders matching the current filters as CSV.</p>
        <label class="ekom-export-option"><input type="checkbox" data-field="includeOrderLines" ${this.exportIncludeOrderLines ? "checked" : ""} ${this.exporting ? "disabled" : ""}><span><strong>Include order lines</strong><small>Include product, variant, SKU, quantity and price details in the CSV.</small></span></label>
        ${this.exportIncludeOrderLines ? '<p class="ekom-export-note">Including order lines can take longer because each matching order needs to be loaded before the CSV is created.</p>' : ""}
        ${this.exporting ? '<p role="status">Exporting orders. This may take a while...</p>' : ""}
      </div><div class="ekom-filter-footer"><button type="button" class="btn-outline" data-action="close-overlay" ${this.exporting ? "disabled" : ""}>Cancel</button><button type="button" class="btn-success" data-action="export-orders" ${this.exporting ? "disabled" : ""}>Export</button></div>
    </div></div>`;
  }
  renderOrderOverlay(e) {
    var i, o, s, d, l, m;
    const t = this.result.orders.findIndex((f) => f.uniqueId === e.uniqueId);
    return `<div class="ekmOverlay ekom-order-drawer ekom-design-drawer" role="dialog" aria-modal="true" aria-label="Order details"><div class="ekmOverlay__panel ekmOrderOverlay__panel"><header class="ekom-panel-heading"><nav class="ekom-order-navigation" aria-label="Order navigation">${[-1, 1].map((f) => {
      const u = t >= 0 ? this.result.orders[t + f] : void 0, p = f < 0 ? "Previous order" : "Next order";
      return `<button type="button" data-action="view-order" data-order-id="${n((u == null ? void 0 : u.uniqueId) || "")}" aria-label="${p}" title="${p} on this page (${f < 0 ? "← / ↑" : "→ / ↓"})" ${!u || this.stateSaving || this.executingActionKey ? "disabled" : ""}>${f < 0 ? "‹" : "›"}</button>`;
    }).join("")}</nav><div class="ekom-order-heading-copy"><h2>#${n(e.referenceId)} ${E((i = e.status) == null ? void 0 : i.paymentStatus)} ${E((o = e.status) == null ? void 0 : o.fulfillmentStatus)} ${E((s = e.status) == null ? void 0 : s.orderStatus)}</h2><p>${n((l = (d = e.customerInformation) == null ? void 0 : d.customer) == null ? void 0 : l.name)} · ${n(O(e.createDate))} · ${n(((m = e.storeInfo) == null ? void 0 : m.alias) || e.storeAlias)} · <span class="ekom-heading-order-id"><span>${n(e.uniqueId)}</span><button type="button" class="ekom-copy-order-id" data-action="copy-order-id" aria-label="Copy order ID" title="Copy order ID"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V4H4v12h4"/></svg></button></span></p></div><button type="button" data-action="close-overlay" aria-label="Close order">×</button></header><div class="ekmOverlay__content ekmOrder">${this.renderOrderDetails(e)}</div></div></div>${this.customerEditorOpen ? this.renderCustomerEditor() : ""}${this.orderLineEditorOpen ? this.renderOrderLineEditor() : ""}${this.shippingProviderEditorOpen ? this.renderShippingProviderEditor() : ""}${this.activeReview ? this.renderReviewDialog() : ""}`;
  }
  renderOrderDetails(e) {
    var m, f, u, p;
    const t = ((m = e.customerInformation) == null ? void 0 : m.customer) || {}, r = ((f = e.customerInformation) == null ? void 0 : f.shipping) || {}, i = e.status, o = [{ key: "order", label: "Order" }, { key: "activity", label: "Activity log" }, { key: "tracking", label: "Tracking & consent" }], s = !this.config || !!this.configError || this.stateSaving || !i || i.requiresReview || ((u = this.orderLockStatus) == null ? void 0 : u.hasLock) || this.orderLockLoading || this.orderLockError, d = (g, y) => `<section id="ekom-order-panel-${g}" role="tabpanel" aria-labelledby="ekom-order-tab-${g}" ${this.orderTab === g ? "" : "hidden"}>${y}</section>`, l = `<div class="ekom-order-columns"><main class="ekom-order-main">
      <section class="ekom-detail-card">${this.renderDesignOrderLines(e)}</section>
      ${this.renderPaymentCard(e)}
      ${this.renderShipment(e)}
      ${Object.keys(e.customData || {}).length ? `<section class="ekom-detail-card"><h3>Notes</h3>${this.renderOrderCustomData(e)}</section>` : ""}
    </main><aside class="ekom-customer-sidebar"><section class="ekom-detail-card"><h3>Customer</h3><strong>${n(t.name)}</strong><div class="ekom-card-heading"><h4>Contact information</h4><button type="button" class="btn-reset" data-action="open-customer-editor">Edit</button></div><p>${n(t.email)}</p><p>${n(t.phone)}</p><h4>Shipping address</h4>${F(r) ? this.renderAddress(r) : "<p>Same as billing address</p>"}<h4>Billing address</h4>${this.renderAddress(t)}${this.renderExtraProperties(t.properties, "customer")}</section></aside></div>`;
    return `<div class="ekom-status-controls"><label>Order<details class="ekom-lifecycle-menu"><summary aria-label="Order status">${this.renderStateDot(i == null ? void 0 : i.orderStatus)}${n((i == null ? void 0 : i.orderStatus) || "Needs review")} ▾</summary><div>${((i == null ? void 0 : i.orderStatus) === "Closed" ? [["Reopen", "Reopen order"]] : [["Close", "Close order"], ["Cancel", "Cancel order"]]).map(([g, y]) => `<button type="button" data-action="order-state-command" data-command="${g}" ${s ? "disabled" : ""}>${y}</button>`).join("")}</div></details></label><div class="ekom-payment-control"><span>Payment</span>${this.renderPaymentMenu(e, !0)}</div><div class="ekom-fulfillment-control"><span>Fulfillment</span><input type="hidden" data-field="fulfillmentState" value="${n((i == null ? void 0 : i.fulfillmentStatus) || "")}"><details class="ekom-lifecycle-menu ekom-fulfillment-menu"><summary aria-label="Fulfillment status" ${s || (i == null ? void 0 : i.orderStatus) !== "Open" ? 'aria-disabled="true"' : ""}>${this.renderStateDot(i == null ? void 0 : i.fulfillmentStatus)}<span data-fulfillment-label>${n(((i == null ? void 0 : i.fulfillmentStatus) || "Needs review").replace(/([a-z])([A-Z])/g, "$1 $2"))}</span><span class="ekom-status-chevron">▾</span></summary><div>${["Unfulfilled", "Picking", "Picked", "Packed", "PartiallyFulfilled", "Fulfilled"].map((g) => `<button type="button" data-action="choose-fulfillment" data-value="${g}" aria-pressed="${(i == null ? void 0 : i.fulfillmentStatus) === g}" ${s || (i == null ? void 0 : i.orderStatus) !== "Open" ? "disabled" : ""}>${this.renderStateDot(g)}${g.replace(/([a-z])([A-Z])/g, "$1 $2")}<span class="ekom-status-check">${(i == null ? void 0 : i.fulfillmentStatus) === g ? "✓" : ""}</span></button>`).join("")}</div></details></div><label class="ekmCheckboxLabel"><input type="checkbox" data-field="notifyOrderStatus" ${this.notifyOrderStatus ? "checked" : ""}>Trigger status events</label><span class="ekom-events-help"><button type="button" class="ekom-events-help-button" aria-label="About trigger status events" aria-describedby="ekom-events-tooltip">i</button><span class="ekom-events-tooltip" id="ekom-events-tooltip" role="tooltip"><strong>Trigger status events ("Fire events")</strong><span><b>On</b> — saves the status and runs whatever handlers are set up for it. What happens depends on the status and your setup, for example an email or an ERP update.</span><span><b>Off</b> — saves the status only. Use for manual corrections so external actions don't repeat.</span><small>Either way the status is saved and logged. It doesn't release stock or replace the payment flow.</small></span></span><button type="button" data-action="save-fulfillment" class="btn-success" ${s || (i == null ? void 0 : i.orderStatus) !== "Open" ? "disabled" : ""}>${this.stateSaving ? "Saving..." : "Save"}</button></div>
      ${i != null && i.requiresReview ? '<p class="status" role="status">Historical payment or fulfillment needs review before updating this order.</p>' : ""}
      ${this.configError ? `<p class="status status--error" role="alert">${n(this.configError)}</p>` : ""}
      <span class="ekom-sr-only">Payment: ${n((p = e.chargedAmount) == null ? void 0 : p.currencyString)}</span>${this.renderOrderLockStatus()}${this.renderOrderActions()}${this.renderGiftcardRecovery()}${this.renderReviews()}
      <div class="ekom-order-tabs" role="tablist" aria-label="Order sections">${o.map((g) => `<button type="button" id="ekom-order-tab-${g.key}" role="tab" aria-selected="${this.orderTab === g.key}" aria-controls="ekom-order-panel-${g.key}" tabindex="${this.orderTab === g.key ? 0 : -1}" data-action="select-order-tab" data-tab="${g.key}">${g.label}</button>`).join("")}</div>
      ${d("order", l)}${d("activity", this.renderActivityLogs())}${d("tracking", this.renderTracking(e) + this.renderConsent(e))}`;
  }
  selectOrderTab(e) {
    ["order", "activity", "tracking"].includes(e) && (this.orderTab = e, this.querySelectorAll('[role="tabpanel"]').forEach((t) => {
      t.hidden = t.id !== `ekom-order-panel-${e}`;
    }), this.querySelectorAll('[data-action="select-order-tab"]').forEach((t) => {
      const r = t.dataset.tab === e;
      t.setAttribute("aria-selected", String(r)), t.tabIndex = r ? 0 : -1, r && t.focus();
    }));
  }
  async changeState(e) {
    var r;
    const t = this.selectedOrder;
    if (!(!(t != null && t.status) || this.stateSaving || !this.config || this.configError) && !(e === "Cancel" && !window.confirm("Cancel this order? This does not refund payments or reverse fulfillment."))) {
      this.stateSaving = !0, this.render();
      try {
        const i = await this.api.changeOrderStates(t.uniqueId, t.status.revision, e, this.notifyOrderStatus);
        if (((r = this.selectedOrder) == null ? void 0 : r.uniqueId) !== t.uniqueId) return;
        t.status = i, this.selectedOrder = await this.api.orderInfo(t.uniqueId), await Promise.all([this.loadOrderLogs(t.uniqueId), this.loadOrderActions(t.uniqueId)]), await this.loadOrders();
      } catch (i) {
        this.showError(k(i, "Could not update the order. Reload it before trying again."));
      } finally {
        this.stateSaving = !1, this.render();
      }
    }
  }
  renderOrderCustomData(e) {
    const t = Object.entries(e.customData || {}).filter(([r, i]) => r.trim() && typeof i == "string" && i.trim());
    return t.length ? `<dl class="ekmOrderCustomData" aria-label="Order custom data">${t.map(([r, i]) => `
      <div class="ekmOrderCustomData__item">
        <dt class="ekmOrderCustomData__key">${n(Se(r))}</dt>
        <dd class="ekmOrderCustomData__value">${n(i)}</dd>
      </div>`).join("")}</dl>` : "";
  }
  renderPaymentCard(e) {
    var o, s, d, l, m, f, u, p, g, y, S, A, I;
    const t = (e.orderLines || []).reduce((L, w) => L + (Number(w.quantity) || 0), 0), r = ((o = e.shippingProvider) == null ? void 0 : o.title) || ((d = (s = e.shippingProvider) == null ? void 0 : s.properties) == null ? void 0 : d.nodeName) || "", i = (L, w, $, C = !1) => `<div${C ? ' class="ekom-money-total"' : ""}><dt>${n(L)}</dt><dd class="ekom-money-description">${n(w)}</dd><dd>${n($ || "—")}</dd></div>`;
    return `<section class="ekom-detail-card ekom-payment-card"><h3>Payment ${E((l = e.status) == null ? void 0 : l.paymentStatus)}</h3><dl class="ekom-money-summary">${i("Subtotal", `${t} ${t === 1 ? "item" : "items"}`, (f = (m = e.subTotal) == null ? void 0 : m.withVat) == null ? void 0 : f.currencyString)}${i("Discount", e.coupon || "", (u = e.discountAmount) != null && u.currencyString ? `-${e.discountAmount.currencyString}` : "")}${i("Shipping", r, (y = (g = (p = e.shippingProvider) == null ? void 0 : p.price) == null ? void 0 : g.withVat) == null ? void 0 : y.currencyString)}${i("Tax", "Included in total", (S = e.chargedVat) == null ? void 0 : S.currencyString)}${i("Total", "", (A = e.chargedAmount) == null ? void 0 : A.currencyString, !0)}</dl>${(I = e.giftcards) != null && I.length ? `<div class="ekom-payment-breakdown">${this.renderGiftcards(e)}</div>` : ""}${this.renderPaymentDetails(e)}</section>`;
  }
  renderPaymentDetails(e) {
    var i, o, s, d, l, m, f;
    const t = (u) => `<dl>${u.map((p) => `<div><dt>${n(p.label)}</dt><dd><span>${n(p.value)}</span>${p.label !== "Currency" && p.value && p.value !== "Not recorded" && p.value !== "—" ? `<button type="button" class="ekom-payment-copy" data-action="copy-provider-value" data-copy="${n(p.value)}" aria-label="Copy ${n(p.label)}" title="Copy ${n(p.label)}"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V4H4v12h4"/></svg></button>` : ""}</dd></div>`).join("")}</dl>`, r = _((i = e.paymentProvider) == null ? void 0 : i.customData, "custompayment").map(([u, p]) => ({ label: T(R(u)), value: p }));
    return r.push({ label: "Paid date", value: e.paidDate ? O(e.paidDate) : "Not recorded" }, { label: "Currency", value: e.currency || ((s = (o = e.storeInfo) == null ? void 0 : o.currency) == null ? void 0 : s.isoCurrencySymbol) || "Not recorded" }), `<section class="ekom-payment-details" aria-label="Payment details"><div class="ekom-payment-detail-grid"><section class="ekom-payment-detail-box"><header><span class="ekom-payment-kind">Payment</span><h4>${n(((d = e.paymentProvider) == null ? void 0 : d.title) || ((m = (l = e.paymentProvider) == null ? void 0 : l.properties) == null ? void 0 : m.nodeName) || "Not recorded")}</h4>${E((f = e.status) == null ? void 0 : f.paymentStatus)}</header>${t(r)}</section>${(e.giftcards || []).map((u) => {
      const p = Z(u).filter((g) => g.label !== D("code")).map((g) => g.label === D("amount") ? { ...g, value: this.formatGiftcardAmount(u.amount, e) } : g);
      return `<section class="ekom-payment-detail-box"><header><span class="ekom-payment-kind ekom-payment-kind-gift">Gift card</span><h4>${n(u.code)}</h4><span class="ekom-gift-state">${n(H(u).status)}</span></header>${t(p)}</section>`;
    }).join("")}</div></section>`;
  }
  renderShipment(e) {
    var y, S, A, I, L;
    const t = e.shippingProvider, r = (t == null ? void 0 : t.title) || ((y = t == null ? void 0 : t.properties) == null ? void 0 : y.nodeName) || "No shipping provider", i = _(t == null ? void 0 : t.customData, "customshipping"), o = (w) => {
      var $;
      return (($ = i.find(([C]) => R(C).replace(/[_ -]/g, "").toLowerCase() === w.toLowerCase())) == null ? void 0 : $[1]) || "";
    }, s = o("TrackingNumber"), d = o("ShipmentState"), l = o("TrackingUrl"), m = /^https?:\/\//i.test(l) ? l : "", f = ((S = e.customerInformation) == null ? void 0 : S.customer) || {}, u = ((A = e.customerInformation) == null ? void 0 : A.shipping) || {}, p = F(u) ? u : f, g = (w, $) => `<button type="button" class="ekom-shipment-copy" data-action="copy-provider-value" data-copy="${n(w)}" aria-label="Copy ${n($)}" title="Copy ${n($)}"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V4H4v12h4"/></svg></button>`;
    return `<section class="ekom-detail-card ekom-shipment"><div class="ekom-shipment-grid"><section class="ekom-shipment-box"><div class="ekom-shipment-heading"><h4>Shipment</h4>${d ? `<span class="ekom-shipment-state">${n(d)}</span>` : ""}</div><div class="ekom-shipment-tracking"><span>Tracking number</span><div><strong>${n(s || "Not available")}</strong>${s ? g(s, "tracking number") : ""}${t ? '<button type="button" class="ekom-shipment-copy" data-action="open-shipping-provider-editor" aria-label="Edit shipping data" title="Edit shipping data"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><path d="M12 20h9M16 3l5 5L7 22l-5 1 1-5Z"/></svg></button>' : ""}${m ? `<a href="${n(m)}" target="_blank" rel="noopener noreferrer">Track shipment ↗</a>` : ""}</div></div></section><section class="ekom-shipment-box"><div class="ekom-shipment-heading"><h4>Method</h4><strong>${n(((L = (I = t == null ? void 0 : t.price) == null ? void 0 : I.withVat) == null ? void 0 : L.currencyString) || "—")}</strong></div><h3>${n(r)}</h3><div class="ekom-shipment-address"><span>Deliver to</span><strong>${n([p.address, p.apartment].filter(Boolean).join(", ") || "No address recorded")}</strong><span>${n([p.zipCode, p.city].filter(Boolean).join(" "))}</span><span>${n(p.country)}</span></div><div><button type="button" data-action="open-shipping-provider-editor">${t ? "Change shipping provider" : "Add shipping provider"}</button></div></section></div>${i.length ? `<details class="ekom-shipment-data" open><summary><strong>Provider data</strong><span>${i.length} fields from ${n(r)}</span><span class="ekom-shipment-hide">Hide ▴</span><span class="ekom-shipment-show">Show ▾</span></summary><dl>${i.map(([w, $]) => `<div><dt>${n(T(R(w)))}</dt><dd><span>${n($ || "—")}</span>${$ ? g($, T(R(w))) : ""}</dd></div>`).join("")}</dl></details>` : ""}</section>`;
  }
  renderPrintableOrder(e) {
    var s, d, l, m, f, u;
    const t = ((s = e.customerInformation) == null ? void 0 : s.customer) || {}, r = ((d = e.customerInformation) == null ? void 0 : d.shipping) || {}, i = this.getOrderStatusValue(e.orderStatus), o = ((l = v.statusList.find((p) => K(p) === i)) == null ? void 0 : l.label) || i;
    return `
      <main class="ekmOrder">
        <h1>Order number: ${n(e.referenceId)}</h1>
        <p>Order status: ${n(((m = e.status) == null ? void 0 : m.orderStatus) || o)}</p>
        ${e.status ? `<p>Payment status: ${n(e.status.paymentStatus.replace(/([a-z])([A-Z])/g, "$1 $2"))} · Fulfillment: ${n(e.status.fulfillmentStatus.replace(/([a-z])([A-Z])/g, "$1 $2"))}</p>` : ""}
        <p>UniqueId: ${n(e.uniqueId)}</p>
        <p>Created date: ${n(O(e.createDate))}</p>
        <p>Paid date: ${n(O(e.paidDate))}</p>
        <p>Store: ${n(((f = e.storeInfo) == null ? void 0 : f.alias) || e.storeAlias)}</p>
        <p>Payment: <strong>${n((u = e.chargedAmount) == null ? void 0 : u.currencyString)}</strong></p>
        ${this.renderOrderCustomData(e)}
        <section class="ekmSplit">
          <div class="ekmSplit__column"><h4>Billing</h4>${this.renderAddress(t)}${this.renderExtraProperties(t.properties, "customer")}</div>
          <div class="ekmSplit__column"><h4>Shipping</h4>${F(r) ? `${this.renderAddress(r)}${this.renderExtraProperties(r.properties, "shipping")}` : "<p>Same as billing address</p>"}</div>
        </section>
        <section class="ekmSplit">
          <div class="ekmSplit__column">${this.renderProvider("Payment Method", e.paymentProvider, "custompayment")}</div>
          <div class="ekmSplit__column">${this.renderProvider("Shipping Method", e.shippingProvider, "customshipping")}</div>
        </section>
        ${this.renderGiftcards(e)}
        ${this.renderOrderLines(e)}
      </main>`;
  }
  renderAddress(e) {
    return ["name", "email", "address", "apartment", "city", "country", "zipCode", "phone"].filter((t) => e[t]).map((t) => `<p>${T(t)}: ${n(q(e[t]))}</p>`).join("");
  }
  renderExtraProperties(e, t) {
    const r = _(e, t);
    return r.length ? `<h5 style="margin-top:20px; font-weight:bold;">${t === "customshipping" ? "Shipping Provider Custom" : `Extra ${t === "shipping" ? "Shipping" : "Customer"}`} Data</h5><ul>${r.map(([i, o]) => `<li><strong>${n(R(i))}</strong>: ${n(o)}</li>`).join("")}</ul>` : "";
  }
  renderProvider(e, t, r) {
    var o, s;
    if (!t)
      return "";
    const i = typeof t.title == "string" && t.title.trim() ? t.title : (o = t.properties) == null ? void 0 : o.nodeName;
    return `<h4>${n(e)}</h4><h4><strong>${n(i)}</strong></h4>${t.price ? `<p>Price: ${n((s = t.price.withVat) == null ? void 0 : s.currencyString)}</p>` : ""}${this.renderExtraProperties(t.customData, r)}`;
  }
  renderGiftcards(e) {
    const t = Array.isArray(e.giftcards) ? e.giftcards : [];
    return t.length ? `<div class="ekmOrderGiftcards"><h4>Gift cards</h4><ul class="ekmOrderGiftcards__list">${t.map((r) => {
      const i = H(r), o = Z(r);
      return `<li><div class="ekmOrderGiftcards__summary"><strong>${n(r.code)}</strong><strong>${n(this.formatGiftcardAmount(r.amount, e))}</strong><span>${n(i.status)}</span></div>
        <p>${n(i.expiryLabel)}: ${n(i.expiry)}</p>
        ${i.warning ? `<p role="alert">${n(i.warning)}</p>` : ""}
        <div class="ekmOrderGiftcards__actions">${this.orderActions.filter((s) => s.giftcardCode != null && s.giftcardCode === r.code).map((s) => this.renderAction(s)).join("")}</div>
        <details><summary>More details</summary><dl>${o.map((s) => `<dt>${n(s.label)}</dt><dd>${n(s.value)}</dd>`).join("")}</dl></details></li>`;
    }).join("")}</ul></div>` : "";
  }
  formatGiftcardAmount(e, t) {
    var o, s, d, l;
    const r = ((s = (o = t.storeInfo) == null ? void 0 : o.currency) == null ? void 0 : s.currencyValue) || "en-US", i = ((l = (d = t.storeInfo) == null ? void 0 : d.currency) == null ? void 0 : l.isoCurrencySymbol) || t.currency;
    try {
      return new Intl.NumberFormat(r, { style: "currency", currency: i }).format(e);
    } catch {
      return String(e);
    }
  }
  get recoveryBusy() {
    return !!this.recoveryOperation || this.recoveryLoading || this.recoveryError || this.recoveryRefreshError || !!this.executingActionKey || this.releasingOrderLock || this.orderLockLoading || this.orderLockError || this.orderActionsLoading || this.orderActionsError;
  }
  renderGiftcardRecovery() {
    var t;
    if (((t = this.recoveryAttempts) == null ? void 0 : t.length) === 0 && !this.recoveryLoading && !this.recoveryError && !this.recoveryOperation && !this.recoveryMessage && !this.recoveryActionError && !this.recoveryRefreshError && !this.orderActionsError)
      return "";
    const e = (r, i) => `<dt>${n(r)}</dt><dd>${n(i ?? "Not recorded")}</dd>`;
    return `<section class="ekmOrderTracking ekmGiftcardRecovery"><h4>Failed gift-card preparation</h4>
      <p>Recovery history for failed checkout preparation, not accepted payment. These actions do not refund a payment or retry checkout.</p>
      ${this.recoveryMessage ? `<p role="status">${n(this.recoveryMessage)}</p>` : ""}
      ${this.recoveryActionError ? `<p role="alert">${n(this.recoveryActionError)}</p>` : ""}
      ${this.recoveryRefreshError ? '<p role="alert">We could not refresh all order details. Reopen the order before taking another recovery action.</p>' : ""}
      ${this.orderActionsError ? '<p role="alert">We could not refresh order actions. Recovery actions are unavailable. Reopen the order to try again.</p>' : ""}
      ${this.recoveryLoading ? "<p>Loading recovery history...</p>" : ""}
      ${this.recoveryError ? '<p role="alert">We could not load gift-card recovery history. Recovery actions are unavailable. Reopen the order to try again.</p>' : ""}
      ${this.recoveryAttempts === void 0 && !this.recoveryLoading && !this.recoveryError ? "<p>Recovery history has not been checked. Recovery actions are unavailable.</p>" : ""}
      ${(this.recoveryAttempts || []).map((r) => {
      const i = r.canReconcile && !!r.reconcileToken;
      return `<article><h5>Preparation attempt: ${n(r.attemptId)}</h5>
          <dl>${e("Created", O(r.createdUtc))}${e("Recovery state", r.state)}${e("Reason", r.reason)}</dl>
          ${r.hasUnknownEffects ? '<p role="alert">Unknown provider effects need reconciliation. A missing claim reference is not evidence that no hold exists. Do not release by card number or amount.</p>' : ""}
          ${r.claims.map((o) => {
        const s = o.canRelease && !!o.releaseToken;
        return `<div class="ekmGiftcardRecovery__claim"><dl>${e("Masked card", o.cardReference)}${e("Claim reference", o.claimReference)}${e("Provider", o.provider)}${e("Account", o.account)}${e("Requested amount", `${o.requestedAmount} ${o.currency}`)}${e("Raw returned amount", o.rawReturnedAmount)}${e("Returned amount type", o.returnedAmountType)}${e("Raw expiry", o.rawExpiry)}${e("Cleanup status", o.cleanupState)}</dl>
              ${o.claimReference ? `<button type="button" class="btn-outline" data-action="release-giftcard-recovery" data-attempt-id="${n(r.attemptId)}" data-claim-id="${n(o.id)}" ${!s || this.recoveryBusy ? "disabled" : ""}>Release failed-preparation hold${this.recoveryOperation === `release:${r.attemptId}:${o.id}` ? " (Working...)" : ""}</button>` : "<p>Missing claim reference: needs reconciliation. No release action is available.</p>"}
              <p>${n(s && o.claimReference ? "Release allowed after confirmation." : o.unavailableReason || "Release unavailable; check reconciliation requirements.")}</p></div>`;
      }).join("")}
          ${!r.claims.length && r.hasUnknownEffects ? "<p>No known claim to release. Reconcile unknown provider effects.</p>" : ""}
          <button type="button" class="btn-outline" data-action="reconcile-giftcard-recovery" data-attempt-id="${n(r.attemptId)}" ${!i || this.recoveryBusy ? "disabled" : ""}>Reconcile failed preparation${this.recoveryOperation === `reconcile:${r.attemptId}` ? " (Working...)" : ""}</button>
          <p>${n(i ? "Reconciliation allowed after confirmation." : r.unavailableReason || "Reconciliation unavailable.")}</p>
        </article>`;
    }).join("")}</section>`;
  }
  renderDesignOrderLines(e) {
    const t = Array.isArray(e.orderLines) ? e.orderLines : [], r = t.reduce((i, o) => i + (Number(o.quantity) || 0), 0);
    return `<div class="ekom-card-heading ekom-lines-heading"><h3>Order lines</h3><span>${t.length} ${t.length === 1 ? "line" : "lines"} · ${r} ${r === 1 ? "item" : "items"}</span><button type="button" data-action="open-order-line-editor">Add order line</button></div><div class="ekom-lines-scroll"><table class="ekom-design-lines"><thead><tr><th>Product</th><th>Quantity</th><th>Unit price</th><th>Discount</th><th>Total</th><th><span class="ekom-sr-only">Actions</span></th></tr></thead><tbody>${t.map((i) => {
      var o, s, d, l, m, f, u, p, g, y;
      return `<tr><td><div class="ekmOrderLineProduct">${Q(i)}<div>${Y(i.product)}<small>${n((o = i.product) == null ? void 0 : o.sku)}</small>${i.variant ? `<small>${n(i.variant.title)}</small>` : ""}${J(i)}</div></div></td><td>${n(i.quantity)}</td><td>${n((l = (d = (s = i.product) == null ? void 0 : s.price) == null ? void 0 : d.withVat) == null ? void 0 : l.currencyString)}</td><td>${n((f = (m = i.amount) == null ? void 0 : m.discountAmount) == null ? void 0 : f.currencyString)}</td><td><strong>${n((p = (u = i.amount) == null ? void 0 : u.withVat) == null ? void 0 : p.currencyString)}</strong></td><td><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${n(i.key)}" data-product-title="${n((g = i.product) == null ? void 0 : g.title)}" ${this.removingOrderLineId === i.key ? "disabled" : ""} aria-label="Remove ${n((y = i.product) == null ? void 0 : y.title)}">×</button></td></tr>`;
    }).join("")}</tbody></table></div>`;
  }
  renderOrderLines(e) {
    var o, s, d, l, m, f, u;
    const t = Array.isArray(e.orderLines) ? e.orderLines : [], r = typeof e.coupon == "string" ? e.coupon.trim() : "", i = r ? `Discount (${r})` : "Discount";
    return `
      <div style="align-items:center; display:flex; gap:10px; justify-content:space-between;"><h4>Order Details</h4><button type="button" class="btn-outline" data-action="open-order-line-editor">Add order line</button></div>
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed">Product</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Unit Price (inc VAT)</div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">Total (inc VAT)</div></div></div>
        <div class="umb-table-body">
          ${t.map((p) => {
      var g, y, S, A, I, L, w, $, C, j, U, V, B, G;
      return `<div class="umb-table-row"><div class="umb-table-cell"><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${n(p.key)}" data-product-title="${n((g = p.product) == null ? void 0 : g.title)}" ${this.removingOrderLineId === p.key ? "disabled" : ""} aria-label="Remove ${n((y = p.product) == null ? void 0 : y.title)}" title="Remove order line">&#128465;</button></div><div class="umb-table-cell not-fixed"><div class="ekmOrderLineProduct">${Q(p)}<div class="ekmOrderLineProduct__details"><span>${Y(p.product)}${(A = (S = p.product) == null ? void 0 : S.sku) != null && A.trim() ? ` (${n(p.product.sku)})` : ""}</span>${p.variant ? `<small style="display:block; margin-top:3px;">${n(p.variant.title)} ${(I = p.variant.sku) != null && I.trim() ? `(${n(p.variant.sku)})` : ""}</small>` : ""}${J(p)}</div></div></div><div class="umb-table-cell">${n(p.quantity)}</div><div class="umb-table-cell">${n(($ = (w = (L = p.product) == null ? void 0 : L.price) == null ? void 0 : w.withVat) == null ? void 0 : $.currencyString)}</div><div class="umb-table-cell">${n((j = (C = p.amount) == null ? void 0 : C.vat) == null ? void 0 : j.currencyString)}</div><div class="umb-table-cell">-${n((V = (U = p.amount) == null ? void 0 : U.discountAmount) == null ? void 0 : V.currencyString)}</div><div class="umb-table-cell"><strong>${n((G = (B = p.amount) == null ? void 0 : B.withVat) == null ? void 0 : G.currencyString)}</strong></div></div>`;
    }).join("")}
        </div>
        <div class="umb-table-footer">
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Sub Total (inc VAT)</div><div class="umb-table-cell">${n((s = (o = e.subTotal) == null ? void 0 : o.withVat) == null ? void 0 : s.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">${n(i)}</div><div class="umb-table-cell">-${n((d = e.discountAmount) == null ? void 0 : d.currencyString)}</div></div>
          ${e.shippingProvider ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Shipping Total</div><div class="umb-table-cell">${n((m = (l = e.shippingProvider.price) == null ? void 0 : l.withVat) == null ? void 0 : m.currencyString)}</div></div>` : ""}
          ${this.renderGiftcardTotal(e)}
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">${n((f = e.chargedVat) == null ? void 0 : f.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Total</div><div class="umb-table-cell"><strong>${n((u = e.chargedAmount) == null ? void 0 : u.currencyString)}</strong></div></div>
        </div>
      </div>
    `;
  }
  renderGiftcardTotal(e) {
    const r = (Array.isArray(e.giftcards) ? e.giftcards : []).reduce((i, o) => i + (o.amount > 0 && (o.claimed || !o.validUntil || Date.parse(o.validUntil) > Date.now()) ? o.amount : 0), 0);
    return r ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell" title="Payable total already includes applicable gift cards; eligible value may exceed the order total.">Gift cards (eligible value)</div><div class="umb-table-cell">${n(this.formatGiftcardAmount(r, e))}</div></div>` : "";
  }
  renderTracking(e) {
    const t = e.tracking;
    return Le(t) ? `<div class="ekmOrderTracking"><h4>Tracking</h4>${Ee(t)}</div>` : '<div class="ekmOrderTracking"><h4>Tracking</h4><p>No tracking data was captured for this order.</p></div>';
  }
  renderConsent(e) {
    const t = e.consent;
    return Ae(t) ? `<div class="ekmOrderTracking"><h4>Consent</h4>${b("Resolved", O(t.resolvedAtUtc))}${b("Source", t.source)}${b("Analytics", ee(t.analytics))}${b("Marketing", ee(t.marketing))}</div>` : '<div class="ekmOrderTracking"><h4>Consent</h4><p>No consent data was captured for this order.</p></div>';
  }
  renderActivityLogs() {
    return this.orderLogsLoading ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Loading activity...</p></div>' : this.orderLogsError ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Unable to load activity log.</p></div>' : this.orderLogs.length ? `<div class="ekmOrderActivityLog"><h4>Activity log</h4><div class="ekom-log-scroll"><table class="ekom-log-table"><thead><tr><th>Date</th><th>Type</th><th>Message</th></tr></thead><tbody>${this.orderLogs.map((t, r) => {
      const i = t.message ?? "", o = this.activityLogExpandedIndexes.has(r), s = this.canExpandActivityLog(i) ? `<button type="button" class="btn-reset ekmOrderActivityLog__toggle" data-action="toggle-activity-log" data-log-index="${r}">${o ? "Show less" : "Show more"}</button>` : "";
      return `<tr><td class="ekmOrderActivityLog__date">${n(O(t.date))}</td><td><span class="ekom-log-type ${this.getActivityLogTypeClass(t)}">${t.logType === z ? "Success" : t.logType === W ? "Alert" : "Info"}</span></td><td><div class="ekmOrderActivityLog__message${o ? " ekmOrderActivityLog__message--expanded" : ""}">${n(i)}</div>${s}</td></tr>`;
    }).join("")}</tbody></table></div></div>` : '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>No activity yet.</p></div>';
  }
  getActivityLogTypeClass(e) {
    switch (e.logType) {
      case z:
        return "ekmOrderActivityLog__icon--success";
      case W:
        return "ekmOrderActivityLog__icon--alert";
      default:
        return "ekmOrderActivityLog__icon--info";
    }
  }
  canExpandActivityLog(e) {
    return e.length > ke;
  }
  renderStateDot(e) {
    return `<span class="ekom-state-dot" data-state="${n(e || "")}" aria-hidden="true"></span>`;
  }
  renderOrderActions() {
    const e = this.orderActions.filter((t) => t.giftcardCode == null);
    return `<div class="ekom-detail-actions"><button type="button" data-action="print-order" class="ekom-print-button" aria-label="Print order" title="Print order"><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 9V3h12v6"></path><rect x="3" y="9" width="18" height="8" rx="2"></rect><path d="M6 14h12v7H6z"></path></svg></button>${e.length ? '<span class="ekom-action-divider" aria-hidden="true"></span>' : ""}${e.map((t) => this.renderAction(t)).join("")}${this.orderActionsLoading ? '<span role="status">Loading actions...</span>' : ""}</div>`;
  }
  renderAction(e) {
    const t = this.executingActionKey === e.key;
    return `<span style="display:inline-block; margin:4px;"><button type="button" class="${e.look === "primary" ? "btn-success" : "btn-outline"}" data-action="execute-order-action" data-action-key="${n(e.key)}" ${e.enabled === !1 || this.executingActionKey || this.releasingOrderLock || this.recoveryOperation ? "disabled" : ""}>${n(e.label)}${t ? " (Working...)" : ""}</button>${e.enabled === !1 && e.disabledReason ? `<small style="display:block;">${n(e.disabledReason)}</small>` : ""}</span>`;
  }
  renderOrderLockStatus() {
    const e = this.orderLockStatus, t = this.orderLockWarning ? `<p role="alert">${n(this.orderLockWarning)}</p>` : "", r = this.orderLockError ? '<p role="alert">We could not check whether checkout is locked. Reopen the order to try again.</p>' : "", i = this.orderLockReleaseError ? `<p role="alert">${n(this.orderLockReleaseError)}</p>` : "";
    if (!(e != null && e.hasLock))
      return `${t}${r}${i}${this.orderLockLoading ? "<p>Checking checkout...</p>" : ""}`;
    const o = !e.canRelease || !e.releaseToken ? e.releaseUnavailableReason || "This checkout lock cannot be released right now." : "";
    return `<section class="ekmOrderTracking"><h4>Checkout is locked</h4>${t}${r}${i}<p>${e.reconciliationRequired && !e.paymentOperationLocked && !e.preparationLocked ? "The previous payment needs checking before the customer can pay or edit their basket." : "A previous checkout did not finish. This prevents the customer from paying or editing their basket."}</p><p>If you have checked that no payment is being processed, Release Lock resets checkout and lets the customer try again.</p><button type="button" class="btn-outline" data-action="release-order-lock" ${o || this.orderLockLoading || this.orderLockError || this.releasingOrderLock || this.executingActionKey || this.recoveryOperation ? "disabled" : ""}>Release Lock</button>${o ? `<p>${n(o)}</p>` : ""}<details><summary>Technical details</summary><ul>${e.paymentOperationLocked ? "<li>Payment operation ownership retained.</li>" : ""}${e.preparationLocked ? "<li>Checkout preparation ownership retained.</li>" : ""}${e.reconciliationRequired ? "<li>Reconciliation required.</li>" : ""}</ul><p>Attempt ID: ${n(e.attemptId || "Not available")}</p><p>Attempt state: ${n(e.attemptState || "Not available")}</p></details></section>`;
  }
  renderCustomerEditor() {
    const e = this.customerEditModel;
    return e ? `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel ekom-editor-customer" role="dialog" aria-modal="true" aria-label="Edit customer information"><div class="ekmOverlay__header"><h3>Edit customer information</h3><button class="btn-reset" type="button" aria-label="Close edit customer information" data-action="close-customer-editor" ${this.customerSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><div class="ekmSplit"><div class="ekmSplit__column"><h4>Billing</h4>${this.renderCustomerFields(e.customer, "customer")}</div><div class="ekmSplit__column"><h4>Shipping</h4>${this.renderCustomerFields(e.shipping, "shipping")}</div></div></div><div class="ekom-editor-footer"><button class="btn-outline" type="button" data-action="close-customer-editor" ${this.customerSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-customer-information" ${this.customerSaving ? "disabled" : ""}>${this.customerSaving ? "Saving..." : "Save customer information"}</button></div></div></div>` : "";
  }
  renderCustomerFields(e, t) {
    return e.map((r) => `<label class="control-group">${n(r.label)} ${r.isExtra ? `<small>(${n(r.key)})</small>` : ""}<input type="text" data-customer-group="${t}" data-customer-key="${n(r.key)}" value="${n(r.value)}" ${this.customerSaving ? "disabled" : ""}></label>`).join("");
  }
  renderOrderLineEditor() {
    const e = this.orderLineEditModel;
    return e ? `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel ekom-editor-order-line" role="dialog" aria-modal="true" aria-label="Add order line"><div class="ekmOverlay__header"><h3>Add order line</h3><button class="btn-reset" type="button" aria-label="Close add order line" data-action="close-order-line-editor" ${this.orderLineSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><label class="control-group">Product ID<input type="text" data-order-line-field="productId" value="${n(e.productId)}" required ${this.orderLineSaving ? "disabled" : ""}></label><label class="control-group">Variant ID<input type="text" data-order-line-field="variantId" value="${n(e.variantId)}" ${this.orderLineSaving ? "disabled" : ""}></label><label class="control-group" style="padding-bottom:20px;">Quantity<input type="number" min="0.000001" step="any" data-order-line-field="quantity" value="${n(e.quantity)}" required ${this.orderLineSaving ? "disabled" : ""}></label></div><div class="ekom-editor-footer"><button class="btn-outline" type="button" data-action="close-order-line-editor" ${this.orderLineSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-order-line" ${this.orderLineSaving ? "disabled" : ""}>${this.orderLineSaving ? "Adding..." : "Add order line"}</button></div></div></div>` : "";
  }
  renderShippingProviderEditor() {
    var e;
    return `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel ekom-editor-shipping-provider" role="dialog" aria-modal="true" aria-label="Shipping provider"><div class="ekmOverlay__header"><h3>${(e = this.selectedOrder) != null && e.shippingProvider ? "Change shipping provider" : "Add shipping provider"}</h3><button class="btn-reset" type="button" aria-label="Close shipping provider" data-action="close-shipping-provider-editor" ${this.shippingProviderSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><label class="control-group">Shipping provider<select data-field="shippingProviderId" ${this.shippingProviderSaving ? "disabled" : ""}><option value="">Select a provider</option>${this.shippingProviders.map((t) => `<option value="${n(t.key)}" ${this.shippingProviderId === t.key ? "selected" : ""}>${n(t.title)}</option>`).join("")}</select></label>${this.shippingProviders.length ? "" : "<p>No shipping providers available for this store.</p>"}<div class="ekom-provider-data-heading"><h4>Provider data</h4><p>Stored on the order. Keys are kept as-is; new keys are prefixed <code>customshipping</code>.</p></div><div class="ekom-provider-field-labels" aria-hidden="true"><span>Key</span><span>Value</span></div><div class="ekom-provider-fields">${this.shippingCustomFields.map((t, r) => `<div class="ekom-provider-field-row"><input aria-label="Key ${r + 1}" type="text" data-shipping-key="${r}" value="${n(t.key)}" ${t.existing ? "readonly" : ""} ${this.shippingProviderSaving ? "disabled" : ""} placeholder="TrackingNumber" maxlength="100"><input aria-label="Value ${r + 1}" type="text" data-shipping-value="${r}" value="${n(t.value)}" ${this.shippingProviderSaving ? "disabled" : ""} maxlength="4096"></div>`).join("")}</div><button class="btn-outline" type="button" data-action="add-shipping-custom-field" ${this.shippingProviderSaving ? "disabled" : ""}>Add custom field</button></div><div class="ekom-editor-footer"><p>Changes apply to this order only.</p><button class="btn-outline" type="button" data-action="close-shipping-provider-editor" ${this.shippingProviderSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-shipping-provider" ${this.shippingProviderSaving || !this.shippingProviders.length ? "disabled" : ""}>${this.shippingProviderSaving ? "Saving..." : "Save shipping provider"}</button></div></div></div>`;
  }
  readShippingCustomFields() {
    this.shippingCustomFields = this.shippingCustomFields.map((e, t) => {
      var r, i;
      return {
        key: e.existing ? e.key : ((r = this.querySelector(`[data-shipping-key="${t}"]`)) == null ? void 0 : r.value) || "",
        value: ((i = this.querySelector(`[data-shipping-value="${t}"]`)) == null ? void 0 : i.value) || "",
        existing: e.existing
      };
    });
  }
  bindEvents() {
    var e, t, r, i, o;
    (e = this.querySelector("[data-review-comment]")) == null || e.addEventListener("input", (s) => {
      this.reviewComment = s.target.value;
    }), (t = this.querySelector(".ekom-review-overlay")) == null || t.addEventListener("click", (s) => {
      s.target === s.currentTarget && !this.reviewSaving && (this.activeReview = void 0, this.render());
    }), (r = this.querySelector("[data-review-resolved]")) == null || r.addEventListener("change", (s) => {
      var l;
      this.reviewResolved = s.target.checked, this.reviewComment = ((l = this.querySelector("[data-review-comment]")) == null ? void 0 : l.value) || "";
      const d = this.querySelector('[data-action="save-review"]');
      d && (d.textContent = this.reviewResolved ? "Mark as reviewed" : "Save note");
    }), this.querySelectorAll(".ekom-row-fulfillment").forEach((s) => {
      s.addEventListener("toggle", () => {
        if (!s.open) return;
        const d = s.querySelector(".ekom-row-fulfillment-menu");
        d.style.top = "", d.style.bottom = "", d.style.maxHeight = "", d.style.overflowY = "auto";
        const l = s.getBoundingClientRect(), m = window.innerHeight - l.bottom - 22, f = l.top - 86;
        d.offsetHeight > m && f > m ? (d.style.top = "auto", d.style.bottom = "calc(100% + 6px)", d.style.maxHeight = `${Math.max(120, f)}px`) : d.style.maxHeight = `${Math.max(120, m)}px`;
      }), s.addEventListener("keydown", (d) => {
        var l;
        d.key === "Escape" && (d.preventDefault(), d.stopPropagation(), s.open = !1, (l = s.querySelector("summary")) == null || l.focus());
      });
    }), this.querySelectorAll("[data-poll-seconds]").forEach((s) => s.addEventListener("click", () => this.setPolling(Number(s.dataset.pollSeconds)))), (i = this.querySelector(".ekom-polling")) == null || i.addEventListener("keydown", (s) => {
      var l;
      if (s.key !== "Escape") return;
      const d = s.currentTarget;
      d.open = !1, (l = d.querySelector("summary")) == null || l.focus(), s.stopPropagation();
    }), pe(this, () => {
      this.page = 1, this.loadOrders();
    }), this.querySelectorAll('[data-action="select-order-tab"]').forEach((s) => {
      s.addEventListener("keydown", (d) => {
        if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(d.key)) return;
        d.preventDefault();
        const l = ["order", "activity", "tracking"], m = l.indexOf(this.orderTab), f = d.key === "Home" ? 0 : d.key === "End" ? l.length - 1 : (m + (d.key === "ArrowRight" ? 1 : -1) + l.length) % l.length;
        this.selectOrderTab(l[f]);
      });
    }), this.querySelectorAll(".ekmOrderLineThumbnail img").forEach((s) => {
      const d = () => {
        s.hidden = !0;
      };
      s.addEventListener("error", d, { once: !0 }), s.complete && s.naturalWidth === 0 && d();
    }), this.overlay === "order" && ((o = this.querySelector(".ekmOverlay")) == null || o.addEventListener("click", (s) => {
      s.target === s.currentTarget && !this.customerEditorOpen && !this.orderLineEditorOpen && !this.shippingProviderEditorOpen && !this.activeReview && this.closeOverlay();
    })), this.querySelectorAll("[data-action]").forEach((s) => {
      s instanceof HTMLSelectElement ? s.addEventListener("change", (d) => void this.handleAction(d)) : s.addEventListener("click", (d) => void this.handleAction(d));
    }), this.querySelectorAll("[data-field]").forEach((s) => {
      s.addEventListener("change", (d) => void this.handleFieldChange(d)), s.dataset.field === "query" && s.addEventListener("input", (d) => this.handleSearchInput(d));
    });
  }
  async handleAction(e) {
    var i, o, s, d, l, m, f;
    const t = e.currentTarget, r = t.dataset.action;
    if (r === "open-review") {
      this.activeReview = this.reviews.find((u) => u.id === t.dataset.reviewId), this.reviewComment = "", this.reviewResolved = !0, this.reviewError = "", this.render(), (i = this.querySelector("[data-review-comment]")) == null || i.focus();
      return;
    }
    if (r === "close-review") {
      this.reviewSaving || (this.activeReview = void 0, this.render());
      return;
    }
    if (r === "save-review") {
      await this.saveReview();
      return;
    }
    if (r === "select-order-view") {
      v.filters.orderStatus = t.dataset.view || "AllOrders", this.page = 1, await this.loadOrders();
      return;
    }
    if (r === "clear-order-filters") {
      v.filters.orderStatus = "AllOrders", v.filters.query = "", this.page = 1, await this.loadOrders();
      return;
    }
    if (r === "order-state-command") {
      await this.changeState(t.dataset.command || "");
      return;
    }
    if (r === "copy-provider-value") {
      try {
        await navigator.clipboard.writeText(t.dataset.copy || ""), this.showSuccess("Copied to clipboard.");
      } catch {
        this.showError("Could not copy this value. Select the text to copy it manually.");
      }
      return;
    }
    if (r === "copy-order-id") {
      const u = (o = this.selectedOrder) == null ? void 0 : o.uniqueId;
      if (!u) return;
      try {
        await navigator.clipboard.writeText(u), this.showNotification("positive", "Copied", "Order ID copied to clipboard.");
      } catch {
        this.showError("Could not copy the order ID. You can select and copy it from the heading.");
      }
      return;
    }
    if (r === "set-payment") {
      await this.changePayment(t);
      return;
    }
    if (r === "set-row-fulfillment") {
      await this.changeRowFulfillment(t);
      return;
    }
    if (r === "choose-fulfillment") {
      if (t.disabled) return;
      const u = t.closest("details"), p = t.dataset.value || "", g = this.querySelector('[data-field="fulfillmentState"]');
      g && (g.value = p), u.querySelector("[data-fulfillment-label]").textContent = p.replace(/([a-z])([A-Z])/g, "$1 $2"), u.querySelector("summary .ekom-state-dot").dataset.state = p, u.querySelectorAll("button[data-value]").forEach((y) => {
        const S = y.dataset.value === p;
        y.setAttribute("aria-pressed", String(S)), y.querySelector(".ekom-status-check").textContent = S ? "✓" : "";
      }), u.removeAttribute("open"), (s = u.querySelector("summary")) == null || s.focus();
      return;
    }
    if (r === "save-fulfillment") {
      const u = (d = this.querySelector('[data-field="fulfillmentState"]')) == null ? void 0 : d.value;
      u && u !== ((m = (l = this.selectedOrder) == null ? void 0 : l.status) == null ? void 0 : m.fulfillmentStatus) && await this.changeState(u === "Fulfilled" ? "Fulfill" : u);
      return;
    }
    if (r === "select-order-tab") {
      this.selectOrderTab(t.dataset.tab || "order");
      return;
    }
    if (r === "set-page") {
      this.page = Number(t.dataset.page || 1), await this.loadOrders();
      return;
    }
    if (r === "open-filter" || r === "open-export") {
      this.overlay = r === "open-filter" ? "filter" : "export", this.render();
      return;
    }
    if (r === "close-overlay") {
      this.closeOverlay();
      return;
    }
    if (r === "apply-filter") {
      this.applyFilterOverlay(), this.overlay = "", this.page = 1, await this.loadOrders();
      return;
    }
    if (r === "export-orders") {
      await this.exportOrders();
      return;
    }
    if (r === "view-order") {
      await this.openOrder(t.dataset.orderId || "");
      return;
    }
    if (r === "save-overlay-status") {
      await this.saveOverlayStatus();
      return;
    }
    if (r === "change-row-status") {
      await this.changeRowStatus(t);
      return;
    }
    if (r === "print-order") {
      if (this.selectedOrder)
        try {
          await me(this.renderPrintableOrder(this.selectedOrder), `Order ${this.selectedOrder.referenceId}`);
        } catch {
          this.showError("Unable to prepare the order for printing. Please try again.");
        }
      return;
    }
    if (r === "toggle-activity-log") {
      const u = Number(t.dataset.logIndex);
      this.activityLogExpandedIndexes.has(u) ? this.activityLogExpandedIndexes.delete(u) : this.activityLogExpandedIndexes.add(u), this.renderPreservingOverlayScroll();
      return;
    }
    if (r === "release-order-lock") {
      await this.releaseOrderLock();
      return;
    }
    if (r === "release-giftcard-recovery" || r === "reconcile-giftcard-recovery") {
      await this.executeGiftcardRecovery(t.dataset.attemptId || "", r === "release-giftcard-recovery" ? t.dataset.claimId || "" : void 0);
      return;
    }
    if (r === "execute-order-action") {
      await this.executeOrderAction(t.dataset.actionKey || "");
      return;
    }
    if (r === "open-customer-editor") {
      this.openCustomerEditor();
      return;
    }
    if (r === "close-customer-editor") {
      this.customerEditorOpen = !1, this.customerEditModel = void 0, this.render();
      return;
    }
    if (r === "save-customer-information") {
      await this.saveCustomerInformation();
      return;
    }
    if (r === "open-order-line-editor") {
      this.orderLineEditModel = { productId: "", variantId: "", quantity: "1" }, this.orderLineEditorOpen = !0, this.render();
      return;
    }
    if (r === "close-order-line-editor") {
      this.orderLineSaving || (this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.render());
      return;
    }
    if (r === "save-order-line") {
      await this.saveOrderLine();
      return;
    }
    if (r === "open-shipping-provider-editor") {
      await this.openShippingProviderEditor();
      return;
    }
    if (r === "close-shipping-provider-editor") {
      this.shippingProviderSaving || (this.shippingProviderEditorOpen = !1, this.render());
      return;
    }
    if (r === "save-shipping-provider") {
      await this.saveShippingProvider();
      return;
    }
    if (r === "add-shipping-custom-field") {
      this.readShippingCustomFields(), this.shippingProviderId = ((f = this.querySelector('[data-field="shippingProviderId"]')) == null ? void 0 : f.value) || "", this.shippingCustomFields.push({ key: "", value: "", existing: !1 }), this.render();
      return;
    }
    r === "remove-order-line" && await this.removeOrderLine(t.dataset.orderLineId || "", t.dataset.productTitle || "this order line");
  }
  async handleFieldChange(e) {
    const t = e.currentTarget, r = t.dataset.field;
    if (r === "includeOrderLines") {
      this.exportIncludeOrderLines = t.checked, this.render();
      return;
    }
    if (r === "notifyOrderStatus") {
      this.notifyOrderStatusOverride = t.checked;
      return;
    }
    r === "orderStatusOverlay" || r === "query" || !r || !(r in v.filters) || (v.filters[r] = t.value, this.page = 1, r === "store" && await this.loadFilterProviders(!0), await this.loadOrders());
  }
  handleSearchInput(e) {
    const t = e.currentTarget;
    v.filters.query = t.value, this.page = 1, window.clearTimeout(this.searchTimer), this.searchTimer = window.setTimeout(() => void this.loadOrders(), 700);
  }
  applyFilterOverlay() {
    const e = Array.from(this.querySelectorAll("[data-status-dimension]")).map((t) => t.value);
    e.length === 3 && (v.filters.orderStatus = e.some(Boolean) ? `states:${e.join(";")}` : "AllOrders"), this.querySelectorAll("[data-filter-field]").forEach((t) => {
      const r = t.dataset.filterField;
      r && r in v.filters && (v.filters[r] = r === "giftcardCode" ? t.value.trim() : t.value);
    });
  }
  async exportOrders() {
    if (this.result.count) {
      this.exporting = !0, this.render();
      try {
        const e = await this.api.exportOrders(v.filters, this.result.count, this.exportIncludeOrderLines);
        ve(e, this.exportIncludeOrderLines ? "orders-with-orderlines.csv" : "orders.csv"), this.overlay = "";
      } catch (e) {
        this.showError(k(e, "Error exporting orders."));
      } finally {
        this.exporting = !1, this.render();
      }
    }
  }
  async openOrder(e) {
    if (!e)
      return;
    this.returnFocusOrderId = e, this.orderTab = "order";
    const t = ++this.orderRequestId;
    ++this.lockRequestId, ++this.recoveryRequestId, this.recoveryAttempts = void 0, this.recoveryLoading = !1, this.recoveryError = !1, this.recoveryOperation = "", this.recoveryMessage = "", this.recoveryActionError = "", this.recoveryRefreshError = !1, this.recoveryScrollTop = void 0, this.notifyOrderStatusOverride = void 0, this.selectedOrder = void 0, this.orderActions = [], this.orderActionsError = !1, this.executingActionKey = "", this.orderLockStatus = void 0, this.releasingOrderLock = !1, this.orderLockWarning = "", this.orderLockReleaseError = "", this.orderLockError = !1, this.orderLockLoading = !1, this.render();
    try {
      const r = await this.api.orderInfo(e);
      if (t !== this.orderRequestId) return;
      this.selectedOrder = r, this.reviews = [], this.activeReview = void 0, this.reviewError = "", this.overlay = "order", this.orderLogs = [], this.orderLogsError = !1, this.activityLogExpandedIndexes.clear(), this.render(), await Promise.all([this.loadOrderLogs(e), this.loadOrderActions(e), this.loadOrderLockStatus(e), this.loadGiftcardRecovery(e), this.loadOrderReviews(e)]);
    } catch (r) {
      if (t !== this.orderRequestId) return;
      this.showError(k(r, "Error on getting orderInfo."));
    }
  }
  async loadOrderReviews(e) {
    var t, r;
    try {
      const i = await this.api.orderReviews(e);
      if (((t = this.selectedOrder) == null ? void 0 : t.uniqueId) !== e) return;
      this.reviews = i, this.render();
    } catch (i) {
      if (((r = this.selectedOrder) == null ? void 0 : r.uniqueId) !== e) return;
      this.reviewError = `Could not load order reviews: ${String(i)}`, this.render();
    }
  }
  renderReviewAlert(e, t = !1) {
    return `<div class="ekom-review-alert"><span class="ekom-review-icon" aria-hidden="true">!</span><div><strong>${n(e.title)}</strong><small>${n(e.source)} · ${n(O(e.createdUtc))}</small><p>${n(e.message)}</p></div>${t ? `<button type="button" data-action="open-review" data-review-id="${n(e.id)}">Review</button>` : ""}</div>`;
  }
  renderReviews() {
    return `${this.reviewError && !this.activeReview ? `<p role="alert">${n(this.reviewError)}</p>` : ""}${this.reviews.map((e) => this.renderReviewAlert(e, !0)).join("")}`;
  }
  renderReviewDialog() {
    var e;
    return `<div class="ekmCustomerInformationModal ekom-review-overlay"><section class="ekmCustomerInformationModal__panel ekom-review-panel" role="dialog" aria-modal="true" aria-label="Review alert">
      <header><h2>Review alert · Order #${n((e = this.selectedOrder) == null ? void 0 : e.referenceId)}</h2><button type="button" data-action="close-review" aria-label="Close review" ${this.reviewSaving ? "disabled" : ""}>×</button></header>
      <div class="ekom-review-body">${this.renderReviewAlert(this.activeReview)}
      <label for="ekom-review-comment">What was done</label><textarea id="ekom-review-comment" data-review-comment maxlength="4000" rows="4" placeholder="Describe how this was resolved or what was checked." ${this.reviewSaving ? "disabled" : ""}>${n(this.reviewComment)}</textarea>
      <p class="ekom-muted">Saved to the activity log with your name and the time.</p>
      <label class="ekom-review-checkbox"><input type="checkbox" data-review-resolved ${this.reviewResolved ? "checked" : ""} ${this.reviewSaving ? "disabled" : ""}><span>Problem is resolved <small>— untick to log a note but keep the alert open</small></span></label>
      ${this.reviewError ? `<p role="alert">${n(this.reviewError)}</p>` : ""}</div>
      <footer><button type="button" data-action="close-review" ${this.reviewSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-review" ${this.reviewSaving ? "disabled" : ""}>${this.reviewSaving ? "Saving..." : this.reviewResolved ? "Mark as reviewed" : "Save note"}</button></footer>
    </section></div>`;
  }
  async saveReview() {
    var t, r;
    if (!this.activeReview || !this.selectedOrder || this.reviewSaving) return;
    const e = this.selectedOrder.uniqueId;
    if (this.reviewComment = ((t = this.querySelector("[data-review-comment]")) == null ? void 0 : t.value) || "", this.reviewResolved = ((r = this.querySelector("[data-review-resolved]")) == null ? void 0 : r.checked) ?? !0, !this.reviewResolved && !this.reviewComment.trim()) {
      this.reviewError = "Describe what was done before saving a note.", this.render();
      return;
    }
    this.reviewSaving = !0, this.reviewError = "", this.render();
    try {
      await this.api.reviewOrder(e, this.activeReview.id, this.reviewComment, this.reviewResolved), this.activeReview = void 0, await Promise.all([this.loadOrderReviews(e), this.loadOrderLogs(e), this.loadOrders()]);
    } catch (i) {
      this.reviewError = String(i);
    } finally {
      this.reviewSaving = !1, this.render();
    }
  }
  async loadOrderLogs(e) {
    const t = this.orderRequestId;
    this.orderLogsLoading = !0, this.orderLogsError = !1, this.stateSaving || this.render();
    try {
      const r = await this.api.orderLogs(e);
      if (t !== this.orderRequestId) return;
      this.orderLogs = r;
    } catch {
      if (t !== this.orderRequestId) return;
      this.orderLogs = [], this.orderLogsError = !0;
    } finally {
      if (t !== this.orderRequestId) return;
      this.orderLogsLoading = !1, this.stateSaving || this.render();
    }
  }
  async loadOrderActions(e) {
    const t = this.orderRequestId;
    this.orderActionsLoading = !0, this.orderActionsError = !1, this.stateSaving || this.render();
    try {
      const r = await this.api.orderActions(e);
      if (t !== this.orderRequestId) return;
      this.orderActions = r;
    } catch {
      if (t !== this.orderRequestId) return;
      this.orderActions = [], this.orderActionsError = !0;
    } finally {
      if (t !== this.orderRequestId) return;
      this.orderActionsLoading = !1, this.stateSaving || this.render();
    }
  }
  async loadOrderLockStatus(e) {
    const t = ++this.lockRequestId, r = this.orderRequestId, i = () => {
      var o;
      return t === this.lockRequestId && r === this.orderRequestId && e === ((o = this.selectedOrder) == null ? void 0 : o.uniqueId);
    };
    this.orderLockLoading = !0, this.orderLockError = !1, this.render();
    try {
      const o = await this.api.orderLockStatus(e);
      i() && (this.orderLockStatus = o);
    } catch {
      i() && (this.orderLockError = !0);
    } finally {
      i() && (this.orderLockLoading = !1, this.render());
    }
  }
  async loadGiftcardRecovery(e) {
    const t = ++this.recoveryRequestId, r = this.orderRequestId, i = () => {
      var o;
      return t === this.recoveryRequestId && r === this.orderRequestId && e === ((o = this.selectedOrder) == null ? void 0 : o.uniqueId);
    };
    this.recoveryLoading = !0, this.recoveryError = !1, this.render();
    try {
      const o = await this.api.giftcardRecovery(e);
      if (!Array.isArray(o) || o.some((s) => !Array.isArray(s.claims)))
        throw new Error("Invalid recovery history response.");
      i() && (this.recoveryAttempts = o);
    } catch {
      i() && (this.recoveryError = !0);
    } finally {
      i() && (this.recoveryLoading = !1, this.render());
    }
  }
  async executeGiftcardRecovery(e, t) {
    var u, p, g;
    const r = (u = this.selectedOrder) == null ? void 0 : u.uniqueId, i = (p = this.recoveryAttempts) == null ? void 0 : p.find((y) => y.attemptId === e);
    if (!r || !i || this.recoveryBusy) return;
    const o = t !== void 0, s = o ? i.claims.find((y) => y.id === t) : void 0, d = o ? s == null ? void 0 : s.releaseToken : i.reconcileToken;
    if (!d || (o ? !(s != null && s.canRelease) || !s.claimReference : !i.canReconcile)) return;
    const l = o ? `Release the failed-preparation hold for masked card ${s.cardReference}, claim ${s.claimReference}? This calls the provider through audited recovery. It does not refund an accepted payment or retry payment or checkout. Unknown effects may still need reconciliation.` : "Reconcile this failed gift-card preparation? The audited backend will check known claims and unknown provider effects. This does not refund an accepted payment or retry payment or checkout. Unresolved effects may remain blocked.";
    if (!window.confirm(l)) return;
    const m = this.orderRequestId, f = () => {
      var y;
      return m === this.orderRequestId && r === ((y = this.selectedOrder) == null ? void 0 : y.uniqueId);
    };
    this.recoveryOperation = o ? `release:${e}:${t}` : `reconcile:${e}`, this.recoveryScrollTop = (g = this.querySelector(".ekmOverlay")) == null ? void 0 : g.scrollTop, this.recoveryMessage = "", this.recoveryActionError = "", this.render();
    try {
      const y = o ? await this.api.releaseGiftcardRecovery(r, e, s.id, d) : await this.api.reconcileGiftcardRecovery(r, e, d);
      if (!f()) return;
      this.recoveryMessage = `${y.message} ${y.resolved ? "Failed preparation resolved." : "Recovery remains unresolved; check the updated history."}`;
    } catch (y) {
      if (!f()) return;
      this.recoveryActionError = k(y, "Recovery could not be confirmed. Check the updated history before trying again.");
    } finally {
      f() && (await Promise.all([
        this.loadGiftcardRecovery(r),
        this.loadOrderLockStatus(r),
        this.loadOrderActions(r),
        this.loadOrderLogs(r),
        this.api.orderInfo(r).then((y) => {
          f() && (this.selectedOrder = y);
        }).catch(() => {
          f() && (this.recoveryRefreshError = !0);
        })
      ]), f() && (this.recoveryOperation = "", this.render(), this.recoveryScrollTop = void 0));
    }
  }
  async releaseOrderLock() {
    var s;
    const e = (s = this.selectedOrder) == null ? void 0 : s.uniqueId, t = this.orderLockStatus;
    if (!e || !(t != null && t.hasLock) || !t.canRelease || !t.releaseToken || this.orderLockLoading || this.orderLockError || this.releasingOrderLock || this.executingActionKey || this.recoveryOperation || !window.confirm("Release this checkout lock? Confirm that no payment is currently being processed and the previous checkout has stopped. The customer's basket will be kept, and they can try checkout again. This does not cancel or refund a payment. Any gift-card hold that cannot be released must be checked separately.")) return;
    const r = this.orderRequestId, i = () => {
      var d;
      return r === this.orderRequestId && e === ((d = this.selectedOrder) == null ? void 0 : d.uniqueId);
    };
    this.releasingOrderLock = !0, this.orderLockReleaseError = "";
    let o = !1;
    this.render();
    try {
      const d = await this.api.releaseOrderLock(e, t.releaseToken);
      if (!i()) return;
      o = !0, this.orderLockWarning = d.warning || "", this.showSuccess(d.message);
      const [l] = await Promise.all([this.api.orderInfo(e), this.loadOrderLogs(e), this.loadOrderActions(e), this.loadOrderLockStatus(e), this.loadGiftcardRecovery(e), this.loadOrderReviews(e)]);
      i() && (this.selectedOrder = l);
    } catch (d) {
      if (!i()) return;
      this.orderLockReleaseError = o ? "Checkout was unlocked, but we could not refresh all order details. Reopen the order to check them." : k(d, "We could not confirm the checkout reset. Check the updated order before trying again."), await Promise.all([
        this.loadOrderLogs(e),
        this.loadOrderActions(e),
        this.loadOrderLockStatus(e),
        this.loadGiftcardRecovery(e),
        this.loadOrderReviews(e),
        this.api.orderInfo(e).then((l) => {
          i() && (this.selectedOrder = l);
        }).catch(() => {
        })
      ]);
    } finally {
      i() && (this.releasingOrderLock = !1, this.render());
    }
  }
  async saveOverlayStatus() {
    var i, o;
    if (!this.config || !((i = this.selectedOrder) != null && i.uniqueId))
      return;
    const e = this.querySelector('[data-field="orderStatusOverlay"]'), t = (e == null ? void 0 : e.value) || "", r = this.notifyOrderStatus;
    try {
      await this.api.changeOrderStatus(this.selectedOrder.uniqueId, t, r), this.selectedOrder.orderStatus = t, this.showSuccess("Order status updated."), await this.loadOrders(), await this.loadOrderLogs(this.selectedOrder.uniqueId);
    } catch (s) {
      e && (e.value = this.getOrderStatusValue((o = this.selectedOrder) == null ? void 0 : o.orderStatus)), this.showError(k(s, "Error updating order status."));
    }
  }
  showSuccess(e) {
    this.showNotification("positive", "Success", e);
  }
  showError(e) {
    this.showNotification("danger", "Error", e);
  }
  showNotification(e, t, r) {
    if (this.notificationContext) {
      this.notificationContext.peek(e, {
        data: {
          headline: t,
          message: r
        }
      });
      return;
    }
    e === "danger" && console.error(`${t}: ${r}`);
  }
  renderPreservingOverlayScroll() {
    const e = this.querySelector(".ekmOverlay"), t = (e == null ? void 0 : e.scrollTop) ?? 0;
    this.render(), requestAnimationFrame(() => {
      const r = this.querySelector(".ekmOverlay");
      r && (r.scrollTop = t);
    });
  }
  closeOverlay() {
    ++this.orderRequestId, ++this.lockRequestId, ++this.recoveryRequestId, this.recoveryOperation = "", this.recoveryScrollTop = void 0, this.overlay = "", this.selectedOrder = void 0, this.customerEditorOpen = !1, this.customerEditModel = void 0, this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.shippingProviderEditorOpen = !1, this.render();
    const e = Array.from(this.querySelectorAll('[data-action="view-order"]')).find((t) => t.dataset.orderId === this.returnFocusOrderId);
    e == null || e.focus(), this.returnFocusOrderId = void 0;
  }
  getOrderStatusValue(e) {
    const t = String(e ?? ""), r = v.statusList.find((i) => String(i.value ?? "") === t || String(i.enumValue ?? "") === t);
    return r ? K(r) : t;
  }
  async changeRowStatus(e) {
    const t = e.dataset.orderId || "";
    if (!(!this.config || !t))
      try {
        await this.api.changeOrderStatus(t, e.value, this.config.managerOrderStatusFireEventsByDefault ?? !0);
        const r = this.result.orders.find((i) => i.uniqueId === t);
        r && (r.orderStatusCol = e.value), this.showSuccess("Order status updated.");
      } catch (r) {
        this.showError(k(r, "Error updating order status.")), await this.loadOrders();
      }
  }
  async executeOrderAction(e) {
    var o;
    if (!((o = this.selectedOrder) != null && o.uniqueId) || !e || this.executingActionKey || this.releasingOrderLock || this.recoveryOperation)
      return;
    const t = this.orderActions.find((s) => s.key === e);
    if (!t || t.enabled === !1) return;
    const r = this.selectedOrder.uniqueId, i = this.orderRequestId;
    if (!(t != null && t.confirmMessage && !window.confirm(t.confirmMessage))) {
      this.executingActionKey = e, this.render();
      try {
        const s = await this.api.executeOrderAction(r, e), d = await s.blob(), l = s.headers.get("content-disposition") || "", m = s.headers.get("content-type") || "";
        if (l.toLowerCase().includes("filename=") || m.startsWith("application/pdf") || m.startsWith("application/octet-stream") || m.startsWith("image/"))
          window.open(URL.createObjectURL(d), "_blank");
        else {
          const u = await d.text();
          this.showSuccess(Ie(u));
        }
        if (i !== this.orderRequestId) return;
        const [f] = await Promise.all([this.api.orderInfo(r), this.loadOrderLockStatus(r), this.loadGiftcardRecovery(r)]);
        if (i !== this.orderRequestId) return;
        this.selectedOrder = f, await Promise.all([this.loadOrderLogs(r), this.loadOrderActions(r)]);
      } catch (s) {
        if (i !== this.orderRequestId) return;
        this.showError(k(s, "Order action failed."));
      } finally {
        if (i !== this.orderRequestId) return;
        this.executingActionKey = "", this.render();
      }
    }
  }
  openCustomerEditor() {
    var i, o;
    const e = this.selectedOrder;
    if (!e)
      return;
    const t = ((i = e.customerInformation) == null ? void 0 : i.customer) || {}, r = ((o = e.customerInformation) == null ? void 0 : o.shipping) || {};
    this.customerEditModel = {
      customer: te(t, ye).concat(re(t.properties, "customer")),
      shipping: te(r, be).concat(re(r.properties, "shipping"))
    }, this.customerEditorOpen = !0, this.render();
  }
  async saveCustomerInformation() {
    var e;
    if (!(!((e = this.selectedOrder) != null && e.uniqueId) || !this.customerEditModel || this.customerSaving)) {
      this.querySelectorAll("[data-customer-group]").forEach((t) => {
        var s;
        const r = t.dataset.customerGroup, i = t.dataset.customerKey, o = (s = this.customerEditModel) == null ? void 0 : s[r].find((d) => d.key === i);
        o && (o.value = t.value);
      }), this.customerSaving = !0, this.render();
      try {
        this.selectedOrder = await this.api.updateCustomerInformation(
          this.selectedOrder.uniqueId,
          ie(this.customerEditModel.customer),
          ie(this.customerEditModel.shipping)
        ), this.customerEditorOpen = !1, this.customerEditModel = void 0, this.showSuccess("Customer information updated."), await this.loadOrders();
      } catch (t) {
        this.showError(k(t, "Error updating customer information."));
      } finally {
        this.customerSaving = !1, this.render();
      }
    }
  }
  async saveOrderLine() {
    var o;
    if (!((o = this.selectedOrder) != null && o.uniqueId) || !this.orderLineEditModel || this.orderLineSaving)
      return;
    this.querySelectorAll("[data-order-line-field]").forEach((s) => {
      const d = s.dataset.orderLineField;
      d && (this.orderLineEditModel[d] = s.value);
    });
    const { productId: e, variantId: t, quantity: r } = this.orderLineEditModel, i = Number(r);
    if (!e.trim() || !Number.isFinite(i) || i <= 0) {
      this.showError("Product ID and a positive quantity are required.");
      return;
    }
    this.orderLineSaving = !0, this.render();
    try {
      this.selectedOrder = await this.api.addOrderLine(this.selectedOrder.uniqueId, e.trim(), t.trim() || void 0, i), this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.showSuccess("Order line added."), await this.refreshOrderAfterLineChange();
    } catch (s) {
      this.showError(k(s, "Error adding order line."));
    } finally {
      this.orderLineSaving = !1, this.render();
    }
  }
  async openShippingProviderEditor() {
    var e, t, r;
    if (this.selectedOrder)
      try {
        const i = ((e = this.selectedOrder.storeInfo) == null ? void 0 : e.alias) || this.selectedOrder.storeAlias;
        this.shippingProviders = await this.api.shippingProviders(i), this.shippingProviderId = ((t = this.selectedOrder.shippingProvider) == null ? void 0 : t.key) || "", this.shippingCustomFields = Object.entries(((r = this.selectedOrder.shippingProvider) == null ? void 0 : r.customData) || {}).filter(([o]) => o.toLowerCase().startsWith("customshipping")).map(([o, s]) => ({ key: o, value: q(s), existing: !0 })), this.shippingProviderEditorOpen = !0, this.render();
      } catch (i) {
        this.showError(k(i, "Error loading shipping providers."));
      }
  }
  async saveShippingProvider() {
    var i, o;
    if (!((i = this.selectedOrder) != null && i.uniqueId) || this.shippingProviderSaving)
      return;
    const e = ((o = this.querySelector('[data-field="shippingProviderId"]')) == null ? void 0 : o.value) || "";
    if (!this.shippingProviders.some((s) => s.key === e)) {
      this.showError("Select a shipping provider.");
      return;
    }
    this.readShippingCustomFields();
    const t = {}, r = /* @__PURE__ */ new Set();
    for (const s of this.shippingCustomFields) {
      const d = s.existing ? s.key : `customshipping${s.key.trim()}`;
      if (!d.slice(14).trim() || d.length > 100 || s.value.length > 4096 || r.has(d.toLowerCase())) {
        this.showError("Enter unique shipping custom data keys and valid values.");
        return;
      }
      r.add(d.toLowerCase()), t[d] = s.value;
    }
    this.shippingProviderSaving = !0, this.render();
    try {
      this.selectedOrder = await this.api.updateShippingProvider(this.selectedOrder.uniqueId, e, t), this.shippingProviderEditorOpen = !1, this.showSuccess("Shipping provider updated."), await this.refreshOrderAfterLineChange();
    } catch (s) {
      this.showError(k(s, "Error updating shipping provider."));
    } finally {
      this.shippingProviderSaving = !1, this.render();
    }
  }
  async removeOrderLine(e, t) {
    var r;
    if (!(!((r = this.selectedOrder) != null && r.uniqueId) || !e || this.removingOrderLineId || !window.confirm(`Remove ${t} from this order?`))) {
      this.removingOrderLineId = e, this.render();
      try {
        this.selectedOrder = await this.api.removeOrderLine(this.selectedOrder.uniqueId, e), this.showSuccess("Order line removed."), await this.refreshOrderAfterLineChange();
      } catch (i) {
        this.showError(k(i, "Error removing order line."));
      } finally {
        this.removingOrderLineId = "", this.render();
      }
    }
  }
  async refreshOrderAfterLineChange() {
    var t;
    if (!((t = this.selectedOrder) != null && t.uniqueId))
      return;
    const e = this.selectedOrder.uniqueId;
    await Promise.all([this.loadOrders(), this.loadOrderLogs(e), this.loadOrderActions(e)]);
  }
}
function k(a, h) {
  return a instanceof Error ? a.message : h;
}
function P(a) {
  return a != null && (typeof a != "string" || a.trim() !== "");
}
function D(a) {
  if (a === "code") return "Gift-card number";
  if (a === "claimId") return "Reservation reference";
  if (a === "transactionId") return "Transaction reference";
  if (a === "usedDate") return "Used on";
  if (a === "validUntil") return "Saved expiry";
  if (a === "claimDate") return "Payment claim date";
  const h = a.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ");
  return h.charAt(0).toUpperCase() + h.slice(1).toLowerCase().replace(/\bid\b/g, "ID");
}
function N(a) {
  if (!P(a)) return "Not recorded";
  if (typeof a == "boolean") return a ? "Yes" : "No";
  if (Array.isArray(a)) return a.length ? a.map(N).join("; ") : "Empty list";
  if (typeof a == "object") {
    const h = Object.entries(a);
    return h.length ? h.map(([e, t]) => `${D(e)}: ${N(t)}`).join("; ") : "Empty object";
  }
  return String(a);
}
function Z(a) {
  const h = ["code", "amount", "claimId", "claimDate", "transactionId", "usedDate", "claimed", "validUntil"];
  return [...h, ...Object.keys(a).filter((e) => !h.includes(e))].map((e) => ({ label: D(e), value: N(a[e]) }));
}
function H(a, h = Date.now()) {
  const e = P(a.claimId), t = P(a.validUntil), r = t ? Date.parse(String(a.validUntil)) : NaN, i = r > h, o = a.claimed === !0 || P(a.transactionId) || P(a.claimDate) || P(a.usedDate), s = o ? "Used" : e && i ? "Reserved" : e && r <= h ? "Reservation expired" : e || t ? "Needs checking" : "Selected";
  let d = "";
  !o && !e && t ? d = "An expiry time is saved, but the reservation reference is missing. Check the gift-card provider if an amount is still held." : !o && e && r <= h ? d = "The saved reservation expiry has passed. This does not confirm that the provider has released the hold." : !o && e && !t && (d = "No expiry time is saved. Check the gift-card provider if an amount is still held."), t && !Number.isFinite(r) && (d += `${d ? " " : ""}The saved expiry time could not be read. Check the gift-card provider.`);
  let l = "Not recorded";
  if (t && (l = Number.isFinite(r) ? new Intl.DateTimeFormat(void 0, { dateStyle: "medium", timeStyle: "long" }).format(new Date(r)) : String(a.validUntil), i)) {
    const m = Math.ceil((r - h) / 6e4), f = m < 60 ? m : m < 1440 ? Math.ceil(m / 60) : Math.ceil(m / 1440), u = m < 60 ? "minute" : m < 1440 ? "hour" : "day";
    l += ` (in about ${f} ${u}${f === 1 ? "" : "s"})`;
  }
  return { status: s, warning: d, expiryLabel: !o && e ? "Reservation ends" : "Saved expiry", expiry: l };
}
function T(a) {
  return a === "zipCode" ? "Zipcode" : a.charAt(0).toUpperCase() + a.slice(1);
}
function we(a) {
  return (/* @__PURE__ */ new Set(["shippingname", "shippingaddress", "shippingcity", "shippingcountry", "shippingemail", "shippingapartment", "shippingzipcode", "shippingphone", "customeremail", "customername", "customeraddress", "customerapartment", "customercity", "customercountry", "customerzipcode", "customerphone"])).has(a.toLowerCase());
}
function R(a) {
  return a.replace(/^customshipping/i, "").replace(/^custompayment/i, "").replace(/^shipping/i, "").replace(/^customer/i, "");
}
function _(a, h) {
  return Object.entries(a || {}).filter(([e, t]) => (!!t || h === "customshipping" && t === "") && e.toLowerCase().startsWith(h) && !we(e)).map(([e, t]) => [e, q(t)]);
}
function F(a) {
  return !!(a != null && a.name || a != null && a.email || a != null && a.address || a != null && a.apartment || a != null && a.city || a != null && a.country || a != null && a.zipCode || a != null && a.phone);
}
function Se(a) {
  return a.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/([A-Z])([A-Z][a-z])/g, "$1 $2").replace(/[_-]+/g, " ").trim().replace(/\b[a-z]/g, (h) => h.toUpperCase());
}
function M(a, h) {
  const e = a.indexOf("#"), t = e >= 0 ? a.slice(e) : "", r = e >= 0 ? a.slice(0, e) : a, i = r.indexOf("?"), o = i >= 0 ? r.slice(0, i) : r, s = new URLSearchParams(i >= 0 ? r.slice(i + 1) : "");
  return s.set("width", String(h)), s.set("format", "webp"), s.set("rmode", "pad"), s.set("rcolor", "ffffff"), `${o}?${s}${t}`.replace(/,/g, "%2C").replace(/ /g, "%20");
}
function Q(a) {
  var t, r;
  const e = [(t = a.variant) == null ? void 0 : t.images, (r = a.product) == null ? void 0 : r.images].flatMap((i) => Array.isArray(i) ? i : []).map((i) => {
    if (typeof (i == null ? void 0 : i.url) != "string" || !i.url.trim()) return "";
    try {
      const o = i.url.trim();
      return ["http:", "https:"].includes(new URL(o, "https://ekom.invalid/").protocol) ? o : "";
    } catch {
      return "";
    }
  }).find(Boolean);
  return `<span class="ekmOrderLineThumbnail" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8" cy="8" r="1.5"/><path d="m3 17 6-6 4 4 3-3 5 5"/></svg>${e ? `<picture><source srcset="${n(M(e, 56))} 1x, ${n(M(e, 112))} 2x"><img src="${n(M(e, 56))}" width="56" height="56" alt="" loading="lazy" decoding="async"></picture>` : ""}</span>`;
}
function Y(a) {
  const h = n(a == null ? void 0 : a.title), e = typeof (a == null ? void 0 : a.url) == "string" ? a.url.trim() : "";
  if (!e) return h;
  try {
    const t = new URL(e, "https://ekom.invalid/").protocol;
    if (t !== "http:" && t !== "https:") return h;
  } catch {
    return h;
  }
  return `<a href="${n(e)}" target="_blank" rel="noopener noreferrer">${h}</a>`;
}
function J(a) {
  var e;
  const h = ((e = a.orderLineInfo) == null ? void 0 : e.properties) || {};
  return Object.entries(h).filter(([, t]) => !!t).map(([t, r]) => `<small style="display:block; margin-top:3px;"><strong>${n(Oe(t))}</strong>: ${n(q(r))}</small>`).join("");
}
function Oe(a) {
  const h = a.replace(/^orderline/i, "").replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim();
  return h ? h.charAt(0).toUpperCase() + h.slice(1) : a;
}
function Le(a) {
  var h, e, t, r;
  return !!(a && (a.source || a.medium || a.campaign || a.term || a.content || a.clickId || a.clickIdType || a.landingUrl || a.referrer || a.captureMethod || a.capturedAtUtc || a.hasCookieSupport !== null && a.hasCookieSupport !== void 0 || (h = a.ga4) != null && h.clientId || (e = a.ga4) != null && e.sessionId || (t = a.meta) != null && t.fbp || (r = a.meta) != null && r.fbc || se(a.algolia)));
}
function Ee(a) {
  var o, s, d, l, m, f;
  const h = Object.entries(((o = a.ga4) == null ? void 0 : o.data) || {}), e = Object.entries(((s = a.meta) == null ? void 0 : s.data) || {}), t = a.algolia, r = Array.isArray(t == null ? void 0 : t.lines) ? t.lines : [], i = se(t) ? `<div class="ekmOrderTracking__provider"><h5>Algolia</h5>${b("User token", t.userToken)}${r.length ? `<ul>${r.map((u) => `<li class="ekmOrderTracking__wrap"><strong>Order line key</strong>: ${n(u.orderLineKey)}<br><strong>Query ID</strong>: ${n(u.queryId)}</li>`).join("")}</ul>` : ""}</div>` : "";
  return `<div class="ekmSplit"><div class="ekmSplit__column">${b("Captured", O(a.capturedAtUtc))}${b("Capture method", a.captureMethod)}${a.hasCookieSupport !== null && a.hasCookieSupport !== void 0 ? `<p>Cookie support: ${a.hasCookieSupport ? "Yes" : "No"}</p>` : ""}${b("Source", a.source)}${b("Medium", a.medium)}${b("Campaign", a.campaign)}${b("Term", a.term)}${b("Content", a.content)}${b("Click ID", a.clickId)}${b("Click ID Type", a.clickIdType)}${b("Landing URL", a.landingUrl)}${b("Referrer", a.referrer)}</div><div class="ekmSplit__column"><h5>GA4</h5>${b("Client ID", (d = a.ga4) == null ? void 0 : d.clientId)}${b("Session ID", (l = a.ga4) == null ? void 0 : l.sessionId)}${X(h)}<h5>Meta</h5>${b("FBP", (m = a.meta) == null ? void 0 : m.fbp)}${b("FBC", (f = a.meta) == null ? void 0 : f.fbc)}${X(e)}${i}</div></div>`;
}
function se(a) {
  return !!(a && (a.userToken || Array.isArray(a.lines) && a.lines.length));
}
function b(a, h) {
  return h ? `<p class="ekmOrderTracking__wrap ekom-tracking-field"><span>${n(a)}</span><strong>${n(h)}</strong></p>` : "";
}
function X(a) {
  return a.length ? `<ul>${a.map(([h, e]) => `<li><strong>${n(h)}</strong>: ${n(e)}</li>`).join("")}</ul>` : "";
}
function Ae(a) {
  return !!(a && (a.resolvedAtUtc || a.source || a.analytics !== null && a.analytics !== void 0 || a.marketing !== null && a.marketing !== void 0));
}
function ee(a) {
  return a === !0 ? "Yes" : a === !1 ? "No" : "Unknown";
}
function te(a, h) {
  return h.map((e) => {
    var t;
    return {
      key: e.key,
      label: e.label,
      value: q(a[e.property] || ((t = a.properties) == null ? void 0 : t[e.key]) || ""),
      isExtra: !1
    };
  });
}
function re(a, h) {
  return _(a, h).map(([e, t]) => ({
    key: e,
    label: R(e).replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim() || e,
    value: t,
    isExtra: !0
  }));
}
function ie(a) {
  return Object.fromEntries(a.map((h) => [h.key, h.value || ""]));
}
function Ie(a) {
  try {
    return JSON.parse(a).message || a;
  } catch {
    return a;
  }
}
customElements.define("ekom-orders-section-view", $e);
export {
  $e as EkomOrdersSectionViewElement,
  $e as default
};
