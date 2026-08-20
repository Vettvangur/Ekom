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
  private readonly handleDocumentClick = (event: MouseEvent): void => {
    const picker = event.composedPath()
      .find(target => target instanceof Element && target.classList.contains('combobox'));

    if (!(picker instanceof Element) || !this.contains(picker)) {
      this.closeDropdowns();
    }
  };

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
    document.addEventListener('click', this.handleDocumentClick);
    void this.loadData();
  }

  override disconnectedCallback(): void {
    document.removeEventListener('click', this.handleDocumentClick);
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
        input { box-sizing: border-box; width: 100%; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        .combobox { position: relative; }
        .combobox-control { display: flex; align-items: center; gap: var(--uui-size-space-2, 8px); box-sizing: border-box; width: 100%; min-height: 40px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-1, 4px) var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); cursor: pointer; }
        .combobox-control:focus { outline: 2px solid var(--uui-color-focus, #3544b1); outline-offset: 1px; }
        .combobox-control[aria-disabled='true'] { cursor: not-allowed; opacity: 0.55; }
        .combobox-value { display: flex; flex: 1; flex-wrap: wrap; gap: var(--uui-size-space-1, 4px); min-width: 0; }
        .placeholder { color: var(--uui-color-text-alt, #515054); }
        .combobox-arrow { flex: 0 0 auto; }
        .combobox-dropdown { position: absolute; z-index: 20; top: calc(100% + 4px); left: 0; right: 0; display: grid; gap: var(--uui-size-space-2, 8px); border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); box-shadow: 0 4px 12px rgb(0 0 0 / 18%); }
        .combobox-dropdown[hidden] { display: none; }
        .chip { display: inline-flex; align-items: center; gap: var(--uui-size-space-1, 4px); border-radius: 999px; padding: 3px 6px 3px 10px; background: var(--uui-color-surface-alt, #f3f3f5); color: var(--uui-color-text, #1b264f); }
        .chip button { display: grid; place-items: center; width: 20px; height: 20px; border: 0; border-radius: 50%; padding: 0; background: transparent; color: inherit; cursor: pointer; font: inherit; font-size: 18px; line-height: 1; }
        .chip button:hover { background: var(--uui-color-border, #d8d7d9); }
        .option-list { display: grid; max-height: 220px; overflow-y: auto; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); background: var(--uui-color-surface, #fff); }
        .option { display: flex; align-items: center; gap: var(--uui-size-space-2, 8px); border: 0; padding: var(--uui-size-space-2, 8px); background: transparent; color: inherit; font: inherit; font-weight: 400; text-align: left; cursor: pointer; }
        .option + .option { border-top: 1px solid var(--uui-color-border, #d8d7d9); }
        .option:hover, .option:focus { background: var(--uui-color-surface-alt, #f3f3f5); }
        .option-mark { width: 16px; text-align: center; }
        .empty-options { padding: var(--uui-size-space-3, 12px); color: var(--uui-color-text-alt, #515054); }
        .clear-button { border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-1, 4px) var(--uui-size-space-2, 8px); background: var(--uui-color-surface-alt, #f3f3f5); color: var(--uui-color-text, #1b264f); cursor: pointer; font: inherit; white-space: nowrap; }
        button:disabled, input:disabled { cursor: not-allowed; opacity: 0.55; }
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
    const descriptionId = `metafield_${index}_description`;

    if (!this.isEmpty(field.description)) {
      const description = document.createElement('small');
      description.id = descriptionId;
      description.textContent = field.description ?? '';
      label.append(description);
    }

    const clearButton = document.createElement('button');
    clearButton.type = 'button';
    clearButton.className = 'clear-button';
    clearButton.dataset.key = field.key ?? '';
    clearButton.dataset.action = 'clear';
    clearButton.textContent = 'Clear';
    clearButton.addEventListener('click', event => this.clearField(event, field));

    labelRow.append(label, clearButton);
    wrapper.append(labelRow);

    const predefinedValues = field.values ?? [];

    if (predefinedValues.length > 0) {
      wrapper.append(field.enableMultipleChoice === true
        ? this.createMultiSelect(field, index, descriptionId)
        : this.createSelect(field, index, predefinedValues, descriptionId));
    } else {
      wrapper.append(this.createTextInput(field, index, descriptionId));
    }

    return wrapper;
  }

  private createSelect(field: Metafield, index: number, _predefinedValues: Metavalue[], descriptionId: string): HTMLDivElement {
    return this.createChoicePicker(field, index, descriptionId, false);
  }

  private createMultiSelect(field: Metafield, index: number, descriptionId: string): HTMLDivElement {
    return this.createChoicePicker(field, index, descriptionId, true);
  }

  private createChoicePicker(field: Metafield, index: number, descriptionId: string, multiple: boolean): HTMLDivElement {
    const picker = document.createElement('div');
    picker.className = `combobox ${multiple ? 'multi-picker' : 'single-picker'}`;
    picker.dataset.key = field.key ?? '';

    const control = document.createElement('div');
    control.id = `metafield_${index}`;
    control.className = 'combobox-control';
    control.dataset.key = field.key ?? '';
    control.dataset.control = 'choice';
    control.tabIndex = 0;
    control.setAttribute('role', 'combobox');
    control.setAttribute('aria-haspopup', 'listbox');
    control.setAttribute('aria-expanded', 'false');
    control.setAttribute('aria-label', field.name ?? field.key ?? 'Metafield');
    this.setDescription(control, field, descriptionId);

    const selectedValues = document.createElement('div');
    selectedValues.className = 'combobox-value';
    selectedValues.setAttribute('aria-label', multiple ? 'Selected values' : 'Selected value');

    const arrow = document.createElement('span');
    arrow.className = 'combobox-arrow';
    arrow.setAttribute('aria-hidden', 'true');
    arrow.textContent = '▾';
    control.append(selectedValues, arrow);

    const dropdown = document.createElement('div');
    dropdown.className = 'combobox-dropdown';
    dropdown.hidden = true;

    const search = document.createElement('input');
    search.type = 'search';
    search.placeholder = 'Search values';
    search.dataset.key = field.key ?? '';
    search.dataset.control = 'search';
    search.setAttribute('aria-label', `Search ${field.name ?? field.key ?? 'metafield'} values`);
    search.addEventListener('input', () => this.renderChoiceOptions(field, picker, search.value, multiple));
    search.addEventListener('keydown', event => {
      if (event.key === 'Escape') {
        event.preventDefault();
        this.closeDropdown(picker, true);
      } else if (event.key === 'ArrowDown') {
        event.preventDefault();
        picker.querySelector<HTMLButtonElement>('.option')?.focus();
      }
    });

    const optionList = document.createElement('div');
    optionList.className = 'option-list';
    optionList.setAttribute('role', 'listbox');
    optionList.setAttribute('aria-label', 'Available values');
    optionList.setAttribute('aria-multiselectable', String(multiple));

    control.addEventListener('click', () => this.toggleDropdown(field, picker, multiple));
    control.addEventListener('keydown', event => {
      if (event.key === 'Enter' || event.key === ' ' || event.key === 'ArrowDown') {
        event.preventDefault();
        this.openDropdown(field, picker, multiple);
      } else if (event.key === 'Escape') {
        event.preventDefault();
        this.closeDropdown(picker);
      }
    });

    dropdown.append(search, optionList);
    picker.append(control, dropdown);
    this.syncChoicePicker(field, picker, multiple);

    return picker;
  }

  private createTextInput(field: Metafield, index: number, descriptionId: string): HTMLInputElement {
    const input = document.createElement('input');
    input.id = `metafield_${index}`;
    input.type = 'text';
    input.dataset.key = field.key ?? '';
    input.dataset.control = 'text';
    input.value = String(this.getFieldValue(field) ?? '');
    input.readOnly = field.readOnly === true;
    this.setDescription(input, field, descriptionId);
    input.addEventListener('input', () => this.setMetafieldValue(field, input.value));

    return input;
  }

  private clearField(event: Event, field: Metafield): void {
    event.preventDefault();

    if (this.isFieldReadonly(field)) {
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

    this.syncDisabledState();
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

      const input = this.editor.querySelector<HTMLInputElement>(`input[data-control="text"][data-key="${CSS.escape(key)}"]`);
      const multiPicker = this.editor.querySelector<HTMLDivElement>(`.multi-picker[data-key="${CSS.escape(key)}"]`);
      const singlePicker = this.editor.querySelector<HTMLDivElement>(`.single-picker[data-key="${CSS.escape(key)}"]`);

      if (input != null) {
        input.value = String(this.getFieldValue(field) ?? '');
      }

      if (multiPicker != null) {
        this.syncChoicePicker(field, multiPicker, true);
      }

      if (singlePicker != null) {
        this.syncChoicePicker(field, singlePicker, false);
      }
    }

    this.syncDisabledState();
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

  private syncChoicePicker(field: Metafield, picker: HTMLDivElement, multiple: boolean): void {
    const selectedIds = new Set(this.getSelectedIds(field));
    const selectedValues = (field.values ?? [])
      .filter(value => selectedIds.has(value.id ?? ''));
    const selectedContainer = picker.querySelector<HTMLDivElement>('.combobox-value');
    const search = picker.querySelector<HTMLInputElement>('input[type="search"]');

    if (selectedContainer != null) {
      if (multiple) {
        const chips = selectedValues.map(value => {
          const label = this.getMetavalueLabel(value);
          const chip = document.createElement('span');
          chip.className = 'chip';
          chip.append(document.createTextNode(label));

          const removeButton = document.createElement('button');
          removeButton.type = 'button';
          removeButton.dataset.key = field.key ?? '';
          removeButton.setAttribute('aria-label', `Remove ${label}`);
          removeButton.textContent = '×';
          removeButton.addEventListener('click', event => {
            event.preventDefault();
            event.stopPropagation();
            this.toggleMetavalue(field, value, false, picker);
          });
          chip.append(removeButton);

          return chip;
        });

        if (chips.length === 0) {
          selectedContainer.replaceChildren(this.createPlaceholder('Select values'));
        } else {
          selectedContainer.replaceChildren(...chips);
        }
      } else {
        const selectedValue = selectedValues[0];
        selectedContainer.replaceChildren(selectedValue == null
          ? this.createPlaceholder('Select value')
          : document.createTextNode(this.getMetavalueLabel(selectedValue)));
      }
    }

    if (!this.isDropdownClosed(picker)) {
      this.renderChoiceOptions(field, picker, search?.value ?? '', multiple);
    }

    this.syncDisabledState();
  }

  private createPlaceholder(text: string): HTMLSpanElement {
    const placeholder = document.createElement('span');
    placeholder.className = 'placeholder';
    placeholder.textContent = text;
    return placeholder;
  }

  private toggleDropdown(field: Metafield, picker: HTMLDivElement, multiple: boolean): void {
    if (this.isDropdownClosed(picker)) {
      this.openDropdown(field, picker, multiple);
    } else {
      this.closeDropdown(picker);
    }
  }

  private openDropdown(field: Metafield, picker: HTMLDivElement, multiple: boolean): void {
    if (this.isFieldReadonly(field)) {
      return;
    }

    this.closeDropdowns(picker);
    const control = picker.querySelector<HTMLDivElement>('.combobox-control');
    const dropdown = picker.querySelector<HTMLDivElement>('.combobox-dropdown');
    const search = picker.querySelector<HTMLInputElement>('input[type="search"]');

    if (control == null || dropdown == null || search == null) {
      return;
    }

    dropdown.hidden = false;
    control.setAttribute('aria-expanded', 'true');
    this.renderChoiceOptions(field, picker, search.value, multiple);
    search.focus();
  }

  private closeDropdown(picker: HTMLDivElement, restoreFocus = false): void {
    const control = picker.querySelector<HTMLDivElement>('.combobox-control');
    const dropdown = picker.querySelector<HTMLDivElement>('.combobox-dropdown');
    const search = picker.querySelector<HTMLInputElement>('input[type="search"]');
    const optionList = picker.querySelector<HTMLDivElement>('.option-list');

    if (dropdown == null) {
      return;
    }

    dropdown.hidden = true;
    control?.setAttribute('aria-expanded', 'false');
    optionList?.replaceChildren();

    if (search != null) {
      search.value = '';
    }

    if (restoreFocus) {
      control?.focus();
    }
  }

  private closeDropdowns(except?: HTMLDivElement): void {
    for (const picker of this.querySelectorAll<HTMLDivElement>('.combobox')) {
      if (picker !== except) {
        this.closeDropdown(picker);
      }
    }
  }

  private isDropdownClosed(picker: HTMLDivElement): boolean {
    return picker.querySelector<HTMLDivElement>('.combobox-dropdown')?.hidden !== false;
  }

  private renderChoiceOptions(field: Metafield, picker: HTMLDivElement, query: string, multiple: boolean): void {
    const optionList = picker.querySelector<HTMLDivElement>('.option-list');

    if (optionList == null || this.isDropdownClosed(picker)) {
      return;
    }

    const normalizedQuery = query.trim().toLocaleLowerCase();
    const selectedIds = new Set(this.getSelectedIds(field));
    const values = (field.values ?? [])
      .filter(value => this.getMetavalueLabel(value).toLocaleLowerCase().includes(normalizedQuery));

    if (values.length === 0) {
      const emptyMessage = document.createElement('span');
      emptyMessage.className = 'empty-options';
      emptyMessage.textContent = 'No matching values';
      optionList.replaceChildren(emptyMessage);
      return;
    }

    const options = values.map(value => {
      const valueId = value.id ?? '';
      const option = document.createElement('button');
      const selected = selectedIds.has(valueId);
      option.type = 'button';
      option.className = 'option';
      option.dataset.key = field.key ?? '';
      option.dataset.valueId = valueId;
      option.setAttribute('role', 'option');
      option.setAttribute('aria-selected', String(selected));

      const mark = document.createElement('span');
      mark.className = 'option-mark';
      mark.setAttribute('aria-hidden', 'true');
      mark.textContent = selected ? '✓' : '';

      const text = document.createElement('span');
      text.textContent = this.getMetavalueLabel(value);
      option.append(mark, text);
      option.addEventListener('click', event => {
        event.preventDefault();
        event.stopPropagation();

        if (multiple) {
          this.toggleMetavalue(field, value, !selected, picker);
          picker.querySelector<HTMLButtonElement>(`button[data-value-id="${CSS.escape(valueId)}"]`)?.focus();
        } else {
          this.selectMetavalue(field, value, picker);
        }
      });
      option.addEventListener('keydown', event => this.handleOptionKeydown(event, picker));

      return option;
    });
    optionList.replaceChildren(...options);
  }

  private toggleMetavalue(field: Metafield, value: Metavalue, selected: boolean, picker: HTMLDivElement): void {
    if (this.isFieldReadonly(field)) {
      return;
    }

    const valueId = value.id ?? '';
    const selectedIds = new Set(this.getSelectedIds(field));

    if (selected) {
      selectedIds.add(valueId);
    } else {
      selectedIds.delete(valueId);
    }

    const selectedValues = (field.values ?? []).filter(item => selectedIds.has(item.id ?? ''));
    this.setMetafieldValue(field, selectedValues);
    this.syncChoicePicker(field, picker, true);
  }

  private selectMetavalue(field: Metafield, value: Metavalue, picker: HTMLDivElement): void {
    if (this.isFieldReadonly(field)) {
      return;
    }

    this.setMetafieldValue(field, value);
    this.syncChoicePicker(field, picker, false);
    this.closeDropdown(picker, true);
  }

  private handleOptionKeydown(event: KeyboardEvent, picker: HTMLDivElement): void {
    if (event.key === 'Escape') {
      event.preventDefault();
      this.closeDropdown(picker, true);
      return;
    }

    if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') {
      return;
    }

    event.preventDefault();
    const options = Array.from(picker.querySelectorAll<HTMLButtonElement>('.option'));
    const currentIndex = options.indexOf(event.currentTarget as HTMLButtonElement);
    const nextIndex = event.key === 'ArrowDown'
      ? Math.min(currentIndex + 1, options.length - 1)
      : Math.max(currentIndex - 1, 0);
    options[nextIndex]?.focus();
  }

  private setDescription(element: HTMLElement, field: Metafield, descriptionId: string): void {
    if (!this.isEmpty(field.description)) {
      element.setAttribute('aria-describedby', descriptionId);
    }
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
    for (const element of this.querySelectorAll<HTMLElement>('input, button')) {
      const key = element.dataset.key;
      const field = this.fields.find(item => item.key === key);
      const isTextInput = element instanceof HTMLInputElement && element.dataset.control === 'text';
      const isEmptyClearButton = element.dataset.action === 'clear' && (field == null || this.isFieldValueEmpty(field));
      const disabled = this.readonly || (!isTextInput && field?.readOnly === true) || isEmptyClearButton;
      element.toggleAttribute('disabled', disabled);
    }

    for (const input of this.querySelectorAll<HTMLInputElement>('input[data-control="text"]')) {
      const key = input.dataset.key;
      const field = this.fields.find(item => item.key === key);
      input.readOnly = field?.readOnly === true;
    }

    for (const control of this.querySelectorAll<HTMLDivElement>('.combobox-control')) {
      const field = this.fields.find(item => item.key === control.dataset.key);
      const disabled = field == null || this.isFieldReadonly(field);
      control.setAttribute('aria-disabled', String(disabled));
      control.tabIndex = disabled ? -1 : 0;

      if (disabled) {
        const picker = control.closest<HTMLDivElement>('.combobox');

        if (picker != null) {
          this.closeDropdown(picker);
        }
      }
    }
  }

  private isFieldReadonly(field: Metafield): boolean {
    return this.readonly || field.readOnly === true;
  }

  private isFieldValueEmpty(field: Metafield): boolean {
    const value = this.getFieldValue(field);
    return Array.isArray(value) ? value.length === 0 : value == null || value === '';
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
