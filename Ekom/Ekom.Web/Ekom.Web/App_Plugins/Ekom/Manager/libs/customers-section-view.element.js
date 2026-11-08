var v = Object.defineProperty;
var w = (s, r, e) => r in s ? v(s, r, { enumerable: !0, configurable: !0, writable: !0, value: e }) : s[r] = e;
var l = (s, r, e) => w(s, typeof r != "symbol" ? r + "" : r, e);
const _ = (s) => class extends s {
  connectedCallback() {
  }
  disconnectedCallback() {
  }
}, k = /* @__PURE__ */ new Date(), O = new Date(k.getFullYear(), 0, 1), d = {
  filters: {
    dateFrom: y(O),
    dateTo: y(k),
    orderStatus: "CompletedOrders",
    store: "",
    paymentProvider: "",
    shippingProvider: "",
    couponCode: "",
    giftcardCode: "",
    productSku: "",
    trackingSource: "",
    trackingMedium: "",
    trackingCampaign: "",
    trackingTerm: "",
    trackingContent: "",
    trackingClickId: "",
    query: ""
  },
  statusList: [],
  stores: [],
  paymentProviders: [],
  shippingFilterProviders: []
};
class p {
  async config() {
    return this.getJson("/ekom/backoffice/Config");
  }
  async searchOrders(r, e, t = 20) {
    return this.getJson("/ekom/manager/SearchOrders", {
      ...r,
      start: r.dateFrom,
      end: r.dateTo,
      page: e,
      pageSize: t
    });
  }
  async exportOrders(r, e, t) {
    return this.getBlob("/ekom/manager/ExportOrders", {
      ...r,
      start: r.dateFrom,
      end: r.dateTo,
      page: 1,
      pageSize: e,
      includeOrderLines: t
    });
  }
  async statusList() {
    return this.getJson("/ekom/manager/StatusList");
  }
  async stores() {
    return this.getJson("/ekom/manager/stores");
  }
  async paymentProviders(r) {
    return this.getJson(`/ekom/manager/paymentproviders/${encodeURIComponent(r)}`);
  }
  async shippingProviders(r) {
    return this.getJson(`/ekom/manager/shippingproviders/${encodeURIComponent(r)}`);
  }
  async orderInfo(r) {
    return this.getJson(`/ekom/manager/OrderInfo/${encodeURIComponent(r)}`);
  }
  async orderLogs(r) {
    return this.getJson(`/ekom/manager/OrderLogs/${encodeURIComponent(r)}`);
  }
  async orderActions(r) {
    return this.getJson(`/ekom/manager/OrderActions/${encodeURIComponent(r)}`);
  }
  async orderLockStatus(r) {
    return this.getJson(`/ekom/manager/orderLockStatus/${encodeURIComponent(r)}`);
  }
  async releaseOrderLock(r, e) {
    return this.postBody(`/ekom/manager/releaseOrderLock/${encodeURIComponent(r)}`, { releaseToken: e });
  }
  async giftcardRecovery(r) {
    return this.getJson(`/ekom/manager/giftcardRecovery/${encodeURIComponent(r)}`);
  }
  async releaseGiftcardRecovery(r, e, t, a) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(r)}/release`, { attemptId: e, claimId: t, token: a });
  }
  async reconcileGiftcardRecovery(r, e, t) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(r)}/reconcile`, { attemptId: e, token: t });
  }
  async executeOrderAction(r, e) {
    const t = await fetch(`/ekom/manager/OrderActions/${encodeURIComponent(r)}/${encodeURIComponent(e)}`, {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json, application/octet-stream" }
    });
    if (!t.ok)
      throw await p.createError(t, "Order action failed.");
    return t;
  }
  async changeOrderStatus(r, e, t) {
    return this.postJson("/ekom/manager/changeOrderStatus", { orderId: r, orderStatus: e, notify: t });
  }
  async updateCustomerInformation(r, e, t) {
    return this.postBody("/ekom/manager/UpdateCustomerInformation", { orderId: r, customer: e, shipping: t });
  }
  async addOrderLine(r, e, t, a) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(r)}/OrderLines`, { productId: e, variantId: t, quantity: a });
  }
  async updateShippingProvider(r, e, t) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(r)}/ShippingProvider`, { providerId: e, customData: t });
  }
  async removeOrderLine(r, e) {
    const t = await fetch(`/ekom/manager/Order/${encodeURIComponent(r)}/OrderLines/${encodeURIComponent(e)}`, {
      method: "DELETE",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await p.createError(t, "Error removing order line.");
    return t.json();
  }
  async charts(r, e) {
    return this.getJson("/ekom/manager/charts", {
      start: r.dateFrom,
      end: r.dateTo,
      orderStatus: r.orderStatus,
      store: r.store
    }, e);
  }
  async mostSoldProducts(r, e, t = 20, a) {
    return this.getJson("/ekom/manager/MostSoldProducts", {
      start: r.dateFrom,
      end: r.dateTo,
      orderStatus: r.orderStatus,
      store: r.store,
      page: e,
      pageSize: t
    }, a);
  }
  async getJson(r, e, t) {
    const a = await fetch(r + b(e), {
      credentials: "same-origin",
      signal: t,
      headers: { Accept: "application/json" }
    });
    if (!a.ok)
      throw await p.createError(a, "Request failed.");
    return a.json();
  }
  async getBlob(r, e) {
    const t = await fetch(r + b(e), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!t.ok)
      throw await p.createError(t, "Request failed.");
    return t.blob();
  }
  async postJson(r, e) {
    const t = await fetch(r + b(e), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await p.createError(t, "Request failed.");
    return t.json();
  }
  async postBody(r, e) {
    const t = await fetch(r, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json"
      },
      body: JSON.stringify(e)
    });
    if (!t.ok)
      throw await p.createError(t, "Request failed.");
    return t.json();
  }
  static async createError(r, e) {
    const t = await r.text();
    let a = t || e;
    try {
      a = JSON.parse(t).message || a;
    } catch {
    }
    return new Error(a || `${e} Status ${r.status}.`);
  }
}
function b(s) {
  if (!s)
    return "";
  const r = new URLSearchParams();
  for (const [t, a] of Object.entries(s))
    r.set(t, a == null ? "" : String(a));
  const e = r.toString();
  return e ? `?${e}` : "";
}
function y(s) {
  const r = String(s.getMonth() + 1).padStart(2, "0"), e = String(s.getDate()).padStart(2, "0");
  return `${s.getFullYear()}-${r}-${e}`;
}
function i(s) {
  return String(s ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
const $ = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`, S = `
.ekmOrderLineProduct { display: flex; align-items: flex-start; gap: 14px; min-width: 0; width: 100%; }
.ekmOrderLineProduct__details { min-width: 0; overflow-wrap: anywhere; }
.ekmOrderLineProduct__details > span:first-child, .ekmOrderLineProduct__details > div:first-child { font-weight: 600; }
.ekmOrderLineProduct__details a { color: inherit; text-decoration: none; }
.ekmOrderLineProduct__details a:hover, .ekmOrderLineProduct__details a:focus-visible { text-decoration: underline; }
.ekmOrderLineProduct__details a::after { content: "\\2197"; margin-left: 6px; color: #686b7d; }
.ekmOrderLineThumbnail { position: relative; display: flex; align-items: center; justify-content: center; flex: 0 0 56px; width: 56px; height: 56px; overflow: hidden; border: 1px dashed #d8d7d9; border-radius: 9px; background: #f6f6f8; color: #686b7d; }
.ekmOrderLineThumbnail svg { width: 24px; height: 24px; }
.ekmOrderLineThumbnail img { position: absolute; inset: 0; width: 100%; height: 100%; object-fit: cover; background: #f6f6f8; }
.ekmOrderLineThumbnail img[hidden] { display: none; }

  ${$}
  :host { display: block; height: 100%; color: var(--uui-color-text, #1b264f); }
  .ekmManager { min-height: 100%; background: var(--uui-color-surface, #f6f4f4); }
  .ekmManager__body { padding: 24px; }
  .cards { display: grid; gap: 16px; grid-template-columns: repeat(3, minmax(0, 1fr)); margin-bottom: 20px; }
  .card { background-color: #fff; border-radius: 6px; box-shadow: 0 1px 3px rgba(0,0,0,.08); padding: 15px; text-align: center; }
  .card strong { display: block; font-size: 24px; margin-bottom: 15px; }
  .ekmSummaryCard { align-items: flex-start; background: linear-gradient(135deg, #fff 0%, #f8fafc 100%); border: 1px solid #e6e8ef; display: flex; flex-direction: column; gap: 8px; min-width: 0; padding: 18px 20px; text-align: left; }
  .ekmSummaryCard__label { color: var(--uui-color-text-alt, #515054); font-size: 13px; font-weight: 700; letter-spacing: .02em; text-transform: uppercase; }
  .ekmSummaryCard__value { color: #1b264f; font-size: clamp(24px, 4vw, 34px); line-height: 1.1; margin: 0; overflow-wrap: anywhere; }
  .umb-sub-header, .ekmToolbar { background: #fff; border-radius: 6px; margin-bottom: 20px; padding: 15px; }
  .ekmManager__filters { align-items: end; display: grid; gap: 15px; grid-template-columns: repeat(4, minmax(150px, max-content)) minmax(260px, 1fr); width: 100%; }
  .ekmManager__filter { display: grid; gap: 5px; min-width: 0; }
  .ekmManager__filter input, .ekmManager__filter select { width: 100%; }
  .ekmManager__search { align-items: end; display: flex; gap: 10px; justify-content: flex-end; min-width: 0; }
  label { font-weight: 700; }
  input:not([type='checkbox']), select { border: 1px solid #d8d7d9; border-radius: 3px; box-sizing: border-box; font: inherit; min-height: 36px; padding: 6px 10px; }
  .ekmCheckboxLabel { align-items: center; display: inline-flex; gap: 6px; }
  .ekmCheckboxLabel input { margin: 0; }
  .form-search { min-width: 0; }
  .form-search input { min-width: 220px; width: 100%; }
  button, .btn { border: 1px solid #d8d7d9; border-radius: 3px; cursor: pointer; font: inherit; min-height: 36px; padding: 7px 14px; }
  button:disabled { cursor: not-allowed; opacity: .6; }
  .btn-primary, .btn-success { background: var(--uui-color-positive, #1b7f43); border-color: var(--uui-color-positive, #1b7f43); color: #fff; }
  .btn-outline { background: #fff; color: #1b264f; }
  .btn-reset { background: transparent; border: 0; min-height: 0; padding: 0; }
  .umb-table { background: #fff; border: 1px solid #e9e9eb; border-radius: 6px; display: table; overflow: hidden; width: 100%; }
  .umb-table-head { display: table-header-group; font-weight: 700; }
  .umb-table-body { display: table-row-group; }
  .umb-table-footer { display: table-footer-group; font-weight: 700; }
  .umb-table-row { display: table-row; }
  .umb-table-cell { border-bottom: 1px solid #eee; color: #111; display: table-cell; padding: 12px; vertical-align: middle; }
  .not-fixed { min-width: 110px; }
  .pagination ul { display: flex; gap: 4px; justify-content: center; list-style: none; padding: 0; }
  .pagination li.active button { background: var(--uui-color-interactive, #3544b1); color: #fff; }
  .ekmGrid { display: grid; gap: 40px; grid-template-columns: 1fr 1fr; margin-bottom: 40px; }
  .ekmChartCard { text-align: left; }
  .ekmChartCard__canvas { height: 320px; position: relative; }
  .ekmChartCard canvas { height: 100%; width: 100%; }
  .ekmSplit { display: flex; gap: 30px; margin-bottom: 30px; }
  .ekmSplit__column { flex: 1; min-width: 0; }
  .ekmOverlay { align-items: flex-start; background: rgba(0,0,0,.35); display: flex; inset: 0; justify-content: center; overflow: auto; padding: 40px 20px; position: fixed; z-index: 10000; }
  .ekmOverlay__panel { background: #fff; border-radius: 3px; box-shadow: 0 10px 30px rgba(0,0,0,.25); max-width: 1100px; width: 100%; }
  .ekmOverlay__panel--small { max-width: 620px; }
  .ekmOverlay__header { align-items: center; border-bottom: 1px solid #d8d7d9; display: flex; gap: 12px; justify-content: space-between; padding: 20px; }
  .ekmOverlay__header h2 { margin: 0; }
  .ekmOverlay__content { padding: 20px; }
  .ekmOrderStatusBar { align-items: center; border-bottom: 1px solid #d8d7d9; display: flex; flex-wrap: wrap; gap: 15px; margin-bottom: 15px; padding-bottom: 15px; }
  .ekmOrderStatusBar__status { align-items: center; display: flex; gap: 8px; }
  .ekmOrderStatusBar__print { margin-left: auto; }
  .ekmOrderTracking, .ekmOrderGiftcards, .ekmOrderActivityLog { border: 1px solid #d8d7d9; margin: 30px 0; padding: 14px 16px; }
  .ekmOrderTracking__header { align-items: center; display: flex; justify-content: space-between; }
  .ekmOrderTracking__wrap { overflow-wrap: anywhere; word-break: break-word; }
  .ekmOrderGiftcards__list { list-style: none; margin: 0; padding: 0; }
  .ekmOrderGiftcards__list > li { border-top: 1px solid #d8d7d9; padding: 16px 0; overflow-wrap: anywhere; }
  .ekmOrderGiftcards__summary, .ekmOrderGiftcards__actions { align-items: baseline; display: flex; flex-wrap: wrap; gap: 12px; }
  .ekmOrderGiftcards__summary strong { font-size: 16px; }
  .ekmOrderGiftcards__summary { gap: 4px 12px; }
  .ekmOrderGiftcards__summary strong + strong { margin-left: auto; }
  .ekmOrderGiftcards__summary > span { flex-basis: 100%; font-weight: 600; }
  .ekmOrderGiftcards__actions { margin: 12px 0; }
  .ekmOrderGiftcards details summary { cursor: pointer; }
  .ekmOrderGiftcards dt { font-weight: 600; margin-top: 8px; }
  .ekmOrderGiftcards dd { margin: 2px 0 0; white-space: pre-wrap; }
  .ekmGiftcardRecovery { overflow-wrap: anywhere; }
  .ekmGiftcardRecovery article { border-top: 1px solid #d8d7d9; margin-top: 16px; padding-top: 12px; }
  .ekmGiftcardRecovery__claim { border-left: 3px solid #d8d7d9; margin: 16px 0; padding-left: 12px; }
  .ekmGiftcardRecovery dt { font-weight: 600; margin-top: 8px; }
  .ekmGiftcardRecovery dd { margin: 2px 0 0; white-space: pre-wrap; }
  .ekmOrderActivityLog__list { border-top: 1px solid #eee; }
  .ekmOrderActivityLog__item { border-bottom: 1px solid #eee; padding: 12px 0; }
  .ekmOrderActivityLog__content { align-items: flex-start; display: flex; gap: 12px; }
  .ekmOrderActivityLog__body { flex: 1; min-width: 0; }
  .ekmOrderActivityLog__icon { align-items: center; border-radius: 50%; display: inline-flex; flex: 0 0 20px; font-size: 12px; font-weight: 700; height: 20px; justify-content: center; line-height: 1; margin-top: 1px; width: 20px; }
  .ekmOrderActivityLog__icon--info { background: #eef3f8; color: #46607a; }
  .ekmOrderActivityLog__icon--success { background: #edf8f0; color: #247f45; }
  .ekmOrderActivityLog__icon--alert { background: #fff4e5; color: #ad5f00; }
  .ekmOrderActivityLog__date { color: #666; font-size: 12px; margin-bottom: 6px; }
  .ekmOrderActivityLog__message { -webkit-box-orient: vertical; -webkit-line-clamp: 3; display: -webkit-box; line-height: 1.5; overflow: hidden; white-space: normal; word-break: break-word; }
  .ekmOrderActivityLog__message--expanded { -webkit-line-clamp: unset; display: block; overflow: visible; }
  .ekmOrderActivityLog__toggle { color: #1b264f; cursor: pointer; font-size: 12px; font-weight: 600; margin-top: 8px; padding: 0; text-decoration: underline; }
  .ekmCustomerInformationModal { align-items: center; background: rgba(0,0,0,.35); display: flex; inset: 0; justify-content: center; padding: 20px; position: fixed; z-index: 10001; }
  .ekmCustomerInformationModal__panel { background: #fff; border-radius: 3px; box-shadow: 0 10px 30px rgba(0,0,0,.25); max-height: 90vh; max-width: 760px; overflow: auto; width: 100%; }
  .control-group { display: grid; gap: 5px; margin-bottom: 14px; }
  .status { margin: 10px 0; }
  .status--error { color: var(--uui-color-danger, #d42054); }
  @media only screen and (max-width: 1180px) { .ekmManager__filters { grid-template-columns: repeat(2, minmax(0, 1fr)); } .ekmManager__search { grid-column: 1 / -1; justify-content: flex-start; } }
  @media only screen and (max-width: 900px) { .cards { grid-template-columns: 1fr; } }
  @media only screen and (max-width: 800px) { .ekmGrid { grid-template-columns: 1fr; } .ekmSplit { flex-direction: column; } }
  @media only screen and (max-width: 720px) {
    .ekmManager__filters { grid-template-columns: 1fr; }
    .ekmManager__search { align-items: stretch; flex-direction: column; }
    .ekmManager__search button, .form-search input { width: 100%; }
    .umb-table, .umb-table-head, .umb-table-body, .umb-table-row, .umb-table-cell { display: block; }
    .umb-table { background: transparent; border: 0; overflow: visible; }
    .umb-table-head { display: none; }
    .umb-table-row { background: #fff; border: 1px solid #e9e9eb; border-radius: 6px; box-shadow: 0 1px 2px rgba(0,0,0,.06); margin-bottom: 12px; overflow: hidden; }
    .umb-table-cell { align-items: center; border-bottom: 1px solid #f0f0f0; display: grid; gap: 12px; grid-template-columns: minmax(110px, 38%) minmax(0, 1fr); min-height: 44px; overflow-wrap: anywhere; padding: 10px 12px; }
    .umb-table-cell::before { color: var(--uui-color-text-alt, #515054); content: attr(data-label); font-size: 12px; font-weight: 700; letter-spacing: .02em; text-transform: uppercase; }
    .umb-table-cell:last-child { border-bottom: 0; }
    .umb-table-cell.not-fixed { min-width: 0; }
    .umb-table-cell select, .umb-table-cell button { width: 100%; }
  }
  @media only screen and (max-width: 640px) {
    .ekmOverlay { padding: 0; }
    .ekmOverlay__panel { border-radius: 0; box-shadow: none; min-height: 100vh; max-width: none; }
    .ekmOverlay__header { padding: 14px 16px; position: sticky; top: 0; z-index: 2; background: #fff; }
    .ekmOverlay__header h2 { font-size: 20px; }
    .ekmOverlay__content { padding: 16px; }
    .ekmOrder h1 { font-size: 22px; line-height: 1.2; overflow-wrap: anywhere; }
    .ekmOrderStatusBar { align-items: stretch; display: grid; grid-template-columns: 1fr; }
    .ekmOrderStatusBar__status { align-items: stretch; display: grid; gap: 5px; }
    .ekmOrderStatusBar__print { margin-left: 0; }
    .ekmOrderStatusBar button, .ekmOrderStatusBar select { width: 100%; }
    .ekmOrder p { overflow-wrap: anywhere; }
    .ekmOrder .umb-table-cell { grid-template-columns: minmax(105px, 36%) minmax(0, 1fr); }
    .ekmCustomerInformationModal { align-items: stretch; padding: 0; }
    .ekmCustomerInformationModal__panel { border-radius: 0; max-height: none; max-width: none; width: 100%; }
  }
  @media only screen and (max-width: 520px) { .ekmManager__body { padding: 16px; } .ekmSummaryCard { padding: 15px; } .ekmSummaryCard__value { font-size: 24px; } .umb-sub-header { padding: 12px; } }
  @media print {
    :host { color: #000; height: auto; }
    .ekmManager { display: none; }
    .ekmOverlay { background: #fff; display: block; inset: auto; overflow: visible; padding: 0; position: static; }
    .ekmOverlay__panel { border-radius: 0; box-shadow: none; max-width: none; min-height: auto; width: 100%; }
    .ekmOverlay__header,
    .ekmOrderStatusBar,
    .ekmOrder button,
    .ekmCustomerInformationModal { display: none !important; }
    .ekmOverlay__content { padding: 0; }
    .ekmOrder h1 { font-size: 22px; margin-top: 0; }
    .ekmSplit { break-inside: avoid; display: flex; gap: 24px; margin-bottom: 18px; }
    .ekmOrderTracking,
    .ekmOrderGiftcards,
    .ekmOrderActivityLog,
    .card,
    .umb-table-row { break-inside: avoid; box-shadow: none; }
    .umb-table { border-color: #bbb; overflow: visible; }
    .umb-table-cell { border-color: #ddd; color: #000; padding: 7px 8px; }
  }
`;
class L extends _(HTMLElement) {
  constructor() {
    super(...arguments);
    l(this, "api", new p());
    l(this, "result");
    l(this, "detail");
    l(this, "query", "");
    l(this, "segment", "all");
    l(this, "sort", "newest");
    l(this, "page", 1);
    l(this, "busy", !1);
    l(this, "error", "");
    l(this, "request", 0);
    l(this, "detailRequest", 0);
    l(this, "other", !1);
    l(this, "classic", !1);
    l(this, "opener");
  }
  connectedCallback() {
    if (super.connectedCallback(), this.classic = this.hasAttribute("classic"), this.classic)
      for (const e of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const t = this.getAttribute(e.toLowerCase());
        t && (d.filters[e] = t);
      }
    this.initialize();
  }
  disconnectedCallback() {
    this.request++, this.detailRequest++, super.disconnectedCallback();
  }
  async initialize() {
    var e;
    this.busy = !0, this.render();
    try {
      d.stores = await this.api.stores(), d.stores.some((t) => t.alias === d.filters.store) || (d.filters.store = ((e = d.stores[0]) == null ? void 0 : e.alias) || ""), await this.load();
    } catch {
      this.error = "Unable to load customers. Please try again.", this.busy = !1, this.render();
    }
  }
  params() {
    const e = d.filters;
    return new URLSearchParams({ store: e.store, start: e.dateFrom, end: e.dateTo, orderStatus: e.orderStatus, query: this.query, segment: this.segment, sort: this.sort, page: String(this.page) });
  }
  async get(e, t) {
    const a = await fetch(`/ekom/manager/customers${e}?${t}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!a.ok) throw new Error("Unable to load customer data. Check the filters and try again.");
    return a.json();
  }
  async load() {
    const e = ++this.request;
    this.detailRequest++, this.detail = void 0, this.result = void 0, this.error = "", this.busy = !0, this.render();
    try {
      if (!d.filters.store) {
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
    var n;
    const a = ++this.detailRequest;
    this.opener = e, this.error = "";
    try {
      const m = await this.get("/detail", new URLSearchParams({ store: d.filters.store, identity: e, page: String(t), other: String(this.other) }));
      if (a !== this.detailRequest || !this.isConnected) return;
      this.detail = m, this.render(), (n = this.querySelector("[data-close]")) == null || n.focus();
    } catch (m) {
      a === this.detailRequest && (this.error = String(m), this.render());
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
      const t = URL.createObjectURL(await e.blob()), a = document.createElement("a");
      a.href = t, a.download = "customers.csv", a.click(), setTimeout(() => URL.revokeObjectURL(t), 1e3);
    } catch (e) {
      this.error = String(e);
    } finally {
      this.busy = !1, this.render();
    }
  }
  amount(e, t) {
    return e.map((a) => {
      var u;
      const n = (u = d.stores.find((h) => h.alias === d.filters.store)) == null ? void 0 : u.currency;
      let m = a.currency;
      !/^[A-Z]{3}$/.test(m) && (n == null ? void 0 : n.currencyValue) === m && (m = n.isoCurrencySymbol || m);
      try {
        return i(new Intl.NumberFormat(void 0, { style: "currency", currency: m }).format(a[t]));
      } catch {
        return `${i(a[t].toLocaleString(void 0, { maximumFractionDigits: 2 }))} ${i(a.currency)}`;
      }
    }).join("<br>") || "—";
  }
  number(e) {
    return e == null ? "—" : i(e.toLocaleString(void 0, { maximumFractionDigits: 1 }));
  }
  date(e) {
    return e ? i(new Date(e).toLocaleDateString()) : "—";
  }
  card(e, t) {
    return `<div class="customer-card"><small>${e}</small><strong>${t}</strong></div>`;
  }
  render() {
    const e = d.filters, t = this.result;
    this.innerHTML = `<style>${S}
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
        <label>Store<select data-filter="store">${d.stores.map((a) => `<option value="${i(a.alias)}" ${a.alias === e.store ? "selected" : ""}>${i(a.title)}</option>`).join("")}</select></label>
        <label>Date From<input type="date" data-filter="dateFrom" value="${i(e.dateFrom)}"></label>
        <label>Date To<input type="date" data-filter="dateTo" value="${i(e.dateTo)}"></label>
        <button data-reload ${this.busy ? "disabled" : ""}>Refresh</button>
      </div>
      <p class="customer-muted">Customers who ordered in the selected period. Lifetime metrics include all qualifying orders in this store. Status filter: ${i(e.orderStatus.replace(/([a-z])([A-Z])/g, "$1 $2"))}.</p>
      ${this.error ? `<p role="alert">${i(this.error)}</p>` : ""}
      ${this.busy ? '<p role="status">Loading…</p>' : ""}
      ${t ? `<div class="customer-cards">${this.card("Customers", this.number(t.totals.customers))}${this.card("Returning", this.number(t.totals.returningPercent) + "%")}${this.card("Average lifetime spend", this.amount(t.spend, "averageLifetime"))}${this.card("Average orders per customer", this.number(t.totals.averageOrders))}${this.card("Average days between orders", this.number(t.totals.averageReorderDays))}</div>
      <p class="customer-muted">${t.missingIdentifiers} qualifying orders in this period have no ${t.identityField === "CustomerUsername" ? "username" : "email"} and are excluded. Spend uses recorded charged amounts, without refund adjustment.</p>` : ""}
      <div class="customer-panel"><form class="customer-toolbar" data-search>
        <label>Search<input name="query" value="${i(this.query)}" placeholder="Name, email or username"></label>
        <button type="submit">Search</button>
        <label>Customers<select data-segment>${["all", "new", "returning"].map((a) => `<option value="${a}" ${a === this.segment ? "selected" : ""}>${a[0].toUpperCase() + a.slice(1)}</option>`).join("")}</select></label>
        <label>Sort by<select data-sort>${[["newest", "Newest first order"], ["oldest", "Oldest first order"], ["lastOrder", "Latest order"], ["orders", "Most orders"], ["name", "Name"]].map(([a, n]) => `<option value="${a}" ${a === this.sort ? "selected" : ""}>${n}</option>`).join("")}</select></label>
        <button type="button" data-export ${this.busy || !(t != null && t.totals.customers) ? "disabled" : ""}>Export</button>
      </form><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Customer</th><th>First order</th><th>Orders</th><th>Lifetime spend</th><th>Average order</th><th>Last order</th><th>Days since last order</th></tr></thead><tbody>
      ${((t == null ? void 0 : t.customers) || []).map((a) => `<tr><td><button data-customer="${i(a.identity)}">${i(a.name || a.email || a.username || a.identity)}<span class="customer-badge">${a.orders > 1 ? "Returning" : "New"}</span><span class="customer-muted">${i(a.email || a.username || a.identity)}</span></button></td><td>${this.date(a.firstOrder)}</td><td>${a.orders}</td><td>${this.amount(a.spend, "total")}</td><td>${this.amount(a.spend, "averageOrder")}</td><td>${this.date(a.lastOrder)}<span class="customer-muted">${i(a.lastStatus || "")}</span></td><td>${a.daysSinceLastOrder}</td></tr>`).join("") || '<tr><td colspan="7">No customers found.</td></tr>'}
      </tbody></table></div><div class="customer-pagination"><button data-page="${this.page - 1}" ${this.page <= 1 || this.busy ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${Math.max(1, (t == null ? void 0 : t.totalPages) || 0)}</span><button data-page="${this.page + 1}" ${this.page >= ((t == null ? void 0 : t.totalPages) || 0) || this.busy ? "disabled" : ""}>Next</button></div></div>
      ${this.detail ? this.renderDetail(this.detail) : ""}</section>`, this.bind();
  }
  renderDetail(e) {
    const t = e.customer;
    return `<div class="customer-overlay"><section role="dialog" aria-modal="true" aria-label="Customer details" class="customer-dialog"><header class="customer-heading"><div><h2>${i(t.name || t.identity)} <span class="customer-badge">${t.orders > 1 ? "Returning" : "New"}</span></h2><p>${i([t.email, t.username].filter(Boolean).join(" · "))}</p></div><button data-close aria-label="Close customer details">Close</button></header>
      <div class="customer-cards">${this.card("Lifetime spend", this.amount(t.spend, "total"))}${this.card("Orders", String(t.orders))}${this.card("Average order", this.amount(t.spend, "averageOrder"))}${this.card("First order", this.date(t.firstOrder))}${this.card("Days since last order", String(t.daysSinceLastOrder))}${this.card("Average reorder interval (days)", this.number(t.averageReorderDays))}</div>
      <div class="customer-detail-grid"><div><h3>Orders <small>(${e.count})</small></h3><label><input type="checkbox" data-other ${this.other ? "checked" : ""}> Show other orders (excluded from lifetime metrics)</label><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Order</th><th>Status</th><th>Created</th><th>Paid</th><th>Total</th></tr></thead><tbody>${e.orders.map((a) => `<tr><td><button data-order="${i(a.uniqueId)}">#${i(a.orderNumber || String(a.referenceId))}</button></td><td>${i(a.orderStatusCol)}</td><td>${this.date(a.createDate)}</td><td>${this.date(a.paidDate)}</td><td>${i(a.formattedTotal)}</td></tr>`).join("") || '<tr><td colspan="5">No orders.</td></tr>'}</tbody></table></div><div class="customer-pagination"><button data-detail-page="${e.page - 1}" ${e.page <= 1 ? "disabled" : ""}>Previous</button><span>${e.page} / ${Math.max(1, e.totalPages)}</span><button data-detail-page="${e.page + 1}" ${e.page >= e.totalPages ? "disabled" : ""}>Next</button></div></div>
      <aside class="customer-snapshot"><h3>Latest order snapshot</h3><p class="customer-muted">Recorded on order #${i(e.snapshot.orderNumber)} · ${this.date(e.snapshot.date)}. Historical details, not a current customer profile.</p>${e.snapshot.unavailable ? "<p>Saved details could not be read.</p>" : ""}<dl>${Object.entries(e.snapshot.fields).map(([a, n]) => `<dt>${i(a)}</dt><dd>${i(n)}</dd>`).join("") || "<dd>No additional details recorded.</dd>"}</dl></aside></div></section></div>`;
  }
  bind() {
    var e, t, a, n, m, u, h, x;
    this.querySelectorAll("[data-filter]").forEach((o) => o.addEventListener("change", () => {
      const c = o.dataset.filter;
      d.filters[c] = o.value, this.dispatchEvent(new CustomEvent("customer-filters-changed", { bubbles: !0, detail: { ...d.filters } })), this.page = 1, this.load();
    })), (e = this.querySelector("[data-search]")) == null || e.addEventListener("submit", (o) => {
      var c;
      o.preventDefault(), this.query = ((c = this.querySelector("[name=query]")) == null ? void 0 : c.value) || "", this.page = 1, this.load();
    }), (t = this.querySelector("[data-segment]")) == null || t.addEventListener("change", (o) => {
      this.segment = o.target.value, this.page = 1, this.load();
    }), (a = this.querySelector("[data-sort]")) == null || a.addEventListener("change", (o) => {
      this.sort = o.target.value, this.page = 1, this.load();
    }), (n = this.querySelector("[data-reload]")) == null || n.addEventListener("click", () => void this.load()), (m = this.querySelector("[data-export]")) == null || m.addEventListener("click", () => void this.export()), (u = this.querySelector("[data-close]")) == null || u.addEventListener("click", () => this.close()), (h = this.querySelector("[data-other]")) == null || h.addEventListener("change", () => {
      this.other = !this.other, this.detail && this.open(this.detail.customer.identity);
    }), this.querySelectorAll("[data-customer]").forEach((o) => o.addEventListener("click", () => {
      this.other = !1, this.open(o.dataset.customer);
    })), this.querySelectorAll("[data-page]").forEach((o) => o.addEventListener("click", () => {
      this.page = Number(o.dataset.page), this.load();
    })), this.querySelectorAll("[data-detail-page]").forEach((o) => o.addEventListener("click", () => {
      this.detail && this.open(this.detail.customer.identity, Number(o.dataset.detailPage));
    })), this.querySelectorAll("[data-order]").forEach((o) => o.addEventListener("click", () => {
      if (this.classic) this.dispatchEvent(new CustomEvent("customer-open-order", { bubbles: !0, detail: { orderId: o.dataset.order } }));
      else {
        d.pendingOrderId = o.dataset.order;
        const c = document.createElement("a");
        c.href = location.pathname.replace(/\/customers\/?$/, "/orders") + "?orderId=" + encodeURIComponent(o.dataset.order || ""), this.appendChild(c), c.click(), c.remove();
      }
    })), (x = this.querySelector("[role=dialog]")) == null || x.addEventListener("keydown", (o) => {
      if (o.key === "Escape" && (o.preventDefault(), this.close()), o.key !== "Tab") return;
      const c = Array.from(this.querySelectorAll("[role=dialog] button:not(:disabled),[role=dialog] input")), g = c[0], f = c[c.length - 1];
      o.shiftKey && document.activeElement === g ? (o.preventDefault(), f == null || f.focus()) : !o.shiftKey && document.activeElement === f && (o.preventDefault(), g == null || g.focus());
    });
  }
}
customElements.define("ekom-customers-section-view", L);
export {
  L as EkomCustomersSectionViewElement,
  L as default
};
