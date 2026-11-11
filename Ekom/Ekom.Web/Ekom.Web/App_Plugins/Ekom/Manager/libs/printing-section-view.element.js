var q = Object.defineProperty;
var N = (a, t, r) => t in a ? q(a, t, { enumerable: !0, configurable: !0, writable: !0, value: r }) : a[t] = r;
var g = (a, t, r) => N(a, typeof t != "symbol" ? t + "" : t, r);
const P = /* @__PURE__ */ new Date(), F = new Date(P.getFullYear(), 0, 1), h = {
  filters: {
    dateFrom: j(F),
    dateTo: j(P),
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
  async changeOrderStates(t, r, e, i, n) {
    return this.postBody(`/ekom/manager/orderStates/${encodeURIComponent(t)}`, { expectedRevision: r, action: e, notify: i, fulfilledQuantities: n });
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
  async searchOrders(t, r, e = 20) {
    return this.getJson("/ekom/manager/SearchOrders", {
      ...t,
      start: t.dateFrom,
      end: t.dateTo,
      page: r,
      pageSize: e
    });
  }
  /** Counts for several order views in one request, so the view tabs do not each run a search. */
  async orderViewCounts(t, r) {
    if (!r.length) return [];
    const { orderStatus: e, ...i } = t;
    return this.getJson("/ekom/manager/OrderViewCounts", {
      ...i,
      start: t.dateFrom,
      end: t.dateTo,
      views: r
    });
  }
  async exportOrders(t, r, e) {
    return this.getBlob("/ekom/manager/ExportOrders", {
      ...t,
      start: t.dateFrom,
      end: t.dateTo,
      page: 1,
      pageSize: r,
      includeOrderLines: e
    });
  }
  async orderReviews(t) {
    const r = [];
    for (let e = 0; ; e += 200) {
      const i = await this.getJson(`/ekom/manager/orders/${encodeURIComponent(t)}/reviews`, { skip: e, take: 200 });
      if (r.push(...i), i.length < 200) return r;
    }
  }
  async reviewOrder(t, r, e, i) {
    return this.postBody(`/ekom/manager/orders/${encodeURIComponent(t)}/reviews/${encodeURIComponent(r)}/review`, { comment: e, resolved: i });
  }
  async statusList() {
    return this.getJson("/ekom/manager/StatusList");
  }
  async stores() {
    const [t, r] = await Promise.all([this.getJson("/ekom/manager/stores"), this.getJson("/ekom/manager/store-currencies")]);
    return t.map((e) => {
      const i = r.find((n) => n.alias === e.alias);
      return { ...e, currencies: (i == null ? void 0 : i.currencies) || [], currencyFormats: ((i == null ? void 0 : i.formats) || []).filter((n) => !!n), dateFormat: (i == null ? void 0 : i.dateFormat) || void 0 };
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
  async releaseOrderLock(t, r) {
    return this.postBody(`/ekom/manager/releaseOrderLock/${encodeURIComponent(t)}`, { releaseToken: r });
  }
  async giftcardRecovery(t) {
    return this.getJson(`/ekom/manager/giftcardRecovery/${encodeURIComponent(t)}`);
  }
  async releaseGiftcardRecovery(t, r, e, i) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(t)}/release`, { attemptId: r, claimId: e, token: i });
  }
  async reconcileGiftcardRecovery(t, r, e) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(t)}/reconcile`, { attemptId: r, token: e });
  }
  async executeOrderAction(t, r) {
    const e = await fetch(`/ekom/manager/OrderActions/${encodeURIComponent(t)}/${encodeURIComponent(r)}`, {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json, application/octet-stream" }
    });
    if (!e.ok)
      throw await v.createError(e, "Order action failed.");
    return e;
  }
  async changeOrderStatus(t, r, e) {
    return this.postJson("/ekom/manager/changeOrderStatus", { orderId: t, orderStatus: r, notify: e });
  }
  async updateCustomerInformation(t, r, e) {
    return this.postBody("/ekom/manager/UpdateCustomerInformation", { orderId: t, customer: r, shipping: e });
  }
  async addOrderLine(t, r, e, i) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(t)}/OrderLines`, { productId: r, variantId: e, quantity: i });
  }
  async updateShippingProvider(t, r, e) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(t)}/ShippingProvider`, { providerId: r, customData: e });
  }
  async removeOrderLine(t, r) {
    const e = await fetch(`/ekom/manager/Order/${encodeURIComponent(t)}/OrderLines/${encodeURIComponent(r)}`, {
      method: "DELETE",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!e.ok)
      throw await v.createError(e, "Error removing order line.");
    return e.json();
  }
  async charts(t, r) {
    return this.getJson("/ekom/manager/charts", {
      start: t.dateFrom,
      end: t.dateTo,
      orderStatus: t.orderStatus,
      store: t.store,
      currency: t.currency
    }, r);
  }
  async mostSoldProducts(t, r, e = 20, i) {
    return this.getJson("/ekom/manager/MostSoldProducts", {
      start: t.dateFrom,
      end: t.dateTo,
      orderStatus: t.orderStatus,
      store: t.store,
      currency: t.currency,
      page: r,
      pageSize: e
    }, i);
  }
  async getJson(t, r, e) {
    const i = await fetch(t + O(r), {
      credentials: "same-origin",
      signal: e,
      headers: { Accept: "application/json" }
    });
    if (!i.ok)
      throw await v.createError(i, "Request failed.");
    return i.json();
  }
  async getBlob(t, r) {
    const e = await fetch(t + O(r), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!e.ok)
      throw await v.createError(e, "Request failed.");
    return e.blob();
  }
  async postJson(t, r) {
    const e = await fetch(t + O(r), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!e.ok)
      throw await v.createError(e, "Request failed.");
    return e.json();
  }
  async postBody(t, r) {
    const e = await fetch(t, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json"
      },
      body: JSON.stringify(r)
    });
    if (!e.ok)
      throw await v.createError(e, "Request failed.");
    return e.json();
  }
  static async createError(t, r) {
    var n, s;
    if (t.status === 401 || t.redirected && /\/Account\/Login/i.test(t.url))
      return new Error("Your session has expired. Sign in to Umbraco and reload the manager.");
    if ((s = (n = t.headers) == null ? void 0 : n.get("content-type")) != null && s.includes("text/html"))
      return new Error(`${r} Status ${t.status}. Reload the manager and try again.`);
    const e = await t.text();
    let i = e || r;
    try {
      i = JSON.parse(e).message || i;
    } catch {
    }
    return new Error(i || `${r} Status ${t.status}.`);
  }
}
function O(a) {
  if (!a)
    return "";
  const t = new URLSearchParams();
  for (const [e, i] of Object.entries(a))
    Array.isArray(i) ? i.forEach((n) => t.append(e, String(n))) : t.set(e, i == null ? "" : String(i));
  const r = t.toString();
  return r ? `?${r}` : "";
}
function j(a) {
  const t = String(a.getMonth() + 1).padStart(2, "0"), r = String(a.getDate()).padStart(2, "0");
  return `${a.getFullYear()}-${t}-${r}`;
}
function w() {
  var a;
  return (a = h.stores.find((t) => t.alias === h.filters.store)) == null ? void 0 : a.dateFormat;
}
function L(a) {
  if (typeof a == "string" && /^\d{4}-\d{2}-\d{2}$/.test(a))
    return { year: Number(a.slice(0, 4)), month: Number(a.slice(5, 7)), day: Number(a.slice(8, 10)), hour: 0, minute: 0, second: 0 };
  const t = a instanceof Date ? a : new Date(a);
  if (!Number.isNaN(t.getTime()))
    return { year: t.getFullYear(), month: t.getMonth() + 1, day: t.getDate(), hour: t.getHours(), minute: t.getMinutes(), second: t.getSeconds() };
}
function _(a, t, r) {
  const e = (i) => String(i).padStart(2, "0");
  return t.replace(/'([^']*)'|\\(.)|d{1,4}|M{1,4}|y{1,5}|H{1,2}|h{1,2}|m{1,2}|s{1,2}|t{1,2}|\/|:/g, (i, n, s) => {
    if (n !== void 0) return n;
    if (s !== void 0) return s;
    switch (i) {
      case "d":
        return String(a.day);
      case "dd":
        return e(a.day);
      case "ddd":
      case "dddd":
        return r.shortestDayNames[new Date(a.year, a.month - 1, a.day).getDay()] || "";
      case "M":
        return String(a.month);
      case "MM":
        return e(a.month);
      case "MMM":
        return r.abbreviatedMonthNames[a.month - 1] || String(a.month);
      case "MMMM":
        return r.monthNames[a.month - 1] || String(a.month);
      case "yy":
        return e(a.year % 100);
      case "H":
        return String(a.hour);
      case "HH":
        return e(a.hour);
      case "h":
        return String(a.hour % 12 || 12);
      case "hh":
        return e(a.hour % 12 || 12);
      case "m":
        return String(a.minute);
      case "mm":
        return e(a.minute);
      case "s":
        return String(a.second);
      case "ss":
        return e(a.second);
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
function I(a) {
  if (!a) return "";
  const t = L(a);
  if (!t) return String(a);
  const r = w();
  return r ? _(t, r.shortDate, r) : new Date(t.year, t.month - 1, t.day).toLocaleDateString();
}
function U(a, t, r = !0) {
  const e = L(a);
  if (!e) return String(a);
  const i = w(), n = new Date(Date.UTC(e.year, e.month - 1, e.day));
  if (!i)
    return n.toLocaleDateString("en-GB", { timeZone: "UTC", month: r ? "short" : "long", year: "numeric" });
  const s = i.yearMonth;
  return _(e, r ? s.replace(/M{4}/g, "MMM") : s, i);
}
function z(a) {
  if (!a)
    return "";
  const t = a instanceof Date ? a : new Date(a);
  if (Number.isNaN(t.getTime()))
    return String(a);
  const r = w(), e = L(t);
  return r && e ? `${_(e, r.shortDate, r)} ${_(e, r.shortTime, r)}` : new Intl.DateTimeFormat(void 0, {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  }).format(t);
}
function y(a) {
  return String(a ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
const G = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`, H = `
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

  ${G}
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
`, M = {
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
H.replaceAll(":host", "&").replace(/#[0-9a-f]{3,8}\b/gi, (a) => M[a.toLowerCase()] ? `var(${M[a.toLowerCase()]})` : a);
function R() {
  var r;
  const a = h.stores.find((e) => e.alias === h.filters.store), t = (a == null ? void 0 : a.currencies) || [];
  if (!t.includes(h.filters.currency || "")) {
    const e = (r = a == null ? void 0 : a.currency) == null ? void 0 : r.isoCurrencySymbol;
    h.filters.currency = t.find((i) => i === e) || t[0] || "";
  }
}
function B(a = "data-field") {
  const t = h.stores.find((e) => e.alias === h.filters.store), r = h.filters.currency || "";
  return `<div class="ekom-store-switch"><span>Store</span><details class="ekom-dropdown ekom-store-currency"><summary aria-label="Store and currency"><strong>${y((t == null ? void 0 : t.title) || "Select store")}</strong>${r ? `<span class="ekom-currency-code">${y(r)}</span>` : ""}<span class="ekom-store-chevron">▾</span></summary><div class="ekom-currency-popover"><div class="ekom-currency-heading"><strong>Store ·<br>Currency</strong><span>Amounts shown in the selected currency</span></div>${h.stores.map((e) => `<section class="ekom-currency-store ${e.alias === (t == null ? void 0 : t.alias) ? "is-selected" : ""}" aria-label="${y(e.title)}"><h3>${y(e.title)}</h3><div class="ekom-currency-choices">${(e.currencies || []).map((i) => `<button type="button" data-header-store="${y(e.alias)}" data-header-currency="${y(i)}" aria-label="${y(e.title)} — ${y(i)}" aria-pressed="${e.alias === (t == null ? void 0 : t.alias) && i === r}">${y(i)}${e.alias === (t == null ? void 0 : t.alias) && i === r ? '<span aria-hidden="true">✓</span>' : ""}</button>`).join("") || '<span class="ekom-currency-empty">No currencies configured or recorded</span>'}</div></section>`).join("")}</div></details><select hidden aria-label="Store" ${a}="store">${h.stores.map((e) => `<option value="${y(e.alias)}" ${e.alias === (t == null ? void 0 : t.alias) ? "selected" : ""}>${y(e.title)}</option>`).join("")}</select></div>`;
}
function J(a, t = "data-field", r = !0) {
  if (typeof document < "u" && !document.querySelector("link[data-ekom-fonts]")) {
    const n = document.createElement("link");
    n.rel = "stylesheet", n.href = "/App_Plugins/Ekom/dist/manager-fonts.css", n.dataset.ekomFonts = "", document.head.append(n);
  }
  const e = ["orders", "analytics", "customers", "printing"], i = typeof location > "u" ? "/umbraco/section/ekommanager" : location.pathname.replace(/\/view\/(orders|analytics|customers|printing)\/?$/, "").replace(/\/$/, "");
  return `<header class="ekom-design-header"><img src="/App_Plugins/Ekom/dist/ekom-logo.svg" alt="Ekom" class="ekom-design-logo"><nav aria-label="Ekom sections" class="ekom-section-switch">${e.map((n) => `<a href="${y(i + "/view/" + n)}" ${n === a ? 'aria-current="page"' : ""}>${n[0].toUpperCase() + n.slice(1)}</a>`).join("")}</nav>${r ? B(t) : '<span class="ekom-store-switch">Accessible stores</span>'}</header>`;
}
function Y(a = /* @__PURE__ */ new Date()) {
  const t = (i) => `${i.getFullYear()}-${String(i.getMonth() + 1).padStart(2, "0")}-${String(i.getDate()).padStart(2, "0")}`, r = (i) => new Date(a.getFullYear(), a.getMonth(), a.getDate() + i), e = t(a);
  return [
    { label: "Today", from: e, to: e },
    { label: "Yesterday", from: t(r(-1)), to: t(r(-1)) },
    { label: "Last 7 days", from: t(r(-6)), to: e },
    { label: "Last 30 days", from: t(r(-29)), to: e },
    { label: "This month", from: t(new Date(a.getFullYear(), a.getMonth(), 1)), to: e },
    { label: "Last month", from: t(new Date(a.getFullYear(), a.getMonth() - 1, 1)), to: t(new Date(a.getFullYear(), a.getMonth(), 0)) },
    { label: "This quarter", from: t(new Date(a.getFullYear(), Math.floor(a.getMonth() / 3) * 3, 1)), to: e },
    { label: "This year", from: t(new Date(a.getFullYear(), 0, 1)), to: e }
  ];
}
function V(a, t) {
  var r, e;
  W(), (r = a.querySelector(".ekom-store-currency")) == null || r.addEventListener("keydown", (i) => {
    var s;
    if (i.key !== "Escape") return;
    const n = i.currentTarget;
    n.open = !1, (s = n.querySelector("summary")) == null || s.focus(), i.stopPropagation();
  }), a.querySelectorAll("[data-header-store]").forEach((i) => i.addEventListener("click", () => {
    var s;
    const n = a.querySelector('[data-field="store"], [data-filter="store"]');
    n && (h.filters.currency = i.dataset.headerCurrency || "", (s = i.closest("details")) == null || s.removeAttribute("open"), n.value = i.dataset.headerStore || "", n.dispatchEvent(new Event("change", { bubbles: !0 })));
  })), a.querySelectorAll("[data-shared-status]").forEach((i) => i.addEventListener("change", () => {
    const n = [0, 1, 2].map((s) => Array.from(a.querySelectorAll(`[data-shared-status="${s}"]:checked`), (c) => c.value).join(","));
    h.filters.orderStatus = n.some(Boolean) ? "states:" + n.join(";") : "AllOrders", t();
  })), (e = a.querySelector("ekom-date-range")) == null || e.addEventListener("date-range-apply", (i) => {
    const n = i.detail;
    h.filters.dateFrom = n.from, h.filters.dateTo = n.to, t();
  });
}
function W() {
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
        const e = this.root.querySelector(".popover"), i = this.root.querySelector(".anchor"), n = this.ownerDocument.defaultView;
        if (!e || !i || !n) return;
        if (n.innerWidth <= 650) {
          e.style.left = "";
          return;
        }
        const s = i.getBoundingClientRect().left;
        e.style.left = `${Math.min(0, Math.max(24 - s, n.innerWidth - 24 - s - e.getBoundingClientRect().width))}px`;
      });
      g(this, "outside", (e) => {
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
      var i;
      this.open = !1, this.render(), e && ((i = this.root.querySelector("[data-toggle]")) == null || i.focus());
    }
    date(e) {
      return e ? w() ? I(e) : `${e.slice(8, 10)}.${e.slice(5, 7)}.${e.slice(0, 4)}` : "Pick a date";
    }
    calendar(e) {
      const i = new Date(Math.floor(e / 12), e % 12, 1), n = (i.getDay() + 6) % 7, s = new Date(i.getFullYear(), i.getMonth() + 1, 0).getDate();
      return `<section class="month"><h3>${y(w() ? U(i, "yearMonth", !1) : i.toLocaleDateString("en", { month: "long", year: "numeric" }))}</h3><div class="days">${(w() ? [1, 2, 3, 4, 5, 6, 0].map((c) => y(w().shortestDayNames[c] || "")) : ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"]).map((c) => `<span class="weekday">${c}</span>`).join("")}${"<span></span>".repeat(n)}${Array.from({ length: s }, (c, u) => {
        const p = `${i.getFullYear()}-${String(i.getMonth() + 1).padStart(2, "0")}-${String(u + 1).padStart(2, "0")}`, d = p === this.from || p === this.to, f = this.from && this.to && p > this.from && p < this.to;
        return `<button type="button" data-day="${p}" aria-label="${p}" aria-pressed="${d || !!f}" class="day ${d ? "edge" : f ? "inside" : ""} ${p === this.from ? "start" : ""} ${p === this.to ? "end" : ""} ${!this.to || this.to === this.from ? "single" : ""}">${u + 1}</button>`;
      }).join("")}</div></section>`;
    }
    render() {
      var c, u, p, d, f;
      const e = this.getAttribute("from") || "", i = this.getAttribute("to") || "", n = Y(), s = ((c = n.find((l) => l.from === e && l.to === i)) == null ? void 0 : c.label) || "Custom";
      T(this.root, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/date-range.css"><div class="range"><span class="label">Date range</span><div class="anchor"><button type="button" class="trigger" data-toggle aria-haspopup="dialog" aria-expanded="${this.open}"><strong>${s}</strong><span class="dates">${this.date(e)} – ${this.date(i)}</span><span class="chevron">▾</span></button>${this.open ? `<div class="popover" role="dialog" aria-label="Choose date range"><nav aria-label="Date presets">${n.map((l, b) => `<button type="button" data-preset="${b}" aria-pressed="${l.from === this.from && l.to === this.to}">${l.label}<span>${l.from === this.from && l.to === this.to ? "✓" : ""}</span></button>`).join("")}</nav><div class="custom"><div class="draft"><div class="${this.from ? "" : "active"}"><span>From</span><strong>${this.date(this.from)}</strong></div><span>→</span><div class="${this.from && !this.to ? "active" : ""}"><span>To</span><strong>${this.date(this.to)}</strong></div></div><div class="calendars"><button type="button" class="previous" data-month="-1" aria-label="Previous month">‹</button><button type="button" class="next" data-month="1" aria-label="Next month">›</button>${this.calendar(this.month)}${this.calendar(this.month + 1)}</div><footer><span>${this.from && this.to ? `${this.date(this.from)} – ${this.date(this.to)}` : "Select an end date"}</span><button type="button" data-cancel>Cancel</button><button type="button" class="apply" data-apply ${!this.from || !this.to ? "disabled" : ""}>Apply</button></footer></div></div>` : ""}</div></div>`), (u = this.root.querySelector("link")) == null || u.addEventListener("load", this.reposition), this.reposition(), (p = this.root.querySelector("[data-toggle]")) == null || p.addEventListener("click", () => {
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
        const b = n[Number(l.dataset.preset)];
        this.from = b.from, this.to = b.to;
        const x = /* @__PURE__ */ new Date(`${this.from}T12:00:00`);
        this.month = x.getFullYear() * 12 + x.getMonth(), this.render(), ($ = this.root.querySelector(`[data-preset="${l.dataset.preset}"]`)) == null || $.focus();
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
  customElements.define("ekom-date-range", a);
}
let D;
function Z(a) {
  const t = a.querySelector(".ekom-design-logo");
  t && (D ?? (D = new v().config().then((r) => r.ekomVersion).catch(() => {
  })), D.then((r) => {
    r && (t.title = `Ekom ${r}`, t.alt = `Ekom ${r}`);
  }));
}
function T(a, t) {
  const r = a.ownerDocument.createElement("template");
  r.innerHTML = t;
  const e = Array.from(a.children).filter((n) => n.matches('style, link[rel="stylesheet"]')), i = Array.from(r.content.children).filter((n) => n.matches('style, link[rel="stylesheet"]'));
  for (const n of i) {
    const s = e.find((c) => c.outerHTML === n.outerHTML);
    s ? e.splice(e.indexOf(s), 1) : a.appendChild(n.cloneNode(!0)), n.remove();
  }
  for (const n of e) n.remove();
  for (const n of Array.from(a.childNodes))
    (n.nodeType !== 1 || !n.matches('style, link[rel="stylesheet"]')) && n.remove();
  a.appendChild(r.content), Z(a);
}
const Q = (a) => class extends a {
  connectedCallback() {
  }
  disconnectedCallback() {
  }
}, X = "/ekom/printer/manager", m = (a) => String(a ?? "").replace(/[&<>"']/g, (t) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[t]), S = (a) => {
  if (!a) return "Never";
  const t = new Date(/[zZ]$|[+-]\d\d:\d\d$/.test(a) ? a : a + "Z");
  return w() ? z(t) : t.toLocaleString(void 0, { year: "numeric", month: "short", day: "numeric", hour: "2-digit", minute: "2-digit" });
}, K = (a) => ({
  PendingRender: "Generating slip",
  Ready: "Queued",
  Claimed: "With agent",
  ConfirmedPrinted: "Confirmed printed",
  Submitted: "Submitted to printer",
  NeedsReview: "Needs review",
  Cancelled: "Cancelled",
  RenderFailed: "Generation failed"
})[a] ?? a;
function ee(a, t) {
  const r = a.hasHtml ? ["preview", "history"] : ["history"];
  return t && (a.state === "RenderFailed" && r.push("retry"), ["PendingRender", "Ready", "RenderFailed"].includes(a.state) && r.push("cancel"), ["Submitted", "ConfirmedPrinted"].includes(a.state) && a.hasHtml && r.push("reprint"), a.state === "NeedsReview" && (r.push("resolve-printed"), a.hasHtml && r.push("resolve-reprint"))), r;
}
function te(a) {
  const t = document.createElement("template");
  t.innerHTML = a;
  const r = document.implementation.createHTMLDocument("Picking slip"), e = new Set("div span p h1 h2 h3 h4 h5 h6 header footer main section article aside table thead tbody tfoot tr th td caption colgroup col ul ol li dl dt dd strong em b i u small br hr pre code blockquote img style".split(" ")), i = new Set("script iframe frame object embed svg math template meta link base form input button textarea select".split(" ")), n = new Set("class id style colspan rowspan scope width height alt title".split(" ")), s = (c, u) => {
    if (c.nodeType === Node.TEXT_NODE) {
      u.appendChild(r.createTextNode(c.textContent ?? ""));
      return;
    }
    if (!(c instanceof Element) || c.namespaceURI !== "http://www.w3.org/1999/xhtml") return;
    const p = c.localName;
    if (i.has(p)) return;
    const d = e.has(p) ? r.createElement(p) : r.createElement("span");
    for (const f of Array.from(c.attributes))
      n.has(f.name) && d.setAttribute(f.name, f.value), p === "img" && f.name === "src" && /^data:image\/(png|jpeg|gif|webp);base64,/i.test(f.value) && d.setAttribute("src", f.value);
    for (const f of Array.from(c.childNodes)) s(f, d);
    u.appendChild(d);
  };
  for (const c of Array.from(t.content.childNodes)) s(c, r.body);
  return '<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src &#39;none&#39;; script-src &#39;none&#39;; style-src &#39;unsafe-inline&#39;; img-src data:; font-src data:; base-uri &#39;none&#39;; form-action &#39;none&#39;; connect-src &#39;none&#39;">' + r.head.innerHTML + "</head><body>" + r.body.innerHTML + "</body></html>";
}
class re extends Q(HTMLElement) {
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
    super.connectedCallback(), this.hasAttribute("classic") || (r = this.consumeContext) == null || r.call(this, "umbThemeContext", (e) => {
      e && this.observe(e.theme, (i) => {
        this.toggleAttribute("data-ekom-dark", i === "umb-dark-theme");
      }, "ekom-manager-theme");
    }), this.render(), this.initialize();
  }
  disconnectedCallback() {
    var r;
    this.generation++, (r = this.cancelConfirmation) == null || r.call(this), super.disconnectedCallback();
  }
  async json(r, e) {
    const i = await fetch(X + r, {
      credentials: "same-origin",
      ...e,
      headers: { Accept: "application/json", ...e == null ? void 0 : e.headers }
    });
    if (!i.ok) throw new Error(i.status === 404 ? "Printing is unavailable or the stored slip has expired." : i.status === 403 ? "You do not have permission to perform this action." : i.status === 409 ? "The job changed or cannot be updated. Refresh the list." : "Could not contact printing. Try refreshing.");
    return i.json();
  }
  async initialize() {
    var r;
    try {
      this.hasAttribute("classic") || (h.stores = await new v().stores(), h.stores.some((e) => e.alias === h.filters.store) || (h.filters.store = ((r = h.stores[0]) == null ? void 0 : r.alias) || ""), R()), this.capabilities = await this.json("/capabilities"), await this.load();
    } catch (e) {
      this.error = String(e.message), this.loading = !1, this.render();
    }
  }
  async load() {
    const r = ++this.generation;
    this.loading = !0, this.error = "", this.render();
    try {
      const e = new URLSearchParams({ page: String(this.page), state: this.state, station: this.station, search: this.search });
      this.hasAttribute("classic") || (e.set("store", h.filters.store), e.set("currency", h.filters.currency || ""));
      const [i, n] = await Promise.all([this.json("/jobs?" + e), this.json("/stations")]);
      if (r !== this.generation) return;
      this.jobs = i, this.stations = this.hasAttribute("classic") ? n : n.filter((s) => s.stores.includes(h.filters.store));
    } catch (e) {
      r === this.generation && (this.error = e.message);
    } finally {
      r === this.generation && (this.loading = !1, this.render());
    }
  }
  render() {
    var f, l, b, x, $, E, A;
    const r = this.loading || this.busy ? "disabled" : "", e = this.stations.filter((o) => o.health.online && o.health.compatible).length, i = this.jobs.jobs.filter((o) => ["NeedsReview", "RenderFailed"].includes(o.state)).length, n = Math.max(1, Math.ceil(this.jobs.total / this.jobs.pageSize)), s = (o) => ({ NeedsReview: "warning", RenderFailed: "danger", Submitted: "success", ConfirmedPrinted: "success", Ready: "info", Claimed: "info" })[o] ?? "neutral", c = (o) => o.replace(/([a-z])([A-Z])/g, "$1 $2");
    T(this.panel, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css"><link rel="stylesheet" href="/App_Plugins/Ekom/dist/printing-layout.css"><main aria-busy="${this.loading}">${J("printing", "data-field", !this.hasAttribute("classic"))}
      <header class="page-heading"><div><p class="subtitle">Track picking slips and warehouse printer connections.</p></div><div class="heading-actions"><span class="connection"><span class="dot"></span>${this.loading ? "Checking stations…" : `${e} of ${this.stations.length} stations online`}</span><button data-reload ${r}>${this.loading ? "Refreshing…" : "Refresh"}</button></div></header>
      <section class="card" aria-labelledby="jobs-heading"><div class="ekom-print-toolbar"><div class="section-heading"><div class="title-group"><h2 id="jobs-heading">Print jobs</h2><span class="count" aria-live="polite">${this.loading ? "Loading…" : `${this.jobs.total} ${this.jobs.total === 1 ? "job" : "jobs"}`}</span></div>${this.state || this.station || this.search ? "<button data-clear>Clear filters</button>" : ""}</div>
      <div class="filters"><label>Status<select data-filter="state">${[["", "All statuses"], ["attention", "Needs attention"], ["PendingRender", "Generating slip"], ["Ready", "Queued"], ["Claimed", "With agent"], ["Submitted", "Submitted to printer"], ["ConfirmedPrinted", "Confirmed printed"], ["Cancelled", "Cancelled"]].map(([o, k]) => `<option value="${o}" ${o === this.state ? "selected" : ""}>${k}</option>`).join("")}</select></label>
      <label>Station<select data-filter="station"><option value="">All stations</option>${this.stations.map((o) => `<option value="${m(o.alias)}" ${o.alias === this.station ? "selected" : ""}>${m(o.alias)}</option>`).join("")}</select></label>
      <label class="search">Order number<input data-filter="search" value="${m(this.search)}" placeholder="Search by order number" maxlength="100" type="search"></label></div></div>
      ${this.error ? `<p class="error" role="alert">${m(this.error)}</p>` : ""}
      ${!this.loading && i ? `<div class="notice"><div><strong>${i} ${i === 1 ? "job on this page needs" : "jobs on this page need"} attention</strong>${this.jobs.jobs.some((o) => o.state === "NeedsReview") ? "Check the physical printer before arranging another copy of an uncertain print." : "Review the affected jobs and retry generating their picking slips."}</div>${this.state !== "attention" ? "<button data-attention>View all needing attention</button>" : ""}</div>` : ""}
      <div class="table" tabindex="0" role="region" aria-label="Print jobs; scroll horizontally on small screens"><table><thead><tr><th scope="col">Order</th><th scope="col">Station</th><th scope="col">Status</th><th scope="col">Created</th><th scope="col">Actions</th></tr></thead><tbody>
      ${this.jobs.jobs.map((o) => {
      var k;
      return `<tr><td><strong>${m(o.orderNumber || "Order unavailable")}</strong><small>${m(o.storeAlias)}</small></td><td>${m(o.stationAlias)}<small>${o.copies} ${o.copies === 1 ? "copy" : "copies"}</small></td>
      <td class="status-cell"><span class="badge ${s(o.state)}"><span class="dot"></span>${m(K(o.state))}</span>${o.state === "NeedsReview" ? "<small>Printing could not be confirmed.</small>" : o.state === "RenderFailed" ? "<small>The picking slip could not be generated.</small>" : ""}</td><td class="created">${m(S(o.createdUtc))}</td>
      <td><div class="actions">${ee(o, ((k = this.capabilities) == null ? void 0 : k.canManage) === !0).map((C) => `<button aria-label="${m({ preview: "Preview", history: "History for", retry: "Retry generation for", cancel: "Cancel", reprint: "Reprint", "resolve-printed": "Mark as printed", "resolve-reprint": "Print again after checking" }[C] + " " + o.orderNumber)}" data-job="${m(o.id)}" data-action="${C}" ${r}>${{ preview: "Preview", history: "History", retry: "Retry generation", cancel: "Cancel", reprint: "Reprint", "resolve-printed": "Mark as printed", "resolve-reprint": "Print again after checking" }[C]}</button>`).join("")}</div></td></tr>`;
    }).join("")}
      ${this.jobs.jobs.length === 0 ? `<tr><td colspan="5"><div class="empty"><strong>${this.loading ? "Loading print jobs…" : "No print jobs to show"}</strong><p>${this.loading ? "Fetching the latest queue and station information." : this.state || this.station || this.search ? "Try a different order number or clear your filters." : "Picking slips appear here when orders reach a configured status."}</p></div></td></tr>` : ""}</tbody></table></div>
      <div class="pager"><span>${this.jobs.total ? `${(this.page - 1) * this.jobs.pageSize + 1}–${Math.min(this.page * this.jobs.pageSize, this.jobs.total)} of ${this.jobs.total} jobs` : "0 jobs"}</span><div class="pager-controls"><button data-page="-1" ${this.page <= 1 || this.loading ? "disabled" : ""}>Previous</button><span>Page ${this.page} of ${n}</span><button data-page="1" ${this.page >= n || this.loading ? "disabled" : ""}>Next</button></div></div>
      <p class="footnote"><strong>Submitted to printer</strong> confirms delivery to the print queue. It does not confirm that paper was printed.</p></section>
      <div class="stations-heading"><div><h2 id="stations-heading">Printer stations</h2><p>Connection health and automatic printing rules.</p></div><span class="count">${this.stations.length} ${this.stations.length === 1 ? "station" : "stations"}</span></div>
      <section class="station-grid" aria-labelledby="stations-heading">${this.stations.map((o) => `<article class="station-card"><div class="station-top"><div><h3>${m(o.alias)}</h3><p class="printer-name">${m(o.health.printerName || "No printer reported")}</p></div><span class="badge ${o.health.online ? o.health.compatible ? "success" : "warning" : "neutral"}"><span class="dot"></span>${o.health.online ? o.health.compatible ? "Online" : "Update required" : "Offline"}</span></div><dl><dt>Store</dt><dd>${m(o.stores.join(", "))}</dd><dt>Print when</dt><dd><div class="chips">${o.triggerMode === "ExplicitCapture" ? '<span class="chip">Explicit capture API</span>' : o.statuses.map((k) => `<span class="chip">${m(c(k))}</span>`).join("") || "No statuses configured"}</div></dd><dt>Copies</dt><dd>${o.copies} ${o.copies === 1 ? "copy" : "copies"} · ${m(o.policy === "OncePerOrder" ? "Once per order" : o.policy === "OncePerStatus" ? "Once per status" : c(o.policy))}</dd><dt>Automation</dt><dd>${o.enabled ? "Enabled" : "Disabled"}</dd><dt>Last connected</dt><dd>${m(S(o.health.lastSeenUtc))}</dd></dl><div class="station-bottom"><span>${o.health.agentVersion ? `Agent ${m(o.health.agentVersion)}` : "Agent not connected"}</span><span>Settings managed externally</span></div></article>`).join("")}
      ${!this.loading && !this.stations.length ? '<div class="station-card empty"><strong>No printer stations</strong><p>No stations are available for your stores.</p></div>' : ""}</section>
      <details class="setup-help"><summary>How do I change printer settings?</summary><p>Change status rules and copies in the website’s Ekom printing configuration. Select the physical printer in the Windows agent. Station settings are read-only here.</p></details>
      <dialog aria-labelledby="detail-heading"><div class="dialog-heading"><h2 id="detail-heading">Print job details</h2><button data-close>Close</button></div><div data-detail></div></dialog></main>`), V(this.panel, () => {
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
    }), (x = this.panel.querySelector("[data-attention]")) == null || x.addEventListener("click", () => {
      this.state = "attention", this.station = "", this.search = "", this.page = 1, this.load();
    });
    for (const o of ["state", "station"]) ($ = this.panel.querySelector(`[data-filter="${o}"]`)) == null || $.addEventListener("change", (k) => {
      this[o] = k.target.value, this.page = 1, this.load();
    });
    (E = this.panel.querySelector('[data-filter="search"]')) == null || E.addEventListener("keydown", (o) => {
      o.key === "Enter" && p();
    }), this.panel.querySelectorAll("[data-page]").forEach((o) => o.addEventListener("click", () => {
      this.page += Number(o.dataset.page), this.load();
    })), this.panel.querySelectorAll("[data-action]").forEach((o) => o.addEventListener("click", () => {
      this.action(o.dataset.job, o.dataset.action);
    }));
    const d = this.panel.querySelector("dialog");
    (A = this.panel.querySelector("[data-close]")) == null || A.addEventListener("click", () => d.close()), d.addEventListener("cancel", (o) => {
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
    return this.cancelConfirmation ? Promise.resolve(!1) : new Promise((e) => {
      const i = document.createElement("dialog");
      i.className = "action-confirmation", i.setAttribute("aria-labelledby", "print-action-title"), i.setAttribute("aria-describedby", "print-action-message"), i.innerHTML = `<h2 id="print-action-title">Confirm printing action</h2><p id="print-action-message">${m(r)}</p><div class="confirmation-actions"><button data-dismiss autofocus>Cancel</button><button class="primary" data-confirm>Confirm</button></div>`;
      const n = (s) => {
        this.cancelConfirmation = void 0, i.remove(), e(s);
      };
      this.cancelConfirmation = () => n(!1), i.addEventListener("close", () => n(i.returnValue === "confirm"), { once: !0 }), i.querySelector("[data-dismiss]").addEventListener("click", () => i.close()), i.querySelector("[data-confirm]").addEventListener("click", () => i.close("confirm")), this.panel.append(i), i.showModal();
    });
  }
  async action(r, e) {
    var i;
    if (!this.busy)
      try {
        if (e === "preview" || e === "history") {
          const c = this.panel.querySelector("dialog"), u = this.panel.querySelector("[data-detail]");
          if (e === "preview") {
            const p = await this.json(`/jobs/${r}/preview`), d = document.createElement("iframe");
            d.title = "Picking slip preview", d.setAttribute("sandbox", ""), d.referrerPolicy = "no-referrer", d.srcdoc = te(p.html), u.replaceChildren(d), u.style.height = "90%";
          } else {
            const p = await this.json(`/jobs/${r}/history`);
            u.style.height = "auto", u.innerHTML = `<h3>Job history</h3>${p.delivery ? `<p>Claimed: ${m(S(p.delivery.claimedUtc))}</p><p>Reported: ${m(S(p.delivery.reportedUtc))}</p><p>Windows queue reference: ${m(p.delivery.spoolJobId || "Not reported")}</p>` : "<p>Not yet claimed by an agent.</p>"}<ul>${p.actions.map((d) => `<li>${m(S(d.createdUtc))} — ${m({ "resolve-printed": "Confirmed physical printing", "resolve-reprint": "Closed uncertain job and queued replacement" }[d.action] ?? d.action)} by ${m(d.actor)}</li>`).join("")}</ul>`;
          }
          c.showModal();
          return;
        }
        if (!((i = this.capabilities) != null && i.canManage) || !await this.confirmAction(e === "resolve-printed" ? "Have you checked the physical output and confirmed this picking slip was printed? This records your confirmation without printing again." : e === "resolve-reprint" ? "Check the physical printer and clear any pending copy from the Windows print queue first. A delayed original may still print. Continue to close this uncertain job and queue one replacement job with the original copy count?" : e === "reprint" ? "Create another copy of this stored picking slip?" : e === "cancel" ? "Cancel this queued picking slip?" : "Retry generating this picking slip?")) return;
        const n = r + ":" + e, s = this.requests.get(n) ?? crypto.randomUUID();
        this.requests.set(n, s), this.busy = !0, this.render(), await this.json(`/jobs/${r}/actions`, { method: "POST", headers: {
          "Content-Type": "application/json",
          [this.capabilities.csrfHeader]: this.capabilities.csrfToken
        }, body: JSON.stringify({ action: e, requestId: s }) }), this.requests.delete(n), this.busy = !1, await this.load();
      } catch (n) {
        this.busy = !1, this.error = n.message, this.render();
      }
  }
}
customElements.get("ekom-printing-section-view") || customElements.define("ekom-printing-section-view", re);
export {
  ee as availableActions,
  te as createPreviewDocument,
  re as default,
  K as stateLabel
};
