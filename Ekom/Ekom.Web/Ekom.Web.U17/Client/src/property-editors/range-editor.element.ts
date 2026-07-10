import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import type {
  ManifestPropertyEditorUi,
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type RangeValue = Record<string, CurrencyRange[]>;

type CurrencyRange = {
  currency: string;
  value: number;
};

type EkomStore = {
  alias?: string;
  currencies?: EkomCurrency[];
};

type EkomCurrency = {
  currencyValue?: string;
  currencySymbol?: string;
  isoCurrencySymbol?: string;
};

export class EkomRangeEditorElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private editor?: HTMLDivElement;
  private status?: HTMLParagraphElement;
  private stores: EkomStore[] = [];
  private rawValue: unknown;
  private internalValue: RangeValue = {};

  get value(): RangeValue {
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
    this.setStatus('Loading ranges...');

    try {
      this.stores = await this.fetchJson<EkomStore[]>(`/ekom/backoffice/Stores/${this.getNodeId()}`);
      this.internalValue = this.ensureRangeStructure(this.normalizeValue(this.rawValue));
      this.renderRanges();
      this.setStatus('');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Could not load ranges.';
      this.setStatus(message, true);
    }
  }

  private renderShell(): void {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-range-editor {
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

        .ekom-range-row {
          display: flex;
          align-items: center;
          gap: var(--uui-size-space-2, 8px);
          margin-bottom: var(--uui-size-space-3, 12px);
        }

        .ekom-range-row:last-child {
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
      <div class="ekom-range-editor"></div>
      <p aria-live="polite"></p>
    `;

    this.editor = this.querySelector('.ekom-range-editor') ?? undefined;
    this.status = this.querySelector('p') ?? undefined;
  }

  private renderRanges(): void {
    if (this.editor == null) {
      return;
    }

    const fragment = document.createDocumentFragment();

    for (const store of this.stores) {
      const storeAlias = store.alias;

      if (storeAlias == null) {
        continue;
      }

      const fieldset = document.createElement('fieldset');
      const legend = document.createElement('legend');
      legend.textContent = storeAlias;
      fieldset.append(legend);

      for (const currency of store.currencies ?? []) {
        if (currency.currencyValue == null) {
          continue;
        }

        fieldset.append(this.createRangeInput(storeAlias, currency));
      }

      fragment.append(fieldset);
    }

    this.editor.replaceChildren(fragment);
    this.syncDisabledState();
  }

  private createRangeInput(storeAlias: string, currency: EkomCurrency): HTMLDivElement {
    const currencyValue = currency.currencyValue ?? '';
    const row = document.createElement('div');
    row.className = 'ekom-range-row';

    const id = `range_${currency.isoCurrencySymbol ?? currencyValue}_${this.name ?? 'range'}_${storeAlias}`;
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
    input.value = String(this.getRange(storeAlias, currencyValue));
    input.addEventListener('input', () => this.setRange(storeAlias, currencyValue, input.value));

    row.append(label, input);

    return row;
  }

  private setRange(storeAlias: string, currency: string, rawValue: string): void {
    const value = this.parseRange(rawValue);
    const ranges = [...(this.internalValue[storeAlias] ?? [])];
    const existing = ranges.find(item => item.currency === currency);

    if (existing == null) {
      ranges.push({
        currency,
        value,
      });
    } else {
      existing.value = value;
    }

    this.internalValue = {
      ...this.internalValue,
      [storeAlias]: ranges,
    };

    this.emitChange();
  }

  private getRange(storeAlias: string, currency: string): number {
    return this.internalValue[storeAlias]?.find(item => item.currency === currency)?.value ?? 0;
  }

  private ensureRangeStructure(value: RangeValue): RangeValue {
    const nextValue: RangeValue = {};

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
          currency: currencyValue,
          value: value[storeAlias]?.find(item => item.currency === currencyValue)?.value ?? 0,
        });
      }
    }

    return nextValue;
  }

  private normalizeValue(value: unknown): RangeValue {
    if (value == null || value === '') {
      return {};
    }

    if (this.isRecord(value) && this.isRecord(value.values)) {
      return this.normalizeWrappedValue(value.values);
    }

    if (this.isRecord(value)) {
      return this.normalizeCurrentFormat(value) ?? {};
    }

    return this.normalizePrimitiveValue(value);
  }

  private normalizeWrappedValue(values: Record<string, unknown>): RangeValue {
    const nextValue: RangeValue = {};

    for (const [storeAlias, rawStoreValue] of Object.entries(values)) {
      const parsedValue = typeof rawStoreValue === 'string'
        ? this.tryParseJson(rawStoreValue)
        : rawStoreValue;

      if (Array.isArray(parsedValue)) {
        nextValue[storeAlias] = this.normalizeRangeArray(parsedValue);
      }
    }

    return nextValue;
  }

  private normalizeCurrentFormat(value: Record<string, unknown>): RangeValue | undefined {
    const nextValue: RangeValue = {};

    for (const [storeAlias, ranges] of Object.entries(value)) {
      if (storeAlias === 'undefined') {
        continue;
      }

      if (!Array.isArray(ranges)) {
        return undefined;
      }

      nextValue[storeAlias] = this.normalizeRangeArray(ranges);
    }

    return nextValue;
  }

  private normalizeRangeArray(ranges: unknown[]): CurrencyRange[] {
    return ranges.map(range => {
      if (!this.isRecord(range)) {
        return undefined;
      }

      return {
        currency: String(range.currency ?? range.Currency ?? ''),
        value: this.parseRange(range.value ?? range.Value),
      };
    }).filter(range => range != null);
  }

  private normalizePrimitiveValue(value: unknown): RangeValue {
    const fallbackStore = this.stores[0]?.alias ?? '';
    const fallbackCurrency = this.stores[0]?.currencies?.[0]?.currencyValue ?? '';

    if (fallbackStore.length === 0 || fallbackCurrency.length === 0) {
      return {};
    }

    return {
      [fallbackStore]: [
        {
          currency: fallbackCurrency,
          value: this.parseRange(value),
        },
      ],
    };
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

      input.value = String(this.getRange(storeAlias, currency));
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

  private parseRange(value: unknown): number {
    if (value == null || value === '') {
      return 0;
    }

    const parsed = Number(String(value).replace(',', '.'));
    return Number.isFinite(parsed) ? parsed : 0;
  }

  private tryParseJson(value: string): unknown {
    try {
      return JSON.parse(value) as unknown;
    } catch {
      return value;
    }
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

customElements.define('ekom-range-editor', EkomRangeEditorElement);

export default EkomRangeEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-range-editor': EkomRangeEditorElement;
  }
}
