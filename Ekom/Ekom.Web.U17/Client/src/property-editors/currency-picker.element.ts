import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import type {
  ManifestPropertyEditorUi,
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type CurrencyValue = CurrencyItem[];

type CurrencyItem = {
  currencyFormat: string;
  currencyValue: string;
  sort: number;
};

export class EkomCurrencyPickerElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private cultureInput?: HTMLInputElement;
  private formatInput?: HTMLInputElement;
  private addButton?: HTMLButtonElement;
  private removeButton?: HTMLButtonElement;
  private select?: HTMLSelectElement;
  private currencies: CurrencyValue = [];

  get value(): CurrencyValue {
    return this.currencies;
  }

  set value(value: unknown) {
    this.currencies = this.normalizeValue(value);
    this.renderOptions();
  }

  get readonly(): boolean {
    return this.hasAttribute('readonly');
  }

  set readonly(value: boolean) {
    this.toggleAttribute('readonly', value);
    this.syncDisabledState();
  }

  override connectedCallback(): void {
    this.render();
  }

  private addCurrency(event: Event): void {
    event.preventDefault();

    if (this.readonly || this.cultureInput == null || this.formatInput == null) {
      return;
    }

    const currencyValue = this.cultureInput.value.trim();
    const currencyFormat = this.formatInput.value.trim();

    if (currencyValue.length === 0 || currencyFormat.length === 0) {
      return;
    }

    this.currencies = [
      ...this.currencies,
      {
        currencyFormat,
        currencyValue,
        sort: this.currencies.length,
      },
    ];

    this.cultureInput.value = '';
    this.formatInput.value = '';
    this.renderOptions();
    this.emitChange();
  }

  private removeCurrency(event: Event): void {
    event.preventDefault();

    if (this.readonly || this.select == null || this.select.value.length === 0) {
      return;
    }

    const selectedIndex = Number.parseInt(this.select.value, 10);

    if (Number.isNaN(selectedIndex)) {
      return;
    }

    this.currencies = this.currencies
      .filter((_, index) => index !== selectedIndex)
      .map((item, index) => ({
        ...item,
        sort: index,
      }));

    this.renderOptions();
    this.emitChange();
  }

  private render(): void {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-currency-picker {
          display: grid;
          gap: var(--uui-size-space-4, 16px);
          justify-items: start;
        }

        .ekom-currency-form {
          display: flex;
          flex-wrap: wrap;
          align-items: end;
          gap: var(--uui-size-space-4, 16px);
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

        select {
          min-width: 320px;
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
      </style>
      <div class="ekom-currency-picker">
        <div class="ekom-currency-form">
          <label>
            Currency Culture:
            <input type="text" name="currencyCulture" autocomplete="off" />
          </label>
          <label>
            Currency Format:
            <input type="text" name="currencyFormat" autocomplete="off" />
          </label>
          <button type="button">Add</button>
        </div>
        <label>
          Current Currencies:
          <select size="7"></select>
        </label>
        <button type="button" data-kind="danger">Remove</button>
      </div>
    `;

    this.cultureInput = this.querySelector('input[name="currencyCulture"]') ?? undefined;
    this.formatInput = this.querySelector('input[name="currencyFormat"]') ?? undefined;
    this.addButton = this.querySelector('button:not([data-kind])') ?? undefined;
    this.removeButton = this.querySelector('button[data-kind="danger"]') ?? undefined;
    this.select = this.querySelector('select') ?? undefined;

    this.addButton?.addEventListener('click', event => this.addCurrency(event));
    this.removeButton?.addEventListener('click', event => this.removeCurrency(event));

    this.renderOptions();
    this.syncDisabledState();
  }

  private renderOptions(): void {
    if (this.select == null) {
      return;
    }

    const fragment = document.createDocumentFragment();
    const sortedCurrencies = [...this.currencies].sort((left, right) => left.sort - right.sort);

    for (const currency of sortedCurrencies) {
      const option = document.createElement('option');
      option.value = String(this.currencies.indexOf(currency));
      option.textContent = this.combine(currency);
      fragment.append(option);
    }

    this.select.replaceChildren(fragment);
  }

  private combine(item: CurrencyItem): string {
    return `Culture: ${item.currencyValue} Format: ${item.currencyFormat}`;
  }

  private normalizeValue(value: unknown): CurrencyValue {
    if (!Array.isArray(value)) {
      return [];
    }

    return value
      .filter(item => item != null && typeof item === 'object')
      .map((item, index) => {
        const currency = item as Partial<CurrencyItem> & { Sort?: number };

        return {
          currencyFormat: currency.currencyFormat ?? '',
          currencyValue: currency.currencyValue ?? '',
          sort: currency.sort ?? currency.Sort ?? index,
        };
      });
  }

  private syncDisabledState(): void {
    const disabled = this.readonly;
    this.cultureInput?.toggleAttribute('disabled', disabled);
    this.formatInput?.toggleAttribute('disabled', disabled);
    this.select?.toggleAttribute('disabled', disabled);
    this.addButton?.toggleAttribute('disabled', disabled);
    this.removeButton?.toggleAttribute('disabled', disabled);
  }

  private emitChange(): void {
    this.dispatchEvent(new UmbChangeEvent());
  }
}

customElements.define('ekom-currency-picker', EkomCurrencyPickerElement);

export default EkomCurrencyPickerElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-currency-picker': EkomCurrencyPickerElement;
  }
}
