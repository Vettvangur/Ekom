import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';
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

type EkomCurrency = {
  currencyValue?: string;
  currencySymbol?: string;
  isoCurrencySymbol?: string;
};

type LegacyPriceValue = Record<string, unknown>;

export class EkomPriceEditorElement extends UmbElementMixin(HTMLElement) implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private editor?: HTMLDivElement;
  private status?: HTMLParagraphElement;
  private stores: EkomStore[] = [];
  private documentId = '';
  private requestId = 0;
  private rawValue: unknown;
  private internalValue: PriceValue = {};

  get value(): PriceValue {
    return this.internalValue;
  }

  set value(value: unknown) {
    this.rawValue = value;
    const normalizedValue = this.normalizeValue(value);
    this.internalValue = this.stores.length > 0
      ? this.ensurePriceStructure(normalizedValue)
      : normalizedValue;
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
    super.connectedCallback();
    this.renderShell();
    this.setStatus('Loading prices...');
    void this.loadStores();

    this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, context => {
      if (context == null) {
        return;
      }

      this.updateDocumentId(context.getUnique());

      this.observe(context.unique, value => this.updateDocumentId(value), 'ekomPriceDocumentId');
    });
  }

  override disconnectedCallback(): void {
    this.requestId++;
    super.disconnectedCallback();
  }

  private async loadStores(): Promise<void> {
    const documentId = this.documentId || this.getDocumentIdFromUrl();
    if (documentId.length === 0) {
      this.setStatus('Save the document before editing prices.');
      return;
    }

    const requestId = ++this.requestId;
    this.setStatus('Loading prices...');

    try {
      const stores = await this.fetchJson<EkomStore[]>(`/ekom/backoffice/Stores/${encodeURIComponent(documentId)}`);
      if (requestId !== this.requestId) {
        return;
      }

      this.stores = stores;
      const currentValue = Object.keys(this.internalValue).length > 0
        ? this.internalValue
        : this.normalizeValue(this.rawValue);
      this.internalValue = this.ensurePriceStructure(currentValue);
      this.renderPrices();
      this.setStatus('');
    } catch (error) {
      if (requestId !== this.requestId) {
        return;
      }

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
    const showStoreGroups = this.stores.length > 1;

    for (const store of this.stores) {
      const storeAlias = store.alias;

      if (storeAlias == null) {
        continue;
      }

      const container = document.createElement(showStoreGroups ? 'fieldset' : 'div');

      if (showStoreGroups) {
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
    this.rawValue = this.internalValue;

    this.emitChange();
  }

  private getPrice(storeAlias: string, currency: string): number {
    return this.internalValue[storeAlias]?.find(item => item.Currency === currency)?.Price ?? 0;
  }

  private ensurePriceStructure(value: PriceValue): PriceValue {
    const nextValue: PriceValue = Object.fromEntries(Object.entries(value).map(([storeAlias, prices]) => [
      storeAlias,
      prices.map(price => ({ ...price })),
    ]));

    for (const store of this.stores) {
      const storeAlias = store.alias;

      if (storeAlias == null) {
        continue;
      }

      const prices = nextValue[storeAlias] ?? [];

      for (const currency of store.currencies ?? []) {
        const currencyValue = currency.currencyValue;

        if (currencyValue == null) {
          continue;
        }

        if (!prices.some(item => item.Currency === currencyValue)) {
          prices.push({
            Currency: currencyValue,
            Price: 0,
          });
        }
      }

      nextValue[storeAlias] = prices;
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

  private updateDocumentId(value: string | null | undefined): void {
    if (value == null || value === this.documentId) {
      return;
    }

    this.documentId = value;
    void this.loadStores();
  }

  private getDocumentIdFromUrl(): string {
    const url = new URL(window.location.href);
    const explicitId = url.searchParams.get('id');

    if (explicitId != null && (/^\d+$/.test(explicitId) || this.isGuid(explicitId))) {
      return explicitId;
    }

    return url.pathname
      .split('/')
      .reverse()
      .find(part => /^\d+$/.test(part) || this.isGuid(part)) ?? '';
  }

  private isGuid(value: string): boolean {
    return /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value);
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
