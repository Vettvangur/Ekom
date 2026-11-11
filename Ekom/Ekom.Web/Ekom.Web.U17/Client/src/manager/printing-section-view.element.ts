import { EkomManagerApi, formatDate, storeDateFormat, managerState, ensureManagerCurrency, bindDateRange, renderManagerContent } from './manager-shared';
import type { UmbThemeContext } from '@umbraco-cms/backoffice/themes';
import { renderManagerHeader } from './manager-shared';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';

type Capabilities = { enabled: boolean; canManage: boolean; csrfHeader: string; csrfToken: string };
type Job = { id: string; orderNumber: string; storeAlias: string; stationAlias: string; state: string; createdUtc: string; copies: number; hasHtml: boolean; errorCode?: string };
type Jobs = { jobs: Job[]; total: number; page: number; pageSize: number };
type Station = { alias: string; stores: string[]; statuses: string[]; triggerMode?: string; policy: string; copies: number; enabled: boolean;
  health: { lastSeenUtc?: string; online: boolean; compatible: boolean; agentVersion?: string; printerName?: string } };
type History = { actions: { action: string; actor: string; createdUtc: string; newJobId?: string }[];
  delivery?: { claimedUtc: string; reportedUtc?: string; submitted?: boolean; spoolJobId?: string } };

