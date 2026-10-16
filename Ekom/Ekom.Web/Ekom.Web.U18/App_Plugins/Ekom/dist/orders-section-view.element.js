var z = Object.defineProperty;
var H = (i, d, e) => d in i ? z(i, d, { enumerable: !0, configurable: !0, writable: !0, value: e }) : i[d] = e;
var l = (i, d, e) => H(i, typeof d != "symbol" ? d + "" : d, e);
import { UmbElementMixin as Z } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as Q } from "@umbraco-cms/backoffice/notification";
import { E as Y, m as u, a as J, e as s, g as $, f as y, p as X, d as O, b as ee } from "./manager-shared.js";
const te = [
  { key: "customerName", label: "Name", property: "name" },
  { key: "customerEmail", label: "Email", property: "email" },
  { key: "customerAddress", label: "Address", property: "address" },
  { key: "customerApartment", label: "Apartment", property: "apartment" },
  { key: "customerCity", label: "City", property: "city" },
  { key: "customerCountry", label: "Country", property: "country" },
  { key: "customerZipCode", label: "Zipcode", property: "zipCode" },
  { key: "customerPhone", label: "Phone", property: "phone" }
], re = [
  { key: "shippingName", label: "Name", property: "name" },
  { key: "shippingEmail", label: "Email", property: "email" },
  { key: "shippingAddress", label: "Address", property: "address" },
  { key: "shippingApartment", label: "Apartment", property: "apartment" },
  { key: "shippingCity", label: "City", property: "city" },
  { key: "shippingCountry", label: "Country", property: "country" },
  { key: "shippingZipCode", label: "Zipcode", property: "zipCode" },
  { key: "shippingPhone", label: "Phone", property: "phone" }
], ie = 180, F = 1, M = 2;
class se extends Z(HTMLElement) {
  constructor() {
    super(...arguments);
    l(this, "api", new Y());
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
    l(this, "executingActionKey", "");
    l(this, "orderRequestId", 0);
    l(this, "lockRequestId", 0);
    l(this, "orderLockStatus");
    l(this, "releasingOrderLock", !1);
    l(this, "orderLockWarning", "");
    l(this, "orderLockReleaseError", "");
    l(this, "orderLockLoading", !1);
    l(this, "orderLockError", !1);
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
    super.connectedCallback(), this.consumeContext(Q, (e) => {
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
      this.error = f(e, "Error loading Ekom Manager."), this.loading = !1, this.render();
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
      this.error = f(e, "Error searching orders.");
    } finally {
      this.loading = !1, this.render();
    }
  }
  render() {
    this.innerHTML = `
      <style>${J}</style>
      <section class="ekmManager">
        <div class="ekmManager__body">
          ${this.configError ? `<p class="status status--error" role="alert">${s(this.configError)}</p>` : ""}
          ${this.renderOrders()}
        </div>
      </section>
      ${this.renderOverlay()}
    `, this.bindEvents();
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
      const r = $(t);
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
      const r = $(t);
      return `<option value="${s(r)}" ${e.orderStatusCol === r ? "selected" : ""}>${s(t.label)}</option>`;
    }).join("")}
          </select>
        </div>
        <div class="umb-table-cell" data-label="Name">${s(e.customerName)}</div>
        <div class="umb-table-cell" data-label="Created">${s(y(e.createDate))}</div>
        <div class="umb-table-cell" data-label="Payment">${s(e.formattedTotal)}</div>
      </div>
    `;
  }
  renderPagination() {
    return `
      <div class="pagination">
        <ul>
          ${X(this.page, this.result.totalPages).map((e) => {
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
      const g = $(p);
      return `<option value="${s(g)}" ${a === g ? "selected" : ""}>${s(p.label)}</option>`;
    }).join("")}
            </select>
          </label>
          <label class="ekmCheckboxLabel"><input type="checkbox" data-field="notifyOrderStatus" ${this.notifyOrderStatus ? "checked" : ""} ${this.config ? "" : "disabled"}> Fire events?</label>
          <button type="button" class="btn-success" data-action="save-overlay-status" ${this.config ? "" : "disabled"}>Save</button>
          <button type="button" class="btn-outline ekmOrderStatusBar__print" data-action="print-order">Print</button>
        </div>
        ${this.configError ? `<p class="status status--error" role="alert">${s(this.configError)}</p>` : ""}
        <p>UniqueId: ${s(e.uniqueId)}</p>
        <p>Created date: ${s(y(e.createDate))}</p>
        <p>Paid date: ${s(y(e.paidDate))}</p>
        <p>Store: ${s(((c = e.storeInfo) == null ? void 0 : c.alias) || e.storeAlias)}</p>
        <p>Payment: <strong>${s((h = e.chargedAmount) == null ? void 0 : h.currencyString)}</strong></p>
        ${this.renderOrderLockStatus()}
        ${this.renderOrderActions()}
      </div>
      <div class="ekmSplit">
        <div class="ekmSplit__column"><h4>Billing</h4><button type="button" class="btn-outline" data-action="open-customer-editor" style="margin-bottom:10px;">Edit customer information</button>${this.renderAddress(t)}${this.renderExtraProperties(t.properties, "customer")}</div>
        <div class="ekmSplit__column"><h4>Shipping</h4>${le(r) ? `${this.renderAddress(r)}${this.renderExtraProperties(r.properties, "shipping")}` : '<p style="font-weight:bold">Same as billing address</p>'}</div>
      </div>
      <div class="ekmSplit">
        <div class="ekmSplit__column">${this.renderProvider("Payment Method", e.paymentProvider, "custompayment")}</div>
        <div class="ekmSplit__column">${e.shippingProvider ? this.renderProvider("Shipping Method", e.shippingProvider, "customshipping") : "<h4>Shipping Method</h4>"}<button type="button" class="btn-outline" data-action="open-shipping-provider-editor">${e.shippingProvider ? "Change shipping provider" : "Add shipping provider"}</button></div>
      </div>
      ${this.renderGiftcards(e)}
      ${this.renderOrderLines(e)}
      ${this.renderTracking(e)}
      ${this.renderConsent(e)}
      ${this.renderActivityLogs()}
    `;
  }
  renderAddress(e) {
    return ["name", "email", "address", "apartment", "city", "country", "zipCode", "phone"].filter((t) => e[t]).map((t) => `<p>${ne(t)}: ${s(O(e[t]))}</p>`).join("");
  }
  renderExtraProperties(e, t) {
    const r = B(e, t);
    return r.length ? `<h5 style="margin-top:20px; font-weight:bold;">${t === "customshipping" ? "Shipping Provider Custom" : `Extra ${t === "shipping" ? "Shipping" : "Customer"}`} Data</h5><ul>${r.map(([a, o]) => `<li><strong>${s(G(a))}</strong>: ${s(o)}</li>`).join("")}</ul>` : "";
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
      const a = oe(r), o = ae(r);
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
  renderOrderLines(e) {
    var o, n, c, h, p, g, b;
    const t = Array.isArray(e.orderLines) ? e.orderLines : [], r = typeof e.coupon == "string" ? e.coupon.trim() : "", a = r ? `Discount (${r})` : "Discount";
    return `
      <div style="align-items:center; display:flex; gap:10px; justify-content:space-between;"><h4>Order Details</h4><button type="button" class="btn-outline" data-action="open-order-line-editor">Add order line</button></div>
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed">Product</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Unit Price (inc VAT)</div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">Total (inc VAT)</div></div></div>
        <div class="umb-table-body">
          ${t.map((v) => {
      var S, E, w, x, I, A, P, C, _, T, q, R;
      return `<div class="umb-table-row"><div class="umb-table-cell"><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${s(v.key)}" data-product-title="${s((S = v.product) == null ? void 0 : S.title)}" ${this.removingOrderLineId === v.key ? "disabled" : ""} aria-label="Remove ${s((E = v.product) == null ? void 0 : E.title)}" title="Remove order line">&#128465;</button></div><div class="umb-table-cell not-fixed"><span>${ce(v.product)} (${s((w = v.product) == null ? void 0 : w.sku)})</span>${v.variant ? `<small style="display:block; margin-top:3px;">${s(v.variant.title)} ${v.variant.sku ? `(${s(v.variant.sku)})` : ""}</small>` : ""}${ue(v)}</div><div class="umb-table-cell">${s(v.quantity)}</div><div class="umb-table-cell">${s((A = (I = (x = v.product) == null ? void 0 : x.price) == null ? void 0 : I.withVat) == null ? void 0 : A.currencyString)}</div><div class="umb-table-cell">${s((C = (P = v.amount) == null ? void 0 : P.vat) == null ? void 0 : C.currencyString)}</div><div class="umb-table-cell">-${s((T = (_ = v.amount) == null ? void 0 : _.discountAmount) == null ? void 0 : T.currencyString)}</div><div class="umb-table-cell"><strong>${s((R = (q = v.amount) == null ? void 0 : q.withVat) == null ? void 0 : R.currencyString)}</strong></div></div>`;
    }).join("")}
        </div>
        <div class="umb-table-footer">
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Sub Total (inc VAT)</div><div class="umb-table-cell">${s((n = (o = e.subTotal) == null ? void 0 : o.withVat) == null ? void 0 : n.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">${s(a)}</div><div class="umb-table-cell">-${s((c = e.discountAmount) == null ? void 0 : c.currencyString)}</div></div>
          ${e.shippingProvider ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Shipping Total</div><div class="umb-table-cell">${s((p = (h = e.shippingProvider.price) == null ? void 0 : h.withVat) == null ? void 0 : p.currencyString)}</div></div>` : ""}
          ${this.renderGiftcardTotal(e)}
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">${s((g = e.chargedVat) == null ? void 0 : g.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Total</div><div class="umb-table-cell"><strong>${s((b = e.chargedAmount) == null ? void 0 : b.currencyString)}</strong></div></div>
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
    return pe(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Tracking</h4><button class="btn-reset" type="button" data-action="toggle-tracking">${this.trackingExpanded ? "Hide" : "Show"}</button></div>${this.trackingExpanded ? ve(t) : ""}</div>` : '<div class="ekmOrderTracking"><h4>Tracking</h4><p>No tracking data was captured for this order.</p></div>';
  }
  renderConsent(e) {
    const t = e.consent;
    return me(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Consent</h4><button class="btn-reset" type="button" data-action="toggle-consent">${this.consentExpanded ? "Hide" : "Show"}</button></div>${this.consentExpanded ? `<p>Resolved: ${s(y(t.resolvedAtUtc))}</p><p>Source: ${s(t.source)}</p><p>Analytics: ${N(t.analytics)}</p><p>Marketing: ${N(t.marketing)}</p>` : ""}</div>` : '<div class="ekmOrderTracking"><h4>Consent</h4><p>No consent data was captured for this order.</p></div>';
  }
  renderActivityLogs() {
    return this.orderLogsLoading ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Loading activity...</p></div>' : this.orderLogsError ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Unable to load activity log.</p></div>' : this.orderLogs.length ? `<div class="ekmOrderActivityLog"><h4>Activity log</h4><div class="ekmOrderActivityLog__list">${this.orderLogs.map((t, r) => {
      const a = t.message ?? "", o = this.activityLogExpandedIndexes.has(r), n = this.canExpandActivityLog(a) ? `<button type="button" class="btn-reset ekmOrderActivityLog__toggle" data-action="toggle-activity-log" data-log-index="${r}">${o ? "Show less" : "Show more"}</button>` : "";
      return `<div class="ekmOrderActivityLog__item"><div class="ekmOrderActivityLog__content"><span class="ekmOrderActivityLog__icon ${this.getActivityLogTypeClass(t)}">${this.getActivityLogIcon(t)}</span><div class="ekmOrderActivityLog__body"><div class="ekmOrderActivityLog__date">${s(y(t.date))}</div><div class="ekmOrderActivityLog__message${o ? " ekmOrderActivityLog__message--expanded" : ""}">${s(a)}</div>${n}</div></div></div>`;
    }).join("")}</div></div>` : '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>No activity yet.</p></div>';
  }
  getActivityLogIcon(e) {
    switch (e.logType) {
      case F:
        return "✓";
      case M:
        return "!";
      default:
        return "i";
    }
  }
  getActivityLogTypeClass(e) {
    switch (e.logType) {
      case F:
        return "ekmOrderActivityLog__icon--success";
      case M:
        return "ekmOrderActivityLog__icon--alert";
      default:
        return "ekmOrderActivityLog__icon--info";
    }
  }
  canExpandActivityLog(e) {
    return e.length > ie;
  }
  renderOrderActions() {
    const e = this.orderActions.filter((t) => t.giftcardCode == null);
    return !e.length && !this.orderActionsLoading ? "" : `<div style="margin-top:15px;"><h4>Order Actions</h4><div style="display:flex; flex-wrap:wrap; gap:10px;">${e.map((t) => this.renderAction(t)).join("")}</div>${this.orderActionsLoading ? "<p>Loading actions...</p>" : ""}</div>`;
  }
  renderAction(e) {
    const t = this.executingActionKey === e.key;
    return `<span style="display:inline-block; margin:4px;"><button type="button" class="${e.look === "primary" ? "btn-success" : "btn-outline"}" data-action="execute-order-action" data-action-key="${s(e.key)}" ${e.enabled === !1 || this.executingActionKey || this.releasingOrderLock ? "disabled" : ""}>${s(e.label)}${t ? " (Working...)" : ""}</button>${e.enabled === !1 && e.disabledReason ? `<small style="display:block;">${s(e.disabledReason)}</small>` : ""}</span>`;
  }
  renderOrderLockStatus() {
    const e = this.orderLockStatus, t = this.orderLockWarning ? `<p role="alert">${s(this.orderLockWarning)}</p>` : "", r = this.orderLockError ? '<p role="alert">We could not check whether checkout is locked. Reopen the order to try again.</p>' : "", a = this.orderLockReleaseError ? `<p role="alert">${s(this.orderLockReleaseError)}</p>` : "";
    if (!(e != null && e.hasLock))
      return `${t}${r}${a}${this.orderLockLoading ? "<p>Checking checkout...</p>" : ""}`;
    const o = !e.canRelease || !e.releaseToken ? e.releaseUnavailableReason || "This checkout lock cannot be released right now." : "";
    return `<section class="ekmOrderTracking"><h4>Checkout is locked</h4>${t}${r}${a}<p>${e.reconciliationRequired && !e.paymentOperationLocked && !e.preparationLocked ? "The previous payment needs checking before the customer can pay or edit their basket." : "A previous checkout did not finish. This prevents the customer from paying or editing their basket."}</p><p>If you have checked that no payment is being processed, Release Lock resets checkout and lets the customer try again.</p><button type="button" class="btn-outline" data-action="release-order-lock" ${o || this.orderLockLoading || this.orderLockError || this.releasingOrderLock || this.executingActionKey ? "disabled" : ""}>Release Lock</button>${o ? `<p>${s(o)}</p>` : ""}<details><summary>Technical details</summary><ul>${e.paymentOperationLocked ? "<li>Payment operation ownership retained.</li>" : ""}${e.preparationLocked ? "<li>Checkout preparation ownership retained.</li>" : ""}${e.reconciliationRequired ? "<li>Reconciliation required.</li>" : ""}</ul><p>Attempt ID: ${s(e.attemptId || "Not available")}</p><p>Attempt state: ${s(e.attemptState || "Not available")}</p></details></section>`;
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
    this.overlay === "order" && ((e = this.querySelector(".ekmOverlay")) == null || e.addEventListener("click", (t) => {
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
      window.print();
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
        ee(e, this.exportIncludeOrderLines ? "orders-with-orderlines.csv" : "orders.csv"), this.overlay = "";
      } catch (e) {
        this.showError(f(e, "Error exporting orders."));
      } finally {
        this.exporting = !1, this.render();
      }
    }
  }
  async openOrder(e) {
    if (!e)
      return;
    const t = ++this.orderRequestId;
    ++this.lockRequestId, this.notifyOrderStatusOverride = void 0, this.selectedOrder = void 0, this.orderActions = [], this.executingActionKey = "", this.orderLockStatus = void 0, this.releasingOrderLock = !1, this.orderLockWarning = "", this.orderLockReleaseError = "", this.orderLockError = !1, this.orderLockLoading = !1, this.render();
    try {
      const r = await this.api.orderInfo(e);
      if (t !== this.orderRequestId) return;
      this.selectedOrder = r, this.overlay = "order", this.trackingExpanded = !1, this.consentExpanded = !1, this.orderLogs = [], this.orderLogsError = !1, this.activityLogExpandedIndexes.clear(), this.render(), await Promise.all([this.loadOrderLogs(e), this.loadOrderActions(e), this.loadOrderLockStatus(e)]);
    } catch (r) {
      if (t !== this.orderRequestId) return;
      this.showError(f(r, "Error on getting orderInfo."));
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
    this.orderActionsLoading = !0, this.render();
    try {
      const r = await this.api.orderActions(e);
      if (t !== this.orderRequestId) return;
      this.orderActions = r;
    } catch {
      if (t !== this.orderRequestId) return;
      this.orderActions = [];
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
  async releaseOrderLock() {
    var n;
    const e = (n = this.selectedOrder) == null ? void 0 : n.uniqueId, t = this.orderLockStatus;
    if (!e || !(t != null && t.hasLock) || !t.canRelease || !t.releaseToken || this.orderLockLoading || this.orderLockError || this.releasingOrderLock || this.executingActionKey || !window.confirm("Release this checkout lock? Confirm that no payment is currently being processed and the previous checkout has stopped. The customer's basket will be kept, and they can try checkout again. This does not cancel or refund a payment. Any gift-card hold that cannot be released must be checked separately.")) return;
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
      const [h] = await Promise.all([this.api.orderInfo(e), this.loadOrderLogs(e), this.loadOrderActions(e), this.loadOrderLockStatus(e)]);
      a() && (this.selectedOrder = h);
    } catch (c) {
      if (!a()) return;
      this.orderLockReleaseError = o ? "Checkout was unlocked, but we could not refresh all order details. Reopen the order to check them." : f(c, "We could not confirm the checkout reset. Check the updated order before trying again."), await Promise.all([
        this.loadOrderLogs(e),
        this.loadOrderActions(e),
        this.loadOrderLockStatus(e),
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
    var r, a;
    if (!this.config || !((r = this.selectedOrder) != null && r.uniqueId))
      return;
    const e = ((a = this.querySelector('[data-field="orderStatusOverlay"]')) == null ? void 0 : a.value) || "", t = this.notifyOrderStatus;
    try {
      await this.api.changeOrderStatus(this.selectedOrder.uniqueId, e, t), this.selectedOrder.orderStatus = e, this.showSuccess("Order status updated."), await this.loadOrders(), await this.loadOrderLogs(this.selectedOrder.uniqueId);
    } catch (o) {
      this.showError(f(o, "Error updating order status."));
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
    ++this.orderRequestId, ++this.lockRequestId, this.overlay = "", this.selectedOrder = void 0, this.customerEditorOpen = !1, this.customerEditModel = void 0, this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.shippingProviderEditorOpen = !1, this.render();
  }
  getOrderStatusValue(e) {
    const t = String(e ?? ""), r = u.statusList.find((a) => String(a.value ?? "") === t || String(a.enumValue ?? "") === t);
    return r ? $(r) : t;
  }
  async changeRowStatus(e) {
    const t = e.dataset.orderId || "";
    if (!(!this.config || !t))
      try {
        await this.api.changeOrderStatus(t, e.value, this.config.managerOrderStatusFireEventsByDefault ?? !0);
        const r = this.result.orders.find((a) => a.uniqueId === t);
        r && (r.orderStatusCol = e.value), this.showSuccess("Order status updated.");
      } catch (r) {
        this.showError(f(r, "Error updating order status.")), await this.loadOrders();
      }
  }
  async executeOrderAction(e) {
    var o;
    if (!((o = this.selectedOrder) != null && o.uniqueId) || !e || this.executingActionKey || this.releasingOrderLock)
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
          const b = await c.text();
          this.showSuccess(ge(b));
        }
        if (a !== this.orderRequestId) return;
        const [g] = await Promise.all([this.api.orderInfo(r), this.loadOrderLockStatus(r)]);
        if (a !== this.orderRequestId) return;
        this.selectedOrder = g, await Promise.all([this.loadOrderLogs(r), this.loadOrderActions(r)]);
      } catch (n) {
        if (a !== this.orderRequestId) return;
        this.showError(f(n, "Order action failed."));
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
      customer: j(t, te).concat(U(t.properties, "customer")),
      shipping: j(r, re).concat(U(r.properties, "shipping"))
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
          V(this.customerEditModel.customer),
          V(this.customerEditModel.shipping)
        ), this.customerEditorOpen = !1, this.customerEditModel = void 0, this.showSuccess("Customer information updated."), await this.loadOrders();
      } catch (t) {
        this.showError(f(t, "Error updating customer information."));
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
      this.showError(f(n, "Error adding order line."));
    } finally {
      this.orderLineSaving = !1, this.render();
    }
  }
  async openShippingProviderEditor() {
    var e, t, r;
    if (this.selectedOrder)
      try {
        const a = ((e = this.selectedOrder.storeInfo) == null ? void 0 : e.alias) || this.selectedOrder.storeAlias;
        this.shippingProviders = await this.api.shippingProviders(a), this.shippingProviderId = ((t = this.selectedOrder.shippingProvider) == null ? void 0 : t.key) || "", this.shippingCustomFields = Object.entries(((r = this.selectedOrder.shippingProvider) == null ? void 0 : r.customData) || {}).filter(([o]) => o.toLowerCase().startsWith("customshipping")).map(([o, n]) => ({ key: o, value: O(n), existing: !0 })), this.shippingProviderEditorOpen = !0, this.render();
      } catch (a) {
        this.showError(f(a, "Error loading shipping providers."));
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
      this.showError(f(n, "Error updating shipping provider."));
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
        this.showError(f(a, "Error removing order line."));
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
function f(i, d) {
  return i instanceof Error ? i.message : d;
}
function k(i) {
  return i != null && (typeof i != "string" || i.trim() !== "");
}
function K(i) {
  if (i === "code") return "Gift-card number";
  if (i === "claimId") return "Reservation reference";
  if (i === "transactionId") return "Transaction reference";
  if (i === "usedDate") return "Used on";
  if (i === "validUntil") return "Saved expiry";
  if (i === "claimDate") return "Payment claim date";
  const d = i.replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ");
  return d.charAt(0).toUpperCase() + d.slice(1).toLowerCase().replace(/\bid\b/g, "ID");
}
function L(i) {
  if (!k(i)) return "Not recorded";
  if (typeof i == "boolean") return i ? "Yes" : "No";
  if (Array.isArray(i)) return i.length ? i.map(L).join("; ") : "Empty list";
  if (typeof i == "object") {
    const d = Object.entries(i);
    return d.length ? d.map(([e, t]) => `${K(e)}: ${L(t)}`).join("; ") : "Empty object";
  }
  return String(i);
}
function ae(i) {
  const d = ["code", "amount", "claimId", "claimDate", "transactionId", "usedDate", "claimed", "validUntil"];
  return [...d, ...Object.keys(i).filter((e) => !d.includes(e))].map((e) => ({ label: K(e), value: L(i[e]) }));
}
function oe(i, d = Date.now()) {
  const e = k(i.claimId), t = k(i.validUntil), r = t ? Date.parse(String(i.validUntil)) : NaN, a = r > d, o = i.claimed === !0 || k(i.transactionId) || k(i.claimDate) || k(i.usedDate), n = o ? "Used" : e && a ? "Reserved" : e && r <= d ? "Reservation expired" : e || t ? "Needs checking" : "Selected";
  let c = "";
  !o && !e && t ? c = "An expiry time is saved, but the reservation reference is missing. Check the gift-card provider if an amount is still held." : !o && e && r <= d ? c = "The saved reservation expiry has passed. This does not confirm that the provider has released the hold." : !o && e && !t && (c = "No expiry time is saved. Check the gift-card provider if an amount is still held."), t && !Number.isFinite(r) && (c += `${c ? " " : ""}The saved expiry time could not be read. Check the gift-card provider.`);
  let h = "Not recorded";
  if (t && (h = Number.isFinite(r) ? new Intl.DateTimeFormat(void 0, { dateStyle: "medium", timeStyle: "long" }).format(new Date(r)) : String(i.validUntil), a)) {
    const p = Math.ceil((r - d) / 6e4), g = p < 60 ? p : p < 1440 ? Math.ceil(p / 60) : Math.ceil(p / 1440), b = p < 60 ? "minute" : p < 1440 ? "hour" : "day";
    h += ` (in about ${g} ${b}${g === 1 ? "" : "s"})`;
  }
  return { status: n, warning: c, expiryLabel: !o && e ? "Reservation ends" : "Saved expiry", expiry: h };
}
function ne(i) {
  return i === "zipCode" ? "Zipcode" : i.charAt(0).toUpperCase() + i.slice(1);
}
function de(i) {
  return (/* @__PURE__ */ new Set(["shippingname", "shippingaddress", "shippingcity", "shippingcountry", "shippingemail", "shippingapartment", "shippingzipcode", "shippingphone", "customeremail", "customername", "customeraddress", "customerapartment", "customercity", "customercountry", "customerzipcode", "customerphone"])).has(i.toLowerCase());
}
function G(i) {
  return i.replace(/^customshipping/i, "").replace(/^custompayment/i, "").replace(/^shipping/i, "").replace(/^customer/i, "");
}
function B(i, d) {
  return Object.entries(i || {}).filter(([e, t]) => (!!t || d === "customshipping" && t === "") && e.toLowerCase().startsWith(d) && !de(e)).map(([e, t]) => [e, O(t)]);
}
function le(i) {
  return !!(i != null && i.name || i != null && i.email || i != null && i.address || i != null && i.apartment || i != null && i.city || i != null && i.country || i != null && i.zipCode || i != null && i.phone);
}
function ce(i) {
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
function ue(i) {
  var e;
  const d = ((e = i.orderLineInfo) == null ? void 0 : e.properties) || {};
  return Object.entries(d).filter(([, t]) => !!t).map(([t, r]) => `<small style="display:block; margin-top:3px;"><strong>${s(he(t))}</strong>: ${s(O(r))}</small>`).join("");
}
function he(i) {
  const d = i.replace(/^orderline/i, "").replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim();
  return d ? d.charAt(0).toUpperCase() + d.slice(1) : i;
}
function pe(i) {
  var d, e, t, r;
  return !!(i && (i.source || i.medium || i.campaign || i.term || i.content || i.clickId || i.clickIdType || i.landingUrl || i.referrer || i.captureMethod || i.capturedAtUtc || i.hasCookieSupport !== null && i.hasCookieSupport !== void 0 || (d = i.ga4) != null && d.clientId || (e = i.ga4) != null && e.sessionId || (t = i.meta) != null && t.fbp || (r = i.meta) != null && r.fbc || W(i.algolia)));
}
function ve(i) {
  var o, n, c, h, p, g;
  const d = Object.entries(((o = i.ga4) == null ? void 0 : o.data) || {}), e = Object.entries(((n = i.meta) == null ? void 0 : n.data) || {}), t = i.algolia, r = Array.isArray(t == null ? void 0 : t.lines) ? t.lines : [], a = W(t) ? `<div class="ekmOrderTracking__provider"><h5>Algolia</h5>${m("User token", t.userToken)}${r.length ? `<ul>${r.map((b) => `<li class="ekmOrderTracking__wrap"><strong>Order line key</strong>: ${s(b.orderLineKey)}<br><strong>Query ID</strong>: ${s(b.queryId)}</li>`).join("")}</ul>` : ""}</div>` : "";
  return `<div class="ekmSplit"><div class="ekmSplit__column">${m("Captured", y(i.capturedAtUtc))}${m("Capture method", i.captureMethod)}${i.hasCookieSupport !== null && i.hasCookieSupport !== void 0 ? `<p>Cookie support: ${i.hasCookieSupport ? "Yes" : "No"}</p>` : ""}${m("Source", i.source)}${m("Medium", i.medium)}${m("Campaign", i.campaign)}${m("Term", i.term)}${m("Content", i.content)}${m("Click ID", i.clickId)}${m("Click ID Type", i.clickIdType)}${m("Landing URL", i.landingUrl)}${m("Referrer", i.referrer)}</div><div class="ekmSplit__column"><h5>GA4</h5>${m("Client ID", (c = i.ga4) == null ? void 0 : c.clientId)}${m("Session ID", (h = i.ga4) == null ? void 0 : h.sessionId)}${D(d)}<h5>Meta</h5>${m("FBP", (p = i.meta) == null ? void 0 : p.fbp)}${m("FBC", (g = i.meta) == null ? void 0 : g.fbc)}${D(e)}${a}</div></div>`;
}
function W(i) {
  return !!(i && (i.userToken || Array.isArray(i.lines) && i.lines.length));
}
function m(i, d) {
  return d ? `<p class="ekmOrderTracking__wrap">${s(i)}: ${s(d)}</p>` : "";
}
function D(i) {
  return i.length ? `<ul>${i.map(([d, e]) => `<li><strong>${s(d)}</strong>: ${s(e)}</li>`).join("")}</ul>` : "";
}
function me(i) {
  return !!(i && (i.resolvedAtUtc || i.source || i.analytics !== null && i.analytics !== void 0 || i.marketing !== null && i.marketing !== void 0));
}
function N(i) {
  return i === !0 ? "Yes" : i === !1 ? "No" : "Unknown";
}
function j(i, d) {
  return d.map((e) => {
    var t;
    return {
      key: e.key,
      label: e.label,
      value: O(i[e.property] || ((t = i.properties) == null ? void 0 : t[e.key]) || ""),
      isExtra: !1
    };
  });
}
function U(i, d) {
  return B(i, d).map(([e, t]) => ({
    key: e,
    label: G(e).replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim() || e,
    value: t,
    isExtra: !0
  }));
}
function V(i) {
  return Object.fromEntries(i.map((d) => [d.key, d.value || ""]));
}
function ge(i) {
  try {
    return JSON.parse(i).message || i;
  } catch {
    return i;
  }
}
customElements.define("ekom-orders-section-view", se);
export {
  se as EkomOrdersSectionViewElement,
  se as default
};
