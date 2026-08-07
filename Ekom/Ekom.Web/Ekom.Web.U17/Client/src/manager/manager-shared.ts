export type ManagerFilters = {
  dateFrom: string;
  dateTo: string;
  orderStatus: string;
  store: string;
  paymentProvider: string;
  productSku: string;
  trackingSource: string;
  trackingMedium: string;
  trackingCampaign: string;
  trackingTerm: string;
  trackingContent: string;
  trackingClickId: string;
  query: string;
};

export type StatusItem = {
  label: string;
  value?: string;
  enumValue?: string;
};

export type StoreItem = {
  alias: string;
  title: string;
};

export type PaymentProviderItem = {
  key: string;
  title: string;
};

export type OrderListItem = {
  uniqueId: string;
  referenceId: string | number;
  orderStatusCol?: string;
  customerName?: string;
  storeAlias?: string;
  createDate?: string;
  formattedTotal?: string;
};

export type OrderSearchResult = {
  orders: OrderListItem[];
  count: number;
  totalPages: number;
  page?: number;
  grandTotal?: string;
  averageAmount?: string;
};

export type ChartPoint = {
  x?: string;
  y?: number;
};

export type ChartSeries = {
  labels: string[];
  points: ChartPoint[];
};

export type ChartData = {
  revenueChart: ChartSeries;
  ordersChart: ChartSeries;
  avarageChart: ChartSeries;
};

export type MostSoldProductsResult = {
  products: Array<Record<string, unknown>>;
  count: number;
  totalPages: number;
  page: number;
};

export type OrderAction = {
  key: string;
  label: string;
  enabled?: boolean;
  look?: string;
  confirmMessage?: string;
};

export type OrderActivityLog = {
  date?: string;
  message?: string;
  logType?: number;
};

export type CustomerInformationField = {
  key: string;
  label: string;
  value: string;
  isExtra: boolean;
};

export type OrderInfo = Record<string, any>;

export type ManagerState = {
  filters: ManagerFilters;
  statusList: StatusItem[];
  stores: StoreItem[];
  paymentProviders: PaymentProviderItem[];
};

const currentDate = new Date();
const januaryFirstCurrentYear = new Date(currentDate.getFullYear(), 0, 1);

export const managerState: ManagerState = {
  filters: {
    dateFrom: toDateInputValue(januaryFirstCurrentYear),
    dateTo: toDateInputValue(currentDate),
    orderStatus: 'CompletedOrders',
    store: '',
    paymentProvider: '',
    productSku: '',
    trackingSource: '',
    trackingMedium: '',
    trackingCampaign: '',
    trackingTerm: '',
    trackingContent: '',
    trackingClickId: '',
    query: '',
  },
  statusList: [],
  stores: [],
  paymentProviders: [],
};

export class EkomManagerApi {
  async searchOrders(filters: ManagerFilters, page: number, pageSize = 20): Promise<OrderSearchResult> {
    return this.getJson('/ekom/manager/SearchOrders', {
      ...filters,
      start: filters.dateFrom,
      end: filters.dateTo,
      page,
      pageSize,
    });
  }

  async exportOrders(filters: ManagerFilters, pageSize: number, includeOrderLines: boolean): Promise<Blob> {
    return this.getBlob('/ekom/manager/ExportOrders', {
      ...filters,
      start: filters.dateFrom,
      end: filters.dateTo,
      page: 1,
      pageSize,
      includeOrderLines,
    });
  }

  async statusList(): Promise<StatusItem[]> {
    return this.getJson('/ekom/manager/StatusList');
  }

  async stores(): Promise<StoreItem[]> {
    return this.getJson('/ekom/manager/stores');
  }

  async paymentProviders(storeAlias: string): Promise<PaymentProviderItem[]> {
    return this.getJson(`/ekom/provider/paymentsproviders/${encodeURIComponent(storeAlias)}`);
  }

  async orderInfo(orderId: string): Promise<OrderInfo> {
    return this.getJson(`/ekom/manager/OrderInfo/${encodeURIComponent(orderId)}`);
  }

