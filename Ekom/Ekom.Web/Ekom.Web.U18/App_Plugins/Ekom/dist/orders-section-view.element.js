var K = Object.defineProperty;
var R = (i, d, e) => d in i ? K(i, d, { enumerable: !0, configurable: !0, writable: !0, value: e }) : i[d] = e;
var n = (i, d, e) => R(i, typeof d != "symbol" ? d + "" : d, e);
import { UmbElementMixin as G } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as z } from "@umbraco-cms/backoffice/notification";
import { E as H, m as l, a as Z, e as s, g as f, f as g, p as Q, d as O, b as W } from "./manager-shared.js";
const Y = [
  { key: "customerName", label: "Name", property: "name" },
  { key: "customerEmail", label: "Email", property: "email" },
  { key: "customerAddress", label: "Address", property: "address" },
  { key: "customerApartment", label: "Apartment", property: "apartment" },
  { key: "customerCity", label: "City", property: "city" },
  { key: "customerCountry", label: "Country", property: "country" },
  { key: "customerZipCode", label: "Zipcode", property: "zipCode" },
  { key: "customerPhone", label: "Phone", property: "phone" }
], J = [
  { key: "shippingName", label: "Name", property: "name" },
  { key: "shippingEmail", label: "Email", property: "email" },
  { key: "shippingAddress", label: "Address", property: "address" },
  { key: "shippingApartment", label: "Apartment", property: "apartment" },
  { key: "shippingCity", label: "City", property: "city" },
  { key: "shippingCountry", label: "Country", property: "country" },
  { key: "shippingZipCode", label: "Zipcode", property: "zipCode" },
  { key: "shippingPhone", label: "Phone", property: "phone" }
], X = 180, T = 1, M = 2;
class ee extends G(HTMLElement) {
  constructor() {
    super(...arguments);
    n(this, "api", new H());
    n(this, "notificationContext");
    n(this, "result", { orders: [], count: 0, totalPages: 0 });
    n(this, "page", 1);
    n(this, "loading", !0);
    n(this, "error", "");
    n(this, "searchTimer", 0);
    n(this, "overlay", "");
    n(this, "selectedOrder");
    n(this, "orderLogs", []);
    n(this, "orderLogsLoading", !1);
    n(this, "orderLogsError", !1);
    n(this, "activityLogExpandedIndexes", /* @__PURE__ */ new Set());
    n(this, "orderActions", []);
    n(this, "orderActionsLoading", !1);
    n(this, "executingActionKey", "");
    n(this, "trackingExpanded", !1);
    n(this, "consentExpanded", !1);
    n(this, "customerEditorOpen", !1);
    n(this, "customerSaving", !1);
    n(this, "customerEditModel");
    n(this, "orderLineEditorOpen", !1);
    n(this, "orderLineSaving", !1);
    n(this, "orderLineEditModel");
    n(this, "shippingProviderEditorOpen", !1);
    n(this, "shippingProviderSaving", !1);
    n(this, "shippingProviders", []);
    n(this, "shippingProviderId", "");
    n(this, "removingOrderLineId", "");
    n(this, "exportIncludeOrderLines", !1);
    n(this, "exporting", !1);
    n(this, "handleKeyDown", (e) => {
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
    super.connectedCallback(), this.consumeContext(z, (e) => {
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
        this.api.stores()
      ]);
      l.statusList = e || [], l.stores = t || [], !l.filters.store && l.stores.length && (l.filters.store = l.stores[0].alias), await this.loadPaymentProviders(), await this.loadOrders();
    } catch (e) {
      this.error = m(e, "Error loading Ekom Manager."), this.loading = !1, this.render();
    }
  }
  async loadPaymentProviders(e = !1) {
    if (!l.filters.store) {
      l.paymentProviders = [];
      return;
    }
    l.paymentProviders = await this.api.paymentProviders(l.filters.store), e && (l.filters.paymentProvider = ""), l.filters.paymentProvider && (l.paymentProviders.some((r) => r.key === l.filters.paymentProvider) || (l.filters.paymentProvider = ""));
  }
  async loadOrders() {
    if (!l.filters.store) {
      this.loading = !1, this.result = { orders: [], count: 0, totalPages: 0 }, this.render();
      return;
    }
    this.loading = !0, this.error = "", this.render();
    try {
      const e = await this.api.searchOrders(l.filters, this.page);
      this.result = {
        ...e,
        orders: e.orders || []
      };
    } catch (e) {
      this.error = m(e, "Error searching orders.");
    } finally {
      this.loading = !1, this.render();
    }
  }
  render() {
    this.innerHTML = `
      <style>${Z}</style>
      <section class="ekmManager">
        <div class="ekmManager__body">
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
    const e = l.filters;
    return `
      <div class="umb-sub-header">
        <div class="ekmManager__filters">
          <label class="ekmManager__filter">Order Status:
            <select data-field="orderStatus">
              <option value="CompletedOrders" ${e.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option>
              <option value="AllOrders" ${e.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>
              ${l.statusList.map((t) => {
      const r = f(t);
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
              ${l.stores.map((t) => `<option value="${s(t.alias)}" ${e.store === t.alias ? "selected" : ""}>${s(t.title)}</option>`).join("")}
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
          <select data-action="change-row-status" data-order-id="${s(e.uniqueId)}">
            ${l.statusList.map((t) => {
      const r = f(t);
      return `<option value="${s(r)}" ${e.orderStatusCol === r ? "selected" : ""}>${s(t.label)}</option>`;
    }).join("")}
          </select>
        </div>
        <div class="umb-table-cell" data-label="Name">${s(e.customerName)}</div>
        <div class="umb-table-cell" data-label="Created">${s(g(e.createDate))}</div>
        <div class="umb-table-cell" data-label="Payment">${s(e.formattedTotal)}</div>
      </div>
    `;
  }
  renderPagination() {
    return `
      <div class="pagination">
        <ul>
          ${Q(this.page, this.result.totalPages).map((e) => {
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
    const e = l.filters;
    return `
      <div class="ekmOverlay">
        <div class="ekmOverlay__panel ekmOverlay__panel--small">
          <div class="ekmOverlay__header"><h2>Filter</h2><button class="btn-reset" type="button" data-action="close-overlay">&times;</button></div>
          <div class="ekmOverlay__content">
            <label class="control-group">Payment provider:
              <select data-filter-field="paymentProvider">
                <option value="">Select payment provider</option>
                ${l.paymentProviders.map((t) => `<option value="${s(t.key)}" ${e.paymentProvider === t.key ? "selected" : ""}>${s(t.title)}</option>`).join("")}
              </select>
            </label>
            ${this.renderFilterInput("productSku", "Product SKU:", "Exact SKU")}
            ${this.renderFilterInput("trackingSource", "Tracking source:", "facebook")}
            ${this.renderFilterInput("trackingMedium", "Tracking medium:", "paid-social")}
            ${this.renderFilterInput("trackingCampaign", "Tracking campaign:", "summer_2026")}
            ${this.renderFilterInput("trackingTerm", "Tracking term:", "running shoes")}
            ${this.renderFilterInput("trackingContent", "Tracking content:", "hero_banner")}
            ${this.renderFilterInput("trackingClickId", "Tracking click id:", "gclid or fbclid")}
            <div style="margin-top:25px; display:flex; gap:10px;"><button type="button" class="btn-success" data-action="apply-filter">Apply</button><button type="button" class="btn-outline" data-action="close-overlay">Cancel</button></div>
          </div>
        </div>
      </div>
    `;
  }
  renderFilterInput(e, t, r) {
    return `
      <label class="control-group">${s(t)}
        <input type="text" data-filter-field="${s(e)}" value="${s(l.filters[e])}" placeholder="${s(r)}">
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
    var o, c, p, v;
    const t = ((o = e.customerInformation) == null ? void 0 : o.customer) || {}, r = ((c = e.customerInformation) == null ? void 0 : c.shipping) || {}, a = this.getOrderStatusValue(e.orderStatus);
    return `
      <div class="ekmOrder__header">
        <h1>Order number: ${s(e.referenceId)}</h1>
        <div class="ekmOrderStatusBar">
          <label class="ekmOrderStatusBar__status">Order Status:
            <select data-field="orderStatusOverlay">
              ${l.statusList.map((b) => {
      const u = f(b);
      return `<option value="${s(u)}" ${a === u ? "selected" : ""}>${s(b.label)}</option>`;
    }).join("")}
            </select>
          </label>
          <label class="ekmCheckboxLabel"><input type="checkbox" data-field="notifyOrderStatus"> Fire events?</label>
          <button type="button" class="btn-success" data-action="save-overlay-status">Save</button>
          <button type="button" class="btn-outline ekmOrderStatusBar__print" data-action="print-order">Print</button>
        </div>
        <p>UniqueId: ${s(e.uniqueId)}</p>
        <p>Created date: ${s(g(e.createDate))}</p>
        <p>Paid date: ${s(g(e.paidDate))}</p>
        <p>Store: ${s(((p = e.storeInfo) == null ? void 0 : p.alias) || e.storeAlias)}</p>
        <p>Payment: <strong>${s((v = e.chargedAmount) == null ? void 0 : v.currencyString)}</strong></p>
        ${this.renderOrderActions()}
      </div>
      <div class="ekmSplit">
        <div class="ekmSplit__column"><h4>Billing</h4><button type="button" class="btn-outline" data-action="open-customer-editor" style="margin-bottom:10px;">Edit customer information</button>${this.renderAddress(t)}${this.renderExtraProperties(t.properties, "customer")}</div>
        <div class="ekmSplit__column"><h4>Shipping</h4>${ie(r) ? `${this.renderAddress(r)}${this.renderExtraProperties(r.properties, "shipping")}` : '<p style="font-weight:bold">Same as billing address</p>'}</div>
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
    return ["name", "email", "address", "apartment", "city", "country", "zipCode", "phone"].filter((t) => e[t]).map((t) => `<p>${te(t)}: ${s(O(e[t]))}</p>`).join("");
  }
  renderExtraProperties(e, t) {
    const r = V(e, t);
    return r.length ? `<h5 style="margin-top:20px; font-weight:bold;">Extra ${t === "shipping" ? "Shipping" : "Customer"} Data</h5><ul>${r.map(([a, o]) => `<li><strong>${s(U(a))}</strong>: ${s(o)}</li>`).join("")}</ul>` : "";
  }
  renderProvider(e, t, r) {
    var o, c;
    if (!t)
      return "";
    const a = typeof t.title == "string" && t.title.trim() ? t.title : (o = t.properties) == null ? void 0 : o.nodeName;
    return `<h4>${s(e)}</h4><h4><strong>${s(a)}</strong></h4>${t.price ? `<p>Price: ${s((c = t.price.withVat) == null ? void 0 : c.currencyString)}</p>` : ""}${this.renderExtraProperties(t.customData, r)}`;
  }
  renderGiftcards(e) {
    const t = Array.isArray(e.giftcards) ? e.giftcards : [];
    return t.length ? `<div class="ekmOrderGiftcards"><h4>Gift cards</h4><ul class="ekmOrderTracking__list">${t.map((r) => {
      const a = r.validUntil && Date.parse(r.validUntil) <= Date.now(), o = r.claimed ? "Claimed" : a ? "Expired" : "Available";
      return `<li><strong>${s(r.code)}</strong>: ${s(this.formatGiftcardAmount(r.amount, e))} (${o})${r.validUntil ? ` · Expires ${s(g(r.validUntil))}` : ""}</li>`;
    }).join("")}</ul></div>` : "";
  }
  formatGiftcardAmount(e, t) {
    var o, c, p, v;
    const r = ((c = (o = t.storeInfo) == null ? void 0 : o.currency) == null ? void 0 : c.currencyValue) || "en-US", a = ((v = (p = t.storeInfo) == null ? void 0 : p.currency) == null ? void 0 : v.isoCurrencySymbol) || t.currency;
    try {
      return new Intl.NumberFormat(r, { style: "currency", currency: a }).format(e);
    } catch {
      return String(e);
    }
  }
  renderOrderLines(e) {
    var r, a, o, c, p, v, b;
    return `
      <div style="align-items:center; display:flex; gap:10px; justify-content:space-between;"><h4>Order Details</h4><button type="button" class="btn-outline" data-action="open-order-line-editor">Add order line</button></div>
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed">Product</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Unit Price (inc VAT)</div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">Total (inc VAT)</div></div></div>
        <div class="umb-table-body">
          ${(Array.isArray(e.orderLines) ? e.orderLines : []).map((u) => {
      var y, $, S, k, E, L, w, x, _, A, I, P, C;
      return `<div class="umb-table-row"><div class="umb-table-cell"><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${s(u.key)}" data-product-title="${s((y = u.product) == null ? void 0 : y.title)}" ${this.removingOrderLineId === u.key ? "disabled" : ""} aria-label="Remove ${s(($ = u.product) == null ? void 0 : $.title)}" title="Remove order line">&#128465;</button></div><div class="umb-table-cell not-fixed">${s((S = u.product) == null ? void 0 : S.title)} (${s((k = u.product) == null ? void 0 : k.sku)})${u.variant ? `<small style="display:block; margin-top:3px;">${s(u.variant.title)} ${u.variant.sku ? `(${s(u.variant.sku)})` : ""}</small>` : ""}${se(u)}</div><div class="umb-table-cell">${s(u.quantity)}</div><div class="umb-table-cell">${s((w = (L = (E = u.product) == null ? void 0 : E.price) == null ? void 0 : L.withVat) == null ? void 0 : w.currencyString)}</div><div class="umb-table-cell">${s((_ = (x = u.amount) == null ? void 0 : x.vat) == null ? void 0 : _.currencyString)}</div><div class="umb-table-cell">-${s((I = (A = u.amount) == null ? void 0 : A.discountAmount) == null ? void 0 : I.currencyString)}</div><div class="umb-table-cell"><strong>${s((C = (P = u.amount) == null ? void 0 : P.withVat) == null ? void 0 : C.currencyString)}</strong></div></div>`;
    }).join("")}
        </div>
        <div class="umb-table-footer">
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Sub Total (inc VAT)</div><div class="umb-table-cell">${s((a = (r = e.subTotal) == null ? void 0 : r.withVat) == null ? void 0 : a.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">-${s((o = e.discountAmount) == null ? void 0 : o.currencyString)}</div></div>
          ${e.shippingProvider ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Shipping Total</div><div class="umb-table-cell">${s((p = (c = e.shippingProvider.price) == null ? void 0 : c.withVat) == null ? void 0 : p.currencyString)}</div></div>` : ""}
          ${this.renderGiftcardTotal(e)}
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">${s((v = e.chargedVat) == null ? void 0 : v.currencyString)}</div></div>
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
    return oe(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Tracking</h4><button class="btn-reset" type="button" data-action="toggle-tracking">${this.trackingExpanded ? "Hide" : "Show"}</button></div>${this.trackingExpanded ? de(t) : ""}</div>` : '<div class="ekmOrderTracking"><h4>Tracking</h4><p>No tracking data was captured for this order.</p></div>';
  }
  renderConsent(e) {
    const t = e.consent;
    return ne(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Consent</h4><button class="btn-reset" type="button" data-action="toggle-consent">${this.consentExpanded ? "Hide" : "Show"}</button></div>${this.consentExpanded ? `<p>Resolved: ${s(g(t.resolvedAtUtc))}</p><p>Source: ${s(t.source)}</p><p>Analytics: ${F(t.analytics)}</p><p>Marketing: ${F(t.marketing)}</p>` : ""}</div>` : '<div class="ekmOrderTracking"><h4>Consent</h4><p>No consent data was captured for this order.</p></div>';
  }
  renderActivityLogs() {
    return this.orderLogsLoading ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Loading activity...</p></div>' : this.orderLogsError ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Unable to load activity log.</p></div>' : this.orderLogs.length ? `<div class="ekmOrderActivityLog"><h4>Activity log</h4><div class="ekmOrderActivityLog__list">${this.orderLogs.map((t, r) => {
      const a = t.message ?? "", o = this.activityLogExpandedIndexes.has(r), c = this.canExpandActivityLog(a) ? `<button type="button" class="btn-reset ekmOrderActivityLog__toggle" data-action="toggle-activity-log" data-log-index="${r}">${o ? "Show less" : "Show more"}</button>` : "";
      return `<div class="ekmOrderActivityLog__item"><div class="ekmOrderActivityLog__content"><span class="ekmOrderActivityLog__icon ${this.getActivityLogTypeClass(t)}">${this.getActivityLogIcon(t)}</span><div class="ekmOrderActivityLog__body"><div class="ekmOrderActivityLog__date">${s(g(t.date))}</div><div class="ekmOrderActivityLog__message${o ? " ekmOrderActivityLog__message--expanded" : ""}">${s(a)}</div>${c}</div></div></div>`;
    }).join("")}</div></div>` : '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>No activity yet.</p></div>';
  }
  getActivityLogIcon(e) {
    switch (e.logType) {
      case T:
        return "✓";
      case M:
        return "!";
      default:
        return "i";
    }
  }
  getActivityLogTypeClass(e) {
    switch (e.logType) {
      case T:
        return "ekmOrderActivityLog__icon--success";
      case M:
        return "ekmOrderActivityLog__icon--alert";
      default:
        return "ekmOrderActivityLog__icon--info";
    }
  }
  canExpandActivityLog(e) {
    return e.length > X;
  }
  renderOrderActions() {
    return !this.orderActions.length && !this.orderActionsLoading ? "" : `<div style="margin-top:15px;"><h4>Order Actions</h4><div style="display:flex; flex-wrap:wrap; gap:10px;">${this.orderActions.map((e) => `<button type="button" class="${e.look === "primary" ? "btn-success" : "btn-outline"}" data-action="execute-order-action" data-action-key="${s(e.key)}" ${e.enabled === !1 || this.executingActionKey === e.key ? "disabled" : ""}>${s(e.label)}</button>`).join("")}</div>${this.orderActionsLoading ? "<p>Loading actions...</p>" : ""}</div>`;
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
    return `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel"><div class="ekmOverlay__header"><h3>${(e = this.selectedOrder) != null && e.shippingProvider ? "Change shipping provider" : "Add shipping provider"}</h3><button class="btn-reset" type="button" data-action="close-shipping-provider-editor" ${this.shippingProviderSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><label class="control-group">Shipping provider<select data-field="shippingProviderId" ${this.shippingProviderSaving ? "disabled" : ""}><option value="">Select a provider</option>${this.shippingProviders.map((t) => `<option value="${s(t.key)}" ${this.shippingProviderId === t.key ? "selected" : ""}>${s(t.title)}</option>`).join("")}</select></label>${this.shippingProviders.length ? "" : "<p>No shipping providers available for this store.</p>"}<div style="display:flex; justify-content:flex-end; gap:10px; padding-top:20px; border-top:1px solid #d8d7d9;"><button class="btn-outline" type="button" data-action="close-shipping-provider-editor" ${this.shippingProviderSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-shipping-provider" ${this.shippingProviderSaving || !this.shippingProviders.length ? "disabled" : ""}>${this.shippingProviderSaving ? "Saving..." : "Save shipping provider"}</button></div></div></div></div>`;
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
      const a = Number(t.dataset.logIndex);
      this.activityLogExpandedIndexes.has(a) ? this.activityLogExpandedIndexes.delete(a) : this.activityLogExpandedIndexes.add(a), this.renderPreservingOverlayScroll();
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
    r === "remove-order-line" && await this.removeOrderLine(t.dataset.orderLineId || "", t.dataset.productTitle || "this order line");
  }
  async handleFieldChange(e) {
    const t = e.currentTarget, r = t.dataset.field;
    if (r === "includeOrderLines") {
      this.exportIncludeOrderLines = t.checked, this.render();
      return;
    }
    r === "orderStatusOverlay" || r === "notifyOrderStatus" || r === "query" || !r || !(r in l.filters) || (l.filters[r] = t.value, this.page = 1, r === "store" && await this.loadPaymentProviders(!0), await this.loadOrders());
  }
  handleSearchInput(e) {
    const t = e.currentTarget;
    l.filters.query = t.value, this.page = 1, window.clearTimeout(this.searchTimer), this.searchTimer = window.setTimeout(() => void this.loadOrders(), 700);
  }
  applyFilterOverlay() {
    this.querySelectorAll("[data-filter-field]").forEach((e) => {
      const t = e.dataset.filterField;
      t && t in l.filters && (l.filters[t] = e.value);
    });
  }
  async exportOrders() {
    if (this.result.count) {
      this.exporting = !0, this.render();
      try {
        const e = await this.api.exportOrders(l.filters, this.result.count, this.exportIncludeOrderLines);
        W(e, this.exportIncludeOrderLines ? "orders-with-orderlines.csv" : "orders.csv"), this.overlay = "";
      } catch (e) {
        this.showError(m(e, "Error exporting orders."));
      } finally {
        this.exporting = !1, this.render();
      }
    }
  }
  async openOrder(e) {
    if (e)
      try {
        this.selectedOrder = await this.api.orderInfo(e), this.overlay = "order", this.trackingExpanded = !1, this.consentExpanded = !1, this.orderLogs = [], this.orderLogsError = !1, this.activityLogExpandedIndexes.clear(), this.render(), await Promise.all([this.loadOrderLogs(e), this.loadOrderActions(e)]);
      } catch (t) {
        this.showError(m(t, "Error on getting orderInfo."));
      }
  }
  async loadOrderLogs(e) {
    this.orderLogsLoading = !0, this.orderLogsError = !1, this.render();
    try {
      this.orderLogs = await this.api.orderLogs(e);
    } catch {
      this.orderLogs = [], this.orderLogsError = !0;
    } finally {
      this.orderLogsLoading = !1, this.render();
    }
  }
  async loadOrderActions(e) {
    this.orderActionsLoading = !0, this.render();
    try {
      this.orderActions = await this.api.orderActions(e);
    } catch {
      this.orderActions = [];
    } finally {
      this.orderActionsLoading = !1, this.render();
    }
  }
  async saveOverlayStatus() {
    var r, a, o;
    if (!((r = this.selectedOrder) != null && r.uniqueId))
      return;
    const e = ((a = this.querySelector('[data-field="orderStatusOverlay"]')) == null ? void 0 : a.value) || "", t = ((o = this.querySelector('[data-field="notifyOrderStatus"]')) == null ? void 0 : o.checked) || !1;
    try {
      await this.api.changeOrderStatus(this.selectedOrder.uniqueId, e, t), this.selectedOrder.orderStatus = e, this.showSuccess("Order status updated."), await this.loadOrders(), await this.loadOrderLogs(this.selectedOrder.uniqueId);
    } catch (c) {
      this.showError(m(c, "Error updating order status."));
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
    this.overlay = "", this.selectedOrder = void 0, this.customerEditorOpen = !1, this.customerEditModel = void 0, this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.shippingProviderEditorOpen = !1, this.render();
  }
  getOrderStatusValue(e) {
    const t = String(e ?? ""), r = l.statusList.find((a) => String(a.value ?? "") === t || String(a.enumValue ?? "") === t);
    return r ? f(r) : t;
  }
  async changeRowStatus(e) {
    const t = e.dataset.orderId || "";
    if (t)
      try {
        await this.api.changeOrderStatus(t, e.value, !0);
        const r = this.result.orders.find((a) => a.uniqueId === t);
        r && (r.orderStatusCol = e.value), this.showSuccess("Order status updated.");
      } catch (r) {
        this.showError(m(r, "Error updating order status.")), await this.loadOrders();
      }
  }
  async executeOrderAction(e) {
    var r;
    if (!((r = this.selectedOrder) != null && r.uniqueId) || !e || this.executingActionKey)
      return;
    const t = this.orderActions.find((a) => a.key === e);
    if (!(t != null && t.confirmMessage && !window.confirm(t.confirmMessage))) {
      this.executingActionKey = e, this.render();
      try {
        const a = await this.api.executeOrderAction(this.selectedOrder.uniqueId, e), o = await a.blob(), c = a.headers.get("content-disposition") || "", p = a.headers.get("content-type") || "";
        if (c.toLowerCase().includes("filename=") || p.startsWith("application/pdf") || p.startsWith("application/octet-stream") || p.startsWith("image/"))
          window.open(URL.createObjectURL(o), "_blank");
        else {
          const v = await o.text();
          this.showSuccess(le(v));
        }
        this.selectedOrder = await this.api.orderInfo(this.selectedOrder.uniqueId), await this.loadOrderLogs(this.selectedOrder.uniqueId), await this.loadOrderActions(this.selectedOrder.uniqueId);
      } catch (a) {
        this.showError(m(a, "Order action failed."));
      } finally {
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
      customer: D(t, Y).concat(N(t.properties, "customer")),
      shipping: D(r, J).concat(N(r.properties, "shipping"))
    }, this.customerEditorOpen = !0, this.render();
  }
  async saveCustomerInformation() {
    var e;
    if (!(!((e = this.selectedOrder) != null && e.uniqueId) || !this.customerEditModel || this.customerSaving)) {
      this.querySelectorAll("[data-customer-group]").forEach((t) => {
        var c;
        const r = t.dataset.customerGroup, a = t.dataset.customerKey, o = (c = this.customerEditModel) == null ? void 0 : c[r].find((p) => p.key === a);
        o && (o.value = t.value);
      }), this.customerSaving = !0, this.render();
      try {
        this.selectedOrder = await this.api.updateCustomerInformation(
          this.selectedOrder.uniqueId,
          j(this.customerEditModel.customer),
          j(this.customerEditModel.shipping)
        ), this.customerEditorOpen = !1, this.customerEditModel = void 0, this.showSuccess("Customer information updated."), await this.loadOrders();
      } catch (t) {
        this.showError(m(t, "Error updating customer information."));
      } finally {
        this.customerSaving = !1, this.render();
      }
    }
  }
  async saveOrderLine() {
    var o;
    if (!((o = this.selectedOrder) != null && o.uniqueId) || !this.orderLineEditModel || this.orderLineSaving)
      return;
    this.querySelectorAll("[data-order-line-field]").forEach((c) => {
      const p = c.dataset.orderLineField;
      p && (this.orderLineEditModel[p] = c.value);
    });
    const { productId: e, variantId: t, quantity: r } = this.orderLineEditModel, a = Number(r);
    if (!e.trim() || !Number.isFinite(a) || a <= 0) {
      this.showError("Product ID and a positive quantity are required.");
      return;
    }
    this.orderLineSaving = !0, this.render();
    try {
      this.selectedOrder = await this.api.addOrderLine(this.selectedOrder.uniqueId, e.trim(), t.trim() || void 0, a), this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.showSuccess("Order line added."), await this.refreshOrderAfterLineChange();
    } catch (c) {
      this.showError(m(c, "Error adding order line."));
    } finally {
      this.orderLineSaving = !1, this.render();
    }
  }
  async openShippingProviderEditor() {
    var e, t;
    if (this.selectedOrder)
      try {
        const r = ((e = this.selectedOrder.storeInfo) == null ? void 0 : e.alias) || this.selectedOrder.storeAlias;
        this.shippingProviders = await this.api.shippingProviders(r), this.shippingProviderId = ((t = this.selectedOrder.shippingProvider) == null ? void 0 : t.key) || "", this.shippingProviderEditorOpen = !0, this.render();
      } catch (r) {
        this.showError(m(r, "Error loading shipping providers."));
      }
  }
  async saveShippingProvider() {
    var t, r;
    if (!((t = this.selectedOrder) != null && t.uniqueId) || this.shippingProviderSaving)
      return;
    const e = ((r = this.querySelector('[data-field="shippingProviderId"]')) == null ? void 0 : r.value) || "";
    if (!this.shippingProviders.some((a) => a.key === e)) {
      this.showError("Select a shipping provider.");
      return;
    }
    this.shippingProviderSaving = !0, this.render();
    try {
      this.selectedOrder = await this.api.updateShippingProvider(this.selectedOrder.uniqueId, e), this.shippingProviderEditorOpen = !1, this.showSuccess("Shipping provider updated."), await this.refreshOrderAfterLineChange();
    } catch (a) {
      this.showError(m(a, "Error updating shipping provider."));
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
        this.showError(m(a, "Error removing order line."));
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
function m(i, d) {
  return i instanceof Error ? i.message : d;
}
function te(i) {
  return i === "zipCode" ? "Zipcode" : i.charAt(0).toUpperCase() + i.slice(1);
}
function re(i) {
  return (/* @__PURE__ */ new Set(["shippingname", "shippingaddress", "shippingcity", "shippingcountry", "shippingemail", "shippingapartment", "shippingzipcode", "shippingphone", "customeremail", "customername", "customeraddress", "customerapartment", "customercity", "customercountry", "customerzipcode", "customerphone"])).has(i.toLowerCase());
}
function U(i) {
  return i.replace(/^customshipping/i, "").replace(/^custompayment/i, "").replace(/^shipping/i, "").replace(/^customer/i, "");
}
function V(i, d) {
  return Object.entries(i || {}).filter(([e, t]) => !!t && e.toLowerCase().startsWith(d) && !re(e)).map(([e, t]) => [e, O(t)]);
}
function ie(i) {
  return !!(i != null && i.name || i != null && i.email || i != null && i.address || i != null && i.apartment || i != null && i.city || i != null && i.country || i != null && i.zipCode || i != null && i.phone);
}
function se(i) {
  var e;
  const d = ((e = i.orderLineInfo) == null ? void 0 : e.properties) || {};
  return Object.entries(d).filter(([, t]) => !!t).map(([t, r]) => `<small style="display:block; margin-top:3px;"><strong>${s(ae(t))}</strong>: ${s(O(r))}</small>`).join("");
}
function ae(i) {
  const d = i.replace(/^orderline/i, "").replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim();
  return d ? d.charAt(0).toUpperCase() + d.slice(1) : i;
}
function oe(i) {
  var d, e, t, r;
  return !!(i && (i.source || i.medium || i.campaign || i.term || i.content || i.clickId || i.clickIdType || i.landingUrl || i.referrer || i.captureMethod || i.capturedAtUtc || i.hasCookieSupport !== null && i.hasCookieSupport !== void 0 || (d = i.ga4) != null && d.clientId || (e = i.ga4) != null && e.sessionId || (t = i.meta) != null && t.fbp || (r = i.meta) != null && r.fbc || B(i.algolia)));
}
function de(i) {
  var o, c, p, v, b, u;
  const d = Object.entries(((o = i.ga4) == null ? void 0 : o.data) || {}), e = Object.entries(((c = i.meta) == null ? void 0 : c.data) || {}), t = i.algolia, r = Array.isArray(t == null ? void 0 : t.lines) ? t.lines : [], a = B(t) ? `<div class="ekmOrderTracking__provider"><h5>Algolia</h5>${h("User token", t.userToken)}${r.length ? `<ul>${r.map((y) => `<li class="ekmOrderTracking__wrap"><strong>Order line key</strong>: ${s(y.orderLineKey)}<br><strong>Query ID</strong>: ${s(y.queryId)}</li>`).join("")}</ul>` : ""}</div>` : "";
  return `<div class="ekmSplit"><div class="ekmSplit__column">${h("Captured", g(i.capturedAtUtc))}${h("Capture method", i.captureMethod)}${i.hasCookieSupport !== null && i.hasCookieSupport !== void 0 ? `<p>Cookie support: ${i.hasCookieSupport ? "Yes" : "No"}</p>` : ""}${h("Source", i.source)}${h("Medium", i.medium)}${h("Campaign", i.campaign)}${h("Term", i.term)}${h("Content", i.content)}${h("Click ID", i.clickId)}${h("Click ID Type", i.clickIdType)}${h("Landing URL", i.landingUrl)}${h("Referrer", i.referrer)}</div><div class="ekmSplit__column"><h5>GA4</h5>${h("Client ID", (p = i.ga4) == null ? void 0 : p.clientId)}${h("Session ID", (v = i.ga4) == null ? void 0 : v.sessionId)}${q(d)}<h5>Meta</h5>${h("FBP", (b = i.meta) == null ? void 0 : b.fbp)}${h("FBC", (u = i.meta) == null ? void 0 : u.fbc)}${q(e)}${a}</div></div>`;
}
function B(i) {
  return !!(i && (i.userToken || Array.isArray(i.lines) && i.lines.length));
}
function h(i, d) {
  return d ? `<p class="ekmOrderTracking__wrap">${s(i)}: ${s(d)}</p>` : "";
}
function q(i) {
  return i.length ? `<ul>${i.map(([d, e]) => `<li><strong>${s(d)}</strong>: ${s(e)}</li>`).join("")}</ul>` : "";
}
function ne(i) {
  return !!(i && (i.resolvedAtUtc || i.source || i.analytics !== null && i.analytics !== void 0 || i.marketing !== null && i.marketing !== void 0));
}
function F(i) {
  return i === !0 ? "Yes" : i === !1 ? "No" : "Unknown";
}
function D(i, d) {
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
function N(i, d) {
  return V(i, d).map(([e, t]) => ({
    key: e,
    label: U(e).replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim() || e,
    value: t,
    isExtra: !0
  }));
}
function j(i) {
  return Object.fromEntries(i.map((d) => [d.key, d.value || ""]));
}
function le(i) {
  try {
    return JSON.parse(i).message || i;
  } catch {
    return i;
  }
}
customElements.define("ekom-orders-section-view", ee);
export {
  ee as EkomOrdersSectionViewElement,
  ee as default
};
