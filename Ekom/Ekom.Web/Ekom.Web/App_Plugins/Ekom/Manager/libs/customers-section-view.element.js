var U = Object.defineProperty;
var I = (o, r, t) => r in o ? U(o, r, { enumerable: !0, configurable: !0, writable: !0, value: t }) : o[r] = t;
var u = (o, r, t) => I(o, typeof r != "symbol" ? r + "" : r, t);
const M = /* @__PURE__ */ new Date(), N = new Date(M.getFullYear(), 0, 1), l = {
  filters: {
    dateFrom: L(N),
    dateTo: L(M),
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
class b {
  async changeOrderStates(r, t, e, a, i) {
    return this.postBody(`/ekom/manager/orderStates/${encodeURIComponent(r)}`, { expectedRevision: t, action: e, notify: a, fulfilledQuantities: i });
  }
  async printingEnabled() {
    try {
      const r = await fetch("/ekom/printer/manager/capabilities", { credentials: "same-origin" });
      return r.ok && (await r.json()).enabled === !0;
    } catch {
      return !1;
    }
  }
  async config() {
    return this.getJson("/ekom/backoffice/Config");
  }
  async searchOrders(r, t, e = 20) {
    return this.getJson("/ekom/manager/SearchOrders", {
      ...r,
      start: r.dateFrom,
      end: r.dateTo,
      page: t,
      pageSize: e
    });
  }
  /** Counts for several order views in one request, so the view tabs do not each run a search. */
  async orderViewCounts(r, t) {
    if (!t.length) return [];
    const { orderStatus: e, ...a } = r;
    return this.getJson("/ekom/manager/OrderViewCounts", {
      ...a,
      start: r.dateFrom,
      end: r.dateTo,
      views: t
    });
  }
  async exportOrders(r, t, e) {
    return this.getBlob("/ekom/manager/ExportOrders", {
      ...r,
      start: r.dateFrom,
      end: r.dateTo,
      page: 1,
      pageSize: t,
      includeOrderLines: e
    });
  }
  async orderReviews(r) {
    const t = [];
    for (let e = 0; ; e += 200) {
      const a = await this.getJson(`/ekom/manager/orders/${encodeURIComponent(r)}/reviews`, { skip: e, take: 200 });
      if (t.push(...a), a.length < 200) return t;
    }
  }
  async reviewOrder(r, t, e, a) {
    return this.postBody(`/ekom/manager/orders/${encodeURIComponent(r)}/reviews/${encodeURIComponent(t)}/review`, { comment: e, resolved: a });
  }
  async statusList() {
    return this.getJson("/ekom/manager/StatusList");
  }
  async stores() {
    const [r, t] = await Promise.all([this.getJson("/ekom/manager/stores"), this.getJson("/ekom/manager/store-currencies")]);
    return r.map((e) => {
      const a = t.find((i) => i.alias === e.alias);
      return { ...e, currencies: (a == null ? void 0 : a.currencies) || [], currencyFormats: ((a == null ? void 0 : a.formats) || []).filter((i) => !!i), dateFormat: (a == null ? void 0 : a.dateFormat) || void 0 };
    });
  }
  async paymentProviders(r) {
    return this.getJson(`/ekom/manager/paymentproviders/${encodeURIComponent(r)}`);
  }
  async shippingProviders(r) {
    return this.getJson(`/ekom/manager/shippingproviders/${encodeURIComponent(r)}`);
  }
  /** The order and everything the drawer shows with it, in one request. */
  async orderDrawer(r) {
    return this.getJson(`/ekom/manager/OrderDrawer/${encodeURIComponent(r)}`);
  }
  async orderInfo(r) {
    return this.getJson(`/ekom/manager/OrderInfo/${encodeURIComponent(r)}`);
  }
  async orderPayments(r) {
    return this.getJson(`/ekom/manager/orderPayments/${encodeURIComponent(r)}`);
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
  async releaseOrderLock(r, t) {
    return this.postBody(`/ekom/manager/releaseOrderLock/${encodeURIComponent(r)}`, { releaseToken: t });
  }
  async giftcardRecovery(r) {
    return this.getJson(`/ekom/manager/giftcardRecovery/${encodeURIComponent(r)}`);
  }
  async releaseGiftcardRecovery(r, t, e, a) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(r)}/release`, { attemptId: t, claimId: e, token: a });
  }
  async reconcileGiftcardRecovery(r, t, e) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(r)}/reconcile`, { attemptId: t, token: e });
  }
  async executeOrderAction(r, t) {
    const e = await fetch(`/ekom/manager/OrderActions/${encodeURIComponent(r)}/${encodeURIComponent(t)}`, {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json, application/octet-stream" }
    });
    if (!e.ok)
      throw await b.createError(e, "Order action failed.");
    return e;
  }
  async changeOrderStatus(r, t, e) {
    return this.postJson("/ekom/manager/changeOrderStatus", { orderId: r, orderStatus: t, notify: e });
  }
  async updateCustomerInformation(r, t, e) {
    return this.postBody("/ekom/manager/UpdateCustomerInformation", { orderId: r, customer: t, shipping: e });
  }
  async addOrderLine(r, t, e, a) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(r)}/OrderLines`, { productId: t, variantId: e, quantity: a });
  }
  async updateShippingProvider(r, t, e) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(r)}/ShippingProvider`, { providerId: t, customData: e });
  }
  async removeOrderLine(r, t) {
    const e = await fetch(`/ekom/manager/Order/${encodeURIComponent(r)}/OrderLines/${encodeURIComponent(t)}`, {
      method: "DELETE",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!e.ok)
      throw await b.createError(e, "Error removing order line.");
    return e.json();
  }
  async charts(r, t) {
    return this.getJson("/ekom/manager/charts", {
      start: r.dateFrom,
      end: r.dateTo,
      orderStatus: r.orderStatus,
      store: r.store,
      currency: r.currency
    }, t);
  }
  async mostSoldProducts(r, t, e = 20, a) {
    return this.getJson("/ekom/manager/MostSoldProducts", {
      start: r.dateFrom,
      end: r.dateTo,
      orderStatus: r.orderStatus,
      store: r.store,
      currency: r.currency,
      page: t,
      pageSize: e
    }, a);
  }
  async getJson(r, t, e) {
    const a = await fetch(r + _(t), {
      credentials: "same-origin",
      signal: e,
      headers: { Accept: "application/json" }
    });
    if (!a.ok)
      throw await b.createError(a, "Request failed.");
    return a.json();
  }
  async getBlob(r, t) {
    const e = await fetch(r + _(t), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!e.ok)
      throw await b.createError(e, "Request failed.");
    return e.blob();
  }
  async postJson(r, t) {
    const e = await fetch(r + _(t), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!e.ok)
      throw await b.createError(e, "Request failed.");
    return e.json();
  }
  async postBody(r, t) {
    const e = await fetch(r, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json"
      },
      body: JSON.stringify(t)
    });
    if (!e.ok)
      throw await b.createError(e, "Request failed.");
    return e.json();
  }
  static async createError(r, t) {
    var i, n;
    if (r.status === 401 || r.redirected && /\/Account\/Login/i.test(r.url))
      return new Error("Your session has expired. Sign in to Umbraco and reload the manager.");
    if ((n = (i = r.headers) == null ? void 0 : i.get("content-type")) != null && n.includes("text/html"))
      return new Error(`${t} Status ${r.status}. Reload the manager and try again.`);
    const e = await r.text();
    let a = e || t;
    try {
      a = JSON.parse(e).message || a;
    } catch {
    }
    return new Error(a || `${t} Status ${r.status}.`);
  }
}
function _(o) {
  if (!o)
    return "";
  const r = new URLSearchParams();
  for (const [e, a] of Object.entries(o))
    Array.isArray(a) ? a.forEach((i) => r.append(e, String(i))) : r.set(e, a == null ? "" : String(a));
  const t = r.toString();
  return t ? `?${t}` : "";
}
function L(o) {
  const r = String(o.getMonth() + 1).padStart(2, "0"), t = String(o.getDate()).padStart(2, "0");
  return `${o.getFullYear()}-${r}-${t}`;
}
function v() {
  var o;
  return (o = l.stores.find((r) => r.alias === l.filters.store)) == null ? void 0 : o.dateFormat;
}
function q(o) {
  if (typeof o == "string" && /^\d{4}-\d{2}-\d{2}$/.test(o))
    return { year: Number(o.slice(0, 4)), month: Number(o.slice(5, 7)), day: Number(o.slice(8, 10)), hour: 0, minute: 0, second: 0 };
  const r = o instanceof Date ? o : new Date(o);
  if (!Number.isNaN(r.getTime()))
    return { year: r.getFullYear(), month: r.getMonth() + 1, day: r.getDate(), hour: r.getHours(), minute: r.getMinutes(), second: r.getSeconds() };
}
function R(o, r, t) {
  const e = (a) => String(a).padStart(2, "0");
  return r.replace(/'([^']*)'|\\(.)|d{1,4}|M{1,4}|y{1,5}|H{1,2}|h{1,2}|m{1,2}|s{1,2}|t{1,2}|\/|:/g, (a, i, n) => {
    if (i !== void 0) return i;
    if (n !== void 0) return n;
    switch (a) {
      case "d":
        return String(o.day);
      case "dd":
        return e(o.day);
      case "ddd":
      case "dddd":
        return t.shortestDayNames[new Date(o.year, o.month - 1, o.day).getDay()] || "";
      case "M":
        return String(o.month);
      case "MM":
        return e(o.month);
      case "MMM":
        return t.abbreviatedMonthNames[o.month - 1] || String(o.month);
      case "MMMM":
        return t.monthNames[o.month - 1] || String(o.month);
      case "yy":
        return e(o.year % 100);
      case "H":
        return String(o.hour);
      case "HH":
        return e(o.hour);
      case "h":
        return String(o.hour % 12 || 12);
      case "hh":
        return e(o.hour % 12 || 12);
      case "m":
        return String(o.minute);
      case "mm":
        return e(o.minute);
      case "s":
        return String(o.second);
      case "ss":
        return e(o.second);
      case "t":
        return (o.hour < 12 ? t.amDesignator : t.pmDesignator).slice(0, 1);
      case "tt":
        return o.hour < 12 ? t.amDesignator : t.pmDesignator;
      case "/":
        return t.dateSeparator || "/";
      case ":":
        return t.timeSeparator || ":";
      default:
        return String(o.year);
    }
  });
}
function S(o) {
  if (!o) return "";
  const r = q(o);
  if (!r) return String(o);
  const t = v();
  return t ? R(r, t.shortDate, t) : new Date(r.year, r.month - 1, r.day).toLocaleDateString();
}
function B(o, r, t = !0) {
  const e = q(o);
  if (!e) return String(o);
  const a = v(), i = new Date(Date.UTC(e.year, e.month - 1, e.day));
  if (!a)
    return i.toLocaleDateString("en-GB", { timeZone: "UTC", month: t ? "short" : "long", year: "numeric" });
  const n = a.yearMonth;
  return R(e, t ? n.replace(/M{4}/g, "MMM") : n, a);
}
function d(o) {
  return String(o ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
function G(o) {
  return o.enumValue || o.value || "";
}
const J = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`, T = `
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

  ${J}
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
`, E = ":is(ekom-orders-section-view, ekom-analytics-section-view, ekom-customers-section-view, ekom-printing-section-view)", D = {
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
}, V = T.replaceAll(":host", "&").replace(/#[0-9a-f]{3,8}\b/gi, (o) => D[o.toLowerCase()] ? `var(${D[o.toLowerCase()]})` : o), Y = `
  .ekom-skeleton { display: block; height: 12px; border-radius: 6px; background: linear-gradient(90deg, var(--ekom-border, #eceef1) 0%, var(--ekom-surface-alt, #f7f8fa) 40%, var(--ekom-border, #eceef1) 80%); background-size: 320px 100%; animation: ekom-shim 1.2s linear infinite; }
  .ekom-skeleton-block { flex: 1; height: auto; border-radius: 8px; background-image: linear-gradient(90deg, var(--ekom-surface-alt, #f7f8fa) 0%, var(--ekom-surface, #fff) 40%, var(--ekom-surface-alt, #f7f8fa) 80%); }
  .ekom-loading { display: flex; flex-direction: column; gap: 14px; margin: 14px 0; }
  .ekom-loading-status { display: flex; align-items: center; gap: 10px; margin: 0; font-size: 13px; line-height: 1; color: var(--ekom-muted, #676b7b); }
  .ekom-loading-spinner { flex: none; width: 14px; height: 14px; border: 2px solid var(--ekom-border, #e1e3e8); border-top-color: var(--ekom-accent, #10694a); border-radius: 50%; animation: ekom-spin .8s linear infinite; }
  .ekom-loading-cards { display: grid; gap: 12px; }
  .ekom-loading-card, .ekom-loading-panel { display: flex; flex-direction: column; background: var(--ekom-surface, #fff); border: 1px solid var(--ekom-border, #e1e3e8); border-radius: 12px; }
  .ekom-loading-card { gap: 12px; padding: 14px 16px; }
  .ekom-loading-panel { gap: 14px; padding: 16px 18px; }
  .ekom-loading-rows { display: flex; flex-direction: column; gap: 12px; margin-top: 14px; padding-top: 6px; }
  .ekom-loading-rows > div { display: flex; align-items: center; gap: 16px; }
  .ekom-loading-rows > p { margin: 0; font-size: 12px; line-height: 1; color: var(--ekom-muted, #676b7b); }
  .ekom-list-skeleton-row:hover { background: transparent !important; }
  .ekom-drawer-close { display: flex; align-items: center; flex: none; gap: 14px; margin-left: auto; }
  .ekom-drawer-close > button { min-height: 0; padding: 4px; border: 0; background: none; color: inherit; font-size: 26px; line-height: 1; cursor: pointer; }
  .ekom-esc-hint { font-size: 13px; white-space: nowrap; color: var(--ekom-muted, #676b7b); }
  .ekom-esc-hint strong { font-weight: 800; color: var(--ekom-text, #191b24); }
  @media (max-width: 720px) { .ekom-esc-hint { display: none; } }
  .ekom-drawer-loading { margin-top: 20px; }
  .ekom-drawer-loading .ekom-order-main { display: grid; gap: 14px; }
  .customer-opening { grid-column: 1 / -1; padding: 0 22px 22px; }
  [hidden] { display: none !important; }
  @media (max-width: 720px) { .ekom-loading-cards { grid-template-columns: repeat(2, minmax(0, 1fr)) !important; } }
  @media (prefers-reduced-motion: reduce) { .ekom-skeleton, .ekom-loading-spinner { animation: none; } }
`, H = `@keyframes ekom-shim { 0% { background-position: -320px 0; } 100% { background-position: 320px 0; } }
@keyframes ekom-spin { to { transform: rotate(360deg); } }
${E} {
  ${T.replaceAll(":host", "&")}
  ${Y}
}
${E}:not([classic]) {
  ${V}
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
function O(o) {
  const r = ["Paid", "Fulfilled", "Closed"].includes(o || "") ? "positive" : ["Failed", "Cancelled"].includes(o || "") ? "negative" : ["Pending", "PartiallyPaid", "PartiallyFulfilled", "PartiallyRefunded"].includes(o || "") ? "warning" : "neutral", t = o ? o.replace(/([a-z])([A-Z])/g, "$1 $2") : "Needs review";
  return `<span class="ekom-badge ekom-badge--${r}">${d(t)}</span>`;
}
const W = [
  { key: "AllOrders", label: "All" },
  { key: "NeedsReview", label: "Needs review" },
  { key: "states:Open;;Unfulfilled,Picking,Picked,Packed,PartiallyFulfilled", label: "Unfulfilled" },
  { key: "states:Open;Pending,Authorized,PartiallyPaid,Failed;", label: "Unpaid" },
  { key: "states:Open;;", label: "Open" },
  { key: "states:Closed;;", label: "Closed" }
];
function Z() {
  var t;
  const o = l.stores.find((e) => e.alias === l.filters.store), r = (o == null ? void 0 : o.currencies) || [];
  if (!r.includes(l.filters.currency || "")) {
    const e = (t = o == null ? void 0 : o.currency) == null ? void 0 : t.isoCurrencySymbol;
    l.filters.currency = r.find((a) => a === e) || r[0] || "";
  }
}
function K(o = "data-field") {
  const r = l.stores.find((e) => e.alias === l.filters.store), t = l.filters.currency || "";
  return `<div class="ekom-store-switch"><span>Store</span><details class="ekom-dropdown ekom-store-currency"><summary aria-label="Store and currency"><strong>${d((r == null ? void 0 : r.title) || "Select store")}</strong>${t ? `<span class="ekom-currency-code">${d(t)}</span>` : ""}<span class="ekom-store-chevron">▾</span></summary><div class="ekom-currency-popover"><div class="ekom-currency-heading"><strong>Store ·<br>Currency</strong><span>Amounts shown in the selected currency</span></div>${l.stores.map((e) => `<section class="ekom-currency-store ${e.alias === (r == null ? void 0 : r.alias) ? "is-selected" : ""}" aria-label="${d(e.title)}"><h3>${d(e.title)}</h3><div class="ekom-currency-choices">${(e.currencies || []).map((a) => `<button type="button" data-header-store="${d(e.alias)}" data-header-currency="${d(a)}" aria-label="${d(e.title)} — ${d(a)}" aria-pressed="${e.alias === (r == null ? void 0 : r.alias) && a === t}">${d(a)}${e.alias === (r == null ? void 0 : r.alias) && a === t ? '<span aria-hidden="true">✓</span>' : ""}</button>`).join("") || '<span class="ekom-currency-empty">No currencies configured or recorded</span>'}</div></section>`).join("")}</div></details><select hidden aria-label="Store" ${o}="store">${l.stores.map((e) => `<option value="${d(e.alias)}" ${e.alias === (r == null ? void 0 : r.alias) ? "selected" : ""}>${d(e.title)}</option>`).join("")}</select></div>`;
}
function Q(o) {
  if (typeof location > "u" || typeof history > "u") return;
  const r = new URL(location.href);
  for (const [t, e] of Object.entries(o))
    e ? r.searchParams.set(t, e) : r.searchParams.delete(t);
  history.replaceState(history.state, "", r.pathname + r.search + r.hash);
}
function X(o, r = "data-field", t = !0) {
  if (typeof document < "u" && !document.querySelector("link[data-ekom-fonts]")) {
    const i = document.createElement("link");
    i.rel = "stylesheet", i.href = "/App_Plugins/Ekom/dist/manager-fonts.css", i.dataset.ekomFonts = "", document.head.append(i);
  }
  const e = ["orders", "analytics", "customers", ...l.printingEnabled || o === "printing" ? ["printing"] : []], a = typeof location > "u" ? "/umbraco/section/ekommanager" : location.pathname.replace(/\/view\/(orders|analytics|customers|printing)\/?$/, "").replace(/\/$/, "");
  return `<header class="ekom-design-header"><img src="/App_Plugins/Ekom/dist/ekom-logo.svg" alt="Ekom" class="ekom-design-logo"><nav aria-label="Ekom sections" class="ekom-section-switch">${e.map((i) => `<a href="${d(a + "/view/" + i)}" ${i === o ? 'aria-current="page"' : ""}>${i[0].toUpperCase() + i.slice(1)}</a>`).join("")}</nav>${t ? K(r) : '<span class="ekom-store-switch">Accessible stores</span>'}</header>`;
}
function ee(o) {
  var t;
  const r = [...W];
  return r.some((e) => e.key === o) || r.push({ key: o, label: o.startsWith("states:") ? "Custom status filters" : ((t = l.statusList.find((e) => G(e) === o)) == null ? void 0 : t.label) || o }), r.map((e) => `<option value="${d(e.key)}" ${e.key === o ? "selected" : ""}>${d(e.label)}</option>`).join("");
}
function j(o = /* @__PURE__ */ new Date()) {
  const r = (a) => `${a.getFullYear()}-${String(a.getMonth() + 1).padStart(2, "0")}-${String(a.getDate()).padStart(2, "0")}`, t = (a) => new Date(o.getFullYear(), o.getMonth(), o.getDate() + a), e = r(o);
  return [
    { label: "Today", from: e, to: e },
    { label: "Yesterday", from: r(t(-1)), to: r(t(-1)) },
    { label: "Last 7 days", from: r(t(-6)), to: e },
    { label: "Last 30 days", from: r(t(-29)), to: e },
    { label: "This month", from: r(new Date(o.getFullYear(), o.getMonth(), 1)), to: e },
    { label: "Last month", from: r(new Date(o.getFullYear(), o.getMonth() - 1, 1)), to: r(new Date(o.getFullYear(), o.getMonth(), 0)) },
    { label: "This quarter", from: r(new Date(o.getFullYear(), Math.floor(o.getMonth() / 3) * 3, 1)), to: e },
    { label: "This year", from: r(new Date(o.getFullYear(), 0, 1)), to: e }
  ];
}
const x = (o, r = 12) => `<span class="ekom-skeleton" style="width:${o};height:${r}px"></span>`;
function te() {
  var t;
  const { dateFrom: o, dateTo: r } = l.filters;
  return ((t = j().find((e) => e.from === o && e.to === r)) == null ? void 0 : t.label) || (v() ? `${S(o)} – ${S(r)}` : `${o} – ${r}`);
}
function re(o) {
  return F(`Loading ${o} for ${te()}…`);
}
function F(o) {
  return `<p class="ekom-loading-status" role="status"><span class="ekom-loading-spinner" aria-hidden="true"></span>${d(o)}</p>`;
}
function A(o) {
  return `<div class="ekom-drawer-close"><span class="ekom-esc-hint" aria-hidden="true"><strong>Esc</strong> close</span>${o}</div>`;
}
function ae() {
  return `<div class="ekom-order-columns ekom-drawer-loading"><main class="ekom-order-main">${$(4)}${$(3)}${$(2)}</main><aside class="ekom-customer-sidebar">${$(5)}</aside></div>`;
}
function $(o = 3) {
  return `<div aria-hidden="true" class="ekom-loading-panel">${x("28%")}${x("100%").repeat(o)}</div>`;
}
function oe(o, r, t) {
  const e = `<div class="ekom-loading-card">${x("40%")}${x("55%", 22)}</div>`;
  return `<div class="ekom-loading">${re(o)}<div aria-hidden="true" class="ekom-loading-cards" style="grid-template-columns:repeat(${r},minmax(0,1fr))">${e.repeat(r)}</div>
    <div aria-hidden="true" class="ekom-loading-panel" style="height:${t}px">${x("30%")}<span class="ekom-skeleton ekom-skeleton-block"></span></div>
    <div aria-hidden="true" class="ekom-loading-panel">${x("28%")}${x("100%").repeat(3)}</div></div>`;
}
function ie() {
  return `<ekom-date-range from="${d(l.filters.dateFrom)}" to="${d(l.filters.dateTo)}"></ekom-date-range>`;
}
const C = /* @__PURE__ */ new WeakMap();
function se(o) {
  const r = o.ownerDocument, t = C.has(r);
  if (C.set(r, o), t) return;
  const e = () => {
    const a = C.get(r);
    return a != null && a.open && a.isConnected ? a : void 0;
  };
  r.addEventListener("pointerdown", (a) => {
    const i = e();
    i && !a.composedPath().includes(i) && (i.open = !1);
  }, !0), r.addEventListener("keydown", (a) => {
    var n;
    const i = e();
    !i || a.key !== "Escape" || (i.open = !1, (n = i.querySelector("summary")) == null || n.focus(), a.preventDefault(), a.stopPropagation());
  }, !0);
}
function ne(o, r) {
  var e;
  de();
  const t = o.querySelector(".ekom-store-currency");
  t && se(t), o.querySelectorAll("[data-header-store]").forEach((a) => a.addEventListener("click", () => {
    var n;
    const i = o.querySelector('[data-field="store"], [data-filter="store"]');
    i && (l.filters.currency = a.dataset.headerCurrency || "", (n = a.closest("details")) == null || n.removeAttribute("open"), i.value = a.dataset.headerStore || "", i.dispatchEvent(new Event("change", { bubbles: !0 })));
  })), o.querySelectorAll("[data-shared-status]").forEach((a) => a.addEventListener("change", () => {
    const i = [0, 1, 2].map((n) => Array.from(o.querySelectorAll(`[data-shared-status="${n}"]:checked`), (m) => m.value).join(","));
    l.filters.orderStatus = i.some(Boolean) ? "states:" + i.join(";") : "AllOrders", r();
  })), (e = o.querySelector("ekom-date-range")) == null || e.addEventListener("date-range-apply", (a) => {
    const i = a.detail;
    l.filters.dateFrom = i.from, l.filters.dateTo = i.to, r();
  });
}
function de() {
  if (typeof customElements > "u" || !customElements.get || customElements.get("ekom-date-range")) return;
  class o extends HTMLElement {
    constructor() {
      super(...arguments);
      u(this, "root", this.attachShadow({ mode: "open" }));
      u(this, "open", !1);
      u(this, "from", "");
      u(this, "to", "");
      u(this, "month", 0);
      u(this, "reposition", () => {
        const e = this.root.querySelector(".popover"), a = this.root.querySelector(".anchor"), i = this.ownerDocument.defaultView;
        if (!e || !a || !i) return;
        if (i.innerWidth <= 650) {
          e.style.left = "";
          return;
        }
        const n = a.getBoundingClientRect().left;
        e.style.left = `${Math.min(0, Math.max(24 - n, i.innerWidth - 24 - n - e.getBoundingClientRect().width))}px`;
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
      var a;
      this.open = !1, this.render(), e && ((a = this.root.querySelector("[data-toggle]")) == null || a.focus());
    }
    date(e) {
      return e ? v() ? S(e) : `${e.slice(8, 10)}.${e.slice(5, 7)}.${e.slice(0, 4)}` : "Pick a date";
    }
    calendar(e) {
      const a = new Date(Math.floor(e / 12), e % 12, 1), i = (a.getDay() + 6) % 7, n = new Date(a.getFullYear(), a.getMonth() + 1, 0).getDate();
      return `<section class="month"><h3>${d(v() ? B(a, "yearMonth", !1) : a.toLocaleDateString("en", { month: "long", year: "numeric" }))}</h3><div class="days">${(v() ? [1, 2, 3, 4, 5, 6, 0].map((m) => d(v().shortestDayNames[m] || "")) : ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"]).map((m) => `<span class="weekday">${m}</span>`).join("")}${"<span></span>".repeat(i)}${Array.from({ length: n }, (m, p) => {
        const h = `${a.getFullYear()}-${String(a.getMonth() + 1).padStart(2, "0")}-${String(p + 1).padStart(2, "0")}`, f = h === this.from || h === this.to, k = this.from && this.to && h > this.from && h < this.to;
        return `<button type="button" data-day="${h}" aria-label="${h}" aria-pressed="${f || !!k}" class="day ${f ? "edge" : k ? "inside" : ""} ${h === this.from ? "start" : ""} ${h === this.to ? "end" : ""} ${!this.to || this.to === this.from ? "single" : ""}">${p + 1}</button>`;
      }).join("")}</div></section>`;
    }
    render() {
      var m, p, h, f, k;
      const e = this.getAttribute("from") || "", a = this.getAttribute("to") || "", i = j(), n = ((m = i.find((c) => c.from === e && c.to === a)) == null ? void 0 : m.label) || "Custom";
      z(this.root, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/date-range.css"><div class="range"><span class="label">Date range</span><div class="anchor"><button type="button" class="trigger" data-toggle aria-haspopup="dialog" aria-expanded="${this.open}"><strong>${n}</strong><span class="dates">${this.date(e)} – ${this.date(a)}</span><span class="chevron">▾</span></button>${this.open ? `<div class="popover" role="dialog" aria-label="Choose date range"><nav aria-label="Date presets">${i.map((c, s) => `<button type="button" data-preset="${s}" aria-pressed="${c.from === this.from && c.to === this.to}">${c.label}<span>${c.from === this.from && c.to === this.to ? "✓" : ""}</span></button>`).join("")}</nav><div class="custom"><div class="draft"><div class="${this.from ? "" : "active"}"><span>From</span><strong>${this.date(this.from)}</strong></div><span>→</span><div class="${this.from && !this.to ? "active" : ""}"><span>To</span><strong>${this.date(this.to)}</strong></div></div><div class="calendars"><button type="button" class="previous" data-month="-1" aria-label="Previous month">‹</button><button type="button" class="next" data-month="1" aria-label="Next month">›</button>${this.calendar(this.month)}${this.calendar(this.month + 1)}</div><footer><span>${this.from && this.to ? `${this.date(this.from)} – ${this.date(this.to)}` : "Select an end date"}</span><button type="button" data-cancel>Cancel</button><button type="button" class="apply" data-apply ${!this.from || !this.to ? "disabled" : ""}>Apply</button></footer></div></div>` : ""}</div></div>`), (p = this.root.querySelector("link")) == null || p.addEventListener("load", this.reposition), this.reposition(), (h = this.root.querySelector("[data-toggle]")) == null || h.addEventListener("click", () => {
        var c;
        if (this.open) {
          this.close();
          return;
        }
        this.reset(), this.open = !0, this.render(), (c = this.root.querySelector("[data-preset]")) == null || c.focus();
      }), (f = this.root.querySelector("[data-cancel]")) == null || f.addEventListener("click", () => this.close()), (k = this.root.querySelector("[data-apply]")) == null || k.addEventListener("click", () => {
        !this.from || !this.to || this.from > this.to || (this.setAttribute("from", this.from), this.setAttribute("to", this.to), this.close(), this.dispatchEvent(new CustomEvent("date-range-apply", { bubbles: !0, composed: !0, detail: { from: this.from, to: this.to } })));
      }), this.root.querySelectorAll("[data-preset]").forEach((c) => c.addEventListener("click", () => {
        var y;
        const s = i[Number(c.dataset.preset)];
        this.from = s.from, this.to = s.to;
        const g = /* @__PURE__ */ new Date(`${this.from}T12:00:00`);
        this.month = g.getFullYear() * 12 + g.getMonth(), this.render(), (y = this.root.querySelector(`[data-preset="${c.dataset.preset}"]`)) == null || y.focus();
      })), this.root.querySelectorAll("[data-month]").forEach((c) => c.addEventListener("click", () => {
        var s;
        this.month += Number(c.dataset.month), this.render(), (s = this.root.querySelector(`[data-month="${c.dataset.month}"]`)) == null || s.focus();
      })), this.root.querySelectorAll("[data-day]").forEach((c) => c.addEventListener("click", () => {
        var g;
        const s = c.dataset.day;
        !this.from || this.to || s < this.from ? (this.from = s, this.to = "") : this.to = s, this.render(), (g = this.root.querySelector(`[data-day="${s}"]`)) == null || g.focus();
      })), this.onkeydown = (c) => {
        c.key === "Escape" && this.open && (c.preventDefault(), c.stopPropagation(), this.close());
      };
    }
  }
  customElements.define("ekom-date-range", o);
}
function le() {
  const o = l.filters.orderStatus.startsWith("states:") ? l.filters.orderStatus.slice(7).split(";") : ["", "", ""];
  return [
    { label: "Order", values: ["Draft", "Open", "Closed", "Cancelled"] },
    { label: "Payment", values: ["Pending", "Authorized", "Paid", "PartiallyPaid", "Failed", "Voided"] },
    { label: "Fulfillment", values: ["Unfulfilled", "Picking", "Picked", "Packed", "PartiallyFulfilled", "Fulfilled"] }
  ].map((t, e) => {
    const a = (o[e] || "").split(",").filter(Boolean), i = a.length === 1 ? a[0].replace(/([a-z])([A-Z])/g, "$1 $2") : a.length ? `${a.length} selected` : "Any";
    return `<div class="ekom-status-filter"><span>${t.label}</span><details class="ekom-dropdown"><summary>${d(i)}<span>▾</span></summary><div class="ekom-dropdown-options">${t.values.map((n) => `<label><input type="checkbox" data-shared-status="${e}" value="${n}" ${a.includes(n) ? "checked" : ""}>${n.replace(/([a-z])([A-Z])/g, "$1 $2")}</label>`).join("")}</div></details></div>`;
  }).join("");
}
let P;
function ce(o) {
  const r = o.querySelector(".ekom-design-logo");
  r && (P ?? (P = new b().config().then((t) => t.ekomVersion).catch(() => {
  })), P.then((t) => {
    t && (r.title = `Ekom ${t}`, r.alt = `Ekom ${t}`);
  }));
}
function z(o, r) {
  const t = o.ownerDocument.createElement("template");
  t.innerHTML = r;
  const e = Array.from(o.children).filter((i) => i.matches('style, link[rel="stylesheet"]')), a = Array.from(t.content.children).filter((i) => i.matches('style, link[rel="stylesheet"]'));
  for (const i of a) {
    const n = e.find((m) => m.outerHTML === i.outerHTML);
    n ? e.splice(e.indexOf(n), 1) : o.appendChild(i.cloneNode(!0)), i.remove();
  }
  for (const i of e) i.remove();
  for (const i of Array.from(o.childNodes))
    (i.nodeType !== 1 || !i.matches('style, link[rel="stylesheet"]')) && i.remove();
  o.appendChild(t.content), ce(o);
}
const me = (o) => class extends o {
  connectedCallback() {
  }
  disconnectedCallback() {
  }
};
class ue extends me(HTMLElement) {
  constructor() {
    super(...arguments);
    u(this, "api", new b());
    u(this, "result");
    u(this, "detail");
    u(this, "query", "");
    u(this, "segment", "all");
    u(this, "sort", "newest");
    u(this, "page", 1);
    u(this, "busy", !1);
    u(this, "error", "");
    u(this, "request", 0);
    u(this, "detailRequest", 0);
    u(this, "detailOpening");
    u(this, "other", !1);
    u(this, "classic", !1);
    u(this, "opener");
    u(this, "dismissOnEscape", (t) => {
      !this.detail && !this.detailOpening || t.key !== "Escape" || (t.preventDefault(), t.stopPropagation(), this.close());
    });
  }
  connectedCallback() {
    var t, e;
    if (super.connectedCallback(), this.hasAttribute("classic") || (t = this.consumeContext) == null || t.call(this, "umbThemeContext", (a) => {
      a && this.observe(a.theme, (i) => {
        this.toggleAttribute("data-ekom-dark", i === "umb-dark-theme");
      }, "ekom-manager-theme");
    }), (e = this.ownerDocument) == null || e.addEventListener("keydown", this.dismissOnEscape, !0), this.classic = this.hasAttribute("classic"), this.classic)
      for (const a of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const i = this.getAttribute(a.toLowerCase());
        i && (l.filters[a] = i);
      }
    this.initialize();
  }
  disconnectedCallback() {
    var t;
    (t = this.ownerDocument) == null || t.removeEventListener("keydown", this.dismissOnEscape, !0), this.request++, this.detailRequest++, super.disconnectedCallback();
  }
  async initialize() {
    var e, a, i;
    const t = new URLSearchParams(this.classic || typeof location > "u" ? "" : location.search);
    t.get("customer") && t.get("store") && (l.filters.store = t.get("store")), this.busy = !0, this.render();
    try {
      const [n, m] = await Promise.all([this.api.stores(), this.classic ? Promise.resolve(!1) : this.api.printingEnabled()]);
      l.stores = n, l.printingEnabled = m, l.stores.some((f) => f.alias === l.filters.store) || (l.filters.store = ((e = l.stores[0]) == null ? void 0 : e.alias) || ""), this.classic || Z(), await this.load();
      const p = l.pendingCustomer;
      l.pendingCustomer = void 0;
      const h = ((i = ((a = this.result) == null ? void 0 : a.identityField) === "CustomerUsername" ? p == null ? void 0 : p.username : p == null ? void 0 : p.email) == null ? void 0 : i.trim()) || t.get("customer");
      h && await this.open(h);
    } catch {
      this.error = "Unable to load customers. Please try again.", this.busy = !1, this.render();
    }
  }
  params() {
    const t = l.filters;
    return new URLSearchParams({ store: t.store, currency: t.currency || "", start: t.dateFrom, end: t.dateTo, orderStatus: t.orderStatus, query: this.query, segment: this.segment, sort: this.sort, page: String(this.page) });
  }
  async get(t, e) {
    const a = await fetch(`/ekom/manager/customers${t}?${e}`, { credentials: "same-origin", headers: { Accept: "application/json" } });
    if (!a.ok) throw new Error("Unable to load customer data. Check the filters and try again.");
    return a.json();
  }
  async load() {
    const t = ++this.request;
    this.detailRequest++, this.detail = void 0, this.link(), this.result = void 0, this.error = "", this.busy = !0, this.render();
    try {
      if (!l.filters.store) {
        this.result = void 0;
        return;
      }
      const e = await this.get("", this.params());
      t === this.request && (this.result = e);
    } catch (e) {
      t === this.request && (this.result = void 0, this.error = String(e));
    } finally {
      t === this.request && (this.busy = !1, this.render());
    }
  }
  async open(t, e = 1) {
    var i, n;
    const a = ++this.detailRequest;
    this.opener = t, this.link(t), this.error = "", this.detail || (this.detailOpening = t, this.render(), (i = this.querySelector("[data-close]")) == null || i.focus());
    try {
      const m = await this.get("/detail", new URLSearchParams({ store: l.filters.store, currency: l.filters.currency || "", identity: t, orderStatus: l.filters.orderStatus, page: String(e), other: String(this.other) }));
      if (a !== this.detailRequest || !this.isConnected) return;
      this.detail = m, this.detailOpening = void 0, this.render(), (n = this.querySelector("[data-close]")) == null || n.focus();
    } catch (m) {
      a === this.detailRequest && (this.detailOpening = void 0, this.error = String(m), this.render());
    }
  }
  close() {
    var t;
    this.detailRequest++, this.detailOpening = void 0, this.detail = void 0, this.link(), this.render(), (t = Array.from(this.querySelectorAll("[data-customer]")).find((e) => e.dataset.customer === this.opener)) == null || t.focus();
  }
  /** Puts the open customer in the address bar, or takes it out when none is open. */
  link(t) {
    this.classic || Q({ customer: t, store: t ? l.filters.store : void 0 });
  }
  async export() {
    this.busy = !0, this.render();
    try {
      const t = await fetch(`/ekom/manager/customers/export?${this.params()}`, { credentials: "same-origin" });
      if (!t.ok) throw new Error("Customer export failed.");
      const e = URL.createObjectURL(await t.blob()), a = document.createElement("a");
      a.href = e, a.download = "customers.csv", a.click(), setTimeout(() => URL.revokeObjectURL(e), 1e3);
    } catch (t) {
      this.error = String(t);
    } finally {
      this.busy = !1, this.render();
    }
  }
  amount(t, e) {
    return t.map((a) => {
      var m;
      const i = (m = l.stores.find((p) => p.alias === l.filters.store)) == null ? void 0 : m.currency;
      let n = a.currency;
      !/^[A-Z]{3}$/.test(n) && (i == null ? void 0 : i.currencyValue) === n && (n = i.isoCurrencySymbol || n);
      try {
        return d(new Intl.NumberFormat(void 0, { style: "currency", currency: n }).format(a[e]));
      } catch {
        return `${d(a[e].toLocaleString(void 0, { maximumFractionDigits: 2 }))} ${d(a.currency)}`;
      }
    }).join("<br>") || "—";
  }
  number(t) {
    return t == null ? "—" : d(t.toLocaleString(void 0, { maximumFractionDigits: 1 }));
  }
  date(t) {
    return t ? d(S(new Date(t))) : "—";
  }
  avatar(t) {
    const e = t.trim().split(/\s+/).slice(0, 2).map((a) => Array.from(a)[0] || "").join("").toUpperCase();
    return this.classic ? "" : `<span class="customer-avatar" aria-hidden="true">${d(e)}</span>`;
  }
  card(t, e) {
    return `<div class="customer-card"><small>${t}</small><strong>${e}</strong></div>`;
  }
  render() {
    const t = l.filters, e = this.result;
    z(this, `<style>${H}${this.classic ? `
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
    ` : ""}</style>${this.classic ? "" : '<link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css"><link rel="stylesheet" href="/App_Plugins/Ekom/dist/customers-layout.css?v=20261111-esc-close">'}<section class="customers ekom-design" aria-busy="${this.busy}">
      ${this.classic ? "" : X("customers", "data-filter")}
      <div class="customer-toolbar ekom-shared-filters">
        ${this.classic ? `<label>Store<select data-filter="store">${l.stores.map((a) => `<option value="${d(a.alias)}" ${a.alias === t.store ? "selected" : ""}>${d(a.title)}</option>`).join("")}</select></label>` : ""}
        ${this.classic ? `<label>Order view<select data-filter="orderStatus">${ee(t.orderStatus)}</select></label>` : le()}
        ${this.classic ? `        <label>Date From<input type="date" data-filter="dateFrom" value="${d(t.dateFrom)}"></label>
        <label>Date To<input type="date" data-filter="dateTo" value="${d(t.dateTo)}"></label>
` : ie()}
        <button data-reload ${this.busy ? "disabled" : ""}>Refresh</button>
      </div>
      <p class="customer-muted">Customers who ordered in the selected period. Lifetime metrics include all qualifying orders in the selected store and currency. </p>
      ${this.error ? `<p role="alert">${d(this.error)}</p>` : ""}
      ${this.busy && e ? '<p role="status">Preparing export...</p>' : ""}
      ${e ? `<div class="customer-cards">${this.card("Customers", this.number(e.totals.customers))}${this.card("Returning", this.number(e.totals.returningPercent) + "%")}${this.card("Average lifetime spend", this.amount(e.spend, "averageLifetime"))}${this.card("Average orders per customer", this.number(e.totals.averageOrders))}${this.card("Average days between orders", this.number(e.totals.averageReorderDays))}</div>
      <p class="customer-muted">${e.missingIdentifiers} qualifying orders in this period have no ${e.identityField === "CustomerUsername" ? "username" : "email"} and are excluded. Spend uses recorded charged amounts, without refund adjustment.</p>` : ""}
      ${this.busy && !e ? oe("Customers", 5, 220) : ""}
      <div class="customer-panel" ${this.busy && !e ? "hidden" : ""}><div class="customer-toolbar" role="search">
        <label>Search<input name="query" value="${d(this.query)}" placeholder="Name, email or username"></label>
        <button type="button" data-search>Search</button>
        <nav class="ekom-customer-segments" aria-label="Customer segment">${["all", "new", "returning"].map((a) => `<button type="button" data-segment-value="${a}" aria-pressed="${a === this.segment}">${a[0].toUpperCase() + a.slice(1)}</button>`).join("")}</nav>
        <label>Sort by<select data-sort>${[["newest", "Newest first order"], ["oldest", "Oldest first order"], ["lastOrder", "Latest order"], ["orders", "Most orders"], ["name", "Name"]].map(([a, i]) => `<option value="${a}" ${a === this.sort ? "selected" : ""}>${i}</option>`).join("")}</select></label>
        <button type="button" data-export ${this.busy || !(e != null && e.totals.customers) ? "disabled" : ""}>Export</button>
      </div><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Customer</th><th>First order</th><th>Orders</th><th>Lifetime spend</th><th>Average order</th><th>Last order</th><th>Days since last order</th></tr></thead><tbody>
      ${((e == null ? void 0 : e.customers) || []).map((a) => `<tr><td><div class="customer-identity">${this.avatar(a.name || a.email || a.username || a.identity)}<button data-customer="${d(a.identity)}">${d(a.name || a.email || a.username || a.identity)}<span class="customer-badge">${a.orders > 1 ? "Returning" : "New"}</span><span class="customer-muted">${d(a.email || a.username || a.identity)}</span></button></div></td><td>${this.date(a.firstOrder)}</td><td>${a.orders}</td><td>${this.amount(a.spend, "total")}</td><td>${this.amount(a.spend, "averageOrder")}</td><td>${this.date(a.lastOrder)}${this.classic ? `<span class="customer-muted">${d(a.lastStatus || "")}</span>` : ""}</td><td>${a.daysSinceLastOrder}</td></tr>`).join("") || `<tr><td colspan="7">${e ? "No customers found." : this.error ? "Customer results could not be loaded." : "Select a store to see customers."}</td></tr>`}
      </tbody></table></div><div class="customer-pagination"><button data-page="${this.page - 1}" ${this.page <= 1 || this.busy ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${Math.max(1, (e == null ? void 0 : e.totalPages) || 0)}</span><button data-page="${this.page + 1}" ${this.page >= ((e == null ? void 0 : e.totalPages) || 0) || this.busy ? "disabled" : ""}>Next</button></div></div>
      ${this.detail ? this.renderDetail(this.detail) : this.detailOpening ? this.renderDetailOpening(this.detailOpening) : ""}</section>`), this.bind();
  }
  /** The customer drawer while the customer is still being fetched. */
  renderDetailOpening(t) {
    var i;
    const e = (i = this.result) == null ? void 0 : i.customers.find((n) => n.identity === t), a = (e == null ? void 0 : e.name) || (e == null ? void 0 : e.email) || (e == null ? void 0 : e.username) || t;
    return `<div class="customer-overlay"><section role="dialog" aria-modal="true" aria-label="Customer details" aria-busy="true" class="customer-dialog"><header class="customer-heading">${this.avatar(a)}<div><h2>${d(a)}</h2>${F("Loading customer…")}</div>${this.classic ? '<button data-close aria-label="Close customer details">Close</button>' : A('<button data-close aria-label="Close customer details">×</button>')}</header>
      <div class="customer-opening">${ae()}</div></section></div>`;
  }
  renderDetail(t) {
    const e = t.customer;
    return `<div class="customer-overlay"><section role="dialog" aria-modal="true" aria-label="Customer details" class="customer-dialog"><header class="customer-heading">${this.avatar(e.name || e.identity)}<div><h2>${d(e.name || e.identity)} <span class="customer-badge">${e.orders > 1 ? "Returning" : "New"}</span></h2><p>${d([e.email, e.username].filter(Boolean).join(" · "))}</p></div>${this.classic ? '<button data-close aria-label="Close customer details">Close</button>' : A('<button data-close aria-label="Close customer details">×</button>')}</header>
      <div class="customer-cards">${this.card("Lifetime spend", this.amount(e.spend, "total"))}${this.card("Orders", String(e.orders))}${this.card("Average order", this.amount(e.spend, "averageOrder"))}${this.card("First order", this.date(e.firstOrder))}${this.card("Days since last order", String(e.daysSinceLastOrder))}${this.card("Average reorder interval (days)", this.number(e.averageReorderDays))}</div>
      <div class="customer-detail-grid"><div><h3>Orders <small>(${t.count})</small></h3><label><input type="checkbox" data-other ${this.other ? "checked" : ""}> Show other orders (excluded from lifetime metrics)</label><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Order</th>${this.classic ? "<th>Status</th>" : "<th>Order status</th><th>Payment</th><th>Fulfillment</th>"}<th>Created</th><th>Paid</th><th>Total</th></tr></thead><tbody>${t.orders.map((a) => {
      var i, n, m;
      return `<tr><td><button data-order="${d(a.uniqueId)}">#${d(a.orderNumber || String(a.referenceId))}</button></td>${this.classic ? `<td>${d(a.orderStatusCol)}</td>` : `<td>${O((i = a.status) == null ? void 0 : i.orderStatus)}</td><td>${O((n = a.status) == null ? void 0 : n.paymentStatus)}</td><td>${O((m = a.status) == null ? void 0 : m.fulfillmentStatus)}</td>`}<td>${this.date(a.createDate)}</td><td>${this.date(a.paidDate)}</td><td>${d(a.formattedTotal)}</td></tr>`;
    }).join("") || `<tr><td colspan="${this.classic ? 5 : 7}">No orders.</td></tr>`}</tbody></table></div><div class="customer-pagination"><button data-detail-page="${t.page - 1}" ${t.page <= 1 ? "disabled" : ""}>Previous</button><span>${t.page} / ${Math.max(1, t.totalPages)}</span><button data-detail-page="${t.page + 1}" ${t.page >= t.totalPages ? "disabled" : ""}>Next</button></div></div>
      <aside class="customer-snapshot"><h3>Latest order snapshot</h3><p class="customer-muted">Recorded on order #${d(t.snapshot.orderNumber)} · ${this.date(t.snapshot.date)}. Historical details, not a current customer profile.</p>${t.snapshot.unavailable ? "<p>Saved details could not be read.</p>" : ""}<dl>${Object.entries(t.snapshot.fields).map(([a, i]) => `<dt>${d(a)}</dt><dd>${d(i)}</dd>`).join("") || "<dd>No additional details recorded.</dd>"}</dl></aside></div></section></div>`;
  }
  bind() {
    var e, a, i, n, m, p, h, f, k, c;
    this.classic || ne(this, () => {
      this.page = 1, this.dispatchEvent(new CustomEvent("customer-filters-changed", { bubbles: !0, detail: { ...l.filters } })), this.load();
    }), this.querySelectorAll("[data-filter]").forEach((s) => s.addEventListener("change", () => {
      const g = s.dataset.filter;
      l.filters[g] = s.value, this.dispatchEvent(new CustomEvent("customer-filters-changed", { bubbles: !0, detail: { ...l.filters } })), this.page = 1, this.load();
    }));
    const t = () => {
      var s;
      this.query = ((s = this.querySelector("[name=query]")) == null ? void 0 : s.value.trim()) || "", this.page = 1, this.load();
    };
    (e = this.querySelector("[data-search]")) == null || e.addEventListener("click", t), (a = this.querySelector("[name=query]")) == null || a.addEventListener("keydown", (s) => {
      s.key !== "Enter" || s.isComposing || (s.preventDefault(), s.stopPropagation(), t());
    }), this.querySelectorAll("[data-segment-value]").forEach((s) => s.addEventListener("click", () => {
      this.segment = s.dataset.segmentValue, this.page = 1, this.load();
    })), (i = this.querySelector("[data-segment]")) == null || i.addEventListener("change", (s) => {
      this.segment = s.target.value, this.page = 1, this.load();
    }), (n = this.querySelector("[data-sort]")) == null || n.addEventListener("change", (s) => {
      this.sort = s.target.value, this.page = 1, this.load();
    }), (m = this.querySelector("[data-reload]")) == null || m.addEventListener("click", () => void this.load()), (p = this.querySelector("[data-export]")) == null || p.addEventListener("click", () => void this.export()), (h = this.querySelector("[data-close]")) == null || h.addEventListener("click", () => this.close()), (f = this.querySelector(".customer-overlay")) == null || f.addEventListener("click", (s) => {
      s.target === s.currentTarget && this.close();
    }), (k = this.querySelector("[data-other]")) == null || k.addEventListener("change", () => {
      this.other = !this.other, this.detail && this.open(this.detail.customer.identity);
    }), this.querySelectorAll("[data-customer]").forEach((s) => s.addEventListener("click", () => {
      this.other = !1, this.open(s.dataset.customer);
    })), this.querySelectorAll("[data-page]").forEach((s) => s.addEventListener("click", () => {
      this.page = Number(s.dataset.page), this.load();
    })), this.querySelectorAll("[data-detail-page]").forEach((s) => s.addEventListener("click", () => {
      this.detail && this.open(this.detail.customer.identity, Number(s.dataset.detailPage));
    })), this.querySelectorAll("[data-order]").forEach((s) => s.addEventListener("click", () => {
      if (this.classic) this.dispatchEvent(new CustomEvent("customer-open-order", { bubbles: !0, detail: { orderId: s.dataset.order } }));
      else {
        l.pendingOrderId = s.dataset.order;
        const g = document.createElement("a");
        g.href = location.pathname.replace(/\/customers\/?$/, "/orders") + "?orderId=" + encodeURIComponent(s.dataset.order || ""), this.appendChild(g), g.click(), g.remove();
      }
    })), (c = this.querySelector("[role=dialog]")) == null || c.addEventListener("keydown", (s) => {
      if (s.key === "Escape" && (s.preventDefault(), this.close()), s.key !== "Tab") return;
      const g = Array.from(this.querySelectorAll("[role=dialog] button:not(:disabled),[role=dialog] input")), y = g[0], w = g[g.length - 1];
      s.shiftKey && document.activeElement === y ? (s.preventDefault(), w == null || w.focus()) : !s.shiftKey && document.activeElement === w && (s.preventDefault(), y == null || y.focus());
    });
  }
}
customElements.define("ekom-customers-section-view", ue);
export {
  ue as EkomCustomersSectionViewElement,
  ue as default
};
