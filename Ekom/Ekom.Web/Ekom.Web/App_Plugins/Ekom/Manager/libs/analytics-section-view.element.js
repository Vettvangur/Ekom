var be = Object.defineProperty;
var ye = (o, r, t) => r in o ? be(o, r, { enumerable: !0, configurable: !0, writable: !0, value: t }) : o[r] = t;
var h = (o, r, t) => ye(o, typeof r != "symbol" ? r + "" : r, t);
const ue = /* @__PURE__ */ new Date(), ve = new Date(ue.getFullYear(), 0, 1), p = {
  filters: {
    dateFrom: se(ve),
    dateTo: se(ue),
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
class T {
  async changeOrderStates(r, t, e, a, s) {
    return this.postBody(`/ekom/manager/orderStates/${encodeURIComponent(r)}`, { expectedRevision: t, action: e, notify: a, fulfilledQuantities: s });
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
      var a;
      return { ...e, currencies: ((a = t.find((s) => s.alias === e.alias)) == null ? void 0 : a.currencies) || [] };
    });
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
      throw await T.createError(e, "Order action failed.");
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
      throw await T.createError(e, "Error removing order line.");
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
    const a = await fetch(r + Z(t), {
      credentials: "same-origin",
      signal: e,
      headers: { Accept: "application/json" }
    });
    if (!a.ok)
      throw await T.createError(a, "Request failed.");
    return a.json();
  }
  async getBlob(r, t) {
    const e = await fetch(r + Z(t), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!e.ok)
      throw await T.createError(e, "Request failed.");
    return e.blob();
  }
  async postJson(r, t) {
    const e = await fetch(r + Z(t), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!e.ok)
      throw await T.createError(e, "Request failed.");
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
      throw await T.createError(e, "Request failed.");
    return e.json();
  }
  static async createError(r, t) {
    var s, n;
    if (r.status === 401 || r.redirected && /\/Account\/Login/i.test(r.url))
      return new Error("Your session has expired. Sign in to Umbraco and reload the manager.");
    if ((n = (s = r.headers) == null ? void 0 : s.get("content-type")) != null && n.includes("text/html"))
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
function Z(o) {
  if (!o)
    return "";
  const r = new URLSearchParams();
  for (const [e, a] of Object.entries(o))
    r.set(e, a == null ? "" : String(a));
  const t = r.toString();
  return t ? `?${t}` : "";
}
function se(o) {
  const r = String(o.getMonth() + 1).padStart(2, "0"), t = String(o.getDate()).padStart(2, "0");
  return `${o.getFullYear()}-${r}-${t}`;
}
function m(o) {
  return String(o ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
function ke(o) {
  return o.enumValue || o.value || "";
}
function xe(o, r) {
  const e = [], a = Math.max(r || 1, 1);
  let s;
  o <= Math.floor(5 / 2) ? s = 1 : o + Math.floor(5 / 2) >= a ? s = Math.max(a - 5 + 1, 1) : s = o - Math.floor(5 / 2);
  for (let n = 0; n < 5; n += 1) {
    const d = s + n;
    d <= a && e.push(d);
  }
  return e.length && Number(e[e.length - 1]) < a && e.push(`...${a}`), e.length && Number(e[0]) > 1 && e.unshift("...1"), e;
}
const we = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`;
function Se(o, r) {
  const t = URL.createObjectURL(o), e = document.createElement("a");
  e.href = t, e.download = r, e.addEventListener("click", (a) => a.stopPropagation()), document.body.append(e), e.click(), e.remove(), URL.revokeObjectURL(t);
}
const me = `
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

  ${we}
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
`, ie = ":is(ekom-orders-section-view, ekom-analytics-section-view, ekom-customers-section-view, ekom-printing-section-view)", ne = {
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
}, $e = me.replaceAll(":host", "&").replace(/#[0-9a-f]{3,8}\b/gi, (o) => ne[o.toLowerCase()] ? `var(${ne[o.toLowerCase()]})` : o), Ce = `${ie} {
  ${me.replaceAll(":host", "&")}
}
${ie}:not([classic]) {
  ${$e}
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
function G(o, r) {
  const t = r == null ? void 0 : r.isoCurrencySymbol, e = (r == null ? void 0 : r.currencyDecimalDigits) ?? (t && /^[A-Z]{3}$/.test(t) ? new Intl.NumberFormat(void 0, { style: "currency", currency: t }).resolvedOptions().maximumFractionDigits : 2);
  return new Intl.NumberFormat(void 0, {
    minimumFractionDigits: e,
    maximumFractionDigits: e,
    ...t && /^[A-Z]{3}$/.test(t) ? { style: "currency", currency: t, currencyDisplay: "code" } : {}
  }).format(o);
}
function _e(o, r, t, e = {}) {
  var te;
  const a = o.getBoundingClientRect(), s = window.devicePixelRatio || 1, n = a.width || 300, d = a.height || 320;
  o.width = n * s, o.height = d * s;
  const i = o.getContext("2d");
  if (!i) return;
  i.scale(s, s), i.font = e.templateStyle ? '10px "Ekom Mono", monospace' : "12px sans-serif";
  const f = r.points || [], w = f.map((v) => v.y == null ? null : Number(v.y)), u = ((te = e.comparison) == null ? void 0 : te.points) || [], l = [...w, ...u.map((v) => v.y)].filter((v) => v != null), c = Math.min(0, ...l), y = Math.max(1, ...l), x = 10 ** (Math.floor(Math.log10(y / 4)) - 1), P = e.integer ? Math.ceil(y / 4) * 4 : e.templateStyle ? Math.ceil(y / 4 / x) * x * 4 : y, D = (v) => e.formatValue ? e.formatValue(v) : e.currency ? G(v, e.currency) : v.toLocaleString(void 0, { maximumFractionDigits: e.integer ? 0 : 2 }), K = e.formatAxisValue ?? D, X = Array.from({ length: 5 }, (v, b) => P - (P - c) * b / 4), A = e.templateStyle ? 1 : Math.max(48, ...X.map((v) => i.measureText(K(v)).width + 12)), E = n - (e.templateStyle ? 1 : 16), q = e.templateStyle ? 8 : 20, O = d - (e.templateStyle ? 26 : 52), S = f.map((v, b) => {
    var _, M, R;
    return {
      x: f.length > 1 ? A + (E - A) * b / (f.length - 1) : (A + E) / 2,
      y: w[b] == null ? null : O - (w[b] - c) / (P - c) * (O - q),
      date: v.x || ((_ = r.labels) == null ? void 0 : _[b]) || "",
      axisLabel: ((M = e.formatLabel) == null ? void 0 : M.call(e, b)) ?? v.x ?? ((R = r.labels) == null ? void 0 : R[b]) ?? "",
      value: w[b]
    };
  }), H = (v = -1) => {
    var oe;
    i.clearRect(0, 0, n, d), i.font = e.templateStyle ? '10px "Ekom Mono", monospace' : "12px sans-serif";
    const b = typeof getComputedStyle == "function" ? getComputedStyle(o) : void 0, _ = (b == null ? void 0 : b.getPropertyValue("--ekom-muted").trim()) || (b == null ? void 0 : b.getPropertyValue("--uui-color-text-alt").trim()) || "#555", M = (b == null ? void 0 : b.getPropertyValue("--ekom-border").trim()) || "#e5e5e5", R = (b == null ? void 0 : b.getPropertyValue("--uui-color-text").trim()) || "", z = (oe = /^#([0-9a-f]{6})$/i.exec(R)) == null ? void 0 : oe[1], I = z ? [0, 2, 4].reduce((k, g) => k + parseInt(z.slice(g, g + 2), 16), 0) > 420 : !1, N = e.templateStyle && !I ? "#10694a" : t;
    i.lineWidth = 1, i.textAlign = e.templateStyle ? "left" : "right", X.forEach((k, g) => {
      const $ = q + (O - q) * g / 4;
      i.fillStyle = _, i.fillText(K(k), e.templateStyle ? A : A - 8, e.templateStyle ? Math.min(O, $ + 3) : $ + 4), !(e.templateStyle && (g === 0 || g === 4)) && (i.globalAlpha = e.templateStyle ? 0.5 : 1, i.strokeStyle = M, i.beginPath(), i.moveTo(A, $), i.lineTo(E, $), i.stroke(), i.globalAlpha = 1);
    });
    const V = Math.max(90, ...S.map((k) => i.measureText(k.axisLabel).width + 32)), re = Math.max(2, Math.floor((E - A) / V)), ge = new Set(Array.from({ length: Math.min(re, S.length) }, (k, g) => Math.round(g * (S.length - 1) / Math.max(1, Math.min(re, S.length) - 1))));
    i.fillStyle = _, S.forEach((k, g) => {
      ge.has(g) && (i.textAlign = S.length === 1 ? "center" : g === 0 ? "left" : g === S.length - 1 ? "right" : "center", i.fillText(k.axisLabel, k.x, O + 24));
    });
    const ae = (k, g) => {
      var C, U;
      i.strokeStyle = g ? "#9b9fad" : N, i.lineWidth = e.templateStyle ? g ? 1.5 : 2.5 : 2, i.lineJoin = "round", i.lineCap = "round", (C = i.setLineDash) == null || C.call(i, g ? [4, 5] : []), i.beginPath();
      let $ = !1;
      k.forEach((L) => {
        if (L.y == null) {
          $ = !1;
          return;
        }
        $ ? i.lineTo(L.x, L.y) : i.moveTo(L.x, L.y), $ = !0;
      }), i.stroke(), (U = i.setLineDash) == null || U.call(i, []);
    };
    if (e.templateStyle && S.length > 1) {
      const k = i.createLinearGradient(0, q, 0, O);
      k.addColorStop(0, "rgba(16,105,74,.22)"), k.addColorStop(1, "rgba(16,105,74,0)"), i.fillStyle = k;
      let g = [];
      const $ = () => {
        g.length > 1 && (i.beginPath(), i.moveTo(g[0].x, O), g.forEach((C) => i.lineTo(C.x, C.y)), i.lineTo(g[g.length - 1].x, O), i.closePath(), i.fill()), g = [];
      };
      S.forEach((C) => {
        C.y == null ? $() : g.push({ x: C.x, y: C.y });
      }), $();
    }
    u.length && ae(S.map((k, g) => {
      var $;
      return {
        x: k.x,
        y: (($ = u[g]) == null ? void 0 : $.y) == null ? null : O - (Number(u[g].y) - c) / (P - c) * (O - q)
      };
    }), !0), ae(S, !1), i.fillStyle = N, S.forEach((k, g) => {
      k.y == null || e.templateStyle && S.length > 1 && g !== v || (i.beginPath(), i.arc(k.x, k.y, g === v ? 5 : 3, 0, Math.PI * 2), i.fill());
    }), S.length || (i.textAlign = "center", i.fillText("No data for this period", (A + E) / 2, d / 2));
    const j = S[v];
    if (j) {
      const k = [j.date, j.value == null ? "—" : D(j.value)];
      if (u[v]) {
        const F = u[v];
        k.push(`Previous: ${F.x || ""}`, F.y == null ? "—" : D(F.y));
      }
      const g = Math.max(...k.map((F) => i.measureText(F).width)) + 20, $ = Math.max(0, Math.min(n - g, j.x - g / 2)), C = k.length * 18 + 12, U = j.y ?? O, L = Math.max(0, Math.min(d - C, U > C + 20 ? U - C - 12 : U + 12));
      i.fillStyle = "#222", i.fillRect($, L, g, C), i.fillStyle = "#fff", i.textAlign = "left", k.forEach((F, fe) => i.fillText(F, $ + 10, L + 19 + fe * 18));
    }
  }, ee = (v) => {
    const b = o.getBoundingClientRect(), _ = (v.clientX - b.left) * n / b.width, M = (v.clientY - b.top) * d / b.height;
    if (e.columnHover)
      return !S.length || _ < A || _ > E || M < q || M > O ? -1 : S.length === 1 ? 0 : Math.round((_ - A) / (E - A) * (S.length - 1));
    let R = -1, z = 16;
    return S.forEach((I, N) => {
      if (I.y == null) return;
      const V = Math.hypot(I.x - _, I.y - M);
      V < z && (R = N, z = V);
    }), R;
  };
  o.onmousemove = (v) => {
    const b = ee(v);
    o.style.cursor = b >= 0 ? "pointer" : "default", H(b);
  }, o.onclick = e.onSelect ? (v) => {
    var _;
    const b = ee(v);
    b >= 0 && ((_ = e.onSelect) == null || _.call(e, b));
  } : null, o.onmouseleave = () => {
    o.style.cursor = "default", H();
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
function Ae() {
  var t;
  const o = p.stores.find((e) => e.alias === p.filters.store), r = (o == null ? void 0 : o.currencies) || [];
  if (!r.includes(p.filters.currency || "")) {
    const e = (t = o == null ? void 0 : o.currency) == null ? void 0 : t.isoCurrencySymbol;
    p.filters.currency = r.find((a) => a === e) || r[0] || "";
  }
}
function Pe(o = "data-field") {
  const r = p.stores.find((e) => e.alias === p.filters.store), t = p.filters.currency || "";
  return `<div class="ekom-store-switch"><span>Store</span><details class="ekom-dropdown ekom-store-currency"><summary aria-label="Store and currency"><strong>${m((r == null ? void 0 : r.title) || "Select store")}</strong>${t ? `<span class="ekom-currency-code">${m(t)}</span>` : ""}<span class="ekom-store-chevron">▾</span></summary><div class="ekom-currency-popover"><div class="ekom-currency-heading"><strong>Store ·<br>Currency</strong><span>Amounts shown in the selected currency</span></div>${p.stores.map((e) => `<section class="ekom-currency-store ${e.alias === (r == null ? void 0 : r.alias) ? "is-selected" : ""}" aria-label="${m(e.title)}"><h3>${m(e.title)}</h3><div class="ekom-currency-choices">${(e.currencies || []).map((a) => `<button type="button" data-header-store="${m(e.alias)}" data-header-currency="${m(a)}" aria-label="${m(e.title)} — ${m(a)}" aria-pressed="${e.alias === (r == null ? void 0 : r.alias) && a === t}">${m(a)}${e.alias === (r == null ? void 0 : r.alias) && a === t ? '<span aria-hidden="true">✓</span>' : ""}</button>`).join("") || '<span class="ekom-currency-empty">No currencies configured or recorded</span>'}</div></section>`).join("")}</div></details><select hidden aria-label="Store" ${o}="store">${p.stores.map((e) => `<option value="${m(e.alias)}" ${e.alias === (r == null ? void 0 : r.alias) ? "selected" : ""}>${m(e.title)}</option>`).join("")}</select></div>`;
}
function De(o, r = "data-field", t = !0) {
  if (typeof document < "u" && !document.querySelector("link[data-ekom-fonts]")) {
    const s = document.createElement("link");
    s.rel = "stylesheet", s.href = "/App_Plugins/Ekom/dist/manager-fonts.css", s.dataset.ekomFonts = "", document.head.append(s);
  }
  const e = ["orders", "analytics", "customers", ...p.printingEnabled || o === "printing" ? ["printing"] : []], a = typeof location > "u" ? "/umbraco/section/ekommanager" : location.pathname.replace(/\/view\/(orders|analytics|customers|printing)\/?$/, "").replace(/\/$/, "");
  return `<header class="ekom-design-header"><img src="/App_Plugins/Ekom/dist/ekom-logo.svg" alt="Ekom" class="ekom-design-logo"><nav aria-label="Ekom sections" class="ekom-section-switch">${e.map((s) => `<a href="${m(a + "/view/" + s)}" ${s === o ? 'aria-current="page"' : ""}>${s[0].toUpperCase() + s.slice(1)}</a>`).join("")}</nav>${t ? Pe(r) : '<span class="ekom-store-switch">Accessible stores</span>'}</header>`;
}
function Me(o) {
  var t;
  const r = [...Oe];
  return r.some((e) => e.key === o) || r.push({ key: o, label: o.startsWith("states:") ? "Custom status filters" : ((t = p.statusList.find((e) => ke(e) === o)) == null ? void 0 : t.label) || o }), r.map((e) => `<option value="${m(e.key)}" ${e.key === o ? "selected" : ""}>${m(e.label)}</option>`).join("");
}
function Le(o = /* @__PURE__ */ new Date()) {
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
function Te() {
  return `<ekom-date-range from="${m(p.filters.dateFrom)}" to="${m(p.filters.dateTo)}"></ekom-date-range>`;
}
function Ee(o, r) {
  var t, e;
  Re(), (t = o.querySelector(".ekom-store-currency")) == null || t.addEventListener("keydown", (a) => {
    var n;
    if (a.key !== "Escape") return;
    const s = a.currentTarget;
    s.open = !1, (n = s.querySelector("summary")) == null || n.focus(), a.stopPropagation();
  }), o.querySelectorAll("[data-header-store]").forEach((a) => a.addEventListener("click", () => {
    var n;
    const s = o.querySelector('[data-field="store"], [data-filter="store"]');
    s && (p.filters.currency = a.dataset.headerCurrency || "", (n = a.closest("details")) == null || n.removeAttribute("open"), s.value = a.dataset.headerStore || "", s.dispatchEvent(new Event("change", { bubbles: !0 })));
  })), o.querySelectorAll("[data-shared-status]").forEach((a) => a.addEventListener("change", () => {
    const s = [0, 1, 2].map((n) => Array.from(o.querySelectorAll(`[data-shared-status="${n}"]:checked`), (d) => d.value).join(","));
    p.filters.orderStatus = s.some(Boolean) ? "states:" + s.join(";") : "AllOrders", r();
  })), (e = o.querySelector("ekom-date-range")) == null || e.addEventListener("date-range-apply", (a) => {
    const s = a.detail;
    p.filters.dateFrom = s.from, p.filters.dateTo = s.to, r();
  });
}
function Re() {
  if (typeof customElements > "u" || !customElements.get || customElements.get("ekom-date-range")) return;
  class o extends HTMLElement {
    constructor() {
      super(...arguments);
      h(this, "root", this.attachShadow({ mode: "open" }));
      h(this, "open", !1);
      h(this, "from", "");
      h(this, "to", "");
      h(this, "month", 0);
      h(this, "reposition", () => {
        const e = this.root.querySelector(".popover"), a = this.root.querySelector(".anchor"), s = this.ownerDocument.defaultView;
        if (!e || !a || !s) return;
        if (s.innerWidth <= 650) {
          e.style.left = "";
          return;
        }
        const n = a.getBoundingClientRect().left;
        e.style.left = `${Math.min(0, Math.max(24 - n, s.innerWidth - 24 - n - e.getBoundingClientRect().width))}px`;
      });
      h(this, "outside", (e) => {
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
      return e ? `${e.slice(8, 10)}.${e.slice(5, 7)}.${e.slice(0, 4)}` : "Pick a date";
    }
    calendar(e) {
      const a = new Date(Math.floor(e / 12), e % 12, 1), s = (a.getDay() + 6) % 7, n = new Date(a.getFullYear(), a.getMonth() + 1, 0).getDate();
      return `<section class="month"><h3>${a.toLocaleDateString("en", { month: "long", year: "numeric" })}</h3><div class="days">${["Mo", "Tu", "We", "Th", "Fr", "Sa", "Su"].map((d) => `<span class="weekday">${d}</span>`).join("")}${"<span></span>".repeat(s)}${Array.from({ length: n }, (d, i) => {
        const f = `${a.getFullYear()}-${String(a.getMonth() + 1).padStart(2, "0")}-${String(i + 1).padStart(2, "0")}`, w = f === this.from || f === this.to, u = this.from && this.to && f > this.from && f < this.to;
        return `<button type="button" data-day="${f}" aria-label="${f}" aria-pressed="${w || !!u}" class="day ${w ? "edge" : u ? "inside" : ""} ${f === this.from ? "start" : ""} ${f === this.to ? "end" : ""} ${!this.to || this.to === this.from ? "single" : ""}">${i + 1}</button>`;
      }).join("")}</div></section>`;
    }
    render() {
      var d, i, f, w, u;
      const e = this.getAttribute("from") || "", a = this.getAttribute("to") || "", s = Le(), n = ((d = s.find((l) => l.from === e && l.to === a)) == null ? void 0 : d.label) || "Custom";
      pe(this.root, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/date-range.css"><div class="range"><span class="label">Date range</span><div class="anchor"><button type="button" class="trigger" data-toggle aria-haspopup="dialog" aria-expanded="${this.open}"><strong>${n}</strong><span class="dates">${this.date(e)} – ${this.date(a)}</span><span class="chevron">▾</span></button>${this.open ? `<div class="popover" role="dialog" aria-label="Choose date range"><nav aria-label="Date presets">${s.map((l, c) => `<button type="button" data-preset="${c}" aria-pressed="${l.from === this.from && l.to === this.to}">${l.label}<span>${l.from === this.from && l.to === this.to ? "✓" : ""}</span></button>`).join("")}</nav><div class="custom"><div class="draft"><div class="${this.from ? "" : "active"}"><span>From</span><strong>${this.date(this.from)}</strong></div><span>→</span><div class="${this.from && !this.to ? "active" : ""}"><span>To</span><strong>${this.date(this.to)}</strong></div></div><div class="calendars"><button type="button" class="previous" data-month="-1" aria-label="Previous month">‹</button><button type="button" class="next" data-month="1" aria-label="Next month">›</button>${this.calendar(this.month)}${this.calendar(this.month + 1)}</div><footer><span>${this.from && this.to ? `${this.date(this.from)} – ${this.date(this.to)}` : "Select an end date"}</span><button type="button" data-cancel>Cancel</button><button type="button" class="apply" data-apply ${!this.from || !this.to ? "disabled" : ""}>Apply</button></footer></div></div>` : ""}</div></div>`), (i = this.root.querySelector("link")) == null || i.addEventListener("load", this.reposition), this.reposition(), (f = this.root.querySelector("[data-toggle]")) == null || f.addEventListener("click", () => {
        var l;
        if (this.open) {
          this.close();
          return;
        }
        this.reset(), this.open = !0, this.render(), (l = this.root.querySelector("[data-preset]")) == null || l.focus();
      }), (w = this.root.querySelector("[data-cancel]")) == null || w.addEventListener("click", () => this.close()), (u = this.root.querySelector("[data-apply]")) == null || u.addEventListener("click", () => {
        !this.from || !this.to || this.from > this.to || (this.setAttribute("from", this.from), this.setAttribute("to", this.to), this.close(), this.dispatchEvent(new CustomEvent("date-range-apply", { bubbles: !0, composed: !0, detail: { from: this.from, to: this.to } })));
      }), this.root.querySelectorAll("[data-preset]").forEach((l) => l.addEventListener("click", () => {
        var x;
        const c = s[Number(l.dataset.preset)];
        this.from = c.from, this.to = c.to;
        const y = /* @__PURE__ */ new Date(`${this.from}T12:00:00`);
        this.month = y.getFullYear() * 12 + y.getMonth(), this.render(), (x = this.root.querySelector(`[data-preset="${l.dataset.preset}"]`)) == null || x.focus();
      })), this.root.querySelectorAll("[data-month]").forEach((l) => l.addEventListener("click", () => {
        var c;
        this.month += Number(l.dataset.month), this.render(), (c = this.root.querySelector(`[data-month="${l.dataset.month}"]`)) == null || c.focus();
      })), this.root.querySelectorAll("[data-day]").forEach((l) => l.addEventListener("click", () => {
        var y;
        const c = l.dataset.day;
        !this.from || this.to || c < this.from ? (this.from = c, this.to = "") : this.to = c, this.render(), (y = this.root.querySelector(`[data-day="${c}"]`)) == null || y.focus();
      })), this.onkeydown = (l) => {
        l.key === "Escape" && this.open && (l.preventDefault(), l.stopPropagation(), this.close());
      };
    }
  }
  customElements.define("ekom-date-range", o);
}
function Fe() {
  const o = p.filters.orderStatus.startsWith("states:") ? p.filters.orderStatus.slice(7).split(";") : ["", "", ""];
  return [
    { label: "Order", values: ["Draft", "Open", "Closed", "Cancelled"] },
    { label: "Payment", values: ["Pending", "Authorized", "Paid", "PartiallyPaid", "Failed", "Voided"] },
    { label: "Fulfillment", values: ["Unfulfilled", "Picking", "Picked", "Packed", "PartiallyFulfilled", "Fulfilled"] }
  ].map((t, e) => {
    const a = (o[e] || "").split(",").filter(Boolean), s = a.length === 1 ? a[0].replace(/([a-z])([A-Z])/g, "$1 $2") : a.length ? `${a.length} selected` : "Any";
    return `<div class="ekom-status-filter"><span>${t.label}</span><details class="ekom-dropdown"><summary>${m(s)}<span>▾</span></summary><div class="ekom-dropdown-options">${t.values.map((n) => `<label><input type="checkbox" data-shared-status="${e}" value="${n}" ${a.includes(n) ? "checked" : ""}>${n.replace(/([a-z])([A-Z])/g, "$1 $2")}</label>`).join("")}</div></details></div>`;
  }).join("");
}
function pe(o, r) {
  const t = o.ownerDocument.createElement("template");
  t.innerHTML = r;
  const e = Array.from(o.children).filter((s) => s.matches('style, link[rel="stylesheet"]')), a = Array.from(t.content.children).filter((s) => s.matches('style, link[rel="stylesheet"]'));
  for (const s of a) {
    const n = e.find((d) => d.outerHTML === s.outerHTML);
    n ? e.splice(e.indexOf(n), 1) : o.appendChild(s.cloneNode(!0)), s.remove();
  }
  for (const s of e) s.remove();
  for (const s of Array.from(o.childNodes))
    (s.nodeType !== 1 || !s.matches('style, link[rel="stylesheet"]')) && s.remove();
  o.appendChild(t.content);
}
const qe = (o) => class extends o {
  connectedCallback() {
  }
  disconnectedCallback() {
  }
}, W = 864e5, J = (o) => o.toISOString().slice(0, 10), Y = (o) => /* @__PURE__ */ new Date(`${o}T00:00:00Z`);
function de(o, r) {
  const t = Y(o), e = Y(r), a = Math.round((e.getTime() - t.getTime()) / W) + 1;
  return { dateFrom: J(new Date(t.getTime() - a * W)), dateTo: J(new Date(t.getTime() - W)) };
}
function he(o, r = /* @__PURE__ */ new Date()) {
  const t = new Date(Date.UTC(r.getFullYear(), r.getMonth(), r.getDate())), e = new Date(t);
  return o === "7" && e.setUTCDate(e.getUTCDate() - 6), o === "30" && e.setUTCDate(e.getUTCDate() - 29), o === "month" && e.setUTCDate(1), o === "year" && (e.setUTCMonth(0), e.setUTCDate(1)), { dateFrom: J(e), dateTo: J(t) };
}
function je(o, r, t, e = /* @__PURE__ */ new Date()) {
  if (t === "") return "";
  const a = ["today", "7", "30", "month", "year"];
  return t && a.includes(t) && a.unshift(t), a.find((s) => {
    const n = he(s, e);
    return n.dateFrom === o && n.dateTo === r;
  }) || "";
}
function Ue(o, r, t) {
  var n, d, i, f, w;
  const e = [], a = o.ordersChart.points;
  let s = 0;
  for (; s < a.length; ) {
    const u = s, l = a[s].x, c = Y(l);
    t === "month" ? (c.setUTCDate(1), c.setUTCMonth(c.getUTCMonth() + 1)) : t === "week" ? c.setUTCDate(c.getUTCDate() + (7 - (c.getUTCDay() + 6) % 7)) : c.setUTCDate(c.getUTCDate() + 1);
    let y = 0, x = 0, P = 0, D = 0;
    do
      y += Number(((n = o.revenueChart.points[s]) == null ? void 0 : n.y) || 0), x += Number(a[s].y || 0), P += Number(((d = r == null ? void 0 : r.revenueChart.points[s]) == null ? void 0 : d.y) || 0), D += Number(((i = r == null ? void 0 : r.ordersChart.points[s]) == null ? void 0 : i.y) || 0), s++;
    while (s < a.length && Y(a[s].x).getTime() < c.getTime());
    e.push({
      start: l,
      end: a[s - 1].x,
      amount: y,
      orders: x,
      average: x ? y / x : null,
      ...r ? {
        previousStart: (f = r.ordersChart.points[u]) == null ? void 0 : f.x,
        previousEnd: (w = r.ordersChart.points[s - 1]) == null ? void 0 : w.x,
        previousAmount: P,
        previousOrders: D,
        previousAverage: D ? P / D : null
      } : {}
    });
  }
  return e;
}
function le(o) {
  const r = o.revenueChart.points.reduce((e, a) => e + Number(a.y || 0), 0), t = o.ordersChart.points.reduce((e, a) => e + Number(a.y || 0), 0);
  return { amount: r, orders: t, average: t ? r / t : null };
}
function ce(o, r) {
  if (o == null || r == null) return "No comparison available";
  if (r === 0) return o === 0 ? "No change" : "No prior value";
  const t = (o - r) / Math.abs(r) * 100;
  return `${t > 0 ? "+" : ""}${t.toLocaleString(void 0, { maximumFractionDigits: 1 })}%`;
}
class ze extends qe(HTMLElement) {
  constructor() {
    super(...arguments);
    h(this, "api", new T());
    h(this, "loading", !0);
    h(this, "loadingMostSoldProducts", !1);
    h(this, "error", "");
    h(this, "productError", "");
    h(this, "chartData");
    h(this, "previousData");
    h(this, "chartFilters");
    h(this, "mostSoldProducts", { products: [], count: 0, totalPages: 0, page: 1 });
    h(this, "pageMostSoldProducts", 1);
    h(this, "resizeObserver");
    h(this, "themeObserver");
    h(this, "themeLoaded", () => this.renderCharts());
    h(this, "chartRequest", 0);
    h(this, "productRequest", 0);
    h(this, "chartAbort");
    h(this, "productAbort");
    h(this, "disposed", !1);
    h(this, "productsVisible", !1);
    h(this, "metric", "amount");
    h(this, "grouping", "day");
    h(this, "compare", !0);
    h(this, "table", !1);
    h(this, "datePreset");
    h(this, "pollingSeconds", 0);
    h(this, "pollingTimer", 0);
    h(this, "polling", !1);
  }
  setPolling(t) {
    [0, 2, 5, 10, 20, 30].includes(t) && (window.clearInterval(this.pollingTimer), this.pollingSeconds = t, t && (this.pollingTimer = window.setInterval(() => void this.pollAnalytics(), t * 1e3)), this.render());
  }
  async pollAnalytics() {
    if (!this.pollingSeconds || this.polling || this.disposed || document.hidden || this.loading || this.loadingMostSoldProducts || this.contains(document.activeElement) || this.querySelector('details[open], dialog[open], [role="dialog"]')) return;
    const t = { ...p.filters };
    if (!t.store) return;
    const e = this.chartRequest, a = this.productRequest, s = this.compare, n = this.productsVisible;
    this.polling = !0;
    try {
      const [d, i, f] = await Promise.all([
        this.api.charts(t),
        s ? this.api.charts({ ...t, ...de(t.dateFrom, t.dateTo) }) : Promise.resolve(void 0),
        n ? this.api.mostSoldProducts(t, this.pageMostSoldProducts, 20) : Promise.resolve(void 0)
      ]);
      if (this.disposed || !this.pollingSeconds || e !== this.chartRequest || a !== this.productRequest || s !== this.compare || n !== this.productsVisible || JSON.stringify(t) !== JSON.stringify(p.filters) || this.contains(document.activeElement) || this.querySelector('details[open], dialog[open], [role="dialog"]')) return;
      const w = f ? { ...f, products: f.products || [] } : this.mostSoldProducts, u = this.error || this.productError || JSON.stringify([d, i, w]) !== JSON.stringify([this.chartData, this.previousData, this.mostSoldProducts]);
      this.chartData = d, this.previousData = i, this.chartFilters = t, this.mostSoldProducts = w, this.error = "", this.productError = "", d.mixedCurrencies && (this.metric = "orders"), u && this.render();
      const l = this.querySelector(".ekom-polling summary");
      l == null || l.classList.add("just-refreshed"), l == null || l.addEventListener("animationend", (c) => {
        c.target === l && l.classList.remove("just-refreshed");
      }, { once: !0 });
    } catch {
    } finally {
      this.polling = !1;
    }
  }
  connectedCallback() {
    var t;
    if (super.connectedCallback(), this.hasAttribute("classic") || (t = this.consumeContext) == null || t.call(this, "umbThemeContext", (e) => {
      e && this.observe(e.theme, (a) => {
        this.toggleAttribute("data-ekom-dark", a === "umb-dark-theme");
      }, "ekom-manager-theme");
    }), this.disposed = !1, this.hasAttribute("classic"))
      for (const e of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const a = this.getAttribute(e.toLowerCase());
        a && (p.filters[e] = a);
      }
    this.render(), this.resizeObserver = new ResizeObserver(() => this.renderCharts()), this.resizeObserver.observe(this), typeof MutationObserver < "u" && (this.themeObserver = new MutationObserver(() => this.renderCharts()), this.themeObserver.observe(document.head, { childList: !0, subtree: !0, attributes: !0, attributeFilter: ["href"] }), document.head.addEventListener("load", this.themeLoaded, !0)), this.initialize();
  }
  disconnectedCallback() {
    var t, e, a, s;
    this.disposed = !0, window.clearInterval(this.pollingTimer), this.pollingSeconds = 0, this.chartRequest++, this.productRequest++, (t = this.chartAbort) == null || t.abort(), (e = this.productAbort) == null || e.abort(), (a = this.resizeObserver) == null || a.disconnect(), this.resizeObserver = void 0, (s = this.themeObserver) == null || s.disconnect(), typeof document < "u" && document.head.removeEventListener("load", this.themeLoaded, !0), super.disconnectedCallback();
  }
  get currency() {
    var e, a;
    const t = ((e = this.chartFilters) == null ? void 0 : e.currency) || p.filters.currency;
    return t ? { isoCurrencySymbol: t } : (a = p.stores.find((s) => {
      var n;
      return s.alias === (((n = this.chartFilters) == null ? void 0 : n.store) || p.filters.store);
    })) == null ? void 0 : a.currency;
  }
  async initialize() {
    var t;
    try {
      const [e, a, s] = await Promise.all([this.api.statusList(), this.api.stores(), this.hasAttribute("classic") ? Promise.resolve(!1) : this.api.printingEnabled()]);
      if (p.printingEnabled = s, this.disposed) return;
      p.statusList = e || [], p.stores = a || [], p.stores.some((n) => n.alias === p.filters.store) || (p.filters.store = ((t = p.stores[0]) == null ? void 0 : t.alias) || ""), this.hasAttribute("classic") || Ae(), await this.loadAnalytics();
    } catch (e) {
      if (this.disposed) return;
      this.error = Q(e, "Unable to load Analytics."), this.loading = !1, this.render();
    }
  }
  async loadAnalytics() {
    await Promise.all([this.loadCharts(), this.productsVisible ? this.loadMostSoldProducts() : Promise.resolve()]);
  }
  async loadCharts() {
    var n;
    const t = ++this.chartRequest;
    (n = this.chartAbort) == null || n.abort();
    const e = this.chartAbort = new AbortController(), a = { ...p.filters };
    a.store || (this.chartData = void 0, this.previousData = void 0), this.error = "", this.loading = !!a.store;
    const s = this.querySelector(".analytics");
    if (s && a.store ? s.setAttribute("aria-busy", "true") : this.render(), !!a.store)
      try {
        const d = this.compare ? { ...a, ...de(a.dateFrom, a.dateTo) } : void 0, [i, f] = await Promise.all([
          this.api.charts(a, e.signal),
          d ? this.api.charts(d, e.signal) : Promise.resolve(void 0)
        ]);
        if (t !== this.chartRequest || this.disposed) return;
        this.chartData = i, this.previousData = f, this.chartFilters = a, i.mixedCurrencies && (this.metric = "orders");
      } catch (d) {
        if (t !== this.chartRequest || this.disposed) return;
        this.chartData = void 0, this.previousData = void 0, this.error = Q(d, "Unable to load charts.");
      } finally {
        t === this.chartRequest && !this.disposed && (this.loading = !1, this.render());
      }
  }
  async loadMostSoldProducts() {
    var s;
    const t = ++this.productRequest;
    (s = this.productAbort) == null || s.abort();
    const e = this.productAbort = new AbortController(), a = { ...p.filters };
    if (this.mostSoldProducts = { products: [], count: 0, totalPages: 0, page: this.pageMostSoldProducts }, this.productError = "", this.loadingMostSoldProducts = !!a.store, this.render(), !!a.store)
      try {
        const n = await this.api.mostSoldProducts(a, this.pageMostSoldProducts, 20, e.signal);
        if (t !== this.productRequest || this.disposed) return;
        this.mostSoldProducts = { ...n, products: n.products || [] }, this.pageMostSoldProducts = n.page || this.pageMostSoldProducts;
      } catch (n) {
        t === this.productRequest && !this.disposed && (this.productError = Q(n, "Unable to load products."));
      } finally {
        t === this.productRequest && !this.disposed && (this.loadingMostSoldProducts = !1, this.render());
      }
  }
  get buckets() {
    return this.chartData ? Ue(this.chartData, this.previousData, this.grouping) : [];
  }
  get comparisonMoneyAvailable() {
    var t, e;
    return !!this.previousData && !((t = this.chartData) != null && t.mixedCurrencies) && !this.previousData.mixedCurrencies && (!((e = this.chartData) != null && e.currency) || !this.previousData.currency || this.chartData.currency === this.previousData.currency);
  }
  amount(t) {
    var a, s, n, d, i;
    if (t == null || (a = this.chartData) != null && a.mixedCurrencies) return "—";
    const e = ((s = this.chartData) == null ? void 0 : s.currency) || ((n = this.previousData) == null ? void 0 : n.currency);
    return !e || e === ((d = this.currency) == null ? void 0 : d.currencyValue) || e === ((i = this.currency) == null ? void 0 : i.isoCurrencySymbol) ? G(t, this.currency) : /^[A-Z]{3}$/.test(e) ? G(t, { isoCurrencySymbol: e }) : `${t.toLocaleString(void 0, { maximumFractionDigits: 2 })} ${e}`;
  }
  label(t) {
    return t === "orders" ? "Orders" : t === "average" ? "Average order value" : "Order amount";
  }
  value(t, e = this.metric) {
    return e === "orders" ? t == null ? "—" : t.toLocaleString() : this.amount(t);
  }
  previousValue(t) {
    return this.metric === "orders" ? t.previousOrders : this.metric === "average" ? t.previousAverage : t.previousAmount;
  }
  range(t, e) {
    return t === e ? t || "" : `${t || ""} – ${e || ""}`;
  }
  render() {
    const t = p.filters, e = je(t.dateFrom, t.dateTo, this.datePreset);
    pe(this, `<style>${Ce}${this.hasAttribute("classic") ? `
      ekom-analytics-section-view { display:block;color:var(--uui-color-text,#20232e) }
      .analytics {background:var(--uui-color-background,#f3f4f7);min-height:80vh}
      .analytics .card {background:var(--uui-color-surface,#fff);border:1px solid #dfe2e8;border-radius:10px;padding:18px;text-align:left;box-shadow:none}
      .analytics .analytics-main {margin-top:18px}
      .analytics h3 {margin:0;font-size:16px}
      .analytics button:disabled {opacity:.5;cursor:default}
      .analytics-toolbar {display:flex;flex-wrap:wrap;gap:12px;align-items:end;margin-bottom:18px}
      .analytics-toolbar label {display:grid;gap:5px;font-size:12px}
      .analytics-toolbar input,.analytics-toolbar select,.analytics-toolbar button,.analytics-table button {font:inherit;padding:8px 12px;border:1px solid #dfe2e8;border-radius:6px;background:var(--uui-color-surface,#fff);color:inherit}
      .analytics-toolbar button {cursor:pointer}
      .analytics-toolbar button[aria-pressed=true] {background:#e7f1ed;border-color:#26795c}
      .analytics-summary {display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:12px;margin:18px 0}
      .analytics-summary .card > span:first-child {display:block;color:#646b7c;font-size:12px;margin-bottom:8px}
      .analytics-summary strong {display:block;font-size:23px;margin:0 0 8px}
      .analytics-muted {color:#70778a;font-size:12px;line-height:1.5}
      .analytics-table-wrap {overflow:auto;max-height:520px}
      .analytics-table {width:100%;border-collapse:collapse}
      .analytics-table th,.analytics-table td {text-align:left;padding:15px 10px;border-bottom:1px solid #e8e9ed;white-space:nowrap}
      .analytics-table th {position:sticky;top:0;background:var(--uui-color-surface,#fff);font-size:11px;text-transform:uppercase;letter-spacing:.07em;color:#70778a}
      .analytics-main {padding:22px}
      .analytics-insight-grid {display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:18px}
      .analytics-time-bars {display:grid;gap:8px;margin-top:16px}
      .analytics-hour-bars {grid-template-columns:repeat(auto-fit,minmax(150px,1fr));gap:10px 18px}
      .analytics-time-row {display:grid;grid-template-columns:64px 1fr 32px;align-items:center;gap:8px;font-size:12px}
      .analytics-time-track {height:8px;background:#edf2f0;border-radius:4px;overflow:hidden}
      .analytics-time-track span {display:block;height:100%;background:#25765c;border-radius:4px}
      .analytics-spinner {display:inline-block;width:14px;height:14px;margin-right:8px;border:2px solid #dfe2e8;border-top-color:#25765c;border-radius:50%;animation:analytics-spin 1s linear infinite}
      @keyframes analytics-spin {to {transform:rotate(360deg)}}
      @media (prefers-reduced-motion:reduce) {.analytics-spinner {animation:none}}
    ` : ""}</style>${this.hasAttribute("classic") ? "" : '<link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css"><link rel="stylesheet" href="/App_Plugins/Ekom/dist/analytics-layout.css">'}<section class="ekmManager analytics ekom-design" aria-busy="${this.loading}"><div class="ekmManager__body">${this.hasAttribute("classic") ? "" : De("analytics")}
      <div class="analytics-toolbar ekom-shared-filters">
        ${this.hasAttribute("classic") ? `<label>Store<select data-field="store">${p.stores.map((a) => `<option value="${m(a.alias)}" ${a.alias === t.store ? "selected" : ""}>${m(a.title)}</option>`).join("")}</select></label>` : ""}
        ${this.hasAttribute("classic") ? `<label>Order view<select data-field="orderStatus">${Me(t.orderStatus)}</select></label>` : Fe()}
        ${this.hasAttribute("classic") ? `        <label>Date From<input type="date" data-field="dateFrom" value="${m(t.dateFrom)}"></label>
        <label>Date To<input type="date" data-field="dateTo" value="${m(t.dateTo)}"></label>
        <label>Date preset<select data-preset><option value="">Custom range</option>${[["today", "Today"], ["7", "Last 7 days"], ["30", "Last 30 days"], ["month", "This month"], ["year", "This year"]].map(([a, s]) => `<option value="${a}" ${e === a ? "selected" : ""}>${s}</option>`).join("")}</select></label>
` : Te()}
        ${this.hasAttribute("classic") ? `<label><span>Comparison</span><span><input type="checkbox" data-compare ${this.compare ? "checked" : ""}> Previous period</span></label><button data-refresh>Refresh</button>` : ""}
      </div>
      ${this.hasAttribute("classic") ? "" : `<div class="analytics-comparison-actions">
        <label class="analytics-compare"><input type="checkbox" data-compare ${this.compare ? "checked" : ""}><span>Compare to previous period</span></label>
        <details class="ekom-polling${this.pollingSeconds ? " is-active" : ""}"><summary aria-label="Analytics polling"><svg aria-hidden="true" width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2.2" stroke-linecap="round" stroke-linejoin="round"><path d="M21 12a9 9 0 1 1-3-6.7"></path><path d="M21 3v6h-6"></path></svg><span>${this.pollingSeconds ? `Polling every ${this.pollingSeconds}s` : "Polling off"}</span><span aria-hidden="true">⌄</span></summary><div class="ekom-poll-options" role="group" aria-label="Polling interval">${[0, 2, 5, 10, 20, 30].map((a) => `<button type="button" data-poll-seconds="${a}" aria-pressed="${a === this.pollingSeconds}"><span aria-hidden="true">•</span>${a ? `Every ${a} seconds` : "Off"}</button>`).join("")}</div></details>
      </div>`}
      ${this.error ? `<p role="alert">${m(this.error)}</p>` : ""}
      ${this.loading ? '<p role="status">Loading analytics…</p>' : t.store ? "" : "<p>No accessible stores.</p>"}
      ${this.chartData ? this.renderAnalytics() + this.renderCustomerInsights() : ""}
      <div class="card analytics-main analytics-products"><div class="analytics-toolbar"><h3>Most Sold Products</h3><button data-products>${this.productsVisible ? "Hide products" : "Load product breakdown"}</button></div>
        <p class="analytics-muted">Fetch the most sold products for the selected store and dates. This may take a minute for larger date ranges. Totals include VAT and discounts, excluding shipping and payment fees.</p>
        ${this.productsVisible ? `${this.productError ? `<p role="alert">${m(this.productError)}</p><button data-product-retry>Retry products</button>` : ""}${this.loadingMostSoldProducts ? '<p role="status"><span class="analytics-spinner" aria-hidden="true"></span>Fetching product breakdown… This may take a minute. You can continue using Analytics.</p>' : this.renderMostSoldProducts()}` : ""}
      </div>
    </div></section>`), this.bindEvents(), this.renderCharts();
  }
  displayRange(t, e) {
    return !t || !e ? "" : new Intl.DateTimeFormat("en-GB", { day: "numeric", month: "short", year: "numeric", timeZone: "UTC" }).formatRange(/* @__PURE__ */ new Date(`${t.slice(0, 10)}T00:00:00Z`), /* @__PURE__ */ new Date(`${e.slice(0, 10)}T00:00:00Z`));
  }
  renderAnalytics() {
    var a, s, n, d;
    const t = le(this.chartData), e = this.previousData ? le(this.previousData) : void 0;
    return `<div class="analytics-summary">${["amount", "orders", "average"].map((i) => `<div class="card"><span>${this.label(i)}</span><strong>${m(this.value(t[i], i))}</strong><span class="analytics-muted">${e && (i === "orders" || this.comparisonMoneyAvailable) ? m(ce(t[i], e[i])) + " vs previous period" : this.compare ? "No monetary comparison available" : "Selected period"}</span></div>`).join("")}</div>
      <p class="analytics-muted">Order amount is the recorded charged amount, after gift cards; it is not refund-adjusted revenue. Dates use ${this.chartData.dateBasis === "PaidDate" ? "payment date" : "order creation date"}, matching Orders.</p>
      ${this.chartData.mixedCurrencies ? '<p role="status">This period contains multiple currencies. Monetary totals are hidden to avoid combining different currencies.</p>' : ""}
      ${this.previousData && !this.comparisonMoneyAvailable ? '<p class="analytics-muted">Monetary comparison is unavailable across different or mixed currencies.</p>' : ""}
      <div class="card analytics-main"><div class="analytics-toolbar">
        <div class="analytics-metrics">${["amount", "orders", "average"].map((i) => `<button data-metric="${i}" aria-pressed="${this.metric === i}" ${i !== "orders" && this.chartData.mixedCurrencies ? "disabled" : ""}>${this.label(i)}</button>`).join("")}</div>
        <label class="analytics-grouping">Group by<select data-grouping>${[["day", "Daily"], ["week", "Weekly"], ["month", "Monthly"]].map(([i, f]) => `<option value="${i}" ${this.grouping === i ? "selected" : ""}>${f}</option>`).join("")}</select></label>
        <button data-table aria-pressed="${this.table}">${this.table ? "Show chart" : "Show data table"}</button><button data-csv>Download CSV</button>
      </div>
      <p class="analytics-muted">${this.previousData ? `Solid: ${m(this.displayRange((a = this.chartFilters) == null ? void 0 : a.dateFrom, (s = this.chartFilters) == null ? void 0 : s.dateTo))}. Dashed: ${m(this.displayRange((n = this.previousData.ordersChart.points[0]) == null ? void 0 : n.x, (d = this.previousData.ordersChart.points.at(-1)) == null ? void 0 : d.x))}. Comparison buckets cover the same number of days.` : "Selected period."} Grouped ${this.grouping === "month" ? "monthly" : this.grouping === "week" ? "weekly" : "daily"}.</p>
      ${this.table ? this.renderTable() : `<div class="analytics-chart-frame" style="height:${this.hasAttribute("classic") ? 360 : 266}px"><canvas id="analytics-chart" style="width:100%;height:100%" role="img" aria-label="${this.label(this.metric)} over time. Use Show data table for exact values and order links."></canvas></div><div class="analytics-legend"><span><i></i>This period</span>${this.previousData ? '<span><i class="previous"></i>Previous period</span>' : ""}<span>Click a bucket to open its orders</span></div>`}
      </div>`;
  }
  renderCustomerInsights() {
    var f, w, u;
    const t = (f = this.chartData) == null ? void 0 : f.customerInsights;
    if (!t) return "";
    const e = (w = this.previousData) == null ? void 0 : w.customerInsights, a = [
      ["Unique purchasing customers", t.uniqueCustomers, e == null ? void 0 : e.uniqueCustomers, !1],
      ["First-time buyers", t.newCustomers, e == null ? void 0 : e.newCustomers, !1],
      ["Returning customer rate", t.returningCustomerRate, e == null ? void 0 : e.returningCustomerRate, !0]
    ], s = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"], n = s.map((l, c) => t.orderTimes.filter((y) => y.weekday === c).reduce((y, x) => y + x.orders, 0)), d = Array.from({ length: 24 }, (l, c) => t.orderTimes.filter((y) => y.hour === c).reduce((y, x) => y + x.orders, 0)), i = (l, c) => l.map((y, x) => `<div class="analytics-time-row" tabindex="0" data-tooltip="${y}: ${c[x]} orders" aria-label="${y}: ${c[x]} orders"><span>${y}</span><div class="analytics-time-track" aria-hidden="true"><span style="width:${100 * c[x] / Math.max(1, ...c)}%"></span></div><span>${c[x]}</span></div>`).join("");
    return `<div class="analytics-summary">${a.map(([l, c, y, x]) => `<div class="card"><span>${l}</span><strong>${c == null ? "—" : m(c.toLocaleString(void 0, { maximumFractionDigits: x ? 1 : 0 })) + (x ? "%" : "")}</strong><span class="analytics-muted">${e ? x ? c == null || y == null ? "No comparison available" : `${(c - y).toLocaleString(void 0, { maximumFractionDigits: 1, signDisplay: "always" })} percentage points vs previous period` : `${ce(c, y ?? null)} vs previous period` : "Selected period"}</span></div>`).join("")}</div>
      <div class="card analytics-main analytics-purchases"><h3>New and returning purchases</h3>
        <div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Purchase type</th><th>Buyers</th><th>Orders</th><th>Order amount</th></tr></thead><tbody>
          <tr><td>First purchase</td><td>${t.newCustomers}</td><td>${t.firstOrders}</td><td>${m(this.amount(t.firstOrderAmount))}</td></tr>
          <tr><td>Repeat purchase</td><td>${t.returningCustomers}</td><td>${t.repeatOrders}</td><td>${m(this.amount(t.repeatOrderAmount))}</td></tr>
        </tbody></table></div>
      </div>
      <div class="analytics-insight-grid">
        <div class="card analytics-main"><div class="analytics-chart-heading"><h3>Orders by weekday</h3><span>By creation day</span></div><div class="analytics-time-bars">${i(s, n)}</div></div>
        ${(u = this.hasAttribute) != null && u.call(this, "classic") ? `<div class="card analytics-main"><h3>Orders by hour</h3><p class="analytics-muted">Recorded creation time, 00–23. No timezone conversion. Totals are counts, not averages per hour.</p><div class="analytics-time-bars analytics-hour-bars">${i(d.map((l, c) => String(c).padStart(2, "0") + ":00"), d)}</div></div>` : `<div class="card analytics-main"><div class="analytics-chart-heading"><h3>Orders by hour</h3><span>${Math.max(...d) > 0 ? `Peak ${String(d.indexOf(Math.max(...d))).padStart(2, "0")}:00` : "No orders"}</span></div><div class="analytics-hour-columns">${d.map((l, c) => `<div class="analytics-hour-column" tabindex="0" data-tooltip="${String(c).padStart(2, "0")}:00: ${l} orders" aria-label="${String(c).padStart(2, "0")}:00: ${l} orders"><span class="analytics-hour-value">${l || ""}</span><span class="analytics-hour-bar" style="height:${130 * l / Math.max(1, ...d)}px"></span><span class="analytics-hour-label">${c % 3 === 0 ? String(c).padStart(2, "0") : ""}</span></div>`).join("")}</div></div>`}

      </div>`;
  }
  renderTable() {
    return `<div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Period</th><th>Orders</th><th>Order amount</th><th>Average order</th>${this.previousData ? `<th>Previous period</th><th>Previous ${this.label(this.metric).toLowerCase()}</th>` : ""}<th>Details</th></tr></thead><tbody>${this.buckets.map((t, e) => `<tr><td>${m(this.range(t.start, t.end))}</td><td>${t.orders}</td><td>${m(this.amount(t.amount))}</td><td>${m(this.amount(t.average))}</td>${this.previousData ? `<td>${m(this.range(t.previousStart, t.previousEnd))}</td><td>${m(this.metric === "orders" || this.comparisonMoneyAvailable ? this.value(this.previousValue(t)) : "—")}</td>` : ""}<td><button data-orders="${e}">Open orders</button></td></tr>`).join("")}</tbody></table></div>`;
  }
  renderMostSoldProducts() {
    return this.mostSoldProducts.products.length ? `
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell">Product</div><div class="umb-table-cell">Sku</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Total</div></div></div>
        <div class="umb-table-body">
          ${this.mostSoldProducts.products.map((t) => `<div class="umb-table-row"><div class="umb-table-cell">${m(B(t, "title", "productTitle", "name"))}</div><div class="umb-table-cell">${m(B(t, "sku", "productSku"))}</div><div class="umb-table-cell">${m(B(t, "productCount", "quantity", "count"))}</div><div class="umb-table-cell">${m(G(Number(B(t, "total")), this.currency))}</div></div>`).join("")}
        </div>
      </div>
      ${this.mostSoldProducts.totalPages > 1 ? this.renderMostSoldProductsPagination() : ""}
    ` : "<p>No products found.</p>";
  }
  renderMostSoldProductsPagination() {
    return `
      <div class="pagination">
        <ul>
          ${xe(this.pageMostSoldProducts, this.mostSoldProducts.totalPages).map((t) => {
      const e = Number(String(t).replace("...", ""));
      return `<li class="${e === this.pageMostSoldProducts ? "active" : ""}"><button type="button" data-action="set-most-sold-page" data-page="${e}" ${e === this.pageMostSoldProducts ? "disabled" : ""}>${m(t)}</button></li>`;
    }).join("")}
        </ul>
      </div>
    `;
  }
  renderCharts() {
    var d;
    const t = this.querySelector("#analytics-chart");
    if (!t || !this.chartData || this.table) return;
    const e = this.buckets, a = this.chartData.currency || ((d = this.previousData) == null ? void 0 : d.currency), s = a && /^[A-Z]{3}$/.test(a) ? { isoCurrencySymbol: a } : this.currency, n = this.previousData && (this.metric === "orders" || this.comparisonMoneyAvailable) ? { labels: [], points: e.map((i) => ({ x: this.range(i.previousStart, i.previousEnd), y: this.previousValue(i) })) } : void 0;
    _e(t, { labels: [], points: e.map((i) => ({ x: this.range(i.start, i.end), y: i[this.metric] })) }, getComputedStyle(t).getPropertyValue("--ekom-accent").trim() || "#10694a", {
      templateStyle: !this.hasAttribute("classic"),
      formatAxisValue: (i) => i.toLocaleString(void 0, { notation: "compact", maximumFractionDigits: 1 }).replace("K", "k"),
      formatLabel: (i) => (/* @__PURE__ */ new Date(`${e[i].start}T00:00:00Z`)).toLocaleDateString("en-GB", { timeZone: "UTC", month: "short", ...this.grouping === "month" ? {} : { day: "numeric" } }),
      currency: this.metric === "orders" ? void 0 : s,
      integer: this.metric === "orders",
      comparison: n,
      formatValue: (i) => this.value(i),
      columnHover: !0,
      onSelect: (i) => this.openOrders(i)
    });
  }
  changedFilters() {
    this.pageMostSoldProducts = 1, this.dispatchEvent(new CustomEvent("analytics-filters-changed", { bubbles: !0, detail: { ...p.filters } })), this.loadAnalytics();
  }
  openOrders(t) {
    const e = this.buckets[t];
    if (!e || !this.chartFilters) return;
    const a = { ...this.chartFilters, dateFrom: e.start, dateTo: e.end };
    for (const s of ["query", "paymentProvider", "shippingProvider", "couponCode", "giftcardCode", "productSku", "trackingSource", "trackingMedium", "trackingCampaign", "trackingTerm", "trackingContent", "trackingClickId"]) a[s] = "";
    if (Object.assign(p.filters, a), this.hasAttribute("classic")) this.dispatchEvent(new CustomEvent("analytics-open-orders", { bubbles: !0, detail: a }));
    else {
      const s = new URLSearchParams({ analyticsDrill: "1", store: a.store, currency: a.currency || "", start: a.dateFrom, end: a.dateTo, orderStatus: a.orderStatus }), n = document.createElement("a");
      n.href = location.pathname.replace(/\/analytics\/?$/, "/orders") + "?" + s, this.appendChild(n), n.click(), n.remove();
    }
  }
  exportCsv() {
    var a, s, n;
    const t = [["From", "To", "Orders", "Order amount", "Average order", "Currency", "Previous from", "Previous to", "Previous orders", "Previous amount", "Previous average"]];
    for (const d of this.buckets) t.push([d.start, d.end, String(d.orders), (a = this.chartData) != null && a.mixedCurrencies ? "" : String(d.amount), (s = this.chartData) != null && s.mixedCurrencies || d.average == null ? "" : String(d.average), ((n = this.chartData) == null ? void 0 : n.currency) || "", d.previousStart || "", d.previousEnd || "", d.previousOrders == null ? "" : String(d.previousOrders), this.comparisonMoneyAvailable ? String(d.previousAmount ?? "") : "", this.comparisonMoneyAvailable ? String(d.previousAverage ?? "") : ""]);
    const e = t.map((d) => d.map((i) => '"' + i.replaceAll('"', '""') + '"').join(",")).join(`\r
`);
    Se(new Blob(["\uFEFF", e], { type: "text/csv;charset=utf-8" }), "analytics.csv");
  }
  bindEvents() {
    var t, e, a, s, n, d, i, f, w;
    this.hasAttribute("classic") || Ee(this, () => {
      this.datePreset = void 0, this.changedFilters();
    }), this.querySelectorAll("[data-field]").forEach((u) => u.addEventListener("change", () => {
      const l = u.dataset.field;
      (l === "dateFrom" || l === "dateTo") && (this.datePreset = void 0), p.filters[l] = u.value, this.changedFilters();
    })), (t = this.querySelector("[data-preset]")) == null || t.addEventListener("change", (u) => {
      const l = u.target.value;
      this.datePreset = l, l ? (Object.assign(p.filters, he(l)), this.changedFilters()) : this.render();
    }), (e = this.querySelector("[data-compare]")) == null || e.addEventListener("change", () => {
      this.compare = !this.compare, this.loadCharts();
    }), (a = this.querySelector("[data-grouping]")) == null || a.addEventListener("change", (u) => {
      this.grouping = u.target.value, this.render();
    }), this.querySelectorAll("[data-metric]").forEach((u) => u.addEventListener("click", () => {
      this.metric = u.dataset.metric, this.render();
    })), (s = this.querySelector("[data-table]")) == null || s.addEventListener("click", () => {
      this.table = !this.table, this.render();
    }), (n = this.querySelector("[data-csv]")) == null || n.addEventListener("click", () => this.exportCsv()), this.querySelectorAll("[data-poll-seconds]").forEach((u) => u.addEventListener("click", () => this.setPolling(Number(u.dataset.pollSeconds)))), (d = this.querySelector(".ekom-polling")) == null || d.addEventListener("keydown", (u) => {
      var c;
      if (u.key !== "Escape") return;
      const l = u.currentTarget;
      l.open = !1, (c = l.querySelector("summary")) == null || c.focus(), u.stopPropagation();
    }), (i = this.querySelector("[data-refresh]")) == null || i.addEventListener("click", () => void this.loadAnalytics()), this.querySelectorAll("[data-orders]").forEach((u) => u.addEventListener("click", () => this.openOrders(Number(u.dataset.orders)))), (f = this.querySelector("[data-products]")) == null || f.addEventListener("click", () => {
      var u;
      this.productsVisible = !this.productsVisible, this.productsVisible ? this.loadMostSoldProducts() : (this.productRequest++, (u = this.productAbort) == null || u.abort(), this.render());
    }), (w = this.querySelector("[data-product-retry]")) == null || w.addEventListener("click", () => void this.loadMostSoldProducts()), this.querySelectorAll('[data-action="set-most-sold-page"]').forEach((u) => u.addEventListener("click", () => {
      this.pageMostSoldProducts = Number(u.dataset.page), this.loadMostSoldProducts();
    }));
  }
}
function B(o, ...r) {
  for (const t of r) if (o[t] != null) return o[t];
  return "";
}
function Q(o, r) {
  return o instanceof Error ? o.message : r;
}
customElements.define("ekom-analytics-section-view", ze);
export {
  ze as EkomAnalyticsSectionViewElement,
  ze as default
};
