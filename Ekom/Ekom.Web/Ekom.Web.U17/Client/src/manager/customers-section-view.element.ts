import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { EkomManagerApi, escapeHtml as esc, managerState, managerStyles } from './manager-shared';

type Spend = { currency: string; total: number; averageOrder: number; averageLifetime: number };
type Customer = { identity: string; name?: string; email?: string; username?: string; shippingCountry?: string; firstOrder: string; lastOrder: string; lastStatus?: string; orders: number; daysSinceLastOrder: number; averageReorderDays?: number; spend: Spend[] };
type Result = { customers: Customer[]; totals: { customers: number; returningPercent: number; averageOrders: number; averageReorderDays?: number }; spend: Spend[]; missingIdentifiers: number; identityField: string; totalPages: number };
type Detail = { customer: Customer; orders: Array<{ uniqueId: string; orderNumber: string; referenceId: number; orderStatusCol: string; createDate: string; paidDate?: string; formattedTotal: string }>; count: number; page: number; totalPages: number; snapshot: { orderId: string; orderNumber: string; date: string; unavailable: boolean; fields: Record<string, string> } };

export class EkomCustomersSectionViewElement extends UmbElementMixin(HTMLElement) {
  private api = new EkomManagerApi();
  private result?: Result;
  private detail?: Detail;
  private query = '';
  private segment = 'all';
  private sort = 'newest';
  private page = 1;
  private busy = false;
  private error = '';
  private request = 0;
  private detailRequest = 0;
  private other = false;
  private classic = false;
  private opener?: string;

  override connectedCallback(): void {
    super.connectedCallback();
    this.classic = this.hasAttribute('classic');
    if (this.classic) {
      for (const field of ['store', 'dateFrom', 'dateTo', 'orderStatus'] as const) {
        const value = this.getAttribute(field.toLowerCase());
        if (value) managerState.filters[field] = value;
      }
    }
    void this.initialize();
  }

  override disconnectedCallback(): void {
    this.request++;
    this.detailRequest++;
    super.disconnectedCallback();
  }

  private async initialize(): Promise<void> {
    this.busy = true;
    this.render();
    try {
      managerState.stores = await this.api.stores();
      if (!managerState.stores.some(x => x.alias === managerState.filters.store)) managerState.filters.store = managerState.stores[0]?.alias || '';
      await this.load();
    } catch {
      this.error = 'Unable to load customers. Please try again.';
      this.busy = false;
      this.render();
    }
  }

  private params(): URLSearchParams {
    const f = managerState.filters;
    return new URLSearchParams({ store: f.store, start: f.dateFrom, end: f.dateTo, orderStatus: f.orderStatus, query: this.query, segment: this.segment, sort: this.sort, page: String(this.page) });
  }

