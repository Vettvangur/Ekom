import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import type {
  ManifestPropertyEditorUi,
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type Language = {
  isoCode?: string;
  cultureName?: string;
};

type Metafield = {
  key?: string;
  name?: string;
  description?: string;
  values?: Metavalue[];
  enableMultipleChoice?: boolean;
  readOnly?: boolean;
};

type Metavalue = {
  id?: string;
  values?: Record<string, string>;
};

type MetafieldValue = {
  key: string;
  values: unknown;
};

export class EkomMetafieldPickerElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private editor?: HTMLDivElement;
  private status?: HTMLParagraphElement;
  private languages: Language[] = [];
  private fields: Metafield[] = [];
  private items: MetafieldValue[] = [];

  get value(): MetafieldValue[] {
    return this.items;
  }

  set value(value: unknown) {
    this.items = this.normalizeValue(value);
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
    void this.loadData();
  }

  private async loadData(): Promise<void> {
    this.setStatus('Loading metafields...');

    try {
      const [languages, fields] = await Promise.all([
        this.fetchJson<Language[]>('/ekom/backoffice/Languages'),
        this.fetchJson<Metafield[]>('/ekom/backoffice/Metafields'),
      ]);

      this.languages = languages;
      this.fields = fields;
      this.ensureFieldValues();
      this.renderFields();
      this.setStatus('');
      this.emitChange();
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Could not load metafields.';
      this.setStatus(message, true);
    }
  }

  private renderShell(): void {
    this.innerHTML = `
      <style>
        :host { display: block; }
        .ekom-metafield-picker { display: grid; gap: var(--uui-size-space-5, 20px); }
        .field { display: grid; gap: var(--uui-size-space-2, 8px); max-width: 680px; }
        .label-row { display: flex; align-items: start; justify-content: space-between; gap: var(--uui-size-space-4, 16px); }
        label { display: grid; gap: var(--uui-size-space-1, 4px); font-weight: 700; }
        small { display: block; color: var(--uui-color-text-alt, #515054); font-weight: 400; }
        input, select { box-sizing: border-box; width: 100%; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        select[multiple] { min-height: 130px; }
        button { border: 0; border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px) var(--uui-size-space-3, 12px); background: var(--uui-color-surface-alt, #f3f3f5); color: var(--uui-color-text, #1b264f); border: 1px solid var(--uui-color-border, #d8d7d9); cursor: pointer; font: inherit; font-weight: 600; white-space: nowrap; }
        button:disabled, input:disabled, select:disabled { cursor: not-allowed; opacity: 0.55; }
        p { margin: 0; color: var(--uui-color-text-alt, #515054); }
        p[data-error='true'] { color: var(--uui-color-danger, #d42054); }
      </style>
      <div class="ekom-metafield-picker"></div>
      <p aria-live="polite"></p>
    `;

    this.editor = this.querySelector('.ekom-metafield-picker') ?? undefined;
    this.status = this.querySelector('p') ?? undefined;
  }

  private renderFields(): void {
    if (this.editor == null) {
      return;
    }

    const fragment = document.createDocumentFragment();

    if (this.fields.length === 0) {
      const message = document.createElement('p');
      message.textContent = 'No metafields exist. You can create them under Metafields in Ekom';
      fragment.append(message);
    }

    this.fields.forEach((field, index) => fragment.append(this.createField(field, index)));

    this.editor.replaceChildren(fragment);
    this.syncDisabledState();
  }

  private createField(field: Metafield, index: number): HTMLDivElement {
    const wrapper = document.createElement('div');
    wrapper.className = 'field';

    const labelRow = document.createElement('div');
    labelRow.className = 'label-row';

    const label = document.createElement('label');
    label.htmlFor = `metafield_${index}`;
    label.textContent = field.name ?? field.key ?? '';

    if (!this.isEmpty(field.description)) {
      const description = document.createElement('small');
      description.textContent = field.description ?? '';
      label.append(description);
    }

    const clearButton = document.createElement('button');
    clearButton.type = 'button';
    clearButton.textContent = 'Clear';
    clearButton.addEventListener('click', event => this.clearField(event, field));

    labelRow.append(label, clearButton);
    wrapper.append(labelRow);

    const predefinedValues = field.values ?? [];

    if (predefinedValues.length > 0) {
      wrapper.append(this.createSelect(field, index, predefinedValues));
    } else {
      wrapper.append(this.createTextInput(field, index));
    }

    return wrapper;
  }

  private createSelect(field: Metafield, index: number, predefinedValues: Metavalue[]): HTMLSelectElement {
    const select = document.createElement('select');
    select.id = `metafield_${index}`;
    select.dataset.key = field.key ?? '';
    select.multiple = field.enableMultipleChoice === true;

    if (!select.multiple) {
      const emptyOption = document.createElement('option');
      emptyOption.value = '';
      emptyOption.textContent = 'Select value';
      select.append(emptyOption);
    }

    for (const value of predefinedValues) {
      const option = document.createElement('option');
      option.value = value.id ?? '';
      option.textContent = this.getMetavalueLabel(value);
      select.append(option);
    }

    this.setSelectValue(select, field);
    select.addEventListener('change', () => this.setMetafieldSelectValue(field, select));

    return select;
  }

  private createTextInput(field: Metafield, index: number): HTMLInputElement {
    const input = document.createElement('input');
    input.id = `metafield_${index}`;
    input.type = 'text';
    input.dataset.key = field.key ?? '';
    input.value = String(this.getFieldValue(field) ?? '');
    input.readOnly = field.readOnly === true;
    input.addEventListener('input', () => this.setMetafieldValue(field, input.value));

    return input;
  }

  private setSelectValue(select: HTMLSelectElement, field: Metafield): void {
    const selectedIds = new Set(this.getSelectedIds(field));

    for (const option of select.options) {
      option.selected = selectedIds.has(option.value);
    }
  }

  private setMetafieldSelectValue(field: Metafield, select: HTMLSelectElement): void {
    if (this.readonly) {
      return;
    }

    const selectedValues = Array.from(select.selectedOptions)
      .map(option => (field.values ?? []).find(value => value.id === option.value))
      .filter(value => value != null);

    this.setMetafieldValue(field, select.multiple ? selectedValues : selectedValues[0] ?? '');
  }

  private clearField(event: Event, field: Metafield): void {
    event.preventDefault();

    if (this.readonly) {
      return;
    }

    const clearValue = (field.values?.length ?? 0) > 0 && field.enableMultipleChoice === true ? [] : '';
    this.setMetafieldValue(field, clearValue);
    this.syncInputs();
  }

  private setMetafieldValue(field: Metafield, value: unknown): void {
    const key = field.key;

    if (key == null) {
      return;
    }

    this.items = this.items.map(item => item.key === key
      ? {
          key,
          values: value,
        }
      : item);

    if (!this.items.some(item => item.key === key)) {
      this.items = [
        ...this.items,
        {
          key,
          values: value,
        },
      ];
    }

    this.emitChange();
  }

  private ensureFieldValues(): void {
    const nextItems = [...this.items];

    for (const field of this.fields) {
      const key = field.key;

      if (key == null || nextItems.some(item => item.key === key)) {
        continue;
      }

      nextItems.push({
        key,
        values: (field.values?.length ?? 0) > 0 ? [] : '',
      });
    }

    this.items = nextItems;
  }

  private syncInputs(): void {
    if (this.editor == null || this.fields.length === 0) {
      return;
    }

    for (const field of this.fields) {
      const key = field.key;

      if (key == null) {
        continue;
      }

      const input = this.editor.querySelector<HTMLInputElement>(`input[data-key="${CSS.escape(key)}"]`);
      const select = this.editor.querySelector<HTMLSelectElement>(`select[data-key="${CSS.escape(key)}"]`);

      if (input != null) {
        input.value = String(this.getFieldValue(field) ?? '');
      }

      if (select != null) {
        this.setSelectValue(select, field);
      }
    }
  }

  private getFieldValue(field: Metafield): unknown {
    return this.items.find(item => item.key === field.key)?.values;
  }

  private getSelectedIds(field: Metafield): string[] {
    const value = this.getFieldValue(field);

    if (Array.isArray(value)) {
      return value.map(item => this.isRecord(item) ? String(item.id ?? '') : String(item));
    }

    if (this.isRecord(value)) {
      return [String(value.id ?? '')];
    }

    return value == null || value === '' ? [] : [String(value)];
  }

  private getMetavalueLabel(value: Metavalue): string {
    const defaultLanguage = this.languages[0]?.isoCode;

    if (defaultLanguage != null && !this.isEmpty(value.values?.[defaultLanguage])) {
      return value.values?.[defaultLanguage] ?? '';
    }

    return Object.values(value.values ?? {}).find(text => !this.isEmpty(text)) ?? value.id ?? '';
  }

  private normalizeValue(value: unknown): MetafieldValue[] {
    if (!Array.isArray(value)) {
      return [];
    }

    return value.map(item => {
      if (!this.isRecord(item) || typeof item.key !== 'string') {
        return undefined;
      }

      return {
        key: item.key,
        values: item.values ?? '',
      };
    }).filter(item => item != null);
  }

  private syncDisabledState(): void {
    for (const element of this.querySelectorAll<HTMLInputElement | HTMLSelectElement | HTMLButtonElement>('input, select, button')) {
      element.disabled = this.readonly;
    }

    for (const input of this.querySelectorAll<HTMLInputElement>('input')) {
      const key = input.dataset.key;
      const field = this.fields.find(item => item.key === key);
      input.readOnly = field?.readOnly === true;
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

  private isEmpty(value: unknown): boolean {
    return value == null || String(value).length === 0;
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

customElements.define('ekom-metafield-picker', EkomMetafieldPickerElement);

export default EkomMetafieldPickerElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-metafield-picker': EkomMetafieldPickerElement;
  }
}
