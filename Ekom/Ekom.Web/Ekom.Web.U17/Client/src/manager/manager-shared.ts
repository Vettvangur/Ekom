export type ManagerConfig = {
  ekomVersion?: string;
  managerOrderStatusFireEventsByDefault?: boolean | null;
};

export type ManagerFilters = {
  dateFrom: string;
  dateTo: string;
  orderStatus: string;
  store: string;
  currency?: string;
  paymentProvider: string;
  shippingProvider: string;
  couponCode: string;
  giftcardCode: string;
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
  currencies?: string[];
  currency?: StoreCurrency;
  /** How each store currency is written, as defined by the culture configured on the store. */
  currencyFormats?: CurrencyFormat[];
  /** How the culture configured on the store writes dates. */
  dateFormat?: DateFormat;
};

/** Patterns are .NET custom date format strings; names come from the store culture. */
export type DateFormat = { culture: string; shortDate: string; shortTime: string; monthDay: string; yearMonth: string; dateSeparator: string; timeSeparator: string; amDesignator: string; pmDesignator: string; monthNames: string[]; abbreviatedMonthNames: string[]; shortestDayNames: string[] };

/** positivePattern follows .NET: 0 = $n, 1 = n$, 2 = $ n, 3 = n $. */
export type CurrencyFormat = { code: string; symbol: string; groupSeparator: string; decimalSeparator: string; decimalDigits: number; positivePattern: number };
export type StoreCurrency = { isoCurrencySymbol?: string; currencyValue?: string; currencyDecimalDigits?: number; format?: CurrencyFormat };

/** A store currency with the store's own format for it, so amounts read the same as in Orders. */
export function storeCurrency(store: StoreItem | undefined, code: string | undefined): StoreCurrency | undefined {
  code ||= store?.currency?.isoCurrencySymbol;
  if (!code) return store?.currency;
  const format = store?.currencyFormats?.find(item => item?.code === code);
  return { ...(store?.currency?.isoCurrencySymbol === code ? store.currency : {}), isoCurrencySymbol: code, ...(format ? { format, currencyDecimalDigits: format.decimalDigits } : {}) };
}

function formatWithStoreCulture(value: number, format: CurrencyFormat): string {
  const [whole, fraction] = Math.abs(value).toFixed(Math.max(0, format.decimalDigits)).split('.');
  const number = whole.replace(/\B(?=(\d{3})+(?!\d))/g, format.groupSeparator) + (fraction ? format.decimalSeparator + fraction : '');
  const amount = [`${format.symbol}${number}`, `${number}${format.symbol}`, `${format.symbol} ${number}`, `${number} ${format.symbol}`][format.positivePattern] ?? `${number} ${format.symbol}`;
  return (value < 0 && Number(whole + '.' + (fraction || '0')) !== 0 ? '-' : '') + amount;
}

export type PaymentProviderItem = {
  key: string;
  title: string;
};

export type ShippingProviderItem = {
  key: string;
  title: string;
};

export type OrderStatusSnapshot = {
  orderStatus: 'Draft' | 'Open' | 'Closed' | 'Cancelled' | null;
  paymentStatus: 'Pending' | 'Authorized' | 'Paid' | 'PartiallyPaid' | 'Failed' | 'PartiallyRefunded' | 'Refunded' | 'Voided' | null;
  fulfillmentStatus: 'Unfulfilled' | 'Picking' | 'Picked' | 'Packed' | 'PartiallyFulfilled' | 'Fulfilled' | null;
  requiresReview: boolean;
  legacyStatus: string;
  revision: number;
  autoCloseSuppressed?: boolean;
  fulfilledQuantities?: Record<string, number>;
};

export type OrderReview = { id: string; title: string; message: string; source: string; createdUtc: string; };

