var Y = Object.defineProperty;
var J = (i, d, e) => d in i ? Y(i, d, { enumerable: !0, configurable: !0, writable: !0, value: e }) : i[d] = e;
var l = (i, d, e) => J(i, typeof d != "symbol" ? d + "" : d, e);
import { UmbElementMixin as X } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as ee } from "@umbraco-cms/backoffice/notification";
import { E as te, m as u, a as re, e as s, g as L, b as $, p as ie, d as S, c as se, h as ae } from "./manager-shared.js";
const oe = [
  { key: "customerName", label: "Name", property: "name" },
  { key: "customerEmail", label: "Email", property: "email" },
  { key: "customerAddress", label: "Address", property: "address" },
  { key: "customerApartment", label: "Apartment", property: "apartment" },
  { key: "customerCity", label: "City", property: "city" },
  { key: "customerCountry", label: "Country", property: "country" },
  { key: "customerZipCode", label: "Zipcode", property: "zipCode" },
  { key: "customerPhone", label: "Phone", property: "phone" }
], ne = [
  { key: "shippingName", label: "Name", property: "name" },
  { key: "shippingEmail", label: "Email", property: "email" },
  { key: "shippingAddress", label: "Address", property: "address" },
  { key: "shippingApartment", label: "Apartment", property: "apartment" },
  { key: "shippingCity", label: "City", property: "city" },
  { key: "shippingCountry", label: "Country", property: "country" },
  { key: "shippingZipCode", label: "Zipcode", property: "zipCode" },
  { key: "shippingPhone", label: "Phone", property: "phone" }
], de = 180, N = 1, j = 2;
class le extends X(HTMLElement) {
  constructor() {
    super(...arguments);
    l(this, "api", new te());
    l(this, "notificationContext");
    l(this, "result", { orders: [], count: 0, totalPages: 0 });
    l(this, "page", 1);
    l(this, "loading", !0);
    l(this, "error", "");
    l(this, "config");
    l(this, "configError", "");
    l(this, "notifyOrderStatusOverride");
    l(this, "searchTimer", 0);
    l(this, "overlay", "");
    l(this, "selectedOrder");
    l(this, "orderLogs", []);
    l(this, "orderLogsLoading", !1);
    l(this, "orderLogsError", !1);
    l(this, "activityLogExpandedIndexes", /* @__PURE__ */ new Set());
    l(this, "orderActions", []);
    l(this, "orderActionsLoading", !1);
    l(this, "orderActionsError", !1);
    l(this, "executingActionKey", "");
    l(this, "orderRequestId", 0);
    l(this, "lockRequestId", 0);
    l(this, "orderLockStatus");
    l(this, "releasingOrderLock", !1);
    l(this, "orderLockWarning", "");
    l(this, "orderLockReleaseError", "");
    l(this, "orderLockLoading", !1);
    l(this, "orderLockError", !1);
    l(this, "recoveryAttempts");
    l(this, "recoveryLoading", !1);
    l(this, "recoveryError", !1);
    l(this, "recoveryRequestId", 0);
    l(this, "recoveryOperation", "");
    l(this, "recoveryMessage", "");
    l(this, "recoveryActionError", "");
    l(this, "recoveryRefreshError", !1);
    l(this, "recoveryScrollTop");
    l(this, "trackingExpanded", !1);
    l(this, "consentExpanded", !1);
    l(this, "customerEditorOpen", !1);
    l(this, "customerSaving", !1);
    l(this, "customerEditModel");
    l(this, "orderLineEditorOpen", !1);
    l(this, "orderLineSaving", !1);
    l(this, "orderLineEditModel");
    l(this, "shippingProviderEditorOpen", !1);
    l(this, "shippingProviderSaving", !1);
    l(this, "shippingProviders", []);
    l(this, "filterProvidersRequestId", 0);
    l(this, "filterProvidersLoading", !1);
    l(this, "shippingProviderId", "");
    l(this, "shippingCustomFields", []);
    l(this, "removingOrderLineId", "");
    l(this, "exportIncludeOrderLines", !1);
    l(this, "exporting", !1);
    l(this, "handleKeyDown", (e) => {
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
    });
  }
  connectedCallback() {
    super.connectedCallback(), this.consumeContext(ee, (e) => {
      this.notificationContext = e;
    }), document.addEventListener("keydown", this.handleKeyDown), this.render(), this.initialize();
  }
  disconnectedCallback() {
    super.disconnectedCallback(), document.removeEventListener("keydown", this.handleKeyDown), window.clearTimeout(this.searchTimer);
  }
  async initialize() {
    try {
      const [e, t] = await Promise.all([
        this.api.statusList(),
        this.api.stores(),
        this.loadConfig()
      ]);
      u.statusList = e || [], u.stores = t || [], !u.filters.store && u.stores.length && (u.filters.store = u.stores[0].alias), await this.loadFilterProviders(), await this.loadOrders();
    } catch (e) {
      this.error = b(e, "Error loading Ekom Manager."), this.loading = !1, this.render();
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
    const t = ++this.filterProvidersRequestId, r = u.filters.store;
    if ((e || !r) && (u.filters.paymentProvider = "", u.filters.shippingProvider = ""), u.paymentProviders = [], u.shippingFilterProviders = [], !r) {
      this.filterProvidersLoading = !1;
      return;
    }
    this.filterProvidersLoading = !0, this.render();
    const [a, o] = await Promise.allSettled([
      this.api.paymentProviders(r),
      this.api.shippingProviders(r)
    ]);
    if (t !== this.filterProvidersRequestId || r !== u.filters.store)
      return;
    const n = a.status === "fulfilled" ? a.value : [], c = o.status === "fulfilled" ? o.value : [];
    u.paymentProviders = n, u.shippingFilterProviders = c, this.filterProvidersLoading = !1, (a.status === "rejected" || o.status === "rejected") && this.showNotification(
      "warning",
      "Filter options unavailable",
      "Some provider options could not be loaded. Orders can still be searched; reload to try again."
    ), u.filters.paymentProvider && (u.paymentProviders.some((p) => p.key === u.filters.paymentProvider) || (u.filters.paymentProvider = "")), u.filters.shippingProvider && !c.some((h) => h.key === u.filters.shippingProvider) && (u.filters.shippingProvider = ""), this.render();
  }
  async loadOrders() {
    if (!u.filters.store) {
      this.loading = !1, this.result = { orders: [], count: 0, totalPages: 0 }, this.render();
      return;
    }
    this.loading = !0, this.error = "", this.render();
    try {
      const e = await this.api.searchOrders(u.filters, this.page);
      this.result = {
        ...e,
        orders: e.orders || []
      };
    } catch (e) {
      this.error = b(e, "Error searching orders.");
    } finally {
      this.loading = !1, this.render();
    }
  }
  render() {
    if (this.innerHTML = `
      <style>${re}</style>
      <section class="ekmManager">
        <div class="ekmManager__body">
          ${this.configError ? `<p class="status status--error" role="alert">${s(this.configError)}</p>` : ""}
          ${this.renderOrders()}
        </div>
      </section>
      ${this.renderOverlay()}
    `, this.bindEvents(), this.recoveryScrollTop !== void 0) {
      const e = this.querySelector(".ekmOverlay");
      e && (e.scrollTop = this.recoveryScrollTop);
    }
  }
  renderOrders() {
    return `
      <div class="cards">
        <div class="card ekmSummaryCard">
          <span class="ekmSummaryCard__label">Orders</span>
          <strong class="ekmSummaryCard__value">${s(this.result.count || 0)}</strong>
        </div>
        <div class="card ekmSummaryCard">
          <span class="ekmSummaryCard__label">Payments total</span>
          <strong class="ekmSummaryCard__value">${s(this.result.grandTotal || 0)}</strong>
        </div>
        <div class="card ekmSummaryCard">
          <span class="ekmSummaryCard__label">Average order amount</span>
          <strong class="ekmSummaryCard__value">${s(this.result.averageAmount || 0)}</strong>
        </div>
      </div>
      ${this.renderToolbar()}
      ${this.error ? `<p class="status status--error">${s(this.error)}</p>` : ""}
      ${this.loading ? "<p>Hang tight! Fetching your order details... This might take a moment.</p>" : this.renderOrderTable()}
      ${!this.loading && this.result.totalPages > 1 ? this.renderPagination() : ""}
    `;
  }
  renderToolbar() {
    const e = u.filters;
    return `
      <div class="umb-sub-header">
        <div class="ekmManager__filters">
          <label class="ekmManager__filter">Order Status:
            <select data-field="orderStatus">
              <option value="CompletedOrders" ${e.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option>
              <option value="AllOrders" ${e.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>
              ${u.statusList.map((t) => {
      const r = L(t);
      return `<option value="${s(r)}" ${e.orderStatus === r ? "selected" : ""}>${s(t.label)}</option>`;
    }).join("")}
            </select>
          </label>
          <label class="ekmManager__filter">Date From:
            <input type="date" data-field="dateFrom" value="${s(e.dateFrom)}">
          </label>
          <label class="ekmManager__filter">Date To:
            <input type="date" data-field="dateTo" value="${s(e.dateTo)}">
          </label>
          <label class="ekmManager__filter">Store:
            <select data-field="store">
              ${u.stores.map((t) => `<option value="${s(t.alias)}" ${e.store === t.alias ? "selected" : ""}>${s(t.title)}</option>`).join("")}
            </select>
          </label>
          <div class="ekmManager__search">
            <button type="button" class="btn-outline" data-action="open-export">Export</button>
            <button type="button" class="btn-primary" data-action="open-filter">Filter</button>
            <div class="form-search"><input type="text" data-field="query" value="${s(e.query)}" placeholder="Type to search..."></div>
          </div>
        </div>
      </div>
    `;
  }
  renderOrderTable() {
    return this.result.orders.length ? `
      <div class="umb-table">
        <div class="umb-table-head">
          <div class="umb-table-row">
            <div class="umb-table-cell not-fixed"></div>
            <div class="umb-table-cell">Order Number</div>
            <div class="umb-table-cell">Status</div>
            <div class="umb-table-cell">Name</div>
            <div class="umb-table-cell">Created</div>
            <div class="umb-table-cell">Payment</div>
          </div>
        </div>
        <div class="umb-table-body">
          ${this.result.orders.map((e) => this.renderOrderRow(e)).join("")}
        </div>
      </div>
    ` : '<div class="umb-table"><div class="umb-table-row"><div class="umb-table-cell">No orders found</div></div></div>';
  }
  renderOrderRow(e) {
    return `
      <div class="umb-table-row">
        <div class="umb-table-cell not-fixed" data-label="Action"><button class="btn-success" type="button" data-action="view-order" data-order-id="${s(e.uniqueId)}">View</button></div>
        <div class="umb-table-cell" data-label="Order Number" title="${s(e.uniqueId)}">${s(e.referenceId)}</div>
        <div class="umb-table-cell" data-label="Status">
          <select data-action="change-row-status" data-order-id="${s(e.uniqueId)}" ${this.config ? "" : "disabled"}>
            ${u.statusList.map((t) => {
      const r = L(t);
      return `<option value="${s(r)}" ${e.orderStatusCol === r ? "selected" : ""}>${s(t.label)}</option>`;
    }).join("")}
          </select>
        </div>
        <div class="umb-table-cell" data-label="Name">${s(e.customerName)}</div>
        <div class="umb-table-cell" data-label="Created">${s($(e.createDate))}</div>
        <div class="umb-table-cell" data-label="Payment">${s(e.formattedTotal)}</div>
      </div>
    `;
  }
  renderPagination() {
    return `
      <div class="pagination">
        <ul>
          ${ie(this.page, this.result.totalPages).map((e) => {
      const t = Number(String(e).replace("...", ""));
      return `<li class="${t === this.page ? "active" : ""}"><button type="button" data-action="set-page" data-page="${t}" ${t === this.page ? "disabled" : ""}>${s(e)}</button></li>`;
    }).join("")}
        </ul>
      </div>
    `;
  }
  renderOverlay() {
    return this.overlay === "filter" ? this.renderFilterOverlay() : this.overlay === "export" ? this.renderExportOverlay() : this.overlay === "order" && this.selectedOrder ? this.renderOrderOverlay(this.selectedOrder) : "";
  }
  renderFilterOverlay() {
    const e = u.filters;
    return `
      <div class="ekmOverlay">
        <div class="ekmOverlay__panel ekmOverlay__panel--small">
          <div class="ekmOverlay__header"><h2>Filter</h2><button class="btn-reset" type="button" data-action="close-overlay">&times;</button></div>
          <div class="ekmOverlay__content">
            <label class="control-group">Payment provider:
              <select data-filter-field="paymentProvider" ${this.filterProvidersLoading ? "disabled" : ""}>
                <option value="">Select payment provider</option>
                ${u.paymentProviders.map((t) => `<option value="${s(t.key)}" ${e.paymentProvider === t.key ? "selected" : ""}>${s(t.title)}</option>`).join("")}
              </select>
            </label>
            ${this.renderFilterInput("productSku", "Product SKU:", "Exact SKU")}
            <label class="control-group">Shipping provider:
              <select data-filter-field="shippingProvider" ${this.filterProvidersLoading ? "disabled" : ""}>
                <option value="">All shipping providers</option>
                ${u.shippingFilterProviders.map((t) => `<option value="${s(t.key)}" ${e.shippingProvider === t.key ? "selected" : ""}>${s(t.title)}</option>`).join("")}
              </select>
            </label>
            ${this.renderFilterInput("couponCode", "Coupon code:", "Exact order-level coupon code")}
            ${this.renderFilterInput("giftcardCode", "Gift card number:", "Full gift card number")}
            ${this.renderFilterInput("trackingSource", "Tracking source:", "facebook")}
            ${this.renderFilterInput("trackingMedium", "Tracking medium:", "paid-social")}
            ${this.renderFilterInput("trackingCampaign", "Tracking campaign:", "summer_2026")}
            ${this.renderFilterInput("trackingTerm", "Tracking term:", "running shoes")}
            ${this.renderFilterInput("trackingContent", "Tracking content:", "hero_banner")}
            ${this.renderFilterInput("trackingClickId", "Tracking click id:", "gclid or fbclid")}
            <div style="margin-top:25px; display:flex; gap:10px;"><button type="button" class="btn-success" data-action="apply-filter" ${this.filterProvidersLoading ? "disabled" : ""}>Apply</button><button type="button" class="btn-outline" data-action="close-overlay">Cancel</button></div>
          </div>
        </div>
      </div>
    `;
  }
  renderFilterInput(e, t, r) {
    return `
      <label class="control-group">${s(t)}
        <input type="text" data-filter-field="${s(e)}" value="${s(u.filters[e])}" placeholder="${s(r)}">
      </label>
    `;
  }
  renderExportOverlay() {
    return `
      <div class="ekmOverlay">
        <div class="ekmOverlay__panel ekmOverlay__panel--small">
          <div class="ekmOverlay__header"><h2>Export orders</h2><button class="btn-reset" type="button" data-action="close-overlay" ${this.exporting ? "disabled" : ""}>&times;</button></div>
          <div class="ekmOverlay__content">
            <p>Choose how to export the orders matching the current filters.</p>
            <label style="display:block; margin-top:15px;"><input type="checkbox" data-field="includeOrderLines" ${this.exportIncludeOrderLines ? "checked" : ""} ${this.exporting ? "disabled" : ""}> Include order lines</label>
            ${this.exportIncludeOrderLines ? '<p style="margin-top:10px;">Including order lines can take longer because each matching order needs to be loaded before the CSV is created.</p>' : ""}
            ${this.exporting ? '<p style="margin-top:15px;">Exporting orders. This may take a while...</p>' : ""}
            <div style="margin-top:25px; display:flex; gap:10px;"><button type="button" class="btn-success" data-action="export-orders" ${this.exporting ? "disabled" : ""}>Export</button><button type="button" class="btn-outline" data-action="close-overlay" ${this.exporting ? "disabled" : ""}>Cancel</button></div>
          </div>
        </div>
      </div>
    `;
  }
  renderOrderOverlay(e) {
    return `
      <div class="ekmOverlay">
        <div class="ekmOverlay__panel ekmOrderOverlay__panel">
          <div class="ekmOverlay__header"><h2>View Order</h2><button class="btn-reset" type="button" data-action="close-overlay">&times;</button></div>
          <div class="ekmOverlay__content ekmOrder">
            ${this.renderOrderDetails(e)}
          </div>
        </div>
      </div>
      ${this.customerEditorOpen ? this.renderCustomerEditor() : ""}
      ${this.orderLineEditorOpen ? this.renderOrderLineEditor() : ""}
      ${this.shippingProviderEditorOpen ? this.renderShippingProviderEditor() : ""}
    `;
  }
  renderOrderDetails(e) {
    var o, n, c, h;
    const t = ((o = e.customerInformation) == null ? void 0 : o.customer) || {}, r = ((n = e.customerInformation) == null ? void 0 : n.shipping) || {}, a = this.getOrderStatusValue(e.orderStatus);
    return `
      <div class="ekmOrder__header">
        <h1>Order number: ${s(e.referenceId)}</h1>
        <div class="ekmOrderStatusBar">
          <label class="ekmOrderStatusBar__status">Order Status:
            <select data-field="orderStatusOverlay" ${this.config ? "" : "disabled"}>
              ${u.statusList.map((p) => {
      const v = L(p);
      return `<option value="${s(v)}" ${a === v ? "selected" : ""}>${s(p.label)}</option>`;
    }).join("")}
            </select>
          </label>
          <label class="ekmCheckboxLabel"><input type="checkbox" data-field="notifyOrderStatus" ${this.notifyOrderStatus ? "checked" : ""} ${this.config ? "" : "disabled"}> Fire events?</label>
          <button type="button" class="btn-success" data-action="save-overlay-status" ${this.config ? "" : "disabled"}>Save</button>
          <button type="button" class="btn-outline ekmOrderStatusBar__print" data-action="print-order">Print</button>
        </div>
        ${this.configError ? `<p class="status status--error" role="alert">${s(this.configError)}</p>` : ""}
        <p>UniqueId: ${s(e.uniqueId)}</p>
        <p>Created date: ${s($(e.createDate))}</p>
        <p>Paid date: ${s($(e.paidDate))}</p>
        <p>Store: ${s(((c = e.storeInfo) == null ? void 0 : c.alias) || e.storeAlias)}</p>
        <p>Payment: <strong>${s((h = e.chargedAmount) == null ? void 0 : h.currencyString)}</strong></p>
        ${this.renderOrderLockStatus()}
        ${this.renderOrderActions()}
      </div>
      ${this.renderOrderCustomData(e)}
      <div class="ekmSplit">
        <div class="ekmSplit__column"><h4>Billing</h4><button type="button" class="btn-outline" data-action="open-customer-editor" style="margin-bottom:10px;">Edit customer information</button>${this.renderAddress(t)}${this.renderExtraProperties(t.properties, "customer")}</div>
        <div class="ekmSplit__column"><h4>Shipping</h4>${U(r) ? `${this.renderAddress(r)}${this.renderExtraProperties(r.properties, "shipping")}` : '<p style="font-weight:bold">Same as billing address</p>'}</div>
      </div>
      <div class="ekmSplit">
        <div class="ekmSplit__column">${this.renderProvider("Payment Method", e.paymentProvider, "custompayment")}</div>
        <div class="ekmSplit__column">${e.shippingProvider ? this.renderProvider("Shipping Method", e.shippingProvider, "customshipping") : "<h4>Shipping Method</h4>"}<button type="button" class="btn-outline" data-action="open-shipping-provider-editor">${e.shippingProvider ? "Change shipping provider" : "Add shipping provider"}</button></div>
      </div>
      ${this.renderGiftcards(e)}
      ${this.renderGiftcardRecovery()}
      ${this.renderOrderLines(e)}
      ${this.renderTracking(e)}
      ${this.renderConsent(e)}
      ${this.renderActivityLogs()}
    `;
  }
  renderOrderCustomData(e) {
    const t = Object.entries(e.customData || {}).filter(([r, a]) => r.trim() && typeof a == "string" && a.trim());
    return t.length ? `<dl class="ekmOrderCustomData" aria-label="Order custom data">${t.map(([r, a]) => `
      <div class="ekmOrderCustomData__item">
        <dt class="ekmOrderCustomData__key">${s(ve(r))}</dt>
        <dd class="ekmOrderCustomData__value">${s(a)}</dd>
      </div>`).join("")}</dl>` : "";
  }
  renderPrintableOrder(e) {
    var n, c, h, p, v;
    const t = ((n = e.customerInformation) == null ? void 0 : n.customer) || {}, r = ((c = e.customerInformation) == null ? void 0 : c.shipping) || {}, a = this.getOrderStatusValue(e.orderStatus), o = ((h = u.statusList.find((f) => L(f) === a)) == null ? void 0 : h.label) || a;
    return `
      <main class="ekmOrder">
        <h1>Order number: ${s(e.referenceId)}</h1>
        <p>Order status: ${s(o)}</p>
        <p>UniqueId: ${s(e.uniqueId)}</p>
        <p>Created date: ${s($(e.createDate))}</p>
        <p>Paid date: ${s($(e.paidDate))}</p>
        <p>Store: ${s(((p = e.storeInfo) == null ? void 0 : p.alias) || e.storeAlias)}</p>
        <p>Payment: <strong>${s((v = e.chargedAmount) == null ? void 0 : v.currencyString)}</strong></p>
        ${this.renderOrderCustomData(e)}
        <section class="ekmSplit">
          <div class="ekmSplit__column"><h4>Billing</h4>${this.renderAddress(t)}${this.renderExtraProperties(t.properties, "customer")}</div>
          <div class="ekmSplit__column"><h4>Shipping</h4>${U(r) ? `${this.renderAddress(r)}${this.renderExtraProperties(r.properties, "shipping")}` : "<p>Same as billing address</p>"}</div>
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
    return ["name", "email", "address", "apartment", "city", "country", "zipCode", "phone"].filter((t) => e[t]).map((t) => `<p>${he(t)}: ${s(S(e[t]))}</p>`).join("");
  }
  renderExtraProperties(e, t) {
    const r = H(e, t);
    return r.length ? `<h5 style="margin-top:20px; font-weight:bold;">${t === "customshipping" ? "Shipping Provider Custom" : `Extra ${t === "shipping" ? "Shipping" : "Customer"}`} Data</h5><ul>${r.map(([a, o]) => `<li><strong>${s(Z(a))}</strong>: ${s(o)}</li>`).join("")}</ul>` : "";
  }
  renderProvider(e, t, r) {
    var o, n;
    if (!t)
      return "";
    const a = typeof t.title == "string" && t.title.trim() ? t.title : (o = t.properties) == null ? void 0 : o.nodeName;
    return `<h4>${s(e)}</h4><h4><strong>${s(a)}</strong></h4>${t.price ? `<p>Price: ${s((n = t.price.withVat) == null ? void 0 : n.currencyString)}</p>` : ""}${this.renderExtraProperties(t.customData, r)}`;
  }
  renderGiftcards(e) {
    const t = Array.isArray(e.giftcards) ? e.giftcards : [];
    return t.length ? `<div class="ekmOrderGiftcards"><h4>Gift cards</h4><ul class="ekmOrderGiftcards__list">${t.map((r) => {
      const a = ue(r), o = ce(r);
      return `<li><div class="ekmOrderGiftcards__summary"><strong>${s(r.code)}</strong><strong>${s(this.formatGiftcardAmount(r.amount, e))}</strong><span>${s(a.status)}</span></div>
        <p>${s(a.expiryLabel)}: ${s(a.expiry)}</p>
        ${a.warning ? `<p role="alert">${s(a.warning)}</p>` : ""}
        <div class="ekmOrderGiftcards__actions">${this.orderActions.filter((n) => n.giftcardCode != null && n.giftcardCode === r.code).map((n) => this.renderAction(n)).join("")}</div>
        <details><summary>More details</summary><dl>${o.map((n) => `<dt>${s(n.label)}</dt><dd>${s(n.value)}</dd>`).join("")}</dl></details></li>`;
    }).join("")}</ul></div>` : "";
  }
  formatGiftcardAmount(e, t) {
    var o, n, c, h;
    const r = ((n = (o = t.storeInfo) == null ? void 0 : o.currency) == null ? void 0 : n.currencyValue) || "en-US", a = ((h = (c = t.storeInfo) == null ? void 0 : c.currency) == null ? void 0 : h.isoCurrencySymbol) || t.currency;
    try {
      return new Intl.NumberFormat(r, { style: "currency", currency: a }).format(e);
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
    const e = (r, a) => `<dt>${s(r)}</dt><dd>${s(a ?? "Not recorded")}</dd>`;
    return `<section class="ekmOrderTracking ekmGiftcardRecovery"><h4>Failed gift-card preparation</h4>
      <p>Recovery history for failed checkout preparation, not accepted payment. These actions do not refund a payment or retry checkout.</p>
      ${this.recoveryMessage ? `<p role="status">${s(this.recoveryMessage)}</p>` : ""}
      ${this.recoveryActionError ? `<p role="alert">${s(this.recoveryActionError)}</p>` : ""}
      ${this.recoveryRefreshError ? '<p role="alert">We could not refresh all order details. Reopen the order before taking another recovery action.</p>' : ""}
      ${this.orderActionsError ? '<p role="alert">We could not refresh order actions. Recovery actions are unavailable. Reopen the order to try again.</p>' : ""}
      ${this.recoveryLoading ? "<p>Loading recovery history...</p>" : ""}
      ${this.recoveryError ? '<p role="alert">We could not load gift-card recovery history. Recovery actions are unavailable. Reopen the order to try again.</p>' : ""}
      ${this.recoveryAttempts === void 0 && !this.recoveryLoading && !this.recoveryError ? "<p>Recovery history has not been checked. Recovery actions are unavailable.</p>" : ""}
      ${(this.recoveryAttempts || []).map((r) => {
      const a = r.canReconcile && !!r.reconcileToken;
      return `<article><h5>Preparation attempt: ${s(r.attemptId)}</h5>
          <dl>${e("Created", $(r.createdUtc))}${e("Recovery state", r.state)}${e("Reason", r.reason)}</dl>
          ${r.hasUnknownEffects ? '<p role="alert">Unknown provider effects need reconciliation. A missing claim reference is not evidence that no hold exists. Do not release by card number or amount.</p>' : ""}
          ${r.claims.map((o) => {
        const n = o.canRelease && !!o.releaseToken;
        return `<div class="ekmGiftcardRecovery__claim"><dl>${e("Masked card", o.cardReference)}${e("Claim reference", o.claimReference)}${e("Provider", o.provider)}${e("Account", o.account)}${e("Requested amount", `${o.requestedAmount} ${o.currency}`)}${e("Raw returned amount", o.rawReturnedAmount)}${e("Returned amount type", o.returnedAmountType)}${e("Raw expiry", o.rawExpiry)}${e("Cleanup status", o.cleanupState)}</dl>
              ${o.claimReference ? `<button type="button" class="btn-outline" data-action="release-giftcard-recovery" data-attempt-id="${s(r.attemptId)}" data-claim-id="${s(o.id)}" ${!n || this.recoveryBusy ? "disabled" : ""}>Release failed-preparation hold${this.recoveryOperation === `release:${r.attemptId}:${o.id}` ? " (Working...)" : ""}</button>` : "<p>Missing claim reference: needs reconciliation. No release action is available.</p>"}
              <p>${s(n && o.claimReference ? "Release allowed after confirmation." : o.unavailableReason || "Release unavailable; check reconciliation requirements.")}</p></div>`;
      }).join("")}
          ${!r.claims.length && r.hasUnknownEffects ? "<p>No known claim to release. Reconcile unknown provider effects.</p>" : ""}
          <button type="button" class="btn-outline" data-action="reconcile-giftcard-recovery" data-attempt-id="${s(r.attemptId)}" ${!a || this.recoveryBusy ? "disabled" : ""}>Reconcile failed preparation${this.recoveryOperation === `reconcile:${r.attemptId}` ? " (Working...)" : ""}</button>
          <p>${s(a ? "Reconciliation allowed after confirmation." : r.unavailableReason || "Reconciliation unavailable.")}</p>
        </article>`;
    }).join("")}</section>`;
  }
  renderOrderLines(e) {
    var o, n, c, h, p, v, f;
    const t = Array.isArray(e.orderLines) ? e.orderLines : [], r = typeof e.coupon == "string" ? e.coupon.trim() : "", a = r ? `Discount (${r})` : "Discount";
    return `
      <div style="align-items:center; display:flex; gap:10px; justify-content:space-between;"><h4>Order Details</h4><button type="button" class="btn-outline" data-action="open-order-line-editor">Add order line</button></div>
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed">Product</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Unit Price (inc VAT)</div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">Total (inc VAT)</div></div></div>
        <div class="umb-table-body">
          ${t.map((m) => {
      var O, g, I, A, x, P, C, _, R, T, q, M, F, D;
      return `<div class="umb-table-row"><div class="umb-table-cell"><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${s(m.key)}" data-product-title="${s((O = m.product) == null ? void 0 : O.title)}" ${this.removingOrderLineId === m.key ? "disabled" : ""} aria-label="Remove ${s((g = m.product) == null ? void 0 : g.title)}" title="Remove order line">&#128465;</button></div><div class="umb-table-cell not-fixed"><div class="ekmOrderLineProduct">${me(m)}<div class="ekmOrderLineProduct__details"><span>${ge(m.product)}${(A = (I = m.product) == null ? void 0 : I.sku) != null && A.trim() ? ` (${s(m.product.sku)})` : ""}</span>${m.variant ? `<small style="display:block; margin-top:3px;">${s(m.variant.title)} ${(x = m.variant.sku) != null && x.trim() ? `(${s(m.variant.sku)})` : ""}</small>` : ""}${ye(m)}</div></div></div><div class="umb-table-cell">${s(m.quantity)}</div><div class="umb-table-cell">${s((_ = (C = (P = m.product) == null ? void 0 : P.price) == null ? void 0 : C.withVat) == null ? void 0 : _.currencyString)}</div><div class="umb-table-cell">${s((T = (R = m.amount) == null ? void 0 : R.vat) == null ? void 0 : T.currencyString)}</div><div class="umb-table-cell">-${s((M = (q = m.amount) == null ? void 0 : q.discountAmount) == null ? void 0 : M.currencyString)}</div><div class="umb-table-cell"><strong>${s((D = (F = m.amount) == null ? void 0 : F.withVat) == null ? void 0 : D.currencyString)}</strong></div></div>`;
    }).join("")}
        </div>
        <div class="umb-table-footer">
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Sub Total (inc VAT)</div><div class="umb-table-cell">${s((n = (o = e.subTotal) == null ? void 0 : o.withVat) == null ? void 0 : n.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">${s(a)}</div><div class="umb-table-cell">-${s((c = e.discountAmount) == null ? void 0 : c.currencyString)}</div></div>
          ${e.shippingProvider ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Shipping Total</div><div class="umb-table-cell">${s((p = (h = e.shippingProvider.price) == null ? void 0 : h.withVat) == null ? void 0 : p.currencyString)}</div></div>` : ""}
          ${this.renderGiftcardTotal(e)}
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">${s((v = e.chargedVat) == null ? void 0 : v.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Total</div><div class="umb-table-cell"><strong>${s((f = e.chargedAmount) == null ? void 0 : f.currencyString)}</strong></div></div>
        </div>
      </div>
    `;
  }
  renderGiftcardTotal(e) {
    const r = (Array.isArray(e.giftcards) ? e.giftcards : []).reduce((a, o) => a + (o.amount > 0 && (o.claimed || !o.validUntil || Date.parse(o.validUntil) > Date.now()) ? o.amount : 0), 0);
    return r ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell" title="Payable total already includes applicable gift cards; eligible value may exceed the order total.">Gift cards (eligible value)</div><div class="umb-table-cell">${s(this.formatGiftcardAmount(r, e))}</div></div>` : "";
  }
  renderTracking(e) {
    const t = e.tracking;
    return be(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Tracking</h4><button class="btn-reset" type="button" data-action="toggle-tracking">${this.trackingExpanded ? "Hide" : "Show"}</button></div>${this.trackingExpanded ? $e(t) : ""}</div>` : '<div class="ekmOrderTracking"><h4>Tracking</h4><p>No tracking data was captured for this order.</p></div>';
  }
  renderConsent(e) {
    const t = e.consent;
    return ke(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Consent</h4><button class="btn-reset" type="button" data-action="toggle-consent">${this.consentExpanded ? "Hide" : "Show"}</button></div>${this.consentExpanded ? `<p>Resolved: ${s($(t.resolvedAtUtc))}</p><p>Source: ${s(t.source)}</p><p>Analytics: ${V(t.analytics)}</p><p>Marketing: ${V(t.marketing)}</p>` : ""}</div>` : '<div class="ekmOrderTracking"><h4>Consent</h4><p>No consent data was captured for this order.</p></div>';
  }
  renderActivityLogs() {
    return this.orderLogsLoading ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Loading activity...</p></div>' : this.orderLogsError ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Unable to load activity log.</p></div>' : this.orderLogs.length ? `<div class="ekmOrderActivityLog"><h4>Activity log</h4><div class="ekmOrderActivityLog__list">${this.orderLogs.map((t, r) => {
      const a = t.message ?? "", o = this.activityLogExpandedIndexes.has(r), n = this.canExpandActivityLog(a) ? `<button type="button" class="btn-reset ekmOrderActivityLog__toggle" data-action="toggle-activity-log" data-log-index="${r}">${o ? "Show less" : "Show more"}</button>` : "";
      return `<div class="ekmOrderActivityLog__item"><div class="ekmOrderActivityLog__content"><span class="ekmOrderActivityLog__icon ${this.getActivityLogTypeClass(t)}">${this.getActivityLogIcon(t)}</span><div class="ekmOrderActivityLog__body"><div class="ekmOrderActivityLog__date">${s($(t.date))}</div><div class="ekmOrderActivityLog__message${o ? " ekmOrderActivityLog__message--expanded" : ""}">${s(a)}</div>${n}</div></div></div>`;
    }).join("")}</div></div>` : '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>No activity yet.</p></div>';
  }
  getActivityLogIcon(e) {
    switch (e.logType) {
      case N:
        return "✓";
      case j:
        return "!";
      default:
        return "i";
    }
  }
  getActivityLogTypeClass(e) {
    switch (e.logType) {
      case N:
        return "ekmOrderActivityLog__icon--success";
      case j:
        return "ekmOrderActivityLog__icon--alert";
      default:
        return "ekmOrderActivityLog__icon--info";
    }
  }
  canExpandActivityLog(e) {
    return e.length > de;
  }
  renderOrderActions() {
    const e = this.orderActions.filter((t) => t.giftcardCode == null);
    return !e.length && !this.orderActionsLoading ? "" : `<div style="margin-top:15px;"><h4>Order Actions</h4><div style="display:flex; flex-wrap:wrap; gap:10px;">${e.map((t) => this.renderAction(t)).join("")}</div>${this.orderActionsLoading ? "<p>Loading actions...</p>" : ""}</div>`;
  }
  renderAction(e) {
    const t = this.executingActionKey === e.key;
    return `<span style="display:inline-block; margin:4px;"><button type="button" class="${e.look === "primary" ? "btn-success" : "btn-outline"}" data-action="execute-order-action" data-action-key="${s(e.key)}" ${e.enabled === !1 || this.executingActionKey || this.releasingOrderLock || this.recoveryOperation ? "disabled" : ""}>${s(e.label)}${t ? " (Working...)" : ""}</button>${e.enabled === !1 && e.disabledReason ? `<small style="display:block;">${s(e.disabledReason)}</small>` : ""}</span>`;
  }
  renderOrderLockStatus() {
    const e = this.orderLockStatus, t = this.orderLockWarning ? `<p role="alert">${s(this.orderLockWarning)}</p>` : "", r = this.orderLockError ? '<p role="alert">We could not check whether checkout is locked. Reopen the order to try again.</p>' : "", a = this.orderLockReleaseError ? `<p role="alert">${s(this.orderLockReleaseError)}</p>` : "";
    if (!(e != null && e.hasLock))
      return `${t}${r}${a}${this.orderLockLoading ? "<p>Checking checkout...</p>" : ""}`;
    const o = !e.canRelease || !e.releaseToken ? e.releaseUnavailableReason || "This checkout lock cannot be released right now." : "";
    return `<section class="ekmOrderTracking"><h4>Checkout is locked</h4>${t}${r}${a}<p>${e.reconciliationRequired && !e.paymentOperationLocked && !e.preparationLocked ? "The previous payment needs checking before the customer can pay or edit their basket." : "A previous checkout did not finish. This prevents the customer from paying or editing their basket."}</p><p>If you have checked that no payment is being processed, Release Lock resets checkout and lets the customer try again.</p><button type="button" class="btn-outline" data-action="release-order-lock" ${o || this.orderLockLoading || this.orderLockError || this.releasingOrderLock || this.executingActionKey || this.recoveryOperation ? "disabled" : ""}>Release Lock</button>${o ? `<p>${s(o)}</p>` : ""}<details><summary>Technical details</summary><ul>${e.paymentOperationLocked ? "<li>Payment operation ownership retained.</li>" : ""}${e.preparationLocked ? "<li>Checkout preparation ownership retained.</li>" : ""}${e.reconciliationRequired ? "<li>Reconciliation required.</li>" : ""}</ul><p>Attempt ID: ${s(e.attemptId || "Not available")}</p><p>Attempt state: ${s(e.attemptState || "Not available")}</p></details></section>`;
  }
  renderCustomerEditor() {
    const e = this.customerEditModel;
    return e ? `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel"><div class="ekmOverlay__header"><h3>Edit customer information</h3><button class="btn-reset" type="button" data-action="close-customer-editor" ${this.customerSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><div class="ekmSplit"><div class="ekmSplit__column"><h4>Billing</h4>${this.renderCustomerFields(e.customer, "customer")}</div><div class="ekmSplit__column"><h4>Shipping</h4>${this.renderCustomerFields(e.shipping, "shipping")}</div></div><div style="display:flex; justify-content:flex-end; gap:10px; padding-top:20px; border-top:1px solid #d8d7d9;"><button class="btn-outline" type="button" data-action="close-customer-editor" ${this.customerSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-customer-information" ${this.customerSaving ? "disabled" : ""}>${this.customerSaving ? "Saving..." : "Save customer information"}</button></div></div></div></div>` : "";
  }
  renderCustomerFields(e, t) {
    return e.map((r) => `<label class="control-group">${s(r.label)} ${r.isExtra ? `<small>(${s(r.key)})</small>` : ""}<input type="text" data-customer-group="${t}" data-customer-key="${s(r.key)}" value="${s(r.value)}" ${this.customerSaving ? "disabled" : ""}></label>`).join("");
  }
  renderOrderLineEditor() {
    const e = this.orderLineEditModel;
    return e ? `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel"><div class="ekmOverlay__header"><h3>Add order line</h3><button class="btn-reset" type="button" data-action="close-order-line-editor" ${this.orderLineSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><label class="control-group">Product ID<input type="text" data-order-line-field="productId" value="${s(e.productId)}" required ${this.orderLineSaving ? "disabled" : ""}></label><label class="control-group">Variant ID<input type="text" data-order-line-field="variantId" value="${s(e.variantId)}" ${this.orderLineSaving ? "disabled" : ""}></label><label class="control-group" style="padding-bottom:20px;">Quantity<input type="number" min="0.000001" step="any" data-order-line-field="quantity" value="${s(e.quantity)}" required ${this.orderLineSaving ? "disabled" : ""}></label><div style="display:flex; justify-content:flex-end; gap:10px; padding-top:20px; border-top:1px solid #d8d7d9;"><button class="btn-outline" type="button" data-action="close-order-line-editor" ${this.orderLineSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-order-line" ${this.orderLineSaving ? "disabled" : ""}>${this.orderLineSaving ? "Adding..." : "Add order line"}</button></div></div></div></div>` : "";
  }
  renderShippingProviderEditor() {
    var e;
    return `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel"><div class="ekmOverlay__header"><h3>${(e = this.selectedOrder) != null && e.shippingProvider ? "Change shipping provider" : "Add shipping provider"}</h3><button class="btn-reset" type="button" data-action="close-shipping-provider-editor" ${this.shippingProviderSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><label class="control-group">Shipping provider<select data-field="shippingProviderId" ${this.shippingProviderSaving ? "disabled" : ""}><option value="">Select a provider</option>${this.shippingProviders.map((t) => `<option value="${s(t.key)}" ${this.shippingProviderId === t.key ? "selected" : ""}>${s(t.title)}</option>`).join("")}</select></label>${this.shippingProviders.length ? "" : "<p>No shipping providers available for this store.</p>"}<h4>Shipping provider custom data</h4><p>New keys are prefixed with customshipping.</p>${this.shippingCustomFields.map((t, r) => `<div class="control-group"><label>Key<input type="text" data-shipping-key="${r}" value="${s(t.key)}" ${t.existing ? "readonly" : ""} ${this.shippingProviderSaving ? "disabled" : ""} placeholder="TrackingNumber" maxlength="100"></label><label>Value<input type="text" data-shipping-value="${r}" value="${s(t.value)}" ${this.shippingProviderSaving ? "disabled" : ""} maxlength="4096"></label></div>`).join("")}<button class="btn-outline" type="button" data-action="add-shipping-custom-field" ${this.shippingProviderSaving ? "disabled" : ""}>Add custom field</button><div style="display:flex; justify-content:flex-end; gap:10px; padding-top:20px; border-top:1px solid #d8d7d9;"><button class="btn-outline" type="button" data-action="close-shipping-provider-editor" ${this.shippingProviderSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-shipping-provider" ${this.shippingProviderSaving || !this.shippingProviders.length ? "disabled" : ""}>${this.shippingProviderSaving ? "Saving..." : "Save shipping provider"}</button></div></div></div></div>`;
  }
  readShippingCustomFields() {
    this.shippingCustomFields = this.shippingCustomFields.map((e, t) => {
      var r, a;
      return {
        key: e.existing ? e.key : ((r = this.querySelector(`[data-shipping-key="${t}"]`)) == null ? void 0 : r.value) || "",
        value: ((a = this.querySelector(`[data-shipping-value="${t}"]`)) == null ? void 0 : a.value) || "",
        existing: e.existing
      };
    });
  }
  bindEvents() {
    var e;
    this.querySelectorAll(".ekmOrderLineThumbnail img").forEach((t) => {
      const r = () => {
        t.hidden = !0;
      };
      t.addEventListener("error", r, { once: !0 }), t.complete && t.naturalWidth === 0 && r();
    }), this.overlay === "order" && ((e = this.querySelector(".ekmOverlay")) == null || e.addEventListener("click", (t) => {
      t.target === t.currentTarget && !this.customerEditorOpen && !this.orderLineEditorOpen && !this.shippingProviderEditorOpen && this.closeOverlay();
    })), this.querySelectorAll("[data-action]").forEach((t) => {
      t instanceof HTMLSelectElement ? t.addEventListener("change", (r) => void this.handleAction(r)) : t.addEventListener("click", (r) => void this.handleAction(r));
    }), this.querySelectorAll("[data-field]").forEach((t) => {
      t.addEventListener("change", (r) => void this.handleFieldChange(r)), t.dataset.field === "query" && t.addEventListener("input", (r) => this.handleSearchInput(r));
    });
  }
  async handleAction(e) {
    var a;
    const t = e.currentTarget, r = t.dataset.action;
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
          await se(this.renderPrintableOrder(this.selectedOrder), `Order ${this.selectedOrder.referenceId}`);
        } catch {
          this.showError("Unable to prepare the order for printing. Please try again.");
        }
      return;
    }
    if (r === "toggle-tracking") {
      this.trackingExpanded = !this.trackingExpanded, this.renderPreservingOverlayScroll();
      return;
    }
    if (r === "toggle-consent") {
      this.consentExpanded = !this.consentExpanded, this.renderPreservingOverlayScroll();
      return;
    }
    if (r === "toggle-activity-log") {
      const o = Number(t.dataset.logIndex);
      this.activityLogExpandedIndexes.has(o) ? this.activityLogExpandedIndexes.delete(o) : this.activityLogExpandedIndexes.add(o), this.renderPreservingOverlayScroll();
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
      this.readShippingCustomFields(), this.shippingProviderId = ((a = this.querySelector('[data-field="shippingProviderId"]')) == null ? void 0 : a.value) || "", this.shippingCustomFields.push({ key: "", value: "", existing: !1 }), this.render();
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
    r === "orderStatusOverlay" || r === "query" || !r || !(r in u.filters) || (u.filters[r] = t.value, this.page = 1, r === "store" && await this.loadFilterProviders(!0), await this.loadOrders());
  }
  handleSearchInput(e) {
    const t = e.currentTarget;
    u.filters.query = t.value, this.page = 1, window.clearTimeout(this.searchTimer), this.searchTimer = window.setTimeout(() => void this.loadOrders(), 700);
  }
  applyFilterOverlay() {
    this.querySelectorAll("[data-filter-field]").forEach((e) => {
      const t = e.dataset.filterField;
      t && t in u.filters && (u.filters[t] = t === "giftcardCode" ? e.value.trim() : e.value);
    });
  }
  async exportOrders() {
    if (this.result.count) {
      this.exporting = !0, this.render();
      try {
        const e = await this.api.exportOrders(u.filters, this.result.count, this.exportIncludeOrderLines);
        ae(e, this.exportIncludeOrderLines ? "orders-with-orderlines.csv" : "orders.csv"), this.overlay = "";
      } catch (e) {
        this.showError(b(e, "Error exporting orders."));
      } finally {
        this.exporting = !1, this.render();
      }
    }
  }
  async openOrder(e) {
    if (!e)
      return;
    const t = ++this.orderRequestId;
    ++this.lockRequestId, ++this.recoveryRequestId, this.recoveryAttempts = void 0, this.recoveryLoading = !1, this.recoveryError = !1, this.recoveryOperation = "", this.recoveryMessage = "", this.recoveryActionError = "", this.recoveryRefreshError = !1, this.recoveryScrollTop = void 0, this.notifyOrderStatusOverride = void 0, this.selectedOrder = void 0, this.orderActions = [], this.orderActionsError = !1, this.executingActionKey = "", this.orderLockStatus = void 0, this.releasingOrderLock = !1, this.orderLockWarning = "", this.orderLockReleaseError = "", this.orderLockError = !1, this.orderLockLoading = !1, this.render();
    try {
      const r = await this.api.orderInfo(e);
      if (t !== this.orderRequestId) return;
      this.selectedOrder = r, this.overlay = "order", this.trackingExpanded = !1, this.consentExpanded = !1, this.orderLogs = [], this.orderLogsError = !1, this.activityLogExpandedIndexes.clear(), this.render(), await Promise.all([this.loadOrderLogs(e), this.loadOrderActions(e), this.loadOrderLockStatus(e), this.loadGiftcardRecovery(e)]);
    } catch (r) {
      if (t !== this.orderRequestId) return;
      this.showError(b(r, "Error on getting orderInfo."));
    }
  }
  async loadOrderLogs(e) {
    const t = this.orderRequestId;
    this.orderLogsLoading = !0, this.orderLogsError = !1, this.render();
    try {
      const r = await this.api.orderLogs(e);
      if (t !== this.orderRequestId) return;
      this.orderLogs = r;
    } catch {
      if (t !== this.orderRequestId) return;
      this.orderLogs = [], this.orderLogsError = !0;
    } finally {
      if (t !== this.orderRequestId) return;
      this.orderLogsLoading = !1, this.render();
    }
  }
  async loadOrderActions(e) {
    const t = this.orderRequestId;
    this.orderActionsLoading = !0, this.orderActionsError = !1, this.render();
    try {
      const r = await this.api.orderActions(e);
      if (t !== this.orderRequestId) return;
      this.orderActions = r;
    } catch {
      if (t !== this.orderRequestId) return;
      this.orderActions = [], this.orderActionsError = !0;
    } finally {
      if (t !== this.orderRequestId) return;
      this.orderActionsLoading = !1, this.render();
    }
  }
  async loadOrderLockStatus(e) {
    const t = ++this.lockRequestId, r = this.orderRequestId, a = () => {
      var o;
      return t === this.lockRequestId && r === this.orderRequestId && e === ((o = this.selectedOrder) == null ? void 0 : o.uniqueId);
    };
    this.orderLockLoading = !0, this.orderLockError = !1, this.render();
    try {
      const o = await this.api.orderLockStatus(e);
      a() && (this.orderLockStatus = o);
    } catch {
      a() && (this.orderLockError = !0);
    } finally {
      a() && (this.orderLockLoading = !1, this.render());
    }
  }
  async loadGiftcardRecovery(e) {
    const t = ++this.recoveryRequestId, r = this.orderRequestId, a = () => {
      var o;
      return t === this.recoveryRequestId && r === this.orderRequestId && e === ((o = this.selectedOrder) == null ? void 0 : o.uniqueId);
    };
    this.recoveryLoading = !0, this.recoveryError = !1, this.render();
    try {
      const o = await this.api.giftcardRecovery(e);
      if (!Array.isArray(o) || o.some((n) => !Array.isArray(n.claims)))
        throw new Error("Invalid recovery history response.");
      a() && (this.recoveryAttempts = o);
    } catch {
      a() && (this.recoveryError = !0);
    } finally {
      a() && (this.recoveryLoading = !1, this.render());
    }
  }
  async executeGiftcardRecovery(e, t) {
    var f, m, O;
    const r = (f = this.selectedOrder) == null ? void 0 : f.uniqueId, a = (m = this.recoveryAttempts) == null ? void 0 : m.find((g) => g.attemptId === e);
    if (!r || !a || this.recoveryBusy) return;
    const o = t !== void 0, n = o ? a.claims.find((g) => g.id === t) : void 0, c = o ? n == null ? void 0 : n.releaseToken : a.reconcileToken;
    if (!c || (o ? !(n != null && n.canRelease) || !n.claimReference : !a.canReconcile)) return;
    const h = o ? `Release the failed-preparation hold for masked card ${n.cardReference}, claim ${n.claimReference}? This calls the provider through audited recovery. It does not refund an accepted payment or retry payment or checkout. Unknown effects may still need reconciliation.` : "Reconcile this failed gift-card preparation? The audited backend will check known claims and unknown provider effects. This does not refund an accepted payment or retry payment or checkout. Unresolved effects may remain blocked.";
    if (!window.confirm(h)) return;
    const p = this.orderRequestId, v = () => {
      var g;
      return p === this.orderRequestId && r === ((g = this.selectedOrder) == null ? void 0 : g.uniqueId);
    };
    this.recoveryOperation = o ? `release:${e}:${t}` : `reconcile:${e}`, this.recoveryScrollTop = (O = this.querySelector(".ekmOverlay")) == null ? void 0 : O.scrollTop, this.recoveryMessage = "", this.recoveryActionError = "", this.render();
    try {
      const g = o ? await this.api.releaseGiftcardRecovery(r, e, n.id, c) : await this.api.reconcileGiftcardRecovery(r, e, c);
      if (!v()) return;
      this.recoveryMessage = `${g.message} ${g.resolved ? "Failed preparation resolved." : "Recovery remains unresolved; check the updated history."}`;
    } catch (g) {
      if (!v()) return;
      this.recoveryActionError = b(g, "Recovery could not be confirmed. Check the updated history before trying again.");
    } finally {
      v() && (await Promise.all([
        this.loadGiftcardRecovery(r),
        this.loadOrderLockStatus(r),
        this.loadOrderActions(r),
        this.loadOrderLogs(r),
        this.api.orderInfo(r).then((g) => {
          v() && (this.selectedOrder = g);
        }).catch(() => {
          v() && (this.recoveryRefreshError = !0);
        })
      ]), v() && (this.recoveryOperation = "", this.render(), this.recoveryScrollTop = void 0));
    }
  }
  async releaseOrderLock() {
    var n;
    const e = (n = this.selectedOrder) == null ? void 0 : n.uniqueId, t = this.orderLockStatus;
    if (!e || !(t != null && t.hasLock) || !t.canRelease || !t.releaseToken || this.orderLockLoading || this.orderLockError || this.releasingOrderLock || this.executingActionKey || this.recoveryOperation || !window.confirm("Release this checkout lock? Confirm that no payment is currently being processed and the previous checkout has stopped. The customer's basket will be kept, and they can try checkout again. This does not cancel or refund a payment. Any gift-card hold that cannot be released must be checked separately.")) return;
    const r = this.orderRequestId, a = () => {
      var c;
      return r === this.orderRequestId && e === ((c = this.selectedOrder) == null ? void 0 : c.uniqueId);
    };
    this.releasingOrderLock = !0, this.orderLockReleaseError = "";
    let o = !1;
    this.render();
    try {
      const c = await this.api.releaseOrderLock(e, t.releaseToken);
      if (!a()) return;
      o = !0, this.orderLockWarning = c.warning || "", this.showSuccess(c.message);
      const [h] = await Promise.all([this.api.orderInfo(e), this.loadOrderLogs(e), this.loadOrderActions(e), this.loadOrderLockStatus(e), this.loadGiftcardRecovery(e)]);
      a() && (this.selectedOrder = h);
    } catch (c) {
      if (!a()) return;
      this.orderLockReleaseError = o ? "Checkout was unlocked, but we could not refresh all order details. Reopen the order to check them." : b(c, "We could not confirm the checkout reset. Check the updated order before trying again."), await Promise.all([
        this.loadOrderLogs(e),
        this.loadOrderActions(e),
        this.loadOrderLockStatus(e),
        this.loadGiftcardRecovery(e),
        this.api.orderInfo(e).then((h) => {
          a() && (this.selectedOrder = h);
        }).catch(() => {
        })
      ]);
    } finally {
      a() && (this.releasingOrderLock = !1, this.render());
    }
  }
  async saveOverlayStatus() {
    var a, o;
    if (!this.config || !((a = this.selectedOrder) != null && a.uniqueId))
      return;
    const e = this.querySelector('[data-field="orderStatusOverlay"]'), t = (e == null ? void 0 : e.value) || "", r = this.notifyOrderStatus;
    try {
      await this.api.changeOrderStatus(this.selectedOrder.uniqueId, t, r), this.selectedOrder.orderStatus = t, this.showSuccess("Order status updated."), await this.loadOrders(), await this.loadOrderLogs(this.selectedOrder.uniqueId);
    } catch (n) {
      e && (e.value = this.getOrderStatusValue((o = this.selectedOrder) == null ? void 0 : o.orderStatus)), this.showError(b(n, "Error updating order status."));
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
  }
  getOrderStatusValue(e) {
    const t = String(e ?? ""), r = u.statusList.find((a) => String(a.value ?? "") === t || String(a.enumValue ?? "") === t);
    return r ? L(r) : t;
  }
  async changeRowStatus(e) {
    const t = e.dataset.orderId || "";
    if (!(!this.config || !t))
      try {
        await this.api.changeOrderStatus(t, e.value, this.config.managerOrderStatusFireEventsByDefault ?? !0);
        const r = this.result.orders.find((a) => a.uniqueId === t);
        r && (r.orderStatusCol = e.value), this.showSuccess("Order status updated.");
      } catch (r) {
        this.showError(b(r, "Error updating order status.")), await this.loadOrders();
      }
  }
  async executeOrderAction(e) {
    var o;
    if (!((o = this.selectedOrder) != null && o.uniqueId) || !e || this.executingActionKey || this.releasingOrderLock || this.recoveryOperation)
      return;
    const t = this.orderActions.find((n) => n.key === e);
    if (!t || t.enabled === !1) return;
    const r = this.selectedOrder.uniqueId, a = this.orderRequestId;
    if (!(t != null && t.confirmMessage && !window.confirm(t.confirmMessage))) {
      this.executingActionKey = e, this.render();
      try {
        const n = await this.api.executeOrderAction(r, e), c = await n.blob(), h = n.headers.get("content-disposition") || "", p = n.headers.get("content-type") || "";
        if (h.toLowerCase().includes("filename=") || p.startsWith("application/pdf") || p.startsWith("application/octet-stream") || p.startsWith("image/"))
          window.open(URL.createObjectURL(c), "_blank");
        else {
          const f = await c.text();
          this.showSuccess(Oe(f));
        }
        if (a !== this.orderRequestId) return;
        const [v] = await Promise.all([this.api.orderInfo(r), this.loadOrderLockStatus(r), this.loadGiftcardRecovery(r)]);
        if (a !== this.orderRequestId) return;
        this.selectedOrder = v, await Promise.all([this.loadOrderLogs(r), this.loadOrderActions(r)]);
      } catch (n) {
        if (a !== this.orderRequestId) return;
        this.showError(b(n, "Order action failed."));
      } finally {
        if (a !== this.orderRequestId) return;
        this.executingActionKey = "", this.render();
      }
    }
  }
  openCustomerEditor() {
    var a, o;
    const e = this.selectedOrder;
    if (!e)
      return;
    const t = ((a = e.customerInformation) == null ? void 0 : a.customer) || {}, r = ((o = e.customerInformation) == null ? void 0 : o.shipping) || {};
    this.customerEditModel = {
      customer: B(t, oe).concat(K(t.properties, "customer")),
      shipping: B(r, ne).concat(K(r.properties, "shipping"))
    }, this.customerEditorOpen = !0, this.render();
  }
  async saveCustomerInformation() {
    var e;
    if (!(!((e = this.selectedOrder) != null && e.uniqueId) || !this.customerEditModel || this.customerSaving)) {
      this.querySelectorAll("[data-customer-group]").forEach((t) => {
        var n;
        const r = t.dataset.customerGroup, a = t.dataset.customerKey, o = (n = this.customerEditModel) == null ? void 0 : n[r].find((c) => c.key === a);
        o && (o.value = t.value);
      }), this.customerSaving = !0, this.render();
      try {
        this.selectedOrder = await this.api.updateCustomerInformation(
          this.selectedOrder.uniqueId,
          W(this.customerEditModel.customer),
          W(this.customerEditModel.shipping)
        ), this.customerEditorOpen = !1, this.customerEditModel = void 0, this.showSuccess("Customer information updated."), await this.loadOrders();
      } catch (t) {
        this.showError(b(t, "Error updating customer information."));
      } finally {
        this.customerSaving = !1, this.render();
      }
    }
  }
  async saveOrderLine() {
    var o;
    if (!((o = this.selectedOrder) != null && o.uniqueId) || !this.orderLineEditModel || this.orderLineSaving)
      return;
    this.querySelectorAll("[data-order-line-field]").forEach((n) => {
      const c = n.dataset.orderLineField;
      c && (this.orderLineEditModel[c] = n.value);
    });
    const { productId: e, variantId: t, quantity: r } = this.orderLineEditModel, a = Number(r);
    if (!e.trim() || !Number.isFinite(a) || a <= 0) {
      this.showError("Product ID and a positive quantity are required.");
      return;
    }
    this.orderLineSaving = !0, this.render();
    try {
      this.selectedOrder = await this.api.addOrderLine(this.selectedOrder.uniqueId, e.trim(), t.trim() || void 0, a), this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.showSuccess("Order line added."), await this.refreshOrderAfterLineChange();
    } catch (n) {
      this.showError(b(n, "Error adding order line."));
    } finally {
      this.orderLineSaving = !1, this.render();
    }
  }
  async openShippingProviderEditor() {
    var e, t, r;
    if (this.selectedOrder)
      try {
        const a = ((e = this.selectedOrder.storeInfo) == null ? void 0 : e.alias) || this.selectedOrder.storeAlias;
        this.shippingProviders = await this.api.shippingProviders(a), this.shippingProviderId = ((t = this.selectedOrder.shippingProvider) == null ? void 0 : t.key) || "", this.shippingCustomFields = Object.entries(((r = this.selectedOrder.shippingProvider) == null ? void 0 : r.customData) || {}).filter(([o]) => o.toLowerCase().startsWith("customshipping")).map(([o, n]) => ({ key: o, value: S(n), existing: !0 })), this.shippingProviderEditorOpen = !0, this.render();
      } catch (a) {
        this.showError(b(a, "Error loading shipping providers."));
      }
  }
  async saveShippingProvider() {
    var a, o;
    if (!((a = this.selectedOrder) != null && a.uniqueId) || this.shippingProviderSaving)
      return;
    const e = ((o = this.querySelector('[data-field="shippingProviderId"]')) == null ? void 0 : o.value) || "";
    if (!this.shippingProviders.some((n) => n.key === e)) {
      this.showError("Select a shipping provider.");
      return;
    }
    this.readShippingCustomFields();
    const t = {}, r = /* @__PURE__ */ new Set();
    for (const n of this.shippingCustomFields) {
      const c = n.existing ? n.key : `customshipping${n.key.trim()}`;
      if (!c.slice(14).trim() || c.length > 100 || n.value.length > 4096 || r.has(c.toLowerCase())) {
        this.showError("Enter unique shipping custom data keys and valid values.");
        return;
      }
      r.add(c.toLowerCase()), t[c] = n.value;
    }
    this.shippingProviderSaving = !0, this.render();
    try {
      this.selectedOrder = await this.api.updateShippingProvider(this.selectedOrder.uniqueId, e, t), this.shippingProviderEditorOpen = !1, this.showSuccess("Shipping provider updated."), await this.refreshOrderAfterLineChange();
    } catch (n) {
      this.showError(b(n, "Error updating shipping provider."));
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
      } catch (a) {
        this.showError(b(a, "Error removing order line."));
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
function b(i, d) {
  return i instanceof Error ? i.message : d;
}
function k(i) {
  return i != null && (typeof i != "string" || i.trim() !== "");
}
function z(i) {
  if (i === "code") return "Gift-card number";
  if (i === "claimId") return "Reservation reference";
  if (i === "transactionId") return "Transaction reference";
  if (i === "usedDate") return "Used on";
  if (i === "validUntil") return "Saved expiry";
  if (i === "claimDate") return "Payment claim date";
  const d = i.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ");
  return d.charAt(0).toUpperCase() + d.slice(1).toLowerCase().replace(/\bid\b/g, "ID");
}
function w(i) {
  if (!k(i)) return "Not recorded";
  if (typeof i == "boolean") return i ? "Yes" : "No";
  if (Array.isArray(i)) return i.length ? i.map(w).join("; ") : "Empty list";
  if (typeof i == "object") {
    const d = Object.entries(i);
    return d.length ? d.map(([e, t]) => `${z(e)}: ${w(t)}`).join("; ") : "Empty object";
  }
  return String(i);
}
function ce(i) {
  const d = ["code", "amount", "claimId", "claimDate", "transactionId", "usedDate", "claimed", "validUntil"];
  return [...d, ...Object.keys(i).filter((e) => !d.includes(e))].map((e) => ({ label: z(e), value: w(i[e]) }));
}
function ue(i, d = Date.now()) {
  const e = k(i.claimId), t = k(i.validUntil), r = t ? Date.parse(String(i.validUntil)) : NaN, a = r > d, o = i.claimed === !0 || k(i.transactionId) || k(i.claimDate) || k(i.usedDate), n = o ? "Used" : e && a ? "Reserved" : e && r <= d ? "Reservation expired" : e || t ? "Needs checking" : "Selected";
  let c = "";
  !o && !e && t ? c = "An expiry time is saved, but the reservation reference is missing. Check the gift-card provider if an amount is still held." : !o && e && r <= d ? c = "The saved reservation expiry has passed. This does not confirm that the provider has released the hold." : !o && e && !t && (c = "No expiry time is saved. Check the gift-card provider if an amount is still held."), t && !Number.isFinite(r) && (c += `${c ? " " : ""}The saved expiry time could not be read. Check the gift-card provider.`);
  let h = "Not recorded";
  if (t && (h = Number.isFinite(r) ? new Intl.DateTimeFormat(void 0, { dateStyle: "medium", timeStyle: "long" }).format(new Date(r)) : String(i.validUntil), a)) {
    const p = Math.ceil((r - d) / 6e4), v = p < 60 ? p : p < 1440 ? Math.ceil(p / 60) : Math.ceil(p / 1440), f = p < 60 ? "minute" : p < 1440 ? "hour" : "day";
    h += ` (in about ${v} ${f}${v === 1 ? "" : "s"})`;
  }
  return { status: n, warning: c, expiryLabel: !o && e ? "Reservation ends" : "Saved expiry", expiry: h };
}
function he(i) {
  return i === "zipCode" ? "Zipcode" : i.charAt(0).toUpperCase() + i.slice(1);
}
function pe(i) {
  return (/* @__PURE__ */ new Set(["shippingname", "shippingaddress", "shippingcity", "shippingcountry", "shippingemail", "shippingapartment", "shippingzipcode", "shippingphone", "customeremail", "customername", "customeraddress", "customerapartment", "customercity", "customercountry", "customerzipcode", "customerphone"])).has(i.toLowerCase());
}
function Z(i) {
  return i.replace(/^customshipping/i, "").replace(/^custompayment/i, "").replace(/^shipping/i, "").replace(/^customer/i, "");
}
function H(i, d) {
  return Object.entries(i || {}).filter(([e, t]) => (!!t || d === "customshipping" && t === "") && e.toLowerCase().startsWith(d) && !pe(e)).map(([e, t]) => [e, S(t)]);
}
function U(i) {
  return !!(i != null && i.name || i != null && i.email || i != null && i.address || i != null && i.apartment || i != null && i.city || i != null && i.country || i != null && i.zipCode || i != null && i.phone);
}
function ve(i) {
  return i.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/([A-Z])([A-Z][a-z])/g, "$1 $2").replace(/[_-]+/g, " ").trim().replace(/\b[a-z]/g, (d) => d.toUpperCase());
}
function E(i, d) {
  const e = i.indexOf("#"), t = e >= 0 ? i.slice(e) : "", r = e >= 0 ? i.slice(0, e) : i, a = r.indexOf("?"), o = a >= 0 ? r.slice(0, a) : r, n = new URLSearchParams(a >= 0 ? r.slice(a + 1) : "");
  return n.set("width", String(d)), n.set("format", "webp"), n.set("rmode", "pad"), n.set("rcolor", "ffffff"), `${o}?${n}${t}`.replace(/,/g, "%2C").replace(/ /g, "%20");
}
function me(i) {
  var t, r;
  const e = [(t = i.variant) == null ? void 0 : t.images, (r = i.product) == null ? void 0 : r.images].flatMap((a) => Array.isArray(a) ? a : []).map((a) => {
    if (typeof (a == null ? void 0 : a.url) != "string" || !a.url.trim()) return "";
    try {
      const o = a.url.trim();
      return ["http:", "https:"].includes(new URL(o, "https://ekom.invalid/").protocol) ? o : "";
    } catch {
      return "";
    }
  }).find(Boolean);
  return `<span class="ekmOrderLineThumbnail" aria-hidden="true"><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="2"/><circle cx="8" cy="8" r="1.5"/><path d="m3 17 6-6 4 4 3-3 5 5"/></svg>${e ? `<picture><source srcset="${s(E(e, 56))} 1x, ${s(E(e, 112))} 2x"><img src="${s(E(e, 56))}" width="56" height="56" alt="" loading="lazy" decoding="async"></picture>` : ""}</span>`;
}
function ge(i) {
  const d = s(i == null ? void 0 : i.title), e = typeof (i == null ? void 0 : i.url) == "string" ? i.url.trim() : "";
  if (!e) return d;
  try {
    const t = new URL(e, "https://ekom.invalid/").protocol;
    if (t !== "http:" && t !== "https:") return d;
  } catch {
    return d;
  }
  return `<a href="${s(e)}" target="_blank" rel="noopener noreferrer">${d}</a>`;
}
function ye(i) {
  var e;
  const d = ((e = i.orderLineInfo) == null ? void 0 : e.properties) || {};
  return Object.entries(d).filter(([, t]) => !!t).map(([t, r]) => `<small style="display:block; margin-top:3px;"><strong>${s(fe(t))}</strong>: ${s(S(r))}</small>`).join("");
}
function fe(i) {
  const d = i.replace(/^orderline/i, "").replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim();
  return d ? d.charAt(0).toUpperCase() + d.slice(1) : i;
}
function be(i) {
  var d, e, t, r;
  return !!(i && (i.source || i.medium || i.campaign || i.term || i.content || i.clickId || i.clickIdType || i.landingUrl || i.referrer || i.captureMethod || i.capturedAtUtc || i.hasCookieSupport !== null && i.hasCookieSupport !== void 0 || (d = i.ga4) != null && d.clientId || (e = i.ga4) != null && e.sessionId || (t = i.meta) != null && t.fbp || (r = i.meta) != null && r.fbc || Q(i.algolia)));
}
function $e(i) {
  var o, n, c, h, p, v;
  const d = Object.entries(((o = i.ga4) == null ? void 0 : o.data) || {}), e = Object.entries(((n = i.meta) == null ? void 0 : n.data) || {}), t = i.algolia, r = Array.isArray(t == null ? void 0 : t.lines) ? t.lines : [], a = Q(t) ? `<div class="ekmOrderTracking__provider"><h5>Algolia</h5>${y("User token", t.userToken)}${r.length ? `<ul>${r.map((f) => `<li class="ekmOrderTracking__wrap"><strong>Order line key</strong>: ${s(f.orderLineKey)}<br><strong>Query ID</strong>: ${s(f.queryId)}</li>`).join("")}</ul>` : ""}</div>` : "";
  return `<div class="ekmSplit"><div class="ekmSplit__column">${y("Captured", $(i.capturedAtUtc))}${y("Capture method", i.captureMethod)}${i.hasCookieSupport !== null && i.hasCookieSupport !== void 0 ? `<p>Cookie support: ${i.hasCookieSupport ? "Yes" : "No"}</p>` : ""}${y("Source", i.source)}${y("Medium", i.medium)}${y("Campaign", i.campaign)}${y("Term", i.term)}${y("Content", i.content)}${y("Click ID", i.clickId)}${y("Click ID Type", i.clickIdType)}${y("Landing URL", i.landingUrl)}${y("Referrer", i.referrer)}</div><div class="ekmSplit__column"><h5>GA4</h5>${y("Client ID", (c = i.ga4) == null ? void 0 : c.clientId)}${y("Session ID", (h = i.ga4) == null ? void 0 : h.sessionId)}${G(d)}<h5>Meta</h5>${y("FBP", (p = i.meta) == null ? void 0 : p.fbp)}${y("FBC", (v = i.meta) == null ? void 0 : v.fbc)}${G(e)}${a}</div></div>`;
}
function Q(i) {
  return !!(i && (i.userToken || Array.isArray(i.lines) && i.lines.length));
}
function y(i, d) {
  return d ? `<p class="ekmOrderTracking__wrap">${s(i)}: ${s(d)}</p>` : "";
}
function G(i) {
  return i.length ? `<ul>${i.map(([d, e]) => `<li><strong>${s(d)}</strong>: ${s(e)}</li>`).join("")}</ul>` : "";
}
function ke(i) {
  return !!(i && (i.resolvedAtUtc || i.source || i.analytics !== null && i.analytics !== void 0 || i.marketing !== null && i.marketing !== void 0));
}
function V(i) {
  return i === !0 ? "Yes" : i === !1 ? "No" : "Unknown";
}
function B(i, d) {
  return d.map((e) => {
    var t;
    return {
      key: e.key,
      label: e.label,
      value: S(i[e.property] || ((t = i.properties) == null ? void 0 : t[e.key]) || ""),
      isExtra: !1
    };
  });
}
function K(i, d) {
  return H(i, d).map(([e, t]) => ({
    key: e,
    label: Z(e).replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim() || e,
    value: t,
    isExtra: !0
  }));
}
function W(i) {
  return Object.fromEntries(i.map((d) => [d.key, d.value || ""]));
}
function Oe(i) {
  try {
    return JSON.parse(i).message || i;
  } catch {
    return i;
  }
}
customElements.define("ekom-orders-section-view", le);
export {
  le as EkomOrdersSectionViewElement,
  le as default
};
