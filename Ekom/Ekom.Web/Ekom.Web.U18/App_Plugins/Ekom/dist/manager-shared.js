var he = Object.defineProperty;
var be = (r, e, a) => e in r ? he(r, e, { enumerable: !0, configurable: !0, writable: !0, value: a }) : r[e] = a;
var D = (r, e, a) => be(r, typeof e != "symbol" ? e + "" : e, a);
function Fe(r, e) {
  var t, o, n;
  if (e || (e = (t = r == null ? void 0 : r.currency) == null ? void 0 : t.isoCurrencySymbol), !e) return r == null ? void 0 : r.currency;
  const a = (o = r == null ? void 0 : r.currencyFormats) == null ? void 0 : o.find((s) => (s == null ? void 0 : s.code) === e);
  return { ...((n = r == null ? void 0 : r.currency) == null ? void 0 : n.isoCurrencySymbol) === e ? r.currency : {}, isoCurrencySymbol: e, ...a ? { format: a, currencyDecimalDigits: a.decimalDigits } : {} };
}
function ke(r, e) {
  const [a, t] = Math.abs(r).toFixed(Math.max(0, e.decimalDigits)).split("."), o = a.replace(/\B(?=(\d{3})+(?!\d))/g, e.groupSeparator) + (t ? e.decimalSeparator + t : ""), n = [`${e.symbol}${o}`, `${o}${e.symbol}`, `${e.symbol} ${o}`, `${o} ${e.symbol}`][e.positivePattern] ?? `${o} ${e.symbol}`;
  return (r < 0 && +(a + "." + (t || "0")) != 0 ? "-" : "") + n;
}
const ce = /* @__PURE__ */ new Date(), ye = new Date(ce.getFullYear(), 0, 1), f = {
  filters: {
    dateFrom: ie(ye),
    dateTo: ie(ce),
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
class A {
  async changeOrderStates(e, a, t, o, n) {
    return this.postBody(`/ekom/manager/orderStates/${encodeURIComponent(e)}`, { expectedRevision: a, action: t, notify: o, fulfilledQuantities: n });
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
  async searchOrders(e, a, t = 20) {
    return this.getJson("/ekom/manager/SearchOrders", {
      ...e,
      start: e.dateFrom,
      end: e.dateTo,
      page: a,
      pageSize: t
    });
  }
  /** Counts for several order views in one request, so the view tabs do not each run a search. */
  async orderViewCounts(e, a) {
    if (!a.length) return [];
    const { orderStatus: t, ...o } = e;
    return this.getJson("/ekom/manager/OrderViewCounts", {
      ...o,
      start: e.dateFrom,
      end: e.dateTo,
      views: a
    });
  }
  async exportOrders(e, a, t) {
    return this.getBlob("/ekom/manager/ExportOrders", {
      ...e,
      start: e.dateFrom,
      end: e.dateTo,
      page: 1,
      pageSize: a,
      includeOrderLines: t
    });
  }
  async orderReviews(e) {
    const a = [];
    for (let t = 0; ; t += 200) {
      const o = await this.getJson(`/ekom/manager/orders/${encodeURIComponent(e)}/reviews`, { skip: t, take: 200 });
      if (a.push(...o), o.length < 200) return a;
    }
  }
  async reviewOrder(e, a, t, o) {
    return this.postBody(`/ekom/manager/orders/${encodeURIComponent(e)}/reviews/${encodeURIComponent(a)}/review`, { comment: t, resolved: o });
  }
  async statusList() {
    return this.getJson("/ekom/manager/StatusList");
  }
  async stores() {
    const [e, a] = await Promise.all([this.getJson("/ekom/manager/stores"), this.getJson("/ekom/manager/store-currencies")]);
    return e.map((t) => {
      const o = a.find((n) => n.alias === t.alias);
      return { ...t, currencies: (o == null ? void 0 : o.currencies) || [], currencyFormats: ((o == null ? void 0 : o.formats) || []).filter((n) => !!n), dateFormat: (o == null ? void 0 : o.dateFormat) || void 0 };
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
  async orderLogs(e) {
    return this.getJson(`/ekom/manager/OrderLogs/${encodeURIComponent(e)}`);
  }
  async orderActions(e) {
    return this.getJson(`/ekom/manager/OrderActions/${encodeURIComponent(e)}`);
  }
  async orderLockStatus(e) {
    return this.getJson(`/ekom/manager/orderLockStatus/${encodeURIComponent(e)}`);
  }
  async releaseOrderLock(e, a) {
    return this.postBody(`/ekom/manager/releaseOrderLock/${encodeURIComponent(e)}`, { releaseToken: a });
  }
  async giftcardRecovery(e) {
    return this.getJson(`/ekom/manager/giftcardRecovery/${encodeURIComponent(e)}`);
  }
  async releaseGiftcardRecovery(e, a, t, o) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(e)}/release`, { attemptId: a, claimId: t, token: o });
  }
  async reconcileGiftcardRecovery(e, a, t) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(e)}/reconcile`, { attemptId: a, token: t });
  }
  async executeOrderAction(e, a) {
    const t = await fetch(`/ekom/manager/OrderActions/${encodeURIComponent(e)}/${encodeURIComponent(a)}`, {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json, application/octet-stream" }
    });
    if (!t.ok)
      throw await A.createError(t, "Order action failed.");
    return t;
  }
  async changeOrderStatus(e, a, t) {
    return this.postJson("/ekom/manager/changeOrderStatus", { orderId: e, orderStatus: a, notify: t });
  }
  async updateCustomerInformation(e, a, t) {
    return this.postBody("/ekom/manager/UpdateCustomerInformation", { orderId: e, customer: a, shipping: t });
  }
  async addOrderLine(e, a, t, o) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(e)}/OrderLines`, { productId: a, variantId: t, quantity: o });
  }
  async updateShippingProvider(e, a, t) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(e)}/ShippingProvider`, { providerId: a, customData: t });
  }
  async removeOrderLine(e, a) {
    const t = await fetch(`/ekom/manager/Order/${encodeURIComponent(e)}/OrderLines/${encodeURIComponent(a)}`, {
      method: "DELETE",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await A.createError(t, "Error removing order line.");
    return t.json();
  }
  async charts(e, a) {
    return this.getJson("/ekom/manager/charts", {
      start: e.dateFrom,
      end: e.dateTo,
      orderStatus: e.orderStatus,
      store: e.store,
      currency: e.currency
    }, a);
  }
  async mostSoldProducts(e, a, t = 20, o) {
    return this.getJson("/ekom/manager/MostSoldProducts", {
      start: e.dateFrom,
      end: e.dateTo,
      orderStatus: e.orderStatus,
      store: e.store,
      currency: e.currency,
      page: a,
      pageSize: t
    }, o);
  }
  async getJson(e, a, t) {
    const o = await fetch(e + W(a), {
      credentials: "same-origin",
      signal: t,
      headers: { Accept: "application/json" }
    });
    if (!o.ok)
      throw await A.createError(o, "Request failed.");
    return o.json();
  }
  async getBlob(e, a) {
    const t = await fetch(e + W(a), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!t.ok)
      throw await A.createError(t, "Request failed.");
    return t.blob();
  }
  async postJson(e, a) {
    const t = await fetch(e + W(a), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await A.createError(t, "Request failed.");
    return t.json();
  }
  async postBody(e, a) {
    const t = await fetch(e, {
      method: "POST",
      credentials: "same-origin",
      headers: {
        Accept: "application/json",
        "Content-Type": "application/json"
      },
      body: JSON.stringify(a)
    });
    if (!t.ok)
      throw await A.createError(t, "Request failed.");
    return t.json();
  }
  static async createError(e, a) {
    var n, s;
    if (e.status === 401 || e.redirected && /\/Account\/Login/i.test(e.url))
      return new Error("Your session has expired. Sign in to Umbraco and reload the manager.");
    if ((s = (n = e.headers) == null ? void 0 : n.get("content-type")) != null && s.includes("text/html"))
      return new Error(`${a} Status ${e.status}. Reload the manager and try again.`);
    const t = await e.text();
    let o = t || a;
    try {
      o = JSON.parse(t).message || o;
    } catch {
    }
    return new Error(o || `${a} Status ${e.status}.`);
  }
}
function W(r) {
  if (!r)
    return "";
  const e = new URLSearchParams();
  for (const [t, o] of Object.entries(r))
    Array.isArray(o) ? o.forEach((n) => e.append(t, String(n))) : e.set(t, o == null ? "" : String(o));
  const a = e.toString();
  return a ? `?${a}` : "";
}
function ie(r) {
  const e = String(r.getMonth() + 1).padStart(2, "0"), a = String(r.getDate()).padStart(2, "0");
  return `${r.getFullYear()}-${e}-${a}`;
}
function P() {
  var r;
  return (r = f.stores.find((e) => e.alias === f.filters.store)) == null ? void 0 : r.dateFormat;
}
function X(r) {
  if (typeof r == "string" && /^\d{4}-\d{2}-\d{2}$/.test(r))
    return { year: Number(r.slice(0, 4)), month: Number(r.slice(5, 7)), day: Number(r.slice(8, 10)), hour: 0, minute: 0, second: 0 };
  const e = r instanceof Date ? r : new Date(r);
  if (!Number.isNaN(e.getTime()))
    return { year: e.getFullYear(), month: e.getMonth() + 1, day: e.getDate(), hour: e.getHours(), minute: e.getMinutes(), second: e.getSeconds() };
}
function J(r, e, a) {
  const t = (o) => String(o).padStart(2, "0");
  return e.replace(/'([^']*)'|\\(.)|d{1,4}|M{1,4}|y{1,5}|H{1,2}|h{1,2}|m{1,2}|s{1,2}|t{1,2}|\/|:/g, (o, n, s) => {
    if (n !== void 0) return n;
    if (s !== void 0) return s;
    switch (o) {
      case "d":
        return String(r.day);
      case "dd":
        return t(r.day);
      case "ddd":
      case "dddd":
        return a.shortestDayNames[new Date(r.year, r.month - 1, r.day).getDay()] || "";
      case "M":
        return String(r.month);
      case "MM":
        return t(r.month);
      case "MMM":
        return a.abbreviatedMonthNames[r.month - 1] || String(r.month);
      case "MMMM":
        return a.monthNames[r.month - 1] || String(r.month);
      case "yy":
        return t(r.year % 100);
      case "H":
        return String(r.hour);
      case "HH":
        return t(r.hour);
      case "h":
        return String(r.hour % 12 || 12);
      case "hh":
        return t(r.hour % 12 || 12);
      case "m":
        return String(r.minute);
      case "mm":
        return t(r.minute);
      case "s":
        return String(r.second);
      case "ss":
        return t(r.second);
      case "t":
        return (r.hour < 12 ? a.amDesignator : a.pmDesignator).slice(0, 1);
      case "tt":
        return r.hour < 12 ? a.amDesignator : a.pmDesignator;
      case "/":
        return a.dateSeparator || "/";
      case ":":
        return a.timeSeparator || ":";
      default:
        return String(r.year);
    }
  });
}
function Q(r) {
  if (!r) return "";
  const e = X(r);
  if (!e) return String(r);
  const a = P();
  return a ? J(e, a.shortDate, a) : new Date(e.year, e.month - 1, e.day).toLocaleDateString();
}
function xe(r, e, a = !0) {
  const t = X(r);
  if (!t) return String(r);
  const o = P(), n = new Date(Date.UTC(t.year, t.month - 1, t.day));
  if (!o)
    return n.toLocaleDateString("en-GB", { timeZone: "UTC", month: a ? "short" : "long", ...e === "monthDay" ? { day: "numeric" } : e === "yearMonth" ? { year: "numeric" } : {} });
  if (e === "month") return (a ? o.abbreviatedMonthNames : o.monthNames)[t.month - 1] || String(t.month);
  const s = e === "monthDay" ? o.monthDay : o.yearMonth;
  return J(t, a ? s.replace(/M{4}/g, "MMM") : s, o);
}
function ze(r) {
  if (!r)
    return "";
  const e = r instanceof Date ? r : new Date(r);
  if (Number.isNaN(e.getTime()))
    return String(r);
  const a = P(), t = X(e);
  return a && t ? `${J(t, a.shortDate, a)} ${J(t, a.shortTime, a)}` : new Intl.DateTimeFormat(void 0, {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  }).format(e);
}
function u(r) {
  return String(r ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
function je(r) {
  const e = document.createElement("textarea");
  return e.innerHTML = String(r ?? ""), e.value;
}
function ve(r) {
  return r.enumValue || r.value || "";
}
function Ie(r, e) {
  const t = [], o = Math.max(e || 1, 1);
  let n;
  r <= Math.floor(5 / 2) ? n = 1 : r + Math.floor(5 / 2) >= o ? n = Math.max(o - 5 + 1, 1) : n = r - Math.floor(5 / 2);
  for (let s = 0; s < 5; s += 1) {
    const g = n + s;
    g <= o && t.push(g);
  }
  return t.length && Number(t[t.length - 1]) < o && t.push(`...${o}`), t.length && Number(t[0]) > 1 && t.unshift("...1"), t;
}
const me = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`;
async function Ue(r, e) {
  const a = document.createElement("iframe");
  a.title = e, a.setAttribute("aria-hidden", "true"), a.style.cssText = "position:fixed;left:-10000px;top:0;width:800px;height:600px;border:0;";
  try {
    const t = new Promise((n, s) => {
      a.onload = () => n(), a.onerror = () => s(new Error("Unable to load the print document."));
    });
    a.srcdoc = we(r, e), document.body.append(a), await t;
    const o = a.contentWindow;
    if (!o) throw new Error("Print window unavailable.");
    await o.document.fonts.ready, o.addEventListener("afterprint", () => a.remove(), { once: !0 }), o.focus(), o.print();
  } catch (t) {
    throw a.remove(), t;
  }
}
function we(r, e) {
  const a = document.createElement("template");
  return a.innerHTML = r, a.content.querySelectorAll("button, input, select, .ekmOrderGiftcards__actions, .ekmOrderLineThumbnail").forEach((t) => t.remove()), a.content.querySelectorAll("details").forEach((t) => t.setAttribute("open", "")), a.content.querySelectorAll("summary").forEach((t) => t.remove()), a.content.querySelectorAll("a").forEach((t) => t.replaceWith(...Array.from(t.childNodes))), `<!doctype html><html><head><meta charset="utf-8"><title>${u(e)}</title><style>
    ${me}
    @page { size: auto; margin: 14mm; }
    * { box-sizing: border-box; }
    html, body { margin: 0; padding: 0; height: auto; overflow: visible; }
    body { color: #111; background: white; font: 10pt/1.4 Arial, sans-serif; overflow-wrap: anywhere; }
    h1 { font-size: 19pt; margin: 0 0 12px; }
    h4 { font-size: 11pt; margin: 12px 0 6px; }
    h5 { font-size: 10pt; margin: 12px 0 6px !important; }
    h1, h4, h5 { break-after: avoid; }
    p { margin: 4px 0; }
    ul { padding-left: 18px; margin: 6px 0; }
    li { margin: 3px 0; }
    .ekmSplit { display: grid; grid-template-columns: minmax(0, 1fr) minmax(0, 1fr); gap: 20px; margin-bottom: 12px; }
    .ekmSplit__column { min-width: 0; break-inside: avoid; }
    .ekmOrderLineProduct__details { min-width: 0; }
    .ekmOrderLineProduct__details small { color: #555; }
    .umb-table { display: table; width: 100%; table-layout: fixed; border-collapse: collapse; font-size: 9pt; }
    .umb-table-head { display: table-header-group; font-weight: bold; break-inside: avoid; }
    .umb-table-body, .umb-table-footer { display: table-row-group; }
    .umb-table-footer { break-inside: avoid; }
    .umb-table-row { display: table-row; break-inside: avoid; }
    .umb-table-cell { display: table-cell; vertical-align: top; padding: 7px 5px; border-bottom: 1px solid #ccc; white-space: normal; overflow-wrap: anywhere; }
    .umb-table-cell:first-child { display: none; }
    .umb-table-cell:nth-child(2) { width: 35%; }
    .umb-table-cell:nth-child(3) { width: 9%; }
    .umb-table-cell:nth-child(n+4) { width: 14%; text-align: right; }
    .umb-table-footer .umb-table-cell:nth-child(6) { font-weight: bold; }
    .ekmOrderGiftcards__summary { display: flex; flex-wrap: wrap; gap: 12px; }
    dt { font-weight: bold; margin-top: 6px; }
    dd { margin: 0 0 4px; white-space: pre-wrap; }
  </style></head><body>${a.innerHTML}</body></html>`;
}
function Ne(r, e) {
  const a = URL.createObjectURL(r), t = document.createElement("a");
  t.href = a, t.download = e, t.addEventListener("click", (o) => o.stopPropagation()), document.body.append(t), t.click(), t.remove(), URL.revokeObjectURL(a);
}
const pe = `
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

  ${me}
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
`, se = ":is(ekom-orders-section-view, ekom-analytics-section-view, ekom-customers-section-view, ekom-printing-section-view)", de = {
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
}, _e = pe.replaceAll(":host", "&").replace(/#[0-9a-f]{3,8}\b/gi, (r) => de[r.toLowerCase()] ? `var(${de[r.toLowerCase()]})` : r), Se = `
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
`, qe = `@keyframes ekom-shim { 0% { background-position: -320px 0; } 100% { background-position: 320px 0; } }
@keyframes ekom-spin { to { transform: rotate(360deg); } }
${se} {
  ${pe.replaceAll(":host", "&")}
  ${Se}
}
${se}:not([classic]) {
  ${_e}
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
function Be(r) {
  const e = ["Paid", "Fulfilled", "Closed"].includes(r || "") ? "positive" : ["Failed", "Cancelled"].includes(r || "") ? "negative" : ["Pending", "PartiallyPaid", "PartiallyFulfilled", "PartiallyRefunded"].includes(r || "") ? "warning" : "neutral", a = r ? r.replace(/([a-z])([A-Z])/g, "$1 $2") : "Needs review";
  return `<span class="ekom-badge ekom-badge--${e}">${u(a)}</span>`;
}
function $e(r, e) {
  if (e != null && e.format) return ke(r, e.format);
  const a = e == null ? void 0 : e.isoCurrencySymbol, t = (e == null ? void 0 : e.currencyDecimalDigits) ?? (a && /^[A-Z]{3}$/.test(a) ? new Intl.NumberFormat(void 0, { style: "currency", currency: a }).resolvedOptions().maximumFractionDigits : 2), o = e == null ? void 0 : e.currencyValue;
  let n;
  try {
    o && !/^[A-Z]{3}$/.test(o) && (n = Intl.NumberFormat.supportedLocalesOf(o)[0]);
  } catch {
  }
  return new Intl.NumberFormat(n, {
    minimumFractionDigits: t,
    maximumFractionDigits: t,
    ...a && /^[A-Z]{3}$/.test(a) ? { style: "currency", currency: a, ...n ? {} : { currencyDisplay: "code" } } : {}
  }).format(r);
}
function Ge(r, e, a, t = {}) {
  var re;
  const o = r.getBoundingClientRect(), n = window.devicePixelRatio || 1, s = o.width || 300, g = o.height || 320;
  r.width = s * n, r.height = g * n;
  const i = r.getContext("2d");
  if (!i) return;
  i.scale(n, n), i.font = t.templateStyle ? '10px "Ekom Mono", monospace' : "12px sans-serif";
  const y = e.points || [], $ = y.map((m) => m.y == null ? null : Number(m.y)), w = ((re = t.comparison) == null ? void 0 : re.points) || [], c = [...$, ...w.map((m) => m.y)].filter((m) => m != null), b = Math.min(0, ...c), O = Math.max(1, ...c), I = 10 ** (Math.floor(Math.log10(O / 4)) - 1), q = t.integer ? Math.ceil(O / 4) * 4 : t.templateStyle ? Math.ceil(O / 4 / I) * I * 4 : O, Y = (m) => t.formatValue ? t.formatValue(m) : t.currency ? $e(m, t.currency) : m.toLocaleString(void 0, { maximumFractionDigits: t.integer ? 0 : 2 }), K = t.formatAxisValue ?? Y, ee = Array.from({ length: 5 }, (m, l) => q - (q - b) * l / 4), S = t.templateStyle ? 1 : Math.max(48, ...ee.map((m) => i.measureText(K(m)).width + 12)), E = s - (t.templateStyle ? 1 : 16), F = t.templateStyle ? 8 : 20, _ = g - (t.templateStyle ? 26 : 52), h = y.map((m, l) => {
    var v, L, T;
    return {
      x: y.length > 1 ? S + (E - S) * l / (y.length - 1) : (S + E) / 2,
      y: $[l] == null ? null : _ - ($[l] - b) / (q - b) * (_ - F),
      date: m.x || ((v = e.labels) == null ? void 0 : v[l]) || "",
      axisLabel: ((L = t.formatLabel) == null ? void 0 : L.call(t, l)) ?? m.x ?? ((T = e.labels) == null ? void 0 : T[l]) ?? "",
      value: $[l]
    };
  }), H = (m = -1) => {
    var ne;
    i.clearRect(0, 0, s, g), i.font = t.templateStyle ? '10px "Ekom Mono", monospace' : "12px sans-serif";
    const l = typeof getComputedStyle == "function" ? getComputedStyle(r) : void 0, v = (l == null ? void 0 : l.getPropertyValue("--ekom-muted").trim()) || (l == null ? void 0 : l.getPropertyValue("--uui-color-text-alt").trim()) || "#555", L = (l == null ? void 0 : l.getPropertyValue("--ekom-border").trim()) || "#e5e5e5", T = (l == null ? void 0 : l.getPropertyValue("--uui-color-text").trim()) || "", U = (ne = /^#([0-9a-f]{6})$/i.exec(T)) == null ? void 0 : ne[1], N = U ? [0, 2, 4].reduce((p, d) => p + parseInt(U.slice(d, d + 2), 16), 0) > 420 : !1, B = t.templateStyle && !N ? "#10694a" : a;
    i.lineWidth = 1, i.textAlign = t.templateStyle ? "left" : "right", ee.forEach((p, d) => {
      const k = F + (_ - F) * d / 4;
      i.fillStyle = v, i.fillText(K(p), t.templateStyle ? S : S - 8, t.templateStyle ? Math.min(_, k + 3) : k + 4), !(t.templateStyle && (d === 0 || d === 4)) && (i.globalAlpha = t.templateStyle ? 0.5 : 1, i.strokeStyle = L, i.beginPath(), i.moveTo(S, k), i.lineTo(E, k), i.stroke(), i.globalAlpha = 1);
    });
    const G = Math.max(90, ...h.map((p) => i.measureText(p.axisLabel).width + 32)), ae = Math.max(2, Math.floor((E - S) / G)), ge = new Set(Array.from({ length: Math.min(ae, h.length) }, (p, d) => Math.round(d * (h.length - 1) / Math.max(1, Math.min(ae, h.length) - 1))));
    i.fillStyle = v, h.forEach((p, d) => {
      ge.has(d) && (i.textAlign = h.length === 1 ? "center" : d === 0 ? "left" : d === h.length - 1 ? "right" : "center", i.fillText(p.axisLabel, p.x, _ + 24));
    });
    const oe = (p, d) => {
      var x, j;
      i.strokeStyle = d ? "#9b9fad" : B, i.lineWidth = t.templateStyle ? d ? 1.5 : 2.5 : 2, i.lineJoin = "round", i.lineCap = "round", (x = i.setLineDash) == null || x.call(i, d ? [4, 5] : []), i.beginPath();
      let k = !1;
      p.forEach((M) => {
        if (M.y == null) {
          k = !1;
          return;
        }
        k ? i.lineTo(M.x, M.y) : i.moveTo(M.x, M.y), k = !0;
      }), i.stroke(), (j = i.setLineDash) == null || j.call(i, []);
    };
    if (t.templateStyle && h.length > 1) {
      const p = i.createLinearGradient(0, F, 0, _);
      p.addColorStop(0, "rgba(16,105,74,.22)"), p.addColorStop(1, "rgba(16,105,74,0)"), i.fillStyle = p;
      let d = [];
      const k = () => {
        d.length > 1 && (i.beginPath(), i.moveTo(d[0].x, _), d.forEach((x) => i.lineTo(x.x, x.y)), i.lineTo(d[d.length - 1].x, _), i.closePath(), i.fill()), d = [];
      };
      h.forEach((x) => {
        x.y == null ? k() : d.push({ x: x.x, y: x.y });
      }), k();
    }
    w.length && oe(h.map((p, d) => {
      var k;
      return {
        x: p.x,
        y: ((k = w[d]) == null ? void 0 : k.y) == null ? null : _ - (Number(w[d].y) - b) / (q - b) * (_ - F)
      };
    }), !0), oe(h, !1), i.fillStyle = B, h.forEach((p, d) => {
      p.y == null || t.templateStyle && h.length > 1 && d !== m || (i.beginPath(), i.arc(p.x, p.y, d === m ? 5 : 3, 0, Math.PI * 2), i.fill());
    }), h.length || (i.textAlign = "center", i.fillText("No data for this period", (S + E) / 2, g / 2));
    const z = h[m];
    if (z) {
      const p = [z.date, z.value == null ? "—" : Y(z.value)];
      if (w[m]) {
        const R = w[m];
        p.push(`Previous: ${R.x || ""}`, R.y == null ? "—" : Y(R.y));
      }
      const d = Math.max(...p.map((R) => i.measureText(R).width)) + 20, k = Math.max(0, Math.min(s - d, z.x - d / 2)), x = p.length * 18 + 12, j = z.y ?? _, M = Math.max(0, Math.min(g - x, j > x + 20 ? j - x - 12 : j + 12));
      i.fillStyle = "#222", i.fillRect(k, M, d, x), i.fillStyle = "#fff", i.textAlign = "left", p.forEach((R, fe) => i.fillText(R, k + 10, M + 19 + fe * 18));
    }
  }, te = (m) => {
    const l = r.getBoundingClientRect(), v = (m.clientX - l.left) * s / l.width, L = (m.clientY - l.top) * g / l.height;
    if (t.columnHover)
      return !h.length || v < S || v > E || L < F || L > _ ? -1 : h.length === 1 ? 0 : Math.round((v - S) / (E - S) * (h.length - 1));
    let T = -1, U = 16;
    return h.forEach((N, B) => {
      if (N.y == null) return;
      const G = Math.hypot(N.x - v, N.y - L);
      G < U && (T = B, U = G);
    }), T;
  };
  r.onmousemove = (m) => {
    const l = te(m);
    r.style.cursor = l >= 0 ? "pointer" : "default", H(l);
  }, r.onclick = t.onSelect ? (m) => {
    var v;
    const l = te(m);
    l >= 0 && ((v = t.onSelect) == null || v.call(t, l));
  } : null, r.onmouseleave = () => {
    r.style.cursor = "default", H();
  }, H();
}
const Oe = [
  { key: "AllOrders", label: "All" },
  { key: "NeedsReview", label: "Needs review" },
  { key: "states:Open;;Unfulfilled,Picking,Picked,Packed,PartiallyFulfilled", label: "Unfulfilled" },
  { key: "states:Open;Pending,Authorized,PartiallyPaid,Failed;", label: "Unpaid" },
  { key: "states:Open;;", label: "Open" },
  { key: "states:Closed;;", label: "Closed" }
];
function Ve() {
  var a;
  const r = f.stores.find((t) => t.alias === f.filters.store), e = (r == null ? void 0 : r.currencies) || [];
  if (!e.includes(f.filters.currency || "")) {
    const t = (a = r == null ? void 0 : r.currency) == null ? void 0 : a.isoCurrencySymbol;
    f.filters.currency = e.find((o) => o === t) || e[0] || "";
  }
}
function Ce(r = "data-field") {
  const e = f.stores.find((t) => t.alias === f.filters.store), a = f.filters.currency || "";
  return `<div class="ekom-store-switch"><span>Store</span><details class="ekom-dropdown ekom-store-currency"><summary aria-label="Store and currency"><strong>${u((e == null ? void 0 : e.title) || "Select store")}</strong>${a ? `<span class="ekom-currency-code">${u(a)}</span>` : ""}<span class="ekom-store-chevron">▾</span></summary><div class="ekom-currency-popover"><div class="ekom-currency-heading"><strong>Store ·<br>Currency</strong><span>Amounts shown in the selected currency</span></div>${f.stores.map((t) => `<section class="ekom-currency-store ${t.alias === (e == null ? void 0 : e.alias) ? "is-selected" : ""}" aria-label="${u(t.title)}"><h3>${u(t.title)}</h3><div class="ekom-currency-choices">${(t.currencies || []).map((o) => `<button type="button" data-header-store="${u(t.alias)}" data-header-currency="${u(o)}" aria-label="${u(t.title)} — ${u(o)}" aria-pressed="${t.alias === (e == null ? void 0 : e.alias) && o === a}">${u(o)}${t.alias === (e == null ? void 0 : e.alias) && o === a ? '<span aria-hidden="true">✓</span>' : ""}</button>`).join("") || '<span class="ekom-currency-empty">No currencies configured or recorded</span>'}</div></section>`).join("")}</div></details><select hidden aria-label="Store" ${r}="store">${f.stores.map((t) => `<option value="${u(t.alias)}" ${t.alias === (e == null ? void 0 : e.alias) ? "selected" : ""}>${u(t.title)}</option>`).join("")}</select></div>`;
}
function Je(r) {
  if (typeof location > "u" || typeof history > "u") return;
  const e = new URL(location.href);
  for (const [a, t] of Object.entries(r))
    t ? e.searchParams.set(a, t) : e.searchParams.delete(a);
  history.replaceState(history.state, "", e.pathname + e.search + e.hash);
}
function Ye(r, e = "data-field", a = !0) {
  if (typeof document < "u" && !document.querySelector("link[data-ekom-fonts]")) {
    const n = document.createElement("link");
    n.rel = "stylesheet", n.href = "/App_Plugins/Ekom/dist/manager-fonts.css", n.dataset.ekomFonts = "", document.head.append(n);
  }
  const t = ["orders", "analytics", "customers", ...f.printingEnabled || r === "printing" ? ["printing"] : []], o = typeof location > "u" ? "/umbraco/section/ekommanager" : location.pathname.replace(/\/view\/(orders|analytics|customers|printing)\/?$/, "").replace(/\/$/, "");
  return `<header class="ekom-design-header"><img src="/App_Plugins/Ekom/dist/ekom-logo.svg" alt="Ekom" class="ekom-design-logo"><nav aria-label="Ekom sections" class="ekom-section-switch">${t.map((n) => `<a href="${u(o + "/view/" + n)}" ${n === r ? 'aria-current="page"' : ""}>${n[0].toUpperCase() + n.slice(1)}</a>`).join("")}</nav>${a ? Ce(e) : '<span class="ekom-store-switch">Accessible stores</span>'}</header>`;
}
function He(r) {
  var a;
  const e = [...Oe];
  return e.some((t) => t.key === r) || e.push({ key: r, label: r.startsWith("states:") ? "Custom status filters" : ((a = f.statusList.find((t) => ve(t) === r)) == null ? void 0 : a.label) || r }), e.map((t) => `<option value="${u(t.key)}" ${t.key === r ? "selected" : ""}>${u(t.label)}</option>`).join("");
}
function ue(r = /* @__PURE__ */ new Date()) {
  const e = (o) => `${o.getFullYear()}-${String(o.getMonth() + 1).padStart(2, "0")}-${String(o.getDate()).padStart(2, "0")}`, a = (o) => new Date(r.getFullYear(), r.getMonth(), r.getDate() + o), t = e(r);
  return [
    { label: "Today", from: t, to: t },
    { label: "Yesterday", from: e(a(-1)), to: e(a(-1)) },
    { label: "Last 7 days", from: e(a(-6)), to: t },
    { label: "Last 30 days", from: e(a(-29)), to: t },
    { label: "This month", from: e(new Date(r.getFullYear(), r.getMonth(), 1)), to: t },
    { label: "Last month", from: e(new Date(r.getFullYear(), r.getMonth() - 1, 1)), to: e(new Date(r.getFullYear(), r.getMonth(), 0)) },
    { label: "This quarter", from: e(new Date(r.getFullYear(), Math.floor(r.getMonth() / 3) * 3, 1)), to: t },
    { label: "This year", from: e(new Date(r.getFullYear(), 0, 1)), to: t }
  ];
}
const C = (r, e = 12) => `<span class="ekom-skeleton" style="width:${r};height:${e}px"></span>`;
function Le() {
  var a;
  const { dateFrom: r, dateTo: e } = f.filters;
  return ((a = ue().find((t) => t.from === r && t.to === e)) == null ? void 0 : a.label) || (P() ? `${Q(r)} – ${Q(e)}` : `${r} – ${e}`);
}
function Me(r) {
  return De(`Loading ${r} for ${Le()}…`);
}
function De(r) {
  return `<p class="ekom-loading-status" role="status"><span class="ekom-loading-spinner" aria-hidden="true"></span>${u(r)}</p>`;
}
function We(r) {
  return `<div class="ekom-drawer-close"><span class="ekom-esc-hint" aria-hidden="true"><strong>Esc</strong> close</span>${r}</div>`;
}
function Ze() {
  return `<div class="ekom-order-columns ekom-drawer-loading"><main class="ekom-order-main">${V(4)}${V(3)}${V(2)}</main><aside class="ekom-customer-sidebar">${V(5)}</aside></div>`;
}
function V(r = 3) {
  return `<div aria-hidden="true" class="ekom-loading-panel">${C("28%")}${C("100%").repeat(r)}</div>`;
}
function Qe(r, e, a) {
  const t = `<div class="ekom-loading-card">${C("40%")}${C("55%", 22)}</div>`;
  return `<div class="ekom-loading">${Me(r)}<div aria-hidden="true" class="ekom-loading-cards" style="grid-template-columns:repeat(${e},minmax(0,1fr))">${t.repeat(e)}</div>
    <div aria-hidden="true" class="ekom-loading-panel" style="height:${a}px">${C("30%")}<span class="ekom-skeleton ekom-skeleton-block"></span></div>
    <div aria-hidden="true" class="ekom-loading-panel">${C("28%")}${C("100%").repeat(3)}</div></div>`;
}
function Xe(r, e = 4) {
  return `<div class="ekom-loading-rows">${`<div aria-hidden="true"><span class="ekom-skeleton" style="flex:1"></span>${C("60px")}${C("110px")}</div>`.repeat(e)}<p role="status">${u(r)}</p></div>`;
}
function Ke(r = 6) {
  return `<div class="ekom-list-row ekom-list-skeleton-row" aria-hidden="true">${["60%", "70%", "55%", "80%", "70%", "60%"].map((a) => C(a)).join("")}<span></span></div>`.repeat(r) + '<span class="ekom-sr-only" role="status">Loading orders…</span>';
}
function et() {
  return `<ekom-date-range from="${u(f.filters.dateFrom)}" to="${u(f.filters.dateTo)}"></ekom-date-range>`;
}
const Z = /* @__PURE__ */ new WeakMap();
function Ae(r) {
  const e = r.ownerDocument, a = Z.has(e);
  if (Z.set(e, r), a) return;
  const t = () => {
    const o = Z.get(e);
    return o != null && o.open && o.isConnected ? o : void 0;
  };
  e.addEventListener("pointerdown", (o) => {
    const n = t();
    n && !o.composedPath().includes(n) && (n.open = !1);
  }, !0), e.addEventListener("keydown", (o) => {
    var s;
    const n = t();
    !n || o.key !== "Escape" || (n.open = !1, (s = n.querySelector("summary")) == null || s.focus(), o.preventDefault(), o.stopPropagation());
  }, !0);
}
function tt(r, e) {
  var t;
  Pe();
  const a = r.querySelector(".ekom-store-currency");
  a && Ae(a), r.querySelectorAll("[data-header-store]").forEach((o) => o.addEventListener("click", () => {
    var s;
    const n = r.querySelector('[data-field="store"], [data-filter="store"]');
    n && (f.filters.currency = o.dataset.headerCurrency || "", (s = o.closest("details")) == null || s.removeAttribute("open"), n.value = o.dataset.headerStore || "", n.dispatchEvent(new Event("change", { bubbles: !0 })));
  })), r.querySelectorAll("[data-shared-status]").forEach((o) => o.addEventListener("change", () => {
    const n = [0, 1, 2].map((s) => Array.from(r.querySelectorAll(`[data-shared-status="${s}"]:checked`), (g) => g.value).join(","));
    f.filters.orderStatus = n.some(Boolean) ? "states:" + n.join(";") : "AllOrders", e();
  })), (t = r.querySelector("ekom-date-range")) == null || t.addEventListener("date-range-apply", (o) => {
    const n = o.detail;
    f.filters.dateFrom = n.from, f.filters.dateTo = n.to, e();
  });
}
function Pe() {
  if (typeof customElements > "u" || !customElements.get || customElements.get("ekom-date-range")) return;
  class r extends HTMLElement {
    constructor() {
      super(...arguments);
      D(this, "root", this.attachShadow({ mode: "open" }));
      D(this, "open", !1);
      D(this, "from", "");
      D(this, "to", "");
      D(this, "month", 0);
      D(this, "reposition", () => {
        const t = this.root.querySelector(".popover"), o = this.root.querySelector(".anchor"), n = this.ownerDocument.defaultView;
        if (!t || !o || !n) return;
        if (n.innerWidth <= 650) {
          t.style.left = "";
          return;
        }
        const s = o.getBoundingClientRect().left;
        t.style.left = `${Math.min(0, Math.max(24 - s, n.innerWidth - 24 - s - t.getBoundingClientRect().width))}px`;
      });
      D(this, "outside", (t) => {
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
      var o;
      this.open = !1, this.render(), t && ((o = this.root.querySelector("[data-toggle]")) == null || o.focus());
    }
    date(t) {
      return t ? P() ? Q(t) : `${t.slice(8, 10)}.${t.slice(5, 7)}.${t.slice(0, 4)}` : "Pick a date";
    }
    calendar(t) {
      const o = new Date(Math.floor(t / 12), t % 12, 1), n = (o.getDay() + 6) % 7, s = new Date(o.getFullYear(), o.getMonth() + 1, 0).getDate();
      return `<section class="month"><h3>${u(P() ? xe(o, "yearMonth", !1) : o.toLocaleDateString("en", { month: "long", year: "numeric" }))}</h3><div class="days">${(P() ? [1, 2, 3, 4, 5, 6, 0].map((g) => u(P().shortestDayNames[g] || "")) : ["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"]).map((g) => `<span class="weekday">${g}</span>`).join("")}${"<span></span>".repeat(n)}${Array.from({ length: s }, (g, i) => {
        const y = `${o.getFullYear()}-${String(o.getMonth() + 1).padStart(2, "0")}-${String(i + 1).padStart(2, "0")}`, $ = y === this.from || y === this.to, w = this.from && this.to && y > this.from && y < this.to;
        return `<button type="button" data-day="${y}" aria-label="${y}" aria-pressed="${$ || !!w}" class="day ${$ ? "edge" : w ? "inside" : ""} ${y === this.from ? "start" : ""} ${y === this.to ? "end" : ""} ${!this.to || this.to === this.from ? "single" : ""}">${i + 1}</button>`;
      }).join("")}</div></section>`;
    }
    render() {
      var g, i, y, $, w;
      const t = this.getAttribute("from") || "", o = this.getAttribute("to") || "", n = ue(), s = ((g = n.find((c) => c.from === t && c.to === o)) == null ? void 0 : g.label) || "Custom";
      Te(this.root, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/date-range.css"><div class="range"><span class="label">Date range</span><div class="anchor"><button type="button" class="trigger" data-toggle aria-haspopup="dialog" aria-expanded="${this.open}"><strong>${s}</strong><span class="dates">${this.date(t)} – ${this.date(o)}</span><span class="chevron">▾</span></button>${this.open ? `<div class="popover" role="dialog" aria-label="Choose date range"><nav aria-label="Date presets">${n.map((c, b) => `<button type="button" data-preset="${b}" aria-pressed="${c.from === this.from && c.to === this.to}">${c.label}<span>${c.from === this.from && c.to === this.to ? "✓" : ""}</span></button>`).join("")}</nav><div class="custom"><div class="draft"><div class="${this.from ? "" : "active"}"><span>From</span><strong>${this.date(this.from)}</strong></div><span>→</span><div class="${this.from && !this.to ? "active" : ""}"><span>To</span><strong>${this.date(this.to)}</strong></div></div><div class="calendars"><button type="button" class="previous" data-month="-1" aria-label="Previous month">‹</button><button type="button" class="next" data-month="1" aria-label="Next month">›</button>${this.calendar(this.month)}${this.calendar(this.month + 1)}</div><footer><span>${this.from && this.to ? `${this.date(this.from)} – ${this.date(this.to)}` : "Select an end date"}</span><button type="button" data-cancel>Cancel</button><button type="button" class="apply" data-apply ${!this.from || !this.to ? "disabled" : ""}>Apply</button></footer></div></div>` : ""}</div></div>`), (i = this.root.querySelector("link")) == null || i.addEventListener("load", this.reposition), this.reposition(), (y = this.root.querySelector("[data-toggle]")) == null || y.addEventListener("click", () => {
        var c;
        if (this.open) {
          this.close();
          return;
        }
        this.reset(), this.open = !0, this.render(), (c = this.root.querySelector("[data-preset]")) == null || c.focus();
      }), ($ = this.root.querySelector("[data-cancel]")) == null || $.addEventListener("click", () => this.close()), (w = this.root.querySelector("[data-apply]")) == null || w.addEventListener("click", () => {
        !this.from || !this.to || this.from > this.to || (this.setAttribute("from", this.from), this.setAttribute("to", this.to), this.close(), this.dispatchEvent(new CustomEvent("date-range-apply", { bubbles: !0, composed: !0, detail: { from: this.from, to: this.to } })));
      }), this.root.querySelectorAll("[data-preset]").forEach((c) => c.addEventListener("click", () => {
        var I;
        const b = n[Number(c.dataset.preset)];
        this.from = b.from, this.to = b.to;
        const O = /* @__PURE__ */ new Date(`${this.from}T12:00:00`);
        this.month = O.getFullYear() * 12 + O.getMonth(), this.render(), (I = this.root.querySelector(`[data-preset="${c.dataset.preset}"]`)) == null || I.focus();
      })), this.root.querySelectorAll("[data-month]").forEach((c) => c.addEventListener("click", () => {
        var b;
        this.month += Number(c.dataset.month), this.render(), (b = this.root.querySelector(`[data-month="${c.dataset.month}"]`)) == null || b.focus();
      })), this.root.querySelectorAll("[data-day]").forEach((c) => c.addEventListener("click", () => {
        var O;
        const b = c.dataset.day;
        !this.from || this.to || b < this.from ? (this.from = b, this.to = "") : this.to = b, this.render(), (O = this.root.querySelector(`[data-day="${b}"]`)) == null || O.focus();
      })), this.onkeydown = (c) => {
        c.key === "Escape" && this.open && (c.preventDefault(), c.stopPropagation(), this.close());
      };
    }
  }
  customElements.define("ekom-date-range", r);
}
function rt() {
  const r = f.filters.orderStatus.startsWith("states:") ? f.filters.orderStatus.slice(7).split(";") : ["", "", ""];
  return [
    { label: "Order", values: ["Draft", "Open", "Closed", "Cancelled"] },
    { label: "Payment", values: ["Pending", "Authorized", "Paid", "PartiallyPaid", "Failed", "Voided"] },
    { label: "Fulfillment", values: ["Unfulfilled", "Picking", "Picked", "Packed", "PartiallyFulfilled", "Fulfilled"] }
  ].map((a, t) => {
    const o = (r[t] || "").split(",").filter(Boolean), n = o.length === 1 ? o[0].replace(/([a-z])([A-Z])/g, "$1 $2") : o.length ? `${o.length} selected` : "Any";
    return `<div class="ekom-status-filter"><span>${a.label}</span><details class="ekom-dropdown"><summary>${u(n)}<span>▾</span></summary><div class="ekom-dropdown-options">${a.values.map((s) => `<label><input type="checkbox" data-shared-status="${t}" value="${s}" ${o.includes(s) ? "checked" : ""}>${s.replace(/([a-z])([A-Z])/g, "$1 $2")}</label>`).join("")}</div></details></div>`;
  }).join("");
}
let le;
function Ee(r) {
  const e = r.querySelector(".ekom-design-logo");
  e && (le ?? (le = new A().config().then((a) => a.ekomVersion).catch(() => {
  })), le.then((a) => {
    a && (e.title = `Ekom ${a}`, e.alt = `Ekom ${a}`);
  }));
}
function Te(r, e) {
  const a = r.ownerDocument.createElement("template");
  a.innerHTML = e;
  const t = Array.from(r.children).filter((n) => n.matches('style, link[rel="stylesheet"]')), o = Array.from(a.content.children).filter((n) => n.matches('style, link[rel="stylesheet"]'));
  for (const n of o) {
    const s = t.find((g) => g.outerHTML === n.outerHTML);
    s ? t.splice(t.indexOf(s), 1) : r.appendChild(n.cloneNode(!0)), n.remove();
  }
  for (const n of t) n.remove();
  for (const n of Array.from(r.childNodes))
    (n.nodeType !== 1 || !n.matches('style, link[rel="stylesheet"]')) && n.remove();
  r.appendChild(a.content), Ee(r);
}
export {
  Ke as A,
  ze as B,
  ve as C,
  je as D,
  A as E,
  Ue as F,
  qe as a,
  Ye as b,
  u as c,
  He as d,
  Ve as e,
  $e as f,
  rt as g,
  et as h,
  Qe as i,
  Xe as j,
  P as k,
  Q as l,
  f as m,
  Ge as n,
  Ne as o,
  Ie as p,
  tt as q,
  Te as r,
  Fe as s,
  xe as t,
  Je as u,
  De as v,
  We as w,
  Ze as x,
  Be as y,
  Oe as z
};
