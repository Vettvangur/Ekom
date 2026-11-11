var ae = Object.defineProperty;
var oe = (o, h, e) => h in o ? ae(o, h, { enumerable: !0, configurable: !0, writable: !0, value: e }) : o[h] = e;
var c = (o, h, e) => oe(o, typeof h != "symbol" ? h + "" : h, e);
import { E as ne, m as v, x as F, e as de, r as le, a as ce, c as a, b as ue, y as he, h as pe, z as E, w as I, u as me, v as ve, A as G, B as q, q as fe, C as ge, o as ye, k as be } from "./manager-shared.js";
import { UmbElementMixin as ke } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as $e } from "@umbraco-cms/backoffice/notification";
const we = [
  { key: "customerName", label: "Name", property: "name" },
  { key: "customerEmail", label: "Email", property: "email" },
  { key: "customerAddress", label: "Address", property: "address" },
  { key: "customerApartment", label: "Apartment", property: "apartment" },
  { key: "customerCity", label: "City", property: "city" },
  { key: "customerCountry", label: "Country", property: "country" },
  { key: "customerZipCode", label: "Zipcode", property: "zipCode" },
  { key: "customerPhone", label: "Phone", property: "phone" }
], Oe = [
  { key: "shippingName", label: "Name", property: "name" },
  { key: "shippingEmail", label: "Email", property: "email" },
  { key: "shippingAddress", label: "Address", property: "address" },
  { key: "shippingApartment", label: "Apartment", property: "apartment" },
  { key: "shippingCity", label: "City", property: "city" },
  { key: "shippingCountry", label: "Country", property: "country" },
  { key: "shippingZipCode", label: "Zipcode", property: "zipCode" },
  { key: "shippingPhone", label: "Phone", property: "phone" }
], Se = 180, z = 1, W = 2;
class Le extends ke(HTMLElement) {
  constructor() {
    super(...arguments);
    c(this, "api", new ne());
    c(this, "notificationContext");
    c(this, "result", { orders: [], count: 0, totalPages: 0 });
    c(this, "page", 1);
    c(this, "loading", !0);
    c(this, "orderOpening", !1);
    c(this, "pendingListKey", "");
    c(this, "shownListKey");
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
      var t, r, i, n;
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
          if (d && d.value !== ((n = (i = this.selectedOrder) == null ? void 0 : i.status) == null ? void 0 : n.fulfillmentStatus)) return;
          const l = ["ArrowLeft", "ArrowUp"].includes(e.key) ? "Previous order" : "Next order", p = this.querySelector(`.ekom-order-navigation button[aria-label="${l}"]`);
          p && !p.disabled && (e.preventDefault(), p.click());
          return;
        }
        if (e.key === "Tab" && this.overlay) {
          const s = this.querySelectorAll(".ekmCustomerInformationModal__panel, .ekmOverlay__panel"), d = s.item(s.length - 1), l = d ? Array.from(d.querySelectorAll('button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), a[href], summary, [tabindex="0"]')).filter((g) => !g.closest("[hidden]") && g.getClientRects().length > 0) : [], p = l[0], f = l[l.length - 1];
          p && f && (!d.contains(document.activeElement) || (e.shiftKey ? document.activeElement === p : document.activeElement === f)) && (e.preventDefault(), (e.shiftKey ? f : p).focus());
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
    }), this.consumeContext($e, (t) => {
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
          try {
            for (const d of await this.api.orderViewCounts(t, this.otherViews(t))) s[d.view] = d.count;
          } catch {
          }
          if (e !== this.orderListRequest || !this.pollingSeconds || !this.isConnected || this.overlay || this.contains(document.activeElement)) return;
          (JSON.stringify(i) !== JSON.stringify(this.result) || F.some((d) => s[d.key] !== this.viewCounts[d.key])) && (this.result = i, this.viewCounts = s, this.loading = !1, this.render());
        }
        const n = this.querySelector(".ekom-polling summary");
        n && (n.classList.add("just-refreshed"), n.addEventListener("animationend", () => n.classList.remove("just-refreshed"), { once: !0 }));
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
        const n = v.pendingOrderId;
        v.pendingOrderId = void 0, await this.openOrder(n);
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
    const [i, n] = await Promise.allSettled([
      this.api.paymentProviders(r),
      this.api.shippingProviders(r)
    ]);
    if (t !== this.filterProvidersRequestId || r !== v.filters.store)
      return;
    const s = i.status === "fulfilled" ? i.value : [], d = n.status === "fulfilled" ? n.value : [];
    v.paymentProviders = s, v.shippingFilterProviders = d, this.filterProvidersLoading = !1, (i.status === "rejected" || n.status === "rejected") && this.showNotification(
      "warning",
      "Filter options unavailable",
      "Some provider options could not be loaded. Orders can still be searched; reload to try again."
    ), v.filters.paymentProvider && (v.paymentProviders.some((p) => p.key === v.filters.paymentProvider) || (v.filters.paymentProvider = "")), v.filters.shippingProvider && !d.some((l) => l.key === v.filters.shippingProvider) && (v.filters.shippingProvider = ""), this.render();
  }
  otherViews(e) {
    return F.map((t) => t.key).filter((t) => t !== e.orderStatus);
  }
  async loadOrders() {
    if (!v.filters.store) {
      this.loading = !1, this.result = { orders: [], count: 0, totalPages: 0 }, this.stateSaving || this.render();
      return;
    }
    const e = ++this.orderListRequest, t = { ...v.filters }, r = this.pendingListKey = JSON.stringify([t, this.page]);
    this.loading = !0, this.error = "", this.stateSaving || this.render();
    try {
      const i = await this.api.searchOrders(t, this.page);
      if (e !== this.orderListRequest) return;
      if (this.shownListKey = r, this.viewCounts[t.orderStatus] = i.count, t.orderStatus === "NeedsReview" && i.count === 0) {
        v.filters.orderStatus = "AllOrders", this.page = 1, await this.loadOrders();
        return;
      }
      const n = this.otherViews(t);
      this.api.orderViewCounts(t, n).catch(() => []).then((s) => {
        e === this.orderListRequest && (n.forEach((d) => delete this.viewCounts[d]), s.forEach((d) => {
          this.viewCounts[d.view] = d.count;
        }));
      }).then(() => {
        e !== this.orderListRequest || !this.isConnected || this.querySelectorAll('[data-action="select-order-view"]').forEach((s) => {
          const d = this.viewCounts[s.dataset.view || ""];
          s.dataset.view === "NeedsReview" && (s.hidden = !(d && d > 0));
          let l = s.querySelector("span");
          if (d === void 0) {
            l == null || l.remove();
            return;
          }
          l || (l = document.createElement("span"), s.appendChild(l)), l.textContent = String(d);
        });
      }), this.result = {
        ...i,
        orders: i.orders || []
      };
    } catch (i) {
      if (e !== this.orderListRequest) return;
      this.error = k(i, "Error searching orders.");
    } finally {
      e === this.orderListRequest && (this.loading = !1, this.stateSaving || this.render());
    }
  }
  render() {
    var i, n, s;
    if (this.orderOpening && ((i = this.querySelector("[data-order-opening]")) == null ? void 0 : i.dataset.orderOpening) === this.returnFocusOrderId) return;
    const e = this.querySelector("[data-review-comment]"), t = e && ((n = this.ownerDocument) == null ? void 0 : n.activeElement) === e ? { start: e.selectionStart, end: e.selectionEnd } : void 0, r = Array.from(this.querySelectorAll(".ekmOverlay, .ekmOverlay__content, .ekmCustomerInformationModal .ekmOverlay__content")).map((d) => d.scrollTop);
    if (le(this, `
      <style>${ce}</style>
      <link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css?v=20261111-open-customer">
      <section class="ekmManager ekom-design">
        <div class="ekmManager__body">
          ${this.configError ? `<p class="status status--error" role="alert">${a(this.configError)}</p>` : ""}
          ${ue("orders")}
          ${this.renderOrders()}
        </div>
      </section>
      ${this.renderOverlay()}
    `), this.bindEvents(), t) {
      const d = this.querySelector("[data-review-comment]");
      d == null || d.focus({ preventScroll: !0 }), d == null || d.setSelectionRange(t.start, t.end);
    }
    if (this.isConnected && this.overlay === "order" && !this.contains(document.activeElement) && ((s = this.querySelector('[data-action="close-overlay"]')) == null || s.focus({ preventScroll: !0 })), this.querySelectorAll(".ekmOverlay, .ekmOverlay__content, .ekmCustomerInformationModal .ekmOverlay__content").forEach((d, l) => {
      d.scrollTop = r[l] ?? 0;
    }), this.recoveryScrollTop !== void 0) {
      const d = this.querySelector(".ekmOverlay");
      d && (d.scrollTop = this.recoveryScrollTop);
    }
  }
  renderOrders() {
    const e = v.filters;
    return `${this.renderToolbar()}<section class="ekom-order-list" aria-label="Orders" aria-busy="${this.loading}">
      <div class="ekom-list-toolbar"><nav class="ekom-order-views" aria-label="Order views">${F.map((t) => `<button type="button" data-action="select-order-view" data-view="${a(t.key)}" ${t.key === "NeedsReview" && !(this.viewCounts[t.key] > 0) ? "hidden" : ""} aria-pressed="${e.orderStatus === t.key}">${t.label}${this.viewCounts[t.key] === void 0 ? "" : `<span>${this.viewCounts[t.key]}</span>`}</button>`).join("")}</nav><div class="ekom-list-search"><label class="ekom-search-field"><svg aria-hidden="true" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><circle cx="11" cy="11" r="7"></circle><path d="M20 20l-3.5-3.5"></path></svg><input type="search" data-field="query" value="${a(e.query)}" placeholder="Search orders" aria-label="Search orders"></label><button type="button" data-action="open-filter"><svg aria-hidden="true" width="13" height="13" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round"><path d="M4 6h16M7 12h10M10 18h4"></path></svg>Filter</button></div></div>
      <div class="ekom-inline-totals"><div><span>Orders</span><strong>${a(this.result.count || 0)}</strong></div><div><span>Order amount</span><strong>${a(this.result.grandTotal || "0")}</strong></div><div><span>Average</span><strong>${a(this.result.averageAmount || "0")}</strong></div></div>
      ${this.error ? `<p class="status status--error" role="alert">${a(this.error)}</p>` : ""}
      ${this.showOrdersLoading ? this.renderOrderTableHead() + he() + "</div>" : this.renderOrderTable()}
      ${this.renderPagination()}
    </section>`;
  }
  renderToolbar() {
    return `<div class="ekom-shared-filters">${pe()}<div class="ekom-shared-filter-actions"><details class="ekom-polling${this.pollingSeconds ? " is-active" : ""}"><summary aria-label="Order polling"><svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-3-6.7"></path><path d="M21 3v6h-6"></path></svg><span>${this.pollingSeconds ? `Polling every ${this.pollingSeconds}s` : "Polling off"}</span><span aria-hidden="true">⌄</span></summary><div class="ekom-poll-options" role="group" aria-label="Polling interval">${[0, 2, 5, 10, 20, 30].map((e) => `<button type="button" data-poll-seconds="${e}" aria-pressed="${e === this.pollingSeconds}"><span aria-hidden="true">•</span>${e ? `Every ${e} seconds` : "Off"}</button>`).join("")}</div></details><button type="button" data-action="open-export">Export</button></div></div>`;
  }
  renderOrderTable() {
    return this.result.orders.length ? `${this.renderOrderTableHead()}${this.result.orders.map((e) => this.renderOrderRow(e)).join("")}</div>` : '<div class="ekom-list-message"><h3>No orders match these filters</h3><p>Try a wider date range, another view, or clear the filters.</p><button type="button" data-action="clear-order-filters">Clear filters</button></div>';
  }
  /** Opens the orders table; the caller closes it after the rows. */
  renderOrderTableHead() {
    return '<div class="ekom-list-table" role="table" aria-label="Orders"><div class="ekom-list-row ekom-list-head" role="row"><span role="columnheader">Order</span><span role="columnheader">Date</span><span role="columnheader">Customer</span><span role="columnheader" class="ekom-align-right">Total</span><span role="columnheader">Payment</span><span role="columnheader">Fulfillment</span><span role="columnheader" class="ekom-align-right">Items</span><span role="columnheader"><span class="ekom-sr-only">Open order</span></span></div>';
  }
  /** Placeholder rows while a different list is on its way; a refresh of the list on screen keeps its rows. */
  get showOrdersLoading() {
    return this.loading && this.pendingListKey !== this.shownListKey;
  }
  renderOrderRow(e) {
    const t = e.status;
    return `<div class="ekom-list-row" role="row">
      <div role="cell" class="ekom-order-number"><span class="ekom-order-reference"><button type="button" data-action="view-order" data-order-id="${a(e.uniqueId)}" aria-label="Open order ${a(e.referenceId)}">#${a(e.referenceId)}</button>${e.needsReview ? '<span class="ekom-review-icon" role="img" aria-label="Needs review" title="Needs review">!</span>' : ""}</span><small>${a((t == null ? void 0 : t.orderStatus) || "Needs review")}</small></div>
      <span role="cell" class="ekom-row-date">${a(E(e.createDate))}</span>
      <div role="cell" class="ekom-row-customer"><strong>${a(e.customerName)}</strong><span>${a(e.customerEmail)}</span></div>
      <strong role="cell" class="ekom-align-right">${a(e.formattedTotal)}</strong>
      <div role="cell">${this.renderPaymentMenu(e)}</div>
      <div role="cell">${this.renderRowFulfillment(e)}</div>
      <span role="cell" class="ekom-align-right ekom-muted">${a(e.itemQuantity ?? "—")}</span>
      <button role="cell" type="button" class="ekom-row-open" data-action="view-order" data-order-id="${a(e.uniqueId)}" aria-label="Open order ${a(e.referenceId)}">›</button>
    </div>`;
  }
  renderPagination() {
    const e = this.result.count || 0, t = Math.max(1, this.result.totalPages);
    return `<footer class="ekom-list-footer"><span>${e ? (this.page - 1) * 20 + 1 : 0}–${Math.min(this.page * 20, e)} of ${e}</span><div><span>20 per page</span><button type="button" data-action="set-page" data-page="${this.page - 1}" ${this.page <= 1 || this.loading ? "disabled" : ""} aria-label="Previous page">‹</button><strong>${this.page} / ${t}</strong><button type="button" data-action="set-page" data-page="${this.page + 1}" ${this.page >= t || this.loading ? "disabled" : ""} aria-label="Next page">›</button></div></footer>`;
  }
  renderOverlay() {
    return this.overlay === "filter" ? this.renderFilterOverlay() : this.overlay === "export" ? this.renderExportOverlay() : this.overlay === "order" && this.orderOpening ? this.renderOrderOpening() : this.overlay === "order" && this.selectedOrder ? this.renderOrderOverlay(this.selectedOrder) : "";
  }
  renderFilterOverlay() {
    const e = v.filters;
    return `
      <div class="ekmOverlay ekom-filter-overlay">
        <div class="ekmOverlay__panel ekom-filter-panel" role="dialog" aria-modal="true" aria-label="Filter orders">
          <div class="ekmOverlay__header"><div><h2>Filter orders</h2><p>Status filters also apply to Analytics and Customers</p></div><button class="btn-reset" type="button" data-action="close-overlay" aria-label="Close filters">&times;</button></div>
          <div class="ekmOverlay__content">
            <h3>Status</h3><div class="ekom-status-filter-grid">${[["Order", ["Draft", "Open", "Closed", "Cancelled"]], ["Payment", ["Pending", "Authorized", "Paid", "PartiallyPaid", "Failed", "Voided"]], ["Fulfillment", ["Unfulfilled", "Picking", "Picked", "Packed", "PartiallyFulfilled", "Fulfilled"]]].map(([t, r], i) => `<label class="control-group">${t}<select data-status-dimension="${i}"><option value="">Any</option>${r.map((n) => `<option value="${n}" ${e.orderStatus.startsWith("states:") && e.orderStatus.slice(7).split(";")[i] === n ? "selected" : ""}>${n.replace(/([a-z])([A-Z])/g, "$1 $2")}</option>`).join("")}</select></label>`).join("")}</div>
            <h3>Payment &amp; shipping</h3><div class="ekom-filter-fields">
            <label class="control-group">Payment provider
              <select data-filter-field="paymentProvider" ${this.filterProvidersLoading ? "disabled" : ""}>
                <option value="">Select payment provider</option>
                ${v.paymentProviders.map((t) => `<option value="${a(t.key)}" ${e.paymentProvider === t.key ? "selected" : ""}>${a(t.title)}</option>`).join("")}
              </select>
            </label>
            <label class="control-group">Shipping provider
              <select data-filter-field="shippingProvider" ${this.filterProvidersLoading ? "disabled" : ""}>
                <option value="">All shipping providers</option>
                ${v.shippingFilterProviders.map((t) => `<option value="${a(t.key)}" ${e.shippingProvider === t.key ? "selected" : ""}>${a(t.title)}</option>`).join("")}
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
    if (!this.config || this.configError || !r || r.requiresReview || r.orderStatus !== "Open") return I(r == null ? void 0 : r.paymentStatus);
    const i = ["Pending", "Authorized", "Paid", "PartiallyPaid", "Failed", "Voided"];
    return `<details class="ekom-row-fulfillment ekom-payment-menu" name="ekom-row-fulfillment"><summary aria-label="Set payment for order ${a(e.referenceId)}">${I(r.paymentStatus)}<span aria-hidden="true">▾</span></summary><div class="ekom-row-fulfillment-menu"><h4>Set payment</h4>${i.map((n) => `<button type="button" data-action="set-payment" data-detail="${t}" data-order-id="${a(e.uniqueId)}" data-value="${n}" aria-pressed="${r.paymentStatus === n}" ${this.stateSaving ? "disabled" : ""}>${this.renderStateDot(n)}${n.replace(/([a-z])([A-Z])/g, "$1 $2")}<span class="ekom-status-check">${r.paymentStatus === n ? "✓" : ""}</span></button>`).join("")}<p class="ekom-payment-note">Updates the recorded status immediately. Does not move money through the payment provider.</p>${t ? "" : `<label><input type="checkbox" data-row-events ${this.config.managerOrderStatusFireEventsByDefault ?? !0 ? "checked" : ""}><span><strong>Trigger status events</strong><small>Runs any handlers set up for this status</small></span></label>`}</div></details>`;
  }
  async changePayment(e) {
    var d;
    const t = e.dataset.detail === "true", r = t ? this.selectedOrder : this.result.orders.find((l) => l.uniqueId === e.dataset.orderId), i = e.dataset.value || "";
    if (e.disabled || this.stateSaving || !this.config || this.configError || !(r != null && r.status) || r.status.requiresReview || r.status.orderStatus !== "Open" || !["Pending", "Authorized", "Paid", "PartiallyPaid", "Failed", "Voided"].includes(i)) return;
    const n = e.closest("details");
    if (r.status.paymentStatus === i) {
      n == null || n.removeAttribute("open");
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
    const s = ((d = n == null ? void 0 : n.querySelector("[data-row-events]")) == null ? void 0 : d.checked) ?? this.config.managerOrderStatusFireEventsByDefault ?? !0;
    this.stateSaving = !0, n == null || n.querySelectorAll("button").forEach((l) => {
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
    return !this.config || this.configError || !t || t.requiresReview || t.orderStatus !== "Open" ? I(t == null ? void 0 : t.fulfillmentStatus) : `<details class="ekom-row-fulfillment" name="ekom-row-fulfillment"><summary aria-label="Set fulfillment for order ${a(e.referenceId)}">${I(t.fulfillmentStatus)}<span aria-hidden="true">▾</span></summary><div class="ekom-row-fulfillment-menu"><h4>Set fulfillment</h4>${["Unfulfilled", "Picking", "Picked", "Packed", "Fulfilled", "PartiallyFulfilled"].map((r) => `<button type="button" data-action="set-row-fulfillment" data-order-id="${a(e.uniqueId)}" data-value="${r}" aria-pressed="${t.fulfillmentStatus === r}" ${this.stateSaving ? "disabled" : ""}>${this.renderStateDot(r)}${r.replace(/([a-z])([A-Z])/g, "$1 $2")}<span class="ekom-status-check">${t.fulfillmentStatus === r ? "✓" : ""}</span></button>`).join("")}<label><input type="checkbox" data-row-events ${this.config.managerOrderStatusFireEventsByDefault ?? !0 ? "checked" : ""}><span><strong>Trigger status events</strong><small>Runs any handlers set up for this status</small></span></label></div></details>`;
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
    const n = ((s = i == null ? void 0 : i.querySelector("[data-row-events]")) == null ? void 0 : s.checked) ?? this.config.managerOrderStatusFireEventsByDefault ?? !0;
    this.stateSaving = !0, i == null || i.querySelectorAll("button").forEach((d) => {
      d.disabled = !0;
    });
    try {
      await this.api.changeOrderStates(t.uniqueId, t.status.revision, r === "Fulfilled" ? "Fulfill" : r, n), this.showSuccess("Fulfillment status updated.");
    } catch (d) {
      this.showError(k(d, "Error updating fulfillment status."));
    } finally {
      await this.loadOrders(), this.stateSaving = !1, this.render();
    }
  }
  renderFilterInput(e, t, r) {
    return `
      <label class="control-group">${a(t)}
        <input type="text" data-filter-field="${a(e)}" value="${a(v.filters[e])}" placeholder="${a(r)}">
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
  /** The order drawer while everything it shows is still being fetched. */
  renderOrderOpening() {
    var t;
    const e = (t = this.result.orders.find((r) => r.uniqueId === this.returnFocusOrderId)) == null ? void 0 : t.referenceId;
    return `<div class="ekmOverlay ekom-order-drawer ekom-design-drawer" role="dialog" aria-modal="true" aria-label="Order details" aria-busy="true" data-order-opening="${a(this.returnFocusOrderId)}"><div class="ekmOverlay__panel ekmOrderOverlay__panel"><header class="ekom-panel-heading"><div class="ekom-order-heading-copy"><h2>${e ? `#${a(e)}` : "Order"}</h2>${me("Loading order…")}</div><button type="button" data-action="close-overlay" aria-label="Close order">×</button></header><div class="ekmOverlay__content ekmOrder">${ve()}</div></div></div>`;
  }
  renderOrderOverlay(e) {
    var i, n, s, d, l, p;
    const t = this.result.orders.findIndex((f) => f.uniqueId === e.uniqueId);
    return `<div class="ekmOverlay ekom-order-drawer ekom-design-drawer" role="dialog" aria-modal="true" aria-label="Order details"><div class="ekmOverlay__panel ekmOrderOverlay__panel"><header class="ekom-panel-heading"><nav class="ekom-order-navigation" aria-label="Order navigation">${[-1, 1].map((f) => {
      const g = t >= 0 ? this.result.orders[t + f] : void 0, m = f < 0 ? "Previous order" : "Next order";
      return `<button type="button" data-action="view-order" data-order-id="${a((g == null ? void 0 : g.uniqueId) || "")}" aria-label="${m}" title="${m} on this page (${f < 0 ? "← / ↑" : "→ / ↓"})" ${!g || this.stateSaving || this.executingActionKey ? "disabled" : ""}>${f < 0 ? "‹" : "›"}</button>`;
    }).join("")}</nav><div class="ekom-order-heading-copy"><h2>#${a(e.referenceId)} ${I((i = e.status) == null ? void 0 : i.paymentStatus)} ${I((n = e.status) == null ? void 0 : n.fulfillmentStatus)} ${I((s = e.status) == null ? void 0 : s.orderStatus)}</h2><p>${a((l = (d = e.customerInformation) == null ? void 0 : d.customer) == null ? void 0 : l.name)} · ${a(E(e.createDate))} · ${a(((p = e.storeInfo) == null ? void 0 : p.alias) || e.storeAlias)} · <span class="ekom-heading-order-id"><span>${a(e.uniqueId)}</span><button type="button" class="ekom-copy-order-id" data-action="copy-order-id" aria-label="Copy order ID" title="Copy order ID"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V4H4v12h4"/></svg></button></span></p></div><button type="button" data-action="close-overlay" aria-label="Close order">×</button></header><div class="ekmOverlay__content ekmOrder">${this.renderOrderDetails(e)}</div></div></div>${this.customerEditorOpen ? this.renderCustomerEditor() : ""}${this.orderLineEditorOpen ? this.renderOrderLineEditor() : ""}${this.shippingProviderEditorOpen ? this.renderShippingProviderEditor() : ""}${this.activeReview ? this.renderReviewDialog() : ""}`;
  }
  renderOrderDetails(e) {
    var p, f, g, m;
    const t = ((p = e.customerInformation) == null ? void 0 : p.customer) || {}, r = ((f = e.customerInformation) == null ? void 0 : f.shipping) || {}, i = e.status, n = [{ key: "order", label: "Order" }, { key: "activity", label: "Activity log" }, { key: "tracking", label: "Tracking & consent" }], s = !this.config || !!this.configError || this.stateSaving || !i || i.requiresReview || ((g = this.orderLockStatus) == null ? void 0 : g.hasLock) || this.orderLockLoading || this.orderLockError, d = (u, y) => `<section id="ekom-order-panel-${u}" role="tabpanel" aria-labelledby="ekom-order-tab-${u}" ${this.orderTab === u ? "" : "hidden"}>${y}</section>`, l = `<div class="ekom-order-columns"><main class="ekom-order-main">
      <section class="ekom-detail-card">${this.renderDesignOrderLines(e)}</section>
      ${this.renderPaymentCard(e)}
      ${this.renderShipment(e)}
      ${Object.keys(e.customData || {}).length ? `<section class="ekom-detail-card"><h3>Notes</h3>${this.renderOrderCustomData(e)}</section>` : ""}
    </main><aside class="ekom-customer-sidebar"><section class="ekom-detail-card"><div class="ekom-card-heading"><h3>Customer</h3>${t.email || t.userName ? '<button type="button" class="ekom-customer-open" data-action="open-customer">Open customer</button>' : ""}</div>${t.email || t.userName ? `<button type="button" class="ekom-customer-open ekom-customer-name" data-action="open-customer">${a(t.name || t.email || t.userName)}</button>` : `<strong>${a(t.name)}</strong>`}<div class="ekom-card-heading"><h4>Contact information</h4><button type="button" class="btn-reset" data-action="open-customer-editor">Edit</button></div><p>${a(t.email)}</p><p>${a(t.phone)}</p><h4>Shipping address</h4>${M(r) ? `${this.renderAddress(r)}${this.renderExtraProperties(r.properties, "shipping")}` : "<p>Same as billing address</p>"}<h4>Billing address</h4>${this.renderAddress(t)}${this.renderExtraProperties(t.properties, "customer")}</section></aside></div>`;
    return `<div class="ekom-status-controls"><label>Order<details class="ekom-lifecycle-menu"><summary aria-label="Order status">${this.renderStateDot(i == null ? void 0 : i.orderStatus)}${a((i == null ? void 0 : i.orderStatus) || "Needs review")} ▾</summary><div>${((i == null ? void 0 : i.orderStatus) === "Closed" ? [["Reopen", "Reopen order"]] : [["Close", "Close order"], ["Cancel", "Cancel order"]]).map(([u, y]) => `<button type="button" data-action="order-state-command" data-command="${u}" ${s ? "disabled" : ""}>${y}</button>`).join("")}</div></details></label><div class="ekom-payment-control"><span>Payment</span>${this.renderPaymentMenu(e, !0)}</div><div class="ekom-fulfillment-control"><span>Fulfillment</span><input type="hidden" data-field="fulfillmentState" value="${a((i == null ? void 0 : i.fulfillmentStatus) || "")}"><details class="ekom-lifecycle-menu ekom-fulfillment-menu"><summary aria-label="Fulfillment status" ${s || (i == null ? void 0 : i.orderStatus) !== "Open" ? 'aria-disabled="true"' : ""}>${this.renderStateDot(i == null ? void 0 : i.fulfillmentStatus)}<span data-fulfillment-label>${a(((i == null ? void 0 : i.fulfillmentStatus) || "Needs review").replace(/([a-z])([A-Z])/g, "$1 $2"))}</span><span class="ekom-status-chevron">▾</span></summary><div>${["Unfulfilled", "Picking", "Picked", "Packed", "PartiallyFulfilled", "Fulfilled"].map((u) => `<button type="button" data-action="choose-fulfillment" data-value="${u}" aria-pressed="${(i == null ? void 0 : i.fulfillmentStatus) === u}" ${s || (i == null ? void 0 : i.orderStatus) !== "Open" ? "disabled" : ""}>${this.renderStateDot(u)}${u.replace(/([a-z])([A-Z])/g, "$1 $2")}<span class="ekom-status-check">${(i == null ? void 0 : i.fulfillmentStatus) === u ? "✓" : ""}</span></button>`).join("")}</div></details></div><label class="ekmCheckboxLabel"><input type="checkbox" data-field="notifyOrderStatus" ${this.notifyOrderStatus ? "checked" : ""}>Trigger status events</label><span class="ekom-events-help"><button type="button" class="ekom-events-help-button" aria-label="About trigger status events" aria-describedby="ekom-events-tooltip">i</button><span class="ekom-events-tooltip" id="ekom-events-tooltip" role="tooltip"><strong>Trigger status events ("Fire events")</strong><span><b>On</b> — saves the status and runs whatever handlers are set up for it. What happens depends on the status and your setup, for example an email or an ERP update.</span><span><b>Off</b> — saves the status only. Use for manual corrections so external actions don't repeat.</span><small>Either way the status is saved and logged. It doesn't release stock or replace the payment flow.</small></span></span><button type="button" data-action="save-fulfillment" class="btn-success" ${s || (i == null ? void 0 : i.orderStatus) !== "Open" ? "disabled" : ""}>${this.stateSaving ? "Saving..." : "Save"}</button></div>
      ${i != null && i.requiresReview ? '<p class="status" role="status">Historical payment or fulfillment needs review before updating this order.</p>' : ""}
      ${this.configError ? `<p class="status status--error" role="alert">${a(this.configError)}</p>` : ""}
      <span class="ekom-sr-only">Payment: ${a((m = e.chargedAmount) == null ? void 0 : m.currencyString)}</span>${this.renderOrderLockStatus()}${this.renderOrderActions()}${this.renderGiftcardRecovery()}${this.renderReviews()}
      <div class="ekom-order-tabs" role="tablist" aria-label="Order sections">${n.map((u) => `<button type="button" id="ekom-order-tab-${u.key}" role="tab" aria-selected="${this.orderTab === u.key}" aria-controls="ekom-order-panel-${u.key}" tabindex="${this.orderTab === u.key ? 0 : -1}" data-action="select-order-tab" data-tab="${u.key}">${u.label}</button>`).join("")}</div>
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
        <dt class="ekmOrderCustomData__key">${a(Ie(r))}</dt>
        <dd class="ekmOrderCustomData__value">${a(i)}</dd>
      </div>`).join("")}</dl>` : "";
  }
  renderPaymentCard(e) {
    var s, d, l, p, f, g, m, u, y, O, w, L, A;
    const t = (e.orderLines || []).reduce((S, $) => S + (Number($.quantity) || 0), 0), r = ((s = e.shippingProvider) == null ? void 0 : s.title) || ((l = (d = e.shippingProvider) == null ? void 0 : d.properties) == null ? void 0 : l.nodeName) || "", i = this.giftcardEligibleValue(e), n = (S, $, C, x = !1) => `<div${x ? ' class="ekom-money-total"' : ""}><dt>${a(S)}</dt><dd class="ekom-money-description">${a($)}</dd><dd>${a(C || "—")}</dd></div>`;
    return `<section class="ekom-detail-card ekom-payment-card"><h3>Payment ${I((p = e.status) == null ? void 0 : p.paymentStatus)}</h3><dl class="ekom-money-summary">${n("Subtotal", `${t} ${t === 1 ? "item" : "items"}`, (g = (f = e.subTotal) == null ? void 0 : f.withVat) == null ? void 0 : g.currencyString)}${n("Discount", e.coupon || "—", (m = e.discountAmount) != null && m.currencyString ? `${Number(e.discountAmount.value) ? "-" : ""}${e.discountAmount.currencyString}` : "")}${n("Shipping", r, (O = (y = (u = e.shippingProvider) == null ? void 0 : u.price) == null ? void 0 : y.withVat) == null ? void 0 : O.currencyString)}${i ? n("Gift cards", "Eligible value, already applied to the total", this.formatGiftcardAmount(i, e)) : ""}${n("Tax", "Included in total", (w = e.chargedVat) == null ? void 0 : w.currencyString)}${n("Total", "", (L = e.chargedAmount) == null ? void 0 : L.currencyString, !0)}</dl>${(A = e.giftcards) != null && A.length ? `<div class="ekom-payment-breakdown">${this.renderGiftcards(e)}</div>` : ""}${this.renderPaymentDetails(e)}</section>`;
  }
  renderPaymentDetails(e) {
    var i, n, s, d, l, p, f;
    const t = (g) => `<dl>${g.map((m) => `<div><dt>${a(m.label)}</dt><dd><span>${a(m.value)}</span>${m.label !== "Currency" && m.label !== "Paid date" && m.value && m.value !== "Not recorded" && m.value !== "—" ? `<button type="button" class="ekom-payment-copy" data-action="copy-provider-value" data-copy="${a(m.value)}" aria-label="Copy ${a(m.label)}" title="Copy ${a(m.label)}"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V4H4v12h4"/></svg></button>` : ""}</dd></div>`).join("")}</dl>`, r = _((i = e.paymentProvider) == null ? void 0 : i.customData, "custompayment").map(([g, m]) => ({ label: T(R(g)), value: m }));
    return r.push({ label: "Paid date", value: e.paidDate ? E(e.paidDate) : "Not recorded" }, { label: "Currency", value: e.currency || ((s = (n = e.storeInfo) == null ? void 0 : n.currency) == null ? void 0 : s.isoCurrencySymbol) || "Not recorded" }), `<section class="ekom-payment-details" aria-label="Payment details"><div class="ekom-payment-detail-grid"><section class="ekom-payment-detail-box"><header><span class="ekom-payment-kind">Payment</span><h4>${a(((d = e.paymentProvider) == null ? void 0 : d.title) || ((p = (l = e.paymentProvider) == null ? void 0 : l.properties) == null ? void 0 : p.nodeName) || "Not recorded")}</h4>${I((f = e.status) == null ? void 0 : f.paymentStatus)}</header>${t(r)}</section>${(e.giftcards || []).map((g) => {
      const m = H(g).filter((u) => u.label !== D("code")).map((u) => u.label === D("amount") ? { ...u, value: this.formatGiftcardAmount(g.amount, e) } : u);
      return `<section class="ekom-payment-detail-box"><header><span class="ekom-payment-kind ekom-payment-kind-gift">Gift card</span><h4>${a(g.code)}</h4><span class="ekom-gift-state">${a(Z(g).status)}</span></header>${t(m)}</section>`;
    }).join("")}</div></section>`;
  }
  renderShipment(e) {
    var y, O, w, L, A;
    const t = e.shippingProvider, r = (t == null ? void 0 : t.title) || ((y = t == null ? void 0 : t.properties) == null ? void 0 : y.nodeName) || "No shipping provider", i = _(t == null ? void 0 : t.customData, "customshipping"), n = (S) => {
      var $;
      return (($ = i.find(([C]) => R(C).replace(/[_ -]/g, "").toLowerCase() === S.toLowerCase())) == null ? void 0 : $[1]) || "";
    }, s = n("TrackingNumber"), d = n("ShipmentState"), l = n("TrackingUrl"), p = /^https?:\/\//i.test(l) ? l : "", f = ((O = e.customerInformation) == null ? void 0 : O.customer) || {}, g = ((w = e.customerInformation) == null ? void 0 : w.shipping) || {}, m = M(g) ? g : f, u = (S, $) => `<button type="button" class="ekom-shipment-copy" data-action="copy-provider-value" data-copy="${a(S)}" aria-label="Copy ${a($)}" title="Copy ${a($)}"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><rect x="8" y="8" width="12" height="12" rx="2"/><path d="M16 8V4H4v12h4"/></svg></button>`;
    return `<section class="ekom-detail-card ekom-shipment"><div class="ekom-shipment-grid"><section class="ekom-shipment-box"><div class="ekom-shipment-heading"><h4>Shipment</h4>${d ? `<span class="ekom-shipment-state">${a(d)}</span>` : ""}</div><div class="ekom-shipment-tracking"><span>Tracking number</span><div><strong>${a(s || "Not available")}</strong>${s ? u(s, "tracking number") : ""}${t ? '<button type="button" class="ekom-shipment-copy" data-action="open-shipping-provider-editor" aria-label="Edit shipping data" title="Edit shipping data"><svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.7" aria-hidden="true"><path d="M12 20h9M16 3l5 5L7 22l-5 1 1-5Z"/></svg></button>' : ""}${p ? `<a href="${a(p)}" target="_blank" rel="noopener noreferrer">Track shipment ↗</a>` : ""}</div></div></section><section class="ekom-shipment-box"><div class="ekom-shipment-heading"><h4>Method</h4><strong>${a(((A = (L = t == null ? void 0 : t.price) == null ? void 0 : L.withVat) == null ? void 0 : A.currencyString) || "—")}</strong></div><h3>${a(r)}</h3><div class="ekom-shipment-address"><span>Deliver to</span><strong>${a([m.address, m.apartment].filter(Boolean).join(", ") || "No address recorded")}</strong><span>${a([m.zipCode, m.city].filter(Boolean).join(" "))}</span><span>${a(m.country)}</span></div><div><button type="button" data-action="open-shipping-provider-editor">${t ? "Change shipping provider" : "Add shipping provider"}</button></div></section></div>${i.length ? `<details class="ekom-shipment-data" open><summary><strong>Provider data</strong><span>${i.length} fields from ${a(r)}</span><span class="ekom-shipment-hide">Hide ▴</span><span class="ekom-shipment-show">Show ▾</span></summary><dl>${i.map(([S, $]) => `<div><dt>${a(T(R(S)))}</dt><dd><span>${a($ || "—")}</span>${$ ? u($, T(R(S))) : ""}</dd></div>`).join("")}</dl></details>` : ""}</section>`;
  }
  renderPrintableOrder(e) {
    var s, d, l, p, f, g;
    const t = ((s = e.customerInformation) == null ? void 0 : s.customer) || {}, r = ((d = e.customerInformation) == null ? void 0 : d.shipping) || {}, i = this.getOrderStatusValue(e.orderStatus), n = ((l = v.statusList.find((m) => G(m) === i)) == null ? void 0 : l.label) || i;
    return `
      <main class="ekmOrder">
        <h1>Order number: ${a(e.referenceId)}</h1>
        <p>Order status: ${a(((p = e.status) == null ? void 0 : p.orderStatus) || n)}</p>
        ${e.status ? `<p>Payment status: ${a(e.status.paymentStatus.replace(/([a-z])([A-Z])/g, "$1 $2"))} · Fulfillment: ${a(e.status.fulfillmentStatus.replace(/([a-z])([A-Z])/g, "$1 $2"))}</p>` : ""}
        <p>UniqueId: ${a(e.uniqueId)}</p>
        <p>Created date: ${a(E(e.createDate))}</p>
        <p>Paid date: ${a(E(e.paidDate))}</p>
        <p>Store: ${a(((f = e.storeInfo) == null ? void 0 : f.alias) || e.storeAlias)}</p>
        <p>Payment: <strong>${a((g = e.chargedAmount) == null ? void 0 : g.currencyString)}</strong></p>
        ${this.renderOrderCustomData(e)}
        <section class="ekmSplit">
          <div class="ekmSplit__column"><h4>Billing</h4>${this.renderAddress(t)}${this.renderExtraProperties(t.properties, "customer")}</div>
          <div class="ekmSplit__column"><h4>Shipping</h4>${M(r) ? `${this.renderAddress(r)}${this.renderExtraProperties(r.properties, "shipping")}` : "<p>Same as billing address</p>"}</div>
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
    return ["name", "email", "address", "apartment", "city", "country", "zipCode", "phone"].filter((t) => e[t]).map((t) => `<p>${T(t)}: ${a(q(e[t]))}</p>`).join("");
  }
  renderExtraProperties(e, t) {
    const r = _(e, t);
    return r.length ? `<h5 style="margin-top:20px; font-weight:bold;">${t === "customshipping" ? "Shipping Provider Custom" : `Extra ${t === "shipping" ? "Shipping" : "Customer"}`} Data</h5><ul>${r.map(([i, n]) => `<li><strong>${a(R(i))}</strong>: ${a(n)}</li>`).join("")}</ul>` : "";
  }
  renderProvider(e, t, r) {
    var n, s;
    if (!t)
      return "";
    const i = typeof t.title == "string" && t.title.trim() ? t.title : (n = t.properties) == null ? void 0 : n.nodeName;
    return `<h4>${a(e)}</h4><h4><strong>${a(i)}</strong></h4>${t.price ? `<p>Price: ${a((s = t.price.withVat) == null ? void 0 : s.currencyString)}</p>` : ""}${this.renderExtraProperties(t.customData, r)}`;
  }
  renderGiftcards(e) {
    const t = Array.isArray(e.giftcards) ? e.giftcards : [];
    return t.length ? `<div class="ekmOrderGiftcards"><h4>Gift cards</h4><ul class="ekmOrderGiftcards__list">${t.map((r) => {
      const i = Z(r), n = H(r);
      return `<li><div class="ekmOrderGiftcards__summary"><strong>${a(r.code)}</strong><strong>${a(this.formatGiftcardAmount(r.amount, e))}</strong><span>${a(i.status)}</span></div>
        <p>${a(i.expiryLabel)}: ${a(i.expiry)}</p>
        ${i.warning ? `<p role="alert">${a(i.warning)}</p>` : ""}
        <div class="ekmOrderGiftcards__actions">${this.orderActions.filter((s) => s.giftcardCode != null && s.giftcardCode === r.code).map((s) => this.renderAction(s)).join("")}</div>
        <details><summary>More details</summary><dl>${n.map((s) => `<dt>${a(s.label)}</dt><dd>${a(s.value)}</dd>`).join("")}</dl></details></li>`;
    }).join("")}</ul></div>` : "";
  }
  formatGiftcardAmount(e, t) {
    var n, s, d, l;
    const r = ((s = (n = t.storeInfo) == null ? void 0 : n.currency) == null ? void 0 : s.currencyValue) || "en-US", i = ((l = (d = t.storeInfo) == null ? void 0 : d.currency) == null ? void 0 : l.isoCurrencySymbol) || t.currency;
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
    const e = (r, i) => `<dt>${a(r)}</dt><dd>${a(i ?? "Not recorded")}</dd>`;
    return `<section class="ekmOrderTracking ekmGiftcardRecovery"><h4>Failed gift-card preparation</h4>
      <p>Recovery history for failed checkout preparation, not accepted payment. These actions do not refund a payment or retry checkout.</p>
      ${this.recoveryMessage ? `<p role="status">${a(this.recoveryMessage)}</p>` : ""}
      ${this.recoveryActionError ? `<p role="alert">${a(this.recoveryActionError)}</p>` : ""}
      ${this.recoveryRefreshError ? '<p role="alert">We could not refresh all order details. Reopen the order before taking another recovery action.</p>' : ""}
      ${this.orderActionsError ? '<p role="alert">We could not refresh order actions. Recovery actions are unavailable. Reopen the order to try again.</p>' : ""}
      ${this.recoveryLoading ? "<p>Loading recovery history...</p>" : ""}
      ${this.recoveryError ? '<p role="alert">We could not load gift-card recovery history. Recovery actions are unavailable. Reopen the order to try again.</p>' : ""}
      ${this.recoveryAttempts === void 0 && !this.recoveryLoading && !this.recoveryError ? "<p>Recovery history has not been checked. Recovery actions are unavailable.</p>" : ""}
      ${(this.recoveryAttempts || []).map((r) => {
      const i = r.canReconcile && !!r.reconcileToken;
      return `<article><h5>Preparation attempt: ${a(r.attemptId)}</h5>
          <dl>${e("Created", E(r.createdUtc))}${e("Recovery state", r.state)}${e("Reason", r.reason)}</dl>
          ${r.hasUnknownEffects ? '<p role="alert">Unknown provider effects need reconciliation. A missing claim reference is not evidence that no hold exists. Do not release by card number or amount.</p>' : ""}
          ${r.claims.map((n) => {
        const s = n.canRelease && !!n.releaseToken;
        return `<div class="ekmGiftcardRecovery__claim"><dl>${e("Masked card", n.cardReference)}${e("Claim reference", n.claimReference)}${e("Provider", n.provider)}${e("Account", n.account)}${e("Requested amount", `${n.requestedAmount} ${n.currency}`)}${e("Raw returned amount", n.rawReturnedAmount)}${e("Returned amount type", n.returnedAmountType)}${e("Raw expiry", n.rawExpiry)}${e("Cleanup status", n.cleanupState)}</dl>
              ${n.claimReference ? `<button type="button" class="btn-outline" data-action="release-giftcard-recovery" data-attempt-id="${a(r.attemptId)}" data-claim-id="${a(n.id)}" ${!s || this.recoveryBusy ? "disabled" : ""}>Release failed-preparation hold${this.recoveryOperation === `release:${r.attemptId}:${n.id}` ? " (Working...)" : ""}</button>` : "<p>Missing claim reference: needs reconciliation. No release action is available.</p>"}
              <p>${a(s && n.claimReference ? "Release allowed after confirmation." : n.unavailableReason || "Release unavailable; check reconciliation requirements.")}</p></div>`;
      }).join("")}
          ${!r.claims.length && r.hasUnknownEffects ? "<p>No known claim to release. Reconcile unknown provider effects.</p>" : ""}
          <button type="button" class="btn-outline" data-action="reconcile-giftcard-recovery" data-attempt-id="${a(r.attemptId)}" ${!i || this.recoveryBusy ? "disabled" : ""}>Reconcile failed preparation${this.recoveryOperation === `reconcile:${r.attemptId}` ? " (Working...)" : ""}</button>
          <p>${a(i ? "Reconciliation allowed after confirmation." : r.unavailableReason || "Reconciliation unavailable.")}</p>
        </article>`;
    }).join("")}</section>`;
  }
  renderDesignOrderLines(e) {
    const t = Array.isArray(e.orderLines) ? e.orderLines : [], r = t.reduce((i, n) => i + (Number(n.quantity) || 0), 0);
    return `<div class="ekom-card-heading ekom-lines-heading"><h3>Order lines</h3><span>${t.length} ${t.length === 1 ? "line" : "lines"} · ${r} ${r === 1 ? "item" : "items"}</span><button type="button" data-action="open-order-line-editor">Add order line</button></div><div class="ekom-lines-scroll"><table class="ekom-design-lines"><thead><tr><th>Product</th><th>Quantity</th><th>Unit price</th><th>VAT</th><th>Discount</th><th>Total</th><th><span class="ekom-sr-only">Actions</span></th></tr></thead><tbody>${t.map((i) => {
      var n, s, d, l, p, f, g, m, u, y, O, w;
      return `<tr><td><div class="ekmOrderLineProduct">${Q(i)}<div>${Y(i.product)}<small>${a((n = i.product) == null ? void 0 : n.sku)}</small>${i.variant ? `<small>${a(i.variant.title)}${typeof i.variant.sku == "string" && i.variant.sku.trim() ? ` (${a(i.variant.sku)})` : ""}</small>` : ""}${J(i)}</div></div></td><td>${a(i.quantity)}</td><td>${a((l = (d = (s = i.product) == null ? void 0 : s.price) == null ? void 0 : d.withVat) == null ? void 0 : l.currencyString)}</td><td>${a((f = (p = i.amount) == null ? void 0 : p.vat) == null ? void 0 : f.currencyString)}</td><td>${a((m = (g = i.amount) == null ? void 0 : g.discountAmount) == null ? void 0 : m.currencyString)}</td><td><strong>${a((y = (u = i.amount) == null ? void 0 : u.withVat) == null ? void 0 : y.currencyString)}</strong></td><td><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${a(i.key)}" data-product-title="${a((O = i.product) == null ? void 0 : O.title)}" ${this.removingOrderLineId === i.key ? "disabled" : ""} aria-label="Remove ${a((w = i.product) == null ? void 0 : w.title)}">×</button></td></tr>`;
    }).join("")}</tbody></table></div>`;
  }
  renderOrderLines(e) {
    var n, s, d, l, p, f, g;
    const t = Array.isArray(e.orderLines) ? e.orderLines : [], r = typeof e.coupon == "string" ? e.coupon.trim() : "", i = r ? `Discount (${r})` : "Discount";
    return `
      <div style="align-items:center; display:flex; gap:10px; justify-content:space-between;"><h4>Order Details</h4><button type="button" class="btn-outline" data-action="open-order-line-editor">Add order line</button></div>
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed">Product</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Unit Price (inc VAT)</div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">Total (inc VAT)</div></div></div>
        <div class="umb-table-body">
          ${t.map((m) => {
      var u, y, O, w, L, A, S, $, C, x, U, V, B, K;
      return `<div class="umb-table-row"><div class="umb-table-cell"><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${a(m.key)}" data-product-title="${a((u = m.product) == null ? void 0 : u.title)}" ${this.removingOrderLineId === m.key ? "disabled" : ""} aria-label="Remove ${a((y = m.product) == null ? void 0 : y.title)}" title="Remove order line">&#128465;</button></div><div class="umb-table-cell not-fixed"><div class="ekmOrderLineProduct">${Q(m)}<div class="ekmOrderLineProduct__details"><span>${Y(m.product)}${(w = (O = m.product) == null ? void 0 : O.sku) != null && w.trim() ? ` (${a(m.product.sku)})` : ""}</span>${m.variant ? `<small style="display:block; margin-top:3px;">${a(m.variant.title)} ${(L = m.variant.sku) != null && L.trim() ? `(${a(m.variant.sku)})` : ""}</small>` : ""}${J(m)}</div></div></div><div class="umb-table-cell">${a(m.quantity)}</div><div class="umb-table-cell">${a(($ = (S = (A = m.product) == null ? void 0 : A.price) == null ? void 0 : S.withVat) == null ? void 0 : $.currencyString)}</div><div class="umb-table-cell">${a((x = (C = m.amount) == null ? void 0 : C.vat) == null ? void 0 : x.currencyString)}</div><div class="umb-table-cell">-${a((V = (U = m.amount) == null ? void 0 : U.discountAmount) == null ? void 0 : V.currencyString)}</div><div class="umb-table-cell"><strong>${a((K = (B = m.amount) == null ? void 0 : B.withVat) == null ? void 0 : K.currencyString)}</strong></div></div>`;
    }).join("")}
        </div>
        <div class="umb-table-footer">
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Sub Total (inc VAT)</div><div class="umb-table-cell">${a((s = (n = e.subTotal) == null ? void 0 : n.withVat) == null ? void 0 : s.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">${a(i)}</div><div class="umb-table-cell">-${a((d = e.discountAmount) == null ? void 0 : d.currencyString)}</div></div>
          ${e.shippingProvider ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Shipping Total</div><div class="umb-table-cell">${a((p = (l = e.shippingProvider.price) == null ? void 0 : l.withVat) == null ? void 0 : p.currencyString)}</div></div>` : ""}
          ${this.renderGiftcardTotal(e)}
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">${a((f = e.chargedVat) == null ? void 0 : f.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Total</div><div class="umb-table-cell"><strong>${a((g = e.chargedAmount) == null ? void 0 : g.currencyString)}</strong></div></div>
        </div>
      </div>
    `;
  }
  /** Value of the order's gift cards that are claimed or have not expired. */
  giftcardEligibleValue(e) {
    return (Array.isArray(e.giftcards) ? e.giftcards : []).reduce((r, i) => r + (i.amount > 0 && (i.claimed || !i.validUntil || Date.parse(i.validUntil) > Date.now()) ? i.amount : 0), 0);
  }
  renderGiftcardTotal(e) {
    const t = this.giftcardEligibleValue(e);
    return t ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell" title="Payable total already includes applicable gift cards; eligible value may exceed the order total.">Gift cards (eligible value)</div><div class="umb-table-cell">${a(this.formatGiftcardAmount(t, e))}</div></div>` : "";
  }
  renderTracking(e) {
    const t = e.tracking;
    return Ce(t) ? `<div class="ekmOrderTracking"><h4>Tracking</h4>${Pe(t)}</div>` : '<div class="ekmOrderTracking"><h4>Tracking</h4><p>No tracking data was captured for this order.</p></div>';
  }
  renderConsent(e) {
    const t = e.consent;
    return Re(t) ? `<div class="ekmOrderTracking"><h4>Consent</h4>${b("Resolved", E(t.resolvedAtUtc))}${b("Source", t.source)}${b("Analytics", ee(t.analytics))}${b("Marketing", ee(t.marketing))}</div>` : '<div class="ekmOrderTracking"><h4>Consent</h4><p>No consent data was captured for this order.</p></div>';
  }
  renderActivityLogs() {
    return this.orderLogsLoading ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Loading activity...</p></div>' : this.orderLogsError ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Unable to load activity log.</p></div>' : this.orderLogs.length ? `<div class="ekmOrderActivityLog"><h4>Activity log</h4><div class="ekom-log-scroll"><table class="ekom-log-table"><thead><tr><th>Date</th><th>Type</th><th>Message</th></tr></thead><tbody>${this.orderLogs.map((t, r) => {
      const i = t.message ?? "", n = this.activityLogExpandedIndexes.has(r), s = this.canExpandActivityLog(i) ? `<button type="button" class="btn-reset ekmOrderActivityLog__toggle" data-action="toggle-activity-log" data-log-index="${r}">${n ? "Show less" : "Show more"}</button>` : "";
      return `<tr><td class="ekmOrderActivityLog__date">${a(E(t.date))}</td><td><span class="ekom-log-type ${this.getActivityLogTypeClass(t)}">${t.logType === z ? "Success" : t.logType === W ? "Alert" : "Info"}</span></td><td><div class="ekmOrderActivityLog__message${n ? " ekmOrderActivityLog__message--expanded" : ""}">${a(i)}</div>${s}</td></tr>`;
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
    return e.length > Se;
  }
  renderStateDot(e) {
    return `<span class="ekom-state-dot" data-state="${a(e || "")}" aria-hidden="true"></span>`;
  }
  renderOrderActions() {
    const e = this.orderActions.filter((t) => t.giftcardCode == null);
    return `<div class="ekom-detail-actions"><button type="button" data-action="print-order" class="ekom-print-button" aria-label="Print order" title="Print order"><svg aria-hidden="true" width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" stroke-linejoin="round"><path d="M6 9V3h12v6"></path><rect x="3" y="9" width="18" height="8" rx="2"></rect><path d="M6 14h12v7H6z"></path></svg></button>${e.length ? '<span class="ekom-action-divider" aria-hidden="true"></span>' : ""}${e.map((t) => this.renderAction(t)).join("")}${this.orderActionsLoading ? '<span role="status">Loading actions...</span>' : ""}</div>`;
  }
  renderAction(e) {
    const t = this.executingActionKey === e.key;
    return `<span style="display:inline-block; margin:4px;"><button type="button" class="${e.look === "primary" ? "btn-success" : "btn-outline"}" data-action="execute-order-action" data-action-key="${a(e.key)}" ${e.enabled === !1 || this.executingActionKey || this.releasingOrderLock || this.recoveryOperation ? "disabled" : ""}>${a(e.label)}${t ? " (Working...)" : ""}</button>${e.enabled === !1 && e.disabledReason ? `<small style="display:block;">${a(e.disabledReason)}</small>` : ""}</span>`;
  }
  renderOrderLockStatus() {
    const e = this.orderLockStatus, t = this.orderLockWarning ? `<p role="alert">${a(this.orderLockWarning)}</p>` : "", r = this.orderLockError ? '<p role="alert">We could not check whether checkout is locked. Reopen the order to try again.</p>' : "", i = this.orderLockReleaseError ? `<p role="alert">${a(this.orderLockReleaseError)}</p>` : "";
    if (!(e != null && e.hasLock))
      return `${t}${r}${i}${this.orderLockLoading ? "<p>Checking checkout...</p>" : ""}`;
    const n = !e.canRelease || !e.releaseToken ? e.releaseUnavailableReason || "This checkout lock cannot be released right now." : "";
    return `<section class="ekmOrderTracking"><h4>Checkout is locked</h4>${t}${r}${i}<p>${e.reconciliationRequired && !e.paymentOperationLocked && !e.preparationLocked ? "The previous payment needs checking before the customer can pay or edit their basket." : "A previous checkout did not finish. This prevents the customer from paying or editing their basket."}</p><p>If you have checked that no payment is being processed, Release Lock resets checkout and lets the customer try again.</p><button type="button" class="btn-outline" data-action="release-order-lock" ${n || this.orderLockLoading || this.orderLockError || this.releasingOrderLock || this.executingActionKey || this.recoveryOperation ? "disabled" : ""}>Release Lock</button>${n ? `<p>${a(n)}</p>` : ""}<details><summary>Technical details</summary><ul>${e.paymentOperationLocked ? "<li>Payment operation ownership retained.</li>" : ""}${e.preparationLocked ? "<li>Checkout preparation ownership retained.</li>" : ""}${e.reconciliationRequired ? "<li>Reconciliation required.</li>" : ""}</ul><p>Attempt ID: ${a(e.attemptId || "Not available")}</p><p>Attempt state: ${a(e.attemptState || "Not available")}</p></details></section>`;
  }
  renderCustomerEditor() {
    const e = this.customerEditModel;
    return e ? `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel ekom-editor-customer" role="dialog" aria-modal="true" aria-label="Edit customer information"><div class="ekmOverlay__header"><h3>Edit customer information</h3><button class="btn-reset" type="button" aria-label="Close edit customer information" data-action="close-customer-editor" ${this.customerSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><div class="ekmSplit"><div class="ekmSplit__column"><h4>Billing</h4>${this.renderCustomerFields(e.customer, "customer")}</div><div class="ekmSplit__column"><h4>Shipping</h4>${this.renderCustomerFields(e.shipping, "shipping")}</div></div></div><div class="ekom-editor-footer"><button class="btn-outline" type="button" data-action="close-customer-editor" ${this.customerSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-customer-information" ${this.customerSaving ? "disabled" : ""}>${this.customerSaving ? "Saving..." : "Save customer information"}</button></div></div></div>` : "";
  }
  renderCustomerFields(e, t) {
    return e.map((r) => `<label class="control-group">${a(r.label)} ${r.isExtra ? `<small>(${a(r.key)})</small>` : ""}<input type="text" data-customer-group="${t}" data-customer-key="${a(r.key)}" value="${a(r.value)}" ${this.customerSaving ? "disabled" : ""}></label>`).join("");
  }
  renderOrderLineEditor() {
    const e = this.orderLineEditModel;
    return e ? `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel ekom-editor-order-line" role="dialog" aria-modal="true" aria-label="Add order line"><div class="ekmOverlay__header"><h3>Add order line</h3><button class="btn-reset" type="button" aria-label="Close add order line" data-action="close-order-line-editor" ${this.orderLineSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><label class="control-group">Product ID<input type="text" data-order-line-field="productId" value="${a(e.productId)}" required ${this.orderLineSaving ? "disabled" : ""}></label><label class="control-group">Variant ID<input type="text" data-order-line-field="variantId" value="${a(e.variantId)}" ${this.orderLineSaving ? "disabled" : ""}></label><label class="control-group" style="padding-bottom:20px;">Quantity<input type="number" min="0.000001" step="any" data-order-line-field="quantity" value="${a(e.quantity)}" required ${this.orderLineSaving ? "disabled" : ""}></label></div><div class="ekom-editor-footer"><button class="btn-outline" type="button" data-action="close-order-line-editor" ${this.orderLineSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-order-line" ${this.orderLineSaving ? "disabled" : ""}>${this.orderLineSaving ? "Adding..." : "Add order line"}</button></div></div></div>` : "";
  }
  renderShippingProviderEditor() {
    var e;
    return `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel ekom-editor-shipping-provider" role="dialog" aria-modal="true" aria-label="Shipping provider"><div class="ekmOverlay__header"><h3>${(e = this.selectedOrder) != null && e.shippingProvider ? "Change shipping provider" : "Add shipping provider"}</h3><button class="btn-reset" type="button" aria-label="Close shipping provider" data-action="close-shipping-provider-editor" ${this.shippingProviderSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><label class="control-group">Shipping provider<select data-field="shippingProviderId" ${this.shippingProviderSaving ? "disabled" : ""}><option value="">Select a provider</option>${this.shippingProviders.map((t) => `<option value="${a(t.key)}" ${this.shippingProviderId === t.key ? "selected" : ""}>${a(t.title)}</option>`).join("")}</select></label>${this.shippingProviders.length ? "" : "<p>No shipping providers available for this store.</p>"}<div class="ekom-provider-data-heading"><h4>Provider data</h4><p>Stored on the order. Keys are kept as-is; new keys are prefixed <code>customshipping</code>.</p></div><div class="ekom-provider-field-labels" aria-hidden="true"><span>Key</span><span>Value</span></div><div class="ekom-provider-fields">${this.shippingCustomFields.map((t, r) => `<div class="ekom-provider-field-row"><input aria-label="Key ${r + 1}" type="text" data-shipping-key="${r}" value="${a(t.key)}" ${t.existing ? "readonly" : ""} ${this.shippingProviderSaving ? "disabled" : ""} placeholder="TrackingNumber" maxlength="100"><input aria-label="Value ${r + 1}" type="text" data-shipping-value="${r}" value="${a(t.value)}" ${this.shippingProviderSaving ? "disabled" : ""} maxlength="4096"></div>`).join("")}</div><button class="btn-outline" type="button" data-action="add-shipping-custom-field" ${this.shippingProviderSaving ? "disabled" : ""}>Add custom field</button></div><div class="ekom-editor-footer"><p>Changes apply to this order only.</p><button class="btn-outline" type="button" data-action="close-shipping-provider-editor" ${this.shippingProviderSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-shipping-provider" ${this.shippingProviderSaving || !this.shippingProviders.length ? "disabled" : ""}>${this.shippingProviderSaving ? "Saving..." : "Save shipping provider"}</button></div></div></div>`;
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
    var e, t, r, i, n;
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
        const l = s.getBoundingClientRect(), p = window.innerHeight - l.bottom - 22, f = l.top - 86;
        d.offsetHeight > p && f > p ? (d.style.top = "auto", d.style.bottom = "calc(100% + 6px)", d.style.maxHeight = `${Math.max(120, f)}px`) : d.style.maxHeight = `${Math.max(120, p)}px`;
      }), s.addEventListener("keydown", (d) => {
        var l;
        d.key === "Escape" && (d.preventDefault(), d.stopPropagation(), s.open = !1, (l = s.querySelector("summary")) == null || l.focus());
      });
    }), this.querySelectorAll("[data-poll-seconds]").forEach((s) => s.addEventListener("click", () => this.setPolling(Number(s.dataset.pollSeconds)))), (i = this.querySelector(".ekom-polling")) == null || i.addEventListener("keydown", (s) => {
      var l;
      if (s.key !== "Escape") return;
      const d = s.currentTarget;
      d.open = !1, (l = d.querySelector("summary")) == null || l.focus(), s.stopPropagation();
    }), fe(this, () => {
      this.page = 1, this.loadOrders();
    }), this.querySelectorAll('[data-action="select-order-tab"]').forEach((s) => {
      s.addEventListener("keydown", (d) => {
        if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(d.key)) return;
        d.preventDefault();
        const l = ["order", "activity", "tracking"], p = l.indexOf(this.orderTab), f = d.key === "Home" ? 0 : d.key === "End" ? l.length - 1 : (p + (d.key === "ArrowRight" ? 1 : -1) + l.length) % l.length;
        this.selectOrderTab(l[f]);
      });
    }), this.querySelectorAll(".ekmOrderLineThumbnail img").forEach((s) => {
      const d = () => {
        s.hidden = !0;
      };
      s.addEventListener("error", d, { once: !0 }), s.complete && s.naturalWidth === 0 && d();
    }), this.overlay === "order" && ((n = this.querySelector(".ekmOverlay")) == null || n.addEventListener("click", (s) => {
      s.target === s.currentTarget && !this.customerEditorOpen && !this.orderLineEditorOpen && !this.shippingProviderEditorOpen && !this.activeReview && this.closeOverlay();
    })), this.querySelectorAll("[data-action]").forEach((s) => {
      s instanceof HTMLSelectElement ? s.addEventListener("change", (d) => void this.handleAction(d)) : s.addEventListener("click", (d) => void this.handleAction(d));
    }), this.querySelectorAll("[data-field]").forEach((s) => {
      s.addEventListener("change", (d) => void this.handleFieldChange(d)), s.dataset.field === "query" && s.addEventListener("input", (d) => this.handleSearchInput(d));
    });
  }
  async handleAction(e) {
    var i, n, s, d, l, p, f, g, m;
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
      const u = (n = this.selectedOrder) == null ? void 0 : n.uniqueId;
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
      const u = t.closest("details"), y = t.dataset.value || "", O = this.querySelector('[data-field="fulfillmentState"]');
      O && (O.value = y), u.querySelector("[data-fulfillment-label]").textContent = y.replace(/([a-z])([A-Z])/g, "$1 $2"), u.querySelector("summary .ekom-state-dot").dataset.state = y, u.querySelectorAll("button[data-value]").forEach((w) => {
        const L = w.dataset.value === y;
        w.setAttribute("aria-pressed", String(L)), w.querySelector(".ekom-status-check").textContent = L ? "✓" : "";
      }), u.removeAttribute("open"), (s = u.querySelector("summary")) == null || s.focus();
      return;
    }
    if (r === "save-fulfillment") {
      const u = (d = this.querySelector('[data-field="fulfillmentState"]')) == null ? void 0 : d.value;
      u && u !== ((p = (l = this.selectedOrder) == null ? void 0 : l.status) == null ? void 0 : p.fulfillmentStatus) && await this.changeState(u === "Fulfilled" ? "Fulfill" : u);
      return;
    }
    if (r === "select-order-tab") {
      this.selectOrderTab(t.dataset.tab || "order");
      return;
    }
    if (r === "open-customer") {
      const u = (g = (f = this.selectedOrder) == null ? void 0 : f.customerInformation) == null ? void 0 : g.customer;
      v.pendingCustomer = { email: (u == null ? void 0 : u.email) || "", username: (u == null ? void 0 : u.userName) || "" };
      const y = document.createElement("a");
      y.href = location.pathname.replace(/\/orders\/?$/, "/customers"), this.appendChild(y), y.click(), y.remove();
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
          await ge(this.renderPrintableOrder(this.selectedOrder), `Order ${this.selectedOrder.referenceId}`);
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
      this.readShippingCustomFields(), this.shippingProviderId = ((m = this.querySelector('[data-field="shippingProviderId"]')) == null ? void 0 : m.value) || "", this.shippingCustomFields.push({ key: "", value: "", existing: !1 }), this.render();
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
        ye(e, this.exportIncludeOrderLines ? "orders-with-orderlines.csv" : "orders.csv"), this.overlay = "";
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
    ++this.lockRequestId, ++this.recoveryRequestId, this.recoveryAttempts = void 0, this.recoveryLoading = !1, this.recoveryError = !1, this.recoveryOperation = "", this.recoveryMessage = "", this.recoveryActionError = "", this.recoveryRefreshError = !1, this.recoveryScrollTop = void 0, this.notifyOrderStatusOverride = void 0, this.selectedOrder = void 0, this.orderActions = [], this.orderActionsError = !1, this.executingActionKey = "", this.orderLockStatus = void 0, this.releasingOrderLock = !1, this.orderLockWarning = "", this.orderLockReleaseError = "", this.orderLockError = !1, this.orderLockLoading = !1;
    const r = this.overlay;
    this.orderOpening = !0, this.overlay = "order", this.render();
    try {
      const i = await this.api.orderInfo(e);
      if (t !== this.orderRequestId) return;
      this.selectedOrder = i, this.reviews = [], this.activeReview = void 0, this.reviewError = "", this.orderLogs = [], this.orderLogsError = !1, this.activityLogExpandedIndexes.clear(), await Promise.all([this.loadOrderLogs(e), this.loadOrderActions(e), this.loadOrderLockStatus(e), this.loadGiftcardRecovery(e), this.loadOrderReviews(e)]);
    } catch (i) {
      if (t !== this.orderRequestId) return;
      this.selectedOrder || (this.overlay = r === "order" ? "" : r), this.showError(k(i, "Error on getting orderInfo."));
    } finally {
      t === this.orderRequestId && (this.orderOpening = !1, this.render());
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
    return `<div class="ekom-review-alert"><span class="ekom-review-icon" aria-hidden="true">!</span><div><strong>${a(e.title)}</strong><small>${a(e.source)} · ${a(E(e.createdUtc))}</small><p>${a(e.message)}</p></div>${t ? `<button type="button" data-action="open-review" data-review-id="${a(e.id)}">Review</button>` : ""}</div>`;
  }
  renderReviews() {
    return `${this.reviewError && !this.activeReview ? `<p role="alert">${a(this.reviewError)}</p>` : ""}${this.reviews.map((e) => this.renderReviewAlert(e, !0)).join("")}`;
  }
  renderReviewDialog() {
    var e;
    return `<div class="ekmCustomerInformationModal ekom-review-overlay"><section class="ekmCustomerInformationModal__panel ekom-review-panel" role="dialog" aria-modal="true" aria-label="Review alert">
      <header><h2>Review alert · Order #${a((e = this.selectedOrder) == null ? void 0 : e.referenceId)}</h2><button type="button" data-action="close-review" aria-label="Close review" ${this.reviewSaving ? "disabled" : ""}>×</button></header>
      <div class="ekom-review-body">${this.renderReviewAlert(this.activeReview)}
      <label for="ekom-review-comment">What was done</label><textarea id="ekom-review-comment" data-review-comment maxlength="4000" rows="4" placeholder="Describe how this was resolved or what was checked." ${this.reviewSaving ? "disabled" : ""}>${a(this.reviewComment)}</textarea>
      <p class="ekom-muted">Saved to the activity log with your name and the time.</p>
      <label class="ekom-review-checkbox"><input type="checkbox" data-review-resolved ${this.reviewResolved ? "checked" : ""} ${this.reviewSaving ? "disabled" : ""}><span>Problem is resolved <small>— untick to log a note but keep the alert open</small></span></label>
      ${this.reviewError ? `<p role="alert">${a(this.reviewError)}</p>` : ""}</div>
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
      var n;
      return t === this.lockRequestId && r === this.orderRequestId && e === ((n = this.selectedOrder) == null ? void 0 : n.uniqueId);
    };
    this.orderLockLoading = !0, this.orderLockError = !1, this.render();
    try {
      const n = await this.api.orderLockStatus(e);
      i() && (this.orderLockStatus = n);
    } catch {
      i() && (this.orderLockError = !0);
    } finally {
      i() && (this.orderLockLoading = !1, this.render());
    }
  }
  async loadGiftcardRecovery(e) {
    const t = ++this.recoveryRequestId, r = this.orderRequestId, i = () => {
      var n;
      return t === this.recoveryRequestId && r === this.orderRequestId && e === ((n = this.selectedOrder) == null ? void 0 : n.uniqueId);
    };
    this.recoveryLoading = !0, this.recoveryError = !1, this.render();
    try {
      const n = await this.api.giftcardRecovery(e);
      if (!Array.isArray(n) || n.some((s) => !Array.isArray(s.claims)))
        throw new Error("Invalid recovery history response.");
      i() && (this.recoveryAttempts = n);
    } catch {
      i() && (this.recoveryError = !0);
    } finally {
      i() && (this.recoveryLoading = !1, this.render());
    }
  }
  async executeGiftcardRecovery(e, t) {
    var g, m, u;
    const r = (g = this.selectedOrder) == null ? void 0 : g.uniqueId, i = (m = this.recoveryAttempts) == null ? void 0 : m.find((y) => y.attemptId === e);
    if (!r || !i || this.recoveryBusy) return;
    const n = t !== void 0, s = n ? i.claims.find((y) => y.id === t) : void 0, d = n ? s == null ? void 0 : s.releaseToken : i.reconcileToken;
    if (!d || (n ? !(s != null && s.canRelease) || !s.claimReference : !i.canReconcile)) return;
    const l = n ? `Release the failed-preparation hold for masked card ${s.cardReference}, claim ${s.claimReference}? This calls the provider through audited recovery. It does not refund an accepted payment or retry payment or checkout. Unknown effects may still need reconciliation.` : "Reconcile this failed gift-card preparation? The audited backend will check known claims and unknown provider effects. This does not refund an accepted payment or retry payment or checkout. Unresolved effects may remain blocked.";
    if (!window.confirm(l)) return;
    const p = this.orderRequestId, f = () => {
      var y;
      return p === this.orderRequestId && r === ((y = this.selectedOrder) == null ? void 0 : y.uniqueId);
    };
    this.recoveryOperation = n ? `release:${e}:${t}` : `reconcile:${e}`, this.recoveryScrollTop = (u = this.querySelector(".ekmOverlay")) == null ? void 0 : u.scrollTop, this.recoveryMessage = "", this.recoveryActionError = "", this.render();
    try {
      const y = n ? await this.api.releaseGiftcardRecovery(r, e, s.id, d) : await this.api.reconcileGiftcardRecovery(r, e, d);
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
    let n = !1;
    this.render();
    try {
      const d = await this.api.releaseOrderLock(e, t.releaseToken);
      if (!i()) return;
      n = !0, this.orderLockWarning = d.warning || "", this.showSuccess(d.message);
      const [l] = await Promise.all([this.api.orderInfo(e), this.loadOrderLogs(e), this.loadOrderActions(e), this.loadOrderLockStatus(e), this.loadGiftcardRecovery(e), this.loadOrderReviews(e)]);
      i() && (this.selectedOrder = l);
    } catch (d) {
      if (!i()) return;
      this.orderLockReleaseError = n ? "Checkout was unlocked, but we could not refresh all order details. Reopen the order to check them." : k(d, "We could not confirm the checkout reset. Check the updated order before trying again."), await Promise.all([
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
    var i, n;
    if (!this.config || !((i = this.selectedOrder) != null && i.uniqueId))
      return;
    const e = this.querySelector('[data-field="orderStatusOverlay"]'), t = (e == null ? void 0 : e.value) || "", r = this.notifyOrderStatus;
    try {
      await this.api.changeOrderStatus(this.selectedOrder.uniqueId, t, r), this.selectedOrder.orderStatus = t, this.showSuccess("Order status updated."), await this.loadOrders(), await this.loadOrderLogs(this.selectedOrder.uniqueId);
    } catch (s) {
      e && (e.value = this.getOrderStatusValue((n = this.selectedOrder) == null ? void 0 : n.orderStatus)), this.showError(k(s, "Error updating order status."));
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
    this.orderOpening = !1, ++this.orderRequestId, ++this.lockRequestId, ++this.recoveryRequestId, this.recoveryOperation = "", this.recoveryScrollTop = void 0, this.overlay = "", this.selectedOrder = void 0, this.customerEditorOpen = !1, this.customerEditModel = void 0, this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.shippingProviderEditorOpen = !1, this.render();
    const e = Array.from(this.querySelectorAll('[data-action="view-order"]')).find((t) => t.dataset.orderId === this.returnFocusOrderId);
    e == null || e.focus(), this.returnFocusOrderId = void 0;
  }
  getOrderStatusValue(e) {
    const t = String(e ?? ""), r = v.statusList.find((i) => String(i.value ?? "") === t || String(i.enumValue ?? "") === t);
    return r ? G(r) : t;
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
    var n;
    if (!((n = this.selectedOrder) != null && n.uniqueId) || !e || this.executingActionKey || this.releasingOrderLock || this.recoveryOperation)
      return;
    const t = this.orderActions.find((s) => s.key === e);
    if (!t || t.enabled === !1) return;
    const r = this.selectedOrder.uniqueId, i = this.orderRequestId;
    if (!(t != null && t.confirmMessage && !window.confirm(t.confirmMessage))) {
      this.executingActionKey = e, this.render();
      try {
        const s = await this.api.executeOrderAction(r, e), d = await s.blob(), l = s.headers.get("content-disposition") || "", p = s.headers.get("content-type") || "";
        if (l.toLowerCase().includes("filename=") || p.startsWith("application/pdf") || p.startsWith("application/octet-stream") || p.startsWith("image/"))
          window.open(URL.createObjectURL(d), "_blank");
        else {
          const g = await d.text();
          this.showSuccess(qe(g));
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
    var i, n;
    const e = this.selectedOrder;
    if (!e)
      return;
    const t = ((i = e.customerInformation) == null ? void 0 : i.customer) || {}, r = ((n = e.customerInformation) == null ? void 0 : n.shipping) || {};
    this.customerEditModel = {
      customer: te(t, we).concat(re(t.properties, "customer")),
      shipping: te(r, Oe).concat(re(r.properties, "shipping"))
    }, this.customerEditorOpen = !0, this.render();
  }
  async saveCustomerInformation() {
    var e;
    if (!(!((e = this.selectedOrder) != null && e.uniqueId) || !this.customerEditModel || this.customerSaving)) {
      this.querySelectorAll("[data-customer-group]").forEach((t) => {
        var s;
        const r = t.dataset.customerGroup, i = t.dataset.customerKey, n = (s = this.customerEditModel) == null ? void 0 : s[r].find((d) => d.key === i);
        n && (n.value = t.value);
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
    var n;
    if (!((n = this.selectedOrder) != null && n.uniqueId) || !this.orderLineEditModel || this.orderLineSaving)
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
        this.shippingProviders = await this.api.shippingProviders(i), this.shippingProviderId = ((t = this.selectedOrder.shippingProvider) == null ? void 0 : t.key) || "", this.shippingCustomFields = Object.entries(((r = this.selectedOrder.shippingProvider) == null ? void 0 : r.customData) || {}).filter(([n]) => n.toLowerCase().startsWith("customshipping")).map(([n, s]) => ({ key: n, value: q(s), existing: !0 })), this.shippingProviderEditorOpen = !0, this.render();
      } catch (i) {
        this.showError(k(i, "Error loading shipping providers."));
      }
  }
  async saveShippingProvider() {
    var i, n;
    if (!((i = this.selectedOrder) != null && i.uniqueId) || this.shippingProviderSaving)
      return;
    const e = ((n = this.querySelector('[data-field="shippingProviderId"]')) == null ? void 0 : n.value) || "";
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
function k(o, h) {
  return o instanceof Error ? o.message : h;
}
function P(o) {
  return o != null && (typeof o != "string" || o.trim() !== "");
}
function D(o) {
  if (o === "code") return "Gift-card number";
  if (o === "claimId") return "Reservation reference";
  if (o === "transactionId") return "Transaction reference";
  if (o === "usedDate") return "Used on";
  if (o === "validUntil") return "Saved expiry";
  if (o === "claimDate") return "Payment claim date";
  const h = o.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ");
  return h.charAt(0).toUpperCase() + h.slice(1).toLowerCase().replace(/\bid\b/g, "ID");
}
function j(o) {
  if (!P(o)) return "Not recorded";
  if (typeof o == "boolean") return o ? "Yes" : "No";
  if (Array.isArray(o)) return o.length ? o.map(j).join("; ") : "Empty list";
  if (typeof o == "object") {
    const h = Object.entries(o);
    return h.length ? h.map(([e, t]) => `${D(e)}: ${j(t)}`).join("; ") : "Empty object";
  }
  return String(o);
}
function H(o) {
  const h = ["code", "amount", "claimId", "claimDate", "transactionId", "usedDate", "claimed", "validUntil"];
  return [...h, ...Object.keys(o).filter((e) => !h.includes(e))].map((e) => ({ label: D(e), value: j(o[e]) }));
}
function Z(o, h = Date.now()) {
  const e = P(o.claimId), t = P(o.validUntil), r = t ? Date.parse(String(o.validUntil)) : NaN, i = r > h, n = o.claimed === !0 || P(o.transactionId) || P(o.claimDate) || P(o.usedDate), s = n ? "Used" : e && i ? "Reserved" : e && r <= h ? "Reservation expired" : e || t ? "Needs checking" : "Selected";
  let d = "";
  !n && !e && t ? d = "An expiry time is saved, but the reservation reference is missing. Check the gift-card provider if an amount is still held." : !n && e && r <= h ? d = "The saved reservation expiry has passed. This does not confirm that the provider has released the hold." : !n && e && !t && (d = "No expiry time is saved. Check the gift-card provider if an amount is still held."), t && !Number.isFinite(r) && (d += `${d ? " " : ""}The saved expiry time could not be read. Check the gift-card provider.`);
  let l = "Not recorded";
  if (t && (l = Number.isFinite(r) ? be() ? E(new Date(r)) : new Intl.DateTimeFormat(void 0, { dateStyle: "medium", timeStyle: "long" }).format(new Date(r)) : String(o.validUntil), i)) {
    const p = Math.ceil((r - h) / 6e4), f = p < 60 ? p : p < 1440 ? Math.ceil(p / 60) : Math.ceil(p / 1440), g = p < 60 ? "minute" : p < 1440 ? "hour" : "day";
    l += ` (in about ${f} ${g}${f === 1 ? "" : "s"})`;
  }
  return { status: s, warning: d, expiryLabel: !n && e ? "Reservation ends" : "Saved expiry", expiry: l };
}
function T(o) {
  return o === "zipCode" ? "Zipcode" : o.charAt(0).toUpperCase() + o.slice(1);
}
function Ee(o) {
  return (/* @__PURE__ */ new Set(["shippingname", "shippingaddress", "shippingcity", "shippingcountry", "shippingemail", "shippingapartment", "shippingzipcode", "shippingphone", "customeremail", "customername", "customeraddress", "customerapartment", "customercity", "customercountry", "customerzipcode", "customerphone"])).has(o.toLowerCase());
}
function R(o) {
  return o.replace(/^customshipping/i, "").replace(/^custompayment/i, "").replace(/^shipping/i, "").replace(/^customer/i, "");
}
function _(o, h) {
  return Object.entries(o || {}).filter(([e, t]) => (!!t || h === "customshipping" && t === "") && e.toLowerCase().startsWith(h) && !Ee(e)).map(([e, t]) => [e, q(t)]);
}
function M(o) {
  return !!(o != null && o.name || o != null && o.email || o != null && o.address || o != null && o.apartment || o != null && o.city || o != null && o.country || o != null && o.zipCode || o != null && o.phone);
}
function Ie(o) {
  return o.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/([A-Z])([A-Z][a-z])/g, "$1 $2").replace(/[_-]+/g, " ").trim().replace(/\b[a-z]/g, (h) => h.toUpperCase());
}
function N(o, h) {
  const e = o.indexOf("#"), t = e >= 0 ? o.slice(e) : "", r = e >= 0 ? o.slice(0, e) : o, i = r.indexOf("?"), n = i >= 0 ? r.slice(0, i) : r, s = new URLSearchParams(i >= 0 ? r.slice(i + 1) : "");
  return s.set("width", String(h)), s.set("format", "webp"), s.set("rmode", "pad"), s.set("rcolor", "ffffff"), `${n}?${s}${t}`.replace(/,/g, "%2C").replace(/ /g, "%20");
}
function Q(o) {
  var t, r;
  const e = [(t = o.variant) == null ? void 0 : t.images, (r = o.product) == null ? void 0 : r.images].flatMap((i) => Array.isArray(i) ? i : []).map((i) => {
    if (typeof (i == null ? void 0 : i.url) != "string" || !i.url.trim()) return "";
    try {
      const n = i.url.trim();
      return ["http:", "https:"].includes(new URL(n, "https://ekom.invalid/").protocol) ? n : "";
    } catch {
      return "";
    }
  }).find(Boolean);
  return `<span class="ekmOrderLineThumbnail" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8" cy="8" r="1.5"/><path d="m3 17 6-6 4 4 3-3 5 5"/></svg>${e ? `<picture><source srcset="${a(N(e, 56))} 1x, ${a(N(e, 112))} 2x"><img src="${a(N(e, 56))}" width="56" height="56" alt="" loading="lazy" decoding="async"></picture>` : ""}</span>`;
}
function Y(o) {
  const h = a(o == null ? void 0 : o.title), e = typeof (o == null ? void 0 : o.url) == "string" ? o.url.trim() : "";
  if (!e) return h;
  try {
    const t = new URL(e, "https://ekom.invalid/").protocol;
    if (t !== "http:" && t !== "https:") return h;
  } catch {
    return h;
  }
  return `<a href="${a(e)}" target="_blank" rel="noopener noreferrer">${h}</a>`;
}
function J(o) {
  var e;
  const h = ((e = o.orderLineInfo) == null ? void 0 : e.properties) || {};
  return Object.entries(h).filter(([, t]) => !!t).map(([t, r]) => `<small style="display:block; margin-top:3px;"><strong>${a(Ae(t))}</strong>: ${a(q(r))}</small>`).join("");
}
function Ae(o) {
  const h = o.replace(/^orderline/i, "").replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim();
  return h ? h.charAt(0).toUpperCase() + h.slice(1) : o;
}
function Ce(o) {
  var h, e, t, r;
  return !!(o && (o.source || o.medium || o.campaign || o.term || o.content || o.clickId || o.clickIdType || o.landingUrl || o.referrer || o.captureMethod || o.capturedAtUtc || o.hasCookieSupport !== null && o.hasCookieSupport !== void 0 || (h = o.ga4) != null && h.clientId || (e = o.ga4) != null && e.sessionId || (t = o.meta) != null && t.fbp || (r = o.meta) != null && r.fbc || se(o.algolia)));
}
function Pe(o) {
  var n, s, d, l, p, f;
  const h = Object.entries(((n = o.ga4) == null ? void 0 : n.data) || {}), e = Object.entries(((s = o.meta) == null ? void 0 : s.data) || {}), t = o.algolia, r = Array.isArray(t == null ? void 0 : t.lines) ? t.lines : [], i = se(t) ? `<div class="ekmOrderTracking__provider"><h5>Algolia</h5>${b("User token", t.userToken)}${r.length ? `<ul>${r.map((g) => `<li class="ekmOrderTracking__wrap"><strong>Order line key</strong>: ${a(g.orderLineKey)}<br><strong>Query ID</strong>: ${a(g.queryId)}</li>`).join("")}</ul>` : ""}</div>` : "";
  return `<div class="ekmSplit"><div class="ekmSplit__column">${b("Captured", E(o.capturedAtUtc))}${b("Capture method", o.captureMethod)}${o.hasCookieSupport !== null && o.hasCookieSupport !== void 0 ? `<p>Cookie support: ${o.hasCookieSupport ? "Yes" : "No"}</p>` : ""}${b("Source", o.source)}${b("Medium", o.medium)}${b("Campaign", o.campaign)}${b("Term", o.term)}${b("Content", o.content)}${b("Click ID", o.clickId)}${b("Click ID Type", o.clickIdType)}${b("Landing URL", o.landingUrl)}${b("Referrer", o.referrer)}</div><div class="ekmSplit__column"><h5>GA4</h5>${b("Client ID", (d = o.ga4) == null ? void 0 : d.clientId)}${b("Session ID", (l = o.ga4) == null ? void 0 : l.sessionId)}${X(h)}<h5>Meta</h5>${b("FBP", (p = o.meta) == null ? void 0 : p.fbp)}${b("FBC", (f = o.meta) == null ? void 0 : f.fbc)}${X(e)}${i}</div></div>`;
}
function se(o) {
  return !!(o && (o.userToken || Array.isArray(o.lines) && o.lines.length));
}
function b(o, h) {
  return h ? `<p class="ekmOrderTracking__wrap ekom-tracking-field"><span>${a(o)}</span><strong>${a(h)}</strong></p>` : "";
}
function X(o) {
  return o.length ? `<ul>${o.map(([h, e]) => `<li><strong>${a(h)}</strong>: ${a(e)}</li>`).join("")}</ul>` : "";
}
function Re(o) {
  return !!(o && (o.resolvedAtUtc || o.source || o.analytics !== null && o.analytics !== void 0 || o.marketing !== null && o.marketing !== void 0));
}
function ee(o) {
  return o === !0 ? "Yes" : o === !1 ? "No" : "Unknown";
}
function te(o, h) {
  return h.map((e) => {
    var t;
    return {
      key: e.key,
      label: e.label,
      value: q(o[e.property] || ((t = o.properties) == null ? void 0 : t[e.key]) || ""),
      isExtra: !1
    };
  });
}
function re(o, h) {
  return _(o, h).map(([e, t]) => ({
    key: e,
    label: R(e).replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim() || e,
    value: t,
    isExtra: !0
  }));
}
function ie(o) {
  return Object.fromEntries(o.map((h) => [h.key, h.value || ""]));
}
function qe(o) {
  try {
    return JSON.parse(o).message || o;
  } catch {
    return o;
  }
}
customElements.define("ekom-orders-section-view", Le);
export {
  Le as EkomOrdersSectionViewElement,
  Le as default
};
