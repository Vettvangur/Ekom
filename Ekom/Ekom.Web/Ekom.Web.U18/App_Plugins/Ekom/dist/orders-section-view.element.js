var V = Object.defineProperty;
var U = (r, d, e) => d in r ? V(r, d, { enumerable: !0, configurable: !0, writable: !0, value: e }) : r[d] = e;
var n = (r, d, e) => U(r, typeof d != "symbol" ? d + "" : d, e);
import { UmbElementMixin as B } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as K } from "@umbraco-cms/backoffice/notification";
import { E as R, m as o, a as z, e as s, g as f, f as b, p as H, d as g, b as Z } from "./manager-shared.js";
const W = [
  { key: "customerName", label: "Name", property: "name" },
  { key: "customerEmail", label: "Email", property: "email" },
  { key: "customerAddress", label: "Address", property: "address" },
  { key: "customerApartment", label: "Apartment", property: "apartment" },
  { key: "customerCity", label: "City", property: "city" },
  { key: "customerCountry", label: "Country", property: "country" },
  { key: "customerZipCode", label: "Zipcode", property: "zipCode" },
  { key: "customerPhone", label: "Phone", property: "phone" }
], Q = [
  { key: "shippingName", label: "Name", property: "name" },
  { key: "shippingEmail", label: "Email", property: "email" },
  { key: "shippingAddress", label: "Address", property: "address" },
  { key: "shippingApartment", label: "Apartment", property: "apartment" },
  { key: "shippingCity", label: "City", property: "city" },
  { key: "shippingCountry", label: "Country", property: "country" },
  { key: "shippingZipCode", label: "Zipcode", property: "zipCode" },
  { key: "shippingPhone", label: "Phone", property: "phone" }
];
class G extends B(HTMLElement) {
  constructor() {
    super(...arguments);
    n(this, "api", new R());
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
        this.exporting || this.closeOverlay();
      }
    });
  }
  connectedCallback() {
    super.connectedCallback(), this.consumeContext(K, (e) => {
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
      o.statusList = e || [], o.stores = t || [], !o.filters.store && o.stores.length && (o.filters.store = o.stores[0].alias), await this.loadPaymentProviders(), await this.loadOrders();
    } catch (e) {
      this.error = h(e, "Error loading Ekom Manager."), this.loading = !1, this.render();
    }
  }
  async loadPaymentProviders(e = !1) {
    if (!o.filters.store) {
      o.paymentProviders = [];
      return;
    }
    o.paymentProviders = await this.api.paymentProviders(o.filters.store), e && (o.filters.paymentProvider = ""), o.filters.paymentProvider && (o.paymentProviders.some((i) => i.key === o.filters.paymentProvider) || (o.filters.paymentProvider = ""));
  }
  async loadOrders() {
    if (!o.filters.store) {
      this.loading = !1, this.result = { orders: [], count: 0, totalPages: 0 }, this.render();
      return;
    }
    this.loading = !0, this.error = "", this.render();
    try {
      const e = await this.api.searchOrders(o.filters, this.page);
      this.result = {
        ...e,
        orders: e.orders || []
      };
    } catch (e) {
      this.error = h(e, "Error searching orders.");
    } finally {
      this.loading = !1, this.render();
    }
  }
  render() {
    this.innerHTML = `
      <style>${z}</style>
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
    const e = o.filters;
    return `
      <div class="umb-sub-header">
        <div class="ekmManager__filters">
          <label class="ekmManager__filter">Order Status:
            <select data-field="orderStatus">
              <option value="CompletedOrders" ${e.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option>
              <option value="AllOrders" ${e.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>
              ${o.statusList.map((t) => {
      const i = f(t);
      return `<option value="${s(i)}" ${e.orderStatus === i ? "selected" : ""}>${s(t.label)}</option>`;
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
              ${o.stores.map((t) => `<option value="${s(t.alias)}" ${e.store === t.alias ? "selected" : ""}>${s(t.title)}</option>`).join("")}
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
            <div class="umb-table-cell">Store</div>
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
            ${o.statusList.map((t) => {
      const i = f(t);
      return `<option value="${s(i)}" ${e.orderStatusCol === i ? "selected" : ""}>${s(t.label)}</option>`;
    }).join("")}
          </select>
        </div>
        <div class="umb-table-cell" data-label="Name">${s(e.customerName)}</div>
        <div class="umb-table-cell" data-label="Store">${s(e.storeAlias)}</div>
        <div class="umb-table-cell" data-label="Created">${s(b(e.createDate))}</div>
        <div class="umb-table-cell" data-label="Payment">${s(e.formattedTotal)}</div>
      </div>
    `;
  }
  renderPagination() {
    return `
      <div class="pagination">
        <ul>
          ${H(this.page, this.result.totalPages).map((e) => {
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
    const e = o.filters;
    return `
      <div class="ekmOverlay">
        <div class="ekmOverlay__panel ekmOverlay__panel--small">
          <div class="ekmOverlay__header"><h2>Filter</h2><button class="btn-reset" type="button" data-action="close-overlay">&times;</button></div>
          <div class="ekmOverlay__content">
            <label class="control-group">Payment provider:
              <select data-filter-field="paymentProvider">
                <option value="">Select payment provider</option>
                ${o.paymentProviders.map((t) => `<option value="${s(t.key)}" ${e.paymentProvider === t.key ? "selected" : ""}>${s(t.title)}</option>`).join("")}
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
  renderFilterInput(e, t, i) {
    return `
      <label class="control-group">${s(t)}
        <input type="text" data-filter-field="${s(e)}" value="${s(o.filters[e])}" placeholder="${s(i)}">
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
    `;
  }
  renderOrderDetails(e) {
    var l, c, p, v;
    const t = ((l = e.customerInformation) == null ? void 0 : l.customer) || {}, i = ((c = e.customerInformation) == null ? void 0 : c.shipping) || {}, a = this.getOrderStatusValue(e.orderStatus);
    return `
      <div class="ekmOrder__header">
        <h1>Order number: ${s(e.referenceId)}</h1>
        <div class="ekmOrderStatusBar">
          <label class="ekmOrderStatusBar__status">Order Status:
            <select data-field="orderStatusOverlay">
              ${o.statusList.map((y) => {
      const u = f(y);
      return `<option value="${s(u)}" ${a === u ? "selected" : ""}>${s(y.label)}</option>`;
    }).join("")}
            </select>
          </label>
          <label class="ekmCheckboxLabel"><input type="checkbox" data-field="notifyOrderStatus"> Fire events?</label>
          <button type="button" class="btn-success" data-action="save-overlay-status">Save</button>
          <button type="button" class="btn-outline ekmOrderStatusBar__print" data-action="print-order">Print</button>
        </div>
        <p>UniqueId: ${s(e.uniqueId)}</p>
        <p>Created date: ${s(b(e.createDate))}</p>
        <p>Paid date: ${s(b(e.paidDate))}</p>
        <p>Store: ${s(((p = e.storeInfo) == null ? void 0 : p.alias) || e.storeAlias)}</p>
        <p>Payment: <strong>${s((v = e.chargedAmount) == null ? void 0 : v.currencyString)}</strong></p>
        ${this.renderOrderActions()}
      </div>
      <div class="ekmSplit">
        <div class="ekmSplit__column"><h4>Billing</h4><button type="button" class="btn-outline" data-action="open-customer-editor" style="margin-bottom:10px;">Edit customer information</button>${this.renderAddress(t)}${this.renderExtraProperties(t.properties, "customer")}</div>
        <div class="ekmSplit__column"><h4>Shipping</h4>${X(i) ? `${this.renderAddress(i)}${this.renderExtraProperties(i.properties, "shipping")}` : '<p style="font-weight:bold">Same as billing address</p>'}</div>
      </div>
      <div class="ekmSplit">
        <div class="ekmSplit__column">${this.renderProvider("Payment Method", e.paymentProvider, "custompayment")}</div>
        <div class="ekmSplit__column">${this.renderProvider("Shipping Method", e.shippingProvider, "customshipping")}</div>
      </div>
      ${this.renderOrderLines(e)}
      ${this.renderTracking(e)}
      ${this.renderConsent(e)}
      ${this.renderActivityLogs()}
    `;
  }
  renderAddress(e) {
    return ["name", "email", "address", "apartment", "city", "country", "zipCode", "phone"].filter((t) => e[t]).map((t) => `<p>${Y(t)}: ${s(g(e[t]))}</p>`).join("");
  }
  renderExtraProperties(e, t) {
    const i = j(e, t);
    return i.length ? `<h5 style="margin-top:20px; font-weight:bold;">Extra ${t === "shipping" ? "Shipping" : "Customer"} Data</h5><ul>${i.map(([a, l]) => `<li><strong>${s(N(a))}</strong>: ${s(l)}</li>`).join("")}</ul>` : "";
  }
  renderProvider(e, t, i) {
    var a;
    return t ? `<h4>${s(e)}</h4><h4><strong>${s(t.title)}</strong></h4>${t.price ? `<p>Price: ${s((a = t.price.withVat) == null ? void 0 : a.currencyString)}</p>` : ""}${this.renderExtraProperties(t.customData, i)}` : "";
  }
  renderOrderLines(e) {
    var i, a, l, c, p, v, y;
    return `
      <div style="align-items:center; display:flex; gap:10px; justify-content:space-between;"><h4>Order Details</h4><button type="button" class="btn-outline" data-action="open-order-line-editor">Add order line</button></div>
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed">Product</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Unit Price (inc VAT)</div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">Total (inc VAT)</div></div></div>
        <div class="umb-table-body">
          ${(Array.isArray(e.orderLines) ? e.orderLines : []).map((u) => {
      var O, $, S, k, w, E, L, x, _, C, A, I, P;
      return `<div class="umb-table-row"><div class="umb-table-cell"><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${s(u.key)}" data-product-title="${s((O = u.product) == null ? void 0 : O.title)}" ${this.removingOrderLineId === u.key ? "disabled" : ""} aria-label="Remove ${s(($ = u.product) == null ? void 0 : $.title)}" title="Remove order line">&#128465;</button></div><div class="umb-table-cell not-fixed">${s((S = u.product) == null ? void 0 : S.title)} (${s((k = u.product) == null ? void 0 : k.sku)})${u.variant ? `<small style="display:block; margin-top:3px;">${s(u.variant.title)} ${u.variant.sku ? `(${s(u.variant.sku)})` : ""}</small>` : ""}${ee(u)}</div><div class="umb-table-cell">${s(u.quantity)}</div><div class="umb-table-cell">${s((L = (E = (w = u.product) == null ? void 0 : w.price) == null ? void 0 : E.withVat) == null ? void 0 : L.currencyString)}</div><div class="umb-table-cell">${s((_ = (x = u.amount) == null ? void 0 : x.vat) == null ? void 0 : _.currencyString)}</div><div class="umb-table-cell">-${s((A = (C = u.amount) == null ? void 0 : C.discountAmount) == null ? void 0 : A.currencyString)}</div><div class="umb-table-cell"><strong>${s((P = (I = u.amount) == null ? void 0 : I.withVat) == null ? void 0 : P.currencyString)}</strong></div></div>`;
    }).join("")}
        </div>
        <div class="umb-table-footer">
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Sub Total (inc VAT)</div><div class="umb-table-cell">${s((a = (i = e.subTotal) == null ? void 0 : i.withVat) == null ? void 0 : a.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">-${s((l = e.discountAmount) == null ? void 0 : l.currencyString)}</div></div>
          ${e.shippingProvider ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Shipping Total</div><div class="umb-table-cell">${s((p = (c = e.shippingProvider.price) == null ? void 0 : c.withVat) == null ? void 0 : p.currencyString)}</div></div>` : ""}
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">${s((v = e.chargedVat) == null ? void 0 : v.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Total</div><div class="umb-table-cell"><strong>${s((y = e.chargedAmount) == null ? void 0 : y.currencyString)}</strong></div></div>
        </div>
      </div>
    `;
  }
  renderTracking(e) {
    const t = e.tracking;
    return re(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Tracking</h4><button class="btn-reset" type="button" data-action="toggle-tracking">${this.trackingExpanded ? "Hide" : "Show"}</button></div>${this.trackingExpanded ? ie(t) : ""}</div>` : '<div class="ekmOrderTracking"><h4>Tracking</h4><p>No tracking data was captured for this order.</p></div>';
  }
  renderConsent(e) {
    const t = e.consent;
    return se(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Consent</h4><button class="btn-reset" type="button" data-action="toggle-consent">${this.consentExpanded ? "Hide" : "Show"}</button></div>${this.consentExpanded ? `<p>Resolved: ${s(b(t.resolvedAtUtc))}</p><p>Source: ${s(t.source)}</p><p>Analytics: ${M(t.analytics)}</p><p>Marketing: ${M(t.marketing)}</p>` : ""}</div>` : '<div class="ekmOrderTracking"><h4>Consent</h4><p>No consent data was captured for this order.</p></div>';
  }
  renderActivityLogs() {
    return this.orderLogsLoading ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Loading activity...</p></div>' : this.orderLogs.length ? `<div class="ekmOrderActivityLog"><h4>Activity log</h4>${this.orderLogs.map((e) => `<div class="ekmOrderActivityLog__item"><div class="ekmOrderActivityLog__date">${s(b(e.date))}</div><div>${s(e.message)}</div></div>`).join("")}</div>` : '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>No activity yet.</p></div>';
  }
  renderOrderActions() {
    return !this.orderActions.length && !this.orderActionsLoading ? "" : `<div style="margin-top:15px;"><h4>Order Actions</h4><div style="display:flex; flex-wrap:wrap; gap:10px;">${this.orderActions.map((e) => `<button type="button" class="${e.look === "primary" ? "btn-success" : "btn-outline"}" data-action="execute-order-action" data-action-key="${s(e.key)}" ${e.enabled === !1 || this.executingActionKey === e.key ? "disabled" : ""}>${s(e.label)}</button>`).join("")}</div>${this.orderActionsLoading ? "<p>Loading actions...</p>" : ""}</div>`;
  }
  renderCustomerEditor() {
    const e = this.customerEditModel;
    return e ? `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel"><div class="ekmOverlay__header"><h3>Edit customer information</h3><button class="btn-reset" type="button" data-action="close-customer-editor" ${this.customerSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><div class="ekmSplit"><div class="ekmSplit__column"><h4>Billing</h4>${this.renderCustomerFields(e.customer, "customer")}</div><div class="ekmSplit__column"><h4>Shipping</h4>${this.renderCustomerFields(e.shipping, "shipping")}</div></div><div style="display:flex; justify-content:flex-end; gap:10px; padding-top:20px; border-top:1px solid #d8d7d9;"><button class="btn-outline" type="button" data-action="close-customer-editor" ${this.customerSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-customer-information" ${this.customerSaving ? "disabled" : ""}>${this.customerSaving ? "Saving..." : "Save customer information"}</button></div></div></div></div>` : "";
  }
  renderCustomerFields(e, t) {
    return e.map((i) => `<label class="control-group">${s(i.label)} ${i.isExtra ? `<small>(${s(i.key)})</small>` : ""}<input type="text" data-customer-group="${t}" data-customer-key="${s(i.key)}" value="${s(i.value)}" ${this.customerSaving ? "disabled" : ""}></label>`).join("");
  }
  renderOrderLineEditor() {
    const e = this.orderLineEditModel;
    return e ? `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel"><div class="ekmOverlay__header"><h3>Add order line</h3><button class="btn-reset" type="button" data-action="close-order-line-editor" ${this.orderLineSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><label class="control-group">Product ID<input type="text" data-order-line-field="productId" value="${s(e.productId)}" required ${this.orderLineSaving ? "disabled" : ""}></label><label class="control-group">Variant ID<input type="text" data-order-line-field="variantId" value="${s(e.variantId)}" ${this.orderLineSaving ? "disabled" : ""}></label><label class="control-group" style="padding-bottom:20px;">Quantity<input type="number" min="0.000001" step="any" data-order-line-field="quantity" value="${s(e.quantity)}" required ${this.orderLineSaving ? "disabled" : ""}></label><div style="display:flex; justify-content:flex-end; gap:10px; padding-top:20px; border-top:1px solid #d8d7d9;"><button class="btn-outline" type="button" data-action="close-order-line-editor" ${this.orderLineSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-order-line" ${this.orderLineSaving ? "disabled" : ""}>${this.orderLineSaving ? "Adding..." : "Add order line"}</button></div></div></div></div>` : "";
  }
  bindEvents() {
    this.querySelectorAll("[data-action]").forEach((e) => {
      e instanceof HTMLSelectElement ? e.addEventListener("change", (t) => void this.handleAction(t)) : e.addEventListener("click", (t) => void this.handleAction(t));
    }), this.querySelectorAll("[data-field]").forEach((e) => {
      e.addEventListener("change", (t) => void this.handleFieldChange(t)), e.dataset.field === "query" && e.addEventListener("input", (t) => this.handleSearchInput(t));
    });
  }
  async handleAction(e) {
    const t = e.currentTarget, i = t.dataset.action;
    if (i === "set-page") {
      this.page = Number(t.dataset.page || 1), await this.loadOrders();
      return;
    }
    if (i === "open-filter" || i === "open-export") {
      this.overlay = i === "open-filter" ? "filter" : "export", this.render();
      return;
    }
    if (i === "close-overlay") {
      this.closeOverlay();
      return;
    }
    if (i === "apply-filter") {
      this.applyFilterOverlay(), this.overlay = "", this.page = 1, await this.loadOrders();
      return;
    }
    if (i === "export-orders") {
      await this.exportOrders();
      return;
    }
    if (i === "view-order") {
      await this.openOrder(t.dataset.orderId || "");
      return;
    }
    if (i === "save-overlay-status") {
      await this.saveOverlayStatus();
      return;
    }
    if (i === "change-row-status") {
      await this.changeRowStatus(t);
      return;
    }
    if (i === "print-order") {
      window.print();
      return;
    }
    if (i === "toggle-tracking") {
      this.trackingExpanded = !this.trackingExpanded, this.renderPreservingOverlayScroll();
      return;
    }
    if (i === "toggle-consent") {
      this.consentExpanded = !this.consentExpanded, this.renderPreservingOverlayScroll();
      return;
    }
    if (i === "execute-order-action") {
      await this.executeOrderAction(t.dataset.actionKey || "");
      return;
    }
    if (i === "open-customer-editor") {
      this.openCustomerEditor();
      return;
    }
    if (i === "close-customer-editor") {
      this.customerEditorOpen = !1, this.customerEditModel = void 0, this.render();
      return;
    }
    if (i === "save-customer-information") {
      await this.saveCustomerInformation();
      return;
    }
    if (i === "open-order-line-editor") {
      this.orderLineEditModel = { productId: "", variantId: "", quantity: "1" }, this.orderLineEditorOpen = !0, this.render();
      return;
    }
    if (i === "close-order-line-editor") {
      this.orderLineSaving || (this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.render());
      return;
    }
    if (i === "save-order-line") {
      await this.saveOrderLine();
      return;
    }
    i === "remove-order-line" && await this.removeOrderLine(t.dataset.orderLineId || "", t.dataset.productTitle || "this order line");
  }
  async handleFieldChange(e) {
    const t = e.currentTarget, i = t.dataset.field;
    if (i === "includeOrderLines") {
      this.exportIncludeOrderLines = t.checked, this.render();
      return;
    }
    i === "orderStatusOverlay" || i === "notifyOrderStatus" || i === "query" || !i || !(i in o.filters) || (o.filters[i] = t.value, this.page = 1, i === "store" && await this.loadPaymentProviders(!0), await this.loadOrders());
  }
  handleSearchInput(e) {
    const t = e.currentTarget;
    o.filters.query = t.value, this.page = 1, window.clearTimeout(this.searchTimer), this.searchTimer = window.setTimeout(() => void this.loadOrders(), 700);
  }
  applyFilterOverlay() {
    this.querySelectorAll("[data-filter-field]").forEach((e) => {
      const t = e.dataset.filterField;
      t && t in o.filters && (o.filters[t] = e.value);
    });
  }
  async exportOrders() {
    if (this.result.count) {
      this.exporting = !0, this.render();
      try {
        const e = await this.api.exportOrders(o.filters, this.result.count, this.exportIncludeOrderLines);
        Z(e, this.exportIncludeOrderLines ? "orders-with-orderlines.csv" : "orders.csv"), this.overlay = "";
      } catch (e) {
        this.showError(h(e, "Error exporting orders."));
      } finally {
        this.exporting = !1, this.render();
      }
    }
  }
  async openOrder(e) {
    if (e)
      try {
        this.selectedOrder = await this.api.orderInfo(e), this.overlay = "order", this.trackingExpanded = !1, this.consentExpanded = !1, this.render(), await Promise.all([this.loadOrderLogs(e), this.loadOrderActions(e)]);
      } catch (t) {
        this.showError(h(t, "Error on getting orderInfo."));
      }
  }
  async loadOrderLogs(e) {
    this.orderLogsLoading = !0, this.render();
    try {
      this.orderLogs = await this.api.orderLogs(e);
    } catch {
      this.orderLogs = [];
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
    var i, a, l;
    if (!((i = this.selectedOrder) != null && i.uniqueId))
      return;
    const e = ((a = this.querySelector('[data-field="orderStatusOverlay"]')) == null ? void 0 : a.value) || "", t = ((l = this.querySelector('[data-field="notifyOrderStatus"]')) == null ? void 0 : l.checked) || !1;
    try {
      await this.api.changeOrderStatus(this.selectedOrder.uniqueId, e, t), this.selectedOrder.orderStatus = e, this.showSuccess("Order status updated."), await this.loadOrders(), await this.loadOrderLogs(this.selectedOrder.uniqueId);
    } catch (c) {
      this.showError(h(c, "Error updating order status."));
    }
  }
  showSuccess(e) {
    this.showNotification("positive", "Success", e);
  }
  showError(e) {
    this.showNotification("danger", "Error", e);
  }
  showNotification(e, t, i) {
    if (this.notificationContext) {
      this.notificationContext.peek(e, {
        data: {
          headline: t,
          message: i
        }
      });
      return;
    }
    e === "danger" && console.error(`${t}: ${i}`);
  }
  renderPreservingOverlayScroll() {
    const e = this.querySelector(".ekmOverlay"), t = (e == null ? void 0 : e.scrollTop) ?? 0;
    this.render(), requestAnimationFrame(() => {
      const i = this.querySelector(".ekmOverlay");
      i && (i.scrollTop = t);
    });
  }
  closeOverlay() {
    this.overlay = "", this.selectedOrder = void 0, this.customerEditorOpen = !1, this.customerEditModel = void 0, this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.render();
  }
  getOrderStatusValue(e) {
    const t = String(e ?? ""), i = o.statusList.find((a) => String(a.value ?? "") === t || String(a.enumValue ?? "") === t);
    return i ? f(i) : t;
  }
  async changeRowStatus(e) {
    const t = e.dataset.orderId || "";
    if (t)
      try {
        await this.api.changeOrderStatus(t, e.value, !0);
        const i = this.result.orders.find((a) => a.uniqueId === t);
        i && (i.orderStatusCol = e.value), this.showSuccess("Order status updated.");
      } catch (i) {
        this.showError(h(i, "Error updating order status.")), await this.loadOrders();
      }
  }
  async executeOrderAction(e) {
    var i;
    if (!((i = this.selectedOrder) != null && i.uniqueId) || !e || this.executingActionKey)
      return;
    const t = this.orderActions.find((a) => a.key === e);
    if (!(t != null && t.confirmMessage && !window.confirm(t.confirmMessage))) {
      this.executingActionKey = e, this.render();
      try {
        const a = await this.api.executeOrderAction(this.selectedOrder.uniqueId, e), l = await a.blob(), c = a.headers.get("content-disposition") || "", p = a.headers.get("content-type") || "";
        if (c.toLowerCase().includes("filename=") || p.startsWith("application/pdf") || p.startsWith("application/octet-stream") || p.startsWith("image/"))
          window.open(URL.createObjectURL(l), "_blank");
        else {
          const v = await l.text();
          this.showSuccess(ae(v));
        }
        this.selectedOrder = await this.api.orderInfo(this.selectedOrder.uniqueId), await this.loadOrderLogs(this.selectedOrder.uniqueId), await this.loadOrderActions(this.selectedOrder.uniqueId);
      } catch (a) {
        this.showError(h(a, "Order action failed."));
      } finally {
        this.executingActionKey = "", this.render();
      }
    }
  }
  openCustomerEditor() {
    var a, l;
    const e = this.selectedOrder;
    if (!e)
      return;
    const t = ((a = e.customerInformation) == null ? void 0 : a.customer) || {}, i = ((l = e.customerInformation) == null ? void 0 : l.shipping) || {};
    this.customerEditModel = {
      customer: q(t, W).concat(F(t.properties, "customer")),
      shipping: q(i, Q).concat(F(i.properties, "shipping"))
    }, this.customerEditorOpen = !0, this.render();
  }
  async saveCustomerInformation() {
    var e;
    if (!(!((e = this.selectedOrder) != null && e.uniqueId) || !this.customerEditModel || this.customerSaving)) {
      this.querySelectorAll("[data-customer-group]").forEach((t) => {
        var c;
        const i = t.dataset.customerGroup, a = t.dataset.customerKey, l = (c = this.customerEditModel) == null ? void 0 : c[i].find((p) => p.key === a);
        l && (l.value = t.value);
      }), this.customerSaving = !0, this.render();
      try {
        this.selectedOrder = await this.api.updateCustomerInformation(
          this.selectedOrder.uniqueId,
          D(this.customerEditModel.customer),
          D(this.customerEditModel.shipping)
        ), this.customerEditorOpen = !1, this.customerEditModel = void 0, this.showSuccess("Customer information updated."), await this.loadOrders();
      } catch (t) {
        this.showError(h(t, "Error updating customer information."));
      } finally {
        this.customerSaving = !1, this.render();
      }
    }
  }
  async saveOrderLine() {
    var l;
    if (!((l = this.selectedOrder) != null && l.uniqueId) || !this.orderLineEditModel || this.orderLineSaving)
      return;
    this.querySelectorAll("[data-order-line-field]").forEach((c) => {
      const p = c.dataset.orderLineField;
      p && (this.orderLineEditModel[p] = c.value);
    });
    const { productId: e, variantId: t, quantity: i } = this.orderLineEditModel, a = Number(i);
    if (!e.trim() || !Number.isFinite(a) || a <= 0) {
      this.showError("Product ID and a positive quantity are required.");
      return;
    }
    this.orderLineSaving = !0, this.render();
    try {
      this.selectedOrder = await this.api.addOrderLine(this.selectedOrder.uniqueId, e.trim(), t.trim() || void 0, a), this.orderLineEditorOpen = !1, this.orderLineEditModel = void 0, this.showSuccess("Order line added."), await this.refreshOrderAfterLineChange();
    } catch (c) {
      this.showError(h(c, "Error adding order line."));
    } finally {
      this.orderLineSaving = !1, this.render();
    }
  }
  async removeOrderLine(e, t) {
    var i;
    if (!(!((i = this.selectedOrder) != null && i.uniqueId) || !e || this.removingOrderLineId || !window.confirm(`Remove ${t} from this order?`))) {
      this.removingOrderLineId = e, this.render();
      try {
        this.selectedOrder = await this.api.removeOrderLine(this.selectedOrder.uniqueId, e), this.showSuccess("Order line removed."), await this.refreshOrderAfterLineChange();
      } catch (a) {
        this.showError(h(a, "Error removing order line."));
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
function h(r, d) {
  return r instanceof Error ? r.message : d;
}
function Y(r) {
  return r === "zipCode" ? "Zipcode" : r.charAt(0).toUpperCase() + r.slice(1);
}
function J(r) {
  return (/* @__PURE__ */ new Set(["shippingname", "shippingaddress", "shippingcity", "shippingcountry", "shippingemail", "shippingapartment", "shippingzipcode", "shippingphone", "customeremail", "customername", "customeraddress", "customerapartment", "customercity", "customercountry", "customerzipcode", "customerphone"])).has(r.toLowerCase());
}
function N(r) {
  return r.replace(/^customshipping/i, "").replace(/^custompayment/i, "").replace(/^shipping/i, "").replace(/^customer/i, "");
}
function j(r, d) {
  return Object.entries(r || {}).filter(([e, t]) => !!t && e.toLowerCase().startsWith(d) && !J(e)).map(([e, t]) => [e, g(t)]);
}
function X(r) {
  return !!(r != null && r.name || r != null && r.email || r != null && r.address || r != null && r.apartment || r != null && r.city || r != null && r.country || r != null && r.zipCode || r != null && r.phone);
}
function ee(r) {
  var e;
  const d = ((e = r.orderLineInfo) == null ? void 0 : e.properties) || {};
  return Object.entries(d).filter(([, t]) => !!t).map(([t, i]) => `<small style="display:block; margin-top:3px;"><strong>${s(te(t))}</strong>: ${s(g(i))}</small>`).join("");
}
function te(r) {
  const d = r.replace(/^orderline/i, "").replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim();
  return d ? d.charAt(0).toUpperCase() + d.slice(1) : r;
}
function re(r) {
  var d, e, t, i;
  return !!(r && (r.source || r.medium || r.campaign || r.term || r.content || r.clickId || r.clickIdType || r.landingUrl || r.referrer || r.captureMethod || r.capturedAtUtc || r.hasCookieSupport !== null && r.hasCookieSupport !== void 0 || (d = r.ga4) != null && d.clientId || (e = r.ga4) != null && e.sessionId || (t = r.meta) != null && t.fbp || (i = r.meta) != null && i.fbc));
}
function ie(r) {
  var t, i, a, l, c, p;
  const d = Object.entries(((t = r.ga4) == null ? void 0 : t.data) || {}), e = Object.entries(((i = r.meta) == null ? void 0 : i.data) || {});
  return `<div class="ekmSplit"><div class="ekmSplit__column">${m("Captured", b(r.capturedAtUtc))}${m("Capture method", r.captureMethod)}${r.hasCookieSupport !== null && r.hasCookieSupport !== void 0 ? `<p>Cookie support: ${r.hasCookieSupport ? "Yes" : "No"}</p>` : ""}${m("Source", r.source)}${m("Medium", r.medium)}${m("Campaign", r.campaign)}${m("Term", r.term)}${m("Content", r.content)}${m("Click ID", r.clickId)}${m("Click ID Type", r.clickIdType)}${m("Landing URL", r.landingUrl)}${m("Referrer", r.referrer)}</div><div class="ekmSplit__column"><h5>GA4</h5>${m("Client ID", (a = r.ga4) == null ? void 0 : a.clientId)}${m("Session ID", (l = r.ga4) == null ? void 0 : l.sessionId)}${T(d)}<h5>Meta</h5>${m("FBP", (c = r.meta) == null ? void 0 : c.fbp)}${m("FBC", (p = r.meta) == null ? void 0 : p.fbc)}${T(e)}</div></div>`;
}
function m(r, d) {
  return d ? `<p class="ekmOrderTracking__wrap">${s(r)}: ${s(d)}</p>` : "";
}
function T(r) {
  return r.length ? `<ul>${r.map(([d, e]) => `<li><strong>${s(d)}</strong>: ${s(e)}</li>`).join("")}</ul>` : "";
}
function se(r) {
  return !!(r && (r.resolvedAtUtc || r.source || r.analytics !== null && r.analytics !== void 0 || r.marketing !== null && r.marketing !== void 0));
}
function M(r) {
  return r === !0 ? "Yes" : r === !1 ? "No" : "Unknown";
}
function q(r, d) {
  return d.map((e) => {
    var t;
    return {
      key: e.key,
      label: e.label,
      value: g(r[e.property] || ((t = r.properties) == null ? void 0 : t[e.key]) || ""),
      isExtra: !1
    };
  });
}
function F(r, d) {
  return j(r, d).map(([e, t]) => ({
    key: e,
    label: N(e).replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim() || e,
    value: t,
    isExtra: !0
  }));
}
function D(r) {
  return Object.fromEntries(r.map((d) => [d.key, d.value || ""]));
}
function ae(r) {
  try {
    return JSON.parse(r).message || r;
  } catch {
    return r;
  }
}
customElements.define("ekom-orders-section-view", G);
export {
  G as EkomOrdersSectionViewElement,
  G as default
};
