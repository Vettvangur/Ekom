var q = Object.defineProperty;
var T = (i, t, a) => t in i ? q(i, t, { enumerable: !0, configurable: !0, writable: !0, value: a }) : i[t] = a;
var u = (i, t, a) => T(i, typeof t != "symbol" ? t + "" : t, a);
const R = /* @__PURE__ */ new Date(), M = new Date(R.getFullYear(), 0, 1), g = {
  filters: {
    dateFrom: L(M),
    dateTo: L(R),
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
class v {
  async changeOrderStates(t, a, e, r, o) {
    return this.postBody(`/ekom/manager/orderStates/${encodeURIComponent(t)}`, { expectedRevision: a, action: e, notify: r, fulfilledQuantities: o });
  }
  async printingEnabled() {
    try {
      const t = await fetch("/ekom/printer/manager/capabilities", { credentials: "same-origin" });
      return t.ok && (await t.json()).enabled === !0;
    } catch {
      return !1;
    }
  }
  async config() {
    return this.getJson("/ekom/backoffice/Config");
  }
  async searchOrders(t, a, e = 20) {
    return this.getJson("/ekom/manager/SearchOrders", {
      ...t,
      start: t.dateFrom,
      end: t.dateTo,
      page: a,
      pageSize: e
    });
  }
  /** Counts for several order views in one request, so the view tabs do not each run a search. */
  async orderViewCounts(t, a) {
    if (!a.length) return [];
    const { orderStatus: e, ...r } = t;
    return this.getJson("/ekom/manager/OrderViewCounts", {
      ...r,
      start: t.dateFrom,
      end: t.dateTo,
      views: a
    });
  }
  async exportOrders(t, a, e) {
    return this.getBlob("/ekom/manager/ExportOrders", {
      ...t,
      start: t.dateFrom,
      end: t.dateTo,
      page: 1,
      pageSize: a,
      includeOrderLines: e
    });
  }
  async orderReviews(t) {
    const a = [];
    for (let e = 0; ; e += 200) {
      const r = await this.getJson(`/ekom/manager/orders/${encodeURIComponent(t)}/reviews`, { skip: e, take: 200 });
      if (a.push(...r), r.length < 200) return a;
    }
  }
  async reviewOrder(t, a, e, r) {
    return this.postBody(`/ekom/manager/orders/${encodeURIComponent(t)}/reviews/${encodeURIComponent(a)}/review`, { comment: e, resolved: r });
  }
  async statusList() {
    return this.getJson("/ekom/manager/StatusList");
  }
  async stores() {
    const [t, a] = await Promise.all([this.getJson("/ekom/manager/stores"), this.getJson("/ekom/manager/store-currencies")]);
    return t.map((e) => {
      var r;
      return {
        ...e,
        currencyModels: (e.currencies || []).filter((o) => !!o && typeof o == "object"),
        currencies: ((r = a.find((o) => o.alias === e.alias)) == null ? void 0 : r.currencies) || []
      };
    });
  }
  async paymentProviders(t) {
    return this.getJson(`/ekom/manager/paymentproviders/${encodeURIComponent(t)}`);
  }
  async shippingProviders(t) {
    return this.getJson(`/ekom/manager/shippingproviders/${encodeURIComponent(t)}`);
  }
  async orderInfo(t) {
    return this.getJson(`/ekom/manager/OrderInfo/${encodeURIComponent(t)}`);
  }
  async orderLogs(t) {
    return this.getJson(`/ekom/manager/OrderLogs/${encodeURIComponent(t)}`);
  }
  async orderActions(t) {
    return this.getJson(`/ekom/manager/OrderActions/${encodeURIComponent(t)}`);
  }
  async orderLockStatus(t) {
    return this.getJson(`/ekom/manager/orderLockStatus/${encodeURIComponent(t)}`);
  }
  async releaseOrderLock(t, a) {
    return this.postBody(`/ekom/manager/releaseOrderLock/${encodeURIComponent(t)}`, { releaseToken: a });
  }
  async giftcardRecovery(t) {
    return this.getJson(`/ekom/manager/giftcardRecovery/${encodeURIComponent(t)}`);
  }
  async releaseGiftcardRecovery(t, a, e, r) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(t)}/release`, { attemptId: a, claimId: e, token: r });
  }
  async reconcileGiftcardRecovery(t, a, e) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(t)}/reconcile`, { attemptId: a, token: e });
  }
  async executeOrderAction(t, a) {
    const e = await fetch(`/ekom/manager/OrderActions/${encodeURIComponent(t)}/${encodeURIComponent(a)}`, {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json, application/octet-stream" }
    });
    if (!e.ok)
      throw await v.createError(e, "Order action failed.");
    return e;
  }
  async changeOrderStatus(t, a, e) {
    return this.postJson("/ekom/manager/changeOrderStatus", { orderId: t, orderStatus: a, notify: e });
  }
  async updateCustomerInformation(t, a, e) {
    return this.postBody("/ekom/manager/UpdateCustomerInformation", { orderId: t, customer: a, shipping: e });
  }
  async addOrderLine(t, a, e, r) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(t)}/OrderLines`, { productId: a, variantId: e, quantity: r });
  }
  async updateShippingProvider(t, a, e) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(t)}/ShippingProvider`, { providerId: a, customData: e });
  }
  async removeOrderLine(t, a) {
    const e = await fetch(`/ekom/manager/Order/${encodeURIComponent(t)}/OrderLines/${encodeURIComponent(a)}`, {
      method: "DELETE",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!e.ok)
      throw await v.createError(e, "Error removing order line.");
    return e.json();
  }
  async charts(t, a) {
    return this.getJson("/ekom/manager/charts", {
      start: t.dateFrom,
      end: t.dateTo,
      orderStatus: t.orderStatus,
      store: t.store,
      currency: t.currency
    }, a);
  }
  async mostSoldProducts(t, a, e = 20, r) {
    return this.getJson("/ekom/manager/MostSoldProducts", {
      start: t.dateFrom,
      end: t.dateTo,
      orderStatus: t.orderStatus,
      store: t.store,
      currency: t.currency,
      page: a,
      pageSize: e
    }, r);
  }
  async getJson(t, a, e) {
    const r = await fetch(t + _(a), {
      credentials: "same-origin",
      signal: e,
      headers: { Accept: "application/json" }
    });
    if (!r.ok)
      throw await v.createError(r, "Request failed.");
    return r.json();
  }
  async getBlob(t, a) {
    const e = await fetch(t + _(a), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!e.ok)
      throw await v.createError(e, "Request failed.");
    return e.blob();
  }
  async postJson(t, a) {
    const e = await fetch(t + _(a), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!e.ok)
      throw await v.createError(e, "Request failed.");
    return e.json();
  }
  async postBody(t, a) {
    const e = await fetch(t, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json"
      },
      body: JSON.stringify(a)
    });
    if (!e.ok)
      throw await v.createError(e, "Request failed.");
    return e.json();
  }
  static async createError(t, a) {
    var o, s;
    if (t.status === 401 || t.redirected && /\/Account\/Login/i.test(t.url))
      return new Error("Your session has expired. Sign in to Umbraco and reload the manager.");
    if ((s = (o = t.headers) == null ? void 0 : o.get("content-type")) != null && s.includes("text/html"))
      return new Error(`${a} Status ${t.status}. Reload the manager and try again.`);
    const e = await t.text();
    let r = e || a;
    try {
      r = JSON.parse(e).message || r;
    } catch {
    }
    return new Error(r || `${a} Status ${t.status}.`);
  }
}
function _(i) {
  if (!i)
    return "";
  const t = new URLSearchParams();
  for (const [e, r] of Object.entries(i))
    Array.isArray(r) ? r.forEach((o) => t.append(e, String(o))) : t.set(e, r == null ? "" : String(r));
  const a = t.toString();
  return a ? `?${a}` : "";
}
function L(i) {
  const t = String(i.getMonth() + 1).padStart(2, "0"), a = String(i.getDate()).padStart(2, "0");
  return `${i.getFullYear()}-${t}-${a}`;
}
function k(i) {
  return String(i ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
const D = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`, I = `
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

  ${D}
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
`, E = {
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
};
I.replaceAll(":host", "&").replace(/#[0-9a-f]{3,8}\b/gi, (i) => E[i.toLowerCase()] ? `var(${E[i.toLowerCase()]})` : i);
function A() {
  var a;
  const i = g.stores.find((e) => e.alias === g.filters.store), t = (i == null ? void 0 : i.currencies) || [];
  if (!t.includes(g.filters.currency || "")) {
    const e = (a = i == null ? void 0 : i.currency) == null ? void 0 : a.isoCurrencySymbol;
    g.filters.currency = t.find((r) => r === e) || t[0] || "";
  }
}
function N(i = "data-field") {
  const t = g.stores.find((e) => e.alias === g.filters.store), a = g.filters.currency || "";
  return `<div class="ekom-store-switch"><span>Store</span><details class="ekom-dropdown ekom-store-currency"><summary aria-label="Store and currency"><strong>${k((t == null ? void 0 : t.title) || "Select store")}</strong>${a ? `<span class="ekom-currency-code">${k(a)}</span>` : ""}<span class="ekom-store-chevron">▾</span></summary><div class="ekom-currency-popover"><div class="ekom-currency-heading"><strong>Store ·<br>Currency</strong><span>Amounts shown in the selected currency</span></div>${g.stores.map((e) => `<section class="ekom-currency-store ${e.alias === (t == null ? void 0 : t.alias) ? "is-selected" : ""}" aria-label="${k(e.title)}"><h3>${k(e.title)}</h3><div class="ekom-currency-choices">${(e.currencies || []).map((r) => `<button type="button" data-header-store="${k(e.alias)}" data-header-currency="${k(r)}" aria-label="${k(e.title)} — ${k(r)}" aria-pressed="${e.alias === (t == null ? void 0 : t.alias) && r === a}">${k(r)}${e.alias === (t == null ? void 0 : t.alias) && r === a ? '<span aria-hidden="true">✓</span>' : ""}</button>`).join("") || '<span class="ekom-currency-empty">No currencies configured or recorded</span>'}</div></section>`).join("")}</div></details><select hidden aria-label="Store" ${i}="store">${g.stores.map((e) => `<option value="${k(e.alias)}" ${e.alias === (t == null ? void 0 : t.alias) ? "selected" : ""}>${k(e.title)}</option>`).join("")}</select></div>`;
}
function U(i, t = "data-field", a = !0) {
  if (typeof document < "u" && !document.querySelector("link[data-ekom-fonts]")) {
    const o = document.createElement("link");
    o.rel = "stylesheet", o.href = "/App_Plugins/Ekom/dist/manager-fonts.css", o.dataset.ekomFonts = "", document.head.append(o);
  }
  const e = ["orders", "analytics", "customers", "printing"], r = typeof location > "u" ? "/umbraco/section/ekommanager" : location.pathname.replace(/\/view\/(orders|analytics|customers|printing)\/?$/, "").replace(/\/$/, "");
  return `<header class="ekom-design-header"><img src="/App_Plugins/Ekom/dist/ekom-logo.svg" alt="Ekom" class="ekom-design-logo"><nav aria-label="Ekom sections" class="ekom-section-switch">${e.map((o) => `<a href="${k(r + "/view/" + o)}" ${o === i ? 'aria-current="page"' : ""}>${o[0].toUpperCase() + o.slice(1)}</a>`).join("")}</nav>${a ? N(t) : '<span class="ekom-store-switch">Accessible stores</span>'}</header>`;
}
function F(i = /* @__PURE__ */ new Date()) {
  const t = (r) => `${r.getFullYear()}-${String(r.getMonth() + 1).padStart(2, "0")}-${String(r.getDate()).padStart(2, "0")}`, a = (r) => new Date(i.getFullYear(), i.getMonth(), i.getDate() + r), e = t(i);
  return [
    { label: "Today", from: e, to: e },
    { label: "Yesterday", from: t(a(-1)), to: t(a(-1)) },
    { label: "Last 7 days", from: t(a(-6)), to: e },
    { label: "Last 30 days", from: t(a(-29)), to: e },
    { label: "This month", from: t(new Date(i.getFullYear(), i.getMonth(), 1)), to: e },
    { label: "Last month", from: t(new Date(i.getFullYear(), i.getMonth() - 1, 1)), to: t(new Date(i.getFullYear(), i.getMonth(), 0)) },
    { label: "This quarter", from: t(new Date(i.getFullYear(), Math.floor(i.getMonth() / 3) * 3, 1)), to: e },
    { label: "This year", from: t(new Date(i.getFullYear(), 0, 1)), to: e }
  ];
}
function z(i, t) {
  var a, e;
  G(), (a = i.querySelector(".ekom-store-currency")) == null || a.addEventListener("keydown", (r) => {
    var s;
    if (r.key !== "Escape") return;
    const o = r.currentTarget;
    o.open = !1, (s = o.querySelector("summary")) == null || s.focus(), r.stopPropagation();
  }), i.querySelectorAll("[data-header-store]").forEach((r) => r.addEventListener("click", () => {
    var s;
    const o = i.querySelector('[data-field="store"], [data-filter="store"]');
    o && (g.filters.currency = r.dataset.headerCurrency || "", (s = r.closest("details")) == null || s.removeAttribute("open"), o.value = r.dataset.headerStore || "", o.dispatchEvent(new Event("change", { bubbles: !0 })));
  })), i.querySelectorAll("[data-shared-status]").forEach((r) => r.addEventListener("change", () => {
    const o = [0, 1, 2].map((s) => Array.from(i.querySelectorAll(`[data-shared-status="${s}"]:checked`), (p) => p.value).join(","));
    g.filters.orderStatus = o.some(Boolean) ? "states:" + o.join(";") : "AllOrders", t();
  })), (e = i.querySelector("ekom-date-range")) == null || e.addEventListener("date-range-apply", (r) => {
    const o = r.detail;
    g.filters.dateFrom = o.from, g.filters.dateTo = o.to, t();
  });
}
function G() {
  if (typeof customElements > "u" || !customElements.get || customElements.get("ekom-date-range")) return;
  class i extends HTMLElement {
    constructor() {
      super(...arguments);
      u(this, "root", this.attachShadow({ mode: "open" }));
      u(this, "open", !1);
      u(this, "from", "");
      u(this, "to", "");
      u(this, "month", 0);
      u(this, "reposition", () => {
        const e = this.root.querySelector(".popover"), r = this.root.querySelector(".anchor"), o = this.ownerDocument.defaultView;
        if (!e || !r || !o) return;
        if (o.innerWidth <= 650) {
          e.style.left = "";
          return;
        }
        const s = r.getBoundingClientRect().left;
        e.style.left = `${Math.min(0, Math.max(24 - s, o.innerWidth - 24 - s - e.getBoundingClientRect().width))}px`;
      });
      u(this, "outside", (e) => {
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
      const r = new Date(Math.floor(e / 12), e % 12, 1), o = (r.getDay() + 6) % 7, s = new Date(r.getFullYear(), r.getMonth() + 1, 0).getDate();
      return `<section class="month"><h3>${r.toLocaleDateString("en", { month: "long", year: "numeric" })}</h3><div class="days">${["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"].map((p) => `<span class="weekday">${p}</span>`).join("")}${"<span></span>".repeat(o)}${Array.from({ length: s }, (p, h) => {
        const c = `${r.getFullYear()}-${String(r.getMonth() + 1).padStart(2, "0")}-${String(h + 1).padStart(2, "0")}`, d = c === this.from || c === this.to, f = this.from && this.to && c > this.from && c < this.to;
        return `<button type="button" data-day="${c}" aria-label="${c}" aria-pressed="${d || !!f}" class="day ${d ? "edge" : f ? "inside" : ""} ${c === this.from ? "start" : ""} ${c === this.to ? "end" : ""} ${!this.to || this.to === this.from ? "single" : ""}">${h + 1}</button>`;
      }).join("")}</div></section>`;
    }
    render() {
      var p, h, c, d, f;
      const e = this.getAttribute("from") || "", r = this.getAttribute("to") || "", o = F(), s = ((p = o.find((l) => l.from === e && l.to === r)) == null ? void 0 : p.label) || "Custom";
      P(this.root, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/date-range.css"><div class="range"><span class="label">Date range</span><div class="anchor"><button type="button" class="trigger" data-toggle aria-haspopup="dialog" aria-expanded="${this.open}"><strong>${s}</strong><span class="dates">${this.date(e)} – ${this.date(r)}</span><span class="chevron">▾</span></button>${this.open ? `<div class="popover" role="dialog" aria-label="Choose date range"><nav aria-label="Date presets">${o.map((l, b) => `<button type="button" data-preset="${b}" aria-pressed="${l.from === this.from && l.to === this.to}">${l.label}<span>${l.from === this.from && l.to === this.to ? "✓" : ""}</span></button>`).join("")}</nav><div class="custom"><div class="draft"><div class="${this.from ? "" : "active"}"><span>From</span><strong>${this.date(this.from)}</strong></div><span>→</span><div class="${this.from && !this.to ? "active" : ""}"><span>To</span><strong>${this.date(this.to)}</strong></div></div><div class="calendars"><button type="button" class="previous" data-month="-1" aria-label="Previous month">‹</button><button type="button" class="next" data-month="1" aria-label="Next month">›</button>${this.calendar(this.month)}${this.calendar(this.month + 1)}</div><footer><span>${this.from && this.to ? `${this.date(this.from)} – ${this.date(this.to)}` : "Select an end date"}</span><button type="button" data-cancel>Cancel</button><button type="button" class="apply" data-apply ${!this.from || !this.to ? "disabled" : ""}>Apply</button></footer></div></div>` : ""}</div></div>`), (h = this.root.querySelector("link")) == null || h.addEventListener("load", this.reposition), this.reposition(), (c = this.root.querySelector("[data-toggle]")) == null || c.addEventListener("click", () => {
        var l;
        if (this.open) {
          this.close();
          return;
        }
        this.reset(), this.open = !0, this.render(), (l = this.root.querySelector("[data-preset]")) == null || l.focus();
      }), (d = this.root.querySelector("[data-cancel]")) == null || d.addEventListener("click", () => this.close()), (f = this.root.querySelector("[data-apply]")) == null || f.addEventListener("click", () => {
        !this.from || !this.to || this.from > this.to || (this.setAttribute("from", this.from), this.setAttribute("to", this.to), this.close(), this.dispatchEvent(new CustomEvent("date-range-apply", { bubbles: !0, composed: !0, detail: { from: this.from, to: this.to } })));
      }), this.root.querySelectorAll("[data-preset]").forEach((l) => l.addEventListener("click", () => {
        var w;
        const b = o[Number(l.dataset.preset)];
        this.from = b.from, this.to = b.to;
        const x = /* @__PURE__ */ new Date(`${this.from}T12:00:00`);
        this.month = x.getFullYear() * 12 + x.getMonth(), this.render(), (w = this.root.querySelector(`[data-preset="${l.dataset.preset}"]`)) == null || w.focus();
      })), this.root.querySelectorAll("[data-month]").forEach((l) => l.addEventListener("click", () => {
        var b;
        this.month += Number(l.dataset.month), this.render(), (b = this.root.querySelector(`[data-month="${l.dataset.month}"]`)) == null || b.focus();
      })), this.root.querySelectorAll("[data-day]").forEach((l) => l.addEventListener("click", () => {
        var x;
        const b = l.dataset.day;
        !this.from || this.to || b < this.from ? (this.from = b, this.to = "") : this.to = b, this.render(), (x = this.root.querySelector(`[data-day="${b}"]`)) == null || x.focus();
      })), this.onkeydown = (l) => {
        l.key === "Escape" && this.open && (l.preventDefault(), l.stopPropagation(), this.close());
      };
    }
  }
  customElements.define("ekom-date-range", i);
}
let j;
function B(i) {
  const t = i.querySelector(".ekom-design-logo");
  t && (j ?? (j = new v().config().then((a) => a.ekomVersion).catch(() => {
  })), j.then((a) => {
    a && (t.title = `Ekom ${a}`, t.alt = `Ekom ${a}`);
  }));
}
function P(i, t) {
  const a = i.ownerDocument.createElement("template");
  a.innerHTML = t;
  const e = Array.from(i.children).filter((o) => o.matches('style, link[rel="stylesheet"]')), r = Array.from(a.content.children).filter((o) => o.matches('style, link[rel="stylesheet"]'));
  for (const o of r) {
    const s = e.find((p) => p.outerHTML === o.outerHTML);
    s ? e.splice(e.indexOf(s), 1) : i.appendChild(o.cloneNode(!0)), o.remove();
  }
  for (const o of e) o.remove();
  for (const o of Array.from(i.childNodes))
    (o.nodeType !== 1 || !o.matches('style, link[rel="stylesheet"]')) && o.remove();
  i.appendChild(a.content), B(i);
}
const J = (i) => class extends i {
  connectedCallback() {
  }
  disconnectedCallback() {
  }
}, H = "/ekom/printer/manager", m = (i) => String(i ?? "").replace(/[&<>"']/g, (t) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[t]), $ = (i) => i ? new Date(/[zZ]$|[+-]\d\d:\d\d$/.test(i) ? i : i + "Z").toLocaleString(void 0, { year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" }) : "Never", Y = (i) => ({
  PendingRender: "Generating slip",
  Ready: "Queued",
  Claimed: "With agent",
  ConfirmedPrinted: "Confirmed printed",
  Submitted: "Submitted to printer",
  NeedsReview: "Needs review",
  Cancelled: "Cancelled",
  RenderFailed: "Generation failed"
})[i] ?? i;
function V(i, t) {
  const a = i.hasHtml ? ["preview", "history"] : ["history"];
  return t && (i.state === "RenderFailed" && a.push("retry"), ["PendingRender", "Ready", "RenderFailed"].includes(i.state) && a.push("cancel"), ["Submitted", "ConfirmedPrinted"].includes(i.state) && i.hasHtml && a.push("reprint"), i.state === "NeedsReview" && (a.push("resolve-printed"), i.hasHtml && a.push("resolve-reprint"))), a;
}
function W(i) {
  const t = document.createElement("template");
  t.innerHTML = i;
  const a = document.implementation.createHTMLDocument("Picking slip"), e = new Set("div span p h1 h2 h3 h4 h5 h6 header footer main section article aside table thead tbody tfoot tr th td caption colgroup col ul ol li dl dt dd strong em b i u small br hr pre code blockquote img style".split(" ")), r = new Set("script iframe frame object embed svg math template meta link base form input button textarea select".split(" ")), o = new Set("class id style colspan rowspan scope width height alt title".split(" ")), s = (p, h) => {
    if (p.nodeType === Node.TEXT_NODE) {
      h.appendChild(a.createTextNode(p.textContent ?? ""));
      return;
    }
    if (!(p instanceof Element) || p.namespaceURI !== "http://www.w3.org/1999/xhtml") return;
    const c = p.localName;
    if (r.has(c)) return;
    const d = e.has(c) ? a.createElement(c) : a.createElement("span");
    for (const f of Array.from(p.attributes))
      o.has(f.name) && d.setAttribute(f.name, f.value), c === "img" && f.name === "src" && /^data:image\/(png|jpeg|gif|webp);base64,/i.test(f.value) && d.setAttribute("src", f.value);
    for (const f of Array.from(p.childNodes)) s(f, d);
    h.appendChild(d);
  };
  for (const p of Array.from(t.content.childNodes)) s(p, a.body);
  return '<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src &#39;none&#39;; script-src &#39;none&#39;; style-src &#39;unsafe-inline&#39;; img-src data:; font-src data:; base-uri &#39;none&#39;; form-action &#39;none&#39;; connect-src &#39;none&#39;">' + a.head.innerHTML + "</head><body>" + a.body.innerHTML + "</body></html>";
}
class Q extends J(HTMLElement) {
  constructor() {
    super(...arguments);
    u(this, "panel", this.attachShadow({ mode: "open" }));
    u(this, "capabilities");
    u(this, "jobs", { jobs: [], total: 0, page: 1, pageSize: 25 });
    u(this, "stations", []);
    u(this, "state", "");
    u(this, "station", "");
    u(this, "search", "");
    u(this, "page", 1);
    u(this, "loading", !0);
    u(this, "error", "");
    u(this, "busy", !1);
    u(this, "generation", 0);
    u(this, "cancelConfirmation");
    u(this, "requests", /* @__PURE__ */ new Map());
  }
  connectedCallback() {
    var a;
    super.connectedCallback(), this.hasAttribute("classic") || (a = this.consumeContext) == null || a.call(this, "umbThemeContext", (e) => {
      e && this.observe(e.theme, (r) => {
        this.toggleAttribute("data-ekom-dark", r === "umb-dark-theme");
      }, "ekom-manager-theme");
    }), this.render(), this.initialize();
  }
  disconnectedCallback() {
    var a;
    this.generation++, (a = this.cancelConfirmation) == null || a.call(this), super.disconnectedCallback();
  }
  async json(a, e) {
    const r = await fetch(H + a, {
      credentials: "same-origin",
      ...e,
      headers: { Accept: "application/json", ...e == null ? void 0 : e.headers }
    });
    if (!r.ok) throw new Error(r.status === 404 ? "Printing is unavailable or the stored slip has expired." : r.status === 403 ? "You do not have permission to perform this action." : r.status === 409 ? "The job changed or cannot be updated. Refresh the list." : "Could not contact printing. Try refreshing.");
    return r.json();
  }
  async initialize() {
    var a;
    try {
      this.hasAttribute("classic") || (g.stores = await new v().stores(), g.stores.some((e) => e.alias === g.filters.store) || (g.filters.store = ((a = g.stores[0]) == null ? void 0 : a.alias) || ""), A()), this.capabilities = await this.json("/capabilities"), await this.load();
    } catch (e) {
      this.error = String(e.message), this.loading = !1, this.render();
    }
  }
  async load() {
    const a = ++this.generation;
    this.loading = !0, this.error = "", this.render();
    try {
      const e = new URLSearchParams({ page: String(this.page), state: this.state, station: this.station, search: this.search });
      this.hasAttribute("classic") || (e.set("store", g.filters.store), e.set("currency", g.filters.currency || ""));
      const [r, o] = await Promise.all([this.json("/jobs?" + e), this.json("/stations")]);
      if (a !== this.generation) return;
      this.jobs = r, this.stations = this.hasAttribute("classic") ? o : o.filter((s) => s.stores.includes(g.filters.store));
    } catch (e) {
      a === this.generation && (this.error = e.message);
    } finally {
      a === this.generation && (this.loading = !1, this.render());
    }
  }
  render() {
    var f, l, b, x, w, C, O;
    const a = this.loading || this.busy ? "disabled" : "", e = this.stations.filter((n) => n.health.online && n.health.compatible).length, r = this.jobs.jobs.filter((n) => ["NeedsReview", "RenderFailed"].includes(n.state)).length, o = Math.max(1, Math.ceil(this.jobs.total / this.jobs.pageSize)), s = (n) => ({ NeedsReview: "warning", RenderFailed: "danger", Submitted: "success", ConfirmedPrinted: "success", Ready: "info", Claimed: "info" })[n] ?? "neutral", p = (n) => n.replace(/([a-z])([A-Z])/g, "$1 $2");
    P(this.panel, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css"><link rel="stylesheet" href="/App_Plugins/Ekom/dist/printing-layout.css"><main aria-busy="${this.loading}">${U("printing", "data-field", !this.hasAttribute("classic"))}
      <header class="page-heading"><div><p class="subtitle">Track picking slips and warehouse printer connections.</p></div><div class="heading-actions"><span class="connection"><span class="dot"></span>${this.loading ? "Checking stations…" : `${e} of ${this.stations.length} stations online`}</span><button data-reload ${a}>${this.loading ? "Refreshing…" : "Refresh"}</button></div></header>
      <section class="card" aria-labelledby="jobs-heading"><div class="ekom-print-toolbar"><div class="section-heading"><div class="title-group"><h2 id="jobs-heading">Print jobs</h2><span class="count" aria-live="polite">${this.loading ? "Loading…" : `${this.jobs.total} ${this.jobs.total === 1 ? "job" : "jobs"}`}</span></div>${this.state || this.station || this.search ? "<button data-clear>Clear filters</button>" : ""}</div>
      <div class="filters"><label>Status<select data-filter="state">${[["", "All statuses"], ["attention", "Needs attention"], ["PendingRender", "Generating slip"], ["Ready", "Queued"], ["Claimed", "With agent"], ["Submitted", "Submitted to printer"], ["ConfirmedPrinted", "Confirmed printed"], ["Cancelled", "Cancelled"]].map(([n, y]) => `<option value="${n}" ${n === this.state ? "selected" : ""}>${y}</option>`).join("")}</select></label>
      <label>Station<select data-filter="station"><option value="">All stations</option>${this.stations.map((n) => `<option value="${m(n.alias)}" ${n.alias === this.station ? "selected" : ""}>${m(n.alias)}</option>`).join("")}</select></label>
      <label class="search">Order number<input data-filter="search" value="${m(this.search)}" placeholder="Search by order number" maxlength="100" type="search"></label></div></div>
      ${this.error ? `<p class="error" role="alert">${m(this.error)}</p>` : ""}
      ${!this.loading && r ? `<div class="notice"><div><strong>${r} ${r === 1 ? "job on this page needs" : "jobs on this page need"} attention</strong>${this.jobs.jobs.some((n) => n.state === "NeedsReview") ? "Check the physical printer before arranging another copy of an uncertain print." : "Review the affected jobs and retry generating their picking slips."}</div>${this.state !== "attention" ? "<button data-attention>View all needing attention</button>" : ""}</div>` : ""}
      <div class="table" tabindex="0" role="region" aria-label="Print jobs; scroll horizontally on small screens"><table><thead><tr><th scope="col">Order</th><th scope="col">Station</th><th scope="col">Status</th><th scope="col">Created</th><th scope="col">Actions</th></tr></thead><tbody>
      ${this.jobs.jobs.map((n) => {
      var y;
      return `<tr><td><strong>${m(n.orderNumber || "Order unavailable")}</strong><small>${m(n.storeAlias)}</small></td><td>${m(n.stationAlias)}<small>${n.copies} ${n.copies === 1 ? "copy" : "copies"}</small></td>
      <td class="status-cell"><span class="badge ${s(n.state)}"><span class="dot"></span>${m(Y(n.state))}</span>${n.state === "NeedsReview" ? "<small>Printing could not be confirmed.</small>" : n.state === "RenderFailed" ? "<small>The picking slip could not be generated.</small>" : ""}</td><td class="created">${m($(n.createdUtc))}</td>
      <td><div class="actions">${V(n, ((y = this.capabilities) == null ? void 0 : y.canManage) === !0).map((S) => `<button aria-label="${m({ preview: "Preview", history: "History for", retry: "Retry generation for", cancel: "Cancel", reprint: "Reprint", "resolve-printed": "Mark as printed", "resolve-reprint": "Print again after checking" }[S] + " " + n.orderNumber)}" data-job="${m(n.id)}" data-action="${S}" ${a}>${{ preview: "Preview", history: "History", retry: "Retry generation", cancel: "Cancel", reprint: "Reprint", "resolve-printed": "Mark as printed", "resolve-reprint": "Print again after checking" }[S]}</button>`).join("")}</div></td></tr>`;
    }).join("")}
      ${this.jobs.jobs.length === 0 ? `<tr><td colspan="5"><div class="empty"><strong>${this.loading ? "Loading print jobs…" : "No print jobs to show"}</strong><p>${this.loading ? "Fetching the latest queue and station information." : this.state || this.station || this.search ? "Try a different order number or clear your filters." : "Picking slips appear here when orders reach a configured status."}</p></div></td></tr>` : ""}</tbody></table></div>
      <div class="pager"><span>${this.jobs.total ? `${(this.page - 1) * this.jobs.pageSize + 1}–${Math.min(this.page * this.jobs.pageSize, this.jobs.total)} of ${this.jobs.total} jobs` : "0 jobs"}</span><div class="pager-controls"><button data-page="-1" ${this.page <= 1 || this.loading ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${o}</span><button data-page="1" ${this.page >= o || this.loading ? "disabled" : ""}>Next</button></div></div>
      <p class="footnote"><strong>Submitted to printer</strong> confirms delivery to the print queue. It does not confirm that paper was printed.</p></section>
      <div class="stations-heading"><div><h2 id="stations-heading">Printer stations</h2><p>Connection health and automatic printing rules.</p></div><span class="count">${this.stations.length} ${this.stations.length === 1 ? "station" : "stations"}</span></div>
      <section class="station-grid" aria-labelledby="stations-heading">${this.stations.map((n) => `<article class="station-card"><div class="station-top"><div><h3>${m(n.alias)}</h3><p class="printer-name">${m(n.health.printerName || "No printer reported")}</p></div><span class="badge ${n.health.online ? n.health.compatible ? "success" : "warning" : "neutral"}"><span class="dot"></span>${n.health.online ? n.health.compatible ? "Online" : "Update required" : "Offline"}</span></div><dl><dt>Store</dt><dd>${m(n.stores.join(", "))}</dd><dt>Print when</dt><dd><div class="chips">${n.triggerMode === "ExplicitCapture" ? '<span class="chip">Explicit capture API</span>' : n.statuses.map((y) => `<span class="chip">${m(p(y))}</span>`).join("") || "No statuses configured"}</div></dd><dt>Copies</dt><dd>${n.copies} ${n.copies === 1 ? "copy" : "copies"} · ${m(n.policy === "OncePerOrder" ? "Once per order" : n.policy === "OncePerStatus" ? "Once per status" : p(n.policy))}</dd><dt>Automation</dt><dd>${n.enabled ? "Enabled" : "Disabled"}</dd><dt>Last connected</dt><dd>${m($(n.health.lastSeenUtc))}</dd></dl><div class="station-bottom"><span>${n.health.agentVersion ? `Agent ${m(n.health.agentVersion)}` : "Agent not connected"}</span><span>Settings managed externally</span></div></article>`).join("")}
      ${!this.loading && !this.stations.length ? '<div class="station-card empty"><strong>No printer stations</strong><p>No stations are available for your stores.</p></div>' : ""}</section>
      <details class="setup-help"><summary>How do I change printer settings?</summary><p>Change status rules and copies in the website’s Ekom printing configuration. Select the physical printer in the Windows agent. Station settings are read-only here.</p></details>
      <dialog aria-labelledby="detail-heading"><div class="dialog-heading"><h2 id="detail-heading">Print job details</h2><button data-close>Close</button></div><div data-detail></div></dialog></main>`), z(this.panel, () => {
      this.load();
    }), (f = this.panel.querySelector('[data-field="store"]')) == null || f.addEventListener("change", (n) => {
      g.filters.store = n.target.value, A(), this.station = "", this.page = 1, this.load();
    });
    const h = this.panel.querySelector('[data-filter="search"]'), c = () => {
      this.search !== (h == null ? void 0 : h.value) && (this.search = (h == null ? void 0 : h.value) || "", this.page = 1, this.load());
    };
    h == null || h.addEventListener("change", c), (l = this.panel.querySelector("[data-reload]")) == null || l.addEventListener("click", () => {
      this.load();
    }), (b = this.panel.querySelector("[data-clear]")) == null || b.addEventListener("click", () => {
      this.state = "", this.station = "", this.search = "", this.page = 1, this.load();
    }), (x = this.panel.querySelector("[data-attention]")) == null || x.addEventListener("click", () => {
      this.state = "attention", this.station = "", this.search = "", this.page = 1, this.load();
    });
    for (const n of ["state", "station"]) (w = this.panel.querySelector(`[data-filter="${n}"]`)) == null || w.addEventListener("change", (y) => {
      this[n] = y.target.value, this.page = 1, this.load();
    });
    (C = this.panel.querySelector('[data-filter="search"]')) == null || C.addEventListener("keydown", (n) => {
      n.key === "Enter" && c();
    }), this.panel.querySelectorAll("[data-page]").forEach((n) => n.addEventListener("click", () => {
      this.page += Number(n.dataset.page), this.load();
    })), this.panel.querySelectorAll("[data-action]").forEach((n) => n.addEventListener("click", () => {
      this.action(n.dataset.job, n.dataset.action);
    }));
    const d = this.panel.querySelector("dialog");
    (O = this.panel.querySelector("[data-close]")) == null || O.addEventListener("click", () => d.close()), d.addEventListener("cancel", (n) => {
      n.preventDefault(), d.close();
    }), d.addEventListener("keydown", (n) => {
      n.key === "Escape" && (n.preventDefault(), n.stopPropagation(), d.close());
    }), d.addEventListener("click", (n) => {
      if (n.target !== d) return;
      const y = d.getBoundingClientRect();
      (n.clientX < y.left || n.clientX > y.right || n.clientY < y.top || n.clientY > y.bottom) && d.close();
    });
  }
  confirmAction(a) {
    return this.cancelConfirmation ? Promise.resolve(!1) : new Promise((e) => {
      const r = document.createElement("dialog");
      r.className = "action-confirmation", r.setAttribute("aria-labelledby", "print-action-title"), r.setAttribute("aria-describedby", "print-action-message"), r.innerHTML = `<h2 id="print-action-title">Confirm printing action</h2><p id="print-action-message">${m(a)}</p><div class="confirmation-actions"><button data-dismiss autofocus>Cancel</button><button class="primary" data-confirm>Confirm</button></div>`;
      const o = (s) => {
        this.cancelConfirmation = void 0, r.remove(), e(s);
      };
      this.cancelConfirmation = () => o(!1), r.addEventListener("close", () => o(r.returnValue === "confirm"), { once: !0 }), r.querySelector("[data-dismiss]").addEventListener("click", () => r.close()), r.querySelector("[data-confirm]").addEventListener("click", () => r.close("confirm")), this.panel.append(r), r.showModal();
    });
  }
  async action(a, e) {
    var r;
    if (!this.busy)
      try {
        if (e === "preview" || e === "history") {
          const p = this.panel.querySelector("dialog"), h = this.panel.querySelector("[data-detail]");
          if (e === "preview") {
            const c = await this.json(`/jobs/${a}/preview`), d = document.createElement("iframe");
            d.title = "Picking slip preview", d.setAttribute("sandbox", ""), d.referrerPolicy = "no-referrer", d.srcdoc = W(c.html), h.replaceChildren(d), h.style.height = "90%";
          } else {
            const c = await this.json(`/jobs/${a}/history`);
            h.style.height = "auto", h.innerHTML = `<h3>Job history</h3>${c.delivery ? `<p>Claimed: ${m($(c.delivery.claimedUtc))}</p><p>Reported: ${m($(c.delivery.reportedUtc))}</p><p>Windows queue reference: ${m(c.delivery.spoolJobId || "Not reported")}</p>` : "<p>Not yet claimed by an agent.</p>"}<ul>${c.actions.map((d) => `<li>${m($(d.createdUtc))} — ${m({ "resolve-printed": "Confirmed physical printing", "resolve-reprint": "Closed uncertain job and queued replacement" }[d.action] ?? d.action)} by ${m(d.actor)}</li>`).join("")}</ul>`;
          }
          p.showModal();
          return;
        }
        if (!((r = this.capabilities) != null && r.canManage) || !await this.confirmAction(e === "resolve-printed" ? "Have you checked the physical output and confirmed this picking slip was printed? This records your confirmation without printing again." : e === "resolve-reprint" ? "Check the physical printer and clear any pending copy from the Windows print queue first. A delayed original may still print. Continue to close this uncertain job and queue one replacement job with the original copy count?" : e === "reprint" ? "Create another copy of this stored picking slip?" : e === "cancel" ? "Cancel this queued picking slip?" : "Retry generating this picking slip?")) return;
        const o = a + ":" + e, s = this.requests.get(o) ?? crypto.randomUUID();
        this.requests.set(o, s), this.busy = !0, this.render(), await this.json(`/jobs/${a}/actions`, { method: "POST", headers: {
          "Content-Type": "application/json",
          [this.capabilities.csrfHeader]: this.capabilities.csrfToken
        }, body: JSON.stringify({ action: e, requestId: s }) }), this.requests.delete(o), this.busy = !1, await this.load();
      } catch (o) {
        this.busy = !1, this.error = o.message, this.render();
      }
  }
}
customElements.get("ekom-printing-section-view") || customElements.define("ekom-printing-section-view", Q);
export {
  V as availableActions,
  W as createPreviewDocument,
  Q as default,
  Y as stateLabel
};
