const $ = /* @__PURE__ */ new Date(), j = new Date($.getFullYear(), 0, 1), J = {
  filters: {
    dateFrom: z(j),
    dateTo: z($),
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
class b {
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
  async statusList() {
    return this.getJson("/ekom/manager/StatusList");
  }
  async stores() {
    return this.getJson("/ekom/manager/stores");
  }
  async paymentProviders(e) {
    return this.getJson(`/ekom/manager/paymentproviders/${encodeURIComponent(e)}`);
  }
  async shippingProviders(e) {
    return this.getJson(`/ekom/manager/shippingproviders/${encodeURIComponent(e)}`);
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
  async releaseOrderLock(e, r) {
    return this.postBody(`/ekom/manager/releaseOrderLock/${encodeURIComponent(e)}`, { releaseToken: r });
  }
  async giftcardRecovery(e) {
    return this.getJson(`/ekom/manager/giftcardRecovery/${encodeURIComponent(e)}`);
  }
  async releaseGiftcardRecovery(e, r, t, i) {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(e)}/release`, { attemptId: r, claimId: t, token: i });
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
      throw await b.createError(t, "Order action failed.");
    return t;
  }
  async changeOrderStatus(e, r, t) {
    return this.postJson("/ekom/manager/changeOrderStatus", { orderId: e, orderStatus: r, notify: t });
  }
  async updateCustomerInformation(e, r, t) {
    return this.postBody("/ekom/manager/UpdateCustomerInformation", { orderId: e, customer: r, shipping: t });
  }
  async addOrderLine(e, r, t, i) {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(e)}/OrderLines`, { productId: r, variantId: t, quantity: i });
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
      throw await b.createError(t, "Error removing order line.");
    return t.json();
  }
  async charts(e) {
    return this.getJson("/ekom/manager/charts", {
      start: e.dateFrom,
      end: e.dateTo,
      orderStatus: e.orderStatus,
      store: e.store
    });
  }
  async mostSoldProducts(e, r, t = 20) {
    return this.getJson("/ekom/manager/MostSoldProducts", {
      start: e.dateFrom,
      end: e.dateTo,
      orderStatus: e.orderStatus,
      store: e.store,
      page: r,
      pageSize: t
    });
  }
  async getJson(e, r) {
    const t = await fetch(e + T(r), {
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await b.createError(t, "Request failed.");
    return t.json();
  }
  async getBlob(e, r) {
    const t = await fetch(e + T(r), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!t.ok)
      throw await b.createError(t, "Request failed.");
    return t.blob();
  }
  async postJson(e, r) {
    const t = await fetch(e + T(r), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await b.createError(t, "Request failed.");
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
      throw await b.createError(t, "Request failed.");
    return t.json();
  }
  static async createError(e, r) {
    const t = await e.text();
    let i = t || r;
    try {
      i = JSON.parse(t).message || i;
    } catch {
    }
    return new Error(i || `${r} Status ${e.status}.`);
  }
}
function T(a) {
  if (!a)
    return "";
  const e = new URLSearchParams();
  for (const [t, i] of Object.entries(a))
    e.set(t, i == null ? "" : String(i));
  const r = e.toString();
  return r ? `?${r}` : "";
}
function z(a) {
  const e = String(a.getMonth() + 1).padStart(2, "0"), r = String(a.getDate()).padStart(2, "0");
  return `${a.getFullYear()}-${e}-${r}`;
}
function F(a) {
  if (!a)
    return "";
  const e = new Date(a);
  return Number.isNaN(e.getTime()) ? a : new Intl.DateTimeFormat(void 0, {
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit"
  }).format(e);
}
function U(a) {
  return String(a ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
function N(a) {
  const e = document.createElement("textarea");
  return e.innerHTML = String(a ?? ""), e.value;
}
function q(a) {
  return a.enumValue || a.value || "";
}
function W(a, e) {
  const t = [], i = Math.max(e || 1, 1);
  let m;
  a <= Math.floor(5 / 2) ? m = 1 : a + Math.floor(5 / 2) >= i ? m = Math.max(i - 5 + 1, 1) : m = a - Math.floor(5 / 2);
  for (let p = 0; p < 5; p += 1) {
    const u = m + p;
    u <= i && t.push(u);
  }
  return t.length && Number(t[t.length - 1]) < i && t.push(`...${i}`), t.length && Number(t[0]) > 1 && t.unshift("...1"), t;
}
const E = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`;
async function H(a, e) {
  const r = document.createElement("iframe");
  r.title = e, r.setAttribute("aria-hidden", "true"), r.style.cssText = "position:fixed;left:-10000px;top:0;width:800px;height:600px;border:0;";
  try {
    const t = new Promise((m, p) => {
      r.onload = () => m(), r.onerror = () => p(new Error("Unable to load the print document."));
    });
    r.srcdoc = D(a, e), document.body.append(r), await t;
    const i = r.contentWindow;
    if (!i) throw new Error("Print window unavailable.");
    await i.document.fonts.ready, i.addEventListener("afterprint", () => r.remove(), { once: !0 }), i.focus(), i.print();
  } catch (t) {
    throw r.remove(), t;
  }
}
function D(a, e) {
  const r = document.createElement("template");
  return r.innerHTML = a, r.content.querySelectorAll("button, input, select, .ekmOrderGiftcards__actions").forEach((t) => t.remove()), r.content.querySelectorAll("details").forEach((t) => t.setAttribute("open", "")), r.content.querySelectorAll("summary").forEach((t) => t.remove()), r.content.querySelectorAll("a").forEach((t) => t.replaceWith(...Array.from(t.childNodes))), `<!doctype html><html><head><meta charset="utf-8"><title>${U(e)}</title><style>
    ${E}
    @page { size: auto; margin: 14mm; }
    * { box-sizing: border-box; }
    html, body { margin: 0; padding: 0; height: auto; overflow: visible; }
    body { color: #111; background: white; font: 10pt/1.4 Arial, sans-serif; overflow-wrap: anywhere; }
    h1 { font-size: 19pt; margin: 0 0 12px; }
    h4 { font-size: 11pt; margin: 18px 0 8px; }
    h5 { font-size: 10pt; margin: 12px 0 6px !important; }
    h1, h4, h5 { break-after: avoid; }
    p { margin: 4px 0; }
    ul { padding-left: 18px; margin: 6px 0; }
    li { margin: 3px 0; }
    .ekmSplit { display: block; margin-bottom: 16px; }
    .ekmSplit__column { margin-bottom: 12px; }
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
function V(a, e) {
  const r = URL.createObjectURL(a), t = document.createElement("a");
  t.href = r, t.download = e, t.addEventListener("click", (i) => i.stopPropagation()), document.body.append(t), t.click(), t.remove(), URL.revokeObjectURL(r);
}
const Y = `
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

  ${E}
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
function B(a, e) {
  const r = e == null ? void 0 : e.isoCurrencySymbol, t = (e == null ? void 0 : e.currencyDecimalDigits) ?? 2;
  return new Intl.NumberFormat(void 0, {
    minimumFractionDigits: t,
    maximumFractionDigits: t,
    ...r && /^[A-Z]{3}$/.test(r) ? { style: "currency", currency: r, currencyDisplay: "code" } : {}
  }).format(a);
}
function Q(a, e, r, t = {}) {
  const i = a.getBoundingClientRect(), m = window.devicePixelRatio || 1, p = i.width || 300, u = i.height || 320;
  a.width = p * m, a.height = u * m;
  const o = a.getContext("2d");
  if (!o) return;
  o.scale(m, m), o.font = "12px sans-serif";
  const w = e.points || [], _ = w.map((s) => Number(s.y || 0)), O = Math.min(0, ..._), P = Math.max(1, ..._), S = t.integer ? Math.ceil(P / 4) * 4 : P, C = (s) => t.currency ? B(s, t.currency) : s.toLocaleString(void 0, { maximumFractionDigits: t.integer ? 0 : 2 }), I = Array.from({ length: 5 }, (s, l) => S - (S - O) * l / 4), h = Math.max(48, ...I.map((s) => o.measureText(C(s)).width + 12)), y = p - 16, L = 20, v = u - 52, c = w.map((s, l) => {
    var x;
    return {
      x: w.length > 1 ? h + (y - h) * l / (w.length - 1) : (h + y) / 2,
      y: v - (_[l] - O) / (S - O) * (v - L),
      date: s.x || ((x = e.labels) == null ? void 0 : x[l]) || "",
      value: _[l]
    };
  }), A = (s = -1) => {
    o.clearRect(0, 0, p, u), o.font = "12px sans-serif", o.lineWidth = 1, o.textAlign = "right", I.forEach((n, d) => {
      const f = L + (v - L) * d / 4;
      o.fillStyle = "#555", o.fillText(C(n), h - 8, f + 4), o.strokeStyle = "#e5e5e5", o.beginPath(), o.moveTo(h, f), o.lineTo(y, f), o.stroke();
    });
    const l = Math.max(90, ...c.map((n) => o.measureText(n.date).width + 16)), x = Math.max(2, Math.floor((y - h) / l)), R = new Set(Array.from({ length: Math.min(x, c.length) }, (n, d) => Math.round(d * (c.length - 1) / Math.max(1, Math.min(x, c.length) - 1))));
    o.fillStyle = "#555", c.forEach((n, d) => {
      R.has(d) && (o.textAlign = c.length === 1 ? "center" : d === 0 ? "left" : d === c.length - 1 ? "right" : "center", o.fillText(n.date, n.x, v + 24));
    }), o.strokeStyle = r, o.lineWidth = 2, o.beginPath(), c.forEach((n, d) => {
      d === 0 ? o.moveTo(n.x, n.y) : o.lineTo(n.x, n.y);
    }), o.stroke(), o.fillStyle = r, c.forEach((n, d) => {
      o.beginPath(), o.arc(n.x, n.y, d === s ? 5 : 3, 0, Math.PI * 2), o.fill();
    }), c.length || (o.textAlign = "center", o.fillText("No data for this period", (h + y) / 2, u / 2));
    const g = c[s];
    if (g) {
      const n = [g.date, C(g.value)], d = Math.max(...n.map((M) => o.measureText(M).width)) + 20, f = Math.max(0, Math.min(p - d, g.x - d / 2)), k = g.y > 70 ? g.y - 62 : g.y + 12;
      o.fillStyle = "#222", o.fillRect(f, k, d, 48), o.fillStyle = "#fff", o.textAlign = "left", n.forEach((M, G) => o.fillText(M, f + 10, k + 19 + G * 18));
    }
  };
  a.onmousemove = (s) => {
    const l = a.getBoundingClientRect(), x = (s.clientX - l.left) * p / l.width, R = (s.clientY - l.top) * u / l.height;
    let g = -1, n = 16;
    c.forEach((d, f) => {
      const k = Math.hypot(d.x - x, d.y - R);
      k < n && (g = f, n = k);
    }), a.style.cursor = g >= 0 ? "pointer" : "default", A(g);
  }, a.onmouseleave = () => {
    a.style.cursor = "default", A();
  }, A();
}
export {
  b as E,
  Y as a,
  F as b,
  H as c,
  N as d,
  U as e,
  B as f,
  q as g,
  V as h,
  J as m,
  W as p,
  Q as r
};
