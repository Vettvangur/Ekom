var G = Object.defineProperty;
var z = (i, d, e) => d in i ? G(i, d, { enumerable: !0, configurable: !0, writable: !0, value: e }) : i[d] = e;
var l = (i, d, e) => z(i, typeof d != "symbol" ? d + "" : d, e);
import { UmbElementMixin as H } from "@umbraco-cms/backoffice/element-api";
import { UMB_NOTIFICATION_CONTEXT as Z } from "@umbraco-cms/backoffice/notification";
import { E as W, m as c, a as Q, e as s, g as $, f as y, p as Y, d as O, b as J } from "./manager-shared.js";
const X = [
  { key: "customerName", label: "Name", property: "name" },
  { key: "customerEmail", label: "Email", property: "email" },
  { key: "customerAddress", label: "Address", property: "address" },
  { key: "customerApartment", label: "Apartment", property: "apartment" },
  { key: "customerCity", label: "City", property: "city" },
  { key: "customerCountry", label: "Country", property: "country" },
  { key: "customerZipCode", label: "Zipcode", property: "zipCode" },
  { key: "customerPhone", label: "Phone", property: "phone" }
], ee = [
  { key: "shippingName", label: "Name", property: "name" },
  { key: "shippingEmail", label: "Email", property: "email" },
  { key: "shippingAddress", label: "Address", property: "address" },
  { key: "shippingApartment", label: "Apartment", property: "apartment" },
  { key: "shippingCity", label: "City", property: "city" },
  { key: "shippingCountry", label: "Country", property: "country" },
  { key: "shippingZipCode", label: "Zipcode", property: "zipCode" },
  { key: "shippingPhone", label: "Phone", property: "phone" }
], te = 180, M = 1, F = 2;
class re extends H(HTMLElement) {
  constructor() {
    super(...arguments);
    l(this, "api", new W());
    l(this, "notificationContext");
    l(this, "result", { orders: [], count: 0, totalPages: 0 });
    l(this, "page", 1);
    l(this, "loading", !0);
    l(this, "error", "");
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
    super.connectedCallback(), this.consumeContext(Z, (e) => {
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
      c.statusList = e || [], c.stores = t || [], !c.filters.store && c.stores.length && (c.filters.store = c.stores[0].alias), await this.loadPaymentProviders(), await this.loadOrders();
    } catch (e) {
      this.error = m(e, "Error loading Ekom Manager."), this.loading = !1, this.render();
    }
  }
  async loadPaymentProviders(e = !1) {
    if (!c.filters.store) {
      c.paymentProviders = [];
      return;
    }
    c.paymentProviders = await this.api.paymentProviders(c.filters.store), e && (c.filters.paymentProvider = ""), c.filters.paymentProvider && (c.paymentProviders.some((r) => r.key === c.filters.paymentProvider) || (c.filters.paymentProvider = ""));
  }
  async loadOrders() {
    if (!c.filters.store) {
      this.loading = !1, this.result = { orders: [], count: 0, totalPages: 0 }, this.render();
      return;
    }
    this.loading = !0, this.error = "", this.render();
    try {
      const e = await this.api.searchOrders(c.filters, this.page);
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
      <style>${Q}</style>
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
    const e = c.filters;
    return `
      <div class="umb-sub-header">
        <div class="ekmManager__filters">
          <label class="ekmManager__filter">Order Status:
            <select data-field="orderStatus">
              <option value="CompletedOrders" ${e.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option>
              <option value="AllOrders" ${e.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>
              ${c.statusList.map((t) => {
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
              ${c.stores.map((t) => `<option value="${s(t.alias)}" ${e.store === t.alias ? "selected" : ""}>${s(t.title)}</option>`).join("")}
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
            ${c.statusList.map((t) => {
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
          ${Y(this.page, this.result.totalPages).map((e) => {
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
    const e = c.filters;
    return `
      <div class="ekmOverlay">
        <div class="ekmOverlay__panel ekmOverlay__panel--small">
          <div class="ekmOverlay__header"><h2>Filter</h2><button class="btn-reset" type="button" data-action="close-overlay">&times;</button></div>
          <div class="ekmOverlay__content">
            <label class="control-group">Payment provider:
              <select data-filter-field="paymentProvider">
                <option value="">Select payment provider</option>
                ${c.paymentProviders.map((t) => `<option value="${s(t.key)}" ${e.paymentProvider === t.key ? "selected" : ""}>${s(t.title)}</option>`).join("")}
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
        <input type="text" data-filter-field="${s(e)}" value="${s(c.filters[e])}" placeholder="${s(r)}">
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
    var o, n, u, v;
    const t = ((o = e.customerInformation) == null ? void 0 : o.customer) || {}, r = ((n = e.customerInformation) == null ? void 0 : n.shipping) || {}, a = this.getOrderStatusValue(e.orderStatus);
    return `
      <div class="ekmOrder__header">
        <h1>Order number: ${s(e.referenceId)}</h1>
        <div class="ekmOrderStatusBar">
          <label class="ekmOrderStatusBar__status">Order Status:
            <select data-field="orderStatusOverlay">
              ${c.statusList.map((g) => {
      const b = $(g);
      return `<option value="${s(b)}" ${a === b ? "selected" : ""}>${s(g.label)}</option>`;
    }).join("")}
            </select>
          </label>
          <label class="ekmCheckboxLabel"><input type="checkbox" data-field="notifyOrderStatus"> Fire events?</label>
          <button type="button" class="btn-success" data-action="save-overlay-status">Save</button>
          <button type="button" class="btn-outline ekmOrderStatusBar__print" data-action="print-order">Print</button>
        </div>
        <p>UniqueId: ${s(e.uniqueId)}</p>
        <p>Created date: ${s(y(e.createDate))}</p>
        <p>Paid date: ${s(y(e.paidDate))}</p>
        <p>Store: ${s(((u = e.storeInfo) == null ? void 0 : u.alias) || e.storeAlias)}</p>
        <p>Payment: <strong>${s((v = e.chargedAmount) == null ? void 0 : v.currencyString)}</strong></p>
        ${this.renderOrderActions()}
      </div>
      <div class="ekmSplit">
        <div class="ekmSplit__column"><h4>Billing</h4><button type="button" class="btn-outline" data-action="open-customer-editor" style="margin-bottom:10px;">Edit customer information</button>${this.renderAddress(t)}${this.renderExtraProperties(t.properties, "customer")}</div>
        <div class="ekmSplit__column"><h4>Shipping</h4>${ae(r) ? `${this.renderAddress(r)}${this.renderExtraProperties(r.properties, "shipping")}` : '<p style="font-weight:bold">Same as billing address</p>'}</div>
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
    return ["name", "email", "address", "apartment", "city", "country", "zipCode", "phone"].filter((t) => e[t]).map((t) => `<p>${ie(t)}: ${s(O(e[t]))}</p>`).join("");
  }
  renderExtraProperties(e, t) {
    const r = K(e, t);
    return r.length ? `<h5 style="margin-top:20px; font-weight:bold;">${t === "customshipping" ? "Shipping Provider Custom" : `Extra ${t === "shipping" ? "Shipping" : "Customer"}`} Data</h5><ul>${r.map(([a, o]) => `<li><strong>${s(B(a))}</strong>: ${s(o)}</li>`).join("")}</ul>` : "";
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
    return t.length ? `<div class="ekmOrderGiftcards"><h4>Gift cards</h4><ul class="ekmOrderTracking__list">${t.map((r) => {
      const a = r.validUntil && Date.parse(r.validUntil) <= Date.now(), o = r.claimed ? "Claimed" : a ? "Expired" : "Available";
      return `<li><strong>${s(r.code)}</strong>: ${s(this.formatGiftcardAmount(r.amount, e))} (${o})${r.validUntil ? ` · Expires ${s(y(r.validUntil))}` : ""}</li>`;
    }).join("")}</ul></div>` : "";
  }
  formatGiftcardAmount(e, t) {
    var o, n, u, v;
    const r = ((n = (o = t.storeInfo) == null ? void 0 : o.currency) == null ? void 0 : n.currencyValue) || "en-US", a = ((v = (u = t.storeInfo) == null ? void 0 : u.currency) == null ? void 0 : v.isoCurrencySymbol) || t.currency;
    try {
      return new Intl.NumberFormat(r, { style: "currency", currency: a }).format(e);
    } catch {
      return String(e);
    }
  }
  renderOrderLines(e) {
    var o, n, u, v, g, b, f;
    const t = Array.isArray(e.orderLines) ? e.orderLines : [], r = typeof e.coupon == "string" ? e.coupon.trim() : "", a = r ? `Discount (${r})` : "Discount";
    return `
      <div style="align-items:center; display:flex; gap:10px; justify-content:space-between;"><h4>Order Details</h4><button type="button" class="btn-outline" data-action="open-order-line-editor">Add order line</button></div>
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed">Product</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Unit Price (inc VAT)</div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">Discount</div><div class="umb-table-cell">Total (inc VAT)</div></div></div>
        <div class="umb-table-body">
          ${t.map((p) => {
      var S, k, E, L, x, w, _, A, C, P, I, T, q;
      return `<div class="umb-table-row"><div class="umb-table-cell"><button type="button" class="btn-reset" data-action="remove-order-line" data-order-line-id="${s(p.key)}" data-product-title="${s((S = p.product) == null ? void 0 : S.title)}" ${this.removingOrderLineId === p.key ? "disabled" : ""} aria-label="Remove ${s((k = p.product) == null ? void 0 : k.title)}" title="Remove order line">&#128465;</button></div><div class="umb-table-cell not-fixed">${s((E = p.product) == null ? void 0 : E.title)} (${s((L = p.product) == null ? void 0 : L.sku)})${p.variant ? `<small style="display:block; margin-top:3px;">${s(p.variant.title)} ${p.variant.sku ? `(${s(p.variant.sku)})` : ""}</small>` : ""}${oe(p)}</div><div class="umb-table-cell">${s(p.quantity)}</div><div class="umb-table-cell">${s((_ = (w = (x = p.product) == null ? void 0 : x.price) == null ? void 0 : w.withVat) == null ? void 0 : _.currencyString)}</div><div class="umb-table-cell">${s((C = (A = p.amount) == null ? void 0 : A.vat) == null ? void 0 : C.currencyString)}</div><div class="umb-table-cell">-${s((I = (P = p.amount) == null ? void 0 : P.discountAmount) == null ? void 0 : I.currencyString)}</div><div class="umb-table-cell"><strong>${s((q = (T = p.amount) == null ? void 0 : T.withVat) == null ? void 0 : q.currencyString)}</strong></div></div>`;
    }).join("")}
        </div>
        <div class="umb-table-footer">
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Sub Total (inc VAT)</div><div class="umb-table-cell">${s((n = (o = e.subTotal) == null ? void 0 : o.withVat) == null ? void 0 : n.currencyString)}</div></div>
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">${s(a)}</div><div class="umb-table-cell">-${s((u = e.discountAmount) == null ? void 0 : u.currencyString)}</div></div>
          ${e.shippingProvider ? `<div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Shipping Total</div><div class="umb-table-cell">${s((g = (v = e.shippingProvider.price) == null ? void 0 : v.withVat) == null ? void 0 : g.currencyString)}</div></div>` : ""}
          ${this.renderGiftcardTotal(e)}
          <div class="umb-table-row"><div class="umb-table-cell"></div><div class="umb-table-cell not-fixed"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell"></div><div class="umb-table-cell">Vat</div><div class="umb-table-cell">${s((b = e.chargedVat) == null ? void 0 : b.currencyString)}</div></div>
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
    return de(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Tracking</h4><button class="btn-reset" type="button" data-action="toggle-tracking">${this.trackingExpanded ? "Hide" : "Show"}</button></div>${this.trackingExpanded ? le(t) : ""}</div>` : '<div class="ekmOrderTracking"><h4>Tracking</h4><p>No tracking data was captured for this order.</p></div>';
  }
  renderConsent(e) {
    const t = e.consent;
    return ce(t) ? `<div class="ekmOrderTracking"><div class="ekmOrderTracking__header"><h4>Consent</h4><button class="btn-reset" type="button" data-action="toggle-consent">${this.consentExpanded ? "Hide" : "Show"}</button></div>${this.consentExpanded ? `<p>Resolved: ${s(y(t.resolvedAtUtc))}</p><p>Source: ${s(t.source)}</p><p>Analytics: ${N(t.analytics)}</p><p>Marketing: ${N(t.marketing)}</p>` : ""}</div>` : '<div class="ekmOrderTracking"><h4>Consent</h4><p>No consent data was captured for this order.</p></div>';
  }
  renderActivityLogs() {
    return this.orderLogsLoading ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Loading activity...</p></div>' : this.orderLogsError ? '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>Unable to load activity log.</p></div>' : this.orderLogs.length ? `<div class="ekmOrderActivityLog"><h4>Activity log</h4><div class="ekmOrderActivityLog__list">${this.orderLogs.map((t, r) => {
      const a = t.message ?? "", o = this.activityLogExpandedIndexes.has(r), n = this.canExpandActivityLog(a) ? `<button type="button" class="btn-reset ekmOrderActivityLog__toggle" data-action="toggle-activity-log" data-log-index="${r}">${o ? "Show less" : "Show more"}</button>` : "";
      return `<div class="ekmOrderActivityLog__item"><div class="ekmOrderActivityLog__content"><span class="ekmOrderActivityLog__icon ${this.getActivityLogTypeClass(t)}">${this.getActivityLogIcon(t)}</span><div class="ekmOrderActivityLog__body"><div class="ekmOrderActivityLog__date">${s(y(t.date))}</div><div class="ekmOrderActivityLog__message${o ? " ekmOrderActivityLog__message--expanded" : ""}">${s(a)}</div>${n}</div></div></div>`;
    }).join("")}</div></div>` : '<div class="ekmOrderActivityLog"><h4>Activity log</h4><p>No activity yet.</p></div>';
  }
  getActivityLogIcon(e) {
    switch (e.logType) {
      case M:
        return "✓";
      case F:
        return "!";
      default:
        return "i";
    }
  }
  getActivityLogTypeClass(e) {
    switch (e.logType) {
      case M:
        return "ekmOrderActivityLog__icon--success";
      case F:
        return "ekmOrderActivityLog__icon--alert";
      default:
        return "ekmOrderActivityLog__icon--info";
    }
  }
  canExpandActivityLog(e) {
    return e.length > te;
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
    r === "orderStatusOverlay" || r === "notifyOrderStatus" || r === "query" || !r || !(r in c.filters) || (c.filters[r] = t.value, this.page = 1, r === "store" && await this.loadPaymentProviders(!0), await this.loadOrders());
  }
  handleSearchInput(e) {
    const t = e.currentTarget;
    c.filters.query = t.value, this.page = 1, window.clearTimeout(this.searchTimer), this.searchTimer = window.setTimeout(() => void this.loadOrders(), 700);
  }
  applyFilterOverlay() {
    this.querySelectorAll("[data-filter-field]").forEach((e) => {
      const t = e.dataset.filterField;
      t && t in c.filters && (c.filters[t] = e.value);
    });
  }
  async exportOrders() {
    if (this.result.count) {
      this.exporting = !0, this.render();
      try {
        const e = await this.api.exportOrders(c.filters, this.result.count, this.exportIncludeOrderLines);
        J(e, this.exportIncludeOrderLines ? "orders-with-orderlines.csv" : "orders.csv"), this.overlay = "";
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
    } catch (n) {
      this.showError(m(n, "Error updating order status."));
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
    const t = String(e ?? ""), r = c.statusList.find((a) => String(a.value ?? "") === t || String(a.enumValue ?? "") === t);
    return r ? $(r) : t;
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
        const a = await this.api.executeOrderAction(this.selectedOrder.uniqueId, e), o = await a.blob(), n = a.headers.get("content-disposition") || "", u = a.headers.get("content-type") || "";
        if (n.toLowerCase().includes("filename=") || u.startsWith("application/pdf") || u.startsWith("application/octet-stream") || u.startsWith("image/"))
          window.open(URL.createObjectURL(o), "_blank");
        else {
          const v = await o.text();
          this.showSuccess(ue(v));
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
      customer: j(t, X).concat(U(t.properties, "customer")),
      shipping: j(r, ee).concat(U(r.properties, "shipping"))
    }, this.customerEditorOpen = !0, this.render();
  }
  async saveCustomerInformation() {
    var e;
    if (!(!((e = this.selectedOrder) != null && e.uniqueId) || !this.customerEditModel || this.customerSaving)) {
      this.querySelectorAll("[data-customer-group]").forEach((t) => {
        var n;
        const r = t.dataset.customerGroup, a = t.dataset.customerKey, o = (n = this.customerEditModel) == null ? void 0 : n[r].find((u) => u.key === a);
        o && (o.value = t.value);
      }), this.customerSaving = !0, this.render();
      try {
        this.selectedOrder = await this.api.updateCustomerInformation(
          this.selectedOrder.uniqueId,
          V(this.customerEditModel.customer),
          V(this.customerEditModel.shipping)
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
    this.querySelectorAll("[data-order-line-field]").forEach((n) => {
      const u = n.dataset.orderLineField;
      u && (this.orderLineEditModel[u] = n.value);
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
      this.showError(m(n, "Error adding order line."));
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
        this.showError(m(a, "Error loading shipping providers."));
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
      const u = n.existing ? n.key : `customshipping${n.key.trim()}`;
      if (!u.slice(14).trim() || u.length > 100 || n.value.length > 4096 || r.has(u.toLowerCase())) {
        this.showError("Enter unique shipping custom data keys and valid values.");
        return;
      }
      r.add(u.toLowerCase()), t[u] = n.value;
    }
    this.shippingProviderSaving = !0, this.render();
    try {
      this.selectedOrder = await this.api.updateShippingProvider(this.selectedOrder.uniqueId, e, t), this.shippingProviderEditorOpen = !1, this.showSuccess("Shipping provider updated."), await this.refreshOrderAfterLineChange();
    } catch (n) {
      this.showError(m(n, "Error updating shipping provider."));
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
function ie(i) {
  return i === "zipCode" ? "Zipcode" : i.charAt(0).toUpperCase() + i.slice(1);
}
function se(i) {
  return (/* @__PURE__ */ new Set(["shippingname", "shippingaddress", "shippingcity", "shippingcountry", "shippingemail", "shippingapartment", "shippingzipcode", "shippingphone", "customeremail", "customername", "customeraddress", "customerapartment", "customercity", "customercountry", "customerzipcode", "customerphone"])).has(i.toLowerCase());
}
function B(i) {
  return i.replace(/^customshipping/i, "").replace(/^custompayment/i, "").replace(/^shipping/i, "").replace(/^customer/i, "");
}
function K(i, d) {
  return Object.entries(i || {}).filter(([e, t]) => (!!t || d === "customshipping" && t === "") && e.toLowerCase().startsWith(d) && !se(e)).map(([e, t]) => [e, O(t)]);
}
function ae(i) {
  return !!(i != null && i.name || i != null && i.email || i != null && i.address || i != null && i.apartment || i != null && i.city || i != null && i.country || i != null && i.zipCode || i != null && i.phone);
}
function oe(i) {
  var e;
  const d = ((e = i.orderLineInfo) == null ? void 0 : e.properties) || {};
  return Object.entries(d).filter(([, t]) => !!t).map(([t, r]) => `<small style="display:block; margin-top:3px;"><strong>${s(ne(t))}</strong>: ${s(O(r))}</small>`).join("");
}
function ne(i) {
  const d = i.replace(/^orderline/i, "").replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim();
  return d ? d.charAt(0).toUpperCase() + d.slice(1) : i;
}
function de(i) {
  var d, e, t, r;
  return !!(i && (i.source || i.medium || i.campaign || i.term || i.content || i.clickId || i.clickIdType || i.landingUrl || i.referrer || i.captureMethod || i.capturedAtUtc || i.hasCookieSupport !== null && i.hasCookieSupport !== void 0 || (d = i.ga4) != null && d.clientId || (e = i.ga4) != null && e.sessionId || (t = i.meta) != null && t.fbp || (r = i.meta) != null && r.fbc || R(i.algolia)));
}
function le(i) {
  var o, n, u, v, g, b;
  const d = Object.entries(((o = i.ga4) == null ? void 0 : o.data) || {}), e = Object.entries(((n = i.meta) == null ? void 0 : n.data) || {}), t = i.algolia, r = Array.isArray(t == null ? void 0 : t.lines) ? t.lines : [], a = R(t) ? `<div class="ekmOrderTracking__provider"><h5>Algolia</h5>${h("User token", t.userToken)}${r.length ? `<ul>${r.map((f) => `<li class="ekmOrderTracking__wrap"><strong>Order line key</strong>: ${s(f.orderLineKey)}<br><strong>Query ID</strong>: ${s(f.queryId)}</li>`).join("")}</ul>` : ""}</div>` : "";
  return `<div class="ekmSplit"><div class="ekmSplit__column">${h("Captured", y(i.capturedAtUtc))}${h("Capture method", i.captureMethod)}${i.hasCookieSupport !== null && i.hasCookieSupport !== void 0 ? `<p>Cookie support: ${i.hasCookieSupport ? "Yes" : "No"}</p>` : ""}${h("Source", i.source)}${h("Medium", i.medium)}${h("Campaign", i.campaign)}${h("Term", i.term)}${h("Content", i.content)}${h("Click ID", i.clickId)}${h("Click ID Type", i.clickIdType)}${h("Landing URL", i.landingUrl)}${h("Referrer", i.referrer)}</div><div class="ekmSplit__column"><h5>GA4</h5>${h("Client ID", (u = i.ga4) == null ? void 0 : u.clientId)}${h("Session ID", (v = i.ga4) == null ? void 0 : v.sessionId)}${D(d)}<h5>Meta</h5>${h("FBP", (g = i.meta) == null ? void 0 : g.fbp)}${h("FBC", (b = i.meta) == null ? void 0 : b.fbc)}${D(e)}${a}</div></div>`;
}
function R(i) {
  return !!(i && (i.userToken || Array.isArray(i.lines) && i.lines.length));
}
function h(i, d) {
  return d ? `<p class="ekmOrderTracking__wrap">${s(i)}: ${s(d)}</p>` : "";
}
function D(i) {
  return i.length ? `<ul>${i.map(([d, e]) => `<li><strong>${s(d)}</strong>: ${s(e)}</li>`).join("")}</ul>` : "";
}
function ce(i) {
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
  return K(i, d).map(([e, t]) => ({
    key: e,
    label: B(e).replace(/([a-z0-9])([A-Z])/g, "$1 $2").replace(/[_-]+/g, " ").trim() || e,
    value: t,
    isExtra: !0
  }));
}
function V(i) {
  return Object.fromEntries(i.map((d) => [d.key, d.value || ""]));
}
function ue(i) {
  try {
    return JSON.parse(i).message || i;
  } catch {
    return i;
  }
}
customElements.define("ekom-orders-section-view", re);
export {
  re as EkomOrdersSectionViewElement,
  re as default
};
