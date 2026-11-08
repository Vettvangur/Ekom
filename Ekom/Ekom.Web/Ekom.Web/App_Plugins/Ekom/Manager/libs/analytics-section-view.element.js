var te = Object.defineProperty;
var re = (i, r, e) => r in i ? te(i, r, { enumerable: !0, configurable: !0, writable: !0, value: e }) : i[r] = e;
var p = (i, r, e) => re(i, typeof r != "symbol" ? r + "" : r, e);
const ae = (i) => class extends i {
  connectedCallback() {
  }
  disconnectedCallback() {
  }
}, K = /* @__PURE__ */ new Date(), oe = new Date(K.getFullYear(), 0, 1), b = {
  filters: {
    dateFrom: Z(oe),
    dateTo: Z(K),
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
class P {
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
      throw await P.createError(t, "Order action failed.");
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
      throw await P.createError(t, "Error removing order line.");
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
    const a = await fetch(r + V(e), {
      credentials: "same-origin",
      signal: t,
      headers: { Accept: "application/json" }
    });
    if (!a.ok)
      throw await P.createError(a, "Request failed.");
    return a.json();
  }
  async getBlob(r, e) {
    const t = await fetch(r + V(e), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!t.ok)
      throw await P.createError(t, "Request failed.");
    return t.blob();
  }
  async postJson(r, e) {
    const t = await fetch(r + V(e), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await P.createError(t, "Request failed.");
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
      throw await P.createError(t, "Request failed.");
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
function V(i) {
  if (!i)
    return "";
  const r = new URLSearchParams();
  for (const [t, a] of Object.entries(i))
    r.set(t, a == null ? "" : String(a));
  const e = r.toString();
  return e ? `?${e}` : "";
}
function Z(i) {
  const r = String(i.getMonth() + 1).padStart(2, "0"), e = String(i.getDate()).padStart(2, "0");
  return `${i.getFullYear()}-${r}-${e}`;
}
function m(i) {
  return String(i ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
function Q(i) {
  return i.enumValue || i.value || "";
}
function ie(i, r) {
  const t = [], a = Math.max(r || 1, 1);
  let s;
  i <= Math.floor(5 / 2) ? s = 1 : i + Math.floor(5 / 2) >= a ? s = Math.max(a - 5 + 1, 1) : s = i - Math.floor(5 / 2);
  for (let n = 0; n < 5; n += 1) {
    const d = s + n;
    d <= a && t.push(d);
  }
  return t.length && Number(t[t.length - 1]) < a && t.push(`...${a}`), t.length && Number(t[0]) > 1 && t.unshift("...1"), t;
}
const se = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`;
function ne(i, r) {
  const e = URL.createObjectURL(i), t = document.createElement("a");
  t.href = e, t.download = r, t.addEventListener("click", (a) => a.stopPropagation()), document.body.append(t), t.click(), t.remove(), URL.revokeObjectURL(e);
}
const de = `
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

  ${se}
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
function z(i, r) {
  const e = r == null ? void 0 : r.isoCurrencySymbol, t = (r == null ? void 0 : r.currencyDecimalDigits) ?? 2;
  return new Intl.NumberFormat(void 0, {
    minimumFractionDigits: t,
    maximumFractionDigits: t,
    ...e && /^[A-Z]{3}$/.test(e) ? { style: "currency", currency: e, currencyDisplay: "code" } : {}
  }).format(i);
}
function le(i, r, e, t = {}) {
  var W;
  const a = i.getBoundingClientRect(), s = window.devicePixelRatio || 1, n = a.width || 300, d = a.height || 320;
  i.width = n * s, i.height = d * s;
  const o = i.getContext("2d");
  if (!o) return;
  o.scale(s, s), o.font = "12px sans-serif";
  const w = r.points || [], l = w.map((c) => c.y == null ? null : Number(c.y)), v = ((W = t.comparison) == null ? void 0 : W.points) || [], U = [...l, ...v.map((c) => c.y)].filter((c) => c != null), x = Math.min(0, ...U), R = Math.max(1, ...U), O = t.integer ? Math.ceil(R / 4) * 4 : R, C = (c) => t.formatValue ? t.formatValue(c) : t.currency ? z(c, t.currency) : c.toLocaleString(void 0, { maximumFractionDigits: t.integer ? 0 : 2 }), M = Array.from({ length: 5 }, (c, g) => O - (O - x) * g / 4), S = Math.max(48, ...M.map((c) => o.measureText(C(c)).width + 12)), A = n - 16, F = 20, $ = d - 52, f = w.map((c, g) => {
    var k;
    return {
      x: w.length > 1 ? S + (A - S) * g / (w.length - 1) : (S + A) / 2,
      y: l[g] == null ? null : $ - (l[g] - x) / (O - x) * ($ - F),
      date: c.x || ((k = r.labels) == null ? void 0 : k[g]) || "",
      value: l[g]
    };
  }), B = (c = -1) => {
    o.clearRect(0, 0, n, d), o.font = "12px sans-serif", o.lineWidth = 1, o.textAlign = "right", M.forEach((u, h) => {
      const y = F + ($ - F) * h / 4;
      o.fillStyle = "#555", o.fillText(C(u), S - 8, y + 4), o.strokeStyle = "#e5e5e5", o.beginPath(), o.moveTo(S, y), o.lineTo(A, y), o.stroke();
    });
    const g = Math.max(90, ...f.map((u) => o.measureText(u.date).width + 16)), k = Math.max(2, Math.floor((A - S) / g)), j = new Set(Array.from({ length: Math.min(k, f.length) }, (u, h) => Math.round(h * (f.length - 1) / Math.max(1, Math.min(k, f.length) - 1))));
    o.fillStyle = "#555", f.forEach((u, h) => {
      j.has(h) && (o.textAlign = f.length === 1 ? "center" : h === 0 ? "left" : h === f.length - 1 ? "right" : "center", o.fillText(u.date, u.x, $ + 24));
    });
    const q = (u, h) => {
      var L, E;
      o.strokeStyle = h ? "#9298a5" : e, o.lineWidth = 2, (L = o.setLineDash) == null || L.call(o, h ? [6, 4] : []), o.beginPath();
      let y = !1;
      u.forEach((D) => {
        if (D.y == null) {
          y = !1;
          return;
        }
        y ? o.lineTo(D.x, D.y) : o.moveTo(D.x, D.y), y = !0;
      }), o.stroke(), (E = o.setLineDash) == null || E.call(o, []);
    };
    v.length && q(f.map((u, h) => {
      var y;
      return {
        x: u.x,
        y: ((y = v[h]) == null ? void 0 : y.y) == null ? null : $ - (Number(v[h].y) - x) / (O - x) * ($ - F)
      };
    }), !0), q(f, !1), o.fillStyle = e, f.forEach((u, h) => {
      u.y != null && (o.beginPath(), o.arc(u.x, u.y, h === c ? 5 : 3, 0, Math.PI * 2), o.fill());
    }), f.length || (o.textAlign = "center", o.fillText("No data for this period", (S + A) / 2, d / 2));
    const _ = f[c];
    if (_) {
      const u = [_.date, _.value == null ? "—" : C(_.value)];
      if (v[c]) {
        const T = v[c];
        u.push(`Previous: ${T.x || ""}`, T.y == null ? "—" : C(T.y));
      }
      const h = Math.max(...u.map((T) => o.measureText(T).width)) + 20, y = Math.max(0, Math.min(n - h, _.x - h / 2)), L = u.length * 18 + 12, E = _.y ?? $, D = Math.max(0, Math.min(d - L, E > L + 20 ? E - L - 12 : E + 12));
      o.fillStyle = "#222", o.fillRect(y, D, h, L), o.fillStyle = "#fff", o.textAlign = "left", u.forEach((T, ee) => o.fillText(T, y + 10, D + 19 + ee * 18));
    }
  }, Y = (c) => {
    const g = i.getBoundingClientRect(), k = (c.clientX - g.left) * n / g.width, j = (c.clientY - g.top) * d / g.height;
    if (t.columnHover)
      return !f.length || k < S || k > A || j < F || j > $ ? -1 : f.length === 1 ? 0 : Math.round((k - S) / (A - S) * (f.length - 1));
    let q = -1, _ = 16;
    return f.forEach((u, h) => {
      if (u.y == null) return;
      const y = Math.hypot(u.x - k, u.y - j);
      y < _ && (q = h, _ = y);
    }), q;
  };
  i.onmousemove = (c) => {
    const g = Y(c);
    i.style.cursor = g >= 0 ? "pointer" : "default", B(g);
  }, i.onclick = t.onSelect ? (c) => {
    var k;
    const g = Y(c);
    g >= 0 && ((k = t.onSelect) == null || k.call(t, g));
  } : null, i.onmouseleave = () => {
    i.style.cursor = "default", B();
  }, B();
}
const J = 864e5, N = (i) => i.toISOString().slice(0, 10), G = (i) => /* @__PURE__ */ new Date(`${i}T00:00:00Z`);
function ce(i, r) {
  const e = G(i), t = G(r), a = Math.round((t.getTime() - e.getTime()) / J) + 1;
  return { dateFrom: N(new Date(e.getTime() - a * J)), dateTo: N(new Date(e.getTime() - J)) };
}
function ue(i, r = /* @__PURE__ */ new Date()) {
  const e = new Date(Date.UTC(r.getFullYear(), r.getMonth(), r.getDate())), t = new Date(e);
  return i === "7" && t.setUTCDate(t.getUTCDate() - 6), i === "30" && t.setUTCDate(t.getUTCDate() - 29), i === "month" && t.setUTCDate(1), i === "year" && (t.setUTCMonth(0), t.setUTCDate(1)), { dateFrom: N(t), dateTo: N(e) };
}
function pe(i, r, e) {
  var n, d, o, w, l;
  const t = [], a = i.ordersChart.points;
  let s = 0;
  for (; s < a.length; ) {
    const v = s, U = a[s].x, x = G(U);
    e === "month" ? (x.setUTCDate(1), x.setUTCMonth(x.getUTCMonth() + 1)) : e === "week" ? x.setUTCDate(x.getUTCDate() + (7 - (x.getUTCDay() + 6) % 7)) : x.setUTCDate(x.getUTCDate() + 1);
    let R = 0, O = 0, C = 0, M = 0;
    do
      R += Number(((n = i.revenueChart.points[s]) == null ? void 0 : n.y) || 0), O += Number(a[s].y || 0), C += Number(((d = r == null ? void 0 : r.revenueChart.points[s]) == null ? void 0 : d.y) || 0), M += Number(((o = r == null ? void 0 : r.ordersChart.points[s]) == null ? void 0 : o.y) || 0), s++;
    while (s < a.length && G(a[s].x).getTime() < x.getTime());
    t.push({
      start: U,
      end: a[s - 1].x,
      amount: R,
      orders: O,
      average: O ? R / O : null,
      ...r ? {
        previousStart: (w = r.ordersChart.points[v]) == null ? void 0 : w.x,
        previousEnd: (l = r.ordersChart.points[s - 1]) == null ? void 0 : l.x,
        previousAmount: C,
        previousOrders: M,
        previousAverage: M ? C / M : null
      } : {}
    });
  }
  return t;
}
function X(i) {
  const r = i.revenueChart.points.reduce((t, a) => t + Number(a.y || 0), 0), e = i.ordersChart.points.reduce((t, a) => t + Number(a.y || 0), 0);
  return { amount: r, orders: e, average: e ? r / e : null };
}
function me(i, r) {
  if (i == null || r == null) return "No comparison available";
  if (r === 0) return i === 0 ? "No change" : "No prior value";
  const e = (i - r) / Math.abs(r) * 100;
  return `${e > 0 ? "+" : ""}${e.toLocaleString(void 0, { maximumFractionDigits: 1 })}%`;
}
class he extends ae(HTMLElement) {
  constructor() {
    super(...arguments);
    p(this, "api", new P());
    p(this, "loading", !0);
    p(this, "loadingMostSoldProducts", !1);
    p(this, "error", "");
    p(this, "productError", "");
    p(this, "chartData");
    p(this, "previousData");
    p(this, "chartFilters");
    p(this, "mostSoldProducts", { products: [], count: 0, totalPages: 0, page: 1 });
    p(this, "pageMostSoldProducts", 1);
    p(this, "resizeObserver");
    p(this, "chartRequest", 0);
    p(this, "productRequest", 0);
    p(this, "chartAbort");
    p(this, "productAbort");
    p(this, "disposed", !1);
    p(this, "productsVisible", !1);
    p(this, "metric", "amount");
    p(this, "grouping", "day");
    p(this, "compare", !0);
    p(this, "table", !1);
  }
  connectedCallback() {
    if (super.connectedCallback(), this.disposed = !1, this.hasAttribute("classic"))
      for (const e of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const t = this.getAttribute(e.toLowerCase());
        t && (b.filters[e] = t);
      }
    this.render(), this.resizeObserver = new ResizeObserver(() => this.renderCharts()), this.resizeObserver.observe(this), this.initialize();
  }
  disconnectedCallback() {
    var e, t, a;
    this.disposed = !0, this.chartRequest++, this.productRequest++, (e = this.chartAbort) == null || e.abort(), (t = this.productAbort) == null || t.abort(), (a = this.resizeObserver) == null || a.disconnect(), this.resizeObserver = void 0, super.disconnectedCallback();
  }
  get currency() {
    var e;
    return (e = b.stores.find((t) => {
      var a;
      return t.alias === (((a = this.chartFilters) == null ? void 0 : a.store) || b.filters.store);
    })) == null ? void 0 : e.currency;
  }
  async initialize() {
    var e;
    try {
      const [t, a] = await Promise.all([this.api.statusList(), this.api.stores()]);
      if (this.disposed) return;
      b.statusList = t || [], b.stores = a || [], b.stores.some((s) => s.alias === b.filters.store) || (b.filters.store = ((e = b.stores[0]) == null ? void 0 : e.alias) || ""), await this.loadAnalytics();
    } catch (t) {
      if (this.disposed) return;
      this.error = H(t, "Unable to load Analytics."), this.loading = !1, this.render();
    }
  }
  async loadAnalytics() {
    await Promise.all([this.loadCharts(), this.productsVisible ? this.loadMostSoldProducts() : Promise.resolve()]);
  }
  async loadCharts() {
    var s;
    const e = ++this.chartRequest;
    (s = this.chartAbort) == null || s.abort();
    const t = this.chartAbort = new AbortController(), a = { ...b.filters };
    if (this.chartData = void 0, this.previousData = void 0, this.error = "", this.loading = !!a.store, this.render(), !!a.store)
      try {
        const n = this.compare ? { ...a, ...ce(a.dateFrom, a.dateTo) } : void 0, [d, o] = await Promise.all([
          this.api.charts(a, t.signal),
          n ? this.api.charts(n, t.signal) : Promise.resolve(void 0)
        ]);
        if (e !== this.chartRequest || this.disposed) return;
        this.chartData = d, this.previousData = o, this.chartFilters = a, d.mixedCurrencies && (this.metric = "orders");
      } catch (n) {
        if (e !== this.chartRequest || this.disposed) return;
        this.error = H(n, "Unable to load charts.");
      } finally {
        e === this.chartRequest && !this.disposed && (this.loading = !1, this.render());
      }
  }
  async loadMostSoldProducts() {
    var s;
    const e = ++this.productRequest;
    (s = this.productAbort) == null || s.abort();
    const t = this.productAbort = new AbortController(), a = { ...b.filters };
    if (this.mostSoldProducts = { products: [], count: 0, totalPages: 0, page: this.pageMostSoldProducts }, this.productError = "", this.loadingMostSoldProducts = !!a.store, this.render(), !!a.store)
      try {
        const n = await this.api.mostSoldProducts(a, this.pageMostSoldProducts, 20, t.signal);
        if (e !== this.productRequest || this.disposed) return;
        this.mostSoldProducts = { ...n, products: n.products || [] }, this.pageMostSoldProducts = n.page || this.pageMostSoldProducts;
      } catch (n) {
        e === this.productRequest && !this.disposed && (this.productError = H(n, "Unable to load products."));
      } finally {
        e === this.productRequest && !this.disposed && (this.loadingMostSoldProducts = !1, this.render());
      }
  }
  get buckets() {
    return this.chartData ? pe(this.chartData, this.previousData, this.grouping) : [];
  }
  get comparisonMoneyAvailable() {
    var e, t;
    return !!this.previousData && !((e = this.chartData) != null && e.mixedCurrencies) && !this.previousData.mixedCurrencies && (!((t = this.chartData) != null && t.currency) || !this.previousData.currency || this.chartData.currency === this.previousData.currency);
  }
  amount(e) {
    var a, s, n, d, o;
    if (e == null || (a = this.chartData) != null && a.mixedCurrencies) return "—";
    const t = ((s = this.chartData) == null ? void 0 : s.currency) || ((n = this.previousData) == null ? void 0 : n.currency);
    return !t || t === ((d = this.currency) == null ? void 0 : d.currencyValue) || t === ((o = this.currency) == null ? void 0 : o.isoCurrencySymbol) ? z(e, this.currency) : /^[A-Z]{3}$/.test(t) ? z(e, { isoCurrencySymbol: t }) : `${e.toLocaleString(void 0, { maximumFractionDigits: 2 })} ${t}`;
  }
  label(e) {
    return e === "orders" ? "Orders" : e === "average" ? "Average order value" : "Order amount";
  }
  value(e, t = this.metric) {
    return t === "orders" ? e == null ? "—" : e.toLocaleString() : this.amount(e);
  }
  previousValue(e) {
    return this.metric === "orders" ? e.previousOrders : this.metric === "average" ? e.previousAverage : e.previousAmount;
  }
  range(e, t) {
    return e === t ? e || "" : `${e || ""} – ${t || ""}`;
  }
  render() {
    const e = b.filters;
    this.innerHTML = `<style>${de}
      ekom-analytics-section-view { display:block }
      .analytics-toolbar {display:flex;flex-wrap:wrap;gap:12px;align-items:end;margin-bottom:18px}
      .analytics-toolbar label {display:grid;gap:5px}
      .analytics-toolbar input,.analytics-toolbar select,.analytics-toolbar button,.analytics-table button {font:inherit;padding:8px 12px;border:1px solid #dfe2e8;border-radius:6px;background:var(--uui-color-surface,#fff);color:inherit}
      .analytics-toolbar button {cursor:pointer}
      .analytics-toolbar button[aria-pressed=true] {background:#e7f1ed;border-color:#26795c}
      .analytics-summary {display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:16px;margin-bottom:20px}
      .analytics-summary .card {padding:20px}
      .analytics-summary strong {display:block;font-size:27px;margin:10px 0}
      .analytics-muted {color:#666f80;font-size:12px}
      .analytics-table-wrap {overflow:auto;max-height:520px}
      .analytics-table {width:100%;border-collapse:collapse}
      .analytics-table th,.analytics-table td {text-align:left;padding:12px;border-bottom:1px solid #e4e7ec;white-space:nowrap}
      .analytics-table th {position:sticky;top:0;background:var(--uui-color-surface,#fff)}
      .analytics-main {padding:22px}
    </style><section class="ekmManager"><div class="ekmManager__body">
      <div class="analytics-toolbar">
        <label>Store<select data-field="store">${b.stores.map((t) => `<option value="${m(t.alias)}" ${t.alias === e.store ? "selected" : ""}>${m(t.title)}</option>`).join("")}</select></label>
        <label>Order status<select data-field="orderStatus"><option value="CompletedOrders" ${e.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option><option value="AllOrders" ${e.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>${b.statusList.map((t) => `<option value="${m(Q(t))}" ${Q(t) === e.orderStatus ? "selected" : ""}>${m(t.label)}</option>`).join("")}</select></label>
        <label>Date From<input type="date" data-field="dateFrom" value="${m(e.dateFrom)}"></label>
        <label>Date To<input type="date" data-field="dateTo" value="${m(e.dateTo)}"></label>
        <label>Date preset<select data-preset><option value="">Custom range</option>${[["today", "Today"], ["7", "Last 7 days"], ["30", "Last 30 days"], ["month", "This month"], ["year", "This year"]].map(([t, a]) => `<option value="${t}">${a}</option>`).join("")}</select></label>
        <label><span>Comparison</span><span><input type="checkbox" data-compare ${this.compare ? "checked" : ""}> Previous period</span></label>
        <button data-refresh>Refresh</button>
      </div>
      ${this.error ? `<p role="alert">${m(this.error)}</p>` : ""}
      ${this.loading ? '<p role="status">Loading analytics…</p>' : e.store ? "" : "<p>No accessible stores.</p>"}
      ${this.chartData ? this.renderAnalytics() : ""}
      <div class="card analytics-main"><div class="analytics-toolbar"><h3>Most Sold Products</h3><button data-products>${this.productsVisible ? "Hide products" : "Load product breakdown"}</button></div>
        <p class="analytics-muted">Loads order-line details on demand. Totals include VAT and discounts, excluding shipping and payment fees.</p>
        ${this.productsVisible ? `${this.productError ? `<p role="alert">${m(this.productError)}</p><button data-product-retry>Retry products</button>` : ""}${this.loadingMostSoldProducts ? '<p role="status">Loading products…</p>' : this.renderMostSoldProducts()}` : ""}
      </div>
    </div></section>`, this.bindEvents(), this.renderCharts();
  }
  renderAnalytics() {
    var a, s, n, d;
    const e = X(this.chartData), t = this.previousData ? X(this.previousData) : void 0;
    return `<div class="analytics-summary">${["amount", "orders", "average"].map((o) => `<div class="card"><span>${this.label(o)}</span><strong>${m(this.value(e[o], o))}</strong><span class="analytics-muted">${t && (o === "orders" || this.comparisonMoneyAvailable) ? m(me(e[o], t[o])) + " vs previous period" : this.compare ? "No monetary comparison available" : "Selected period"}</span></div>`).join("")}</div>
      <p class="analytics-muted">Order amount is the recorded charged amount, after gift cards; it is not refund-adjusted revenue. Dates use ${this.chartData.dateBasis === "PaidDate" ? "payment date" : "order creation date"}, matching Orders.</p>
      ${this.chartData.mixedCurrencies ? '<p role="status">This period contains multiple currencies. Monetary totals are hidden to avoid combining different currencies.</p>' : ""}
      ${this.previousData && !this.comparisonMoneyAvailable ? '<p class="analytics-muted">Monetary comparison is unavailable across different or mixed currencies.</p>' : ""}
      <div class="card analytics-main"><div class="analytics-toolbar">
        ${["amount", "orders", "average"].map((o) => `<button data-metric="${o}" aria-pressed="${this.metric === o}" ${o !== "orders" && this.chartData.mixedCurrencies ? "disabled" : ""}>${this.label(o)}</button>`).join("")}
        <label>Group by<select data-grouping>${[["day", "Daily"], ["week", "Weekly"], ["month", "Monthly"]].map(([o, w]) => `<option value="${o}" ${this.grouping === o ? "selected" : ""}>${w}</option>`).join("")}</select></label>
        <button data-table aria-pressed="${this.table}">${this.table ? "Show chart" : "Show data table"}</button><button data-csv>Download CSV</button>
      </div>
      <p class="analytics-muted">${this.previousData ? `Solid: ${m(this.range((a = this.chartFilters) == null ? void 0 : a.dateFrom, (s = this.chartFilters) == null ? void 0 : s.dateTo))}. Dashed: ${m(this.range((n = this.previousData.ordersChart.points[0]) == null ? void 0 : n.x, (d = this.previousData.ordersChart.points.at(-1)) == null ? void 0 : d.x))}. Comparison buckets cover the same number of days.` : "Selected period."} Click a bucket to open its orders.</p>
      ${this.table ? this.renderTable() : `<div style="height:360px"><canvas id="analytics-chart" style="width:100%;height:100%" role="img" aria-label="${this.label(this.metric)} over time. Use Show data table for exact values and order links."></canvas></div>`}
      </div>`;
  }
  renderTable() {
    return `<div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Period</th><th>Orders</th><th>Order amount</th><th>Average order</th>${this.previousData ? `<th>Previous period</th><th>Previous ${this.label(this.metric).toLowerCase()}</th>` : ""}<th>Details</th></tr></thead><tbody>${this.buckets.map((e, t) => `<tr><td>${m(this.range(e.start, e.end))}</td><td>${e.orders}</td><td>${m(this.amount(e.amount))}</td><td>${m(this.amount(e.average))}</td>${this.previousData ? `<td>${m(this.range(e.previousStart, e.previousEnd))}</td><td>${m(this.metric === "orders" || this.comparisonMoneyAvailable ? this.value(this.previousValue(e)) : "—")}</td>` : ""}<td><button data-orders="${t}">Open orders</button></td></tr>`).join("")}</tbody></table></div>`;
  }
  renderMostSoldProducts() {
    return this.mostSoldProducts.products.length ? `
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell">Product</div><div class="umb-table-cell">Sku</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Total</div></div></div>
        <div class="umb-table-body">
          ${this.mostSoldProducts.products.map((e) => `<div class="umb-table-row"><div class="umb-table-cell">${m(I(e, "title", "productTitle", "name"))}</div><div class="umb-table-cell">${m(I(e, "sku", "productSku"))}</div><div class="umb-table-cell">${m(I(e, "productCount", "quantity", "count"))}</div><div class="umb-table-cell">${m(z(Number(I(e, "total")), this.currency))}</div></div>`).join("")}
        </div>
      </div>
      ${this.mostSoldProducts.totalPages > 1 ? this.renderMostSoldProductsPagination() : ""}
    ` : "<p>No products found.</p>";
  }
  renderMostSoldProductsPagination() {
    return `
      <div class="pagination">
        <ul>
          ${ie(this.pageMostSoldProducts, this.mostSoldProducts.totalPages).map((e) => {
      const t = Number(String(e).replace("...", ""));
      return `<li class="${t === this.pageMostSoldProducts ? "active" : ""}"><button type="button" data-action="set-most-sold-page" data-page="${t}" ${t === this.pageMostSoldProducts ? "disabled" : ""}>${m(e)}</button></li>`;
    }).join("")}
        </ul>
      </div>
    `;
  }
  renderCharts() {
    var d;
    const e = this.querySelector("#analytics-chart");
    if (!e || !this.chartData || this.table) return;
    const t = this.buckets, a = this.chartData.currency || ((d = this.previousData) == null ? void 0 : d.currency), s = a && /^[A-Z]{3}$/.test(a) ? { isoCurrencySymbol: a } : this.currency, n = this.previousData && (this.metric === "orders" || this.comparisonMoneyAvailable) ? { labels: [], points: t.map((o) => ({ x: this.range(o.previousStart, o.previousEnd), y: this.previousValue(o) })) } : void 0;
    le(e, { labels: [], points: t.map((o) => ({ x: this.range(o.start, o.end), y: o[this.metric] })) }, "#25765c", {
      currency: this.metric === "orders" ? void 0 : s,
      integer: this.metric === "orders",
      comparison: n,
      formatValue: (o) => this.value(o),
      columnHover: !0,
      onSelect: (o) => this.openOrders(o)
    });
  }
  changedFilters() {
    this.pageMostSoldProducts = 1, this.dispatchEvent(new CustomEvent("analytics-filters-changed", { bubbles: !0, detail: { ...b.filters } })), this.loadAnalytics();
  }
  openOrders(e) {
    const t = this.buckets[e];
    if (!t || !this.chartFilters) return;
    const a = { ...this.chartFilters, dateFrom: t.start, dateTo: t.end };
    for (const s of ["query", "paymentProvider", "shippingProvider", "couponCode", "giftcardCode", "productSku", "trackingSource", "trackingMedium", "trackingCampaign", "trackingTerm", "trackingContent", "trackingClickId"]) a[s] = "";
    if (Object.assign(b.filters, a), this.hasAttribute("classic")) this.dispatchEvent(new CustomEvent("analytics-open-orders", { bubbles: !0, detail: a }));
    else {
      const s = new URLSearchParams({ analyticsDrill: "1", store: a.store, start: a.dateFrom, end: a.dateTo, orderStatus: a.orderStatus }), n = document.createElement("a");
      n.href = location.pathname.replace(/\/analytics\/?$/, "/orders") + "?" + s, this.appendChild(n), n.click(), n.remove();
    }
  }
  exportCsv() {
    var a, s, n;
    const e = [["From", "To", "Orders", "Order amount", "Average order", "Currency", "Previous from", "Previous to", "Previous orders", "Previous amount", "Previous average"]];
    for (const d of this.buckets) e.push([d.start, d.end, String(d.orders), (a = this.chartData) != null && a.mixedCurrencies ? "" : String(d.amount), (s = this.chartData) != null && s.mixedCurrencies || d.average == null ? "" : String(d.average), ((n = this.chartData) == null ? void 0 : n.currency) || "", d.previousStart || "", d.previousEnd || "", d.previousOrders == null ? "" : String(d.previousOrders), this.comparisonMoneyAvailable ? String(d.previousAmount ?? "") : "", this.comparisonMoneyAvailable ? String(d.previousAverage ?? "") : ""]);
    const t = e.map((d) => d.map((o) => '"' + o.replaceAll('"', '""') + '"').join(",")).join(`\r
`);
    ne(new Blob(["\uFEFF", t], { type: "text/csv;charset=utf-8" }), "analytics.csv");
  }
  bindEvents() {
    var e, t, a, s, n, d, o, w;
    this.querySelectorAll("[data-field]").forEach((l) => l.addEventListener("change", () => {
      const v = l.dataset.field;
      b.filters[v] = l.value, this.changedFilters();
    })), (e = this.querySelector("[data-preset]")) == null || e.addEventListener("change", (l) => {
      const v = l.target.value;
      v && (Object.assign(b.filters, ue(v)), this.changedFilters());
    }), (t = this.querySelector("[data-compare]")) == null || t.addEventListener("change", () => {
      this.compare = !this.compare, this.loadCharts();
    }), (a = this.querySelector("[data-grouping]")) == null || a.addEventListener("change", (l) => {
      this.grouping = l.target.value, this.render();
    }), this.querySelectorAll("[data-metric]").forEach((l) => l.addEventListener("click", () => {
      this.metric = l.dataset.metric, this.render();
    })), (s = this.querySelector("[data-table]")) == null || s.addEventListener("click", () => {
      this.table = !this.table, this.render();
    }), (n = this.querySelector("[data-csv]")) == null || n.addEventListener("click", () => this.exportCsv()), (d = this.querySelector("[data-refresh]")) == null || d.addEventListener("click", () => void this.loadAnalytics()), this.querySelectorAll("[data-orders]").forEach((l) => l.addEventListener("click", () => this.openOrders(Number(l.dataset.orders)))), (o = this.querySelector("[data-products]")) == null || o.addEventListener("click", () => {
      var l;
      this.productsVisible = !this.productsVisible, this.productsVisible ? this.loadMostSoldProducts() : (this.productRequest++, (l = this.productAbort) == null || l.abort(), this.render());
    }), (w = this.querySelector("[data-product-retry]")) == null || w.addEventListener("click", () => void this.loadMostSoldProducts()), this.querySelectorAll('[data-action="set-most-sold-page"]').forEach((l) => l.addEventListener("click", () => {
      this.pageMostSoldProducts = Number(l.dataset.page), this.loadMostSoldProducts();
    }));
  }
}
function I(i, ...r) {
  for (const e of r) if (i[e] != null) return i[e];
  return "";
}
function H(i, r) {
  return i instanceof Error ? i.message : r;
}
customElements.define("ekom-analytics-section-view", he);
export {
  he as EkomAnalyticsSectionViewElement,
  he as default
};
