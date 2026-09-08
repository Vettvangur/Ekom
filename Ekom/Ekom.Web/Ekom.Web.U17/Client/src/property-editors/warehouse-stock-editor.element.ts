import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import type {
  ManifestPropertyEditorUi,
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type WarehouseStockValue = {
  sku: string;
  items: WarehouseStockItem[];
};

type WarehouseStockItem = {
  storeAlias: string;
  warehouseKey: string;
  code: string;
  name: string;
  visible: boolean;
  balance: number | null;
  isDirty: boolean;
};

export class EkomWarehouseStockEditorElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;

  private editor?: HTMLDivElement;
  private status?: HTMLParagraphElement;
  private warehouseStock: WarehouseStockValue = { sku: '', items: [] };

  get value(): WarehouseStockValue {
    return this.warehouseStock;
  }

  set value(value: unknown) {
    this.warehouseStock = this.normalizeValue(value);
    this.syncInputs();
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
    void this.loadWarehouseStock();
  }

  private async loadWarehouseStock(): Promise<void> {
    const contentKey = this.getContentKey();
    if (contentKey == null) {
      this.setStatus('Save the product or variant with a SKU before editing warehouse stock.');
      return;
    }

    this.setStatus('Loading warehouse stock...');

    try {
      const loadedStock = await this.fetchJson<WarehouseStockValue>(`/ekom/backoffice/WarehouseStock/${contentKey}`);
      this.warehouseStock = this.mergePendingChanges(loadedStock, this.warehouseStock);
      this.renderStock();

      if (this.warehouseStock.sku.length === 0) {
        this.setStatus('Save a SKU before editing warehouse stock.');
      } else if (this.warehouseStock.items.length === 0) {
        this.setStatus('No warehouses are configured for this product or variant.');
      } else {
        this.setStatus('');
      }
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Could not load warehouse stock.';
      this.setStatus(message, true);
    }
  }

  private renderShell(): void {
    this.innerHTML = `
      <style>
        :host { display: block; }
        .editor { display: grid; gap: var(--uui-size-space-4, 16px); width: 100%; max-width: 600px; }
        fieldset { border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); margin: 0; padding: var(--uui-size-space-4, 16px); background: var(--uui-color-surface, #fff); }
        legend { padding: 0 var(--uui-size-space-2, 8px); color: var(--uui-color-text, #1b264f); font-size: 16px; font-weight: 700; }
        .header, .row { display: grid; grid-template-columns: 100px minmax(150px, 1fr) 110px; gap: var(--uui-size-space-3, 12px); align-items: center; }
        .header { padding: 0 var(--uui-size-space-2, 8px) var(--uui-size-space-2, 8px); color: var(--uui-color-text-alt, #515054); font-size: 12px; font-weight: 700; text-transform: uppercase; }
        .header > :last-child { text-align: center; }
        .row { min-height: 40px; padding: var(--uui-size-space-2, 8px); border-top: 1px solid var(--uui-color-border, #d8d7d9); }
        input { box-sizing: border-box; width: 100%; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; text-align: right; }
        input:disabled { cursor: not-allowed; opacity: 0.55; }
        .hidden { color: var(--uui-color-text-alt, #515054); font-size: 12px; }
        p { margin: 0; color: var(--uui-color-text-alt, #515054); line-height: 1.4; }
        p[data-error='true'] { color: var(--uui-color-danger, #d42054); }
        @media (max-width: 600px) {
          .header { display: none; }
          .row { grid-template-columns: minmax(80px, 1fr) minmax(120px, 2fr) 90px; gap: var(--uui-size-space-2, 8px); }
        }
      </style>
      <div class="editor"></div>
      <p aria-live="polite"></p>
    `;

    this.editor = this.querySelector('.editor') ?? undefined;
    this.status = this.querySelector('p') ?? undefined;
  }

  private renderStock(): void {
    if (this.editor == null) {
      return;
    }

    const fragment = document.createDocumentFragment();
    const stores = [...new Set(this.warehouseStock.items.map(item => item.storeAlias))];

    for (const storeAlias of stores) {
      const fieldset = document.createElement('fieldset');
      const legend = document.createElement('legend');
      legend.textContent = storeAlias;

      const header = document.createElement('div');
      header.className = 'header';
      for (const label of ['Code', 'Warehouse', 'Balance']) {
        const cell = document.createElement('div');
        cell.textContent = label;
        header.append(cell);
      }

      fieldset.append(legend, header);
      for (const item of this.warehouseStock.items.filter(item => item.storeAlias === storeAlias)) {
        fieldset.append(this.createRow(item));
      }

      fragment.append(fieldset);
    }

    this.editor.replaceChildren(fragment);
    this.syncDisabledState();
  }

  private createRow(item: WarehouseStockItem): HTMLDivElement {
    const row = document.createElement('div');
    row.className = 'row';

    const code = document.createElement('div');
    code.textContent = item.code;

    const name = document.createElement('div');
    name.textContent = item.name;
    if (!item.visible) {
      const hidden = document.createElement('span');
      hidden.className = 'hidden';
      hidden.textContent = ' (Hidden)';
      name.append(hidden);
    }

    const input = document.createElement('input');
    input.type = 'number';
    input.min = '0';
    input.step = 'any';
    input.placeholder = 'Not set';
    input.dataset.store = item.storeAlias;
    input.dataset.warehouse = item.warehouseKey;
    input.value = item.balance == null ? '' : String(item.balance);
    input.addEventListener('input', () => this.setBalance(item.storeAlias, item.warehouseKey, input));

    row.append(code, name, input);
    return row;
  }

  private setBalance(storeAlias: string, warehouseKey: string, input: HTMLInputElement): void {
    const rawValue = input.value;
    const parsed = rawValue === '' ? null : Number(rawValue);
    if (parsed != null && (!Number.isFinite(parsed) || parsed < 0)) {
      input.setCustomValidity('Balance must be a non-negative number.');
      return;
    }

    input.setCustomValidity('');
    const balance = parsed;

    this.warehouseStock = {
      ...this.warehouseStock,
      items: this.warehouseStock.items.map(item => item.storeAlias === storeAlias && item.warehouseKey === warehouseKey
        ? { ...item, balance, isDirty: true }
        : item),
    };
    this.dispatchEvent(new UmbChangeEvent());
  }

  private normalizeValue(value: unknown): WarehouseStockValue {
    const parsed = typeof value === 'string' ? this.tryParseJson(value) : value;
    if (parsed == null || typeof parsed !== 'object' || Array.isArray(parsed)) {
      return { sku: '', items: [] };
    }

    const stock = parsed as Partial<WarehouseStockValue>;
    return {
      sku: typeof stock.sku === 'string' ? stock.sku : '',
      items: Array.isArray(stock.items)
        ? stock.items
          .filter(item => item != null && typeof item === 'object')
          .map(item => ({ ...item, isDirty: item.isDirty === true }))
        : [],
    };
  }

  private mergePendingChanges(loaded: WarehouseStockValue, current: WarehouseStockValue): WarehouseStockValue {
    const pending = current.items.filter(item => item.isDirty);
    const loadedIdentities = new Set(loaded.items.map(item => this.getIdentity(item)));

    return {
      ...loaded,
      items: [
        ...loaded.items.map(item => {
          const pendingItem = pending.find(candidate => this.getIdentity(candidate) === this.getIdentity(item));
          return pendingItem == null
            ? { ...item, isDirty: false }
            : { ...item, balance: pendingItem.balance, isDirty: true };
        }),
        ...pending.filter(item => !loadedIdentities.has(this.getIdentity(item))),
      ],
    };
  }

  private getIdentity(item: WarehouseStockItem): string {
    return `${item.storeAlias.trim().toUpperCase()}|${item.warehouseKey.toUpperCase()}`;
  }

  private syncInputs(): void {
    for (const input of this.querySelectorAll<HTMLInputElement>('input[data-warehouse]')) {
      const item = this.warehouseStock.items.find(item => item.storeAlias === input.dataset.store && item.warehouseKey === input.dataset.warehouse);
      input.value = item?.balance == null ? '' : String(item.balance);
    }
  }

  private syncDisabledState(): void {
    for (const input of this.querySelectorAll<HTMLInputElement>('input')) {
      input.disabled = this.readonly;
    }
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

  private tryParseJson(value: string): unknown {
    try {
      return JSON.parse(value) as unknown;
    } catch {
      return undefined;
    }
  }

  private async fetchJson<T>(url: string): Promise<T> {
    const response = await fetch(url, {
      credentials: 'same-origin',
      headers: { Accept: 'application/json' },
    });

    if (!response.ok) {
      throw new Error(`Request to ${url} failed with status ${response.status}.`);
    }

    return await response.json() as T;
  }
}

customElements.define('ekom-warehouse-stock-editor', EkomWarehouseStockEditorElement);

export default EkomWarehouseStockEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-warehouse-stock-editor': EkomWarehouseStockEditorElement;
  }
}
