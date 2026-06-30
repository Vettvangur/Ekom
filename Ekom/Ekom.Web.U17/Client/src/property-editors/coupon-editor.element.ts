import type {
  ManifestPropertyEditorUi,
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type CouponItem = {
  couponCode?: string;
  numberAvailable?: number;
  date?: string;
};

type CouponListResponse = {
  item1?: CouponItem[];
  item2?: number;
  data?: CouponItem[];
  totalPages?: number;
};

type CouponGenerationRequest = {
  count: number;
  numberAvailable: number;
  prefix: string;
  randomLength: number;
  characterSet: 'UppercaseAlphanumeric' | 'Numbers' | 'Letters';
};

type CouponGenerationResponse = {
  created?: number;
};

export class EkomCouponEditorElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private readonly pageSize = 10;
  private coupons: CouponItem[] = [];
  private page = 1;
  private totalPages = 0;
  private query = '';
  private status?: HTMLParagraphElement;

  get value(): unknown {
    return undefined;
  }

  set value(_value: unknown) {
    // Coupons are stored in Ekom tables through the backoffice API.
  }

  get readonly(): boolean {
    return this.hasAttribute('readonly');
  }

  set readonly(value: boolean) {
    this.toggleAttribute('readonly', value);
    this.syncDisabledState();
  }

  override connectedCallback(): void {
    this.renderShell();

    if (this.isCreateMode() || this.getContentKey() == null) {
      this.renderCreateMessage();
      return;
    }

    void this.loadCoupons();
  }

  private renderShell(): void {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-coupon-editor {
          display: grid;
          gap: var(--uui-size-space-4, 16px);
        }

        .actions,
        .search,
        .pager,
        form {
          display: flex;
          flex-wrap: wrap;
          align-items: end;
          gap: var(--uui-size-space-3, 12px);
        }

        form {
          border: 1px solid var(--uui-color-border, #d8d7d9);
          border-radius: var(--uui-border-radius, 3px);
          padding: var(--uui-size-space-4, 16px);
        }

        form[hidden] {
          display: none;
        }

        label {
          display: grid;
          gap: var(--uui-size-space-1, 4px);
          font-weight: 600;
        }

        input,
        select {
          box-sizing: border-box;
          min-height: 32px;
          border: 1px solid var(--uui-color-border, #d8d7d9);
          border-radius: var(--uui-border-radius, 3px);
          padding: var(--uui-size-space-2, 8px);
          background: var(--uui-color-surface, #fff);
          color: var(--uui-color-text, #1b264f);
          font: inherit;
        }

        table {
          width: 100%;
          border-collapse: collapse;
          background: var(--uui-color-surface, #fff);
        }

        th,
        td {
          border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
          padding: var(--uui-size-space-3, 12px);
          text-align: left;
        }

        button {
          border: 0;
          border-radius: var(--uui-border-radius, 3px);
          padding: var(--uui-size-space-2, 8px) var(--uui-size-space-4, 16px);
          background: var(--uui-color-interactive, #3544b1);
          color: var(--uui-color-interactive-contrast, #fff);
          cursor: pointer;
          font: inherit;
          font-weight: 600;
        }

        button[data-kind='secondary'] {
          background: var(--uui-color-surface-alt, #f3f3f5);
          color: var(--uui-color-text, #1b264f);
        }

        button[data-kind='danger'] {
          background: var(--uui-color-danger, #d42054);
          color: var(--uui-color-danger-contrast, #fff);
        }

        button:disabled,
        input:disabled,
        select:disabled {
          cursor: not-allowed;
          opacity: 0.55;
        }

        p {
          margin: 0;
          color: var(--uui-color-text-alt, #515054);
          line-height: 1.4;
        }

        p[data-error='true'] {
          color: var(--uui-color-danger, #d42054);
        }
      </style>
      <div class="ekom-coupon-editor"></div>
      <p aria-live="polite"></p>
    `;

    this.status = this.querySelector('p') ?? undefined;
  }

  private renderCreateMessage(): void {
    const editor = this.getEditor();
    editor.replaceChildren();
    editor.textContent = 'You need to save the order discount before you can add coupon codes. You will need to refresh the page after saving the order discount.';
  }

  private renderEditor(): void {
    const editor = this.getEditor();
    editor.innerHTML = `
      <div class="actions">
        <button type="button" data-action="show-add">Add Code</button>
        <button type="button" data-action="show-generate" data-kind="secondary">Generate Coupons</button>
        <button type="button" data-action="export" data-kind="secondary">Export</button>
      </div>
      <form data-form="add" hidden>
        <label>
          Coupon Code
          <input type="text" name="couponCode" autocomplete="off" required />
        </label>
        <label>
          Usage limits
          <input type="number" name="numberAvailable" min="0" value="1" required />
        </label>
        <button type="submit">Add</button>
        <button type="button" data-action="cancel-forms" data-kind="secondary">Cancel</button>
      </form>
      <form data-form="generate" hidden>
        <label>
          Amount
          <input type="number" name="count" min="1" value="10" required />
        </label>
        <label>
          Usage limits
          <input type="number" name="numberAvailable" min="0" value="1" required />
        </label>
        <label>
          Prefix
          <input type="text" name="prefix" autocomplete="off" />
        </label>
        <label>
          Random length
          <input type="number" name="randomLength" min="1" value="8" required />
        </label>
        <label>
          Character set
          <select name="characterSet">
            <option value="UppercaseAlphanumeric">Uppercase letters and numbers</option>
            <option value="Numbers">Numbers only</option>
            <option value="Letters">Letters only</option>
          </select>
        </label>
        <button type="submit">Generate</button>
        <button type="button" data-action="cancel-forms" data-kind="secondary">Cancel</button>
      </form>
      <div class="search">
        <label>
          Search
          <input type="search" name="query" placeholder="Type to search..." value="${this.escapeAttribute(this.query)}" />
        </label>
      </div>
      <table>
        <thead>
          <tr>
            <th>Coupon Code</th>
            <th>Usage limits</th>
            <th>Created</th>
            <th></th>
          </tr>
        </thead>
        <tbody></tbody>
      </table>
      <div class="pager">
        <button type="button" data-action="previous" data-kind="secondary">Previous</button>
        <span>Page ${this.page} of ${Math.max(this.totalPages, 1)}</span>
        <button type="button" data-action="next" data-kind="secondary">Next</button>
      </div>
    `;

    editor.querySelector<HTMLButtonElement>('[data-action="show-add"]')?.addEventListener('click', () => this.showForm('add'));
    editor.querySelector<HTMLButtonElement>('[data-action="show-generate"]')?.addEventListener('click', () => this.showForm('generate'));
    editor.querySelector<HTMLButtonElement>('[data-action="export"]')?.addEventListener('click', () => this.exportCoupons());
    editor.querySelectorAll<HTMLButtonElement>('[data-action="cancel-forms"]').forEach(button => {
      button.addEventListener('click', () => this.hideForms());
    });
    editor.querySelector<HTMLButtonElement>('[data-action="previous"]')?.addEventListener('click', () => this.changePage(this.page - 1));
    editor.querySelector<HTMLButtonElement>('[data-action="next"]')?.addEventListener('click', () => this.changePage(this.page + 1));
    editor.querySelector<HTMLInputElement>('input[name="query"]')?.addEventListener('input', event => this.search((event.target as HTMLInputElement).value));
    editor.querySelector<HTMLFormElement>('form[data-form="add"]')?.addEventListener('submit', event => void this.addCoupon(event));
    editor.querySelector<HTMLFormElement>('form[data-form="generate"]')?.addEventListener('submit', event => void this.generateCoupons(event));

    this.renderRows();
    this.syncDisabledState();
  }

  private renderRows(): void {
    const tbody = this.querySelector<HTMLTableSectionElement>('tbody');
    if (tbody == null) {
      return;
    }

    if (this.coupons.length === 0) {
      tbody.innerHTML = '<tr><td colspan="4">No coupon codes found</td></tr>';
      return;
    }

    const fragment = document.createDocumentFragment();

    for (const coupon of this.coupons) {
      const row = document.createElement('tr');
      row.innerHTML = `
        <td>${this.escapeHtml(coupon.couponCode ?? '')}</td>
        <td>${coupon.numberAvailable ?? ''}</td>
        <td>${this.formatDate(coupon.date)}</td>
        <td></td>
      `;

      const deleteButton = document.createElement('button');
      deleteButton.type = 'button';
      deleteButton.dataset.kind = 'danger';
      deleteButton.textContent = 'Delete';
      deleteButton.disabled = this.readonly;
      deleteButton.addEventListener('click', () => void this.deleteCoupon(coupon.couponCode ?? ''));
      row.lastElementChild?.append(deleteButton);
      fragment.append(row);
    }

    tbody.replaceChildren(fragment);
  }

  private async loadCoupons(): Promise<void> {
    const discountId = this.getContentKey();

    if (discountId == null) {
      this.renderCreateMessage();
      return;
    }

    this.setStatus('Loading coupons...');

    try {
      const params = new URLSearchParams({
        query: this.query,
        page: String(this.page),
        pageSize: String(this.pageSize),
      });
      const result = await this.fetchJson<CouponListResponse>(`/ekom/backoffice/coupon/discountId/${discountId}?${params}`);
      this.coupons = result.item1 ?? result.data ?? [];
      this.totalPages = result.item2 ?? result.totalPages ?? 0;
      this.renderEditor();
      this.setStatus('');
    } catch (error) {
      this.setStatus(this.getErrorMessage(error, 'Could not load coupons.'), true);
    }
  }

  private async addCoupon(event: Event): Promise<void> {
    event.preventDefault();

    if (this.readonly) {
      return;
    }

    const form = event.currentTarget as HTMLFormElement;
    const couponCode = this.getFormValue(form, 'couponCode');
    const numberAvailable = this.getFormNumber(form, 'numberAvailable');

    if (couponCode.length === 0 || numberAvailable == null) {
      this.setStatus('Coupon Code and Usage Limit are required fields.', true);
      return;
    }

    try {
      await this.fetchText(`/ekom/backoffice/coupon/${encodeURIComponent(couponCode)}/NumberAvailable/${numberAvailable}/discountId/${this.getContentKey()}`, { method: 'POST' });
      this.hideForms();
      form.reset();
      await this.loadCoupons();
      this.setStatus('Coupon Code added successfully.');
    } catch (error) {
      const response = error as { status?: number };
      this.setStatus(response.status === 409 ? 'Coupon Code already exists on this or another discount.' : this.getErrorMessage(error, 'Error on creating coupon.'), true);
    }
  }

  private async generateCoupons(event: Event): Promise<void> {
    event.preventDefault();

    if (this.readonly) {
      return;
    }

    const form = event.currentTarget as HTMLFormElement;
    const request: CouponGenerationRequest = {
      count: this.getFormNumber(form, 'count') ?? 0,
      numberAvailable: this.getFormNumber(form, 'numberAvailable') ?? -1,
      prefix: this.getFormValue(form, 'prefix'),
      randomLength: this.getFormNumber(form, 'randomLength') ?? 0,
      characterSet: this.getFormValue(form, 'characterSet') as CouponGenerationRequest['characterSet'],
    };

    if (request.count <= 0) {
      this.setStatus('Amount must be greater than zero.', true);
      return;
    }

    if (request.numberAvailable < 0) {
      this.setStatus('Usage Limit is required and can not be negative.', true);
      return;
    }

    if (request.randomLength <= 0) {
      this.setStatus('Random length must be greater than zero.', true);
      return;
    }

    try {
      const result = await this.fetchJson<CouponGenerationResponse>(`/ekom/backoffice/coupon/generate/discountId/${this.getContentKey()}`, {
        method: 'POST',
        body: JSON.stringify(request),
        headers: {
          'Content-Type': 'application/json',
        },
      });
      this.hideForms();
      await this.loadCoupons();
      this.setStatus(`Generated ${result.created ?? 0} coupon codes.`);
    } catch (error) {
      this.setStatus(this.getErrorMessage(error, 'Error on generating coupons.'), true);
    }
  }

  private async deleteCoupon(couponCode: string): Promise<void> {
    if (this.readonly || couponCode.length === 0) {
      return;
    }

    try {
      await this.fetchText(`/ekom/backoffice/coupon/${encodeURIComponent(couponCode)}/discountId/${this.getContentKey()}`, { method: 'DELETE' });
      await this.loadCoupons();
      this.setStatus('Coupon Code removed.');
    } catch (error) {
      this.setStatus(this.getErrorMessage(error, 'Error on removing coupon.'), true);
    }
  }

  private search(query: string): void {
    this.query = query;
    this.page = 1;
    void this.loadCoupons();
  }

  private changePage(page: number): void {
    if (page < 1 || (this.totalPages > 0 && page > this.totalPages)) {
      return;
    }

    this.page = page;
    void this.loadCoupons();
  }

  private exportCoupons(): void {
    const discountId = this.getContentKey();

    if (discountId != null) {
      window.location.href = `/ekom/backoffice/coupon/export/discountId/${discountId}`;
    }
  }

  private showForm(name: 'add' | 'generate'): void {
    this.hideForms();
    this.querySelector<HTMLFormElement>(`form[data-form="${name}"]`)?.removeAttribute('hidden');
  }

  private hideForms(): void {
    this.querySelectorAll<HTMLFormElement>('form').forEach(form => form.hidden = true);
  }

  private syncDisabledState(): void {
    this.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLButtonElement>('input, select, button').forEach(element => {
      if (element.matches('[data-action="export"], input[name="query"], [data-action="previous"], [data-action="next"]')) {
        return;
      }

      element.disabled = this.readonly;
    });
  }

  private getEditor(): HTMLDivElement {
    return this.querySelector<HTMLDivElement>('.ekom-coupon-editor')!;
  }

  private setStatus(message: string, isError = false): void {
    if (this.status == null) {
      return;
    }

    this.status.textContent = message;
    this.status.dataset.error = String(isError);
  }

  private getContentKey(): string | undefined {
    return window.location.pathname
      .split('/')
      .find(part => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(part));
  }

  private isCreateMode(): boolean {
    return window.location.pathname.includes('/workspace/document/create/');
  }

  private getFormValue(form: HTMLFormElement, name: string): string {
    return (new FormData(form).get(name)?.toString() ?? '').trim();
  }

  private getFormNumber(form: HTMLFormElement, name: string): number | undefined {
    const value = this.getFormValue(form, name);

    if (value.length === 0) {
      return undefined;
    }

    const parsed = Number.parseInt(value, 10);
    return Number.isNaN(parsed) ? undefined : parsed;
  }

  private formatDate(value: string | undefined): string {
    if (value == null || value.length === 0) {
      return '';
    }

    const date = new Date(value);

    if (Number.isNaN(date.getTime())) {
      return value;
    }

    return date.toLocaleString();
  }

  private escapeHtml(value: string): string {
    return value
      .replaceAll('&', '&amp;')
      .replaceAll('<', '&lt;')
      .replaceAll('>', '&gt;')
      .replaceAll('"', '&quot;')
      .replaceAll("'", '&#039;');
  }

  private escapeAttribute(value: string): string {
    return this.escapeHtml(value);
  }

  private getErrorMessage(error: unknown, fallback: string): string {
    return error instanceof Error ? error.message : fallback;
  }

  private async fetchJson<T>(url: string, init?: RequestInit): Promise<T> {
    const response = await fetch(url, {
      credentials: 'same-origin',
      headers: {
        Accept: 'application/json',
        ...init?.headers,
      },
      ...init,
    });

    if (!response.ok) {
      throw Object.assign(new Error(`Request to ${url} failed with status ${response.status}.`), { status: response.status });
    }

    return await response.json() as T;
  }

  private async fetchText(url: string, init?: RequestInit): Promise<string> {
    const response = await fetch(url, {
      credentials: 'same-origin',
      ...init,
    });

    if (!response.ok) {
      throw Object.assign(new Error(`Request to ${url} failed with status ${response.status}.`), { status: response.status });
    }

    return await response.text();
  }
}

customElements.define('ekom-coupon-editor', EkomCouponEditorElement);

export default EkomCouponEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-coupon-editor': EkomCouponEditorElement;
  }
}
