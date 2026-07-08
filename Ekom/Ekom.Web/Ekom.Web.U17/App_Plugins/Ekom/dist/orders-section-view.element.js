var N = Object.defineProperty;
var j = (r, a, e) => a in r ? N(r, a, { enumerable: !0, configurable: !0, writable: !0, value: e }) : r[a] = e;
var n = (r, a, e) => j(r, typeof a != "symbol" ? a + "" : a, e);
import { UmbElementMixin as V } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as U } from "@umbraco-cms/backoffice/notification";
import { E as B, m as l, a as K, e as i, g, f as b, p as z, d as f, b as R } from "./manager-shared.js";
const H = [
  { key: "customerName", label: "Name", property: "name" },
  { key: "customerEmail", label: "Email", property: "email" },
  { key: "customerAddress", label: "Address", property: "address" },
  { key: "customerApartment", label: "Apartment", property: "apartment" },
  { key: "customerCity", label: "City", property: "city" },
  { key: "customerCountry", label: "Country", property: "country" },
  { key: "customerZipCode", label: "Zipcode", property: "zipCode" },
  { key: "customerPhone", label: "Phone", property: "phone" }
], Z = [
  { key: "shippingName", label: "Name", property: "name" },
  { key: "shippingEmail", label: "Email", property: "email" },
  { key: "shippingAddress", label: "Address", property: "address" },
  { key: "shippingApartment", label: "Apartment", property: "apartment" },
  { key: "shippingCity", label: "City", property: "city" },
  { key: "shippingCountry", label: "Country", property: "country" },
  { key: "shippingZipCode", label: "Zipcode", property: "zipCode" },
  { key: "shippingPhone", label: "Phone", property: "phone" }
];
class W extends V(HTMLElement) {
  constructor() {
    super(...arguments);
    n(this, "api", new B());
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
        this.exporting || this.closeOverlay();
      }
    });
  }
  connectedCallback() {
    super.connectedCallback(), this.consumeContext(U, (e) => {
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
      this.error = h(e, "Error loading Ekom Manager."), this.loading = !1, this.render();
    }
  }
  async loadPaymentProviders(e = !1) {
    if (!l.filters.store) {
      l.paymentProviders = [];
      return;
    }
    l.paymentProviders = await this.api.paymentProviders(l.filters.store), e && (l.filters.paymentProvider = ""), l.filters.paymentProvider && (l.paymentProviders.some((s) => s.key === l.filters.paymentProvider) || (l.filters.paymentProvider = ""));
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
      this.error = h(e, "Error searching orders.");
    } finally {
      this.loading = !1, this.render();
    }
  }
  render() {
    this.innerHTML = `
      <style>${K}</style>
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
          <strong class="ekmSummaryCard__value">${i(this.result.count || 0)}</strong>
        </div>
        <div class="card ekmSummaryCard">
          <span class="ekmSummaryCard__label">Payments total</span>
          <strong class="ekmSummaryCard__value">${i(this.result.grandTotal || 0)}</strong>
        </div>
        <div class="card ekmSummaryCard">
          <span class="ekmSummaryCard__label">Average order amount</span>
          <strong class="ekmSummaryCard__value">${i(this.result.averageAmount || 0)}</strong>
        </div>
      </div>
      ${this.renderToolbar()}
      ${this.error ? `<p class="status status--error">${i(this.error)}</p>` : ""}
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
      const s = g(t);
      return `<option value="${i(s)}" ${e.orderStatus === s ? "selected" : ""}>${i(t.label)}</option>`;
    }).join("")}
            </select>
          </label>
          <label class="ekmManager__filter">Date From:
            <input type="date" data-field="dateFrom" value="${i(e.dateFrom)}">
          </label>
          <label class="ekmManager__filter">Date To:
            <input type="date" data-field="dateTo" value="${i(e.dateTo)}">
          </label>
          <label class="ekmManager__filter">Store:
            <select data-field="store">
              ${l.stores.map((t) => `<option value="${i(t.alias)}" ${e.store === t.alias ? "selected" : ""}>${i(t.title)}</option>`).join("")}
            </select>
          </label>
          <div class="ekmManager__search">
            <button type="button" class="btn-outline" data-action="open-export">Export</button>
            <button type="button" class="btn-primary" data-action="open-filter">Filter</button>
            <div class="form-search"><input type="text" data-field="query" value="${i(e.query)}" placeholder="Type to search..."></div>
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
        <div class="umb-table-cell not-fixed" data-label="Action"><button class="btn-success" type="button" data-action="view-order" data-order-id="${i(e.uniqueId)}">View</button></div>
        <div class="umb-table-cell" data-label="Order Number" title="${i(e.uniqueId)}">${i(e.referenceId)}</div>
        <div class="umb-table-cell" data-label="Status">
          <select data-action="change-row-status" data-order-id="${i(e.uniqueId)}">
            ${l.statusList.map((t) => {
      const s = g(t);
      return `<option value="${i(s)}" ${e.orderStatusCol === s ? "selected" : ""}>${i(t.label)}</option>`;
    }).join("")}
          </select>
        </div>
        <div class="umb-table-cell" data-label="Name">${i(e.customerName)}</div>
        <div class="umb-table-cell" data-label="Store">${i(e.storeAlias)}</div>
        <div class="umb-table-cell" data-label="Created">${i(b(e.createDate))}</div>
        <div class="umb-table-cell" data-label="Payment">${i(e.formattedTotal)}</div>
      </div>
    `;
  }
  renderPagination() {
    return `
      <div class="pagination">
        <ul>
          ${z(this.page, this.result.totalPages).map((e) => {
      const t = Number(String(e).replace("...", ""));
      return `<li class="${t === this.page ? "active" : ""}"><button type="button" data-action="set-page" data-page="${t}" ${t === this.page ? "disabled" : ""}>${i(e)}</button></li>`;
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
                ${l.paymentProviders.map((t) => `<option value="${i(t.key)}" ${e.paymentProvider === t.key ? "selected" : ""}>${i(t.title)}</option>`).join("")}
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
  renderFilterInput(e, t, s) {
    return `
      <label class="control-group">${i(t)}
        <input type="text" data-filter-field="${i(e)}" value="${i(l.filters[e])}" placeholder="${i(s)}">
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
    `;
  }
  renderOrderDetails(e) {
    var d, c, m, v;
    const t = ((d = e.customerInformation) == null ? void 0 : d.customer) || {}, s = ((c = e.customerInformation) == null ? void 0 : c.shipping) || {}, o = this.getOrderStatusValue(e.orderStatus);
    return `
      <div class="ekmOrder__header">
        <h1>Order number: ${i(e.referenceId)}</h1>
        <div class="ekmOrderStatusBar">
          <label class="ekmOrderStatusBar__status">Order Status:
            <select data-field="orderStatusOverlay">
              ${l.statusList.map((y) => {
      const u = g(y);
      return `<option value="${i(u)}" ${o === u ? "selected" : ""}>${i(y.label)}</option>`;
    }).join("")}
            </select>
          </label>
          <label class="ekmCheckboxLabel"><input type="checkbox" data-field="notifyOrderStatus"> Fire events?</label>
          <button type="button" class="btn-success" data-action="save-overlay-status">Save</button>
          <button type="button" class="btn-outline ekmOrderStatusBar__print" data-action="print-order">Print</button>
        </div>
        <p>UniqueId: ${i(e.uniqueId)}</p>
        <p>Created date: ${i(b(e.createDate))}</p>
        <p>Paid date: ${i(b(e.paidDate))}</p>
        <p>Store: ${i(((m = e.storeInfo) == null ? void 0 : m.alias) || e.storeAlias)}</p>
        <p>Payment: <strong>${i((v = e.chargedAmount) == null ? void 0 : v.currencyString)}</strong></p>
        ${this.renderOrderActions()}
      </div>
      <div class="ekmSplit">
        <div class="ekmSplit__column"><h4>Billing</h4><button type="button" class="btn-outline" data-action="open-customer-editor" style="margin-bottom:10px;">Edit customer information</button>${this.renderAddress(t)}${this.renderExtraProperties(t.properties, "customer")}</div>
        <div class="ekmSplit__column"><h4>Shipping</h4>${J(s) ? `${this.renderAddress(s)}${this.renderExtraProperties(s.properties, "shipping")}` : '<p style="font-weight:bold">Same as billing address</p>'}</div>
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
    return ["name", "email", "address", "apartment", "city", "country", "zipCode", "phone"].filter((t) => e[t]).map((t) => `<p>${G(t)}: ${i(f(e[t]))}</p>`).join("");
  }
  renderExtraProperties(e, t) {
    const s = D(e, t);
    return s.length ? `<h5 style="margin-top:20px; font-weight:bold;">Extra ${t === "shipping" ? "Shipping" : "Customer"} Data</h5><ul>${s.map(([o, d]) => `<li><strong>${i(q(o))}</strong>: ${i(d)}</li>`).join("")}</ul>` : "";
  }
  renderProvider(e, t, s) {
    var o;
    return t ? `<h4>${i(e)}</h4><h4><strong>${i(t.title)}</strong></h4>${t.price ? `<p>Price: ${i((o = t.price.withVat) == null ? void 0 : o.currencyString)}</p>` : ""}${this.renderExtraProperties(t.customData, s)}` : "";
  }
  renderOrderLines(e) {
    var s, o, d, c, m, v, y;
    return `
      <h4>Order Details</h4>
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell not-fixed">Product</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Unit Price (inc VAT)</div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">Total (inc VAT)</div></div></div>
        <div class="umb-table-body">
          ${(Array.isArray(e.orderLines) ? e.orderLines : []).map((u) => {
      var $, O, k, S, w, x, E, _, C, A, L;
      return `<div class="umb-table-row"><div class="umb-table-cell not-fixed">${i(($ = u.product) == null ? void 0 : $.title)} (${i((O = u.product) == null ? void 0 : O.sku)})${u.variant ? `<small style="display:block; margin-top:3px;">${i(u.variant.title)} ${u.variant.sku ? `(${i(u.variant.sku)})` : ""}</small>` : ""}${Q(u)}</div><div class="umb-table-cell">${i(u.quantity)}</div><div class="umb-table-cell">${i((w = (S = (k = u.product) == null ? void 0 : k.price) == null ? void 0 : S.withVat) == null ? void 0 : w.currencyString)}</div><div class="umb-table-cell">${i((E = (x = u.amount) == null ? void 0 : x.vat) == null ? void 0 : E.currencyString)}</div><div class="umb-table-cell">-${i((C = (_ = u.amount) == null ? void 0 : _.discountAmount) == null ? void 0 : C.currencyString)}</div><div class="umb-table-cell"><strong>${i((L = (A = u.amount) == null ? void 0 : A.withVat) == null ? void 0 : L.currencyString)}</strong></div></div>`;
    }).join("")}
        </div>
        <div class="umb-table-footer">
          <div class="umb-table-row"><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Sub Total (inc VAT)</div><div class="umb-table-cell">${i((o = (s = e.subTotal) == null ? void 0 : s.withVat) == null ? void 0 : o.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">-${i((d = e.discountAmount) == null ? void 0 : d.currencyString)}</div></div>
          ${e.shippingProvider ? `<div class="umb-table-row"><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Shipping Total</div><div class="umb-table-cell">${i((m = (c = e.shippingProvider.price) == null ? void 0 : c.withVat) == null ? void 0 : m.currencyString)}</div></div>` : ""}
          <div class="umb-table-row"><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">${i((v = e.chargedVat) == null ? void 0 : v.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Total</div><div class="umb-table-cell"><strong>${i((y = e.chargedAmount) == null ? void 0 : y.currencyString)}</strong></div></div>
        </div>
      </div>
    `;
  }
  renderTracking(e) {
    const t = e.tracking;
    return ee(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Tracking</h4><button class="btn-reset" type="button" data-action="toggle-tracking">${this.trackingExpanded ? "Hide" : "Show"}</button></div>${this.trackingExpanded ? te(t) : ""}</div>` : '<div class="ekmOrderTracking"><h4>Tracking</h4><p>No tracking data was captured for this order.</p></div>';
  }
  renderConsent(e) {
    const t = e.consent;
    return re(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Consent</h4><button class="btn-reset" type="button" data-action="toggle-consent">${this.consentExpanded ? "Hide" : "Show"}</button></div>${this.consentExpanded ? `<p>Resolved: ${i(b(t.resolvedAtUtc))}</p><p>Source: ${i(t.source)}</p><p>Analytics: ${T(t.analytics)}</p><p>Marketing: ${T(t.marketing)}</p>` : ""}</div>` : '<div class="ekmOrderTracking"><h4>Consent</h4><p>No consent data was captured for this order.</p></div>';
  }
  renderActivityLogs() {
    return this.orderLogsLoading ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Loading activity...</p></div>' : this.orderLogs.length ? `<div class="ekmOrderActivityLog"><h4>Activity log</h4>${this.orderLogs.map((e) => `<div class="ekmOrderActivityLog__item"><div class="ekmOrderActivityLog__date">${i(b(e.date))}</div><div>${i(e.message)}</div></div>`).join("")}</div>` : '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>No activity yet.</p></div>';
  }
  renderOrderActions() {
    return !this.orderActions.length && !this.orderActionsLoading ? "" : `<div style="margin-top:15px;"><h4>Order Actions</h4><div style="display:flex; flex-wrap:wrap; gap:10px;">${this.orderActions.map((e) => `<button type="button" class="${e.look === "primary" ? "btn-success" : "btn-outline"}" data-action="execute-order-action" data-action-key="${i(e.key)}" ${e.enabled === !1 || this.executingActionKey === e.key ? "disabled" : ""}>${i(e.label)}</button>`).join("")}</div>${this.orderActionsLoading ? "<p>Loading actions...</p>" : ""}</div>`;
  }
  renderCustomerEditor() {
    const e = this.customerEditModel;
    return e ? `<div class="ekmCustomerInformationModal"><div class="ekmCustomerInformationModal__panel"><div class="ekmOverlay__header"><h3>Edit customer information</h3><button class="btn-reset" type="button" data-action="close-customer-editor" ${this.customerSaving ? "disabled" : ""}>&times;</button></div><div class="ekmOverlay__content"><div class="ekmSplit"><div class="ekmSplit__column"><h4>Billing</h4>${this.renderCustomerFields(e.customer, "customer")}</div><div class="ekmSplit__column"><h4>Shipping</h4>${this.renderCustomerFields(e.shipping, "shipping")}</div></div><div style="display:flex; justify-content:flex-end; gap:10px; padding-top:20px; border-top:1px solid #d8d7d9;"><button class="btn-outline" type="button" data-action="close-customer-editor" ${this.customerSaving ? "disabled" : ""}>Cancel</button><button class="btn-success" type="button" data-action="save-customer-information" ${this.customerSaving ? "disabled" : ""}>${this.customerSaving ? "Saving..." : "Save customer information"}</button></div></div></div></div>` : "";
  }
  renderCustomerFields(e, t) {
    return e.map((s) => `<label class="control-group">${i(s.label)} ${s.isExtra ? `<small>(${i(s.key)})</small>` : ""}<input type="text" data-customer-group="${t}" data-customer-key="${i(s.key)}" value="${i(s.value)}" ${this.customerSaving ? "disabled" : ""}></label>`).join("");
  }
  bindEvents() {
    this.querySelectorAll("[data-action]").forEach((e) => {
      e instanceof HTMLSelectElement ? e.addEventListener("change", (t) => void this.handleAction(t)) : e.addEventListener("click", (t) => void this.handleAction(t));
    }), this.querySelectorAll("[data-field]").forEach((e) => {
      e.addEventListener("change", (t) => void this.handleFieldChange(t)), e.dataset.field === "query" && e.addEventListener("input", (t) => this.handleSearchInput(t));
    });
  }
  async handleAction(e) {
    const t = e.currentTarget, s = t.dataset.action;
    if (s === "set-page") {
      this.page = Number(t.dataset.page || 1), await this.loadOrders();
      return;
    }
    if (s === "open-filter" || s === "open-export") {
      this.overlay = s === "open-filter" ? "filter" : "export", this.render();
      return;
    }
    if (s === "close-overlay") {
      this.closeOverlay();
      return;
    }
    if (s === "apply-filter") {
      this.applyFilterOverlay(), this.overlay = "", this.page = 1, await this.loadOrders();
      return;
    }
    if (s === "export-orders") {
      await this.exportOrders();
      return;
    }
    if (s === "view-order") {
      await this.openOrder(t.dataset.orderId || "");
      return;
    }
    if (s === "save-overlay-status") {
      await this.saveOverlayStatus();
      return;
    }
    if (s === "change-row-status") {
      await this.changeRowStatus(t);
      return;
    }
    if (s === "print-order") {
      window.print();
      return;
    }
    if (s === "toggle-tracking") {
      this.trackingExpanded = !this.trackingExpanded, this.renderPreservingOverlayScroll();
      return;
    }
    if (s === "toggle-consent") {
      this.consentExpanded = !this.consentExpanded, this.renderPreservingOverlayScroll();
      return;
    }
    if (s === "execute-order-action") {
      await this.executeOrderAction(t.dataset.actionKey || "");
      return;
    }
    if (s === "open-customer-editor") {
      this.openCustomerEditor();
      return;
    }
    if (s === "close-customer-editor") {
      this.customerEditorOpen = !1, this.customerEditModel = void 0, this.render();
      return;
    }
    s === "save-customer-information" && await this.saveCustomerInformation();
  }
  async handleFieldChange(e) {
    const t = e.currentTarget, s = t.dataset.field;
    if (s === "includeOrderLines") {
      this.exportIncludeOrderLines = t.checked, this.render();
      return;
    }
    s === "orderStatusOverlay" || s === "notifyOrderStatus" || s === "query" || !s || !(s in l.filters) || (l.filters[s] = t.value, this.page = 1, s === "store" && await this.loadPaymentProviders(!0), await this.loadOrders());
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
        R(e, this.exportIncludeOrderLines ? "orders-with-orderlines.csv" : "orders.csv"), this.overlay = "";
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
    var s, o, d;
    if (!((s = this.selectedOrder) != null && s.uniqueId))
      return;
    const e = ((o = this.querySelector('[data-field="orderStatusOverlay"]')) == null ? void 0 : o.value) || "", t = ((d = this.querySelector('[data-field="notifyOrderStatus"]')) == null ? void 0 : d.checked) || !1;
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
  showNotification(e, t, s) {
    if (this.notificationContext) {
      this.notificationContext.peek(e, {
        data: {
          headline: t,
          message: s
        }
      });
      return;
    }
    e === "danger" && console.error(`${t}: ${s}`);
  }
  renderPreservingOverlayScroll() {
    const e = this.querySelector(".ekmOverlay"), t = (e == null ? void 0 : e.scrollTop) ?? 0;
    this.render(), requestAnimationFrame(() => {
      const s = this.querySelector(".ekmOverlay");
      s && (s.scrollTop = t);
    });
  }
  closeOverlay() {
    this.overlay = "", this.selectedOrder = void 0, this.customerEditorOpen = !1, this.customerEditModel = void 0, this.render();
  }
  getOrderStatusValue(e) {
    const t = String(e ?? ""), s = l.statusList.find((o) => String(o.value ?? "") === t || String(o.enumValue ?? "") === t);
    return s ? g(s) : t;
  }
  async changeRowStatus(e) {
    const t = e.dataset.orderId || "";
    if (t)
      try {
        await this.api.changeOrderStatus(t, e.value, !0);
        const s = this.result.orders.find((o) => o.uniqueId === t);
        s && (s.orderStatusCol = e.value), this.showSuccess("Order status updated.");
      } catch (s) {
        this.showError(h(s, "Error updating order status.")), await this.loadOrders();
      }
  }
  async executeOrderAction(e) {
    var s;
    if (!((s = this.selectedOrder) != null && s.uniqueId) || !e || this.executingActionKey)
      return;
    const t = this.orderActions.find((o) => o.key === e);
    if (!(t != null && t.confirmMessage && !window.confirm(t.confirmMessage))) {
      this.executingActionKey = e, this.render();
      try {
        const o = await this.api.executeOrderAction(this.selectedOrder.uniqueId, e), d = await o.blob(), c = o.headers.get("content-disposition") || "", m = o.headers.get("content-type") || "";
        if (c.toLowerCase().includes("filename=") || m.startsWith("application/pdf") || m.startsWith("application/octet-stream") || m.startsWith("image/"))
          window.open(URL.createObjectURL(d), "_blank");
        else {
          const v = await d.text();
          this.showSuccess(se(v));
        }
        this.selectedOrder = await this.api.orderInfo(this.selectedOrder.uniqueId), await this.loadOrderLogs(this.selectedOrder.uniqueId), await this.loadOrderActions(this.selectedOrder.uniqueId);
      } catch (o) {
        this.showError(h(o, "Order action failed."));
      } finally {
        this.executingActionKey = "", this.render();
      }
    }
  }
  openCustomerEditor() {
    var o, d;
    const e = this.selectedOrder;
    if (!e)
      return;
    const t = ((o = e.customerInformation) == null ? void 0 : o.customer) || {}, s = ((d = e.customerInformation) == null ? void 0 : d.shipping) || {};
    this.customerEditModel = {
      customer: P(t, H).concat(M(t.properties, "customer")),
      shipping: P(s, Z).concat(M(s.properties, "shipping"))
    }, this.customerEditorOpen = !0, this.render();
  }
  async saveCustomerInformation() {
    var e;
    if (!(!((e = this.selectedOrder) != null && e.uniqueId) || !this.customerEditModel || this.customerSaving)) {
      this.querySelectorAll("[data-customer-group]").forEach((t) => {
        var c;
        const s = t.dataset.customerGroup, o = t.dataset.customerKey, d = (c = this.customerEditModel) == null ? void 0 : c[s].find((m) => m.key === o);
        d && (d.value = t.value);
      }), this.customerSaving = !0, this.render();
      try {
        this.selectedOrder = await this.api.updateCustomerInformation(
          this.selectedOrder.uniqueId,
          F(this.customerEditModel.customer),
          F(this.customerEditModel.shipping)
        ), this.customerEditorOpen = !1, this.customerEditModel = void 0, this.showSuccess("Customer information updated."), await this.loadOrders();
      } catch (t) {
        this.showError(h(t, "Error updating customer information."));
      } finally {
        this.customerSaving = !1, this.render();
      }
    }
  }
}
function h(r, a) {
  return r instanceof Error ? r.message : a;
}
function G(r) {
  return r === "zipCode" ? "Zipcode" : r.charAt(0).toUpperCase() + r.slice(1);
}
function Y(r) {
  return (/* @__PURE__ */ new Set(["shippingname", "shippingaddress", "shippingcity", "shippingcountry", "shippingemail", "shippingapartment", "shippingzipcode", "shippingphone", "customeremail", "customername", "customeraddress", "customerapartment", "customercity", "customercountry", "customerzipcode", "customerphone"])).has(r.toLowerCase());
}
function q(r) {
  return r.replace(/^customshipping/i, "").replace(/^custompayment/i, "").replace(/^shipping/i, "").replace(/^customer/i, "");
}
function D(r, a) {
  return Object.entries(r || {}).filter(([e, t]) => !!t && e.toLowerCase().startsWith(a) && !Y(e)).map(([e, t]) => [e, f(t)]);
}
function J(r) {
  return !!(r != null && r.name || r != null && r.email || r != null && r.address || r != null && r.apartment || r != null && r.city || r != null && r.country || r != null && r.zipCode || r != null && r.phone);
}
function Q(r) {
  var e;
  const a = ((e = r.orderLineInfo) == null ? void 0 : e.properties) || {};
  return Object.entries(a).filter(([, t]) => !!t).map(([t, s]) => `<small style="display:block; margin-top:3px;"><strong>${i(X(t))}</strong>: ${i(f(s))}</small>`).join("");
}
function X(r) {
  const a = r.replace(/^orderline/i, "").replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim();
  return a ? a.charAt(0).toUpperCase() + a.slice(1) : r;
}
function ee(r) {
  var a, e, t, s;
  return !!(r && (r.source || r.medium || r.campaign || r.term || r.content || r.clickId || r.clickIdType || r.landingUrl || r.referrer || r.captureMethod || r.capturedAtUtc || r.hasCookieSupport !== null && r.hasCookieSupport !== void 0 || (a = r.ga4) != null && a.clientId || (e = r.ga4) != null && e.sessionId || (t = r.meta) != null && t.fbp || (s = r.meta) != null && s.fbc));
}
function te(r) {
  var t, s, o, d, c, m;
  const a = Object.entries(((t = r.ga4) == null ? void 0 : t.data) || {}), e = Object.entries(((s = r.meta) == null ? void 0 : s.data) || {});
  return `<div class="ekmSplit"><div class="ekmSplit__column">${p("Captured", b(r.capturedAtUtc))}${p("Capture method", r.captureMethod)}${r.hasCookieSupport !== null && r.hasCookieSupport !== void 0 ? `<p>Cookie support: ${r.hasCookieSupport ? "Yes" : "No"}</p>` : ""}${p("Source", r.source)}${p("Medium", r.medium)}${p("Campaign", r.campaign)}${p("Term", r.term)}${p("Content", r.content)}${p("Click ID", r.clickId)}${p("Click ID Type", r.clickIdType)}${p("Landing URL", r.landingUrl)}${p("Referrer", r.referrer)}</div><div class="ekmSplit__column"><h5>GA4</h5>${p("Client ID", (o = r.ga4) == null ? void 0 : o.clientId)}${p("Session ID", (d = r.ga4) == null ? void 0 : d.sessionId)}${I(a)}<h5>Meta</h5>${p("FBP", (c = r.meta) == null ? void 0 : c.fbp)}${p("FBC", (m = r.meta) == null ? void 0 : m.fbc)}${I(e)}</div></div>`;
}
function p(r, a) {
  return a ? `<p class="ekmOrderTracking__wrap">${i(r)}: ${i(a)}</p>` : "";
}
function I(r) {
  return r.length ? `<ul>${r.map(([a, e]) => `<li><strong>${i(a)}</strong>: ${i(e)}</li>`).join("")}</ul>` : "";
}
function re(r) {
  return !!(r && (r.resolvedAtUtc || r.source || r.analytics !== null && r.analytics !== void 0 || r.marketing !== null && r.marketing !== void 0));
}
function T(r) {
  return r === !0 ? "Yes" : r === !1 ? "No" : "Unknown";
}
function P(r, a) {
  return a.map((e) => {
    var t;
    return {
      key: e.key,
      label: e.label,
      value: f(r[e.property] || ((t = r.properties) == null ? void 0 : t[e.key]) || ""),
      isExtra: !1
    };
  });
}
function M(r, a) {
  return D(r, a).map(([e, t]) => ({
    key: e,
    label: q(e).replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim() || e,
    value: t,
    isExtra: !0
  }));
}
function F(r) {
  return Object.fromEntries(r.map((a) => [a.key, a.value || ""]));
}
function se(r) {
  try {
    return JSON.parse(r).message || r;
  } catch {
    return r;
  }
}
customElements.define("ekom-orders-section-view", W);
export {
  W as EkomOrdersSectionViewElement,
  W as default
};
