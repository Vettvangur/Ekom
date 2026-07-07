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

export class EkomZonePickerElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private availableSelect?: HTMLSelectElement;
  private selectedSelect?: HTMLSelectElement;
  private addButton?: HTMLButtonElement;
  private removeButton?: HTMLButtonElement;
  private status?: HTMLParagraphElement;
  private countries: Country[] = [];
  private selectedCodes: string[] = [];

  get value(): string {
    return this.selectedCodes.join(',');
  }

  set value(value: unknown) {
    this.selectedCodes = typeof value === 'string'
      ? value.split(',').map(item => item.trim()).filter(item => item.length > 0)
      : [];
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
        .ekom-zone-picker { display: flex; flex-wrap: wrap; align-items: center; gap: var(--uui-size-space-3, 12px); }
        .buttons { display: grid; gap: var(--uui-size-space-2, 8px); }
        label { display: grid; gap: var(--uui-size-space-1, 4px); font-weight: 600; }
        select { box-sizing: border-box; min-width: 240px; min-height: 180px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        button { border: 0; border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px) var(--uui-size-space-4, 16px); background: var(--uui-color-interactive, #3544b1); color: var(--uui-color-interactive-contrast, #fff); cursor: pointer; font: inherit; font-weight: 600; }
        button:disabled, select:disabled { cursor: not-allowed; opacity: 0.55; }
        p { flex-basis: 100%; margin: 0; color: var(--uui-color-text-alt, #515054); }
        p[data-error='true'] { color: var(--uui-color-danger, #d42054); }
      </style>
      <div class="ekom-zone-picker">
        <label>
          Available Countries
          <select data-list="available" size="10" multiple></select>
        </label>
        <div class="buttons">
          <button type="button" data-action="add">Add</button>
          <button type="button" data-action="remove">Remove</button>
        </div>
        <label>
          Selected Countries
          <select data-list="selected" size="10" multiple></select>
        </label>
        <p aria-live="polite"></p>
      </div>
    `;

    this.availableSelect = this.querySelector('select[data-list="available"]') ?? undefined;
    this.selectedSelect = this.querySelector('select[data-list="selected"]') ?? undefined;
    this.addButton = this.querySelector('button[data-action="add"]') ?? undefined;
    this.removeButton = this.querySelector('button[data-action="remove"]') ?? undefined;
    this.status = this.querySelector('p') ?? undefined;

    this.addButton?.addEventListener('click', event => this.moveSelected(event, this.availableSelect, true));
    this.removeButton?.addEventListener('click', event => this.moveSelected(event, this.selectedSelect, false));
    this.syncDisabledState();
  }

  private renderOptions(): void {
    if (this.availableSelect == null || this.selectedSelect == null) {
      return;
    }

    const selected = new Set(this.selectedCodes);
    this.availableSelect.replaceChildren(...this.createOptions(this.countries.filter(country => !selected.has(this.getCountryCode(country)))));
    this.selectedSelect.replaceChildren(...this.createOptions(this.countries.filter(country => selected.has(this.getCountryCode(country)))));
  }

  private createOptions(countries: Country[]): HTMLOptionElement[] {
    return countries
      .slice()
      .sort((left, right) => this.getCountryLabel(left).localeCompare(this.getCountryLabel(right)))
      .map(country => {
        const option = document.createElement('option');
        option.value = this.getCountryCode(country);
        option.textContent = this.getCountryLabel(country);
        return option;
      });
  }

  private moveSelected(event: Event, select: HTMLSelectElement | undefined, add: boolean): void {
    event.preventDefault();

    if (this.readonly || select == null) {
      return;
    }

    const movingCodes = Array.from(select.selectedOptions).map(option => option.value);

    if (movingCodes.length === 0) {
      return;
    }

    if (add) {
      this.selectedCodes = Array.from(new Set([...this.selectedCodes, ...movingCodes]));
    } else {
      const moving = new Set(movingCodes);
      this.selectedCodes = this.selectedCodes.filter(code => !moving.has(code));
    }

    this.renderOptions();
    this.emitChange();
  }

  private syncDisabledState(): void {
    const disabled = this.readonly;
    this.availableSelect?.toggleAttribute('disabled', disabled);
    this.selectedSelect?.toggleAttribute('disabled', disabled);
    this.addButton?.toggleAttribute('disabled', disabled);
    this.removeButton?.toggleAttribute('disabled', disabled);
  }

  private getCountryCode(country: Country): string {
    return country.code ?? country.Code ?? '';
  }

  private getCountryLabel(country: Country): string {
    return country.name ?? country.Name ?? this.getCountryCode(country);
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

customElements.define('ekom-zone-picker', EkomZonePickerElement);

export default EkomZonePickerElement;
