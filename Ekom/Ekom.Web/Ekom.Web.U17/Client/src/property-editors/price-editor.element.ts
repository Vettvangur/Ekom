import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import type {
  ManifestPropertyEditorUi,
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type PriceValue = Record<string, CurrencyPrice[]>;

type CurrencyPrice = {
  Currency: string;
  Price: number;
};

type EkomStore = {
  alias?: string;
  title?: string;
  currencies?: EkomCurrency[];
};

type EkomConfig = {
  perStoreStock?: boolean;
};

type EkomCurrency = {
  currencyValue?: string;
  currencySymbol?: string;
  isoCurrencySymbol?: string;
};

type LegacyPriceValue = Record<string, unknown>;

export class EkomPriceEditorElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private editor?: HTMLDivElement;
  private status?: HTMLParagraphElement;
  private stores: EkomStore[] = [];
  private showStoreFieldsets = true;
  private rawValue: unknown;
  private internalValue: PriceValue = {};

  get value(): PriceValue {
    return this.internalValue;
  }

  set value(value: unknown) {
    this.rawValue = value;
    this.internalValue = this.normalizeValue(value);
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
    void this.loadStores();
  }

  private async loadStores(): Promise<void> {
    this.setStatus('Loading prices...');

    try {
      const [config, stores] = await Promise.all([
        this.fetchJson<EkomConfig>('/ekom/backoffice/Config'),
        this.fetchJson<EkomStore[]>(`/ekom/backoffice/Stores/${this.getNodeId()}`),
      ]);

      this.showStoreFieldsets = config.perStoreStock !== false;
      this.stores = stores;
      this.internalValue = this.ensurePriceStructure(this.normalizeValue(this.rawValue));
      this.renderPrices();
      this.setStatus('');
      this.emitChange();
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Could not load prices.';
      this.setStatus(message, true);
    }
  }

  private renderShell(): void {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-price-editor {
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

        .ekom-price-row {
          display: flex;
          align-items: center;
          gap: var(--uui-size-space-2, 8px);
          margin-bottom: var(--uui-size-space-3, 12px);
        }

        .ekom-price-row:last-child {
          margin-bottom: 0;
        }

        label {
          min-width: 45px;
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
      <div class="ekom-price-editor"></div>
      <p aria-live="polite"></p>
    `;

    this.editor = this.querySelector('.ekom-price-editor') ?? undefined;
    this.status = this.querySelector('p') ?? undefined;
  }

  private renderPrices(): void {
    if (this.editor == null) {
      return;
    }

    const fragment = document.createDocumentFragment();

    for (const store of this.stores) {
      const storeAlias = store.alias;

      if (storeAlias == null) {
        continue;
      }

      const container = this.showStoreFieldsets
        ? document.createElement('fieldset')
        : document.createDocumentFragment();

      if (container instanceof HTMLFieldSetElement) {
        const legend = document.createElement('legend');
        legend.textContent = storeAlias;
        container.append(legend);
      }

      for (const currency of store.currencies ?? []) {
        const currencyValue = currency.currencyValue;

        if (currencyValue == null) {
          continue;
        }

        container.append(this.createPriceInput(storeAlias, currency));
      }

      fragment.append(container);
    }

    this.editor.replaceChildren(fragment);
    this.syncDisabledState();
  }

  private createPriceInput(storeAlias: string, currency: EkomCurrency): HTMLDivElement {
    const currencyValue = currency.currencyValue ?? '';
    const row = document.createElement('div');
    row.className = 'ekom-price-row';

    const id = `price_${currency.isoCurrencySymbol ?? currencyValue}_${this.name ?? 'price'}_${storeAlias}`;
    const label = document.createElement('label');
    label.htmlFor = id;
    label.textContent = currency.isoCurrencySymbol ?? currencyValue;

    const input = document.createElement('input');
    input.type = 'number';
    input.min = '0';
    input.step = 'any';
    input.id = id;
    input.dataset.store = storeAlias;
    input.dataset.currency = currencyValue;
    input.value = String(this.getPrice(storeAlias, currencyValue));
    input.addEventListener('input', () => this.setPrice(storeAlias, currencyValue, input.value));

    const symbol = document.createElement('span');
    symbol.textContent = currency.currencySymbol ?? '';

    row.append(label, input, symbol);

    return row;
  }

  private setPrice(storeAlias: string, currency: string, rawPrice: string): void {
    const price = this.parsePrice(rawPrice);
    let updatedExisting = false;
    const prices = (this.internalValue[storeAlias] ?? []).map(item => {
      if (item.Currency !== currency) {
        return item;
      }

      updatedExisting = true;
      return {
        ...item,
        Price: price,
      };
    });

    if (!updatedExisting) {
      prices.push({
        Currency: currency,
        Price: price,
      });
    }

    this.internalValue = {
      ...this.internalValue,
      [storeAlias]: prices,
    };

    this.emitChange();
  }

  private getPrice(storeAlias: string, currency: string): number {
    return this.internalValue[storeAlias]?.find(item => item.Currency === currency)?.Price ?? 0;
  }

  private ensurePriceStructure(value: PriceValue): PriceValue {
    const nextValue: PriceValue = {};

    for (const store of this.stores) {
      const storeAlias = store.alias;

      if (storeAlias == null) {
        continue;
      }

      nextValue[storeAlias] = [];

      for (const currency of store.currencies ?? []) {
        const currencyValue = currency.currencyValue;

        if (currencyValue == null) {
          continue;
        }

        nextValue[storeAlias].push({
          Currency: currencyValue,
          Price: value[storeAlias]?.find(item => item.Currency === currencyValue)?.Price ?? 0,
        });
      }
    }

    return nextValue;
  }

  private normalizeValue(value: unknown): PriceValue {
    if (value == null || value === '') {
      return {};
    }

    if (!this.isRecord(value)) {
      return {};
    }

    const validValue = this.normalizeCurrentFormat(value);

    if (validValue != null) {
      return validValue;
    }

    return this.transformLegacyValue(value);
  }

  private normalizeCurrentFormat(value: LegacyPriceValue): PriceValue | undefined {
    const nextValue: PriceValue = {};

    for (const [storeAlias, prices] of Object.entries(value)) {
      if (storeAlias === 'undefined') {
        continue;
      }

      if (!Array.isArray(prices)) {
        return undefined;
      }

      nextValue[storeAlias] = prices.map(price => {
        if (!this.isRecord(price) || !('Currency' in price) || !('Price' in price)) {
          return undefined;
        }

        return {
          Currency: String(price.Currency),
          Price: this.parsePrice(price.Price),
        };
      }).filter(price => price != null);
    }

    return nextValue;
  }

  private transformLegacyValue(value: LegacyPriceValue): PriceValue {
    const nextValue: PriceValue = {};
    const fallbackCurrency = this.stores[0]?.currencies?.[0]?.currencyValue ?? '';

    for (const [storeAlias, storeValue] of Object.entries(value)) {
      if (storeAlias === 'undefined' || !this.isRecord(storeValue)) {
        continue;
      }

      nextValue[storeAlias] = Object.values(storeValue).map(item => {
        const price = this.isRecord(item) && 'Price' in item
          ? item.Price
          : item;

        return {
          Currency: fallbackCurrency,
          Price: this.parsePrice(price),
        };
      });
    }

    return nextValue;
  }

  private syncInputs(): void {
    if (this.editor == null) {
      return;
    }

    for (const input of this.editor.querySelectorAll<HTMLInputElement>('input[data-store][data-currency]')) {
      const storeAlias = input.dataset.store;
      const currency = input.dataset.currency;

      if (storeAlias == null || currency == null) {
        continue;
      }

      input.value = String(this.getPrice(storeAlias, currency));
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

  private parsePrice(value: unknown): number {
    if (value == null || value === '') {
      return 0;
    }

    const parsed = Number(value);
    return Number.isFinite(parsed) ? parsed : 0;
  }

  private isRecord(value: unknown): value is Record<string, unknown> {
    return value != null && typeof value === 'object' && !Array.isArray(value);
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

customElements.define('ekom-price-editor', EkomPriceEditorElement);

export default EkomPriceEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-price-editor': EkomPriceEditorElement;
  }
}
