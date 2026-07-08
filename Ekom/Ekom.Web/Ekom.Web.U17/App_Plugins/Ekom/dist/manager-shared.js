const f = /* @__PURE__ */ new Date(), y = new Date(f.getFullYear(), 0, 1), w = {
  filters: {
    dateFrom: h(y),
    dateTo: h(f),
    orderStatus: "CompletedOrders",
    store: "",
    paymentProvider: "",
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
  paymentProviders: []
};
class m {
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
    return this.getJson(`/ekom/provider/paymentsproviders/${encodeURIComponent(e)}`);
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
  async executeOrderAction(e, r) {
    const t = await fetch(`/ekom/manager/OrderActions/${encodeURIComponent(e)}/${encodeURIComponent(r)}`, {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json, application/octet-stream" }
    });
    if (!t.ok)
      throw await m.createError(t, "Order action failed.");
    return t;
  }
  async changeOrderStatus(e, r, t) {
    return this.postJson("/ekom/manager/changeOrderStatus", { orderId: e, orderStatus: r, notify: t });
  }
  async updateCustomerInformation(e, r, t) {
    return this.postBody("/ekom/manager/UpdateCustomerInformation", { orderId: e, customer: r, shipping: t });
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
    const t = await fetch(e + u(r), {
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await m.createError(t, "Request failed.");
    return t.json();
  }
  async getBlob(e, r) {
    const t = await fetch(e + u(r), {
      credentials: "same-origin",
      headers: { Accept: "text/csv, application/octet-stream" }
    });
    if (!t.ok)
      throw await m.createError(t, "Request failed.");
    return t.blob();
  }
  async postJson(e, r) {
    const t = await fetch(e + u(r), {
      method: "POST",
      credentials: "same-origin",
      headers: { Accept: "application/json" }
    });
    if (!t.ok)
      throw await m.createError(t, "Request failed.");
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
      throw await m.createError(t, "Request failed.");
    return t.json();
  }
  static async createError(e, r) {
    const t = await e.text();
    let n = t || r;
    try {
      n = JSON.parse(t).message || n;
    } catch {
    }
    return new Error(n || `${r} Status ${e.status}.`);
  }
}
function u(a) {
  if (!a)
    return "";
  const e = new URLSearchParams();
  for (const [t, n] of Object.entries(a))
    e.set(t, n == null ? "" : String(n));
  const r = e.toString();
  return r ? `?${r}` : "";
}
function h(a) {
  const e = String(a.getMonth() + 1).padStart(2, "0"), r = String(a.getDate()).padStart(2, "0");
  return `${a.getFullYear()}-${e}-${r}`;
}
function _(a) {
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
function v(a) {
  return String(a ?? "").replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");
}
function S(a) {
  const e = document.createElement("textarea");
  return e.innerHTML = String(a ?? ""), e.value;
}
function O(a) {
  return a.enumValue || a.value || "";
}
function C(a, e) {
  const t = [], n = Math.max(e || 1, 1);
  let d;
  a <= Math.floor(5 / 2) ? d = 1 : a + Math.floor(5 / 2) >= n ? d = Math.max(n - 5 + 1, 1) : d = a - Math.floor(5 / 2);
  for (let i = 0; i < 5; i += 1) {
    const o = d + i;
    o <= n && t.push(o);
  }
  return t.length && Number(t[t.length - 1]) < n && t.push(`...${n}`), t.length && Number(t[0]) > 1 && t.unshift("...1"), t;
}
function M(a, e) {
  const r = URL.createObjectURL(a), t = document.createElement("a");
  t.href = r, t.download = e, document.body.append(t), t.click(), t.remove(), URL.revokeObjectURL(r);
}
const T = `
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
  .ekmOrderTracking, .ekmOrderActivityLog { border: 1px solid #d8d7d9; margin: 30px 0; padding: 14px 16px; }
  .ekmOrderTracking__header { align-items: center; display: flex; justify-content: space-between; }
  .ekmOrderTracking__wrap { overflow-wrap: anywhere; word-break: break-word; }
  .ekmOrderActivityLog__item { border-top: 1px solid #eee; padding: 12px 0; }
  .ekmOrderActivityLog__date { color: #666; font-size: 12px; margin-bottom: 6px; }
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
    .ekmOrderActivityLog,
    .card,
    .umb-table-row { break-inside: avoid; box-shadow: none; }
    .umb-table { border-color: #bbb; overflow: visible; }
    .umb-table-cell { border-color: #ddd; color: #000; padding: 7px 8px; }
  }
`;
function j(a, e, r) {
  const t = a.getBoundingClientRect(), n = window.devicePixelRatio || 1, d = Math.max(t.width, 300), i = Math.max(t.height, 220);
  a.width = d * n, a.height = i * n;
  const o = a.getContext("2d");
  if (!o)
    return;
  o.scale(n, n), o.clearRect(0, 0, d, i), o.strokeStyle = "#e5e5e5", o.lineWidth = 1;
  for (let s = 0; s < 5; s += 1) {
    const l = 20 + (i - 50) / 4 * s;
    o.beginPath(), o.moveTo(40, l), o.lineTo(d - 10, l), o.stroke();
  }
  const p = e.points || [], k = p.map((s) => Number(s.y || 0)), b = Math.max(...k, 1), x = p.length > 1 ? (d - 60) / (p.length - 1) : 0;
  o.strokeStyle = r, o.lineWidth = 2, o.beginPath(), p.forEach((s, l) => {
    const c = 40 + x * l, g = i - 30 - Number(s.y || 0) / b * (i - 60);
    l === 0 ? o.moveTo(c, g) : o.lineTo(c, g);
  }), o.stroke(), o.fillStyle = r, p.forEach((s, l) => {
    const c = 40 + x * l, g = i - 30 - Number(s.y || 0) / b * (i - 60);
    o.beginPath(), o.arc(c, g, 3, 0, Math.PI * 2), o.fill();
  });
}
export {
  m as E,
  T as a,
  M as b,
  S as d,
  v as e,
  _ as f,
  O as g,
  w as m,
  C as p,
  j as r
};
