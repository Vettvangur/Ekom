import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';

type Capabilities = { enabled: boolean; canManage: boolean; csrfHeader: string; csrfToken: string };
type Job = { id: string; orderNumber: string; storeAlias: string; stationAlias: string; state: string; createdUtc: string; copies: number; hasHtml: boolean; errorCode?: string };
type Jobs = { jobs: Job[]; total: number; page: number; pageSize: number };
type Station = { alias: string; stores: string[]; statuses: string[]; policy: string; copies: number; enabled: boolean;
  health: { lastSeenUtc?: string; online: boolean; compatible: boolean; agentVersion?: string; printerName?: string } };
type History = { actions: { action: string; actor: string; createdUtc: string; newJobId?: string }[];
  delivery?: { claimedUtc: string; reportedUtc?: string; submitted?: boolean; spoolJobId?: string } };

const root = '/ekom/printer/manager';
const escape = (value: unknown): string => String(value ?? '').replace(/[&<>"']/g, ch => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[ch]!));
const date = (value?: string): string => value ? new Date(/[zZ]$|[+-]\d\d:\d\d$/.test(value) ? value : value + 'Z').toLocaleString() : 'Never';
export const stateLabel = (state: string): string => ({ PendingRender: 'Generating slip', Ready: 'Queued', Claimed: 'With agent',
  Submitted: 'Submitted to Windows', NeedsReview: 'Needs review', Cancelled: 'Cancelled', RenderFailed: 'Generation failed' }[state] ?? state);

export function availableActions(job: Job, canManage: boolean): string[] {
  const actions = job.hasHtml ? ['preview', 'history'] : ['history'];
  if (!canManage) return actions;
  if (job.state === 'RenderFailed') actions.push('retry');
  if (['PendingRender', 'Ready', 'RenderFailed'].includes(job.state)) actions.push('cancel');
  if (job.state === 'Submitted' && job.hasHtml) actions.push('reprint');
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
  private requests = new Map<string, string>();

  override connectedCallback(): void { super.connectedCallback(); this.render(); void this.initialize(); }
  override disconnectedCallback(): void { this.generation++; super.disconnectedCallback(); }

  private async json<T>(path: string, init?: RequestInit): Promise<T> {
    const response = await fetch(root + path, { credentials: 'same-origin', ...init,
      headers: { Accept: 'application/json', ...init?.headers } });
    if (!response.ok) throw new Error(response.status === 404 ? 'Printing is unavailable or the stored slip has expired.' :
      response.status === 403 ? 'You do not have permission to perform this action.' :
        response.status === 409 ? 'The job changed or cannot be updated. Refresh the list.' : 'Could not contact printing. Try refreshing.');
    return response.json() as Promise<T>;
  }

  private async initialize(): Promise<void> {
    try { this.capabilities = await this.json<Capabilities>('/capabilities'); await this.load(); }
    catch (error) { this.error = String((error as Error).message); this.loading = false; this.render(); }
  }

  private async load(): Promise<void> {
    const generation = ++this.generation;
    this.loading = true; this.error = ''; this.render();
    try {
      const query = new URLSearchParams({ page: String(this.page), state: this.state, station: this.station, search: this.search });
      const [jobs, stations] = await Promise.all([this.json<Jobs>('/jobs?' + query), this.json<Station[]>('/stations')]);
      if (generation !== this.generation) return;
      this.jobs = jobs; this.stations = stations;
    } catch (error) { if (generation === this.generation) this.error = (error as Error).message; }
    finally { if (generation === this.generation) { this.loading = false; this.render(); } }
  }

  private render(): void {
    const disabled = this.loading || this.busy ? 'disabled' : '';
    this.panel.innerHTML = `<style>
      :host{display:block;font:inherit;color:var(--uui-color-text,#303033)} *{box-sizing:border-box}
      main{padding:24px;max-width:1500px;margin:auto} h2{font-size:20px;margin:0 0 16px} h3{font-size:16px}
      section{background:var(--uui-color-surface,#fff);padding:20px;margin-bottom:20px;border:1px solid var(--uui-color-border,#ddd);border-radius:4px}
      .filters,.actions,.pager{display:flex;gap:12px;align-items:end;flex-wrap:wrap;margin-bottom:16px} label{display:grid;gap:5px}
      input,select,button{font:inherit;padding:8px;border:1px solid var(--uui-color-border,#bbb);border-radius:3px;background:var(--uui-color-surface,#fff);color:inherit}
      button:not(:disabled){cursor:pointer} button:disabled{opacity:.5} button:focus-visible,input:focus-visible,select:focus-visible{outline:2px solid var(--uui-color-selected,#3544b1)}
      table{border-collapse:collapse;width:100%;text-align:left} th,td{padding:12px 8px;border-bottom:1px solid var(--uui-color-border,#ddd);vertical-align:top}
      td small{display:block;margin-top:6px} .table{overflow:auto} .actions{gap:6px;margin:0} .actions button{padding:5px 8px}
      .error{color:var(--uui-color-danger,#a22);padding:12px 0}.muted{color:var(--uui-color-text-alt,#666)} .attention{font-weight:600;color:var(--uui-color-warning-emphasis,#794600)}
      .pager{justify-content:space-between;margin:16px 0 0} dialog{width:min(1000px,95vw);height:90vh;background:var(--uui-color-surface,#fff);color:inherit;border:1px solid #aaa;padding:20px} dialog iframe{width:100%;height:calc(100% - 50px);border:1px solid #ddd;background:white} dialog::backdrop{background:#0006}
      @media(max-width:650px){main{padding:12px}section{padding:12px}}
    </style><main aria-busy="${this.loading}"><section><h2>Print jobs</h2>
      <div class="filters"><label>State<select data-filter="state">${[['','All jobs'],['attention','Needs attention'],['PendingRender','Generating slip'],['Ready','Queued'],['Claimed','With agent'],['Submitted','Submitted to Windows'],['Cancelled','Cancelled']].map(([v,l]) => `<option value="${v}" ${v === this.state ? 'selected' : ''}>${l}</option>`).join('')}</select></label>
      <label>Station<select data-filter="station"><option value="">All stations</option>${this.stations.map(s => `<option value="${escape(s.alias)}" ${s.alias === this.station ? 'selected' : ''}>${escape(s.alias)}</option>`).join('')}</select></label>
      <label>Order number<input data-filter="search" value="${escape(this.search)}" maxlength="100" type="search"></label><button data-refresh ${disabled}>Refresh</button></div>
      ${this.error ? `<p class="error" role="alert">${escape(this.error)}</p>` : ''}
      <p class="muted" aria-live="polite">${this.loading ? 'Loading…' : `${this.jobs.total} jobs`}</p>
      <div class="table"><table><thead><tr><th>Order</th><th>Station</th><th>State</th><th>Created</th><th>Actions</th></tr></thead><tbody>
      ${this.jobs.jobs.map(job => `<tr><td>${escape(job.orderNumber)}<small>${escape(job.storeAlias)}</small></td><td>${escape(job.stationAlias)}<small>${job.copies} ${job.copies === 1 ? 'copy' : 'copies'}</small></td>
      <td class="${['NeedsReview','RenderFailed'].includes(job.state) ? 'attention' : ''}">${escape(stateLabel(job.state))}${job.state === 'NeedsReview' ? '<small>Check the printer before arranging another copy.</small>' : ''}</td><td>${escape(date(job.createdUtc))}</td>
      <td><div class="actions">${availableActions(job, this.capabilities?.canManage === true).map(action => `<button data-job="${escape(job.id)}" data-action="${action}" ${disabled}>${({preview:'Preview',history:'History',retry:'Retry generation',cancel:'Cancel',reprint:'Reprint'} as Record<string,string>)[action]}</button>`).join('')}</div></td></tr>`).join('')}
      ${!this.loading && this.jobs.jobs.length === 0 ? '<tr><td colspan="5">No print jobs match these filters.</td></tr>' : ''}</tbody></table></div>
      <div class="pager"><button data-page="-1" ${this.page <= 1 || this.loading ? 'disabled' : ''}>Previous</button><span>Page ${this.page} of ${Math.max(1, Math.ceil(this.jobs.total / 25))}</span><button data-page="1" ${this.page * 25 >= this.jobs.total || this.loading ? 'disabled' : ''}>Next</button></div>
      <p class="muted">Submitted to Windows means accepted by the print queue, not confirmed physical printing.</p></section>
      <section><h2>Stations</h2><p class="muted">Settings are read-only here. Configure printing in appsettings.json and select the printer in the Windows agent.</p>
      <div class="table"><table><thead><tr><th>Station / printer</th><th>Connection</th><th>Last seen</th><th>Trigger settings</th></tr></thead><tbody>${this.stations.map(s => `<tr><td>${escape(s.alias)}<small>${escape(s.health.printerName || 'Printer not reported')}</small><small>${escape(s.stores.join(', '))}</small></td><td>${s.health.online ? 'Online' : 'Offline'}<small>${s.health.agentVersion ? `Agent ${escape(s.health.agentVersion)}${s.health.compatible ? '' : ' — incompatible'}` : 'No agent connected'}</small></td><td>${escape(date(s.health.lastSeenUtc))}</td><td>${s.enabled ? escape(s.statuses.join(', ')) : 'Disabled'}<small>${escape(s.policy)} · ${s.copies} ${s.copies === 1 ? 'copy' : 'copies'}</small></td></tr>`).join('')}
      ${!this.loading && !this.stations.length ? '<tr><td colspan="4">No stations are available for your stores.</td></tr>' : ''}</tbody></table></div></section><dialog><button data-close>Close</button><div data-detail></div></dialog></main>`;
    this.panel.querySelector('[data-refresh]')?.addEventListener('click', () => { this.search = (this.panel.querySelector('[data-filter="search"]') as HTMLInputElement).value; this.page = 1; void this.load(); });
    for (const name of ['state', 'station'] as const) this.panel.querySelector(`[data-filter="${name}"]`)?.addEventListener('change', e => { this[name] = (e.target as HTMLSelectElement).value; this.page = 1; void this.load(); });
    this.panel.querySelector('[data-filter="search"]')?.addEventListener('keydown', e => { if ((e as KeyboardEvent).key === 'Enter') (this.panel.querySelector('[data-refresh]') as HTMLButtonElement).click(); });
    this.panel.querySelectorAll<HTMLButtonElement>('[data-page]').forEach(b => b.addEventListener('click', () => { this.page += Number(b.dataset.page); void this.load(); }));
    this.panel.querySelectorAll<HTMLButtonElement>('[data-action]').forEach(b => b.addEventListener('click', () => { void this.action(b.dataset.job!, b.dataset.action!); }));
    this.panel.querySelector('[data-close]')?.addEventListener('click', () => (this.panel.querySelector('dialog') as HTMLDialogElement).close());
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
          detail.innerHTML = `<h3>Job history</h3>${history.delivery ? `<p>Claimed: ${escape(date(history.delivery.claimedUtc))}</p><p>Reported: ${escape(date(history.delivery.reportedUtc))}</p><p>Windows queue reference: ${escape(history.delivery.spoolJobId || 'Not reported')}</p>` : '<p>Not yet claimed by an agent.</p>'}<ul>${history.actions.map(x => `<li>${escape(date(x.createdUtc))} — ${escape(x.action)} by ${escape(x.actor)}</li>`).join('')}</ul>`;
        }
        dialog.showModal(); return;
      }
      if (!this.capabilities?.canManage) return;
      if (!window.confirm(action === 'reprint' ? 'Create another copy of this stored picking slip?' : action === 'cancel' ? 'Cancel this queued picking slip?' : 'Retry generating this picking slip?')) return;
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
