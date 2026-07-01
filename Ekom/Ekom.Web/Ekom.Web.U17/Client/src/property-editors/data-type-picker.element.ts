import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import type {
  ManifestPropertyEditorUi,
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type EkomDataTypeOption = {
  guid: string;
  name: string;
  editorAlias: string;
};

export class EkomDataTypePickerElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private select?: HTMLSelectElement;
  private status?: HTMLParagraphElement;
  private options: EkomDataTypeOption[] = [];
  private internalValue?: EkomDataTypeOption;
  private loaded = false;

  get value(): EkomDataTypeOption | undefined {
    return this.internalValue;
  }

  set value(value: unknown) {
    this.internalValue = this.normalizeValue(value);
    this.syncSelection();
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
    void this.loadDataTypes();
  }

  private async loadDataTypes(): Promise<void> {
    if (this.loaded) {
      return;
    }

    this.setStatus('Loading data types...');

    try {
      const response = await fetch('/ekom/backoffice/GetNonEkomDataTypes', {
        credentials: 'same-origin',
        headers: {
          Accept: 'application/json',
        },
      });

      if (!response.ok) {
        throw new Error(`Data type request failed with status ${response.status}.`);
      }

      this.options = await response.json() as EkomDataTypeOption[];
      this.options.sort((left, right) => left.name.localeCompare(right.name));
      this.loaded = true;
      this.renderOptions();
      this.syncSelection();
      this.setStatus('');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Could not load data types.';
      this.setStatus(message, true);
    }
  }

  private onChange(): void {
    const guid = this.select?.value;
    this.internalValue = this.options.find(option => option.guid === guid);
    this.dispatchEvent(new UmbChangeEvent());
  }

  private render(): void {
    this.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-data-type-picker {
          display: grid;
          gap: var(--uui-size-space-2, 8px);
        }

        select {
          box-sizing: border-box;
          width: 100%;
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
      <div class="ekom-data-type-picker">
        <select></select>
        <p aria-live="polite"></p>
      </div>
    `;

    this.select = this.querySelector('select') ?? undefined;
    this.status = this.querySelector('p') ?? undefined;
    this.select?.addEventListener('change', () => this.onChange());
    this.renderOptions();
    this.syncSelection();
    this.syncDisabledState();
  }

  private renderOptions(): void {
    if (this.select == null) {
      return;
    }

    const fragment = document.createDocumentFragment();
    const emptyOption = document.createElement('option');
    emptyOption.value = '';
    emptyOption.textContent = 'Select a data type';
    fragment.append(emptyOption);

    for (const option of this.options) {
      const element = document.createElement('option');
      element.value = option.guid;
      element.textContent = `${option.name} (${option.editorAlias})`;
      fragment.append(element);
    }

    this.select.replaceChildren(fragment);
  }

  private syncSelection(): void {
    if (this.select == null) {
      return;
    }

    this.select.value = this.internalValue?.guid ?? '';
  }

  private syncDisabledState(): void {
    if (this.select != null) {
      this.select.disabled = this.readonly;
    }
  }

  private setStatus(message: string, isError = false): void {
    if (this.status == null) {
      return;
    }

    this.status.textContent = message;
    this.status.dataset.error = String(isError);
  }

  private normalizeValue(value: unknown): EkomDataTypeOption | undefined {
    if (value != null && typeof value === 'object' && 'guid' in value) {
      const typedValue = value as Partial<EkomDataTypeOption>;

      if (typeof typedValue.guid === 'string') {
        return {
          guid: typedValue.guid,
          name: typedValue.name ?? typedValue.guid,
          editorAlias: typedValue.editorAlias ?? '',
        };
      }
    }

    return undefined;
  }
}

customElements.define('ekom-data-type-picker', EkomDataTypePickerElement);

export default EkomDataTypePickerElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-data-type-picker': EkomDataTypePickerElement;
  }
}
