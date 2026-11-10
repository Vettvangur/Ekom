var E = Object.defineProperty;
var A = (o, a, t) => a in o ? E(o, a, { enumerable: !0, configurable: !0, writable: !0, value: t }) : o[a] = t;
var m = (o, a, t) => A(o, typeof a != "symbol" ? a + "" : a, t);
const O = /* @__PURE__ */ new Date(), q = new Date(O.getFullYear(), 0, 1), l = {
  filters: {
    dateFrom: $(q),
    dateTo: $(O),
    orderStatus: "AllOrders",
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
class k {
  async changeOrderStates(a, t, e, r, s) {
    return this.postBody(`/ekom/manager/orderStates/${encodeURIComponent(a)}`, { expectedRevision: t, action: e, notify: r, fulfilledQuantities: s });
  }
  async printingEnabled() {
    try {
      const a = await fetch("/ekom/printer/manager/capabilities", { credentials: "same-origin" });
      return a.ok && (await a.json()).enabled === !0;
    } catch {
      return !1;
    }
  }
  async config() {
    return this.getJson("/ekom/backoffice/Config");
  }
  async searchOrders(a, t, e = 20) {
    return this.getJson("/ekom/manager/SearchOrders", {
      ...a,
      start: a.dateFrom,
      end: a.dateTo,
      page: t,
      pageSize: e
    });
  }
  async exportOrders(a, t, e) {
    return this.getBlob("/ekom/manager/ExportOrders", {
      ...a,
      start: a.dateFrom,
      end: a.dateTo,
      page: 1,
      pageSize: t,
      includeOrderLines: e
    });
  }
  async orderReviews(a) {
    const t = [];
    for (let e = 0; ; e += 200) {
      const r = await this.getJson(`/ekom/manager/orders/${encodeURIComponent(a)}/reviews`, { skip: e, take: 200 });
      if (t.push(...r), r.length < 200) return t;
    }
  }
  async reviewOrder(a, t, e, r) {
    return this.postBody(`/ekom/manager/orders/${encodeURIComponent(a)}/reviews/${encodeURIComponent(t)}/review`, { comment: e, resolved: r });
  }
  async statusList() {
    return this.getJson("/ekom/manager/StatusList");
  }
  async stores() {
    const [a, t] = await Promise.all([this.getJson("/ekom/manager/stores"), this.getJson("/ekom/manager/store-currencies")]);
    return a.map((e) => {
      var r;
      return { ...e, currencies: ((r = t.find((s) => s.alias === e.alias)) == null ? void 0 : r.currencies) || [] };
    });
  }
  async paymentProviders(a) {
    return this.getJson(`/ekom/manager/paymentproviders/${encodeURIComponent(a)}`);
  }
  async shippingProviders(a) {
    return this.getJson(`/ekom/manager/shippingproviders/${encodeURIComponent(a)}`);
  }
  async orderInfo(a) {
    return this.getJson(`/ekom/manager/OrderInfo/${encodeURIComponent(a)}`);
  }
  async orderLogs(a) {
    return this.getJson(`/ekom/manager/OrderLogs/${encodeURIComponent(a)}`);
  }
  async orderActions(a) {
    return this.getJson(`/ekom/manager/OrderActions/${encodeURIComponent(a)}`);
  }
  async orderLockStatus(a) {
    return this.getJson(`/ekom/manager/orderLockStatus/${encodeURIComponent(a)}`);
  }
  async releaseOrderLock(a, t) {
    return this.postBody(`/ekom/manager/releaseOrderLock/${encodeURIComponent(a)}`, { releaseToken: t });
  }
  async giftcardRecovery(a) {
    return this.getJson(`/ekom/manager/giftcardRecovery/${encodeURIComponent(a)}`);
  }
  async releaseGiftcardRecovery(a, t, e, r) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(a)}/release`, { attemptId: t, claimId: e, token: r });
  }
  async reconcileGiftcardRecovery(a, t, e) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(a)}/reconcile`, { attemptId: t, token: e });
  }
  async executeOrderAction(a, t) {
    const e = await fetch(`/ekom/manager/OrderActions/${encodeURIComponent(a)}/${encodeURIComponent(t)}`, {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json, application/octet-stream" }
    });
    if (!e.ok)
      throw await k.createError(e, "Order action failed.");
    return e;
  }
  async changeOrderStatus(a, t, e) {
    return this.postJson("/ekom/manager/changeOrderStatus", { orderId: a, orderStatus: t, notify: e });
  }
  async updateCustomerInformation(a, t, e) {
    return this.postBody("/ekom/manager/UpdateCustomerInformation", { orderId: a, customer: t, shipping: e });
  }
  async addOrderLine(a, t, e, r) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(a)}/OrderLines`, { productId: t, variantId: e, quantity: r });
  }
  async updateShippingProvider(a, t, e) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(a)}/ShippingProvider`, { providerId: t, customData: e });
  }
  async removeOrderLine(a, t) {
    const e = await fetch(`/ekom/manager/Order/${encodeURIComponent(a)}/OrderLines/${encodeURIComponent(t)}`, {
      method: "DELETE",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!e.ok)
      throw await k.createError(e, "Error removing order line.");
    return e.json();
  }
  async charts(a, t) {
    return this.getJson("/ekom/manager/charts", {
      start: a.dateFrom,
      end: a.dateTo,
      orderStatus: a.orderStatus,
      store: a.store,
      currency: a.currency
    }, t);
  }
  async mostSoldProducts(a, t, e = 20, r) {
    return this.getJson("/ekom/manager/MostSoldProducts", {
      start: a.dateFrom,
      end: a.dateTo,
      orderStatus: a.orderStatus,
      store: a.store,
      currency: a.currency,
      page: t,
      pageSize: e
    }, r);
  }
  async getJson(a, t, e) {
    const r = await fetch(a + v(t), {
      credentials: "same-origin",
      signal: e,
      headers: { Accept: "application/json" }
    });
    if (!r.ok)
      throw await k.createError(r, "Request failed.");
    return r.json();
  }
  async getBlob(a, t) {
    const e = await fetch(a + v(t), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!e.ok)
      throw await k.createError(e, "Request failed.");
    return e.blob();
  }
  async postJson(a, t) {
    const e = await fetch(a + v(t), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!e.ok)
      throw await k.createError(e, "Request failed.");
    return e.json();
  }
  async postBody(a, t) {
    const e = await fetch(a, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json"
      },
      body: JSON.stringify(t)
    });
    if (!e.ok)
      throw await k.createError(e, "Request failed.");
    return e.json();
  }
  static async createError(a, t) {
    var s, d;
    if (a.status === 401 || a.redirected && /\/Account\/Login/i.test(a.url))
      return new Error("Your session has expired. Sign in to Umbraco and reload the manager.");
    if ((d = (s = a.headers) == null ? void 0 : s.get("content-type")) != null && d.includes("text/html"))
      return new Error(`${t} Status ${a.status}. Reload the manager and try again.`);
    const e = await a.text();
    let r = e || t;
    try {
      r = JSON.parse(e).message || r;
    } catch {
    }
    return new Error(r || `${t} Status ${a.status}.`);
  }
}
function v(o) {
  if (!o)
    return "";
  const a = new URLSearchParams();
  for (const [e, r] of Object.entries(o))
    a.set(e, r == null ? "" : String(r));
  const t = a.toString();
  return t ? `?${t}` : "";
}
function $(o) {
  const a = String(o.getMonth() + 1).padStart(2, "0"), t = String(o.getDate()).padStart(2, "0");
  return `${o.getFullYear()}-${a}-${t}`;
}
function n(o) {
  return String(o ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
function P(o) {
  return o.enumValue || o.value || "";
}
const R = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`, C = `
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

  ${R}
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
`, _ = ":is(ekom-orders-section-view, ekom-analytics-section-view, ekom-customers-section-view, ekom-printing-section-view)", S = {
  "#fff": "--ekom-surface",
  "#f6f4f4": "--ekom-bg",
  "#f6f6f8": "--ekom-surface-alt",
  "#f8fafc": "--ekom-surface-alt",
  "#eef3f8": "--ekom-surface-alt",
  "#edf8f0": "--ekom-surface-alt",
  "#fff4e5": "--ekom-surface-alt",
  "#000": "--ekom-text",
  "#111": "--ekom-text",
  "#1b264f": "--ekom-text",
  "#46607a": "--ekom-muted",
  "#515054": "--ekom-muted",
  "#666": "--ekom-muted",
  "#686b7d": "--ekom-muted",
  "#d8d7d9": "--ekom-border",
  "#ddd": "--ekom-border",
  "#bbb": "--ekom-border",
  "#e6e8ef": "--ekom-border",
  "#e9e9eb": "--ekom-border",
  "#eee": "--ekom-border",
  "#f0f0f0": "--ekom-border",
  "#247f45": "--ekom-positive",
  "#ad5f00": "--ekom-warning"
}, D = C.replaceAll(":host", "&").replace(/#[0-9a-f]{3,8}\b/gi, (o) => S[o.toLowerCase()] ? `var(${S[o.toLowerCase()]})` : o), j = `${_} {
  ${C.replaceAll(":host", "&")}
}
${_}:not([classic]) {
  ${D}
  --ekom-bg: var(--uui-color-background, #f0f1f4);
  --ekom-surface: var(--uui-color-surface, #fff);
  --ekom-surface-alt: var(--uui-color-surface-alt, #f6f7f9);
  --ekom-text: var(--uui-color-text, #191b24);
  --ekom-muted: var(--uui-color-text-alt, #676b7b);
  --ekom-border: var(--uui-color-border, #e1e3e8);
  --ekom-accent: color-mix(in srgb, var(--uui-color-positive-standalone, #10694a) 55%, var(--uui-color-text, #191b24));
  --ekom-positive: color-mix(in srgb, var(--uui-color-positive-standalone, #0b5238) 55%, var(--uui-color-text, #191b24));
  --ekom-warning: color-mix(in srgb, var(--uui-color-warning-standalone, #7a4e06) 55%, var(--uui-color-text, #191b24));
  --ekom-negative: color-mix(in srgb, var(--uui-color-danger-standalone, #8c1d18) 55%, var(--uui-color-text, #191b24));
  display: block; height: 100%; color: var(--ekom-text); font-family: inherit;
  &, *, *::before, *::after { box-sizing: border-box; }
  [hidden] { display: none !important; }
  h1, h2, h3, h4, p { color: inherit; }
  .ekmManager { background: var(--ekom-bg); color: var(--ekom-text); }
  .ekmManager__body { padding: 28px; max-width: 1800px; margin-inline: auto; }
  .ekom-heading { display: flex; align-items: center; justify-content: space-between; gap: 20px; margin-bottom: 24px; }
  .ekom-heading h1 { font-size: 26px; font-weight: 800; margin: 0 0 6px; letter-spacing: -.02em; }
  .ekom-heading p, .ekom-muted { margin: 0; color: var(--ekom-muted); font-size: 13px; }
  .cards { gap: 16px; margin-bottom: 24px; }
  .card, .ekmSummaryCard { background: var(--ekom-surface); border: 1px solid var(--ekom-border); border-radius: 12px; box-shadow: none; padding: 20px; }
  .ekmSummaryCard__label { color: var(--ekom-muted); font-size: 11px; letter-spacing: .08em; }
  .ekmSummaryCard__value { color: var(--ekom-text); font-size: 28px; margin: 0; }
  .umb-sub-header, .ekmToolbar { background: var(--ekom-surface); border: 1px solid var(--ekom-border); border-radius: 12px; padding: 18px; }
  .ekmManager__filters { gap: 14px; grid-template-columns: repeat(4, minmax(120px, 1fr)); }
  .ekmManager__filter { color: var(--ekom-muted); font-size: 12px; }
  .ekmManager__search { grid-column: 1 / -1; justify-content: flex-end; }
  input:not([type='checkbox']), select, textarea { background: var(--ekom-surface); color: var(--ekom-text); border: 1px solid var(--ekom-border); border-radius: 7px; padding: 9px 12px; min-height: 38px; }
  button, .btn { border-radius: 7px; background: var(--ekom-surface); color: var(--ekom-text); border-color: var(--ekom-border); font-weight: 600; }
  button:hover:not(:disabled) { background: var(--ekom-surface-alt); }
  button:disabled { cursor: not-allowed; opacity: .55; }
  :is(button, input, select, textarea, summary, a):focus-visible { outline: 2px solid var(--uui-color-focus, var(--ekom-accent)); outline-offset: 3px; }
  .btn-primary, .btn-success { background: var(--uui-color-positive, #10694a); border-color: var(--uui-color-positive, #10694a); color: var(--uui-color-positive-contrast, #fff); }
  .btn-primary:hover:not(:disabled), .btn-success:hover:not(:disabled) { background: var(--uui-color-positive-emphasis, #0b5238); }
  .btn-reset { background: transparent; border: 0; color: var(--ekom-muted); }
  .umb-table { background: var(--ekom-surface); border: 1px solid var(--ekom-border); border-radius: 12px; width: 100%; }
  .umb-table-head { background: var(--ekom-surface-alt); color: var(--ekom-muted); font-size: 11px; text-transform: uppercase; letter-spacing: .05em; }
  .umb-table-cell { border-color: var(--ekom-border); color: inherit; padding: 15px 12px; vertical-align: middle; }
  .umb-table-body .umb-table-row:hover { background: var(--ekom-surface-alt); }
  .ekom-badge { display: inline-flex; align-items: center; gap: 6px; border-radius: 6px; padding: 5px 8px; white-space: nowrap; font-size: 12px; font-weight: 600; color: var(--ekom-muted); background: color-mix(in srgb, var(--ekom-muted) 12%, var(--ekom-surface)); }
  .ekom-badge::before { content: ''; width: 6px; height: 6px; border-radius: 50%; background: currentColor; }
  .ekom-badge--positive { color: var(--ekom-positive); background: color-mix(in srgb, var(--ekom-positive) 12%, var(--ekom-surface)); }
  .ekom-badge--warning { color: var(--ekom-warning); background: color-mix(in srgb, var(--ekom-warning) 12%, var(--ekom-surface)); }
  .ekom-badge--negative { color: var(--ekom-negative); background: color-mix(in srgb, var(--ekom-negative) 12%, var(--ekom-surface)); }
  .ekom-statuses { display: flex; flex-wrap: wrap; gap: 16px; margin: 16px 0; }
  .ekom-statuses > div { display: grid; gap: 6px; }
  .ekom-statuses dt { color: var(--ekom-muted); font-size: 11px; text-transform: uppercase; letter-spacing: .06em; }
  .ekom-statuses dd { margin: 0; }
  .ekom-legacy-status { margin-top: 8px; font-size: 12px; }
  .ekom-legacy-status summary { cursor: pointer; color: var(--ekom-muted); }
  .ekom-legacy-status select { margin-top: 8px; max-width: 190px; }
  .ekmOverlay { background: rgb(0 0 0 / .35); }
  .ekmOverlay__panel, .ekmOverlay__header, .ekmCustomerInformationModal__panel { background: var(--ekom-surface); color: var(--ekom-text); border-color: var(--ekom-border); }
  .ekmOverlay__header { border-bottom: 1px solid var(--ekom-border); }
  .ekom-order-drawer { padding: 0; display: flex; justify-content: flex-end; }
  .ekom-order-drawer .ekmOrderOverlay__panel { width: min(1060px, 94vw); max-width: none; min-height: 100%; margin: 0; border-radius: 0; box-shadow: -12px 0 40px rgb(0 0 0 / .15); }
  .ekom-order-drawer .ekmOverlay__header { position: sticky; top: 0; z-index: 2; padding: 20px 24px; }
  .ekmOverlay__content { padding: 24px; }
  .ekom-order-tabs { display: flex; gap: 4px; overflow-x: auto; border-bottom: 1px solid var(--ekom-border); margin: 20px 0; }
  .ekom-order-tabs button { background: transparent; border: 0; border-bottom: 2px solid transparent; border-radius: 0; padding: 12px; white-space: nowrap; color: var(--ekom-muted); }
  .ekom-order-tabs button[aria-selected='true'] { color: var(--ekom-accent); border-bottom-color: var(--ekom-accent); }
  .ekmOrderTracking, .ekmOrderGiftcards, .ekmOrderActivityLog, .ekmOrderCustomData, .ekmGiftcardRecovery__claim { color: var(--ekom-text); background: var(--ekom-surface-alt); border-color: var(--ekom-border); border-radius: 10px; }
  .ekmOrderCustomData__key, .ekmOrderActivityLog__date, .ekmOrderLineThumbnail { color: var(--ekom-muted); }
  .ekmOrderCustomData__value, .ekmOrderActivityLog__toggle { color: var(--ekom-text); }
  .ekmOrderLineThumbnail, .ekmOrderLineThumbnail img { background: var(--ekom-surface-alt); border-color: var(--ekom-border); }
  .pagination li.active button { background: var(--uui-color-interactive); color: var(--uui-color-interactive-contrast); }
  @media (max-width: 1024px) { .ekmManager__filters { grid-template-columns: repeat(2, minmax(0, 1fr)); } }
  @media (max-width: 720px) {
    .ekmManager__body { padding: 16px; }
    .ekom-order-drawer .ekmOrderOverlay__panel { width: 100%; }
    .umb-table-row { background: var(--ekom-surface); border-color: var(--ekom-border); box-shadow: none; }
    .ekmManager__filters { grid-template-columns: 1fr; }
    .ekmOverlay__content { padding: 16px; }
  }
  @media print { --ekom-text: #000; --ekom-muted: #444; --ekom-surface: #fff; --ekom-surface-alt: #fff; --ekom-border: #bbb; .ekom-order-tabs, .ekom-legacy-status { display: none; } }
}`;
function w(o) {
  const a = ["Paid", "Fulfilled", "Closed"].includes(o || "") ? "positive" : ["Failed", "Cancelled"].includes(o || "") ? "negative" : ["Pending", "PartiallyPaid", "PartiallyFulfilled", "PartiallyRefunded"].includes(o || "") ? "warning" : "neutral", t = o ? o.replace(/([a-z])([A-Z])/g, "$1 $2") : "Needs review";
  return `<span class="ekom-badge ekom-badge--${a}">${n(t)}</span>`;
}
const T = [
  { key: "AllOrders", label: "All" },
  { key: "NeedsReview", label: "Needs review" },
  { key: "states:Open;;Unfulfilled,Picking,Picked,Packed,PartiallyFulfilled", label: "Unfulfilled" },
  { key: "states:Open;Pending,Authorized,PartiallyPaid,Failed;", label: "Unpaid" },
  { key: "states:Open;;", label: "Open" },
  { key: "states:Closed;;", label: "Closed" }
];
function M() {
  var t;
  const o = l.stores.find((e) => e.alias === l.filters.store), a = (o == null ? void 0 : o.currencies) || [];
  if (!a.includes(l.filters.currency || "")) {
    const e = (t = o == null ? void 0 : o.currency) == null ? void 0 : t.isoCurrencySymbol;
    l.filters.currency = a.find((r) => r === e) || a[0] || "";
  }
}
function F(o = "data-field") {
  const a = l.stores.find((e) => e.alias === l.filters.store), t = l.filters.currency || "";
  return `<div class="ekom-store-switch"><span>Store</span><details class="ekom-dropdown ekom-store-currency"><summary aria-label="Store and currency"><strong>${n((a == null ? void 0 : a.title) || "Select store")}</strong>${t ? `<span class="ekom-currency-code">${n(t)}</span>` : ""}<span class="ekom-store-chevron">▾</span></summary><div class="ekom-currency-popover"><div class="ekom-currency-heading"><strong>Store ·<br>Currency</strong><span>Amounts shown in the selected currency</span></div>${l.stores.map((e) => `<section class="ekom-currency-store ${e.alias === (a == null ? void 0 : a.alias) ? "is-selected" : ""}" aria-label="${n(e.title)}"><h3>${n(e.title)}</h3><div class="ekom-currency-choices">${(e.currencies || []).map((r) => `<button type="button" data-header-store="${n(e.alias)}" data-header-currency="${n(r)}" aria-label="${n(e.title)} — ${n(r)}" aria-pressed="${e.alias === (a == null ? void 0 : a.alias) && r === t}">${n(r)}${e.alias === (a == null ? void 0 : a.alias) && r === t ? '<span aria-hidden="true">✓</span>' : ""}</button>`).join("") || '<span class="ekom-currency-empty">No currencies configured or recorded</span>'}</div></section>`).join("")}</div></details><select hidden aria-label="Store" ${o}="store">${l.stores.map((e) => `<option value="${n(e.alias)}" ${e.alias === (a == null ? void 0 : a.alias) ? "selected" : ""}>${n(e.title)}</option>`).join("")}</select></div>`;
}
function z(o, a = "data-field", t = !0) {
  if (typeof document < "u" && !document.querySelector("link[data-ekom-fonts]")) {
    const s = document.createElement("link");
    s.rel = "stylesheet", s.href = "/App_Plugins/Ekom/dist/manager-fonts.css", s.dataset.ekomFonts = "", document.head.append(s);
  }
  const e = ["orders", "analytics", "customers", ...l.printingEnabled || o === "printing" ? ["printing"] : []], r = typeof location > "u" ? "/umbraco/section/ekommanager" : location.pathname.replace(/\/view\/(orders|analytics|customers|printing)\/?$/, "").replace(/\/$/, "");
  return `<header class="ekom-design-header"><img src="/App_Plugins/Ekom/dist/ekom-logo.svg" alt="Ekom" class="ekom-design-logo"><nav aria-label="Ekom sections" class="ekom-section-switch">${e.map((s) => `<a href="${n(r + "/view/" + s)}" ${s === o ? 'aria-current="page"' : ""}>${s[0].toUpperCase() + s.slice(1)}</a>`).join("")}</nav>${t ? F(a) : '<span class="ekom-store-switch">Accessible stores</span>'}</header>`;
}
function U(o) {
  var t;
  const a = [...T];
  return a.some((e) => e.key === o) || a.push({ key: o, label: o.startsWith("states:") ? "Custom status filters" : ((t = l.statusList.find((e) => P(e) === o)) == null ? void 0 : t.label) || o }), a.map((e) => `<option value="${n(e.key)}" ${e.key === o ? "selected" : ""}>${n(e.label)}</option>`).join("");
}
function I(o = /* @__PURE__ */ new Date()) {
  const a = (r) => `${r.getFullYear()}-${String(r.getMonth() + 1).padStart(2, "0")}-${String(r.getDate()).padStart(2, "0")}`, t = (r) => new Date(o.getFullYear(), o.getMonth(), o.getDate() + r), e = a(o);
  return [
    { label: "Today", from: e, to: e },
    { label: "Yesterday", from: a(t(-1)), to: a(t(-1)) },
    { label: "Last 7 days", from: a(t(-6)), to: e },
    { label: "Last 30 days", from: a(t(-29)), to: e },
    { label: "This month", from: a(new Date(o.getFullYear(), o.getMonth(), 1)), to: e },
    { label: "Last month", from: a(new Date(o.getFullYear(), o.getMonth() - 1, 1)), to: a(new Date(o.getFullYear(), o.getMonth(), 0)) },
    { label: "This quarter", from: a(new Date(o.getFullYear(), Math.floor(o.getMonth() / 3) * 3, 1)), to: e },
    { label: "This year", from: a(new Date(o.getFullYear(), 0, 1)), to: e }
  ];
}
function B() {
  return `<ekom-date-range from="${n(l.filters.dateFrom)}" to="${n(l.filters.dateTo)}"></ekom-date-range>`;
}
function N(o, a) {
  var t, e;
  G(), (t = o.querySelector(".ekom-store-currency")) == null || t.addEventListener("keydown", (r) => {
    var d;
    if (r.key !== "Escape") return;
    const s = r.currentTarget;
    s.open = !1, (d = s.querySelector("summary")) == null || d.focus(), r.stopPropagation();
  }), o.querySelectorAll("[data-header-store]").forEach((r) => r.addEventListener("click", () => {
    var d;
    const s = o.querySelector('[data-field="store"], [data-filter="store"]');
    s && (l.filters.currency = r.dataset.headerCurrency || "", (d = r.closest("details")) == null || d.removeAttribute("open"), s.value = r.dataset.headerStore || "", s.dispatchEvent(new Event("change", { bubbles: !0 })));
  })), o.querySelectorAll("[data-shared-status]").forEach((r) => r.addEventListener("change", () => {
    const s = [0, 1, 2].map((d) => Array.from(o.querySelectorAll(`[data-shared-status="${d}"]:checked`), (u) => u.value).join(","));
    l.filters.orderStatus = s.some(Boolean) ? "states:" + s.join(";") : "AllOrders", a();
  })), (e = o.querySelector("ekom-date-range")) == null || e.addEventListener("date-range-apply", (r) => {
    const s = r.detail;
    l.filters.dateFrom = s.from, l.filters.dateTo = s.to, a();
  });
}
function G() {
  if (typeof customElements > "u" || !customElements.get || customElements.get("ekom-date-range")) return;
  class o extends HTMLElement {
    constructor() {
      super(...arguments);
      m(this, "root", this.attachShadow({ mode: "open" }));
      m(this, "open", !1);
      m(this, "from", "");
      m(this, "to", "");
      m(this, "month", 0);
      m(this, "reposition", () => {
        const e = this.root.querySelector(".popover"), r = this.root.querySelector(".anchor"), s = this.ownerDocument.defaultView;
        if (!e || !r || !s) return;
        if (s.innerWidth <= 650) {
          e.style.left = "";
          return;
        }
        const d = r.getBoundingClientRect().left;
        e.style.left = `${Math.min(0, Math.max(24 - d, s.innerWidth - 24 - d - e.getBoundingClientRect().width))}px`;
      });
      m(this, "outside", (e) => {
        this.open && !e.composedPath().includes(this) && this.close(!1);
      });
    }
    connectedCallback() {
      var e;
      this.render(), this.ownerDocument.addEventListener("pointerdown", this.outside), (e = this.ownerDocument.defaultView) == null || e.addEventListener("resize", this.reposition);
    }
    disconnectedCallback() {
      var e;
      this.ownerDocument.removeEventListener("pointerdown", this.outside), (e = this.ownerDocument.defaultView) == null || e.removeEventListener("resize", this.reposition);
    }
    reset() {
      this.from = this.getAttribute("from") || "", this.to = this.getAttribute("to") || "";
      const e = /^\d{4}-\d{2}-\d{2}$/.test(this.from) ? /* @__PURE__ */ new Date(`${this.from}T12:00:00`) : /* @__PURE__ */ new Date();
      this.month = e.getFullYear() * 12 + e.getMonth();
    }
    close(e = !0) {
      var r;
      this.open = !1, this.render(), e && ((r = this.root.querySelector("[data-toggle]")) == null || r.focus());
    }
    date(e) {
      return e ? `${e.slice(8, 10)}.${e.slice(5, 7)}.${e.slice(0, 4)}` : "Pick a date";
    }
    calendar(e) {
      const r = new Date(Math.floor(e / 12), e % 12, 1), s = (r.getDay() + 6) % 7, d = new Date(r.getFullYear(), r.getMonth() + 1, 0).getDate();
      return `<section class="month"><h3>${r.toLocaleDateString("en", { month: "long", year: "numeric" })}</h3><div class="days">${["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"].map((u) => `<span class="weekday">${u}</span>`).join("")}${"<span></span>".repeat(s)}${Array.from({ length: d }, (u, g) => {
        const h = `${r.getFullYear()}-${String(r.getMonth() + 1).padStart(2, "0")}-${String(g + 1).padStart(2, "0")}`, f = h === this.from || h === this.to, b = this.from && this.to && h > this.from && h < this.to;
        return `<button type="button" data-day="${h}" aria-label="${h}" aria-pressed="${f || !!b}" class="day ${f ? "edge" : b ? "inside" : ""} ${h === this.from ? "start" : ""} ${h === this.to ? "end" : ""} ${!this.to || this.to === this.from ? "single" : ""}">${g + 1}</button>`;
      }).join("")}</div></section>`;
    }
    render() {
      var u, g, h, f, b;
      const e = this.getAttribute("from") || "", r = this.getAttribute("to") || "", s = I(), d = ((u = s.find((c) => c.from === e && c.to === r)) == null ? void 0 : u.label) || "Custom";
      L(this.root, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/date-range.css"><div class="range"><span class="label">Date range</span><div class="anchor"><button type="button" class="trigger" data-toggle aria-haspopup="dialog" aria-expanded="${this.open}"><strong>${d}</strong><span class="dates">${this.date(e)} – ${this.date(r)}</span><span class="chevron">▾</span></button>${this.open ? `<div class="popover" role="dialog" aria-label="Choose date range"><nav aria-label="Date presets">${s.map((c, i) => `<button type="button" data-preset="${i}" aria-pressed="${c.from === this.from && c.to === this.to}">${c.label}<span>${c.from === this.from && c.to === this.to ? "✓" : ""}</span></button>`).join("")}</nav><div class="custom"><div class="draft"><div class="${this.from ? "" : "active"}"><span>From</span><strong>${this.date(this.from)}</strong></div><span>→</span><div class="${this.from && !this.to ? "active" : ""}"><span>To</span><strong>${this.date(this.to)}</strong></div></div><div class="calendars"><button type="button" class="previous" data-month="-1" aria-label="Previous month">‹</button><button type="button" class="next" data-month="1" aria-label="Next month">›</button>${this.calendar(this.month)}${this.calendar(this.month + 1)}</div><footer><span>${this.from && this.to ? `${this.date(this.from)} – ${this.date(this.to)}` : "Select an end date"}</span><button type="button" data-cancel>Cancel</button><button type="button" class="apply" data-apply ${!this.from || !this.to ? "disabled" : ""}>Apply</button></footer></div></div>` : ""}</div></div>`), (g = this.root.querySelector("link")) == null || g.addEventListener("load", this.reposition), this.reposition(), (h = this.root.querySelector("[data-toggle]")) == null || h.addEventListener("click", () => {
        var c;
        if (this.open) {
          this.close();
          return;
        }
        this.reset(), this.open = !0, this.render(), (c = this.root.querySelector("[data-preset]")) == null || c.focus();
      }), (f = this.root.querySelector("[data-cancel]")) == null || f.addEventListener("click", () => this.close()), (b = this.root.querySelector("[data-apply]")) == null || b.addEventListener("click", () => {
        !this.from || !this.to || this.from > this.to || (this.setAttribute("from", this.from), this.setAttribute("to", this.to), this.close(), this.dispatchEvent(new CustomEvent("date-range-apply", { bubbles: !0, composed: !0, detail: { from: this.from, to: this.to } })));
      }), this.root.querySelectorAll("[data-preset]").forEach((c) => c.addEventListener("click", () => {
        var y;
        const i = s[Number(c.dataset.preset)];
        this.from = i.from, this.to = i.to;
        const p = /* @__PURE__ */ new Date(`${this.from}T12:00:00`);
        this.month = p.getFullYear() * 12 + p.getMonth(), this.render(), (y = this.root.querySelector(`[data-preset="${c.dataset.preset}"]`)) == null || y.focus();
      })), this.root.querySelectorAll("[data-month]").forEach((c) => c.addEventListener("click", () => {
        var i;
        this.month += Number(c.dataset.month), this.render(), (i = this.root.querySelector(`[data-month="${c.dataset.month}"]`)) == null || i.focus();
      })), this.root.querySelectorAll("[data-day]").forEach((c) => c.addEventListener("click", () => {
        var p;
        const i = c.dataset.day;
        !this.from || this.to || i < this.from ? (this.from = i, this.to = "") : this.to = i, this.render(), (p = this.root.querySelector(`[data-day="${i}"]`)) == null || p.focus();
      })), this.onkeydown = (c) => {
        c.key === "Escape" && this.open && (c.preventDefault(), c.stopPropagation(), this.close());
      };
    }
  }
  customElements.define("ekom-date-range", o);
}
function J() {
  const o = l.filters.orderStatus.startsWith("states:") ? l.filters.orderStatus.slice(7).split(";") : ["", "", ""];
  return [
    { label: "Order", values: ["Draft", "Open", "Closed", "Cancelled"] },
    { label: "Payment", values: ["Pending", "Authorized", "Paid", "PartiallyPaid", "Failed", "Voided"] },
    { label: "Fulfillment", values: ["Unfulfilled", "Picking", "Picked", "Packed", "PartiallyFulfilled", "Fulfilled"] }
  ].map((t, e) => {
    const r = (o[e] || "").split(",").filter(Boolean), s = r.length === 1 ? r[0].replace(/([a-z])([A-Z])/g, "$1 $2") : r.length ? `${r.length} selected` : "Any";
    return `<div class="ekom-status-filter"><span>${t.label}</span><details class="ekom-dropdown"><summary>${n(s)}<span>▾</span></summary><div class="ekom-dropdown-options">${t.values.map((d) => `<label><input type="checkbox" data-shared-status="${e}" value="${d}" ${r.includes(d) ? "checked" : ""}>${d.replace(/([a-z])([A-Z])/g, "$1 $2")}</label>`).join("")}</div></details></div>`;
  }).join("");
}
function L(o, a) {
  const t = o.ownerDocument.createElement("template");
  t.innerHTML = a;
  const e = Array.from(o.children).filter((s) => s.matches('style, link[rel="stylesheet"]')), r = Array.from(t.content.children).filter((s) => s.matches('style, link[rel="stylesheet"]'));
  for (const s of r) {
    const d = e.find((u) => u.outerHTML === s.outerHTML);
    d ? e.splice(e.indexOf(d), 1) : o.appendChild(s.cloneNode(!0)), s.remove();
  }
  for (const s of e) s.remove();
  for (const s of Array.from(o.childNodes))
    (s.nodeType !== 1 || !s.matches('style, link[rel="stylesheet"]')) && s.remove();
  o.appendChild(t.content);
}
const Y = (o) => class extends o {
  connectedCallback() {
  }
  disconnectedCallback() {
  }
};
class V extends Y(HTMLElement) {
  constructor() {
    super(...arguments);
    m(this, "api", new k());
    m(this, "result");
    m(this, "detail");
    m(this, "query", "");
    m(this, "segment", "all");
    m(this, "sort", "newest");
    m(this, "page", 1);
    m(this, "busy", !1);
    m(this, "error", "");
    m(this, "request", 0);
    m(this, "detailRequest", 0);
    m(this, "other", !1);
    m(this, "classic", !1);
    m(this, "opener");
    m(this, "dismissOnEscape", (t) => {
      !this.detail || t.key !== "Escape" || (t.preventDefault(), t.stopPropagation(), this.close());
    });
  }
  connectedCallback() {
    var t, e;
    if (super.connectedCallback(), this.hasAttribute("classic") || (t = this.consumeContext) == null || t.call(this, "umbThemeContext", (r) => {
      r && this.observe(r.theme, (s) => {
        this.toggleAttribute("data-ekom-dark", s === "umb-dark-theme");
      }, "ekom-manager-theme");
    }), (e = this.ownerDocument) == null || e.addEventListener("keydown", this.dismissOnEscape, !0), this.classic = this.hasAttribute("classic"), this.classic)
      for (const r of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const s = this.getAttribute(r.toLowerCase());
        s && (l.filters[r] = s);
      }
    this.initialize();
  }
  disconnectedCallback() {
    var t;
    (t = this.ownerDocument) == null || t.removeEventListener("keydown", this.dismissOnEscape, !0), this.request++, this.detailRequest++, super.disconnectedCallback();
  }
  async initialize() {
    var t;
    this.busy = !0, this.render();
    try {
      const [e, r] = await Promise.all([this.api.stores(), this.classic ? Promise.resolve(!1) : this.api.printingEnabled()]);
      l.stores = e, l.printingEnabled = r, l.stores.some((s) => s.alias === l.filters.store) || (l.filters.store = ((t = l.stores[0]) == null ? void 0 : t.alias) || ""), this.classic || M(), await this.load();
    } catch {
      this.error = "Unable to load customers. Please try again.", this.busy = !1, this.render();
    }
  }
  params() {
    const t = l.filters;
    return new URLSearchParams({ store: t.store, currency: t.currency || "", start: t.dateFrom, end: t.dateTo, orderStatus: t.orderStatus, query: this.query, segment: this.segment, sort: this.sort, page: String(this.page) });
  }
  async get(t, e) {
    const r = await fetch(`/ekom/manager/customers${t}?${e}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!r.ok) throw new Error("Unable to load customer data. Check the filters and try again.");
    return r.json();
  }
  async load() {
    const t = ++this.request;
    this.detailRequest++, this.detail = void 0, this.result = void 0, this.error = "", this.busy = !0;
    const e = this.querySelector(".customers");
    e && l.filters.store ? e.setAttribute("aria-busy", "true") : this.render();
    try {
      if (!l.filters.store) {
        this.result = void 0;
        return;
      }
      const r = await this.get("", this.params());
      t === this.request && (this.result = r);
    } catch (r) {
      t === this.request && (this.result = void 0, this.error = String(r));
    } finally {
      t === this.request && (this.busy = !1, this.render());
    }
  }
  async open(t, e = 1) {
    var s;
    const r = ++this.detailRequest;
    this.opener = t, this.error = "";
    try {
      const d = await this.get("/detail", new URLSearchParams({ store: l.filters.store, currency: l.filters.currency || "", identity: t, orderStatus: l.filters.orderStatus, page: String(e), other: String(this.other) }));
      if (r !== this.detailRequest || !this.isConnected) return;
      this.detail = d, this.render(), (s = this.querySelector("[data-close]")) == null || s.focus();
    } catch (d) {
      r === this.detailRequest && (this.error = String(d), this.render());
    }
  }
  close() {
    var t;
    this.detailRequest++, this.detail = void 0, this.render(), (t = Array.from(this.querySelectorAll("[data-customer]")).find((e) => e.dataset.customer === this.opener)) == null || t.focus();
  }
  async export() {
    this.busy = !0, this.render();
    try {
      const t = await fetch(`/ekom/manager/customers/export?${this.params()}`, { credentials: "same-origin" });
      if (!t.ok) throw new Error("Customer export failed.");
      const e = URL.createObjectURL(await t.blob()), r = document.createElement("a");
      r.href = e, r.download = "customers.csv", r.click(), setTimeout(() => URL.revokeObjectURL(e), 1e3);
    } catch (t) {
      this.error = String(t);
    } finally {
      this.busy = !1, this.render();
    }
  }
  amount(t, e) {
    return t.map((r) => {
      var u;
      const s = (u = l.stores.find((g) => g.alias === l.filters.store)) == null ? void 0 : u.currency;
      let d = r.currency;
      !/^[A-Z]{3}$/.test(d) && (s == null ? void 0 : s.currencyValue) === d && (d = s.isoCurrencySymbol || d);
      try {
        return n(new Intl.NumberFormat(void 0, { style: "currency", currency: d }).format(r[e]));
      } catch {
        return `${n(r[e].toLocaleString(void 0, { maximumFractionDigits: 2 }))} ${n(r.currency)}`;
      }
    }).join("<br>") || "—";
  }
  number(t) {
    return t == null ? "—" : n(t.toLocaleString(void 0, { maximumFractionDigits: 1 }));
  }
  date(t) {
    return t ? n(new Date(t).toLocaleDateString()) : "—";
  }
  avatar(t) {
    const e = t.trim().split(/\s+/).slice(0, 2).map((r) => Array.from(r)[0] || "").join("").toUpperCase();
    return this.classic ? "" : `<span class="customer-avatar" aria-hidden="true">${n(e)}</span>`;
  }
  card(t, e) {
    return `<div class="customer-card"><small>${t}</small><strong>${e}</strong></div>`;
  }
  render() {
    const t = l.filters, e = this.result;
    L(this, `<style>${j}${this.classic ? `
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
    ` : ""}</style>${this.classic ? "" : '<link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css"><link rel="stylesheet" href="/App_Plugins/Ekom/dist/customers-layout.css?v=20261110-toolbar">'}<section class="customers ekom-design" aria-busy="${this.busy}">
      ${this.classic ? "" : z("customers", "data-filter")}
      <div class="customer-toolbar ekom-shared-filters">
        ${this.classic ? `<label>Store<select data-filter="store">${l.stores.map((r) => `<option value="${n(r.alias)}" ${r.alias === t.store ? "selected" : ""}>${n(r.title)}</option>`).join("")}</select></label>` : ""}
        ${this.classic ? `<label>Order view<select data-filter="orderStatus">${U(t.orderStatus)}</select></label>` : J()}
        ${this.classic ? `        <label>Date From<input type="date" data-filter="dateFrom" value="${n(t.dateFrom)}"></label>
        <label>Date To<input type="date" data-filter="dateTo" value="${n(t.dateTo)}"></label>
` : B()}
        <button data-reload ${this.busy ? "disabled" : ""}>Refresh</button>
      </div>
      <p class="customer-muted">Customers who ordered in the selected period. Lifetime metrics include all qualifying orders in the selected store and currency. </p>
      ${this.error ? `<p role="alert">${n(this.error)}</p>` : ""}
      ${this.busy && e ? '<p role="status">Preparing export...</p>' : ""}
      ${e ? `<div class="customer-cards">${this.card("Customers", this.number(e.totals.customers))}${this.card("Returning", this.number(e.totals.returningPercent) + "%")}${this.card("Average lifetime spend", this.amount(e.spend, "averageLifetime"))}${this.card("Average orders per customer", this.number(e.totals.averageOrders))}${this.card("Average days between orders", this.number(e.totals.averageReorderDays))}</div>
      <p class="customer-muted">${e.missingIdentifiers} qualifying orders in this period have no ${e.identityField === "CustomerUsername" ? "username" : "email"} and are excluded. Spend uses recorded charged amounts, without refund adjustment.</p>` : ""}
      <div class="customer-panel"><div class="customer-toolbar" role="search">
        <label>Search<input name="query" value="${n(this.query)}" placeholder="Name, email or username"></label>
        <button type="button" data-search>Search</button>
        <nav class="ekom-customer-segments" aria-label="Customer segment">${["all", "new", "returning"].map((r) => `<button type="button" data-segment-value="${r}" aria-pressed="${r === this.segment}">${r[0].toUpperCase() + r.slice(1)}</button>`).join("")}</nav>
        <label>Sort by<select data-sort>${[["newest", "Newest first order"], ["oldest", "Oldest first order"], ["lastOrder", "Latest order"], ["orders", "Most orders"], ["name", "Name"]].map(([r, s]) => `<option value="${r}" ${r === this.sort ? "selected" : ""}>${s}</option>`).join("")}</select></label>
        <button type="button" data-export ${this.busy || !(e != null && e.totals.customers) ? "disabled" : ""}>Export</button>
      </div><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Customer</th><th>First order</th><th>Orders</th><th>Lifetime spend</th><th>Average order</th><th>Last order</th><th>Days since last order</th></tr></thead><tbody>
      ${((e == null ? void 0 : e.customers) || []).map((r) => `<tr><td><div class="customer-identity">${this.avatar(r.name || r.email || r.username || r.identity)}<button data-customer="${n(r.identity)}">${n(r.name || r.email || r.username || r.identity)}<span class="customer-badge">${r.orders > 1 ? "Returning" : "New"}</span><span class="customer-muted">${n(r.email || r.username || r.identity)}</span></button></div></td><td>${this.date(r.firstOrder)}</td><td>${r.orders}</td><td>${this.amount(r.spend, "total")}</td><td>${this.amount(r.spend, "averageOrder")}</td><td>${this.date(r.lastOrder)}${this.classic ? `<span class="customer-muted">${n(r.lastStatus || "")}</span>` : ""}</td><td>${r.daysSinceLastOrder}</td></tr>`).join("") || `<tr><td colspan="7">${this.busy ? '<div class="customer-loading" role="status"><span class="customer-spinner" aria-hidden="true"></span><strong>Loading customers...</strong><span class="customer-muted">Large datasets may take a moment.</span></div>' : e ? "No customers found." : this.error ? "Customer results could not be loaded." : "Select a store to see customers."}</td></tr>`}
      </tbody></table></div><div class="customer-pagination"><button data-page="${this.page - 1}" ${this.page <= 1 || this.busy ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${Math.max(1, (e == null ? void 0 : e.totalPages) || 0)}</span><button data-page="${this.page + 1}" ${this.page >= ((e == null ? void 0 : e.totalPages) || 0) || this.busy ? "disabled" : ""}>Next</button></div></div>
      ${this.detail ? this.renderDetail(this.detail) : ""}</section>`), this.bind();
  }
  renderDetail(t) {
    const e = t.customer;
    return `<div class="customer-overlay"><section role="dialog" aria-modal="true" aria-label="Customer details" class="customer-dialog"><header class="customer-heading">${this.avatar(e.name || e.identity)}<div><h2>${n(e.name || e.identity)} <span class="customer-badge">${e.orders > 1 ? "Returning" : "New"}</span></h2><p>${n([e.email, e.username].filter(Boolean).join(" · "))}</p></div><button data-close aria-label="Close customer details">${this.classic ? "Close" : "×"}</button></header>
      <div class="customer-cards">${this.card("Lifetime spend", this.amount(e.spend, "total"))}${this.card("Orders", String(e.orders))}${this.card("Average order", this.amount(e.spend, "averageOrder"))}${this.card("First order", this.date(e.firstOrder))}${this.card("Days since last order", String(e.daysSinceLastOrder))}${this.card("Average reorder interval (days)", this.number(e.averageReorderDays))}</div>
      <div class="customer-detail-grid"><div><h3>Orders <small>(${t.count})</small></h3><label><input type="checkbox" data-other ${this.other ? "checked" : ""}> Show other orders (excluded from lifetime metrics)</label><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Order</th>${this.classic ? "<th>Status</th>" : "<th>Order status</th><th>Payment</th><th>Fulfillment</th>"}<th>Created</th><th>Paid</th><th>Total</th></tr></thead><tbody>${t.orders.map((r) => {
      var s, d, u;
      return `<tr><td><button data-order="${n(r.uniqueId)}">#${n(r.orderNumber || String(r.referenceId))}</button></td>${this.classic ? `<td>${n(r.orderStatusCol)}</td>` : `<td>${w((s = r.status) == null ? void 0 : s.orderStatus)}</td><td>${w((d = r.status) == null ? void 0 : d.paymentStatus)}</td><td>${w((u = r.status) == null ? void 0 : u.fulfillmentStatus)}</td>`}<td>${this.date(r.createDate)}</td><td>${this.date(r.paidDate)}</td><td>${n(r.formattedTotal)}</td></tr>`;
    }).join("") || `<tr><td colspan="${this.classic ? 5 : 7}">No orders.</td></tr>`}</tbody></table></div><div class="customer-pagination"><button data-detail-page="${t.page - 1}" ${t.page <= 1 ? "disabled" : ""}>Previous</button><span>${t.page} / ${Math.max(1, t.totalPages)}</span><button data-detail-page="${t.page + 1}" ${t.page >= t.totalPages ? "disabled" : ""}>Next</button></div></div>
      <aside class="customer-snapshot"><h3>Latest order snapshot</h3><p class="customer-muted">Recorded on order #${n(t.snapshot.orderNumber)} · ${this.date(t.snapshot.date)}. Historical details, not a current customer profile.</p>${t.snapshot.unavailable ? "<p>Saved details could not be read.</p>" : ""}<dl>${Object.entries(t.snapshot.fields).map(([r, s]) => `<dt>${n(r)}</dt><dd>${n(s)}</dd>`).join("") || "<dd>No additional details recorded.</dd>"}</dl></aside></div></section></div>`;
  }
  bind() {
    var e, r, s, d, u, g, h, f, b, c;
    this.classic || N(this, () => {
      this.page = 1, this.dispatchEvent(new CustomEvent("customer-filters-changed", { bubbles: !0, detail: { ...l.filters } })), this.load();
    }), this.querySelectorAll("[data-filter]").forEach((i) => i.addEventListener("change", () => {
      const p = i.dataset.filter;
      l.filters[p] = i.value, this.dispatchEvent(new CustomEvent("customer-filters-changed", { bubbles: !0, detail: { ...l.filters } })), this.page = 1, this.load();
    }));
    const t = () => {
      var i;
      this.query = ((i = this.querySelector("[name=query]")) == null ? void 0 : i.value.trim()) || "", this.page = 1, this.load();
    };
    (e = this.querySelector("[data-search]")) == null || e.addEventListener("click", t), (r = this.querySelector("[name=query]")) == null || r.addEventListener("keydown", (i) => {
      i.key !== "Enter" || i.isComposing || (i.preventDefault(), i.stopPropagation(), t());
    }), this.querySelectorAll("[data-segment-value]").forEach((i) => i.addEventListener("click", () => {
      this.segment = i.dataset.segmentValue, this.page = 1, this.load();
    })), (s = this.querySelector("[data-segment]")) == null || s.addEventListener("change", (i) => {
      this.segment = i.target.value, this.page = 1, this.load();
    }), (d = this.querySelector("[data-sort]")) == null || d.addEventListener("change", (i) => {
      this.sort = i.target.value, this.page = 1, this.load();
    }), (u = this.querySelector("[data-reload]")) == null || u.addEventListener("click", () => void this.load()), (g = this.querySelector("[data-export]")) == null || g.addEventListener("click", () => void this.export()), (h = this.querySelector("[data-close]")) == null || h.addEventListener("click", () => this.close()), (f = this.querySelector(".customer-overlay")) == null || f.addEventListener("click", (i) => {
      i.target === i.currentTarget && this.close();
    }), (b = this.querySelector("[data-other]")) == null || b.addEventListener("change", () => {
      this.other = !this.other, this.detail && this.open(this.detail.customer.identity);
    }), this.querySelectorAll("[data-customer]").forEach((i) => i.addEventListener("click", () => {
      this.other = !1, this.open(i.dataset.customer);
    })), this.querySelectorAll("[data-page]").forEach((i) => i.addEventListener("click", () => {
      this.page = Number(i.dataset.page), this.load();
    })), this.querySelectorAll("[data-detail-page]").forEach((i) => i.addEventListener("click", () => {
      this.detail && this.open(this.detail.customer.identity, Number(i.dataset.detailPage));
    })), this.querySelectorAll("[data-order]").forEach((i) => i.addEventListener("click", () => {
      if (this.classic) this.dispatchEvent(new CustomEvent("customer-open-order", { bubbles: !0, detail: { orderId: i.dataset.order } }));
      else {
        l.pendingOrderId = i.dataset.order;
        const p = document.createElement("a");
        p.href = location.pathname.replace(/\/customers\/?$/, "/orders") + "?orderId=" + encodeURIComponent(i.dataset.order || ""), this.appendChild(p), p.click(), p.remove();
      }
    })), (c = this.querySelector("[role=dialog]")) == null || c.addEventListener("keydown", (i) => {
      if (i.key === "Escape" && (i.preventDefault(), this.close()), i.key !== "Tab") return;
      const p = Array.from(this.querySelectorAll("[role=dialog] button:not(:disabled),[role=dialog] input")), y = p[0], x = p[p.length - 1];
      i.shiftKey && document.activeElement === y ? (i.preventDefault(), x == null || x.focus()) : !i.shiftKey && document.activeElement === x && (i.preventDefault(), y == null || y.focus());
    });
  }
}
customElements.define("ekom-customers-section-view", V);
export {
  V as EkomCustomersSectionViewElement,
  V as default
};