  async orderLogs(orderId: string): Promise<OrderActivityLog[]> {
    return this.getJson(`/ekom/manager/OrderLogs/${encodeURIComponent(orderId)}`);
  }

  async orderActions(orderId: string): Promise<OrderAction[]> {
    return this.getJson(`/ekom/manager/OrderActions/${encodeURIComponent(orderId)}`);
  }

  async executeOrderAction(orderId: string, actionKey: string): Promise<Response> {
    const response = await fetch(`/ekom/manager/OrderActions/${encodeURIComponent(orderId)}/${encodeURIComponent(actionKey)}`, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { Accept: 'application/json, application/octet-stream' },
    });

    if (!response.ok) {
      throw await EkomManagerApi.createError(response, 'Order action failed.');
    }

    return response;
  }

  async changeOrderStatus(orderId: string, orderStatus: string, notify: boolean): Promise<boolean> {
    return this.postJson('/ekom/manager/changeOrderStatus', { orderId, orderStatus, notify });
  }

  async updateCustomerInformation(orderId: string, customer: Record<string, string>, shipping: Record<string, string>): Promise<OrderInfo> {
    return this.postBody('/ekom/manager/UpdateCustomerInformation', { orderId, customer, shipping });
  }

  async addOrderLine(orderId: string, productId: string, variantId: string | undefined, quantity: number): Promise<OrderInfo> {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(orderId)}/OrderLines`, { productId, variantId, quantity });
  }

  async removeOrderLine(orderId: string, lineId: string): Promise<OrderInfo> {
    const response = await fetch(`/ekom/manager/Order/${encodeURIComponent(orderId)}/OrderLines/${encodeURIComponent(lineId)}`, {
      method: 'DELETE',
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });

    if (!response.ok) {
      throw await EkomManagerApi.createError(response, 'Error removing order line.');
    }

    return response.json() as Promise<OrderInfo>;
  }

  async charts(filters: ManagerFilters): Promise<ChartData> {
    return this.getJson('/ekom/manager/charts', {
      start: filters.dateFrom,
      end: filters.dateTo,
      orderStatus: filters.orderStatus,
      store: filters.store,
    });
  }

  async mostSoldProducts(filters: ManagerFilters, page: number, pageSize = 20): Promise<MostSoldProductsResult> {
    return this.getJson('/ekom/manager/MostSoldProducts', {
      start: filters.dateFrom,
      end: filters.dateTo,
      orderStatus: filters.orderStatus,
      store: filters.store,
      page,
      pageSize,
    });
  }

  private async getJson<T>(url: string, query?: Record<string, unknown>): Promise<T> {
    const response = await fetch(url + buildQueryString(query), {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });

    if (!response.ok) {
      throw await EkomManagerApi.createError(response, 'Request failed.');
    }

    return response.json() as Promise<T>;
  }

  private async getBlob(url: string, query?: Record<string, unknown>): Promise<Blob> {
    const response = await fetch(url + buildQueryString(query), {
      credentials: 'same-origin',
      headers: { Accept: 'text/csv, application/octet-stream' },
    });

    if (!response.ok) {
      throw await EkomManagerApi.createError(response, 'Request failed.');
    }

    return response.blob();
  }

  private async postJson<T>(url: string, query?: Record<string, unknown>): Promise<T> {
    const response = await fetch(url + buildQueryString(query), {
      method: 'POST',
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });

    if (!response.ok) {
      throw await EkomManagerApi.createError(response, 'Request failed.');
    }

    return response.json() as Promise<T>;
  }

  private async postBody<T>(url: string, body: unknown): Promise<T> {
    const response = await fetch(url, {
      method: 'POST',
      credentials: 'same-origin',
      headers: {
        Accept: 'application/json',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      throw await EkomManagerApi.createError(response, 'Request failed.');
    }

    return response.json() as Promise<T>;
  }

  private static async createError(response: Response, fallbackMessage: string): Promise<Error> {
    const text = await response.text();
    let message = text || fallbackMessage;

    try {
      const data = JSON.parse(text) as { message?: string };
      message = data.message || message;
    } catch {
      // Use plain text response.
    }

    return new Error(message || `${fallbackMessage} Status ${response.status}.`);
  }
}

export function buildQueryString(query?: Record<string, unknown>): string {
  if (!query) {
    return '';
  }

  const params = new URLSearchParams();

  for (const [key, value] of Object.entries(query)) {
    params.set(key, value == null ? '' : String(value));
  }

  const value = params.toString();
  return value ? `?${value}` : '';
}

export function toDateInputValue(value: Date): string {
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${value.getFullYear()}-${month}-${day}`;
}