export type OrderListItem = {
  needsReview?: boolean;
  status?: OrderStatusSnapshot;
  uniqueId: string;
  referenceId: string | number;
  orderStatusCol?: string;
  customerName?: string;
  customerEmail?: string;
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

export type OrderViewCount = {
  view: string;
  count: number;
};

export type ChartPoint = {
  x?: string;
  y?: number | null;
};

export type ChartSeries = {
  labels: string[];
  points: ChartPoint[];
};

export type CustomerAnalytics = {
  uniqueCustomers: number; newCustomers: number; returningCustomers: number;
  returningCustomerRate: number | null; firstOrders: number; repeatOrders: number;
  firstOrderAmount: number; repeatOrderAmount: number; missingIdentifiers: number; identityField: string;
  orderTimes: Array<{ weekday: number; hour: number; orders: number }>;
};

export type ChartData = {
  customerInsights?: CustomerAnalytics;
  dateBasis?: string;
  currency?: string;
  mixedCurrencies?: boolean;
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
  giftcardCode?: string;
  disabledReason?: string;
};

export type OrderLockStatus = {
  hasLock: boolean;
  paymentOperationLocked: boolean;
  preparationLocked: boolean;
  reconciliationRequired: boolean;
  attemptId: string | null;
  attemptState: string | null;
  releaseToken?: string | null;
  canRelease: boolean;
  releaseUnavailableReason?: string | null;
};

export type OrderLockReleaseResult = {
  message: string;
  warning?: string | null;
};

export type GiftcardRecoveryClaim = {
  id: string;
  cardReference: string;
  claimReference: string;
  provider: string;
  account: string;
  requestedAmount: number;
  currency: string;
  rawReturnedAmount?: string | null;
  returnedAmountType?: string | null;
  rawExpiry?: string | null;
  cleanupState: string;
  canRelease: boolean;
  releaseToken?: string | null;
  unavailableReason?: string | null;
};

export type GiftcardRecoveryAttempt = {
  attemptId: string;
  createdUtc: string;
  state: string;
  reason: string;
  hasUnknownEffects: boolean;
  canReconcile: boolean;
  reconcileToken?: string | null;
  unavailableReason?: string | null;
  claims: GiftcardRecoveryClaim[];
};

export type GiftcardRecoveryResult = {
  message: string;
  resolved: boolean;
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

/** A section other than the order is null when the server could not load it. */
export interface OrderDrawerData {
  order: OrderInfo;
  logs: OrderActivityLog[] | null;
  actions: OrderAction[] | null;
  lockStatus: OrderLockStatus | null;
  giftcardRecovery: GiftcardRecoveryAttempt[] | null;
  reviews: OrderReview[] | null;
}

export type ManagerState = {
  pendingOrderId?: string;
  /** Customer to open in the Customers section, handed over from an order. */
  pendingCustomer?: { email: string; username: string };
  printingEnabled?: boolean;
  filters: ManagerFilters;
  statusList: StatusItem[];
  stores: StoreItem[];
  paymentProviders: PaymentProviderItem[];
  shippingFilterProviders: ShippingProviderItem[];
};

const currentDate = new Date();
const januaryFirstCurrentYear = new Date(currentDate.getFullYear(), 0, 1);

export const managerState: ManagerState = {
  filters: {
    dateFrom: toDateInputValue(januaryFirstCurrentYear),
    dateTo: toDateInputValue(currentDate),
    orderStatus: 'AllOrders',
    store: '',
    paymentProvider: '',
    shippingProvider: '',
    couponCode: '',
    giftcardCode: '',
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
  shippingFilterProviders: [],
};

export class EkomManagerApi {
  async changeOrderStates(orderId: string, expectedRevision: number, action: string, notify: boolean, fulfilledQuantities?: Record<string, number>): Promise<OrderStatusSnapshot> {
    return this.postBody(`/ekom/manager/orderStates/${encodeURIComponent(orderId)}`, { expectedRevision, action, notify, fulfilledQuantities });
  }

  async printingEnabled(): Promise<boolean> {
    try {
      const response = await fetch('/ekom/printer/manager/capabilities', { credentials: 'same-origin' });
      return response.ok && (await response.json()).enabled === true;
    } catch { return false; }
  }

  async config(): Promise<ManagerConfig> {
    return this.getJson('/ekom/backoffice/Config');
  }

  async searchOrders(filters: ManagerFilters, page: number, pageSize = 20): Promise<OrderSearchResult> {
    return this.getJson('/ekom/manager/SearchOrders', {
      ...filters,
      start: filters.dateFrom,
      end: filters.dateTo,
      page,
      pageSize,
    });
  }

  /** Counts for several order views in one request, so the view tabs do not each run a search. */
  async orderViewCounts(filters: ManagerFilters, views: string[]): Promise<OrderViewCount[]> {
    if (!views.length) return [];
    const { orderStatus: _orderStatus, ...shared } = filters;
    return this.getJson('/ekom/manager/OrderViewCounts', {
      ...shared,
      start: filters.dateFrom,
      end: filters.dateTo,
      views,
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

  async orderReviews(orderId: string): Promise<OrderReview[]> {
    const reviews: OrderReview[] = [];
    for (let skip = 0; ; skip += 200) {
      const page = await this.getJson<OrderReview[]>(`/ekom/manager/orders/${encodeURIComponent(orderId)}/reviews`, { skip, take: 200 });
      reviews.push(...page);
      if (page.length < 200) return reviews;
    }
  }

  async reviewOrder(orderId: string, reviewId: string, comment: string, resolved: boolean): Promise<OrderReview> {
    return this.postBody(`/ekom/manager/orders/${encodeURIComponent(orderId)}/reviews/${encodeURIComponent(reviewId)}/review`, { comment, resolved });
  }

  async statusList(): Promise<StatusItem[]> {
    return this.getJson('/ekom/manager/StatusList');
  }

  async stores(): Promise<StoreItem[]> {
    const [stores, choices] = await Promise.all([this.getJson<StoreItem[]>('/ekom/manager/stores'), this.getJson<Array<{ alias: string; currencies: string[]; formats?: Array<CurrencyFormat | null>; dateFormat?: DateFormat | null }>>('/ekom/manager/store-currencies')]);
    return stores.map(store => {
      const choice = choices.find(item => item.alias === store.alias);
      return { ...store, currencies: choice?.currencies || [], currencyFormats: (choice?.formats || []).filter((format): format is CurrencyFormat => !!format), dateFormat: choice?.dateFormat || undefined };
    });
  }

  async paymentProviders(storeAlias: string): Promise<PaymentProviderItem[]> {
    return this.getJson(`/ekom/manager/paymentproviders/${encodeURIComponent(storeAlias)}`);
  }

  async shippingProviders(storeAlias: string): Promise<ShippingProviderItem[]> {
    return this.getJson(`/ekom/manager/shippingproviders/${encodeURIComponent(storeAlias)}`);
  }

  /** The order and everything the drawer shows with it, in one request. */
  async orderDrawer(orderId: string): Promise<OrderDrawerData> {
    return this.getJson(`/ekom/manager/OrderDrawer/${encodeURIComponent(orderId)}`);
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

  async orderLockStatus(orderId: string): Promise<OrderLockStatus> {
    return this.getJson(`/ekom/manager/orderLockStatus/${encodeURIComponent(orderId)}`);
  }

  async releaseOrderLock(orderId: string, releaseToken: string): Promise<OrderLockReleaseResult> {
    return this.postBody(`/ekom/manager/releaseOrderLock/${encodeURIComponent(orderId)}`, { releaseToken });
  }

  async giftcardRecovery(orderId: string): Promise<GiftcardRecoveryAttempt[]> {
    return this.getJson(`/ekom/manager/giftcardRecovery/${encodeURIComponent(orderId)}`);
  }

  async releaseGiftcardRecovery(orderId: string, attemptId: string, claimId: string, token: string): Promise<GiftcardRecoveryResult> {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(orderId)}/release`, { attemptId, claimId, token });
  }

  async reconcileGiftcardRecovery(orderId: string, attemptId: string, token: string): Promise<GiftcardRecoveryResult> {
    return this.postBody(`/ekom/manager/giftcardRecovery/${encodeURIComponent(orderId)}/reconcile`, { attemptId, token });
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

  async updateShippingProvider(orderId: string, providerId: string, customData: Record<string, string>): Promise<OrderInfo> {
    return this.postBody(`/ekom/manager/Order/${encodeURIComponent(orderId)}/ShippingProvider`, { providerId, customData });
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

  async charts(filters: ManagerFilters, signal?: AbortSignal): Promise<ChartData> {
    return this.getJson('/ekom/manager/charts', {
      start: filters.dateFrom,
      end: filters.dateTo,
      orderStatus: filters.orderStatus,
      store: filters.store,
      currency: filters.currency,
    }, signal);
  }

  async mostSoldProducts(filters: ManagerFilters, page: number, pageSize = 20, signal?: AbortSignal): Promise<MostSoldProductsResult> {
    return this.getJson('/ekom/manager/MostSoldProducts', {
      start: filters.dateFrom,
      end: filters.dateTo,
      orderStatus: filters.orderStatus,
      store: filters.store,
      currency: filters.currency,
      page,
      pageSize,
    }, signal);
  }

  private async getJson<T>(url: string, query?: Record<string, unknown>, signal?: AbortSignal): Promise<T> {
    const response = await fetch(url + buildQueryString(query), {
      credentials: 'same-origin',
      signal,
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
    if (response.status === 401 || (response.redirected && /\/Account\/Login/i.test(response.url))) {
      return new Error('Your session has expired. Sign in to Umbraco and reload the manager.');
    }
    if (response.headers?.get('content-type')?.includes('text/html')) {
      return new Error(`${fallbackMessage} Status ${response.status}. Reload the manager and try again.`);
    }
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
    if (Array.isArray(value)) value.forEach(item => params.append(key, String(item)));
    else params.set(key, value == null ? '' : String(value));
  }

  const value = params.toString();
  return value ? `?${value}` : '';
}

export function toDateInputValue(value: Date): string {
  const month = String(value.getMonth() + 1).padStart(2, '0');
  const day = String(value.getDate()).padStart(2, '0');
  return `${value.getFullYear()}-${month}-${day}`;
}

/** How the selected store's culture writes dates; undefined until the stores have loaded. */
export function storeDateFormat(): DateFormat | undefined {
  return managerState.stores.find(store => store.alias === managerState.filters.store)?.dateFormat;
}

type DateParts = { year: number; month: number; day: number; hour: number; minute: number; second: number };

// A plain yyyy-MM-dd is a calendar day, not a moment: keep it as written whatever the time zone.
function toDateParts(value: string | Date): DateParts | undefined {
  if (typeof value === 'string' && /^\d{4}-\d{2}-\d{2}$/.test(value)) {
    return { year: Number(value.slice(0, 4)), month: Number(value.slice(5, 7)), day: Number(value.slice(8, 10)), hour: 0, minute: 0, second: 0 };
  }
  const date = value instanceof Date ? value : new Date(value);
  if (Number.isNaN(date.getTime())) return undefined;
  return { year: date.getFullYear(), month: date.getMonth() + 1, day: date.getDate(), hour: date.getHours(), minute: date.getMinutes(), second: date.getSeconds() };
}

/** Applies a .NET custom date pattern (the form the server sends) using the store culture's names. */
export function formatDatePattern(parts: DateParts, pattern: string, format: DateFormat): string {
  const pad = (value: number): string => String(value).padStart(2, '0');
  return pattern.replace(/'([^']*)'|\\(.)|d{1,4}|M{1,4}|y{1,5}|H{1,2}|h{1,2}|m{1,2}|s{1,2}|t{1,2}|\/|:/g, (token: string, quoted?: string, escaped?: string) => {
    if (quoted !== undefined) return quoted;
    if (escaped !== undefined) return escaped;
    switch (token) {
      case 'd': return String(parts.day);
      case 'dd': return pad(parts.day);
      case 'ddd': case 'dddd': return format.shortestDayNames[new Date(parts.year, parts.month - 1, parts.day).getDay()] || '';
      case 'M': return String(parts.month);
      case 'MM': return pad(parts.month);
      case 'MMM': return format.abbreviatedMonthNames[parts.month - 1] || String(parts.month);
      case 'MMMM': return format.monthNames[parts.month - 1] || String(parts.month);
      case 'yy': return pad(parts.year % 100);
      case 'H': return String(parts.hour);
      case 'HH': return pad(parts.hour);
      case 'h': return String(parts.hour % 12 || 12);
      case 'hh': return pad(parts.hour % 12 || 12);
      case 'm': return String(parts.minute);
      case 'mm': return pad(parts.minute);
      case 's': return String(parts.second);
      case 'ss': return pad(parts.second);
      case 't': return (parts.hour < 12 ? format.amDesignator : format.pmDesignator).slice(0, 1);
      case 'tt': return parts.hour < 12 ? format.amDesignator : format.pmDesignator;
      case '/': return format.dateSeparator || '/';
      case ':': return format.timeSeparator || ':';
      default: return String(parts.year);
    }
  });
}

/** A date without time, in the store culture's short date form. */
export function formatDay(value?: string | Date): string {
  if (!value) return '';
  const parts = toDateParts(value);
  if (!parts) return String(value);
  const format = storeDateFormat();
  if (format) return formatDatePattern(parts, format.shortDate, format);
  return new Date(parts.year, parts.month - 1, parts.day).toLocaleDateString();
}

/** Day and month ("5 Jan") or month and year ("January 2026") as the store culture orders them. */
export function formatDayPart(value: string | Date, kind: 'monthDay' | 'yearMonth' | 'month', abbreviated = true): string {
  const parts = toDateParts(value);
  if (!parts) return String(value);
  const format = storeDateFormat();
  const date = new Date(Date.UTC(parts.year, parts.month - 1, parts.day));
  if (!format) {
    return date.toLocaleDateString('en-GB', { timeZone: 'UTC', month: abbreviated ? 'short' : 'long', ...(kind === 'monthDay' ? { day: 'numeric' } : kind === 'yearMonth' ? { year: 'numeric' } : {}) });
  }
  if (kind === 'month') return (abbreviated ? format.abbreviatedMonthNames : format.monthNames)[parts.month - 1] || String(parts.month);
  const pattern = kind === 'monthDay' ? format.monthDay : format.yearMonth;
  return formatDatePattern(parts, abbreviated ? pattern.replace(/M{4}/g, 'MMM') : pattern, format);
}

export function formatDate(value?: string | Date): string {
  if (!value) {
    return '';
  }

  const date = value instanceof Date ? value : new Date(value);

  if (Number.isNaN(date.getTime())) {
    return String(value);
  }

  const format = storeDateFormat();
  const parts = toDateParts(date);
  if (format && parts) {
    return `${formatDatePattern(parts, format.shortDate, format)} ${formatDatePattern(parts, format.shortTime, format)}`;
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

const orderCustomDataStyles = `
  .ekmOrderCustomData { display: flex; flex-wrap: wrap; gap: 8px; margin: 24px 0; padding: 14px 16px; border: 1px solid #e1e3e8; border-radius: 12px; }
  .ekmOrderCustomData__item { display: flex; flex-wrap: wrap; align-items: baseline; gap: 4px 8px; min-width: 0; max-width: 100%; padding: 7px 12px; border: 1px solid #e1e3e8; border-radius: 8px; background: #f6f7f9; line-height: 1.5; break-inside: avoid; }
  .ekmOrderCustomData__key { margin: 0; color: #686d7c; font-weight: 400; overflow-wrap: anywhere; }
  .ekmOrderCustomData__value { margin: 0; min-width: 0; color: #252830; font-weight: 600; white-space: pre-wrap; overflow-wrap: anywhere; }
`;

/** Print an isolated document so backoffice shadow roots and scroll containers cannot clip it. */
export async function printOrderDocument(content: string, title: string): Promise<void> {
  const frame = document.createElement('iframe');
  frame.title = title;
  frame.setAttribute('aria-hidden', 'true');
  frame.style.cssText = 'position:fixed;left:-10000px;top:0;width:800px;height:600px;border:0;';
  try {
    const loaded = new Promise<void>((resolve, reject) => {
      frame.onload = () => resolve();
      frame.onerror = () => reject(new Error('Unable to load the print document.'));
    });
    frame.srcdoc = createOrderPrintDocument(content, title);
    document.body.append(frame);
    await loaded;
    const printWindow = frame.contentWindow;
    if (!printWindow) throw new Error('Print window unavailable.');
    await printWindow.document.fonts.ready;
    printWindow.addEventListener('afterprint', () => frame.remove(), { once: true });
    printWindow.focus();
    printWindow.print();
  } catch (error) {
    frame.remove();
    throw error;
  }
}

export function createOrderPrintDocument(content: string, title: string): string {
  const template = document.createElement('template');
  template.innerHTML = content;
  template.content.querySelectorAll('button, input, select, .ekmOrderGiftcards__actions, .ekmOrderLineThumbnail').forEach(element => element.remove());
  template.content.querySelectorAll('details').forEach(element => element.setAttribute('open', ''));
  template.content.querySelectorAll('summary').forEach(element => element.remove());
  // Preserve product names without backoffice links or browser-added link URLs.
  template.content.querySelectorAll('a').forEach(element => element.replaceWith(...Array.from(element.childNodes)));
  return `<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(title)}</title><style>
    ${orderCustomDataStyles}
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
  </style></head><body>${template.innerHTML}</body></html>`;
}

export function downloadBlob(blob: Blob, filename: string): void {
  const url = URL.createObjectURL(blob);
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  // Keep the backoffice router from handling this native download as navigation.
  anchor.addEventListener('click', event => event.stopPropagation());
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  URL.revokeObjectURL(url);
}

const baseManagerStyles = `
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

  ${orderCustomDataStyles}
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

// Scope the legacy rules too: these elements render in light DOM, where :host is ineffective.
// CSS nesting keeps generic input/button/table rules inside the manager boundary.
const managerScope = ':is(ekom-orders-section-view, ekom-analytics-section-view, ekom-customers-section-view, ekom-printing-section-view)';
const themeColorTokens: Record<string, string> = {
  '#fff': '--ekom-surface', '#f6f4f4': '--ekom-bg',
  '#f6f6f8': '--ekom-surface-alt', '#f8fafc': '--ekom-surface-alt', '#eef3f8': '--ekom-surface-alt',
  '#edf8f0': '--ekom-surface-alt', '#fff4e5': '--ekom-surface-alt',
  '#000': '--ekom-text', '#111': '--ekom-text', '#1b264f': '--ekom-text',
  '#46607a': '--ekom-muted', '#515054': '--ekom-muted', '#666': '--ekom-muted', '#686b7d': '--ekom-muted',
  '#d8d7d9': '--ekom-border', '#ddd': '--ekom-border', '#bbb': '--ekom-border',
  '#e6e8ef': '--ekom-border', '#e9e9eb': '--ekom-border', '#eee': '--ekom-border', '#f0f0f0': '--ekom-border',
  '#247f45': '--ekom-positive', '#ad5f00': '--ekom-warning',
};
const themedManagerStyles = baseManagerStyles.replaceAll(':host', '&')
  .replace(/#[0-9a-f]{3,8}\b/gi, color => themeColorTokens[color.toLowerCase()] ? `var(${themeColorTokens[color.toLowerCase()]})` : color);

// Loading patterns from the Ekom Manager design: shimmering placeholders shaped like the content they stand in for.
const loadingStyles = `
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
`;

export const managerStyles = `@keyframes ekom-shim { 0% { background-position: -320px 0; } 100% { background-position: 320px 0; } }
@keyframes ekom-spin { to { transform: rotate(360deg); } }
${managerScope} {
  ${baseManagerStyles.replaceAll(':host', '&')}
  ${loadingStyles}
}
${managerScope}:not([classic]) {
  ${themedManagerStyles}
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

/** Display evidence gaps without manufacturing an Unknown business enum value. */
export function renderStatusBadge(value: string | null | undefined): string {
  const tone = ['Paid', 'Fulfilled', 'Closed'].includes(value || '') ? 'positive'
    : ['Failed', 'Cancelled'].includes(value || '') ? 'negative'
    : ['Pending', 'PartiallyPaid', 'PartiallyFulfilled', 'PartiallyRefunded'].includes(value || '') ? 'warning' : 'neutral';
  const label = value ? value.replace(/([a-z])([A-Z])/g, '$1 $2') : 'Needs review';
  return `<span class="ekom-badge ekom-badge--${tone}">${escapeHtml(label)}</span>`;
}

export function renderOrderStatusSummary(status?: OrderStatusSnapshot): string {
  return `<dl class="ekom-statuses"><div><dt>Order</dt><dd>${renderStatusBadge(status?.orderStatus)}</dd></div><div><dt>Payment</dt><dd>${renderStatusBadge(status?.paymentStatus)}</dd></div><div><dt>Fulfillment</dt><dd>${renderStatusBadge(status?.fulfillmentStatus)}</dd></div></dl>`;
}

export function formatAnalyticsAmount(value: number, currency?: StoreItem['currency']): string {
  if (currency?.format) return formatWithStoreCulture(value, currency.format);
  const code = currency?.isoCurrencySymbol;
  const digits = currency?.currencyDecimalDigits ?? (code && /^[A-Z]{3}$/.test(code) ? new Intl.NumberFormat(undefined, { style: 'currency', currency: code }).resolvedOptions().maximumFractionDigits : 2);
  // Format in the store currency's own culture, as Orders does, so both views read the same.
  // Without a usable culture, fall back to the viewer's locale with an unambiguous currency code.
  const culture = currency?.currencyValue;
  let locale: string | undefined;
  try { if (culture && !/^[A-Z]{3}$/.test(culture)) locale = Intl.NumberFormat.supportedLocalesOf(culture)[0]; }
  catch { /* Not a culture name. */ }
  return new Intl.NumberFormat(locale, {
    minimumFractionDigits: digits,
    maximumFractionDigits: digits,
    ...(code && /^[A-Z]{3}$/.test(code) ? { style: 'currency', currency: code, ...(locale ? {} : { currencyDisplay: 'code' }) } : {}),
  }).format(value);
}

export function renderLineChart(canvas: HTMLCanvasElement, series: ChartSeries, color: string,
  options: { templateStyle?: boolean; formatAxisValue?: (value: number) => string; formatLabel?: (index: number) => string; currency?: StoreItem['currency']; integer?: boolean; formatValue?: (value: number) => string; comparison?: ChartSeries; columnHover?: boolean; onSelect?: (index: number) => void } = {}): void {
  const rect = canvas.getBoundingClientRect();
  const dpr = window.devicePixelRatio || 1;
  const width = rect.width || 300;
  const height = rect.height || 320;
  canvas.width = width * dpr;
  canvas.height = height * dpr;
  const ctx = canvas.getContext('2d');
  if (!ctx) return;
  ctx.scale(dpr, dpr);
  ctx.font = options.templateStyle ? '10px "Ekom Mono", monospace' : '12px sans-serif';


  const points = series.points || [];
  const values = points.map(point => point.y == null ? null : Number(point.y));
  const comparison = options.comparison?.points || [];
  const allValues = [...values, ...comparison.map(point => point.y)].filter((value): value is number => value != null);
  const min = Math.min(0, ...allValues);
  const rawMax = Math.max(1, ...allValues);
  const tickUnit = 10 ** (Math.floor(Math.log10(rawMax / 4)) - 1);
  const max = options.integer ? Math.ceil(rawMax / 4) * 4 : options.templateStyle ? Math.ceil(rawMax / 4 / tickUnit) * tickUnit * 4 : rawMax;
  const formatValue = (value: number) => options.formatValue ? options.formatValue(value) : options.currency
    ? formatAnalyticsAmount(value, options.currency)
    : value.toLocaleString(undefined, { maximumFractionDigits: options.integer ? 0 : 2 });
  const formatAxisValue = options.formatAxisValue ?? formatValue;
  const ticks = Array.from({ length: 5 }, (_, i) => max - (max - min) * i / 4);
  const left = options.templateStyle ? 1 : Math.max(48, ...ticks.map(value => ctx.measureText(formatAxisValue(value)).width + 12));
  const right = width - (options.templateStyle ? 1 : 16);
  const top = options.templateStyle ? 8 : 20;
  const bottom = height - (options.templateStyle ? 26 : 52);
  const positions = points.map((point, index) => ({
    x: points.length > 1 ? left + (right - left) * index / (points.length - 1) : (left + right) / 2,
    y: values[index] == null ? null : bottom - (values[index]! - min) / (max - min) * (bottom - top),
    date: point.x || series.labels?.[index] || '',
    axisLabel: options.formatLabel?.(index) ?? point.x ?? series.labels?.[index] ?? '',
    value: values[index],
  }));

  const draw = (active = -1) => {
    ctx.clearRect(0, 0, width, height);
    ctx.font = options.templateStyle ? '10px "Ekom Mono", monospace' : '12px sans-serif';
    const theme = typeof getComputedStyle === 'function' ? getComputedStyle(canvas) : undefined;
    const textColor = theme?.getPropertyValue('--ekom-muted').trim() || theme?.getPropertyValue('--uui-color-text-alt').trim() || '#555';
    const gridColor = theme?.getPropertyValue('--ekom-border').trim() || '#e5e5e5';
    // The screenshot's green is kept in light mode; use the themed accent on dark surfaces.
    const foreground = theme?.getPropertyValue('--uui-color-text').trim() || '';
    const foregroundHex = /^#([0-9a-f]{6})$/i.exec(foreground)?.[1];
    const lightText = foregroundHex ? [0,2,4].reduce((sum, offset) => sum + parseInt(foregroundHex.slice(offset, offset + 2), 16), 0) > 420 : false;
    const lineColor = options.templateStyle && !lightText ? '#10694a' : color;
    ctx.lineWidth = 1;
    ctx.textAlign = options.templateStyle ? 'left' : 'right';
    ticks.forEach((value, index) => {
      const y = top + (bottom - top) * index / 4;
      ctx.fillStyle = textColor;
      ctx.fillText(formatAxisValue(value), options.templateStyle ? left : left - 8, options.templateStyle ? Math.min(bottom, y + 3) : y + 4);
      if (options.templateStyle && (index === 0 || index === 4)) return;
      ctx.globalAlpha = options.templateStyle ? .5 : 1;
      ctx.strokeStyle = gridColor;
      ctx.beginPath();
      ctx.moveTo(left, y);
      ctx.lineTo(right, y);
      ctx.stroke();
      ctx.globalAlpha = 1;
    });
    const labelWidth = Math.max(90, ...positions.map(point => ctx.measureText(point.axisLabel).width + 32));
    const labelCount = Math.max(2, Math.floor((right - left) / labelWidth));
    const labelIndices = new Set(Array.from({ length: Math.min(labelCount, positions.length) }, (_, i) =>
      Math.round(i * (positions.length - 1) / Math.max(1, Math.min(labelCount, positions.length) - 1))));
    ctx.fillStyle = textColor;
    positions.forEach((point, index) => {
      if (!labelIndices.has(index)) return;
      ctx.textAlign = positions.length === 1 ? 'center' : index === 0 ? 'left' : index === positions.length - 1 ? 'right' : 'center';
      ctx.fillText(point.axisLabel, point.x, bottom + 24);
    });
    const line = (data: Array<{ x: number; y: number | null }>, dashed: boolean) => {
      ctx.strokeStyle = dashed ? '#9b9fad' : lineColor;
      ctx.lineWidth = options.templateStyle ? (dashed ? 1.5 : 2.5) : 2;
      ctx.lineJoin = 'round';
      ctx.lineCap = 'round';
      ctx.setLineDash?.(dashed ? [4, 5] : []);
      ctx.beginPath();
      let connected = false;
      data.forEach(point => {
        if (point.y == null) { connected = false; return; }
        if (connected) ctx.lineTo(point.x, point.y);
        else ctx.moveTo(point.x, point.y);
        connected = true;
      });
      ctx.stroke();
      ctx.setLineDash?.([]);
    };
    if (options.templateStyle && positions.length > 1) {
      const gradient = ctx.createLinearGradient(0, top, 0, bottom);
      gradient.addColorStop(0, 'rgba(16,105,74,.22)');
      gradient.addColorStop(1, 'rgba(16,105,74,0)');
      ctx.fillStyle = gradient;
      // Close each contiguous run separately so missing amounts remain genuine gaps.
      let run: Array<{ x: number; y: number }> = [];
      const fillRun = () => {
        if (run.length > 1) {
          ctx.beginPath(); ctx.moveTo(run[0].x, bottom);
          run.forEach(point => ctx.lineTo(point.x, point.y));
          ctx.lineTo(run[run.length - 1].x, bottom); ctx.closePath(); ctx.fill();
        }
        run = [];
      };
      positions.forEach(point => { if (point.y == null) fillRun(); else run.push({ x: point.x, y: point.y }); });
      fillRun();
    }
    if (comparison.length) line(positions.map((point, index) => ({
      x: point.x,
      y: comparison[index]?.y == null ? null : bottom - (Number(comparison[index].y) - min) / (max - min) * (bottom - top),
    })), true);
    line(positions, false);
    ctx.fillStyle = lineColor;
    positions.forEach((point, index) => {
      if (point.y == null || (options.templateStyle && positions.length > 1 && index !== active)) return;
      ctx.beginPath();
      ctx.arc(point.x, point.y, index === active ? 5 : 3, 0, Math.PI * 2);
      ctx.fill();
    });
    if (!positions.length) {
      ctx.textAlign = 'center';
      ctx.fillText('No data for this period', (left + right) / 2, height / 2);
    }
    const point = positions[active];
    if (point) {
      const lines = [point.date, point.value == null ? '—' : formatValue(point.value)];
      if (comparison[active]) {
        const previous = comparison[active];
        lines.push(`Previous: ${previous.x || ''}`, previous.y == null ? '—' : formatValue(previous.y));
      }
      const boxWidth = Math.max(...lines.map(line => ctx.measureText(line).width)) + 20;
      const x = Math.max(0, Math.min(width - boxWidth, point.x - boxWidth / 2));
      const boxHeight = lines.length * 18 + 12;
      const pointY = point.y ?? bottom;
      const y = Math.max(0, Math.min(height - boxHeight, pointY > boxHeight + 20 ? pointY - boxHeight - 12 : pointY + 12));
      ctx.fillStyle = '#222';
      ctx.fillRect(x, y, boxWidth, boxHeight);
      ctx.fillStyle = '#fff';
      ctx.textAlign = 'left';
      lines.forEach((line, index) => ctx.fillText(line, x + 10, y + 19 + index * 18));
    }
  };
  const hit = (event: MouseEvent) => {
    const bounds = canvas.getBoundingClientRect();
    const x = (event.clientX - bounds.left) * width / bounds.width;
    const y = (event.clientY - bounds.top) * height / bounds.height;
    if (options.columnHover) {
      if (!positions.length || x < left || x > right || y < top || y > bottom) return -1;
      return positions.length === 1 ? 0 : Math.round((x - left) / (right - left) * (positions.length - 1));
    }
    let active = -1;
    let distance = 16;
    positions.forEach((point, index) => {
      if (point.y == null) return;
      const current = Math.hypot(point.x - x, point.y - y);
      if (current < distance) { active = index; distance = current; }
    });
    return active;
  };
  canvas.onmousemove = event => {
    const active = hit(event);
    canvas.style.cursor = active >= 0 ? 'pointer' : 'default';
    draw(active);
  };
  canvas.onclick = options.onSelect ? event => {
    const index = hit(event);
    if (index >= 0) options.onSelect?.(index);
  } : null;
  canvas.onmouseleave = () => { canvas.style.cursor = 'default'; draw(); };
  draw();
}

export const orderViews = [
  { key: 'AllOrders', label: 'All' },
  { key: 'NeedsReview', label: 'Needs review' },
  { key: 'states:Open;;Unfulfilled,Picking,Picked,Packed,PartiallyFulfilled', label: 'Unfulfilled' },
  { key: 'states:Open;Pending,Authorized,PartiallyPaid,Failed;', label: 'Unpaid' },
  { key: 'states:Open;;', label: 'Open' },
  { key: 'states:Closed;;', label: 'Closed' },
];

export function ensureManagerCurrency(): void {
  const store = managerState.stores.find(item => item.alias === managerState.filters.store);
  const choices = store?.currencies || [];
  if (!choices.includes(managerState.filters.currency || '')) {
    const preferred = store?.currency?.isoCurrencySymbol;
    managerState.filters.currency = choices.find(code => code === preferred) || choices[0] || '';
  }
}

export function renderStoreCurrencySelector(storeAttribute = 'data-field'): string {
  const selected = managerState.stores.find(store => store.alias === managerState.filters.store);
  const currency = managerState.filters.currency || '';
  return `<div class="ekom-store-switch"><span>Store</span><details class="ekom-dropdown ekom-store-currency"><summary aria-label="Store and currency"><strong>${escapeHtml(selected?.title || 'Select store')}</strong>${currency ? `<span class="ekom-currency-code">${escapeHtml(currency)}</span>` : ''}<span class="ekom-store-chevron">▾</span></summary><div class="ekom-currency-popover"><div class="ekom-currency-heading"><strong>Store ·<br>Currency</strong><span>Amounts shown in the selected currency</span></div>${managerState.stores.map(store => `<section class="ekom-currency-store ${store.alias === selected?.alias ? 'is-selected' : ''}" aria-label="${escapeHtml(store.title)}"><h3>${escapeHtml(store.title)}</h3><div class="ekom-currency-choices">${(store.currencies || []).map(code => `<button type="button" data-header-store="${escapeHtml(store.alias)}" data-header-currency="${escapeHtml(code)}" aria-label="${escapeHtml(store.title)} — ${escapeHtml(code)}" aria-pressed="${store.alias === selected?.alias && code === currency}">${escapeHtml(code)}${store.alias === selected?.alias && code === currency ? '<span aria-hidden="true">✓</span>' : ''}</button>`).join('') || '<span class="ekom-currency-empty">No currencies configured or recorded</span>'}</div></section>`).join('')}</div></details><select hidden aria-label="Store" ${storeAttribute}="store">${managerState.stores.map(store => `<option value="${escapeHtml(store.alias)}" ${store.alias === selected?.alias ? 'selected' : ''}>${escapeHtml(store.title)}</option>`).join('')}</select></div>`;
}

/** Keeps the open order or customer in the address bar, so the page can be linked to. An empty value removes the parameter. */
export function setManagerLink(params: Record<string, string | undefined>): void {
  if (typeof location === 'undefined' || typeof history === 'undefined') return;
  const url = new URL(location.href);
  for (const [name, value] of Object.entries(params)) {
    if (value) url.searchParams.set(name, value); else url.searchParams.delete(name);
  }
  history.replaceState(history.state, '', url.pathname + url.search + url.hash);
}

export function renderManagerHeader(active: string, storeAttribute = 'data-field', showStore = true): string {
  if (typeof document !== 'undefined' && !document.querySelector('link[data-ekom-fonts]')) {
    const link = document.createElement('link'); link.rel = 'stylesheet'; link.href = '/App_Plugins/Ekom/dist/manager-fonts.css'; link.dataset.ekomFonts = ''; document.head.append(link);
  }
  const sections = ['orders', 'analytics', 'customers', ...(managerState.printingEnabled || active === 'printing' ? ['printing'] : [])];
  const route = typeof location === 'undefined' ? '/umbraco/section/ekommanager' : location.pathname.replace(/\/view\/(orders|analytics|customers|printing)\/?$/, '').replace(/\/$/, '');
  return `<header class="ekom-design-header"><img src="/App_Plugins/Ekom/dist/ekom-logo.svg" alt="Ekom" class="ekom-design-logo"><nav aria-label="Ekom sections" class="ekom-section-switch">${sections.map(section => `<a href="${escapeHtml(route + '/view/' + section)}" ${section === active ? 'aria-current="page"' : ''}>${section[0].toUpperCase() + section.slice(1)}</a>`).join('')}</nav>${showStore ? renderStoreCurrencySelector(storeAttribute) : '<span class="ekom-store-switch">Accessible stores</span>'}</header>`;
}

/** Preserve custom status filters when changing sections instead of silently selecting another scope. */
export function renderOrderViewOptions(value: string): string {
  const views = [...orderViews];
  if (!views.some(view => view.key === value)) views.push({ key: value, label: value.startsWith('states:') ? 'Custom status filters' : managerState.statusList.find(status => getStatusValue(status) === value)?.label || value });
  return views.map(view => `<option value="${escapeHtml(view.key)}" ${view.key === value ? 'selected' : ''}>${escapeHtml(view.label)}</option>`).join('');
}

export function dateRangePresets(now = new Date()): Array<{ label: string; from: string; to: string }> {
  const iso = (date: Date) => `${date.getFullYear()}-${String(date.getMonth() + 1).padStart(2, '0')}-${String(date.getDate()).padStart(2, '0')}`;
  const offset = (days: number) => new Date(now.getFullYear(), now.getMonth(), now.getDate() + days);
  const today = iso(now);
  return [
    { label: 'Today', from: today, to: today },
    { label: 'Yesterday', from: iso(offset(-1)), to: iso(offset(-1)) },
    { label: 'Last 7 days', from: iso(offset(-6)), to: today },
    { label: 'Last 30 days', from: iso(offset(-29)), to: today },
    { label: 'This month', from: iso(new Date(now.getFullYear(), now.getMonth(), 1)), to: today },
    { label: 'Last month', from: iso(new Date(now.getFullYear(), now.getMonth() - 1, 1)), to: iso(new Date(now.getFullYear(), now.getMonth(), 0)) },
    { label: 'This quarter', from: iso(new Date(now.getFullYear(), Math.floor(now.getMonth() / 3) * 3, 1)), to: today },
    { label: 'This year', from: iso(new Date(now.getFullYear(), 0, 1)), to: today },
  ];
}

const skeletonBar = (width: string, height = 12): string => `<span class="ekom-skeleton" style="width:${width};height:${height}px"></span>`;

/** The selected period as the date range picker names it, for loading messages. */
export function dateRangeLabel(): string {
  const { dateFrom, dateTo } = managerState.filters;
  return dateRangePresets().find(preset => preset.from === dateFrom && preset.to === dateTo)?.label || (storeDateFormat() ? `${formatDay(dateFrom)} – ${formatDay(dateTo)}` : `${dateFrom} – ${dateTo}`);
}

/** Spinner line announcing what is loading; the only part of a loading state read to screen readers. */
export function renderLoadingStatus(section: string): string {
  return renderLoadingLine(`Loading ${section} for ${dateRangeLabel()}…`);
}

/** Spinner with a message, announced to screen readers. */
export function renderLoadingLine(message: string): string {
  return `<p class="ekom-loading-status" role="status"><span class="ekom-loading-spinner" aria-hidden="true"></span>${escapeHtml(message)}</p>`;
}

/** A drawer's close button with the keyboard hint the design shows beside it. */
export function renderDrawerClose(button: string): string {
  return `<div class="ekom-drawer-close"><span class="ekom-esc-hint" aria-hidden="true"><strong>Esc</strong> close</span>${button}</div>`;
}

/** Body of a detail drawer while it loads: the same two-column placeholder for orders and customers. */
export function renderDrawerLoading(): string {
  return `<div class="ekom-order-columns ekom-drawer-loading"><main class="ekom-order-main">${renderLoadingPanel(4)}${renderLoadingPanel(3)}${renderLoadingPanel(2)}</main><aside class="ekom-customer-sidebar">${renderLoadingPanel(5)}</aside></div>`;
}

/** Placeholder card: a short title bar followed by full-width lines. */
export function renderLoadingPanel(lines = 3): string {
  return `<div aria-hidden="true" class="ekom-loading-panel">${skeletonBar('28%')}${skeletonBar('100%').repeat(lines)}</div>`;
}

/** Section placeholder: status line, summary cards, a chart-sized block and a few lines of text. */
export function renderSectionLoading(section: string, cards: number, blockHeight: number): string {
  const card = `<div class="ekom-loading-card">${skeletonBar('40%')}${skeletonBar('55%', 22)}</div>`;
  return `<div class="ekom-loading">${renderLoadingStatus(section)}<div aria-hidden="true" class="ekom-loading-cards" style="grid-template-columns:repeat(${cards},minmax(0,1fr))">${card.repeat(cards)}</div>
    <div aria-hidden="true" class="ekom-loading-panel" style="height:${blockHeight}px">${skeletonBar('30%')}<span class="ekom-skeleton ekom-skeleton-block"></span></div>
    <div aria-hidden="true" class="ekom-loading-panel">${skeletonBar('28%')}${skeletonBar('100%').repeat(3)}</div></div>`;
}

/** Placeholder rows for a table that is still being calculated. */
export function renderRowsLoading(message: string, rows = 4): string {
  const row = `<div aria-hidden="true"><span class="ekom-skeleton" style="flex:1"></span>${skeletonBar('60px')}${skeletonBar('110px')}</div>`;
  return `<div class="ekom-loading-rows">${row.repeat(rows)}<p role="status">${escapeHtml(message)}</p></div>`;
}

/** Placeholder rows for the orders list, laid out on the list's own grid. */
export function renderOrderRowsLoading(rows = 6): string {
  const row = `<div class="ekom-list-row ekom-list-skeleton-row" aria-hidden="true">${['60%', '70%', '55%', '80%', '70%', '60%'].map(width => skeletonBar(width)).join('')}<span></span></div>`;
  return row.repeat(rows) + '<span class="ekom-sr-only" role="status">Loading orders…</span>';
}

export function renderDateRange(): string {
  return `<ekom-date-range from="${escapeHtml(managerState.filters.dateFrom)}" to="${escapeHtml(managerState.filters.dateTo)}"></ekom-date-range>`;
}

// The store and currency menu shown in each document. Escape or a press anywhere outside the
// open menu closes it, wherever focus is. Each render binds a new menu, so only the latest is kept.
const storeMenus = new WeakMap<Document, HTMLDetailsElement>();

function watchStoreMenu(menu: HTMLDetailsElement): void {
  const page = menu.ownerDocument;
  const watched = storeMenus.has(page);
  storeMenus.set(page, menu);
  if (watched) return;
  const current = (): HTMLDetailsElement | undefined => {
    const latest = storeMenus.get(page);
    return latest?.open && latest.isConnected ? latest : undefined;
  };
  page.addEventListener('pointerdown', event => {
    const open = current();
    if (open && !event.composedPath().includes(open)) open.open = false;
  }, true);
  page.addEventListener('keydown', event => {
    const open = current();
    if (!open || event.key !== 'Escape') return;
    open.open = false;
    open.querySelector('summary')?.focus();
    event.preventDefault();
    event.stopPropagation();
  }, true);
}

export function bindDateRange(host: HTMLElement | ShadowRoot, apply: () => void): void {
  registerDateRange();
  const storeMenu = host.querySelector<HTMLDetailsElement>('.ekom-store-currency');
  if (storeMenu) watchStoreMenu(storeMenu);
  host.querySelectorAll<HTMLButtonElement>('[data-header-store]').forEach(button => button.addEventListener('click', () => {
    const select = host.querySelector<HTMLSelectElement>('[data-field="store"], [data-filter="store"]');
    if (!select) return;
    managerState.filters.currency = button.dataset.headerCurrency || '';
    button.closest('details')?.removeAttribute('open');
    select.value = button.dataset.headerStore || '';
    select.dispatchEvent(new Event('change', { bubbles: true }));
  }));
  host.querySelectorAll<HTMLInputElement>('[data-shared-status]').forEach(input => input.addEventListener('change', () => {
    const groups = [0, 1, 2].map(index => Array.from(host.querySelectorAll<HTMLInputElement>(`[data-shared-status="${index}"]:checked`), checkbox => checkbox.value).join(','));
    managerState.filters.orderStatus = groups.some(Boolean) ? 'states:' + groups.join(';') : 'AllOrders';
    apply();
  }));
  host.querySelector('ekom-date-range')?.addEventListener('date-range-apply', event => {
    const range = (event as CustomEvent<{ from: string; to: string }>).detail;
    managerState.filters.dateFrom = range.from;
    managerState.filters.dateTo = range.to;
    apply();
  });
}

// Lazy registration keeps shared data helpers usable by classic clients and server-side tests.
export function registerDateRange(): void {
  if (typeof customElements === 'undefined' || !customElements.get || customElements.get('ekom-date-range')) return;
  class EkomDateRange extends HTMLElement {
    private readonly root = this.attachShadow({ mode: 'open' });
    private open = false;
    private from = '';
    private to = '';
    private month = 0;
    private readonly reposition = () => {
      const popover = this.root.querySelector<HTMLElement>('.popover');
      const anchor = this.root.querySelector<HTMLElement>('.anchor');
      const view = this.ownerDocument.defaultView;
      if (!popover || !anchor || !view) return;
      if (view.innerWidth <= 650) { popover.style.left = ''; return; }
      const left = anchor.getBoundingClientRect().left;
      popover.style.left = `${Math.min(0, Math.max(24 - left, view.innerWidth - 24 - left - popover.getBoundingClientRect().width))}px`;
    };
    private readonly outside = (event: Event) => { if (this.open && !event.composedPath().includes(this)) this.close(false); };
    override connectedCallback(): void {
      this.render();
      this.ownerDocument.addEventListener('pointerdown', this.outside);
      this.ownerDocument.defaultView?.addEventListener('resize', this.reposition);
    }
    override disconnectedCallback(): void { this.ownerDocument.removeEventListener('pointerdown', this.outside); this.ownerDocument.defaultView?.removeEventListener('resize', this.reposition); }
    private reset(): void {
      this.from = this.getAttribute('from') || '';
      this.to = this.getAttribute('to') || '';
      const date = /^\d{4}-\d{2}-\d{2}$/.test(this.from) ? new Date(`${this.from}T12:00:00`) : new Date();
      this.month = date.getFullYear() * 12 + date.getMonth();
    }
    private close(focus = true): void {
      this.open = false; this.render();
      if (focus) this.root.querySelector<HTMLButtonElement>('[data-toggle]')?.focus();
    }
    private date(value: string): string { return !value ? 'Pick a date' : storeDateFormat() ? formatDay(value) : `${value.slice(8, 10)}.${value.slice(5, 7)}.${value.slice(0, 4)}`; }
    private calendar(index: number): string {
      const first = new Date(Math.floor(index / 12), index % 12, 1);
      const lead = (first.getDay() + 6) % 7;
      const days = new Date(first.getFullYear(), first.getMonth() + 1, 0).getDate();
      return `<section class="month"><h3>${escapeHtml(storeDateFormat() ? formatDayPart(first, 'yearMonth', false) : first.toLocaleDateString('en', { month: 'long', year: 'numeric' }))}</h3><div class="days">${(storeDateFormat() ? [1, 2, 3, 4, 5, 6, 0].map(day => escapeHtml(storeDateFormat()!.shortestDayNames[day] || '')) : ['Mo','Tu','We','Th','Fr','Sa','Su']).map(day => `<span class="weekday">${day}</span>`).join('')}${'<span></span>'.repeat(lead)}${Array.from({ length: days }, (_, i) => {
        const value = `${first.getFullYear()}-${String(first.getMonth() + 1).padStart(2, '0')}-${String(i + 1).padStart(2, '0')}`;
        const edge = value === this.from || value === this.to;
        const inside = this.from && this.to && value > this.from && value < this.to;
        return `<button type="button" data-day="${value}" aria-label="${value}" aria-pressed="${edge || !!inside}" class="day ${edge ? 'edge' : inside ? 'inside' : ''} ${value === this.from ? 'start' : ''} ${value === this.to ? 'end' : ''} ${!this.to || this.to === this.from ? 'single' : ''}">${i + 1}</button>`;
      }).join('')}</div></section>`;
    }
    private render(): void {
      const savedFrom = this.getAttribute('from') || '';
      const savedTo = this.getAttribute('to') || '';
      const presets = dateRangePresets();
      const label = presets.find(p => p.from === savedFrom && p.to === savedTo)?.label || 'Custom';
      renderManagerContent(this.root, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/date-range.css"><div class="range"><span class="label">Date range</span><div class="anchor"><button type="button" class="trigger" data-toggle aria-haspopup="dialog" aria-expanded="${this.open}"><strong>${label}</strong><span class="dates">${this.date(savedFrom)} – ${this.date(savedTo)}</span><span class="chevron">▾</span></button>${this.open ? `<div class="popover" role="dialog" aria-label="Choose date range"><nav aria-label="Date presets">${presets.map((preset, index) => `<button type="button" data-preset="${index}" aria-pressed="${preset.from === this.from && preset.to === this.to}">${preset.label}<span>${preset.from === this.from && preset.to === this.to ? '✓' : ''}</span></button>`).join('')}</nav><div class="custom"><div class="draft"><div class="${!this.from ? 'active' : ''}"><span>From</span><strong>${this.date(this.from)}</strong></div><span>→</span><div class="${this.from && !this.to ? 'active' : ''}"><span>To</span><strong>${this.date(this.to)}</strong></div></div><div class="calendars"><button type="button" class="previous" data-month="-1" aria-label="Previous month">‹</button><button type="button" class="next" data-month="1" aria-label="Next month">›</button>${this.calendar(this.month)}${this.calendar(this.month + 1)}</div><footer><span>${this.from && this.to ? `${this.date(this.from)} – ${this.date(this.to)}` : 'Select an end date'}</span><button type="button" data-cancel>Cancel</button><button type="button" class="apply" data-apply ${!this.from || !this.to ? 'disabled' : ''}>Apply</button></footer></div></div>` : ''}</div></div>`);
      this.root.querySelector('link')?.addEventListener('load', this.reposition);
      this.reposition();
      this.root.querySelector('[data-toggle]')?.addEventListener('click', () => {
        if (this.open) { this.close(); return; }
        this.reset(); this.open = true; this.render(); this.root.querySelector<HTMLButtonElement>('[data-preset]')?.focus();
      });
      this.root.querySelector('[data-cancel]')?.addEventListener('click', () => this.close());
      this.root.querySelector('[data-apply]')?.addEventListener('click', () => {
        if (!this.from || !this.to || this.from > this.to) return;
        this.setAttribute('from', this.from); this.setAttribute('to', this.to); this.close();
        this.dispatchEvent(new CustomEvent('date-range-apply', { bubbles: true, composed: true, detail: { from: this.from, to: this.to } }));
      });
      this.root.querySelectorAll<HTMLButtonElement>('[data-preset]').forEach(button => button.addEventListener('click', () => {
        const preset = presets[Number(button.dataset.preset)]; this.from = preset.from; this.to = preset.to;
        const date = new Date(`${this.from}T12:00:00`); this.month = date.getFullYear() * 12 + date.getMonth(); this.render();
        this.root.querySelector<HTMLButtonElement>(`[data-preset="${button.dataset.preset}"]`)?.focus();
      }));
      this.root.querySelectorAll<HTMLButtonElement>('[data-month]').forEach(button => button.addEventListener('click', () => {
        this.month += Number(button.dataset.month); this.render(); this.root.querySelector<HTMLButtonElement>(`[data-month="${button.dataset.month}"]`)?.focus();
      }));
      this.root.querySelectorAll<HTMLButtonElement>('[data-day]').forEach(button => button.addEventListener('click', () => {
        const value = button.dataset.day!;
        if (!this.from || this.to || value < this.from) { this.from = value; this.to = ''; } else this.to = value;
        this.render(); this.root.querySelector<HTMLButtonElement>(`[data-day="${value}"]`)?.focus();
      }));
      this.onkeydown = event => { if (event.key === 'Escape' && this.open) { event.preventDefault(); event.stopPropagation(); this.close(); } };
    }
  }
  customElements.define('ekom-date-range', EkomDateRange);
}

export function renderStatusFilters(): string {
  const values = managerState.filters.orderStatus.startsWith('states:') ? managerState.filters.orderStatus.slice(7).split(';') : ['', '', ''];
  const groups = [
    { label: 'Order', values: ['Draft','Open','Closed','Cancelled'] },
    { label: 'Payment', values: ['Pending','Authorized','Paid','PartiallyPaid','Failed','Voided'] },
    { label: 'Fulfillment', values: ['Unfulfilled','Picking','Picked','Packed','PartiallyFulfilled','Fulfilled'] },
  ];
  return groups.map((group,index) => {
    const selected = (values[index] || '').split(',').filter(Boolean);
    const label = selected.length === 1 ? selected[0].replace(/([a-z])([A-Z])/g, '$1 $2') : selected.length ? `${selected.length} selected` : 'Any';
    return `<div class="ekom-status-filter"><span>${group.label}</span><details class="ekom-dropdown"><summary>${escapeHtml(label)}<span>▾</span></summary><div class="ekom-dropdown-options">${group.values.map(value => `<label><input type="checkbox" data-shared-status="${index}" value="${value}" ${selected.includes(value) ? 'checked' : ''}>${value.replace(/([a-z])([A-Z])/g, '$1 $2')}</label>`).join('')}</div></details></div>`;
  }).join('');
}

let managerVersionRequest: Promise<string | undefined> | undefined;

/** Load the installed server version once, without re-rendering the active screen. */
function bindManagerVersion(root: HTMLElement | ShadowRoot): void {
  const logo = root.querySelector<HTMLImageElement>('.ekom-design-logo');
  if (!logo) return;
  managerVersionRequest ??= new EkomManagerApi().config()
    .then(config => config.ekomVersion)
    .catch(() => undefined);
  void managerVersionRequest.then(version => {
    if (version) {
      logo.title = `Ekom ${version}`;
      logo.alt = `Ekom ${version}`;
    }
  });
}

/** Keep loaded styles connected so refreshing content cannot flash unstyled markup. */
export function renderManagerContent(root: HTMLElement | ShadowRoot, html: string): void {
  const template = root.ownerDocument.createElement('template');
  template.innerHTML = html;
  const styles = Array.from(root.children).filter(node => node.matches('style, link[rel="stylesheet"]'));
  const incoming = Array.from(template.content.children).filter(node => node.matches('style, link[rel="stylesheet"]'));
  for (const node of incoming) {
    const existing = styles.find(style => style.outerHTML === node.outerHTML);
    if (existing) styles.splice(styles.indexOf(existing), 1);
    else root.appendChild(node.cloneNode(true));
    node.remove();
  }
  for (const obsolete of styles) obsolete.remove();
  for (const node of Array.from(root.childNodes)) {
    if (node.nodeType !== 1 || !(node as Element).matches('style, link[rel="stylesheet"]')) node.remove();
  }
  root.appendChild(template.content);
  bindManagerVersion(root);
}
