import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import type {
  ManifestPropertyEditorUi,
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type Country = {
  name?: string;
  Name?: string;
  code?: string;
  Code?: string;
};

export class EkomCountryPickerElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private select?: HTMLSelectElement;
  private status?: HTMLParagraphElement;
  private countries: Country[] = [];
  private internalValue = '';

  get value(): string {
    return this.internalValue;
  }

  set value(value: unknown) {
    this.internalValue = typeof value === 'string' ? value : '';
    this.syncValue();
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
    void this.loadCountries();
  }

  private async loadCountries(): Promise<void> {
    this.setStatus('Loading countries...');

    try {
      this.countries = await this.fetchJson<Country[]>('/ekom/api/countries');
      this.renderOptions();
      this.setStatus('');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Could not load countries.';
      this.setStatus(message, true);
    }
  }

  private renderShell(): void {
    this.innerHTML = `
      <style>
        :host { display: block; }
        .ekom-country-picker { display: grid; gap: var(--uui-size-space-2, 8px); justify-items: start; }
        select { box-sizing: border-box; min-width: 260px; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        select:disabled { cursor: not-allowed; opacity: 0.55; }
        p { margin: 0; color: var(--uui-color-text-alt, #515054); }
        p[data-error='true'] { color: var(--uui-color-danger, #d42054); }
      </style>
      <div class="ekom-country-picker">
        <select></select>
        <p aria-live="polite"></p>
      </div>
    `;

    this.select = this.querySelector('select') ?? undefined;
    this.status = this.querySelector('p') ?? undefined;
    this.select?.addEventListener('change', () => this.setCountryValue());
    this.syncDisabledState();
  }

  private renderOptions(): void {
    if (this.select == null) {
      return;
    }

    const fragment = document.createDocumentFragment();

    for (const country of this.countries) {
      const code = this.getCountryCode(country);

      if (code.length === 0) {
        continue;
      }

      const option = document.createElement('option');
      option.value = code;
      option.textContent = this.getCountryLabel(country, code);
      fragment.append(option);
    }

    this.select.replaceChildren(fragment);

    if (this.internalValue.length === 0 && this.select.options.length > 0) {
      this.internalValue = this.select.options[0]?.value ?? '';
      this.emitChange();
    }

    this.syncValue();
  }

  private setCountryValue(): void {
    if (this.readonly || this.select == null) {
      return;
    }

    this.internalValue = this.select.value;
    this.emitChange();
  }

  private syncValue(): void {
    if (this.select == null) {
      return;
    }

    this.select.value = this.internalValue;
  }

  private syncDisabledState(): void {
    this.select?.toggleAttribute('disabled', this.readonly);
  }

  private getCountryCode(country: Country): string {
    return country.code ?? country.Code ?? '';
  }

  private getCountryLabel(country: Country, code: string): string {
    const name = country.name ?? country.Name ?? code;
    return `${name} (${code})`;
  }

  private setStatus(message: string, isError = false): void {
    if (this.status == null) {
      return;
    }

    this.status.textContent = message;
    this.status.dataset.error = String(isError);
  }

  private async fetchJson<T>(url: string): Promise<T> {
    const response = await fetch(url, {
      credentials: 'same-origin',
    });

    if (!response.ok) {
      throw new Error(`Request failed: ${response.status}`);
    }

    return await response.json() as T;
  }

  private emitChange(): void {
    this.dispatchEvent(new UmbChangeEvent());
  }
}

customElements.define('ekom-country-picker', EkomCountryPickerElement);

export default EkomCountryPickerElement;
