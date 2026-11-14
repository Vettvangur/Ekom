var N = Object.defineProperty;
var I = (a, e, r) => e in a ? N(a, e, { enumerable: !0, configurable: !0, writable: !0, value: r }) : a[e] = r;
var g = (a, e, r) => I(a, typeof e != "symbol" ? e + "" : e, r);
const T = /* @__PURE__ */ new Date(), F = new Date(T.getFullYear(), 0, 1), h = {
  filters: {
    dateFrom: j(F),
    dateTo: j(T),
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
  async changeOrderStates(e, r, t, n, i) {
    return this.postBody(`/ekom/manager/orderStates/${encodeURIComponent(e)}`, { expectedRevision: r, action: t, notify: n, fulfilledQuantities: i });
  }
  async printingEnabled() {
    try {
      const e = await fetch("/ekom/printer/manager/capabilities", { credentials: "same-origin" });
      return e.ok && (await e.json()).enabled === !0;
    } catch {
      return !1;
    }
  }
  async config() {
    return this.getJson("/ekom/backoffice/Config");
  }
  async searchOrders(e, r, t = 20) {
    return this.getJson("/ekom/manager/SearchOrders", {
      ...e,
      start: e.dateFrom,
      end: e.dateTo,
      page: r,
      pageSize: t
    });
  }
  /** Counts for several order views in one request, so the view tabs do not each run a search. */
  async orderViewCounts(e, r) {
    if (!r.length) return [];
    const { orderStatus: t, ...n } = e;
    return this.getJson("/ekom/manager/OrderViewCounts", {
      ...n,
      start: e.dateFrom,
      end: e.dateTo,
      views: r
    });
  }
  async exportOrders(e, r, t) {
    return this.getBlob("/ekom/manager/ExportOrders", {
      ...e,
      start: e.dateFrom,
      end: e.dateTo,
      page: 1,
      pageSize: r,
      includeOrderLines: t
    });
  }
  async orderReviews(e) {
    const r = [];
    for (let t = 0; ; t += 200) {
      const n = await this.getJson(`/ekom/manager/orders/${encodeURIComponent(e)}/reviews`, { skip: t, take: 200 });
      if (r.push(...n), n.length < 200) return r;
    }
  }
  async reviewOrder(e, r, t, n) {
    return this.postBody(`/ekom/manager/orders/${encodeURIComponent(e)}/reviews/${encodeURIComponent(r)}/review`, { comment: t, resolved: n });
  }
  async statusList() {
    return this.getJson("/ekom/manager/StatusList");
  }
  async stores() {
    const [e, r] = await Promise.all([this.getJson("/ekom/manager/stores"), this.getJson("/ekom/manager/store-currencies")]);
    return e.map((t) => {
      const n = r.find((i) => i.alias === t.alias);
      return { ...t, currencies: (n == null ? void 0 : n.currencies) || [], currencyFormats: ((n == null ? void 0 : n.formats) || []).filter((i) => !!i), dateFormat: (n == null ? void 0 : n.dateFormat) || void 0 };
    });
  }
  async paymentProviders(e) {
    return this.getJson(`/ekom/manager/paymentproviders/${encodeURIComponent(e)}`);
  }
  async shippingProviders(e) {
    return this.getJson(`/ekom/manager/shippingproviders/${encodeURIComponent(e)}`);
  }
  /** The order and everything the drawer shows with it, in one request. */
  async orderDrawer(e) {
    return this.getJson(`/ekom/manager/OrderDrawer/${encodeURIComponent(e)}`);
  }
  async orderInfo(e) {
    return this.getJson(`/ekom/manager/OrderInfo/${encodeURIComponent(e)}`);
  }
  async orderPayments(e) {
    return this.getJson(`/ekom/manager/orderPayments/${encodeURIComponent(e)}`);
  }
  async orderLogs(e) {
    return this.getJson(`/ekom/manager/OrderLogs/${encodeURIComponent(e)}`);
  }
  async orderActions(e) {
    return this.getJson(`/ekom/manager/OrderActions/${encodeURIComponent(e)}`);
  }
  async orderLockStatus(e) {
    return this.getJson(`/ekom/manager/orderLockStatus/${encodeURIComponent(e)}`);
  }
  async releaseOrderLock(e, r) {
    return this.postBody(`/ekom/manager/releaseOrderLock/${encodeURIComponent(e)}`, { releaseToken: r });
  }
  async giftcardRecovery(e) {
    return this.getJson(`/ekom/manager/giftcardRecovery/${encodeURIComponent(e)}`);
  }
  async releaseGiftcardRecovery(e, r, t, n) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(e)}/release`, { attemptId: r, claimId: t, token: n });
  }
  async reconcileGiftcardRecovery(e, r, t) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(e)}/reconcile`, { attemptId: r, token: t });
  }
  async executeOrderAction(e, r) {
    const t = await fetch(`/ekom/manager/OrderActions/${encodeURIComponent(e)}/${encodeURIComponent(r)}`, {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json, application/octet-stream" }
    });
    if (!t.ok)
      throw await v.createError(t, "Order action failed.");
    return t;
  }
  async changeOrderStatus(e, r, t) {
    return this.postJson("/ekom/manager/changeOrderStatus", { orderId: e, orderStatus: r, notify: t });
  }
  async updateCustomerInformation(e, r, t) {
    return this.postBody("/ekom/manager/UpdateCustomerInformation", { orderId: e, customer: r, shipping: t });
  }
  async addOrderLine(e, r, t, n) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(e)}/OrderLines`, { productId: r, variantId: t, quantity: n });
  }
  async updateShippingProvider(e, r, t) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(e)}/ShippingProvider`, { providerId: r, customData: t });
  }
  async removeOrderLine(e, r) {
    const t = await fetch(`/ekom/manager/Order/${encodeURIComponent(e)}/OrderLines/${encodeURIComponent(r)}`, {
      method: "DELETE",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await v.createError(t, "Error removing order line.");
    return t.json();
  }
  async charts(e, r) {
    return this.getJson("/ekom/manager/charts", {
      start: e.dateFrom,
      end: e.dateTo,
      orderStatus: e.orderStatus,
      store: e.store,
      currency: e.currency
    }, r);
  }
  async mostSoldProducts(e, r, t = 20, n) {
    return this.getJson("/ekom/manager/MostSoldProducts", {
      start: e.dateFrom,
      end: e.dateTo,
      orderStatus: e.orderStatus,
      store: e.store,
      currency: e.currency,
      page: r,
      pageSize: t
    }, n);
  }
  async getJson(e, r, t) {
    const n = await fetch(e + O(r), {
      credentials: "same-origin",
      signal: t,
      headers: { Accept: "application/json" }
    });
    if (!n.ok)
      throw await v.createError(n, "Request failed.");
    return n.json();
  }
  async getBlob(e, r) {
    const t = await fetch(e + O(r), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!t.ok)
      throw await v.createError(t, "Request failed.");
    return t.blob();
  }
  async postJson(e, r) {
    const t = await fetch(e + O(r), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await v.createError(t, "Request failed.");
    return t.json();
  }
  async postBody(e, r) {
    const t = await fetch(e, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json"
      },
      body: JSON.stringify(r)
    });
    if (!t.ok)
      throw await v.createError(t, "Request failed.");
    return t.json();
  }
  static async createError(e, r) {
    var i, s;
    if (e.status === 401 || e.redirected && /\/Account\/Login/i.test(e.url))
      return new Error("Your session has expired. Sign in to Umbraco and reload the manager.");
    if ((s = (i = e.headers) == null ? void 0 : i.get("content-type")) != null && s.includes("text/html"))
      return new Error(`${r} Status ${e.status}. Reload the manager and try again.`);
    const t = await e.text();
    let n = t || r;
    try {
      n = JSON.parse(t).message || n;
    } catch {
    }
    return new Error(n || `${r} Status ${e.status}.`);
  }
}
function O(a) {
  if (!a)
    return "";
  const e = new URLSearchParams();
  for (const [t, n] of Object.entries(a))
    Array.isArray(n) ? n.forEach((i) => e.append(t, String(i))) : e.set(t, n == null ? "" : String(n));
  const r = e.toString();
  return r ? `?${r}` : "";
}
function j(a) {
  const e = String(a.getMonth() + 1).padStart(2, "0"), r = String(a.getDate()).padStart(2, "0");
  return `${a.getFullYear()}-${e}-${r}`;
}
function x() {
  var a;
  return (a = h.stores.find((e) => e.alias === h.filters.store)) == null ? void 0 : a.dateFormat;
}
function E(a) {
  if (typeof a == "string" && /^\d{4}-\d{2}-\d{2}$/.test(a))
    return { year: Number(a.slice(0, 4)), month: Number(a.slice(5, 7)), day: Number(a.slice(8, 10)), hour: 0, minute: 0, second: 0 };
  const e = a instanceof Date ? a : new Date(a);
  if (!Number.isNaN(e.getTime()))
    return { year: e.getFullYear(), month: e.getMonth() + 1, day: e.getDate(), hour: e.getHours(), minute: e.getMinutes(), second: e.getSeconds() };
}
function _(a, e, r) {
  const t = (n) => String(n).padStart(2, "0");
  return e.replace(/'([^']*)'|\\(.)|d{1,4}|M{1,4}|y{1,5}|H{1,2}|h{1,2}|m{1,2}|s{1,2}|t{1,2}|\/|:/g, (n, i, s) => {
    if (i !== void 0) return i;
    if (s !== void 0) return s;
    switch (n) {
      case "d":
        return String(a.day);
      case "dd":
        return t(a.day);
      case "ddd":
      case "dddd":
        return r.shortestDayNames[new Date(a.year, a.month - 1, a.day).getDay()] || "";
      case "M":
        return String(a.month);
      case "MM":
        return t(a.month);
      case "MMM":
        return r.abbreviatedMonthNames[a.month - 1] || String(a.month);
      case "MMMM":
        return r.monthNames[a.month - 1] || String(a.month);
      case "yy":
        return t(a.year % 100);
      case "H":
        return String(a.hour);
      case "HH":
        return t(a.hour);
      case "h":
        return String(a.hour % 12 || 12);
      case "hh":
        return t(a.hour % 12 || 12);
      case "m":
        return String(a.minute);
      case "mm":
        return t(a.minute);
      case "s":
        return String(a.second);
      case "ss":
        return t(a.second);
      case "t":
        return (a.hour < 12 ? r.amDesignator : r.pmDesignator).slice(0, 1);
      case "tt":
        return a.hour < 12 ? r.amDesignator : r.pmDesignator;
      case "/":
        return r.dateSeparator || "/";
      case ":":
        return r.timeSeparator || ":";
      default:
        return String(a.year);
    }
  });
}
function U(a) {
  if (!a) return "";
  const e = E(a);
  if (!e) return String(a);
  const r = x();
  return r ? _(e, r.shortDate, r) : new Date(e.year, e.month - 1, e.day).toLocaleDateString();
}
function z(a, e, r = !0) {
  const t = E(a);
  if (!t) return String(a);
  const n = x(), i = new Date(Date.UTC(t.year, t.month - 1, t.day));
  if (!n)
    return i.toLocaleDateString("en-GB", { timeZone: "UTC", month: r ? "short" : "long", year: "numeric" });
  const s = n.yearMonth;
  return _(t, r ? s.replace(/M{4}/g, "MMM") : s, n);
}
function G(a) {
  if (!a)
    return "";
  const e = a instanceof Date ? a : new Date(a);
  if (Number.isNaN(e.getTime()))
    return String(a);
  const r = x(), t = E(e);
  return r && t ? `${_(t, r.shortDate, r)} ${_(t, r.shortTime, r)}` : new Intl.DateTimeFormat(void 0, {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  }).format(e);
}
function y(a) {
  return String(a ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
const H = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`, J = `
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

  ${H}
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
`, D = {
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
J.replaceAll(":host", "&").replace(/#[0-9a-f]{3,8}\b/gi, (a) => D[a.toLowerCase()] ? `var(${D[a.toLowerCase()]})` : a);
function R() {
  var r;
  const a = h.stores.find((t) => t.alias === h.filters.store), e = (a == null ? void 0 : a.currencies) || [];
  if (!e.includes(h.filters.currency || "")) {
    const t = (r = a == null ? void 0 : a.currency) == null ? void 0 : r.isoCurrencySymbol;
    h.filters.currency = e.find((n) => n === t) || e[0] || "";
  }
}
function B(a = "data-field") {
  const e = h.stores.find((t) => t.alias === h.filters.store), r = h.filters.currency || "";
  return `<div class="ekom-store-switch"><span>Store</span><details class="ekom-dropdown ekom-store-currency"><summary aria-label="Store and currency"><strong>${y((e == null ? void 0 : e.title) || "Select store")}</strong>${r ? `<span class="ekom-currency-code">${y(r)}</span>` : ""}<span class="ekom-store-chevron">▾</span></summary><div class="ekom-currency-popover"><div class="ekom-currency-heading"><strong>Store ·<br>Currency</strong><span>Amounts shown in the selected currency</span></div>${h.stores.map((t) => `<section class="ekom-currency-store ${t.alias === (e == null ? void 0 : e.alias) ? "is-selected" : ""}" aria-label="${y(t.title)}"><h3>${y(t.title)}</h3><div class="ekom-currency-choices">${(t.currencies || []).map((n) => `<button type="button" data-header-store="${y(t.alias)}" data-header-currency="${y(n)}" aria-label="${y(t.title)} — ${y(n)}" aria-pressed="${t.alias === (e == null ? void 0 : e.alias) && n === r}">${y(n)}${t.alias === (e == null ? void 0 : e.alias) && n === r ? '<span aria-hidden="true">✓</span>' : ""}</button>`).join("") || '<span class="ekom-currency-empty">No currencies configured or recorded</span>'}</div></section>`).join("")}</div></details><select hidden aria-label="Store" ${a}="store">${h.stores.map((t) => `<option value="${y(t.alias)}" ${t.alias === (e == null ? void 0 : e.alias) ? "selected" : ""}>${y(t.title)}</option>`).join("")}</select></div>`;
}
function Y(a, e = "data-field", r = !0) {
  if (typeof document < "u" && !document.querySelector("link[data-ekom-fonts]")) {
    const i = document.createElement("link");
    i.rel = "stylesheet", i.href = "/App_Plugins/Ekom/dist/manager-fonts.css", i.dataset.ekomFonts = "", document.head.append(i);
  }
  const t = ["orders", "analytics", "customers", "printing"], n = typeof location > "u" ? "/umbraco/section/ekommanager" : location.pathname.replace(/\/view\/(orders|analytics|customers|printing)\/?$/, "").replace(/\/$/, "");
  return `<header class="ekom-design-header"><img src="/App_Plugins/Ekom/dist/ekom-logo.svg" alt="Ekom" class="ekom-design-logo"><nav aria-label="Ekom sections" class="ekom-section-switch">${t.map((i) => `<a href="${y(n + "/view/" + i)}" ${i === a ? 'aria-current="page"' : ""}>${i[0].toUpperCase() + i.slice(1)}</a>`).join("")}</nav>${r ? B(e) : '<span class="ekom-store-switch">Accessible stores</span>'}</header>`;
}
function V(a = /* @__PURE__ */ new Date()) {
  const e = (n) => `${n.getFullYear()}-${String(n.getMonth() + 1).padStart(2, "0")}-${String(n.getDate()).padStart(2, "0")}`, r = (n) => new Date(a.getFullYear(), a.getMonth(), a.getDate() + n), t = e(a);
  return [
    { label: "Today", from: t, to: t },
    { label: "Yesterday", from: e(r(-1)), to: e(r(-1)) },
    { label: "Last 7 days", from: e(r(-6)), to: t },
    { label: "Last 30 days", from: e(r(-29)), to: t },
    { label: "This month", from: e(new Date(a.getFullYear(), a.getMonth(), 1)), to: t },
    { label: "Last month", from: e(new Date(a.getFullYear(), a.getMonth() - 1, 1)), to: e(new Date(a.getFullYear(), a.getMonth(), 0)) },
    { label: "This quarter", from: e(new Date(a.getFullYear(), Math.floor(a.getMonth() / 3) * 3, 1)), to: t },
    { label: "This year", from: e(new Date(a.getFullYear(), 0, 1)), to: t }
  ];
}
const L = /* @__PURE__ */ new WeakMap();
function W(a) {
  const e = a.ownerDocument, r = L.has(e);
  if (L.set(e, a), r) return;
  const t = () => {
    const n = L.get(e);
    return n != null && n.open && n.isConnected ? n : void 0;
  };
  e.addEventListener("pointerdown", (n) => {
    const i = t();
    i && !n.composedPath().includes(i) && (i.open = !1);
  }, !0), e.addEventListener("keydown", (n) => {
    var s;
    const i = t();
    !i || n.key !== "Escape" || (i.open = !1, (s = i.querySelector("summary")) == null || s.focus(), n.preventDefault(), n.stopPropagation());
  }, !0);
}
function Z(a, e) {
  var t;
  Q();
  const r = a.querySelector(".ekom-store-currency");
  r && W(r), a.querySelectorAll("[data-header-store]").forEach((n) => n.addEventListener("click", () => {
    var s;
    const i = a.querySelector('[data-field="store"], [data-filter="store"]');
    i && (h.filters.currency = n.dataset.headerCurrency || "", (s = n.closest("details")) == null || s.removeAttribute("open"), i.value = n.dataset.headerStore || "", i.dispatchEvent(new Event("change", { bubbles: !0 })));
  })), a.querySelectorAll("[data-shared-status]").forEach((n) => n.addEventListener("change", () => {
    const i = [0, 1, 2].map((s) => Array.from(a.querySelectorAll(`[data-shared-status="${s}"]:checked`), (c) => c.value).join(","));
    h.filters.orderStatus = i.some(Boolean) ? "states:" + i.join(";") : "AllOrders", e();
  })), (t = a.querySelector("ekom-date-range")) == null || t.addEventListener("date-range-apply", (n) => {
    const i = n.detail;
    h.filters.dateFrom = i.from, h.filters.dateTo = i.to, e();
  });
}
function Q() {
  if (typeof customElements > "u" || !customElements.get || customElements.get("ekom-date-range")) return;
  class a extends HTMLElement {
    constructor() {
      super(...arguments);
      g(this, "root", this.attachShadow({ mode: "open" }));
      g(this, "open", !1);
      g(this, "from", "");
      g(this, "to", "");
      g(this, "month", 0);
      g(this, "reposition", () => {
        const t = this.root.querySelector(".popover"), n = this.root.querySelector(".anchor"), i = this.ownerDocument.defaultView;
        if (!t || !n || !i) return;
        if (i.innerWidth <= 650) {
          t.style.left = "";
          return;
        }
        const s = n.getBoundingClientRect().left;
        t.style.left = `${Math.min(0, Math.max(24 - s, i.innerWidth - 24 - s - t.getBoundingClientRect().width))}px`;
      });
      g(this, "outside", (t) => {
        this.open && !t.composedPath().includes(this) && this.close(!1);
      });
    }
    connectedCallback() {
      var t;
      this.render(), this.ownerDocument.addEventListener("pointerdown", this.outside), (t = this.ownerDocument.defaultView) == null || t.addEventListener("resize", this.reposition);
    }
    disconnectedCallback() {
      var t;
      this.ownerDocument.removeEventListener("pointerdown", this.outside), (t = this.ownerDocument.defaultView) == null || t.removeEventListener("resize", this.reposition);
    }
    reset() {
      this.from = this.getAttribute("from") || "", this.to = this.getAttribute("to") || "";
      const t = /^\d{4}-\d{2}-\d{2}$/.test(this.from) ? /* @__PURE__ */ new Date(`${this.from}T12:00:00`) : /* @__PURE__ */ new Date();
      this.month = t.getFullYear() * 12 + t.getMonth();
    }
    close(t = !0) {
      var n;
      this.open = !1, this.render(), t && ((n = this.root.querySelector("[data-toggle]")) == null || n.focus());
    }
    date(t) {
      return t ? x() ? U(t) : `${t.slice(8, 10)}.${t.slice(5, 7)}.${t.slice(0, 4)}` : "Pick a date";
    }
    calendar(t) {
      const n = new Date(Math.floor(t / 12), t % 12, 1), i = (n.getDay() + 6) % 7, s = new Date(n.getFullYear(), n.getMonth() + 1, 0).getDate();
      return `<section class="month"><h3>${y(x() ? z(n, "yearMonth", !1) : n.toLocaleDateString("en", { month: "long", year: "numeric" }))}</h3><div class="days">${(x() ? [1, 2, 3, 4, 5, 6, 0].map((c) => y(x().shortestDayNames[c] || "")) : ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"]).map((c) => `<span class="weekday">${c}</span>`).join("")}${"<span></span>".repeat(i)}${Array.from({ length: s }, (c, u) => {
        const p = `${n.getFullYear()}-${String(n.getMonth() + 1).padStart(2, "0")}-${String(u + 1).padStart(2, "0")}`, d = p === this.from || p === this.to, f = this.from && this.to && p > this.from && p < this.to;
        return `<button type="button" data-day="${p}" aria-label="${p}" aria-pressed="${d || !!f}" class="day ${d ? "edge" : f ? "inside" : ""} ${p === this.from ? "start" : ""} ${p === this.to ? "end" : ""} ${!this.to || this.to === this.from ? "single" : ""}">${u + 1}</button>`;
      }).join("")}</div></section>`;
    }
    render() {
      var c, u, p, d, f;
      const t = this.getAttribute("from") || "", n = this.getAttribute("to") || "", i = V(), s = ((c = i.find((l) => l.from === t && l.to === n)) == null ? void 0 : c.label) || "Custom";
      q(this.root, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/date-range.css"><div class="range"><span class="label">Date range</span><div class="anchor"><button type="button" class="trigger" data-toggle aria-haspopup="dialog" aria-expanded="${this.open}"><strong>${s}</strong><span class="dates">${this.date(t)} – ${this.date(n)}</span><span class="chevron">▾</span></button>${this.open ? `<div class="popover" role="dialog" aria-label="Choose date range"><nav aria-label="Date presets">${i.map((l, b) => `<button type="button" data-preset="${b}" aria-pressed="${l.from === this.from && l.to === this.to}">${l.label}<span>${l.from === this.from && l.to === this.to ? "✓" : ""}</span></button>`).join("")}</nav><div class="custom"><div class="draft"><div class="${this.from ? "" : "active"}"><span>From</span><strong>${this.date(this.from)}</strong></div><span>→</span><div class="${this.from && !this.to ? "active" : ""}"><span>To</span><strong>${this.date(this.to)}</strong></div></div><div class="calendars"><button type="button" class="previous" data-month="-1" aria-label="Previous month">‹</button><button type="button" class="next" data-month="1" aria-label="Next month">›</button>${this.calendar(this.month)}${this.calendar(this.month + 1)}</div><footer><span>${this.from && this.to ? `${this.date(this.from)} – ${this.date(this.to)}` : "Select an end date"}</span><button type="button" data-cancel>Cancel</button><button type="button" class="apply" data-apply ${!this.from || !this.to ? "disabled" : ""}>Apply</button></footer></div></div>` : ""}</div></div>`), (u = this.root.querySelector("link")) == null || u.addEventListener("load", this.reposition), this.reposition(), (p = this.root.querySelector("[data-toggle]")) == null || p.addEventListener("click", () => {
        var l;
        if (this.open) {
          this.close();
          return;
        }
        this.reset(), this.open = !0, this.render(), (l = this.root.querySelector("[data-preset]")) == null || l.focus();
      }), (d = this.root.querySelector("[data-cancel]")) == null || d.addEventListener("click", () => this.close()), (f = this.root.querySelector("[data-apply]")) == null || f.addEventListener("click", () => {
        !this.from || !this.to || this.from > this.to || (this.setAttribute("from", this.from), this.setAttribute("to", this.to), this.close(), this.dispatchEvent(new CustomEvent("date-range-apply", { bubbles: !0, composed: !0, detail: { from: this.from, to: this.to } })));
      }), this.root.querySelectorAll("[data-preset]").forEach((l) => l.addEventListener("click", () => {
        var $;
        const b = i[Number(l.dataset.preset)];
        this.from = b.from, this.to = b.to;
        const w = /* @__PURE__ */ new Date(`${this.from}T12:00:00`);
        this.month = w.getFullYear() * 12 + w.getMonth(), this.render(), ($ = this.root.querySelector(`[data-preset="${l.dataset.preset}"]`)) == null || $.focus();
      })), this.root.querySelectorAll("[data-month]").forEach((l) => l.addEventListener("click", () => {
        var b;
        this.month += Number(l.dataset.month), this.render(), (b = this.root.querySelector(`[data-month="${l.dataset.month}"]`)) == null || b.focus();
      })), this.root.querySelectorAll("[data-day]").forEach((l) => l.addEventListener("click", () => {
        var w;
        const b = l.dataset.day;
        !this.from || this.to || b < this.from ? (this.from = b, this.to = "") : this.to = b, this.render(), (w = this.root.querySelector(`[data-day="${b}"]`)) == null || w.focus();
      })), this.onkeydown = (l) => {
        l.key === "Escape" && this.open && (l.preventDefault(), l.stopPropagation(), this.close());
      };
    }
  }
  customElements.define("ekom-date-range", a);
}
let P;
function X(a) {
  const e = a.querySelector(".ekom-design-logo");
  e && (P ?? (P = new v().config().then((r) => r.ekomVersion).catch(() => {
  })), P.then((r) => {
    r && (e.title = `Ekom ${r}`, e.alt = `Ekom ${r}`);
  }));
}
function q(a, e) {
  const r = a.ownerDocument.createElement("template");
  r.innerHTML = e;
  const t = Array.from(a.children).filter((i) => i.matches('style, link[rel="stylesheet"]')), n = Array.from(r.content.children).filter((i) => i.matches('style, link[rel="stylesheet"]'));
  for (const i of n) {
    const s = t.find((c) => c.outerHTML === i.outerHTML);
    s ? t.splice(t.indexOf(s), 1) : a.appendChild(i.cloneNode(!0)), i.remove();
  }
  for (const i of t) i.remove();
  for (const i of Array.from(a.childNodes))
    (i.nodeType !== 1 || !i.matches('style, link[rel="stylesheet"]')) && i.remove();
  a.appendChild(r.content), X(a);
}
const K = (a) => class extends a {
  connectedCallback() {
  }
  disconnectedCallback() {
  }
}, ee = "/ekom/printer/manager", m = (a) => String(a ?? "").replace(/[&<>"']/g, (e) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[e]), S = (a) => {
  if (!a) return "Never";
  const e = new Date(/[zZ]$|[+-]\d\d:\d\d$/.test(a) ? a : a + "Z");
  return x() ? G(e) : e.toLocaleString(void 0, { year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}, te = (a) => ({
  PendingRender: "Generating slip",
  Ready: "Queued",
  Claimed: "With agent",
  ConfirmedPrinted: "Confirmed printed",
  Submitted: "Submitted to printer",
  NeedsReview: "Needs review",
  Cancelled: "Cancelled",
  RenderFailed: "Generation failed"
})[a] ?? a;
function re(a, e) {
  const r = a.hasHtml ? ["preview", "history"] : ["history"];
  return e && (a.state === "RenderFailed" && r.push("retry"), ["PendingRender", "Ready", "RenderFailed"].includes(a.state) && r.push("cancel"), ["Submitted", "ConfirmedPrinted"].includes(a.state) && a.hasHtml && r.push("reprint"), a.state === "NeedsReview" && (r.push("resolve-printed"), a.hasHtml && r.push("resolve-reprint"))), r;
}
function ae(a) {
  const e = document.createElement("template");
  e.innerHTML = a;
  const r = document.implementation.createHTMLDocument("Picking slip"), t = new Set("div span p h1 h2 h3 h4 h5 h6 header footer main section article aside table thead tbody tfoot tr th td caption colgroup col ul ol li dl dt dd strong em b i u small br hr pre code blockquote img style".split(" ")), n = new Set("script iframe frame object embed svg math template meta link base form input button textarea select".split(" ")), i = new Set("class id style colspan rowspan scope width height alt title".split(" ")), s = (c, u) => {
    if (c.nodeType === Node.TEXT_NODE) {
      u.appendChild(r.createTextNode(c.textContent ?? ""));
      return;
    }
    if (!(c instanceof Element) || c.namespaceURI !== "http://www.w3.org/1999/xhtml") return;
    const p = c.localName;
    if (n.has(p)) return;
    const d = t.has(p) ? r.createElement(p) : r.createElement("span");
    for (const f of Array.from(c.attributes))
      i.has(f.name) && d.setAttribute(f.name, f.value), p === "img" && f.name === "src" && /^data:image\/(png|jpeg|gif|webp);base64,/i.test(f.value) && d.setAttribute("src", f.value);
    for (const f of Array.from(c.childNodes)) s(f, d);
    u.appendChild(d);
  };
  for (const c of Array.from(e.content.childNodes)) s(c, r.body);
  return '<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src &#39;none&#39;; script-src &#39;none&#39;; style-src &#39;unsafe-inline&#39;; img-src data:; font-src data:; base-uri &#39;none&#39;; form-action &#39;none&#39;; connect-src &#39;none&#39;">' + r.head.innerHTML + "</head><body>" + r.body.innerHTML + "</body></html>";
}
class ne extends K(HTMLElement) {
  constructor() {
    super(...arguments);
    g(this, "panel", this.attachShadow({ mode: "open" }));
    g(this, "capabilities");
    g(this, "jobs", { jobs: [], total: 0, page: 1, pageSize: 25 });
    g(this, "stations", []);
    g(this, "state", "");
    g(this, "station", "");
    g(this, "search", "");
    g(this, "page", 1);
    g(this, "loading", !0);
    g(this, "error", "");
    g(this, "busy", !1);
    g(this, "generation", 0);
    g(this, "cancelConfirmation");
    g(this, "requests", /* @__PURE__ */ new Map());
  }
  connectedCallback() {
    var r;
    super.connectedCallback(), this.hasAttribute("classic") || (r = this.consumeContext) == null || r.call(this, "umbThemeContext", (t) => {
      t && this.observe(t.theme, (n) => {
        this.toggleAttribute("data-ekom-dark", n === "umb-dark-theme");
      }, "ekom-manager-theme");
    }), this.render(), this.initialize();
  }
  disconnectedCallback() {
    var r;
    this.generation++, (r = this.cancelConfirmation) == null || r.call(this), super.disconnectedCallback();
  }
  async json(r, t) {
    const n = await fetch(ee + r, {
      credentials: "same-origin",
      ...t,
      headers: { Accept: "application/json", ...t == null ? void 0 : t.headers }
    });
    if (!n.ok) throw new Error(n.status === 404 ? "Printing is unavailable or the stored slip has expired." : n.status === 403 ? "You do not have permission to perform this action." : n.status === 409 ? "The job changed or cannot be updated. Refresh the list." : "Could not contact printing. Try refreshing.");
    return n.json();
  }
  async initialize() {
    var r;
    try {
      this.hasAttribute("classic") || (h.stores = await new v().stores(), h.stores.some((t) => t.alias === h.filters.store) || (h.filters.store = ((r = h.stores[0]) == null ? void 0 : r.alias) || ""), R()), this.capabilities = await this.json("/capabilities"), await this.load();
    } catch (t) {
      this.error = String(t.message), this.loading = !1, this.render();
    }
  }
  async load() {
    const r = ++this.generation;
    this.loading = !0, this.error = "", this.render();
    try {
      const t = new URLSearchParams({ page: String(this.page), state: this.state, station: this.station, search: this.search });
      this.hasAttribute("classic") || (t.set("store", h.filters.store), t.set("currency", h.filters.currency || ""));
      const [n, i] = await Promise.all([this.json("/jobs?" + t), this.json("/stations")]);
      if (r !== this.generation) return;
      this.jobs = n, this.stations = this.hasAttribute("classic") ? i : i.filter((s) => s.stores.includes(h.filters.store));
    } catch (t) {
      r === this.generation && (this.error = t.message);
    } finally {
      r === this.generation && (this.loading = !1, this.render());
    }
  }
  render() {
    var f, l, b, w, $, A, M;
    const r = this.loading || this.busy ? "disabled" : "", t = this.stations.filter((o) => o.health.online && o.health.compatible).length, n = this.jobs.jobs.filter((o) => ["NeedsReview", "RenderFailed"].includes(o.state)).length, i = Math.max(1, Math.ceil(this.jobs.total / this.jobs.pageSize)), s = (o) => ({ NeedsReview: "warning", RenderFailed: "danger", Submitted: "success", ConfirmedPrinted: "success", Ready: "info", Claimed: "info" })[o] ?? "neutral", c = (o) => o.replace(/([a-z])([A-Z])/g, "$1 $2");
    q(this.panel, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css"><link rel="stylesheet" href="/App_Plugins/Ekom/dist/printing-layout.css"><main aria-busy="${this.loading}">${Y("printing", "data-field", !this.hasAttribute("classic"))}
      <header class="page-heading"><div><p class="subtitle">Track picking slips and warehouse printer connections.</p></div><div class="heading-actions"><span class="connection"><span class="dot"></span>${this.loading ? "Checking stations…" : `${t} of ${this.stations.length} stations online`}</span><button data-reload ${r}>${this.loading ? "Refreshing…" : "Refresh"}</button></div></header>
      <section class="card" aria-labelledby="jobs-heading"><div class="ekom-print-toolbar"><div class="section-heading"><div class="title-group"><h2 id="jobs-heading">Print jobs</h2><span class="count" aria-live="polite">${this.loading ? "Loading…" : `${this.jobs.total} ${this.jobs.total === 1 ? "job" : "jobs"}`}</span></div>${this.state || this.station || this.search ? "<button data-clear>Clear filters</button>" : ""}</div>
      <div class="filters"><label>Status<select data-filter="state">${[["", "All statuses"], ["attention", "Needs attention"], ["PendingRender", "Generating slip"], ["Ready", "Queued"], ["Claimed", "With agent"], ["Submitted", "Submitted to printer"], ["ConfirmedPrinted", "Confirmed printed"], ["Cancelled", "Cancelled"]].map(([o, k]) => `<option value="${o}" ${o === this.state ? "selected" : ""}>${k}</option>`).join("")}</select></label>
      <label>Station<select data-filter="station"><option value="">All stations</option>${this.stations.map((o) => `<option value="${m(o.alias)}" ${o.alias === this.station ? "selected" : ""}>${m(o.alias)}</option>`).join("")}</select></label>
      <label class="search">Order number<input data-filter="search" value="${m(this.search)}" placeholder="Search by order number" maxlength="100" type="search"></label></div></div>
      ${this.error ? `<p class="error" role="alert">${m(this.error)}</p>` : ""}
      ${!this.loading && n ? `<div class="notice"><div><strong>${n} ${n === 1 ? "job on this page needs" : "jobs on this page need"} attention</strong>${this.jobs.jobs.some((o) => o.state === "NeedsReview") ? "Check the physical printer before arranging another copy of an uncertain print." : "Review the affected jobs and retry generating their picking slips."}</div>${this.state !== "attention" ? "<button data-attention>View all needing attention</button>" : ""}</div>` : ""}
      <div class="table" tabindex="0" role="region" aria-label="Print jobs; scroll horizontally on small screens"><table><thead><tr><th scope="col">Order</th><th scope="col">Station</th><th scope="col">Status</th><th scope="col">Created</th><th scope="col">Actions</th></tr></thead><tbody>
      ${this.jobs.jobs.map((o) => {
      var k;
      return `<tr><td><strong>${m(o.orderNumber || "Order unavailable")}</strong><small>${m(o.storeAlias)}</small></td><td>${m(o.stationAlias)}<small>${o.copies} ${o.copies === 1 ? "copy" : "copies"}</small></td>
      <td class="status-cell"><span class="badge ${s(o.state)}"><span class="dot"></span>${m(te(o.state))}</span>${o.state === "NeedsReview" ? "<small>Printing could not be confirmed.</small>" : o.state === "RenderFailed" ? "<small>The picking slip could not be generated.</small>" : ""}</td><td class="created">${m(S(o.createdUtc))}</td>
      <td><div class="actions">${re(o, ((k = this.capabilities) == null ? void 0 : k.canManage) === !0).map((C) => `<button aria-label="${m({ preview: "Preview", history: "History for", retry: "Retry generation for", cancel: "Cancel", reprint: "Reprint", "resolve-printed": "Mark as printed", "resolve-reprint": "Print again after checking" }[C] + " " + o.orderNumber)}" data-job="${m(o.id)}" data-action="${C}" ${r}>${{ preview: "Preview", history: "History", retry: "Retry generation", cancel: "Cancel", reprint: "Reprint", "resolve-printed": "Mark as printed", "resolve-reprint": "Print again after checking" }[C]}</button>`).join("")}</div></td></tr>`;
    }).join("")}
      ${this.jobs.jobs.length === 0 ? `<tr><td colspan="5"><div class="empty"><strong>${this.loading ? "Loading print jobs…" : "No print jobs to show"}</strong><p>${this.loading ? "Fetching the latest queue and station information." : this.state || this.station || this.search ? "Try a different order number or clear your filters." : "Picking slips appear here when orders reach a configured status."}</p></div></td></tr>` : ""}</tbody></table></div>
      <div class="pager"><span>${this.jobs.total ? `${(this.page - 1) * this.jobs.pageSize + 1}–${Math.min(this.page * this.jobs.pageSize, this.jobs.total)} of ${this.jobs.total} jobs` : "0 jobs"}</span><div class="pager-controls"><button data-page="-1" ${this.page <= 1 || this.loading ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${i}</span><button data-page="1" ${this.page >= i || this.loading ? "disabled" : ""}>Next</button></div></div>
      <p class="footnote"><strong>Submitted to printer</strong> confirms delivery to the print queue. It does not confirm that paper was printed.</p></section>
      <div class="stations-heading"><div><h2 id="stations-heading">Printer stations</h2><p>Connection health and automatic printing rules.</p></div><span class="count">${this.stations.length} ${this.stations.length === 1 ? "station" : "stations"}</span></div>
      <section class="station-grid" aria-labelledby="stations-heading">${this.stations.map((o) => `<article class="station-card"><div class="station-top"><div><h3>${m(o.alias)}</h3><p class="printer-name">${m(o.health.printerName || "No printer reported")}</p></div><span class="badge ${o.health.online ? o.health.compatible ? "success" : "warning" : "neutral"}"><span class="dot"></span>${o.health.online ? o.health.compatible ? "Online" : "Update required" : "Offline"}</span></div><dl><dt>Store</dt><dd>${m(o.stores.join(", "))}</dd><dt>Print when</dt><dd><div class="chips">${o.triggerMode === "ExplicitCapture" ? '<span class="chip">Explicit capture API</span>' : o.statuses.map((k) => `<span class="chip">${m(c(k))}</span>`).join("") || "No statuses configured"}</div></dd><dt>Copies</dt><dd>${o.copies} ${o.copies === 1 ? "copy" : "copies"} · ${m(o.policy === "OncePerOrder" ? "Once per order" : o.policy === "OncePerStatus" ? "Once per status" : c(o.policy))}</dd><dt>Automation</dt><dd>${o.enabled ? "Enabled" : "Disabled"}</dd><dt>Last connected</dt><dd>${m(S(o.health.lastSeenUtc))}</dd></dl><div class="station-bottom"><span>${o.health.agentVersion ? `Agent ${m(o.health.agentVersion)}` : "Agent not connected"}</span><span>Settings managed externally</span></div></article>`).join("")}
      ${!this.loading && !this.stations.length ? '<div class="station-card empty"><strong>No printer stations</strong><p>No stations are available for your stores.</p></div>' : ""}</section>
      <details class="setup-help"><summary>How do I change printer settings?</summary><p>Change status rules and copies in the website’s Ekom printing configuration. Select the physical printer in the Windows agent. Station settings are read-only here.</p></details>
      <dialog aria-labelledby="detail-heading"><div class="dialog-heading"><h2 id="detail-heading">Print job details</h2><button data-close>Close</button></div><div data-detail></div></dialog></main>`), Z(this.panel, () => {
      this.load();
    }), (f = this.panel.querySelector('[data-field="store"]')) == null || f.addEventListener("change", (o) => {
      h.filters.store = o.target.value, R(), this.station = "", this.page = 1, this.load();
    });
    const u = this.panel.querySelector('[data-filter="search"]'), p = () => {
      this.search !== (u == null ? void 0 : u.value) && (this.search = (u == null ? void 0 : u.value) || "", this.page = 1, this.load());
    };
    u == null || u.addEventListener("change", p), (l = this.panel.querySelector("[data-reload]")) == null || l.addEventListener("click", () => {
      this.load();
    }), (b = this.panel.querySelector("[data-clear]")) == null || b.addEventListener("click", () => {
      this.state = "", this.station = "", this.search = "", this.page = 1, this.load();
    }), (w = this.panel.querySelector("[data-attention]")) == null || w.addEventListener("click", () => {
      this.state = "attention", this.station = "", this.search = "", this.page = 1, this.load();
    });
    for (const o of ["state", "station"]) ($ = this.panel.querySelector(`[data-filter="${o}"]`)) == null || $.addEventListener("change", (k) => {
      this[o] = k.target.value, this.page = 1, this.load();
    });
    (A = this.panel.querySelector('[data-filter="search"]')) == null || A.addEventListener("keydown", (o) => {
      o.key === "Enter" && p();
    }), this.panel.querySelectorAll("[data-page]").forEach((o) => o.addEventListener("click", () => {
      this.page += Number(o.dataset.page), this.load();
    })), this.panel.querySelectorAll("[data-action]").forEach((o) => o.addEventListener("click", () => {
      this.action(o.dataset.job, o.dataset.action);
    }));
    const d = this.panel.querySelector("dialog");
    (M = this.panel.querySelector("[data-close]")) == null || M.addEventListener("click", () => d.close()), d.addEventListener("cancel", (o) => {
      o.preventDefault(), d.close();
    }), d.addEventListener("keydown", (o) => {
      o.key === "Escape" && (o.preventDefault(), o.stopPropagation(), d.close());
    }), d.addEventListener("click", (o) => {
      if (o.target !== d) return;
      const k = d.getBoundingClientRect();
      (o.clientX < k.left || o.clientX > k.right || o.clientY < k.top || o.clientY > k.bottom) && d.close();
    });
  }
  confirmAction(r) {
    return this.cancelConfirmation ? Promise.resolve(!1) : new Promise((t) => {
      const n = document.createElement("dialog");
      n.className = "action-confirmation", n.setAttribute("aria-labelledby", "print-action-title"), n.setAttribute("aria-describedby", "print-action-message"), n.innerHTML = `<h2 id="print-action-title">Confirm printing action</h2><p id="print-action-message">${m(r)}</p><div class="confirmation-actions"><button data-dismiss autofocus>Cancel</button><button class="primary" data-confirm>Confirm</button></div>`;
      const i = (s) => {
        this.cancelConfirmation = void 0, n.remove(), t(s);
      };
      this.cancelConfirmation = () => i(!1), n.addEventListener("close", () => i(n.returnValue === "confirm"), { once: !0 }), n.querySelector("[data-dismiss]").addEventListener("click", () => n.close()), n.querySelector("[data-confirm]").addEventListener("click", () => n.close("confirm")), this.panel.append(n), n.showModal();
    });
  }
  async action(r, t) {
    var n;
    if (!this.busy)
      try {
        if (t === "preview" || t === "history") {
          const c = this.panel.querySelector("dialog"), u = this.panel.querySelector("[data-detail]");
          if (t === "preview") {
            const p = await this.json(`/jobs/${r}/preview`), d = document.createElement("iframe");
            d.title = "Picking slip preview", d.setAttribute("sandbox", ""), d.referrerPolicy = "no-referrer", d.srcdoc = ae(p.html), u.replaceChildren(d), u.style.height = "90%";
          } else {
            const p = await this.json(`/jobs/${r}/history`);
            u.style.height = "auto", u.innerHTML = `<h3>Job history</h3>${p.delivery ? `<p>Claimed: ${m(S(p.delivery.claimedUtc))}</p><p>Reported: ${m(S(p.delivery.reportedUtc))}</p><p>Windows queue reference: ${m(p.delivery.spoolJobId || "Not reported")}</p>` : "<p>Not yet claimed by an agent.</p>"}<ul>${p.actions.map((d) => `<li>${m(S(d.createdUtc))} — ${m({ "resolve-printed": "Confirmed physical printing", "resolve-reprint": "Closed uncertain job and queued replacement" }[d.action] ?? d.action)} by ${m(d.actor)}</li>`).join("")}</ul>`;
          }
          c.showModal();
          return;
        }
        if (!((n = this.capabilities) != null && n.canManage) || !await this.confirmAction(t === "resolve-printed" ? "Have you checked the physical output and confirmed this picking slip was printed? This records your confirmation without printing again." : t === "resolve-reprint" ? "Check the physical printer and clear any pending copy from the Windows print queue first. A delayed original may still print. Continue to close this uncertain job and queue one replacement job with the original copy count?" : t === "reprint" ? "Create another copy of this stored picking slip?" : t === "cancel" ? "Cancel this queued picking slip?" : "Retry generating this picking slip?")) return;
        const i = r + ":" + t, s = this.requests.get(i) ?? crypto.randomUUID();
        this.requests.set(i, s), this.busy = !0, this.render(), await this.json(`/jobs/${r}/actions`, { method: "POST", headers: {
          "Content-Type": "application/json",
          [this.capabilities.csrfHeader]: this.capabilities.csrfToken
        }, body: JSON.stringify({ action: t, requestId: s }) }), this.requests.delete(i), this.busy = !1, await this.load();
      } catch (i) {
        this.busy = !1, this.error = i.message, this.render();
      }
  }
}
customElements.get("ekom-printing-section-view") || customElements.define("ekom-printing-section-view", ne);
export {
  re as availableActions,
  ae as createPreviewDocument,
  ne as default,
  te as stateLabel
};
