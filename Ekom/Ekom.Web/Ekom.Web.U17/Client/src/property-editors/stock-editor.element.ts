import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import type {
  ManifestPropertyEditorUi,
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type StockValue = StockItem[];

type StockItem = {
  storeAlias: string;
  value: number;
};

type EkomConfig = {
  perStoreStock?: boolean;
};

type EkomStore = {
  alias?: string;
  title?: string;
};

export class EkomStockEditorElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private editor?: HTMLDivElement;
  private status?: HTMLParagraphElement;
  private stocks: StockValue = [];

  get value(): StockValue {
    return this.stocks;
  }

  set value(value: unknown) {
    this.stocks = this.normalizeValue(value);
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
    void this.loadStock();
  }

  private async loadStock(): Promise<void> {
    this.setStatus('Loading stock...');

    try {
      const config = await this.fetchJson<EkomConfig>('/ekom/backoffice/Config');
      const contentKey = this.getContentKey();

      this.stocks = config.perStoreStock
        ? await this.loadPerStoreStock(contentKey)
        : [await this.loadStockItem(contentKey, '')];

      this.renderStock();
      this.setStatus('');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Could not load stock.';
      this.setStatus(message, true);
    }
  }

  private async loadPerStoreStock(contentKey: string | undefined): Promise<StockValue> {
    const stores = await this.fetchJson<EkomStore[]>(`/ekom/backoffice/Stores/${this.getNodeId()}`);

    if (stores.length <= 1) {
      return [await this.loadStockItem(contentKey, '')];
    }

    const stockItems: StockValue = [];

    for (const store of stores) {
      if (store.alias == null) {
        continue;
      }

      stockItems.push(await this.loadStockItem(contentKey, store.alias));
    }

    return stockItems;
  }

  private async loadStockItem(contentKey: string | undefined, storeAlias: string): Promise<StockItem> {
    if (contentKey == null) {
      return {
        storeAlias,
        value: this.getExistingValue(storeAlias),
      };
    }

    const url = storeAlias.length > 0
      ? `/ekom/backoffice/Stock/${contentKey}/StoreAlias/${storeAlias}`
      : `/ekom/backoffice/Stock/${contentKey}`;

    try {
      const value = await this.fetchJson<number>(url);

      return {
        storeAlias,
        value: this.parseStock(value),
      };
    } catch {
      return {
        storeAlias,
        value: this.getExistingValue(storeAlias),
      };
    }
  }

  private renderShell(): void {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-stock-editor {
          display: grid;
          gap: var(--uui-size-space-5, 20px);
        }

        fieldset {
          border: 1px solid var(--uui-color-border, #d8d7d9);
          border-radius: var(--uui-border-radius, 3px);
          margin: 0;
          padding: var(--uui-size-space-4, 16px);
        }

        legend {
          padding: 0 var(--uui-size-space-2, 8px);
          font-size: 18px;
          font-weight: 700;
        }

        input {
          box-sizing: border-box;
          min-height: 32px;
          border: 1px solid var(--uui-color-border, #d8d7d9);
          border-radius: var(--uui-border-radius, 3px);
          padding: var(--uui-size-space-2, 8px);
          background: var(--uui-color-surface, #fff);
          color: var(--uui-color-text, #1b264f);
          font: inherit;
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
      <div class="ekom-stock-editor"></div>
      <p aria-live="polite"></p>
    `;

    this.editor = this.querySelector('.ekom-stock-editor') ?? undefined;
    this.status = this.querySelector('p') ?? undefined;
  }

  private renderStock(): void {
    if (this.editor == null) {
      return;
    }

    const fragment = document.createDocumentFragment();

    for (const stock of this.stocks) {
      if (stock.storeAlias.length === 0) {
        fragment.append(this.createStockInput(stock));
        continue;
      }

      const fieldset = document.createElement('fieldset');
      const legend = document.createElement('legend');
      legend.textContent = stock.storeAlias;
      fieldset.append(legend, this.createStockInput(stock));
      fragment.append(fieldset);
    }

    this.editor.replaceChildren(fragment);
    this.syncDisabledState();
  }

  private createStockInput(stock: StockItem): HTMLInputElement {
    const input = document.createElement('input');
    input.type = 'number';
    input.min = '0';
    input.step = 'any';
    input.id = `stock_${stock.storeAlias}`;
    input.dataset.store = stock.storeAlias;
    input.value = String(stock.value);
    input.addEventListener('input', () => this.setStock(stock.storeAlias, input.value));

    return input;
  }

  private setStock(storeAlias: string, rawValue: string): void {
    const value = this.parseStock(rawValue);
    let updatedExisting = false;
    const stocks = this.stocks.map(item => {
      if (item.storeAlias !== storeAlias) {
        return item;
      }

      updatedExisting = true;
      return {
        ...item,
        value,
      };
    });

    if (!updatedExisting) {
      stocks.push({
        storeAlias,
        value,
      });
    }

    this.stocks = stocks;
    this.emitChange();
  }

  private getExistingValue(storeAlias: string): number {
    return this.stocks.find(item => item.storeAlias === storeAlias)?.value ?? 0;
  }

  private normalizeValue(value: unknown): StockValue {
    if (!Array.isArray(value)) {
      return [];
    }

    return value
      .filter(item => item != null && typeof item === 'object')
      .map(item => {
        const stock = item as Partial<StockItem>;

        return {
          storeAlias: stock.storeAlias ?? '',
          value: this.parseStock(stock.value),
        };
      });
  }

  private syncInputs(): void {
    if (this.editor == null) {
      return;
    }

    for (const input of this.editor.querySelectorAll<HTMLInputElement>('input[data-store]')) {
      input.value = String(this.getExistingValue(input.dataset.store ?? ''));
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

  private emitChange(): void {
    this.dispatchEvent(new UmbChangeEvent());
  }

  private getContentKey(): string | undefined {
    return window.location.pathname
      .split('/')
      .find(part => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(part));
  }

  private getNodeId(): number {
    const url = new URL(window.location.href);
    const explicitId = url.searchParams.get('id');

    if (explicitId != null) {
      const parsed = Number.parseInt(explicitId, 10);

      if (!Number.isNaN(parsed)) {
        return parsed;
      }
    }

    const numericPathPart = url.pathname
      .split('/')
      .reverse()
      .find(part => /^\d+$/.test(part));

    if (numericPathPart == null) {
      return 0;
    }

    return Number.parseInt(numericPathPart, 10);
  }

  private parseStock(value: unknown): number {
    if (value == null || value === '') {
      return 0;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  private async fetchJson<T>(url: string): Promise<T> {
    const response = await fetch(url, {
      credentials: 'same-origin',
      headers: {
        Accept: 'application/json',
      },
    });

    if (!response.ok) {
      throw new Error(`Request to ${url} failed with status ${response.status}.`);
    }

    return await response.json() as T;
  }
}

customElements.define('ekom-stock-editor', EkomStockEditorElement);

export default EkomStockEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-stock-editor': EkomStockEditorElement;
  }
}
