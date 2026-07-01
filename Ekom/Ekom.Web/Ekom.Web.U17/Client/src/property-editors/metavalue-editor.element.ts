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

type MetavalueItem = {
  id: string;
  values: Record<string, string>;
};

export class EkomMetavalueEditorElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private editor?: HTMLDivElement;
  private status?: HTMLParagraphElement;
  private languages: Language[] = [];
  private items: MetavalueItem[] = [];

  get value(): MetavalueItem[] {
    return this.items;
  }

  set value(value: unknown) {
    this.items = this.normalizeValue(value);
    this.renderRows();
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
    void this.loadLanguages();
  }

  private async loadLanguages(): Promise<void> {
    this.setStatus('Loading languages...');

    try {
      this.languages = await this.fetchJson<Language[]>('/ekom/backoffice/Languages');
      this.renderRows();
      this.setStatus('');
    } catch (error) {
      const message = error instanceof Error ? error.message : 'Could not load languages.';
      this.setStatus(message, true);
    }
  }

  private renderShell(): void {
    this.innerHTML = `
      <style>
        :host { display: block; }
        .ekom-metavalue-editor { display: grid; gap: var(--uui-size-space-4, 16px); max-width: 900px; }
        .header, .row { display: grid; grid-template-columns: repeat(var(--language-count, 1), minmax(160px, 1fr)) auto; gap: var(--uui-size-space-2, 8px); align-items: center; }
        .header { font-weight: 700; }
        input { box-sizing: border-box; width: 100%; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        .actions { display: flex; gap: var(--uui-size-space-1, 4px); }
        button { border: 0; border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px) var(--uui-size-space-3, 12px); background: var(--uui-color-interactive, #3544b1); color: var(--uui-color-interactive-contrast, #fff); cursor: pointer; font: inherit; font-weight: 600; }
        button[data-kind='danger'] { background: var(--uui-color-danger, #d42054); color: var(--uui-color-danger-contrast, #fff); }
        button[data-kind='secondary'] { background: var(--uui-color-surface-alt, #f3f3f5); color: var(--uui-color-text, #1b264f); border: 1px solid var(--uui-color-border, #d8d7d9); }
        button:disabled, input:disabled { cursor: not-allowed; opacity: 0.55; }
        p { margin: 0; color: var(--uui-color-text-alt, #515054); }
        p[data-error='true'] { color: var(--uui-color-danger, #d42054); }
      </style>
      <div class="ekom-metavalue-editor"></div>
      <p aria-live="polite"></p>
    `;

    this.editor = this.querySelector('.ekom-metavalue-editor') ?? undefined;
    this.status = this.querySelector('p') ?? undefined;
  }

  private renderRows(): void {
    if (this.editor == null) {
      return;
    }

    this.editor.style.setProperty('--language-count', String(Math.max(this.languages.length, 1)));
    const fragment = document.createDocumentFragment();

    if (this.languages.length > 0 && this.items.length > 0) {
      const header = document.createElement('div');
      header.className = 'header';

      for (const language of this.languages) {
        const column = document.createElement('div');
        column.textContent = language.cultureName ?? language.isoCode ?? '';
        header.append(column);
      }

      header.append(document.createElement('div'));
      fragment.append(header);
    }

    this.items.forEach((item, index) => fragment.append(this.createRow(item, index)));

    const addButton = document.createElement('button');
    addButton.type = 'button';
    addButton.textContent = 'Add';
    addButton.addEventListener('click', event => this.addItem(event));
    fragment.append(addButton);

    this.editor.replaceChildren(fragment);
    this.syncDisabledState();
  }

  private createRow(item: MetavalueItem, index: number): HTMLDivElement {
    const row = document.createElement('div');
    row.className = 'row';

    for (const language of this.languages) {
      const isoCode = language.isoCode ?? '';
      const input = document.createElement('input');
      input.type = 'text';
      input.value = item.values[isoCode] ?? '';
      input.addEventListener('input', () => this.setLanguageValue(index, isoCode, input.value));
      row.append(input);
    }

    const actions = document.createElement('div');
    actions.className = 'actions';

    actions.append(
      this.createActionButton('↑', () => this.moveItem(index, -1), 'secondary', index === 0),
      this.createActionButton('↓', () => this.moveItem(index, 1), 'secondary', index === this.items.length - 1),
      this.createActionButton('Remove', () => this.removeItem(index), 'danger'),
    );

    row.append(actions);
    return row;
  }

  private createActionButton(label: string, action: () => void, kind?: string, disabled = false): HTMLButtonElement {
    const button = document.createElement('button');
    button.type = 'button';
    button.textContent = label;

    if (kind != null) {
      button.dataset.kind = kind;
    }

    button.dataset.disabledWhenEnabled = String(disabled);
    button.disabled = disabled;
    button.addEventListener('click', event => {
      event.preventDefault();

      if (!this.readonly) {
        action();
      }
    });

    return button;
  }

  private addItem(event: Event): void {
    event.preventDefault();

    if (this.readonly) {
      return;
    }

    const values: Record<string, string> = {};

    for (const language of this.languages) {
      if (language.isoCode != null) {
        values[language.isoCode] = '';
      }
    }

    this.items = [
      ...this.items,
      {
        id: Math.random().toString(16).slice(2),
        values,
      },
    ];

    this.renderRows();
    this.emitChange();
  }

  private removeItem(index: number): void {
    this.items = this.items.filter((_, itemIndex) => itemIndex !== index);
    this.renderRows();
    this.emitChange();
  }

  private moveItem(index: number, direction: number): void {
    const nextIndex = index + direction;

    if (nextIndex < 0 || nextIndex >= this.items.length) {
      return;
    }

    const nextItems = [...this.items];
    const [item] = nextItems.splice(index, 1);
    nextItems.splice(nextIndex, 0, item);
    this.items = nextItems;
    this.renderRows();
    this.emitChange();
  }

  private setLanguageValue(index: number, isoCode: string, value: string): void {
    const item = this.items[index];

    if (item == null) {
      return;
    }

    this.items = this.items.map((currentItem, itemIndex) => itemIndex === index
      ? {
          ...currentItem,
          values: {
            ...currentItem.values,
            [isoCode]: value,
          },
        }
      : currentItem);

    this.emitChange();
  }

  private normalizeValue(value: unknown): MetavalueItem[] {
    if (!Array.isArray(value)) {
      return [];
    }

    return value.map(item => {
      if (!this.isRecord(item)) {
        return undefined;
      }

      return {
        id: String(item.id ?? Math.random().toString(16).slice(2)),
        values: this.isRecord(item.values) ? this.normalizeValues(item.values) : {},
      };
    }).filter(item => item != null);
  }

  private normalizeValues(values: Record<string, unknown>): Record<string, string> {
    const normalizedValues: Record<string, string> = {};

    for (const [key, value] of Object.entries(values)) {
      normalizedValues[key] = value == null ? '' : String(value);
    }

    return normalizedValues;
  }

  private syncDisabledState(): void {
    for (const input of this.querySelectorAll<HTMLInputElement>('input')) {
      input.disabled = this.readonly;
    }

    for (const button of this.querySelectorAll<HTMLButtonElement>('button')) {
      button.disabled = this.readonly || button.dataset.disabledWhenEnabled === 'true';
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

customElements.define('ekom-metavalue-editor', EkomMetavalueEditorElement);

export default EkomMetavalueEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-metavalue-editor': EkomMetavalueEditorElement;
  }
}