export function formatDate(value?: string): string {
  if (!value) {
    return '';
  }

  const date = new Date(value);

  if (Number.isNaN(date.getTime())) {
    return value;
  }

  return new Intl.DateTimeFormat(undefined, {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

export function escapeHtml(value: unknown): string {
  return String(value ?? '')
    .replaceAll('&', '&amp;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;')
    .replaceAll('"', '&quot;')
    .replaceAll("'", '&#39;');
}

export function decodeHtml(value: unknown): string {
  const textArea = document.createElement('textarea');
  textArea.innerHTML = String(value ?? '');
  return textArea.value;
}

export function getStatusValue(status: StatusItem): string {
  return status.enumValue || status.value || '';
}

export function getStatusLabel(statuses: StatusItem[], value?: string): string {
  const item = statuses.find(status => status.enumValue === value || status.value === value);
  return item?.label || value || '';
}

export function pageRange(page: number, totalPages: number): Array<number | string> {
  const rangeSize = 5;
  const result: Array<number | string> = [];
  const safeTotalPages = Math.max(totalPages || 1, 1);
  let start: number;

  if (page <= Math.floor(rangeSize / 2)) {
    start = 1;
  } else if (page + Math.floor(rangeSize / 2) >= safeTotalPages) {
    start = Math.max(safeTotalPages - rangeSize + 1, 1);
  } else {
    start = page - Math.floor(rangeSize / 2);
  }

  for (let i = 0; i < rangeSize; i += 1) {
    const pageNumber = start + i;

    if (pageNumber <= safeTotalPages) {
      result.push(pageNumber);
    }
  }

  if (result.length && Number(result[result.length - 1]) < safeTotalPages) {
    result.push(`...${safeTotalPages}`);
  }

  if (result.length && Number(result[0]) > 1) {
    result.unshift('...1');
  }

  return result;
}

export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

export const managerStyles = `
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

export function renderLineChart(canvas: HTMLCanvasElement, series: ChartSeries, color: string): void {
  const rect = canvas.getBoundingClientRect();
  const dpr = window.devicePixelRatio || 1;
  const width = Math.max(rect.width, 300);
  const height = Math.max(rect.height, 220);
  canvas.width = width * dpr;
  canvas.height = height * dpr;

  const ctx = canvas.getContext('2d');
  if (!ctx) {
    return;
  }

  ctx.scale(dpr, dpr);
  ctx.clearRect(0, 0, width, height);
  ctx.strokeStyle = '#e5e5e5';
  ctx.lineWidth = 1;

  for (let i = 0; i < 5; i += 1) {
    const y = 20 + ((height - 50) / 4) * i;
    ctx.beginPath();
    ctx.moveTo(40, y);
    ctx.lineTo(width - 10, y);
    ctx.stroke();
  }

  const points = series.points || [];
  const values = points.map(point => Number(point.y || 0));
  const max = Math.max(...values, 1);
  const step = points.length > 1 ? (width - 60) / (points.length - 1) : 0;

  ctx.strokeStyle = color;
  ctx.lineWidth = 2;
  ctx.beginPath();

  points.forEach((point, index) => {
    const x = 40 + step * index;
    const y = height - 30 - ((Number(point.y || 0) / max) * (height - 60));

    if (index === 0) {
      ctx.moveTo(x, y);
    } else {
      ctx.lineTo(x, y);
    }
  });

  ctx.stroke();

  ctx.fillStyle = color;
  points.forEach((point, index) => {
    const x = 40 + step * index;
    const y = height - 30 - ((Number(point.y || 0) / max) * (height - 60));
    ctx.beginPath();
    ctx.arc(x, y, 3, 0, Math.PI * 2);
    ctx.fill();
  });
}