  private async get<T>(path: string, params: URLSearchParams): Promise<T> {
    const response = await fetch(`/ekom/manager/customers${path}?${params}`, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
    if (!response.ok) throw new Error('Unable to load customer data. Check the filters and try again.');
    return response.json() as Promise<T>;
  }

  private async load(): Promise<void> {
    const request = ++this.request;
    this.detailRequest++;
    this.detail = undefined;
    this.result = undefined;
    this.error = '';
    this.busy = true;
    this.render();
    try {
      if (!managerState.filters.store) { this.result = undefined; return; }
      const result = await this.get<Result>('', this.params());
      if (request === this.request) this.result = result;
    } catch (error) {
      if (request === this.request) { this.result = undefined; this.error = String(error); }
    } finally {
      if (request === this.request) { this.busy = false; this.render(); }
    }
  }

  private async open(identity: string, page = 1): Promise<void> {
    const request = ++this.detailRequest;
    this.opener = identity;
    this.error = '';
    try {
      const detail = await this.get<Detail>('/detail', new URLSearchParams({ store: managerState.filters.store, identity, page: String(page), other: String(this.other) }));
      if (request !== this.detailRequest || !this.isConnected) return;
      this.detail = detail;
      this.render();
      this.querySelector<HTMLButtonElement>('[data-close]')?.focus();
    } catch (error) {
      if (request === this.detailRequest) { this.error = String(error); this.render(); }
    }
  }

  private close(): void {
    this.detailRequest++;
    this.detail = undefined;
    this.render();
    Array.from(this.querySelectorAll<HTMLButtonElement>('[data-customer]')).find(x => x.dataset.customer === this.opener)?.focus();
  }

  private async export(): Promise<void> {
    this.busy = true;
    this.render();
    try {
      const response = await fetch(`/ekom/manager/customers/export?${this.params()}`, { credentials: 'same-origin' });
      if (!response.ok) throw new Error('Customer export failed.');
      const url = URL.createObjectURL(await response.blob());
      const link = document.createElement('a');
      link.href = url; link.download = 'customers.csv'; link.click();
      setTimeout(() => URL.revokeObjectURL(url), 1000);
    } catch (error) { this.error = String(error); }
    finally { this.busy = false; this.render(); }
  }

  private amount(spend: Spend[], field: 'total' | 'averageOrder' | 'averageLifetime'): string {
    return spend.map(x => {
      const storeCurrency = managerState.stores.find(s => s.alias === managerState.filters.store)?.currency;
      let currency = x.currency;
      if (!/^[A-Z]{3}$/.test(currency) && storeCurrency?.currencyValue === currency) currency = storeCurrency.isoCurrencySymbol || currency;
      try { return esc(new Intl.NumberFormat(undefined, { style: 'currency', currency }).format(x[field])); }
      catch { return `${esc(x[field].toLocaleString(undefined, { maximumFractionDigits: 2 }))} ${esc(x.currency)}`; }
    }).join('<br>') || '—';
  }

  private number(value?: number | null): string { return value == null ? '—' : esc(value.toLocaleString(undefined, { maximumFractionDigits: 1 })); }
  private date(value?: string): string { return value ? esc(new Date(value).toLocaleDateString()) : '—'; }
  private card(label: string, value: string): string { return `<div class="customer-card"><small>${label}</small><strong>${value}</strong></div>`; }

  private render(): void {
    const f = managerState.filters, r = this.result;
    this.innerHTML = `<style>${managerStyles}
      ekom-customers-section-view { display:block; color:var(--uui-color-text,#20232e) }
      .customers { padding:24px; background:var(--uui-color-background,#f3f4f7); min-height:80vh }
      .customer-cards { display:grid;grid-template-columns:repeat(auto-fit,minmax(155px,1fr));gap:12px;margin:18px 0 }
      .customer-card,.customer-panel { background:var(--uui-color-surface,#fff);border:1px solid #dfe2e8;border-radius:10px;padding:18px }
      .customer-card small { display:block;color:#646b7c;font-size:12px;margin-bottom:8px }
      .customer-card strong { font-size:23px }
      .customer-toolbar { display:flex;flex-wrap:wrap;align-items:end;gap:12px }
      .customer-toolbar label { display:grid;gap:5px;font-size:12px }
      .customers input,.customers select,.customers button,.customer-dialog button { font:inherit;padding:8px 12px;border:1px solid #dfe2e8;border-radius:6px;background:var(--uui-color-surface,#fff);color:inherit }
      .customers button,.customer-dialog button { cursor:pointer }
      .customers button:disabled { opacity:.5;cursor:default }
      .customer-table-wrap { overflow:auto }
      .customer-table { border-collapse:collapse;width:100%;font-size:14px }
      .customer-table th,.customer-table td { text-align:left;padding:15px 10px;border-bottom:1px solid #e8e9ed;white-space:nowrap }
      .customer-table th { font-size:11px;text-transform:uppercase;letter-spacing:.07em;color:#70778a }
      .customer-table button[data-customer] { border:0;padding:0;text-align:left;background:none;font-weight:600 }
      .customer-muted { display:block;color:#70778a;font-size:12px;margin-top:5px }
      .customer-badge { background:#edf2f0;color:#237053;border-radius:10px;padding:3px 8px;font-size:11px;margin-left:8px }
      .customer-pagination { display:flex;gap:16px;align-items:center;justify-content:flex-end;padding-top:18px }
      .customer-overlay { position:fixed;inset:0;background:#0006;z-index:10000;display:flex;justify-content:flex-end }
      .customer-dialog { background:var(--uui-color-surface,#fff);width:min(1200px,95vw);height:100%;overflow:auto;padding:28px;box-sizing:border-box }
      .customer-heading { display:flex;justify-content:space-between;gap:16px;align-items:start }
      .customer-heading h2 { margin:0 0 8px }
      .customer-detail-grid { display:grid;grid-template-columns:minmax(0,2fr) minmax(230px,1fr);gap:24px }
      .customer-snapshot { background:var(--uui-color-background,#f3f4f7);padding:20px;border-radius:8px }
      .customer-snapshot dt { color:#70778a;font-size:12px;margin-top:15px }
      .customer-snapshot dd { margin:4px 0;overflow-wrap:anywhere }
      @media(max-width:800px) { .customer-detail-grid { grid-template-columns:1fr } .customers { padding:12px } }
    </style><section class="customers" aria-busy="${this.busy}">
      <div class="customer-toolbar">
        <label>Store<select data-filter="store">${managerState.stores.map(s => `<option value="${esc(s.alias)}" ${s.alias === f.store ? 'selected' : ''}>${esc(s.title)}</option>`).join('')}</select></label>
        <label>Date From<input type="date" data-filter="dateFrom" value="${esc(f.dateFrom)}"></label>
        <label>Date To<input type="date" data-filter="dateTo" value="${esc(f.dateTo)}"></label>
        <button data-reload ${this.busy ? 'disabled' : ''}>Refresh</button>
      </div>
      <p class="customer-muted">Customers who ordered in the selected period. Lifetime metrics include all qualifying orders in this store. Status filter: ${esc(f.orderStatus.replace(/([a-z])([A-Z])/g, '$1 $2'))}.</p>
      ${this.error ? `<p role="alert">${esc(this.error)}</p>` : ''}
      ${this.busy ? '<p role="status">Loading…</p>' : ''}
      ${r ? `<div class="customer-cards">${this.card('Customers', this.number(r.totals.customers))}${this.card('Returning', this.number(r.totals.returningPercent) + '%')}${this.card('Average lifetime spend', this.amount(r.spend,'averageLifetime'))}${this.card('Average orders per customer',this.number(r.totals.averageOrders))}${this.card('Average days between orders',this.number(r.totals.averageReorderDays))}</div>
      <p class="customer-muted">${r.missingIdentifiers} qualifying orders in this period have no ${r.identityField === 'CustomerUsername' ? 'username' : 'email'} and are excluded. Spend uses recorded charged amounts, without refund adjustment.</p>` : ''}
      <div class="customer-panel"><div class="customer-toolbar" role="search">
        <label>Search<input name="query" value="${esc(this.query)}" placeholder="Name, email or username"></label>
        <button type="button" data-search>Search</button>
        <label>Customers<select data-segment>${['all','new','returning'].map(s => `<option value="${s}" ${s === this.segment ? 'selected' : ''}>${s[0].toUpperCase()+s.slice(1)}</option>`).join('')}</select></label>
        <label>Sort by<select data-sort>${[['newest','Newest first order'],['oldest','Oldest first order'],['lastOrder','Latest order'],['orders','Most orders'],['name','Name']].map(([v,l]) => `<option value="${v}" ${v === this.sort ? 'selected' : ''}>${l}</option>`).join('')}</select></label>
        <button type="button" data-export ${this.busy || !r?.totals.customers ? 'disabled' : ''}>Export</button>
      </div><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Customer</th><th>First order</th><th>Orders</th><th>Lifetime spend</th><th>Average order</th><th>Last order</th><th>Days since last order</th></tr></thead><tbody>
      ${(r?.customers || []).map(c => `<tr><td><button data-customer="${esc(c.identity)}">${esc(c.name || c.email || c.username || c.identity)}<span class="customer-badge">${c.orders > 1 ? 'Returning' : 'New'}</span><span class="customer-muted">${esc(c.email || c.username || c.identity)}</span></button></td><td>${this.date(c.firstOrder)}</td><td>${c.orders}</td><td>${this.amount(c.spend,'total')}</td><td>${this.amount(c.spend,'averageOrder')}</td><td>${this.date(c.lastOrder)}<span class="customer-muted">${esc(c.lastStatus || '')}</span></td><td>${c.daysSinceLastOrder}</td></tr>`).join('') || '<tr><td colspan="7">No customers found.</td></tr>'}
      </tbody></table></div><div class="customer-pagination"><button data-page="${this.page-1}" ${this.page <= 1 || this.busy ? 'disabled' : ''}>Previous</button><span>Page ${this.page} of ${Math.max(1,r?.totalPages || 0)}</span><button data-page="${this.page+1}" ${this.page >= (r?.totalPages || 0) || this.busy ? 'disabled' : ''}>Next</button></div></div>
      ${this.detail ? this.renderDetail(this.detail) : ''}</section>`;
    this.bind();
  }

  private renderDetail(d: Detail): string {
    const c = d.customer;
    return `<div class="customer-overlay"><section role="dialog" aria-modal="true" aria-label="Customer details" class="customer-dialog"><header class="customer-heading"><div><h2>${esc(c.name || c.identity)} <span class="customer-badge">${c.orders > 1 ? 'Returning' : 'New'}</span></h2><p>${esc([c.email,c.username].filter(Boolean).join(' · '))}</p></div><button data-close aria-label="Close customer details">Close</button></header>
      <div class="customer-cards">${this.card('Lifetime spend',this.amount(c.spend,'total'))}${this.card('Orders',String(c.orders))}${this.card('Average order',this.amount(c.spend,'averageOrder'))}${this.card('First order',this.date(c.firstOrder))}${this.card('Days since last order',String(c.daysSinceLastOrder))}${this.card('Average reorder interval (days)',this.number(c.averageReorderDays))}</div>
      <div class="customer-detail-grid"><div><h3>Orders <small>(${d.count})</small></h3><label><input type="checkbox" data-other ${this.other ? 'checked' : ''}> Show other orders (excluded from lifetime metrics)</label><div class="customer-table-wrap"><table class="customer-table"><thead><tr><th>Order</th><th>Status</th><th>Created</th><th>Paid</th><th>Total</th></tr></thead><tbody>${d.orders.map(o => `<tr><td><button data-order="${esc(o.uniqueId)}">#${esc(o.orderNumber || String(o.referenceId))}</button></td><td>${esc(o.orderStatusCol)}</td><td>${this.date(o.createDate)}</td><td>${this.date(o.paidDate)}</td><td>${esc(o.formattedTotal)}</td></tr>`).join('') || '<tr><td colspan="5">No orders.</td></tr>'}</tbody></table></div><div class="customer-pagination"><button data-detail-page="${d.page-1}" ${d.page <= 1 ? 'disabled' : ''}>Previous</button><span>${d.page} / ${Math.max(1,d.totalPages)}</span><button data-detail-page="${d.page+1}" ${d.page >= d.totalPages ? 'disabled' : ''}>Next</button></div></div>
      <aside class="customer-snapshot"><h3>Latest order snapshot</h3><p class="customer-muted">Recorded on order #${esc(d.snapshot.orderNumber)} · ${this.date(d.snapshot.date)}. Historical details, not a current customer profile.</p>${d.snapshot.unavailable ? '<p>Saved details could not be read.</p>' : ''}<dl>${Object.entries(d.snapshot.fields).map(([k,v]) => `<dt>${esc(k)}</dt><dd>${esc(v)}</dd>`).join('') || '<dd>No additional details recorded.</dd>'}</dl></aside></div></section></div>`;
  }

  private bind(): void {
    this.querySelectorAll<HTMLInputElement | HTMLSelectElement>('[data-filter]').forEach(input => input.addEventListener('change', () => {
      const key = input.dataset.filter as 'store' | 'dateFrom' | 'dateTo';
      managerState.filters[key] = input.value;
      this.dispatchEvent(new CustomEvent('customer-filters-changed', { bubbles:true, detail: { ...managerState.filters } }));
      this.page = 1; void this.load();
    }));
    const search = () => {
      this.query = this.querySelector<HTMLInputElement>('[name=query]')?.value.trim() || '';
      this.page = 1;
      void this.load();
    };
    this.querySelector('[data-search]')?.addEventListener('click', search);
    this.querySelector<HTMLInputElement>('[name=query]')?.addEventListener('keydown', event => {
      if (event.key !== 'Enter' || event.isComposing) return;
      event.preventDefault();
      event.stopPropagation();
      search();
    });
    this.querySelector<HTMLSelectElement>('[data-segment]')?.addEventListener('change', event => { this.segment = (event.target as HTMLSelectElement).value; this.page=1; void this.load(); });
    this.querySelector<HTMLSelectElement>('[data-sort]')?.addEventListener('change', event => { this.sort = (event.target as HTMLSelectElement).value; this.page=1; void this.load(); });
    this.querySelector('[data-reload]')?.addEventListener('click', () => void this.load());
    this.querySelector('[data-export]')?.addEventListener('click', () => void this.export());
    this.querySelector('[data-close]')?.addEventListener('click', () => this.close());
    this.querySelector('[data-other]')?.addEventListener('change', () => { this.other = !this.other; if (this.detail) void this.open(this.detail.customer.identity); });
    this.querySelectorAll<HTMLButtonElement>('[data-customer]').forEach(b => b.addEventListener('click', () => { this.other=false; void this.open(b.dataset.customer!); }));
    this.querySelectorAll<HTMLButtonElement>('[data-page]').forEach(b => b.addEventListener('click', () => { this.page=Number(b.dataset.page); void this.load(); }));
    this.querySelectorAll<HTMLButtonElement>('[data-detail-page]').forEach(b => b.addEventListener('click', () => { if (this.detail) void this.open(this.detail.customer.identity, Number(b.dataset.detailPage)); }));
    this.querySelectorAll<HTMLButtonElement>('[data-order]').forEach(b => b.addEventListener('click', () => {
      if (this.classic) this.dispatchEvent(new CustomEvent('customer-open-order', { bubbles:true, detail: { orderId:b.dataset.order } }));
      else {
        managerState.pendingOrderId = b.dataset.order;
        const link=document.createElement('a'); link.href=location.pathname.replace(/\/customers\/?$/, '/orders') + '?orderId=' + encodeURIComponent(b.dataset.order || ''); this.appendChild(link); link.click(); link.remove();
      }
    }));
    this.querySelector<HTMLElement>('[role=dialog]')?.addEventListener('keydown', event => {
      if (event.key === 'Escape') { event.preventDefault(); this.close(); }
      if (event.key !== 'Tab') return;
      const focusable = Array.from(this.querySelectorAll<HTMLElement>('[role=dialog] button:not(:disabled),[role=dialog] input'));
      const first=focusable[0], last=focusable[focusable.length-1];
      if (event.shiftKey && document.activeElement === first) { event.preventDefault(); last?.focus(); }
      else if (!event.shiftKey && document.activeElement === last) { event.preventDefault(); first?.focus(); }
    });
  }
}
customElements.define('ekom-customers-section-view', EkomCustomersSectionViewElement);

export default EkomCustomersSectionViewElement;
