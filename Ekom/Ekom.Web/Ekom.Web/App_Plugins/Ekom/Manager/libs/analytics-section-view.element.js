var ae = Object.defineProperty;
var ie = (s, r, e) => r in s ? ae(s, r, { enumerable: !0, configurable: !0, writable: !0, value: e }) : s[r] = e;
var g = (s, r, e) => ie(s, typeof r != "symbol" ? r + "" : r, e);
const se = (s) => class extends s {
  connectedCallback() {
  }
  disconnectedCallback() {
  }
}, ee = /* @__PURE__ */ new Date(), oe = new Date(ee.getFullYear(), 0, 1), x = {
  filters: {
    dateFrom: Z(oe),
    dateTo: Z(ee),
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
class M {
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
      throw await M.createError(t, "Order action failed.");
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
      throw await M.createError(t, "Error removing order line.");
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
      throw await M.createError(a, "Request failed.");
    return a.json();
  }
  async getBlob(r, e) {
    const t = await fetch(r + V(e), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!t.ok)
      throw await M.createError(t, "Request failed.");
    return t.blob();
  }
  async postJson(r, e) {
    const t = await fetch(r + V(e), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await M.createError(t, "Request failed.");
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
      throw await M.createError(t, "Request failed.");
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
function V(s) {
  if (!s)
    return "";
  const r = new URLSearchParams();
  for (const [t, a] of Object.entries(s))
    r.set(t, a == null ? "" : String(a));
  const e = r.toString();
  return e ? `?${e}` : "";
}
function Z(s) {
  const r = String(s.getMonth() + 1).padStart(2, "0"), e = String(s.getDate()).padStart(2, "0");
  return `${s.getFullYear()}-${r}-${e}`;
}
function p(s) {
  return String(s ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
function Q(s) {
  return s.enumValue || s.value || "";
}
function ne(s, r) {
  const t = [], a = Math.max(r || 1, 1);
  let o;
  s <= Math.floor(5 / 2) ? o = 1 : s + Math.floor(5 / 2) >= a ? o = Math.max(a - 5 + 1, 1) : o = s - Math.floor(5 / 2);
  for (let n = 0; n < 5; n += 1) {
    const d = o + n;
    d <= a && t.push(d);
  }
  return t.length && Number(t[t.length - 1]) < a && t.push(`...${a}`), t.length && Number(t[0]) > 1 && t.unshift("...1"), t;
}
const de = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`;
function le(s, r) {
  const e = URL.createObjectURL(s), t = document.createElement("a");
  t.href = e, t.download = r, t.addEventListener("click", (a) => a.stopPropagation()), document.body.append(t), t.click(), t.remove(), URL.revokeObjectURL(e);
}
const ce = `
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

  ${de}
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
function z(s, r) {
  const e = r == null ? void 0 : r.isoCurrencySymbol, t = (r == null ? void 0 : r.currencyDecimalDigits) ?? 2;
  return new Intl.NumberFormat(void 0, {
    minimumFractionDigits: t,
    maximumFractionDigits: t,
    ...e && /^[A-Z]{3}$/.test(e) ? { style: "currency", currency: e, currencyDisplay: "code" } : {}
  }).format(s);
}
function ue(s, r, e, t = {}) {
  var W;
  const a = s.getBoundingClientRect(), o = window.devicePixelRatio || 1, n = a.width || 300, d = a.height || 320;
  s.width = n * o, s.height = d * o;
  const i = s.getContext("2d");
  if (!i) return;
  i.scale(o, o), i.font = "12px sans-serif";
  const S = r.points || [], l = S.map((u) => u.y == null ? null : Number(u.y)), h = ((W = t.comparison) == null ? void 0 : W.points) || [], f = [...l, ...h.map((u) => u.y)].filter((u) => u != null), c = Math.min(0, ...f), v = Math.max(1, ...f), _ = t.integer ? Math.ceil(v / 4) * 4 : v, D = (u) => t.formatValue ? t.formatValue(u) : t.currency ? z(u, t.currency) : u.toLocaleString(void 0, { maximumFractionDigits: t.integer ? 0 : 2 }), T = Array.from({ length: 5 }, (u, y) => _ - (_ - c) * y / 4), C = Math.max(48, ...T.map((u) => i.measureText(D(u)).width + 12)), L = n - 16, U = 20, P = d - 52, k = S.map((u, y) => {
    var O;
    return {
      x: S.length > 1 ? C + (L - C) * y / (S.length - 1) : (C + L) / 2,
      y: l[y] == null ? null : P - (l[y] - c) / (_ - c) * (P - U),
      date: u.x || ((O = r.labels) == null ? void 0 : O[y]) || "",
      value: l[y]
    };
  }), G = (u = -1) => {
    i.clearRect(0, 0, n, d), i.font = "12px sans-serif", i.lineWidth = 1, i.textAlign = "right", T.forEach((m, b) => {
      const w = U + (P - U) * b / 4;
      i.fillStyle = "#555", i.fillText(D(m), C - 8, w + 4), i.strokeStyle = "#e5e5e5", i.beginPath(), i.moveTo(C, w), i.lineTo(L, w), i.stroke();
    });
    const y = Math.max(90, ...k.map((m) => i.measureText(m.date).width + 16)), O = Math.max(2, Math.floor((L - C) / y)), q = new Set(Array.from({ length: Math.min(O, k.length) }, (m, b) => Math.round(b * (k.length - 1) / Math.max(1, Math.min(O, k.length) - 1))));
    i.fillStyle = "#555", k.forEach((m, b) => {
      q.has(b) && (i.textAlign = k.length === 1 ? "center" : b === 0 ? "left" : b === k.length - 1 ? "right" : "center", i.fillText(m.date, m.x, P + 24));
    });
    const j = (m, b) => {
      var R, F;
      i.strokeStyle = b ? "#9298a5" : e, i.lineWidth = 2, (R = i.setLineDash) == null || R.call(i, b ? [6, 4] : []), i.beginPath();
      let w = !1;
      m.forEach((A) => {
        if (A.y == null) {
          w = !1;
          return;
        }
        w ? i.lineTo(A.x, A.y) : i.moveTo(A.x, A.y), w = !0;
      }), i.stroke(), (F = i.setLineDash) == null || F.call(i, []);
    };
    h.length && j(k.map((m, b) => {
      var w;
      return {
        x: m.x,
        y: ((w = h[b]) == null ? void 0 : w.y) == null ? null : P - (Number(h[b].y) - c) / (_ - c) * (P - U)
      };
    }), !0), j(k, !1), i.fillStyle = e, k.forEach((m, b) => {
      m.y != null && (i.beginPath(), i.arc(m.x, m.y, b === u ? 5 : 3, 0, Math.PI * 2), i.fill());
    }), k.length || (i.textAlign = "center", i.fillText("No data for this period", (C + L) / 2, d / 2));
    const $ = k[u];
    if ($) {
      const m = [$.date, $.value == null ? "—" : D($.value)];
      if (h[u]) {
        const E = h[u];
        m.push(`Previous: ${E.x || ""}`, E.y == null ? "—" : D(E.y));
      }
      const b = Math.max(...m.map((E) => i.measureText(E).width)) + 20, w = Math.max(0, Math.min(n - b, $.x - b / 2)), R = m.length * 18 + 12, F = $.y ?? P, A = Math.max(0, Math.min(d - R, F > R + 20 ? F - R - 12 : F + 12));
      i.fillStyle = "#222", i.fillRect(w, A, b, R), i.fillStyle = "#fff", i.textAlign = "left", m.forEach((E, re) => i.fillText(E, w + 10, A + 19 + re * 18));
    }
  }, Y = (u) => {
    const y = s.getBoundingClientRect(), O = (u.clientX - y.left) * n / y.width, q = (u.clientY - y.top) * d / y.height;
    if (t.columnHover)
      return !k.length || O < C || O > L || q < U || q > P ? -1 : k.length === 1 ? 0 : Math.round((O - C) / (L - C) * (k.length - 1));
    let j = -1, $ = 16;
    return k.forEach((m, b) => {
      if (m.y == null) return;
      const w = Math.hypot(m.x - O, m.y - q);
      w < $ && (j = b, $ = w);
    }), j;
  };
  s.onmousemove = (u) => {
    const y = Y(u);
    s.style.cursor = y >= 0 ? "pointer" : "default", G(y);
  }, s.onclick = t.onSelect ? (u) => {
    var O;
    const y = Y(u);
    y >= 0 && ((O = t.onSelect) == null || O.call(t, y));
  } : null, s.onmouseleave = () => {
    s.style.cursor = "default", G();
  }, G();
}
const J = 864e5, N = (s) => s.toISOString().slice(0, 10), B = (s) => /* @__PURE__ */ new Date(`${s}T00:00:00Z`);
function pe(s, r) {
  const e = B(s), t = B(r), a = Math.round((t.getTime() - e.getTime()) / J) + 1;
  return { dateFrom: N(new Date(e.getTime() - a * J)), dateTo: N(new Date(e.getTime() - J)) };
}
function te(s, r = /* @__PURE__ */ new Date()) {
  const e = new Date(Date.UTC(r.getFullYear(), r.getMonth(), r.getDate())), t = new Date(e);
  return s === "7" && t.setUTCDate(t.getUTCDate() - 6), s === "30" && t.setUTCDate(t.getUTCDate() - 29), s === "month" && t.setUTCDate(1), s === "year" && (t.setUTCMonth(0), t.setUTCDate(1)), { dateFrom: N(t), dateTo: N(e) };
}
function me(s, r, e, t = /* @__PURE__ */ new Date()) {
  if (e === "") return "";
  const a = ["today", "7", "30", "month", "year"];
  return e && a.includes(e) && a.unshift(e), a.find((o) => {
    const n = te(o, t);
    return n.dateFrom === s && n.dateTo === r;
  }) || "";
}
function he(s, r, e) {
  var n, d, i, S, l;
  const t = [], a = s.ordersChart.points;
  let o = 0;
  for (; o < a.length; ) {
    const h = o, f = a[o].x, c = B(f);
    e === "month" ? (c.setUTCDate(1), c.setUTCMonth(c.getUTCMonth() + 1)) : e === "week" ? c.setUTCDate(c.getUTCDate() + (7 - (c.getUTCDay() + 6) % 7)) : c.setUTCDate(c.getUTCDate() + 1);
    let v = 0, _ = 0, D = 0, T = 0;
    do
      v += Number(((n = s.revenueChart.points[o]) == null ? void 0 : n.y) || 0), _ += Number(a[o].y || 0), D += Number(((d = r == null ? void 0 : r.revenueChart.points[o]) == null ? void 0 : d.y) || 0), T += Number(((i = r == null ? void 0 : r.ordersChart.points[o]) == null ? void 0 : i.y) || 0), o++;
    while (o < a.length && B(a[o].x).getTime() < c.getTime());
    t.push({
      start: f,
      end: a[o - 1].x,
      amount: v,
      orders: _,
      average: _ ? v / _ : null,
      ...r ? {
        previousStart: (S = r.ordersChart.points[h]) == null ? void 0 : S.x,
        previousEnd: (l = r.ordersChart.points[o - 1]) == null ? void 0 : l.x,
        previousAmount: D,
        previousOrders: T,
        previousAverage: T ? D / T : null
      } : {}
    });
  }
  return t;
}
function X(s) {
  const r = s.revenueChart.points.reduce((t, a) => t + Number(a.y || 0), 0), e = s.ordersChart.points.reduce((t, a) => t + Number(a.y || 0), 0);
  return { amount: r, orders: e, average: e ? r / e : null };
}
function K(s, r) {
  if (s == null || r == null) return "No comparison available";
  if (r === 0) return s === 0 ? "No change" : "No prior value";
  const e = (s - r) / Math.abs(r) * 100;
  return `${e > 0 ? "+" : ""}${e.toLocaleString(void 0, { maximumFractionDigits: 1 })}%`;
}
class ge extends se(HTMLElement) {
  constructor() {
    super(...arguments);
    g(this, "api", new M());
    g(this, "loading", !0);
    g(this, "loadingMostSoldProducts", !1);
    g(this, "error", "");
    g(this, "productError", "");
    g(this, "chartData");
    g(this, "previousData");
    g(this, "chartFilters");
    g(this, "mostSoldProducts", { products: [], count: 0, totalPages: 0, page: 1 });
    g(this, "pageMostSoldProducts", 1);
    g(this, "resizeObserver");
    g(this, "chartRequest", 0);
    g(this, "productRequest", 0);
    g(this, "chartAbort");
    g(this, "productAbort");
    g(this, "disposed", !1);
    g(this, "productsVisible", !1);
    g(this, "metric", "amount");
    g(this, "grouping", "day");
    g(this, "compare", !0);
    g(this, "table", !1);
    g(this, "datePreset");
  }
  connectedCallback() {
    if (super.connectedCallback(), this.disposed = !1, this.hasAttribute("classic"))
      for (const e of ["store", "dateFrom", "dateTo", "orderStatus"]) {
        const t = this.getAttribute(e.toLowerCase());
        t && (x.filters[e] = t);
      }
    this.render(), this.resizeObserver = new ResizeObserver(() => this.renderCharts()), this.resizeObserver.observe(this), this.initialize();
  }
  disconnectedCallback() {
    var e, t, a;
    this.disposed = !0, this.chartRequest++, this.productRequest++, (e = this.chartAbort) == null || e.abort(), (t = this.productAbort) == null || t.abort(), (a = this.resizeObserver) == null || a.disconnect(), this.resizeObserver = void 0, super.disconnectedCallback();
  }
  get currency() {
    var e;
    return (e = x.stores.find((t) => {
      var a;
      return t.alias === (((a = this.chartFilters) == null ? void 0 : a.store) || x.filters.store);
    })) == null ? void 0 : e.currency;
  }
  async initialize() {
    var e;
    try {
      const [t, a] = await Promise.all([this.api.statusList(), this.api.stores()]);
      if (this.disposed) return;
      x.statusList = t || [], x.stores = a || [], x.stores.some((o) => o.alias === x.filters.store) || (x.filters.store = ((e = x.stores[0]) == null ? void 0 : e.alias) || ""), await this.loadAnalytics();
    } catch (t) {
      if (this.disposed) return;
      this.error = H(t, "Unable to load Analytics."), this.loading = !1, this.render();
    }
  }
  async loadAnalytics() {
    await Promise.all([this.loadCharts(), this.productsVisible ? this.loadMostSoldProducts() : Promise.resolve()]);
  }
  async loadCharts() {
    var o;
    const e = ++this.chartRequest;
    (o = this.chartAbort) == null || o.abort();
    const t = this.chartAbort = new AbortController(), a = { ...x.filters };
    if (this.chartData = void 0, this.previousData = void 0, this.error = "", this.loading = !!a.store, this.render(), !!a.store)
      try {
        const n = this.compare ? { ...a, ...pe(a.dateFrom, a.dateTo) } : void 0, [d, i] = await Promise.all([
          this.api.charts(a, t.signal),
          n ? this.api.charts(n, t.signal) : Promise.resolve(void 0)
        ]);
        if (e !== this.chartRequest || this.disposed) return;
        this.chartData = d, this.previousData = i, this.chartFilters = a, d.mixedCurrencies && (this.metric = "orders");
      } catch (n) {
        if (e !== this.chartRequest || this.disposed) return;
        this.error = H(n, "Unable to load charts.");
      } finally {
        e === this.chartRequest && !this.disposed && (this.loading = !1, this.render());
      }
  }
  async loadMostSoldProducts() {
    var o;
    const e = ++this.productRequest;
    (o = this.productAbort) == null || o.abort();
    const t = this.productAbort = new AbortController(), a = { ...x.filters };
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
    return this.chartData ? he(this.chartData, this.previousData, this.grouping) : [];
  }
  get comparisonMoneyAvailable() {
    var e, t;
    return !!this.previousData && !((e = this.chartData) != null && e.mixedCurrencies) && !this.previousData.mixedCurrencies && (!((t = this.chartData) != null && t.currency) || !this.previousData.currency || this.chartData.currency === this.previousData.currency);
  }
  amount(e) {
    var a, o, n, d, i;
    if (e == null || (a = this.chartData) != null && a.mixedCurrencies) return "—";
    const t = ((o = this.chartData) == null ? void 0 : o.currency) || ((n = this.previousData) == null ? void 0 : n.currency);
    return !t || t === ((d = this.currency) == null ? void 0 : d.currencyValue) || t === ((i = this.currency) == null ? void 0 : i.isoCurrencySymbol) ? z(e, this.currency) : /^[A-Z]{3}$/.test(t) ? z(e, { isoCurrencySymbol: t }) : `${e.toLocaleString(void 0, { maximumFractionDigits: 2 })} ${t}`;
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
    const e = x.filters, t = me(e.dateFrom, e.dateTo, this.datePreset);
    this.innerHTML = `<style>${ce}
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
    </style><section class="ekmManager analytics"><div class="ekmManager__body">
      <div class="analytics-toolbar">
        <label>Store<select data-field="store">${x.stores.map((a) => `<option value="${p(a.alias)}" ${a.alias === e.store ? "selected" : ""}>${p(a.title)}</option>`).join("")}</select></label>
        <label>Order status<select data-field="orderStatus"><option value="CompletedOrders" ${e.orderStatus === "CompletedOrders" ? "selected" : ""}>Completed Orders</option><option value="AllOrders" ${e.orderStatus === "AllOrders" ? "selected" : ""}>All Orders</option>${x.statusList.map((a) => `<option value="${p(Q(a))}" ${Q(a) === e.orderStatus ? "selected" : ""}>${p(a.label)}</option>`).join("")}</select></label>
        <label>Date From<input type="date" data-field="dateFrom" value="${p(e.dateFrom)}"></label>
        <label>Date To<input type="date" data-field="dateTo" value="${p(e.dateTo)}"></label>
        <label>Date preset<select data-preset><option value="">Custom range</option>${[["today", "Today"], ["7", "Last 7 days"], ["30", "Last 30 days"], ["month", "This month"], ["year", "This year"]].map(([a, o]) => `<option value="${a}" ${t === a ? "selected" : ""}>${o}</option>`).join("")}</select></label>
        <label><span>Comparison</span><span><input type="checkbox" data-compare ${this.compare ? "checked" : ""}> Previous period</span></label>
        <button data-refresh>Refresh</button>
      </div>
      ${this.error ? `<p role="alert">${p(this.error)}</p>` : ""}
      ${this.loading ? '<p role="status">Loading analytics…</p>' : e.store ? "" : "<p>No accessible stores.</p>"}
      ${this.chartData ? this.renderAnalytics() + this.renderCustomerInsights() : ""}
      <div class="card analytics-main"><div class="analytics-toolbar"><h3>Most Sold Products</h3><button data-products>${this.productsVisible ? "Hide products" : "Load product breakdown"}</button></div>
        <p class="analytics-muted">Fetch the most sold products for the selected store and dates. This may take a minute for larger date ranges. Totals include VAT and discounts, excluding shipping and payment fees.</p>
        ${this.productsVisible ? `${this.productError ? `<p role="alert">${p(this.productError)}</p><button data-product-retry>Retry products</button>` : ""}${this.loadingMostSoldProducts ? '<p role="status"><span class="analytics-spinner" aria-hidden="true"></span>Fetching product breakdown… This may take a minute. You can continue using Analytics.</p>' : this.renderMostSoldProducts()}` : ""}
      </div>
    </div></section>`, this.bindEvents(), this.renderCharts();
  }
  renderAnalytics() {
    var a, o, n, d;
    const e = X(this.chartData), t = this.previousData ? X(this.previousData) : void 0;
    return `<div class="analytics-summary">${["amount", "orders", "average"].map((i) => `<div class="card"><span>${this.label(i)}</span><strong>${p(this.value(e[i], i))}</strong><span class="analytics-muted">${t && (i === "orders" || this.comparisonMoneyAvailable) ? p(K(e[i], t[i])) + " vs previous period" : this.compare ? "No monetary comparison available" : "Selected period"}</span></div>`).join("")}</div>
      <p class="analytics-muted">Order amount is the recorded charged amount, after gift cards; it is not refund-adjusted revenue. Dates use ${this.chartData.dateBasis === "PaidDate" ? "payment date" : "order creation date"}, matching Orders.</p>
      ${this.chartData.mixedCurrencies ? '<p role="status">This period contains multiple currencies. Monetary totals are hidden to avoid combining different currencies.</p>' : ""}
      ${this.previousData && !this.comparisonMoneyAvailable ? '<p class="analytics-muted">Monetary comparison is unavailable across different or mixed currencies.</p>' : ""}
      <div class="card analytics-main"><div class="analytics-toolbar">
        ${["amount", "orders", "average"].map((i) => `<button data-metric="${i}" aria-pressed="${this.metric === i}" ${i !== "orders" && this.chartData.mixedCurrencies ? "disabled" : ""}>${this.label(i)}</button>`).join("")}
        <label>Group by<select data-grouping>${[["day", "Daily"], ["week", "Weekly"], ["month", "Monthly"]].map(([i, S]) => `<option value="${i}" ${this.grouping === i ? "selected" : ""}>${S}</option>`).join("")}</select></label>
        <button data-table aria-pressed="${this.table}">${this.table ? "Show chart" : "Show data table"}</button><button data-csv>Download CSV</button>
      </div>
      <p class="analytics-muted">${this.previousData ? `Solid: ${p(this.range((a = this.chartFilters) == null ? void 0 : a.dateFrom, (o = this.chartFilters) == null ? void 0 : o.dateTo))}. Dashed: ${p(this.range((n = this.previousData.ordersChart.points[0]) == null ? void 0 : n.x, (d = this.previousData.ordersChart.points.at(-1)) == null ? void 0 : d.x))}. Comparison buckets cover the same number of days.` : "Selected period."} Click a bucket to open its orders.</p>
      ${this.table ? this.renderTable() : `<div style="height:360px"><canvas id="analytics-chart" style="width:100%;height:100%" role="img" aria-label="${this.label(this.metric)} over time. Use Show data table for exact values and order links."></canvas></div>`}
      </div>`;
  }
  renderCustomerInsights() {
    var S, l;
    const e = (S = this.chartData) == null ? void 0 : S.customerInsights;
    if (!e) return "";
    const t = (l = this.previousData) == null ? void 0 : l.customerInsights, a = [
      ["Unique purchasing customers", e.uniqueCustomers, t == null ? void 0 : t.uniqueCustomers, !1],
      ["First-time buyers", e.newCustomers, t == null ? void 0 : t.newCustomers, !1],
      ["Returning customer rate", e.returningCustomerRate, t == null ? void 0 : t.returningCustomerRate, !0]
    ], o = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"], n = o.map((h, f) => e.orderTimes.filter((c) => c.weekday === f).reduce((c, v) => c + v.orders, 0)), d = Array.from({ length: 24 }, (h, f) => e.orderTimes.filter((c) => c.hour === f).reduce((c, v) => c + v.orders, 0)), i = (h, f) => h.map((c, v) => `<div class="analytics-time-row"><span>${c}</span><div class="analytics-time-track" aria-hidden="true"><span style="width:${100 * f[v] / Math.max(1, ...f)}%"></span></div><span>${f[v]}</span></div>`).join("");
    return `<div class="analytics-summary">${a.map(([h, f, c, v]) => `<div class="card"><span>${h}</span><strong>${f == null ? "—" : p(f.toLocaleString(void 0, { maximumFractionDigits: v ? 1 : 0 })) + (v ? "%" : "")}</strong><span class="analytics-muted">${t ? v ? f == null || c == null ? "No comparison available" : `${(f - c).toLocaleString(void 0, { maximumFractionDigits: 1, signDisplay: "always" })} percentage points vs previous period` : `${K(f, c ?? null)} vs previous period` : "Selected period"}</span></div>`).join("")}</div>
      <div class="card analytics-main"><h3>New and returning purchases</h3>
        <p class="analytics-muted">Based on qualifying purchase orders and ${e.identityField === "CustomerUsername" ? "username" : "email"}. A repeat purchase has an earlier qualifying order in this store, ordered by payment time (creation time when unpaid). A buyer can appear in both rows if they buy for the first time and return in this period. Returning rate is repeat buyers divided by unique identified buyers.</p>
        <div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Purchase type</th><th>Buyers</th><th>Orders</th><th>Order amount</th></tr></thead><tbody>
          <tr><td>First purchase</td><td>${e.newCustomers}</td><td>${e.firstOrders}</td><td>${p(this.amount(e.firstOrderAmount))}</td></tr>
          <tr><td>Repeat purchase</td><td>${e.returningCustomers}</td><td>${e.repeatOrders}</td><td>${p(this.amount(e.repeatOrderAmount))}</td></tr>
        </tbody></table></div>
        <p class="analytics-muted">${e.missingIdentifiers} qualifying orders have no customer identifier and are excluded from buyer counts and this breakdown. They remain included in the time charts below. Incomplete and cancelled orders are excluded from these customer insights, even with All Orders selected.</p>
      </div>
      <div class="analytics-insight-grid">
        <div class="card analytics-main"><h3>Orders by weekday</h3><p class="analytics-muted">Qualifying orders within the selected filters, grouped by recorded creation day.</p><div class="analytics-time-bars">${i(o, n)}</div></div>
        <div class="card analytics-main"><h3>Orders by hour</h3><p class="analytics-muted">Recorded creation time, 00–23. No timezone conversion. Totals are counts, not averages per hour.</p><div class="analytics-time-bars analytics-hour-bars">${i(d.map((h, f) => String(f).padStart(2, "0") + ":00"), d)}</div></div>
      </div>`;
  }
  renderTable() {
    return `<div class="analytics-table-wrap"><table class="analytics-table"><thead><tr><th>Period</th><th>Orders</th><th>Order amount</th><th>Average order</th>${this.previousData ? `<th>Previous period</th><th>Previous ${this.label(this.metric).toLowerCase()}</th>` : ""}<th>Details</th></tr></thead><tbody>${this.buckets.map((e, t) => `<tr><td>${p(this.range(e.start, e.end))}</td><td>${e.orders}</td><td>${p(this.amount(e.amount))}</td><td>${p(this.amount(e.average))}</td>${this.previousData ? `<td>${p(this.range(e.previousStart, e.previousEnd))}</td><td>${p(this.metric === "orders" || this.comparisonMoneyAvailable ? this.value(this.previousValue(e)) : "—")}</td>` : ""}<td><button data-orders="${t}">Open orders</button></td></tr>`).join("")}</tbody></table></div>`;
  }
  renderMostSoldProducts() {
    return this.mostSoldProducts.products.length ? `
      <div class="umb-table">
        <div class="umb-table-head"><div class="umb-table-row"><div class="umb-table-cell">Product</div><div class="umb-table-cell">Sku</div><div class="umb-table-cell">Quantity</div><div class="umb-table-cell">Total</div></div></div>
        <div class="umb-table-body">
          ${this.mostSoldProducts.products.map((e) => `<div class="umb-table-row"><div class="umb-table-cell">${p(I(e, "title", "productTitle", "name"))}</div><div class="umb-table-cell">${p(I(e, "sku", "productSku"))}</div><div class="umb-table-cell">${p(I(e, "productCount", "quantity", "count"))}</div><div class="umb-table-cell">${p(z(Number(I(e, "total")), this.currency))}</div></div>`).join("")}
        </div>
      </div>
      ${this.mostSoldProducts.totalPages > 1 ? this.renderMostSoldProductsPagination() : ""}
    ` : "<p>No products found.</p>";
  }
  renderMostSoldProductsPagination() {
    return `
      <div class="pagination">
        <ul>
          ${ne(this.pageMostSoldProducts, this.mostSoldProducts.totalPages).map((e) => {
      const t = Number(String(e).replace("...", ""));
      return `<li class="${t === this.pageMostSoldProducts ? "active" : ""}"><button type="button" data-action="set-most-sold-page" data-page="${t}" ${t === this.pageMostSoldProducts ? "disabled" : ""}>${p(e)}</button></li>`;
    }).join("")}
        </ul>
      </div>
    `;
  }
  renderCharts() {
    var d;
    const e = this.querySelector("#analytics-chart");
    if (!e || !this.chartData || this.table) return;
    const t = this.buckets, a = this.chartData.currency || ((d = this.previousData) == null ? void 0 : d.currency), o = a && /^[A-Z]{3}$/.test(a) ? { isoCurrencySymbol: a } : this.currency, n = this.previousData && (this.metric === "orders" || this.comparisonMoneyAvailable) ? { labels: [], points: t.map((i) => ({ x: this.range(i.previousStart, i.previousEnd), y: this.previousValue(i) })) } : void 0;
    ue(e, { labels: [], points: t.map((i) => ({ x: this.range(i.start, i.end), y: i[this.metric] })) }, "#25765c", {
      currency: this.metric === "orders" ? void 0 : o,
      integer: this.metric === "orders",
      comparison: n,
      formatValue: (i) => this.value(i),
      columnHover: !0,
      onSelect: (i) => this.openOrders(i)
    });
  }
  changedFilters() {
    this.pageMostSoldProducts = 1, this.dispatchEvent(new CustomEvent("analytics-filters-changed", { bubbles: !0, detail: { ...x.filters } })), this.loadAnalytics();
  }
  openOrders(e) {
    const t = this.buckets[e];
    if (!t || !this.chartFilters) return;
    const a = { ...this.chartFilters, dateFrom: t.start, dateTo: t.end };
    for (const o of ["query", "paymentProvider", "shippingProvider", "couponCode", "giftcardCode", "productSku", "trackingSource", "trackingMedium", "trackingCampaign", "trackingTerm", "trackingContent", "trackingClickId"]) a[o] = "";
    if (Object.assign(x.filters, a), this.hasAttribute("classic")) this.dispatchEvent(new CustomEvent("analytics-open-orders", { bubbles: !0, detail: a }));
    else {
      const o = new URLSearchParams({ analyticsDrill: "1", store: a.store, start: a.dateFrom, end: a.dateTo, orderStatus: a.orderStatus }), n = document.createElement("a");
      n.href = location.pathname.replace(/\/analytics\/?$/, "/orders") + "?" + o, this.appendChild(n), n.click(), n.remove();
    }
  }
  exportCsv() {
    var a, o, n;
    const e = [["From", "To", "Orders", "Order amount", "Average order", "Currency", "Previous from", "Previous to", "Previous orders", "Previous amount", "Previous average"]];
    for (const d of this.buckets) e.push([d.start, d.end, String(d.orders), (a = this.chartData) != null && a.mixedCurrencies ? "" : String(d.amount), (o = this.chartData) != null && o.mixedCurrencies || d.average == null ? "" : String(d.average), ((n = this.chartData) == null ? void 0 : n.currency) || "", d.previousStart || "", d.previousEnd || "", d.previousOrders == null ? "" : String(d.previousOrders), this.comparisonMoneyAvailable ? String(d.previousAmount ?? "") : "", this.comparisonMoneyAvailable ? String(d.previousAverage ?? "") : ""]);
    const t = e.map((d) => d.map((i) => '"' + i.replaceAll('"', '""') + '"').join(",")).join(`\r
`);
    le(new Blob(["\uFEFF", t], { type: "text/csv;charset=utf-8" }), "analytics.csv");
  }
  bindEvents() {
    var e, t, a, o, n, d, i, S;
    this.querySelectorAll("[data-field]").forEach((l) => l.addEventListener("change", () => {
      const h = l.dataset.field;
      (h === "dateFrom" || h === "dateTo") && (this.datePreset = void 0), x.filters[h] = l.value, this.changedFilters();
    })), (e = this.querySelector("[data-preset]")) == null || e.addEventListener("change", (l) => {
      const h = l.target.value;
      this.datePreset = h, h ? (Object.assign(x.filters, te(h)), this.changedFilters()) : this.render();
    }), (t = this.querySelector("[data-compare]")) == null || t.addEventListener("change", () => {
      this.compare = !this.compare, this.loadCharts();
    }), (a = this.querySelector("[data-grouping]")) == null || a.addEventListener("change", (l) => {
      this.grouping = l.target.value, this.render();
    }), this.querySelectorAll("[data-metric]").forEach((l) => l.addEventListener("click", () => {
      this.metric = l.dataset.metric, this.render();
    })), (o = this.querySelector("[data-table]")) == null || o.addEventListener("click", () => {
      this.table = !this.table, this.render();
    }), (n = this.querySelector("[data-csv]")) == null || n.addEventListener("click", () => this.exportCsv()), (d = this.querySelector("[data-refresh]")) == null || d.addEventListener("click", () => void this.loadAnalytics()), this.querySelectorAll("[data-orders]").forEach((l) => l.addEventListener("click", () => this.openOrders(Number(l.dataset.orders)))), (i = this.querySelector("[data-products]")) == null || i.addEventListener("click", () => {
      var l;
      this.productsVisible = !this.productsVisible, this.productsVisible ? this.loadMostSoldProducts() : (this.productRequest++, (l = this.productAbort) == null || l.abort(), this.render());
    }), (S = this.querySelector("[data-product-retry]")) == null || S.addEventListener("click", () => void this.loadMostSoldProducts()), this.querySelectorAll('[data-action="set-most-sold-page"]').forEach((l) => l.addEventListener("click", () => {
      this.pageMostSoldProducts = Number(l.dataset.page), this.loadMostSoldProducts();
    }));
  }
}
function I(s, ...r) {
  for (const e of r) if (s[e] != null) return s[e];
  return "";
}
function H(s, r) {
  return s instanceof Error ? s.message : r;
}
customElements.define("ekom-analytics-section-view", ge);
export {
  ge as EkomAnalyticsSectionViewElement,
  ge as default
};
