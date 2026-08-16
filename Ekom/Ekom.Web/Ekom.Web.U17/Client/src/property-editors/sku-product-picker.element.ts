import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import { createExtensionElement } from '@umbraco-cms/backoffice/extension-api';
import { umbExtensionsRegistry } from '@umbraco-cms/backoffice/extension-registry';
import {
  UmbPropertyEditorConfigCollection,
  type ManifestPropertyEditorUi,
  type UmbPropertyEditorConfigCollection as UmbPropertyEditorConfigCollectionType,
  type UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type DocumentReference = {
  type: 'document';
  unique: string;
};

type SkuProductPickerItem = {
  key: string;
  sku: string;
};

export class EkomSkuProductPickerElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollectionType;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private nativeEditor?: UmbPropertyEditorUiElement;
  private skus: string[] = [];

  get value(): string[] {
    return this.skus;
  }

  set value(value: unknown) {
    this.skus = this.normalizeSkus(value);
    void this.syncNativeValue();
  }

  get readonly(): boolean {
    return this.hasAttribute('readonly');
  }

  set readonly(value: boolean) {
    this.toggleAttribute('readonly', value);
    this.syncReadOnlyState();
  }

  override connectedCallback(): void {
    void this.createNativeEditor();
  }

  override disconnectedCallback(): void {
    this.nativeEditor?.destroy?.();
  }

  private async createNativeEditor(): Promise<void> {
    const manifest = umbExtensionsRegistry.getByAlias<ManifestPropertyEditorUi>('Umb.PropertyEditorUi.ContentPicker');
    if (manifest == null) {
      throw new Error('Could not find the Umbraco Content Picker property editor UI.');
    }

    const editor = await createExtensionElement<UmbPropertyEditorUiElement>(manifest);
    if (editor == null) {
      throw new Error('Could not create the Umbraco Content Picker property editor UI.');
    }

    editor.manifest = manifest;
    editor.name = this.name;
    editor.config = this.config ?? new UmbPropertyEditorConfigCollection([]);
    editor.readonly = this.readonly;
    editor.mandatory = this.mandatory;
    editor.mandatoryMessage = this.mandatoryMessage;
    editor.toggleAttribute('readonly', this.readonly);
    editor.addEventListener('change', event => this.onNativeEditorChange(event));
    editor.addEventListener('property-value-change', event => this.onNativeEditorChange(event));

    this.nativeEditor = editor;
    this.replaceChildren(editor);
    await this.syncNativeValue();
  }

  private async syncNativeValue(): Promise<void> {
    if (this.nativeEditor == null) {
      return;
    }

    const items = await this.post<SkuProductPickerItem[]>('skus', { skus: this.skus });
    this.nativeEditor.value = items.map(item => ({
      type: 'document',
      unique: item.key,
    } satisfies DocumentReference));
  }

  private async onNativeEditorChange(event: Event): Promise<void> {
    event.stopPropagation();

    if (this.nativeEditor == null) {
      return;
    }

    const keys = this.getKeys(this.nativeEditor.value);
    const items = await this.post<SkuProductPickerItem[]>('keys', { keys });
    this.skus = items.map(item => item.sku);
    this.dispatchEvent(new UmbChangeEvent());
  }

  private getKeys(value: unknown): string[] {
    if (!Array.isArray(value)) {
      return [];
    }

    return value
      .map(item => typeof item === 'object' && item != null && 'unique' in item ? item.unique : undefined)
      .filter((key): key is string => typeof key === 'string');
  }

  private normalizeSkus(value: unknown): string[] {
    if (!Array.isArray(value)) {
      return [];
    }

    return value
      .filter((sku): sku is string => typeof sku === 'string')
      .map(sku => sku.trim())
      .filter(sku => sku.length > 0);
  }

  private syncReadOnlyState(): void {
    if (this.nativeEditor == null) {
      return;
    }

    this.nativeEditor.readonly = this.readonly;
    this.nativeEditor.toggleAttribute('readonly', this.readonly);
  }

  private async post<T>(route: string, body: unknown): Promise<T> {
    const response = await fetch(`/ekom/backoffice/SkuProductPicker/${route}`, {
      method: 'POST',
      headers: {
        'Content-Type': 'application/json',
      },
      body: JSON.stringify(body),
    });

    if (!response.ok) {
      throw new Error(`Could not resolve selected products (${response.status}).`);
    }

    return response.json() as Promise<T>;
  }
}

customElements.define('ekom-sku-product-picker', EkomSkuProductPickerElement);

declare global {
  interface HTMLElementTagNameMap {
    'ekom-sku-product-picker': EkomSkuProductPickerElement;
  }
}

export default EkomSkuProductPickerElement;