const root = '/ekom/printer/manager';
const escape = (value: unknown): string => String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]!));
const date = (value?: string): string => {
  if (!value) return 'Never';
  const moment = new Date(/[zZ]$|[+-]\d\d:\d\d$/.test(value) ? value : value + 'Z');
  return storeDateFormat() ? formatDate(moment) : moment.toLocaleString(undefined, { year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit' });
};
export const stateLabel = (state: string): string => ({ PendingRender: 'Generating slip', Ready: 'Queued', Claimed: 'With agent',
  ConfirmedPrinted: 'Confirmed printed', Submitted: 'Submitted to printer', NeedsReview: 'Needs review', Cancelled: 'Cancelled', RenderFailed: 'Generation failed' }[state] ?? state);

export function availableActions(job: Job, canManage: boolean): string[] {
  const actions = job.hasHtml ? ['preview', 'history'] : ['history'];
  if (!canManage) return actions;
  if (job.state === 'RenderFailed') actions.push('retry');
  if (['PendingRender', 'Ready', 'RenderFailed'].includes(job.state)) actions.push('cancel');
  if (['Submitted', 'ConfirmedPrinted'].includes(job.state) && job.hasHtml) actions.push('reprint');
  if (job.state === 'NeedsReview') {
    actions.push('resolve-printed');
    if (job.hasHtml) actions.push('resolve-reprint');
  }
  return actions;
}

/** Rebuild an inert document: no scripts, navigation, forms, frames or external assets. */
export function createPreviewDocument(html: string): string {
  // Template contents stay inert while inspecting untrusted images and frames.
  const template = document.createElement('template');
  template.innerHTML = html;
  const output = document.implementation.createHTMLDocument('Picking slip');
  const allowed = new Set('div span p h1 h2 h3 h4 h5 h6 header footer main section article aside table thead tbody tfoot tr th td caption colgroup col ul ol li dl dt dd strong em b i u small br hr pre code blockquote img style'.split(' '));
  const dropped = new Set('script iframe frame object embed svg math template meta link base form input button textarea select'.split(' '));
  const attributes = new Set('class id style colspan rowspan scope width height alt title'.split(' '));
  const copy = (node: Node, parent: Node): void => {
    if (node.nodeType === Node.TEXT_NODE) { parent.appendChild(output.createTextNode(node.textContent ?? '')); return; }
    if (!(node instanceof Element) || node.namespaceURI !== 'http://www.w3.org/1999/xhtml') return;
    const tag = node.localName;
    if (dropped.has(tag)) return;
    const target = allowed.has(tag) ? output.createElement(tag) : output.createElement('span');
    for (const attribute of Array.from(node.attributes)) {
      if (attributes.has(attribute.name)) target.setAttribute(attribute.name, attribute.value);
      if (tag === 'img' && attribute.name === 'src' && /^data:image\/(png|jpeg|gif|webp);base64,/i.test(attribute.value))
        target.setAttribute('src', attribute.value);
    }
    for (const child of Array.from(node.childNodes)) copy(child, target);
    parent.appendChild(target);
  };
  for (const node of Array.from(template.content.childNodes)) copy(node, output.body);
  return '<!doctype html><html><head><meta charset="utf-8"><meta http-equiv="Content-Security-Policy" content="default-src &#39;none&#39;; script-src &#39;none&#39;; style-src &#39;unsafe-inline&#39;; img-src data:; font-src data:; base-uri &#39;none&#39;; form-action &#39;none&#39;; connect-src &#39;none&#39;">'
    + output.head.innerHTML + '</head><body>' + output.body.innerHTML + '</body></html>';
}

export default class EkomPrintingSectionViewElement extends UmbElementMixin(HTMLElement) {
  private readonly panel = this.attachShadow({ mode: 'open' });
  private capabilities?: Capabilities;
  private jobs: Jobs = { jobs: [], total: 0, page: 1, pageSize: 25 };
  private stations: Station[] = [];
  private state = '';
  private station = '';
  private search = '';
  private page = 1;
  private loading = true;
  private error = '';
  private busy = false;
  private generation = 0;
  private cancelConfirmation?: () => void;
  private requests = new Map<string, string>();

  override connectedCallback(): void {
    super.connectedCallback();
    if (!this.hasAttribute('classic')) this.consumeContext?.<UmbThemeContext>('umbThemeContext', context => {
      if (context) this.observe(context.theme, alias => {
        this.toggleAttribute('data-ekom-dark', alias === 'umb-dark-theme');
      }, 'ekom-manager-theme');
    });
    this.render();
    void this.initialize();
  }
  override disconnectedCallback(): void { this.generation++; this.cancelConfirmation?.(); super.disconnectedCallback(); }

  private async json<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(root + path, { credentials: 'same-origin', ...init,
      headers: { Accept: 'application/json', ...init?.headers } });
    if (!response.ok) throw new Error(response.status === 404 ? 'Printing is unavailable or the stored slip has expired.' :
      response.status === 403 ? 'You do not have permission to perform this action.' :
        response.status === 409 ? 'The job changed or cannot be updated. Refresh the list.' : 'Could not contact printing. Try refreshing.');
    return response.json() as Promise<T>;
  }

  private async initialize(): Promise<void> {
    try {
      if (!this.hasAttribute('classic')) {
        managerState.stores = await new EkomManagerApi().stores();
        if (!managerState.stores.some(store => store.alias === managerState.filters.store)) managerState.filters.store = managerState.stores[0]?.alias || '';
        ensureManagerCurrency();
      }
      this.capabilities = await this.json<Capabilities>('/capabilities'); await this.load(); }
    catch (error) { this.error = String((error as Error).message); this.loading = false; this.render(); }
  }

  private async load(): Promise<void> {
    const generation = ++this.generation;
    this.loading = true; this.error = ''; this.render();
    try {
      const query = new URLSearchParams({ page: String(this.page), state: this.state, station: this.station, search: this.search });
      if (!this.hasAttribute('classic')) { query.set('store', managerState.filters.store); query.set('currency', managerState.filters.currency || ''); }
      const [jobs, stations] = await Promise.all([this.json<Jobs>('/jobs?' + query), this.json<Station[]>('/stations')]);
      if (generation !== this.generation) return;
      this.jobs = jobs; this.stations = this.hasAttribute('classic') ? stations : stations.filter(station => station.stores.includes(managerState.filters.store));
    } catch (error) { if (generation === this.generation) this.error = (error as Error).message; }
    finally { if (generation === this.generation) { this.loading = false; this.render(); } }
  }

  private render(): void {
    const disabled = this.loading || this.busy ? 'disabled' : '';
    const online = this.stations.filter(s => s.health.online && s.health.compatible).length;
    const attention = this.jobs.jobs.filter(j => ['NeedsReview', 'RenderFailed'].includes(j.state)).length;
    const totalPages = Math.max(1, Math.ceil(this.jobs.total / this.jobs.pageSize));
    const tone = (state: string) => ({ NeedsReview: 'warning', RenderFailed: 'danger', Submitted: 'success', ConfirmedPrinted: 'success', Ready: 'info', Claimed: 'info' }[state] ?? 'neutral');
    const words = (value: string) => value.replace(/([a-z])([A-Z])/g, '$1 $2');
    renderManagerContent(this.panel, `<link rel="stylesheet" href="/App_Plugins/Ekom/dist/manager-layout.css"><link rel="stylesheet" href="/App_Plugins/Ekom/dist/printing-layout.css"><main aria-busy="${this.loading}">${renderManagerHeader('printing', 'data-field', !this.hasAttribute('classic'))}
      <header class="page-heading"><div><p class="subtitle">Track picking slips and warehouse printer connections.</p></div><div class="heading-actions"><span class="connection"><span class="dot"></span>${this.loading ? 'Checking stations…' : `${online} of ${this.stations.length} stations online`}</span><button data-reload ${disabled}>${this.loading ? 'Refreshing…' : 'Refresh'}</button></div></header>
      <section class="card" aria-labelledby="jobs-heading"><div class="ekom-print-toolbar"><div class="section-heading"><div class="title-group"><h2 id="jobs-heading">Print jobs</h2><span class="count" aria-live="polite">${this.loading ? 'Loading…' : `${this.jobs.total} ${this.jobs.total === 1 ? 'job' : 'jobs'}`}</span></div>${this.state || this.station || this.search ? '<button data-clear>Clear filters</button>' : ''}</div>
      <div class="filters"><label>Status<select data-filter="state">${[['','All statuses'],['attention','Needs attention'],['PendingRender','Generating slip'],['Ready','Queued'],['Claimed','With agent'],['Submitted','Submitted to printer'],['ConfirmedPrinted','Confirmed printed'],['Cancelled','Cancelled']].map(([v,l]) => `<option value="${v}" ${v === this.state ? 'selected' : ''}>${l}</option>`).join('')}</select></label>
      <label>Station<select data-filter="station"><option value="">All stations</option>${this.stations.map(s => `<option value="${escape(s.alias)}" ${s.alias === this.station ? 'selected' : ''}>${escape(s.alias)}</option>`).join('')}</select></label>
      <label class="search">Order number<input data-filter="search" value="${escape(this.search)}" placeholder="Search by order number" maxlength="100" type="search"></label></div></div>
      ${this.error ? `<p class="error" role="alert">${escape(this.error)}</p>` : ''}
      ${!this.loading && attention ? `<div class="notice"><div><strong>${attention} ${attention === 1 ? 'job on this page needs' : 'jobs on this page need'} attention</strong>${this.jobs.jobs.some(job => job.state === 'NeedsReview') ? 'Check the physical printer before arranging another copy of an uncertain print.' : 'Review the affected jobs and retry generating their picking slips.'}</div>${this.state !== 'attention' ? '<button data-attention>View all needing attention</button>' : ''}</div>` : ''}
      <div class="table" tabindex="0" role="region" aria-label="Print jobs; scroll horizontally on small screens"><table><thead><tr><th scope="col">Order</th><th scope="col">Station</th><th scope="col">Status</th><th scope="col">Created</th><th scope="col">Actions</th></tr></thead><tbody>
      ${this.jobs.jobs.map(job => `<tr><td><strong>${escape(job.orderNumber || 'Order unavailable')}</strong><small>${escape(job.storeAlias)}</small></td><td>${escape(job.stationAlias)}<small>${job.copies} ${job.copies === 1 ? 'copy' : 'copies'}</small></td>
      <td class="status-cell"><span class="badge ${tone(job.state)}"><span class="dot"></span>${escape(stateLabel(job.state))}</span>${job.state === 'NeedsReview' ? '<small>Printing could not be confirmed.</small>' : job.state === 'RenderFailed' ? '<small>The picking slip could not be generated.</small>' : ''}</td><td class="created">${escape(date(job.createdUtc))}</td>
      <td><div class="actions">${availableActions(job, this.capabilities?.canManage === true).map(action => `<button aria-label="${escape(({preview:'Preview',history:'History for',retry:'Retry generation for',cancel:'Cancel',reprint:'Reprint','resolve-printed':'Mark as printed','resolve-reprint':'Print again after checking'} as Record<string,string>)[action] + ' ' + job.orderNumber)}" data-job="${escape(job.id)}" data-action="${action}" ${disabled}>${({preview:'Preview',history:'History',retry:'Retry generation',cancel:'Cancel',reprint:'Reprint','resolve-printed':'Mark as printed','resolve-reprint':'Print again after checking'} as Record<string,string>)[action]}</button>`).join('')}</div></td></tr>`).join('')}
      ${this.jobs.jobs.length === 0 ? `<tr><td colspan="5"><div class="empty"><strong>${this.loading ? 'Loading print jobs…' : 'No print jobs to show'}</strong><p>${this.loading ? 'Fetching the latest queue and station information.' : this.state || this.station || this.search ? 'Try a different order number or clear your filters.' : 'Picking slips appear here when orders reach a configured status.'}</p></div></td></tr>` : ''}</tbody></table></div>
      <div class="pager"><span>${this.jobs.total ? `${(this.page - 1) * this.jobs.pageSize + 1}–${Math.min(this.page * this.jobs.pageSize, this.jobs.total)} of ${this.jobs.total} jobs` : '0 jobs'}</span><div class="pager-controls"><button data-page="-1" ${this.page <= 1 || this.loading ? 'disabled' : ''}>Previous</button><span>Page ${this.page} of ${totalPages}</span><button data-page="1" ${this.page >= totalPages || this.loading ? 'disabled' : ''}>Next</button></div></div>
      <p class="footnote"><strong>Submitted to printer</strong> confirms delivery to the print queue. It does not confirm that paper was printed.</p></section>
      <div class="stations-heading"><div><h2 id="stations-heading">Printer stations</h2><p>Connection health and automatic printing rules.</p></div><span class="count">${this.stations.length} ${this.stations.length === 1 ? 'station' : 'stations'}</span></div>
      <section class="station-grid" aria-labelledby="stations-heading">${this.stations.map(s => `<article class="station-card"><div class="station-top"><div><h3>${escape(s.alias)}</h3><p class="printer-name">${escape(s.health.printerName || 'No printer reported')}</p></div><span class="badge ${!s.health.online ? 'neutral' : !s.health.compatible ? 'warning' : 'success'}"><span class="dot"></span>${!s.health.online ? 'Offline' : !s.health.compatible ? 'Update required' : 'Online'}</span></div><dl><dt>Store</dt><dd>${escape(s.stores.join(', '))}</dd><dt>Print when</dt><dd><div class="chips">${s.triggerMode === 'ExplicitCapture' ? '<span class="chip">Explicit capture API</span>' : s.statuses.map(status => `<span class="chip">${escape(words(status))}</span>`).join('') || 'No statuses configured'}</div></dd><dt>Copies</dt><dd>${s.copies} ${s.copies === 1 ? 'copy' : 'copies'} · ${escape(s.policy === 'OncePerOrder' ? 'Once per order' : s.policy === 'OncePerStatus' ? 'Once per status' : words(s.policy))}</dd><dt>Automation</dt><dd>${s.enabled ? 'Enabled' : 'Disabled'}</dd><dt>Last connected</dt><dd>${escape(date(s.health.lastSeenUtc))}</dd></dl><div class="station-bottom"><span>${s.health.agentVersion ? `Agent ${escape(s.health.agentVersion)}` : 'Agent not connected'}</span><span>Settings managed externally</span></div></article>`).join('')}
      ${!this.loading && !this.stations.length ? '<div class="station-card empty"><strong>No printer stations</strong><p>No stations are available for your stores.</p></div>' : ''}</section>
      <details class="setup-help"><summary>How do I change printer settings?</summary><p>Change status rules and copies in the website’s Ekom printing configuration. Select the physical printer in the Windows agent. Station settings are read-only here.</p></details>
      <dialog aria-labelledby="detail-heading"><div class="dialog-heading"><h2 id="detail-heading">Print job details</h2><button data-close>Close</button></div><div data-detail></div></dialog></main>`);
    bindDateRange(this.panel, () => { void this.load(); });
    this.panel.querySelector('[data-field="store"]')?.addEventListener('change', event => {
      managerState.filters.store = (event.target as HTMLSelectElement).value;
      ensureManagerCurrency(); this.station = ''; this.page = 1; void this.load();
    });
    const search = this.panel.querySelector<HTMLInputElement>('[data-filter="search"]');
    const applySearch = () => { if (this.search === search?.value) return; this.search = search?.value || ''; this.page = 1; void this.load(); };
    search?.addEventListener('change', applySearch);
    this.panel.querySelector('[data-reload]')?.addEventListener('click', () => { void this.load(); });
    this.panel.querySelector('[data-clear]')?.addEventListener('click', () => { this.state = ''; this.station = ''; this.search = ''; this.page = 1; void this.load(); });
    this.panel.querySelector('[data-attention]')?.addEventListener('click', () => { this.state = 'attention'; this.station = ''; this.search = ''; this.page = 1; void this.load(); });
    for (const name of ['state', 'station'] as const) this.panel.querySelector(`[data-filter="${name}"]`)?.addEventListener('change', e => { this[name] = (e.target as HTMLSelectElement).value; this.page = 1; void this.load(); });
    this.panel.querySelector('[data-filter="search"]')?.addEventListener('keydown', e => { if ((e as KeyboardEvent).key === 'Enter') applySearch(); });
    this.panel.querySelectorAll<HTMLButtonElement>('[data-page]').forEach(b => b.addEventListener('click', () => { this.page += Number(b.dataset.page); void this.load(); }));
    this.panel.querySelectorAll<HTMLButtonElement>('[data-action]').forEach(b => b.addEventListener('click', () => { void this.action(b.dataset.job!, b.dataset.action!); }));
    const dialog = this.panel.querySelector('dialog') as HTMLDialogElement;
    this.panel.querySelector('[data-close]')?.addEventListener('click', () => dialog.close());
    dialog.addEventListener('cancel', event => { event.preventDefault(); dialog.close(); });
    dialog.addEventListener('keydown', event => {
      if (event.key === 'Escape') { event.preventDefault(); event.stopPropagation(); dialog.close(); }
    });
    dialog.addEventListener('click', event => {
      if (event.target !== dialog) return;
      const bounds = dialog.getBoundingClientRect();
      if (event.clientX < bounds.left || event.clientX > bounds.right || event.clientY < bounds.top || event.clientY > bounds.bottom) dialog.close();
    });
  }

  private confirmAction(message: string): Promise<boolean> {
    if (this.cancelConfirmation) return Promise.resolve(false);
    return new Promise(resolve => {
      const dialog = document.createElement('dialog');
      dialog.className = 'action-confirmation';
      dialog.setAttribute('aria-labelledby', 'print-action-title');
      dialog.setAttribute('aria-describedby', 'print-action-message');
      dialog.innerHTML = `<h2 id="print-action-title">Confirm printing action</h2><p id="print-action-message">${escape(message)}</p><div class="confirmation-actions"><button data-dismiss autofocus>Cancel</button><button class="primary" data-confirm>Confirm</button></div>`;
      const finish = (accepted: boolean) => {
        this.cancelConfirmation = undefined;
        dialog.remove();
        resolve(accepted);
      };
      this.cancelConfirmation = () => finish(false);
      dialog.addEventListener('close', () => finish(dialog.returnValue === 'confirm'), { once: true });
      dialog.querySelector('[data-dismiss]')!.addEventListener('click', () => dialog.close());
      dialog.querySelector('[data-confirm]')!.addEventListener('click', () => dialog.close('confirm'));
      this.panel.append(dialog);
      dialog.showModal();
    });
  }

  private async action(id: string, action: string): Promise<void> {
    if (this.busy) return;
    try {
      if (action === 'preview' || action === 'history') {
        const dialog = this.panel.querySelector('dialog') as HTMLDialogElement;
        const detail = this.panel.querySelector('[data-detail]') as HTMLElement;
        if (action === 'preview') {
          const result = await this.json<{ html: string }>(`/jobs/${id}/preview`);
          const frame = document.createElement('iframe'); frame.title = 'Picking slip preview';
          frame.setAttribute('sandbox', ''); frame.referrerPolicy = 'no-referrer'; frame.srcdoc = createPreviewDocument(result.html);
          detail.replaceChildren(frame); detail.style.height = '90%';
        } else {
          const history = await this.json<History>(`/jobs/${id}/history`);
          detail.style.height = 'auto';
          detail.innerHTML = `<h3>Job history</h3>${history.delivery ? `<p>Claimed: ${escape(date(history.delivery.claimedUtc))}</p><p>Reported: ${escape(date(history.delivery.reportedUtc))}</p><p>Windows queue reference: ${escape(history.delivery.spoolJobId || 'Not reported')}</p>` : '<p>Not yet claimed by an agent.</p>'}<ul>${history.actions.map(x => `<li>${escape(date(x.createdUtc))} — ${escape(({ 'resolve-printed': 'Confirmed physical printing', 'resolve-reprint': 'Closed uncertain job and queued replacement' } as Record<string,string>)[x.action] ?? x.action)} by ${escape(x.actor)}</li>`).join('')}</ul>`;
        }
        dialog.showModal(); return;
      }
      if (!this.capabilities?.canManage) return;
      if (!await this.confirmAction(action === 'resolve-printed' ? 'Have you checked the physical output and confirmed this picking slip was printed? This records your confirmation without printing again.' : action === 'resolve-reprint' ? 'Check the physical printer and clear any pending copy from the Windows print queue first. A delayed original may still print. Continue to close this uncertain job and queue one replacement job with the original copy count?' : action === 'reprint' ? 'Create another copy of this stored picking slip?' : action === 'cancel' ? 'Cancel this queued picking slip?' : 'Retry generating this picking slip?')) return;
      const key = id + ':' + action;
      const requestId = this.requests.get(key) ?? crypto.randomUUID(); this.requests.set(key, requestId);
      this.busy = true; this.render();
      await this.json(`/jobs/${id}/actions`, { method: 'POST', headers: { 'Content-Type': 'application/json',
        [this.capabilities.csrfHeader]: this.capabilities.csrfToken }, body: JSON.stringify({ action, requestId }) });
      this.requests.delete(key); this.busy = false; await this.load();
    } catch (error) { this.busy = false; this.error = (error as Error).message; this.render(); }
  }
}

if (!customElements.get('ekom-printing-section-view')) customElements.define('ekom-printing-section-view', EkomPrintingSectionViewElement);
