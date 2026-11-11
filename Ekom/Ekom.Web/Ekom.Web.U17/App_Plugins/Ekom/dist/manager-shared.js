var le = Object.defineProperty;
var de = (a, t, r) => t in a ? le(a, t, { enumerable: !0, configurable: !0, writable: !0, value: r }) : a[t] = r;
var A = (a, t, r) => de(a, typeof t != "symbol" ? t + "" : t, r);
const ae = /* @__PURE__ */ new Date(), ce = new Date(ae.getFullYear(), 0, 1), h = {
  filters: {
    dateFrom: K(ce),
    dateTo: K(ae),
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
class M {
  async changeOrderStates(t, r, e, o, n) {
    return this.postBody(`/ekom/manager/orderStates/${encodeURIComponent(t)}`, { expectedRevision: r, action: e, notify: o, fulfilledQuantities: n });
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
      const o = await this.getJson(`/ekom/manager/orders/${encodeURIComponent(t)}/reviews`, { skip: e, take: 200 });
      if (r.push(...o), o.length < 200) return r;
    }
  }
  async reviewOrder(t, r, e, o) {
    return this.postBody(`/ekom/manager/orders/${encodeURIComponent(t)}/reviews/${encodeURIComponent(r)}/review`, { comment: e, resolved: o });
  }
  async statusList() {
    return this.getJson("/ekom/manager/StatusList");
  }
  async stores() {
    const [t, r] = await Promise.all([this.getJson("/ekom/manager/stores"), this.getJson("/ekom/manager/store-currencies")]);
    return t.map((e) => {
      var o;
      return { ...e, currencies: ((o = r.find((n) => n.alias === e.alias)) == null ? void 0 : o.currencies) || [] };
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
  async releaseGiftcardRecovery(t, r, e, o) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(t)}/release`, { attemptId: r, claimId: e, token: o });
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
      throw await M.createError(e, "Order action failed.");
    return e;
  }
  async changeOrderStatus(t, r, e) {
    return this.postJson("/ekom/manager/changeOrderStatus", { orderId: t, orderStatus: r, notify: e });
  }
  async updateCustomerInformation(t, r, e) {
    return this.postBody("/ekom/manager/UpdateCustomerInformation", { orderId: t, customer: r, shipping: e });
  }
  async addOrderLine(t, r, e, o) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(t)}/OrderLines`, { productId: r, variantId: e, quantity: o });
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
      throw await M.createError(e, "Error removing order line.");
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
  async mostSoldProducts(t, r, e = 20, o) {
    return this.getJson("/ekom/manager/MostSoldProducts", {
      start: t.dateFrom,
      end: t.dateTo,
      orderStatus: t.orderStatus,
      store: t.store,
      currency: t.currency,
      page: r,
      pageSize: e
    }, o);
  }
  async getJson(t, r, e) {
    const o = await fetch(t + V(r), {
      credentials: "same-origin",
      signal: e,
      headers: { Accept: "application/json" }
    });
    if (!o.ok)
      throw await M.createError(o, "Request failed.");
    return o.json();
  }
  async getBlob(t, r) {
    const e = await fetch(t + V(r), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!e.ok)
      throw await M.createError(e, "Request failed.");
    return e.blob();
  }
  async postJson(t, r) {
    const e = await fetch(t + V(r), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!e.ok)
      throw await M.createError(e, "Request failed.");
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
      throw await M.createError(e, "Request failed.");
    return e.json();
  }
  static async createError(t, r) {
    var n, s;
    if (t.status === 401 || t.redirected && /\/Account\/Login/i.test(t.url))
      return new Error("Your session has expired. Sign in to Umbraco and reload the manager.");
    if ((s = (n = t.headers) == null ? void 0 : n.get("content-type")) != null && s.includes("text/html"))
      return new Error(`${r} Status ${t.status}. Reload the manager and try again.`);
    const e = await t.text();
    let o = e || r;
    try {
      o = JSON.parse(e).message || o;
    } catch {
    }
    return new Error(o || `${r} Status ${t.status}.`);
  }
}
function V(a) {
  if (!a)
    return "";
  const t = new URLSearchParams();
  for (const [e, o] of Object.entries(a))
    t.set(e, o == null ? "" : String(o));
  const r = t.toString();
  return r ? `?${r}` : "";
}
function K(a) {
  const t = String(a.getMonth() + 1).padStart(2, "0"), r = String(a.getDate()).padStart(2, "0");
  return `${a.getFullYear()}-${t}-${r}`;
}
function we(a) {
  if (!a)
    return "";
  const t = new Date(a);
  return Number.isNaN(t.getTime()) ? a : new Intl.DateTimeFormat(void 0, {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  }).format(t);
}
function f(a) {
  return String(a ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
function _e(a) {
  const t = document.createElement("textarea");
  return t.innerHTML = String(a ?? ""), t.value;
}
function me(a) {
  return a.enumValue || a.value || "";
}
function Se(a, t) {
  const e = [], o = Math.max(t || 1, 1);
  let n;
  a <= Math.floor(5 / 2) ? n = 1 : a + Math.floor(5 / 2) >= o ? n = Math.max(o - 5 + 1, 1) : n = a - Math.floor(5 / 2);
  for (let s = 0; s < 5; s += 1) {
    const u = n + s;
    u <= o && e.push(u);
  }
  return e.length && Number(e[e.length - 1]) < o && e.push(`...${o}`), e.length && Number(e[0]) > 1 && e.unshift("...1"), e;
}
const oe = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`;
async function Oe(a, t) {
  const r = document.createElement("iframe");
  r.title = t, r.setAttribute("aria-hidden", "true"), r.style.cssText = "position:fixed;left:-10000px;top:0;width:800px;height:600px;border:0;";
  try {
    const e = new Promise((n, s) => {
      r.onload = () => n(), r.onerror = () => s(new Error("Unable to load the print document."));
    });
    r.srcdoc = pe(a, t), document.body.append(r), await e;
    const o = r.contentWindow;
    if (!o) throw new Error("Print window unavailable.");
    await o.document.fonts.ready, o.addEventListener("afterprint", () => r.remove(), { once: !0 }), o.focus(), o.print();
  } catch (e) {
    throw r.remove(), e;
  }
}
function pe(a, t) {
  const r = document.createElement("template");
  return r.innerHTML = a, r.content.querySelectorAll("button, input, select, .ekmOrderGiftcards__actions, .ekmOrderLineThumbnail").forEach((e) => e.remove()), r.content.querySelectorAll("details").forEach((e) => e.setAttribute("open", "")), r.content.querySelectorAll("summary").forEach((e) => e.remove()), r.content.querySelectorAll("a").forEach((e) => e.replaceWith(...Array.from(e.childNodes))), `<!doctype html><html><head><meta charset="utf-8"><title>${f(t)}</title><style>
    ${oe}
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
  </style></head><body>${r.innerHTML}</body></html>`;
}
function $e(a, t) {
  const r = URL.createObjectURL(a), e = document.createElement("a");
  e.href = r, e.download = t, e.addEventListener("click", (o) => o.stopPropagation()), document.body.append(e), e.click(), e.remove(), URL.revokeObjectURL(r);
}
const ne = `
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

  ${oe}
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
`, ee = ":is(ekom-orders-section-view, ekom-analytics-section-view, ekom-customers-section-view, ekom-printing-section-view)", te = {
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
}, ue = ne.replaceAll(":host", "&").replace(/#[0-9a-f]{3,8}\b/gi, (a) => te[a.toLowerCase()] ? `var(${te[a.toLowerCase()]})` : a), Ce = `${ee} {
  ${ne.replaceAll(":host", "&")}
}
${ee}:not([classic]) {
  ${ue}
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
function Le(a) {
  const t = ["Paid", "Fulfilled", "Closed"].includes(a || "") ? "positive" : ["Failed", "Cancelled"].includes(a || "") ? "negative" : ["Pending", "PartiallyPaid", "PartiallyFulfilled", "PartiallyRefunded"].includes(a || "") ? "warning" : "neutral", r = a ? a.replace(/([a-z])([A-Z])/g, "$1 $2") : "Needs review";
  return `<span class="ekom-badge ekom-badge--${t}">${f(r)}</span>`;
}
function ge(a, t) {
  const r = t == null ? void 0 : t.isoCurrencySymbol, e = (t == null ? void 0 : t.currencyDecimalDigits) ?? (r && /^[A-Z]{3}$/.test(r) ? new Intl.NumberFormat(void 0, { style: "currency", currency: r }).resolvedOptions().maximumFractionDigits : 2);
  return new Intl.NumberFormat(void 0, {
    minimumFractionDigits: e,
    maximumFractionDigits: e,
    ...r && /^[A-Z]{3}$/.test(r) ? { style: "currency", currency: r, currencyDisplay: "code" } : {}
  }).format(a);
}
function Ae(a, t, r, e = {}) {
  var W;
  const o = a.getBoundingClientRect(), n = window.devicePixelRatio || 1, s = o.width || 300, u = o.height || 320;
  a.width = s * n, a.height = u * n;
  const i = a.getContext("2d");
  if (!i) return;
  i.scale(n, n), i.font = e.templateStyle ? '10px "Ekom Mono", monospace' : "12px sans-serif";
  const x = t.points || [], O = x.map((m) => m.y == null ? null : Number(m.y)), w = ((W = e.comparison) == null ? void 0 : W.points) || [], c = [...O, ...w.map((m) => m.y)].filter((m) => m != null), b = Math.min(0, ...c), $ = Math.max(1, ...c), F = 10 ** (Math.floor(Math.log10($ / 4)) - 1), q = e.integer ? Math.ceil($ / 4) * 4 : e.templateStyle ? Math.ceil($ / 4 / F) * F * 4 : $, G = (m) => e.formatValue ? e.formatValue(m) : e.currency ? ge(m, e.currency) : m.toLocaleString(void 0, { maximumFractionDigits: e.integer ? 0 : 2 }), J = e.formatAxisValue ?? G, Y = Array.from({ length: 5 }, (m, d) => q - (q - b) * d / 4), S = e.templateStyle ? 1 : Math.max(48, ...Y.map((m) => i.measureText(J(m)).width + 12)), E = s - (e.templateStyle ? 1 : 16), R = e.templateStyle ? 8 : 20, _ = u - (e.templateStyle ? 26 : 52), g = x.map((m, d) => {
    var v, C, P;
    return {
      x: x.length > 1 ? S + (E - S) * d / (x.length - 1) : (S + E) / 2,
      y: O[d] == null ? null : _ - (O[d] - b) / (q - b) * (_ - R),
      date: m.x || ((v = t.labels) == null ? void 0 : v[d]) || "",
      axisLabel: ((C = e.formatLabel) == null ? void 0 : C.call(e, d)) ?? m.x ?? ((P = t.labels) == null ? void 0 : P[d]) ?? "",
      value: O[d]
    };
  }), N = (m = -1) => {
    var X;
    i.clearRect(0, 0, s, u), i.font = e.templateStyle ? '10px "Ekom Mono", monospace' : "12px sans-serif";
    const d = typeof getComputedStyle == "function" ? getComputedStyle(a) : void 0, v = (d == null ? void 0 : d.getPropertyValue("--ekom-muted").trim()) || (d == null ? void 0 : d.getPropertyValue("--uui-color-text-alt").trim()) || "#555", C = (d == null ? void 0 : d.getPropertyValue("--ekom-border").trim()) || "#e5e5e5", P = (d == null ? void 0 : d.getPropertyValue("--uui-color-text").trim()) || "", j = (X = /^#([0-9a-f]{6})$/i.exec(P)) == null ? void 0 : X[1], I = j ? [0, 2, 4].reduce((p, l) => p + parseInt(j.slice(l, l + 2), 16), 0) > 420 : !1, U = e.templateStyle && !I ? "#10694a" : r;
    i.lineWidth = 1, i.textAlign = e.templateStyle ? "left" : "right", Y.forEach((p, l) => {
      const k = R + (_ - R) * l / 4;
      i.fillStyle = v, i.fillText(J(p), e.templateStyle ? S : S - 8, e.templateStyle ? Math.min(_, k + 3) : k + 4), !(e.templateStyle && (l === 0 || l === 4)) && (i.globalAlpha = e.templateStyle ? 0.5 : 1, i.strokeStyle = C, i.beginPath(), i.moveTo(S, k), i.lineTo(E, k), i.stroke(), i.globalAlpha = 1);
    });
    const B = Math.max(90, ...g.map((p) => i.measureText(p.axisLabel).width + 32)), Z = Math.max(2, Math.floor((E - S) / B)), ie = new Set(Array.from({ length: Math.min(Z, g.length) }, (p, l) => Math.round(l * (g.length - 1) / Math.max(1, Math.min(Z, g.length) - 1))));
    i.fillStyle = v, g.forEach((p, l) => {
      ie.has(l) && (i.textAlign = g.length === 1 ? "center" : l === 0 ? "left" : l === g.length - 1 ? "right" : "center", i.fillText(p.axisLabel, p.x, _ + 24));
    });
    const Q = (p, l) => {
      var y, z;
      i.strokeStyle = l ? "#9b9fad" : U, i.lineWidth = e.templateStyle ? l ? 1.5 : 2.5 : 2, i.lineJoin = "round", i.lineCap = "round", (y = i.setLineDash) == null || y.call(i, l ? [4, 5] : []), i.beginPath();
      let k = !1;
      p.forEach((L) => {
        if (L.y == null) {
          k = !1;
          return;
        }
        k ? i.lineTo(L.x, L.y) : i.moveTo(L.x, L.y), k = !0;
      }), i.stroke(), (z = i.setLineDash) == null || z.call(i, []);
    };
    if (e.templateStyle && g.length > 1) {
      const p = i.createLinearGradient(0, R, 0, _);
      p.addColorStop(0, "rgba(16,105,74,.22)"), p.addColorStop(1, "rgba(16,105,74,0)"), i.fillStyle = p;
      let l = [];
      const k = () => {
        l.length > 1 && (i.beginPath(), i.moveTo(l[0].x, _), l.forEach((y) => i.lineTo(y.x, y.y)), i.lineTo(l[l.length - 1].x, _), i.closePath(), i.fill()), l = [];
      };
      g.forEach((y) => {
        y.y == null ? k() : l.push({ x: y.x, y: y.y });
      }), k();
    }
    w.length && Q(g.map((p, l) => {
      var k;
      return {
        x: p.x,
        y: ((k = w[l]) == null ? void 0 : k.y) == null ? null : _ - (Number(w[l].y) - b) / (q - b) * (_ - R)
      };
    }), !0), Q(g, !1), i.fillStyle = U, g.forEach((p, l) => {
      p.y == null || e.templateStyle && g.length > 1 && l !== m || (i.beginPath(), i.arc(p.x, p.y, l === m ? 5 : 3, 0, Math.PI * 2), i.fill());
    }), g.length || (i.textAlign = "center", i.fillText("No data for this period", (S + E) / 2, u / 2));
    const D = g[m];
    if (D) {
      const p = [D.date, D.value == null ? "—" : G(D.value)];
      if (w[m]) {
        const T = w[m];
        p.push(`Previous: ${T.x || ""}`, T.y == null ? "—" : G(T.y));
      }
      const l = Math.max(...p.map((T) => i.measureText(T).width)) + 20, k = Math.max(0, Math.min(s - l, D.x - l / 2)), y = p.length * 18 + 12, z = D.y ?? _, L = Math.max(0, Math.min(u - y, z > y + 20 ? z - y - 12 : z + 12));
      i.fillStyle = "#222", i.fillRect(k, L, l, y), i.fillStyle = "#fff", i.textAlign = "left", p.forEach((T, se) => i.fillText(T, k + 10, L + 19 + se * 18));
    }
  }, H = (m) => {
    const d = a.getBoundingClientRect(), v = (m.clientX - d.left) * s / d.width, C = (m.clientY - d.top) * u / d.height;
    if (e.columnHover)
      return !g.length || v < S || v > E || C < R || C > _ ? -1 : g.length === 1 ? 0 : Math.round((v - S) / (E - S) * (g.length - 1));
    let P = -1, j = 16;
    return g.forEach((I, U) => {
      if (I.y == null) return;
      const B = Math.hypot(I.x - v, I.y - C);
      B < j && (P = U, j = B);
    }), P;
  };
  a.onmousemove = (m) => {
    const d = H(m);
    a.style.cursor = d >= 0 ? "pointer" : "default", N(d);
  }, a.onclick = e.onSelect ? (m) => {
    var v;
    const d = H(m);
    d >= 0 && ((v = e.onSelect) == null || v.call(e, d));
  } : null, a.onmouseleave = () => {
    a.style.cursor = "default", N();
  }, N();
}
const fe = [
  { key: "AllOrders", label: "All" },
  { key: "NeedsReview", label: "Needs review" },
  { key: "states:Open;;Unfulfilled,Picking,Picked,Packed,PartiallyFulfilled", label: "Unfulfilled" },
  { key: "states:Open;Pending,Authorized,PartiallyPaid,Failed;", label: "Unpaid" },
  { key: "states:Open;;", label: "Open" },
  { key: "states:Closed;;", label: "Closed" }
];
function Me() {
  var r;
  const a = h.stores.find((e) => e.alias === h.filters.store), t = (a == null ? void 0 : a.currencies) || [];
  if (!t.includes(h.filters.currency || "")) {
    const e = (r = a == null ? void 0 : a.currency) == null ? void 0 : r.isoCurrencySymbol;
    h.filters.currency = t.find((o) => o === e) || t[0] || "";
  }
}
function he(a = "data-field") {
  const t = h.stores.find((e) => e.alias === h.filters.store), r = h.filters.currency || "";
  return `<div class="ekom-store-switch"><span>Store</span><details class="ekom-dropdown ekom-store-currency"><summary aria-label="Store and currency"><strong>${f((t == null ? void 0 : t.title) || "Select store")}</strong>${r ? `<span class="ekom-currency-code">${f(r)}</span>` : ""}<span class="ekom-store-chevron">▾</span></summary><div class="ekom-currency-popover"><div class="ekom-currency-heading"><strong>Store ·<br>Currency</strong><span>Amounts shown in the selected currency</span></div>${h.stores.map((e) => `<section class="ekom-currency-store ${e.alias === (t == null ? void 0 : t.alias) ? "is-selected" : ""}" aria-label="${f(e.title)}"><h3>${f(e.title)}</h3><div class="ekom-currency-choices">${(e.currencies || []).map((o) => `<button type="button" data-header-store="${f(e.alias)}" data-header-currency="${f(o)}" aria-label="${f(e.title)} — ${f(o)}" aria-pressed="${e.alias === (t == null ? void 0 : t.alias) && o === r}">${f(o)}${e.alias === (t == null ? void 0 : t.alias) && o === r ? '<span aria-hidden="true">✓</span>' : ""}</button>`).join("") || '<span class="ekom-currency-empty">No currencies configured or recorded</span>'}</div></section>`).join("")}</div></details><select hidden aria-label="Store" ${a}="store">${h.stores.map((e) => `<option value="${f(e.alias)}" ${e.alias === (t == null ? void 0 : t.alias) ? "selected" : ""}>${f(e.title)}</option>`).join("")}</select></div>`;
}
function Ee(a, t = "data-field", r = !0) {
  if (typeof document < "u" && !document.querySelector("link[data-ekom-fonts]")) {
    const n = document.createElement("link");
    n.rel = "stylesheet", n.href = "/App_Plugins/Ekom/dist/manager-fonts.css", n.dataset.ekomFonts = "", document.head.append(n);
  }
  const e = ["orders", "analytics", "customers", ...h.printingEnabled || a === "printing" ? ["printing"] : []], o = typeof location > "u" ? "/umbraco/section/ekommanager" : location.pathname.replace(/\/view\/(orders|analytics|customers|printing)\/?$/, "").replace(/\/$/, "");
  return `<header class="ekom-design-header"><img src="/App_Plugins/Ekom/dist/ekom-logo.svg" alt="Ekom" class="ekom-design-logo"><nav aria-label="Ekom sections" class="ekom-section-switch">${e.map((n) => `<a href="${f(o + "/view/" + n)}" ${n === a ? 'aria-current="page"' : ""}>${n[0].toUpperCase() + n.slice(1)}</a>`).join("")}</nav>${r ? he(t) : '<span class="ekom-store-switch">Accessible stores</span>'}</header>`;
}
function Pe(a) {
  var r;
  const t = [...fe];
  return t.some((e) => e.key === a) || t.push({ key: a, label: a.startsWith("states:") ? "Custom status filters" : ((r = h.statusList.find((e) => me(e) === a)) == null ? void 0 : r.label) || a }), t.map((e) => `<option value="${f(e.key)}" ${e.key === a ? "selected" : ""}>${f(e.label)}</option>`).join("");
}
function be(a = /* @__PURE__ */ new Date()) {
  const t = (o) => `${o.getFullYear()}-${String(o.getMonth() + 1).padStart(2, "0")}-${String(o.getDate()).padStart(2, "0")}`, r = (o) => new Date(a.getFullYear(), a.getMonth(), a.getDate() + o), e = t(a);
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
function Te() {
  return `<ekom-date-range from="${f(h.filters.dateFrom)}" to="${f(h.filters.dateTo)}"></ekom-date-range>`;
}
function Re(a, t) {
  var r, e;
  ke(), (r = a.querySelector(".ekom-store-currency")) == null || r.addEventListener("keydown", (o) => {
    var s;
    if (o.key !== "Escape") return;
    const n = o.currentTarget;
    n.open = !1, (s = n.querySelector("summary")) == null || s.focus(), o.stopPropagation();
  }), a.querySelectorAll("[data-header-store]").forEach((o) => o.addEventListener("click", () => {
    var s;
    const n = a.querySelector('[data-field="store"], [data-filter="store"]');
    n && (h.filters.currency = o.dataset.headerCurrency || "", (s = o.closest("details")) == null || s.removeAttribute("open"), n.value = o.dataset.headerStore || "", n.dispatchEvent(new Event("change", { bubbles: !0 })));
  })), a.querySelectorAll("[data-shared-status]").forEach((o) => o.addEventListener("change", () => {
    const n = [0, 1, 2].map((s) => Array.from(a.querySelectorAll(`[data-shared-status="${s}"]:checked`), (u) => u.value).join(","));
    h.filters.orderStatus = n.some(Boolean) ? "states:" + n.join(";") : "AllOrders", t();
  })), (e = a.querySelector("ekom-date-range")) == null || e.addEventListener("date-range-apply", (o) => {
    const n = o.detail;
    h.filters.dateFrom = n.from, h.filters.dateTo = n.to, t();
  });
}
function ke() {
  if (typeof customElements > "u" || !customElements.get || customElements.get("ekom-date-range")) return;
  class a extends HTMLElement {
    constructor() {
      super(...arguments);
      A(this, "root", this.attachShadow({ mode: "open" }));
      A(this, "open", !1);
      A(this, "from", "");
      A(this, "to", "");
      A(this, "month", 0);
      A(this, "reposition", () => {
        const e = this.root.querySelector(".popover"), o = this.root.querySelector(".anchor"), n = this.ownerDocument.defaultView;
        if (!e || !o || !n) return;
        if (n.innerWidth <= 650) {
          e.style.left = "";
          return;
        }
        const s = o.getBoundingClientRect().left;
        e.style.left = `${Math.min(0, Math.max(24 - s, n.innerWidth - 24 - s - e.getBoundingClientRect().width))}px`;
      });
      A(this, "outside", (e) => {
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
      var o;
      this.open = !1, this.render(), e && ((o = this.root.querySelector("[data-toggle]")) == null || o.focus());
    }
    date(e) {
      return e ? `${e.slice(8, 10)}.${e.slice(5, 7)}.${e.slice(0, 4)}` : "Pick a date";
    }
    calendar(e) {
      const o = new Date(Math.floor(e / 12), e % 12, 1), n = (o.getDay() + 6) % 7, s = new Date(o.getFullYear(), o.getMonth() + 1, 0).getDate();
      return `<section class="month"><h3>${o.toLocaleDateString("en", { month: "long", year: "numeric" })}</h3><div class="days">${["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"].map((u) => `<span class="weekday">${u}</span>`).join("")}${"<span></span>".repeat(n)}${Array.from({ length: s }, (u, i) => {
        const x = `${o.getFullYear()}-${String(o.getMonth() + 1).padStart(2, "0")}-${String(i + 1).padStart(2, "0")}`, O = x === this.from || x === this.to, w = this.from && this.to && x > this.from && x < this.to;
        return `<button type="button" data-day="${x}" aria-label="${x}" aria-pressed="${O || !!w}" class="day ${O ? "edge" : w ? "inside" : ""} ${x === this.from ? "start" : ""} ${x === this.to ? "end" : ""} ${!this.to || this.to === this.from ? "single" : ""}">${i + 1}</button>`;
      }).join("")}</div></section>`;
    }
    render() {
      var u, i, x, O, w;
      const e = this.getAttribute("from") || "", o = this.getAttribute("to") || "", n = be(), s = ((u = n.find((c) => c.from === e && c.to === o)) == null ? void 0 : u.label) || "Custom";
      ye(this.root, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/date-range.css"><div class="range"><span class="label">Date range</span><div class="anchor"><button type="button" class="trigger" data-toggle aria-haspopup="dialog" aria-expanded="${this.open}"><strong>${s}</strong><span class="dates">${this.date(e)} – ${this.date(o)}</span><span class="chevron">▾</span></button>${this.open ? `<div class="popover" role="dialog" aria-label="Choose date range"><nav aria-label="Date presets">${n.map((c, b) => `<button type="button" data-preset="${b}" aria-pressed="${c.from === this.from && c.to === this.to}">${c.label}<span>${c.from === this.from && c.to === this.to ? "✓" : ""}</span></button>`).join("")}</nav><div class="custom"><div class="draft"><div class="${this.from ? "" : "active"}"><span>From</span><strong>${this.date(this.from)}</strong></div><span>→</span><div class="${this.from && !this.to ? "active" : ""}"><span>To</span><strong>${this.date(this.to)}</strong></div></div><div class="calendars"><button type="button" class="previous" data-month="-1" aria-label="Previous month">‹</button><button type="button" class="next" data-month="1" aria-label="Next month">›</button>${this.calendar(this.month)}${this.calendar(this.month + 1)}</div><footer><span>${this.from && this.to ? `${this.date(this.from)} – ${this.date(this.to)}` : "Select an end date"}</span><button type="button" data-cancel>Cancel</button><button type="button" class="apply" data-apply ${!this.from || !this.to ? "disabled" : ""}>Apply</button></footer></div></div>` : ""}</div></div>`), (i = this.root.querySelector("link")) == null || i.addEventListener("load", this.reposition), this.reposition(), (x = this.root.querySelector("[data-toggle]")) == null || x.addEventListener("click", () => {
        var c;
        if (this.open) {
          this.close();
          return;
        }
        this.reset(), this.open = !0, this.render(), (c = this.root.querySelector("[data-preset]")) == null || c.focus();
      }), (O = this.root.querySelector("[data-cancel]")) == null || O.addEventListener("click", () => this.close()), (w = this.root.querySelector("[data-apply]")) == null || w.addEventListener("click", () => {
        !this.from || !this.to || this.from > this.to || (this.setAttribute("from", this.from), this.setAttribute("to", this.to), this.close(), this.dispatchEvent(new CustomEvent("date-range-apply", { bubbles: !0, composed: !0, detail: { from: this.from, to: this.to } })));
      }), this.root.querySelectorAll("[data-preset]").forEach((c) => c.addEventListener("click", () => {
        var F;
        const b = n[Number(c.dataset.preset)];
        this.from = b.from, this.to = b.to;
        const $ = /* @__PURE__ */ new Date(`${this.from}T12:00:00`);
        this.month = $.getFullYear() * 12 + $.getMonth(), this.render(), (F = this.root.querySelector(`[data-preset="${c.dataset.preset}"]`)) == null || F.focus();
      })), this.root.querySelectorAll("[data-month]").forEach((c) => c.addEventListener("click", () => {
        var b;
        this.month += Number(c.dataset.month), this.render(), (b = this.root.querySelector(`[data-month="${c.dataset.month}"]`)) == null || b.focus();
      })), this.root.querySelectorAll("[data-day]").forEach((c) => c.addEventListener("click", () => {
        var $;
        const b = c.dataset.day;
        !this.from || this.to || b < this.from ? (this.from = b, this.to = "") : this.to = b, this.render(), ($ = this.root.querySelector(`[data-day="${b}"]`)) == null || $.focus();
      })), this.onkeydown = (c) => {
        c.key === "Escape" && this.open && (c.preventDefault(), c.stopPropagation(), this.close());
      };
    }
  }
  customElements.define("ekom-date-range", a);
}
function De() {
  const a = h.filters.orderStatus.startsWith("states:") ? h.filters.orderStatus.slice(7).split(";") : ["", "", ""];
  return [
    { label: "Order", values: ["Draft", "Open", "Closed", "Cancelled"] },
    { label: "Payment", values: ["Pending", "Authorized", "Paid", "PartiallyPaid", "Failed", "Voided"] },
    { label: "Fulfillment", values: ["Unfulfilled", "Picking", "Picked", "Packed", "PartiallyFulfilled", "Fulfilled"] }
  ].map((r, e) => {
    const o = (a[e] || "").split(",").filter(Boolean), n = o.length === 1 ? o[0].replace(/([a-z])([A-Z])/g, "$1 $2") : o.length ? `${o.length} selected` : "Any";
    return `<div class="ekom-status-filter"><span>${r.label}</span><details class="ekom-dropdown"><summary>${f(n)}<span>▾</span></summary><div class="ekom-dropdown-options">${r.values.map((s) => `<label><input type="checkbox" data-shared-status="${e}" value="${s}" ${o.includes(s) ? "checked" : ""}>${s.replace(/([a-z])([A-Z])/g, "$1 $2")}</label>`).join("")}</div></details></div>`;
  }).join("");
}
let re;
function xe(a) {
  const t = a.querySelector(".ekom-design-logo");
  t && (re ?? (re = new M().config().then((r) => r.ekomVersion).catch(() => {
  })), re.then((r) => {
    r && (t.title = `Ekom ${r}`, t.alt = `Ekom ${r}`);
  }));
}
function ye(a, t) {
  const r = a.ownerDocument.createElement("template");
  r.innerHTML = t;
  const e = Array.from(a.children).filter((n) => n.matches('style, link[rel="stylesheet"]')), o = Array.from(r.content.children).filter((n) => n.matches('style, link[rel="stylesheet"]'));
  for (const n of o) {
    const s = e.find((u) => u.outerHTML === n.outerHTML);
    s ? e.splice(e.indexOf(s), 1) : a.appendChild(n.cloneNode(!0)), n.remove();
  }
  for (const n of e) n.remove();
  for (const n of Array.from(a.childNodes))
    (n.nodeType !== 1 || !n.matches('style, link[rel="stylesheet"]')) && n.remove();
  a.appendChild(r.content), xe(a);
}
export {
  M as E,
  Ce as a,
  Ee as b,
  f as c,
  Pe as d,
  Me as e,
  ge as f,
  De as g,
  Te as h,
  Ae as i,
  $e as j,
  Re as k,
  Le as l,
  h as m,
  we as n,
  fe as o,
  Se as p,
  me as q,
  ye as r,
  _e as s,
  Oe as t
};
