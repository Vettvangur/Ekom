import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import { createExtensionElement } from '@umbraco-cms/backoffice/extension-api';
import { umbExtensionsRegistry } from '@umbraco-cms/backoffice/extension-registry';
import { UmbLitElement } from '@umbraco-cms/backoffice/lit-element';
import {
  UMB_PROPERTY_CONTEXT,
  UMB_PROPERTY_DATASET_CONTEXT,
  type UmbPropertyContext,
  type UmbPropertyDatasetContext,
} from '@umbraco-cms/backoffice/property';
import {
  UmbPropertyEditorConfigCollection,
  type ManifestPropertyEditorUi,
  type UmbPropertyEditorConfig,
  type UmbPropertyEditorConfigProperty,
  type UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type EkomPropertyType = 'Language' | 'Store';

type EkomPropertyValue = {
  values?: Record<string, unknown>;
  dtdGuid: string;
  type: EkomPropertyType;
};

type EkomTab = {
  value: string;
  text: string;
};

type EkomDataType = {
  guid?: string;
  propertyEditorAlias?: string;
  preValues?: Record<string, unknown> | UmbPropertyEditorConfig;
  view?: string;
};

type EkomLanguage = {
  isoCode?: string;
  cultureName?: string;
};

type EkomStore = {
  alias?: string;
  title?: string;
};

type EkomPropertyConfig = Record<string, unknown>;

type EkomCharacterReplacement = {
  Char?: string;
  Replacement?: string;
};

type EkomTitleChangedEventDetail = {
  tab: string;
  title: string;
  slug: string;
};

const emptyGuid = '00000000-0000-0000-0000-000000000000';
const currentTabStorageKey = 'ekomCurrentTab';
const titleChangedEventName = 'ekom-property-title-changed';
const tabChangedEventName = 'ekom-property-tab-changed';

export class EkomPropertyEditorElement extends UmbLitElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  mandatory?: boolean;
  mandatoryMessage?: string;

  private editor?: UmbPropertyEditorUiElement;
  private editorContainer?: HTMLDivElement;
  private status?: HTMLDivElement;
  private tabsContainer?: HTMLDivElement;
  private propertyAlias = '';
  private propertyContext?: UmbPropertyContext<EkomPropertyValue>;
  private propertyDatasetContext?: UmbPropertyDatasetContext;
  private rawConfig?: UmbPropertyEditorConfigCollection;
  private wrappedDataType?: EkomDataType;
  private tabs: EkomTab[] = [];
  private currentTab?: EkomTab;
  private loading = true;
  private failed = false;
  private errorMessage = '';
  private lastAutofilledNodeName?: string;
  private readonly manuallyEditedSlugTabs = new Set<string>();
  private readonly onTitleChanged = (event: Event): void => this.handleTitleChanged(event);
  private readonly onTabChanged = (event: Event): void => this.handleTabChanged(event);
  private internalValue: EkomPropertyValue = {
    values: {},
    dtdGuid: emptyGuid,
    type: 'Language',
  };

  get value(): EkomPropertyValue {
    return this.internalValue;
  }

  set value(value: unknown) {
    this.internalValue = this.normalizeValue(value);
    this.syncCurrentEditorValue();
  }

  get config(): UmbPropertyEditorConfigCollection | undefined {
    return this.rawConfig;
  }

  set config(config: UmbPropertyEditorConfigCollection | undefined) {
    this.rawConfig = config;
    void this.load();
  }

  get readonly(): boolean {
    return this.hasAttribute('readonly');
  }

  set readonly(value: boolean) {
    this.toggleAttribute('readonly', value);
    if (this.editor != null) {
      this.editor.readonly = value;
      this.editor.toggleAttribute('readonly', value);
    }
  }

  override connectedCallback(): void {
    super.connectedCallback();

    this.consumeContext(UMB_PROPERTY_CONTEXT, context => {
      if (context == null) {
        return;
      }

      this.propertyContext = context as UmbPropertyContext<EkomPropertyValue>;
      this.observe(context.alias, alias => {
        this.propertyAlias = alias ?? '';
      }, 'ekomPropertyAlias');
    });

    this.consumeContext(UMB_PROPERTY_DATASET_CONTEXT, context => {
      if (context == null) {
        return;
      }

      this.propertyDatasetContext = context;
      this.observe(context.name, name => this.tryAutofillFromNodeName(name), 'ekomPropertyNodeName');
    });

    this.renderShell();
    window.addEventListener(titleChangedEventName, this.onTitleChanged);
    window.addEventListener(tabChangedEventName, this.onTabChanged);
    void this.load();
  }

  override destroy(): void {
    window.removeEventListener(titleChangedEventName, this.onTitleChanged);
    window.removeEventListener(tabChangedEventName, this.onTabChanged);
    this.editor?.destroy?.();
  }

  private async load(): Promise<void> {
    if (!this.isConnected || this.rawConfig == null) {
      return;
    }

    this.setLoading();

    try {
      const config = this.getConfigObject();
      const wrappedGuid = this.extractGuid(config.dataType);

      if (wrappedGuid == null) {
        throw new Error('No wrapped data type has been configured for this Ekom property.');
      }

      this.wrappedDataType = await this.fetchJson<EkomDataType>(`/ekom/backoffice/DataType/${wrappedGuid}`);
      const useLanguages = Boolean(config.useLanguages);
      this.internalValue.type = useLanguages ? 'Language' : 'Store';

      const contentKey = this.getContentKey();
      this.tabs = useLanguages
        ? await this.loadLanguageTabs(contentKey)
        : await this.loadStoreTabs(this.getNodeId());

      this.currentTab = this.getStoredTab() ?? this.tabs[0];
      this.loading = false;
      this.failed = false;
      this.syncStatus();
      this.renderTabs();
      this.tryAutofillFromNodeName(this.propertyDatasetContext?.getName());
      await this.renderCurrentEditor();
    } catch (error) {
      this.loading = false;
      this.failed = true;
      this.errorMessage = error instanceof Error ? error.message : 'Could not render the property.';
      this.syncStatus();
    }
  }

  private async loadLanguageTabs(contentKey: string | undefined): Promise<EkomTab[]> {
    const url = contentKey != null
      ? `/ekom/backoffice/Languages/${encodeURIComponent(contentKey)}`
      : '/ekom/backoffice/Languages';
    const languages = await this.fetchJson<EkomLanguage[]>(url);

    return languages
      .filter(language => language.isoCode != null)
      .map(language => ({
        value: language.isoCode ?? '',
        text: language.cultureName ?? language.isoCode ?? '',
      }));
  }

  private async loadStoreTabs(nodeId: number): Promise<EkomTab[]> {
    const id = this.propertyAlias === 'disable' ? 1 : nodeId;
    const stores = await this.fetchJson<EkomStore[]>(`/ekom/backoffice/Stores/${id}`);

    return stores
      .filter(store => store.alias != null)
      .map(store => ({
        value: store.alias ?? '',
        text: store.title ?? store.alias ?? '',
      }));
  }

  private async renderCurrentEditor(): Promise<void> {
    if (this.editorContainer == null || this.loading || this.failed || this.currentTab == null || this.wrappedDataType == null) {
      return;
    }

    const propertyEditorUiAlias = this.wrappedDataType.view;

    if (propertyEditorUiAlias == null || propertyEditorUiAlias.length === 0) {
      throw new Error('The wrapped data type does not expose a property editor UI alias.');
    }

    const manifest = umbExtensionsRegistry.getByAlias<ManifestPropertyEditorUi>(propertyEditorUiAlias);

    if (manifest == null) {
      throw new Error(`Could not find property editor UI "${propertyEditorUiAlias}".`);
    }

    this.editor?.destroy?.();
    this.editorContainer.replaceChildren();

    const editor = await createExtensionElement<UmbPropertyEditorUiElement>(manifest);

    if (editor == null) {
      throw new Error(`Could not create property editor UI "${propertyEditorUiAlias}".`);
    }

    editor.manifest = manifest;
    editor.name = `${this.name ?? this.propertyAlias}.${this.currentTab.value}`;
    editor.value = this.internalValue.values?.[this.currentTab.value];
    editor.config = new UmbPropertyEditorConfigCollection(this.getWrappedConfig());
    editor.readonly = this.readonly;
    editor.mandatory = false;

    if (!this.stringIsNullOrWhiteSpace(this.mandatoryMessage)) {
      editor.mandatoryMessage = this.mandatoryMessage;
    }
    editor.toggleAttribute('readonly', this.readonly);
    editor.addEventListener('change', event => this.onWrappedEditorChange(event));
    editor.addEventListener('property-value-change', event => this.onWrappedEditorChange(event));

    this.editor = editor;
    this.editorContainer.append(editor);
    this.tryAutofillFromNodeName(this.propertyDatasetContext?.getName());
  }

  private onWrappedEditorChange(event: Event): void {
    event.stopPropagation();

    if (this.currentTab == null || this.editor == null) {
      return;
    }

    if (this.propertyAlias === 'slug') {
      this.manuallyEditedSlugTabs.add(this.currentTab.value);
    }

    this.internalValue = {
      ...this.internalValue,
      values: {
        ...this.internalValue.values,
        [this.currentTab.value]: this.editor.value,
      },
    };

    this.emitChange();
    this.emitTitleChanged();
  }

  private handleTitleChanged(event: Event): void {
    if (!this.isCreateMode() || this.propertyAlias !== 'slug' || this.tabs.length === 0) {
      return;
    }

    const detail = (event as CustomEvent<EkomTitleChangedEventDetail>).detail;

    if (detail == null || this.stringIsNullOrWhiteSpace(detail.tab)) {
      return;
    }

    if (this.manuallyEditedSlugTabs.has(detail.tab)) {
      return;
    }

    this.setTabValue(detail.tab, detail.slug);
  }

  private emitTitleChanged(): void {
    if (!this.isCreateMode() || this.propertyAlias !== 'title' || this.currentTab == null) {
      return;
    }

    const title = this.editor?.value;

    if (typeof title !== 'string') {
      return;
    }

    window.dispatchEvent(new CustomEvent<EkomTitleChangedEventDetail>(titleChangedEventName, {
      detail: {
        tab: this.currentTab.value,
        title,
        slug: this.slugify(title),
      },
    }));
  }

  private setCurrentTab(tab: EkomTab): void {
    localStorage.setItem(currentTabStorageKey, JSON.stringify(tab.value));
    this.selectTab(tab.value);
    window.dispatchEvent(new CustomEvent<string>(tabChangedEventName, {
      detail: tab.value,
    }));
  }

  private handleTabChanged(event: Event): void {
    const tabValue = (event as CustomEvent<string>).detail;

    if (this.stringIsNullOrWhiteSpace(tabValue) || tabValue === this.currentTab?.value) {
      return;
    }

    this.selectTab(tabValue);
  }

  private selectTab(tabValue: string): void {
    const tab = this.tabs.find(item => item.value === tabValue);

    if (tab == null) {
      return;
    }

    this.currentTab = tab;
    this.renderTabs();
    void this.renderCurrentEditor();
  }

  private emitChange(): void {
    this.propertyContext?.setValue(this.internalValue);
    this.dispatchEvent(new UmbChangeEvent());
  }

  private tryAutofillFromNodeName(nodeName: string | undefined): void {
    if (!this.isCreateMode() || this.tabs.length === 0) {
      return;
    }

    if (this.stringIsNullOrWhiteSpace(nodeName)) {
      this.lastAutofilledNodeName = undefined;
      return;
    }

    const shouldFillTitle = this.propertyAlias === 'title';
    const shouldFillSlug = this.propertyAlias === 'slug';

    if (!shouldFillTitle && !shouldFillSlug) {
      return;
    }

    if (nodeName === this.lastAutofilledNodeName) {
      return;
    }

    const value = shouldFillTitle ? nodeName : this.slugify(nodeName);
    let changedCurrentTab = false;
    let changed = false;

    const values = { ...this.internalValue.values };

    for (const tab of this.tabs) {
      if (shouldFillSlug && this.manuallyEditedSlugTabs.has(tab.value)) {
        continue;
      }

      values[tab.value] = value;
      changed = true;

      if (tab.value === this.currentTab?.value) {
        changedCurrentTab = true;
      }
    }

    if (!changed) {
      this.lastAutofilledNodeName = nodeName;
      return;
    }

    this.internalValue = {
      ...this.internalValue,
      values,
    };

    if (changedCurrentTab && this.editor != null) {
      this.editor.value = value;
    }

    this.lastAutofilledNodeName = nodeName;
    this.emitChange();
  }

  private syncCurrentEditorValue(): void {
    if (this.editor == null || this.currentTab == null) {
      return;
    }

    this.editor.value = this.internalValue.values?.[this.currentTab.value];
  }

  private setTabValue(tab: string, value: string): void {
    this.internalValue = {
      ...this.internalValue,
      values: {
        ...this.internalValue.values,
        [tab]: value,
      },
    };

    if (tab === this.currentTab?.value && this.editor != null) {
      this.editor.value = value;
    }

    this.emitChange();
  }

  private isCreateMode(): boolean {
    return window.location.pathname.includes('/workspace/document/create/');
  }

  private slugify(value: string): string {
    let inputValue = value;

    for (const replacement of this.getCharReplacements()) {
      if (this.stringIsNullOrWhiteSpace(replacement.Char)) {
        continue;
      }

      inputValue = inputValue.replaceAll(replacement.Char, replacement.Replacement ?? '');
    }

    return inputValue
      .normalize('NFKD')
      .toLowerCase()
      .trim()
      .replace(/\s+/g, '-')
      .replace(/[^\w-]+/g, '')
      .replace(/--+/g, '-');
  }

  private getCharReplacements(): EkomCharacterReplacement[] {
    const serverVariables = (window as Window & {
      Umbraco?: {
        Sys?: {
          ServerVariables?: {
            ekom?: {
              charCollections?: EkomCharacterReplacement[];
            };
          };
        };
      };
    }).Umbraco?.Sys?.ServerVariables;

    return serverVariables?.ekom?.charCollections ?? [];
  }

  private setLoading(): void {
    this.loading = true;
    this.failed = false;
    this.errorMessage = '';
    this.syncStatus();
  }

  private renderShell(): void {
    const template = document.createElement('template');
    template.innerHTML = `
      <style>
        :host {
          display: block;
        }

        .ekom-property-editor {
          display: grid;
          gap: var(--uui-size-space-4, 16px);
        }

        .ekom-tabs {
          display: flex;
          flex-wrap: wrap;
          gap: var(--uui-size-space-1, 4px);
          margin: 0;
          padding: 0;
          border-bottom: 1px solid var(--uui-color-border, #d8d7d9);
          list-style: none;
        }

        .ekom-tab {
          border: 0;
          border-bottom: 3px solid transparent;
          padding: var(--uui-size-space-3, 12px) var(--uui-size-space-4, 16px);
          background: transparent;
          color: var(--uui-color-text, #1b264f);
          cursor: pointer;
          font: inherit;
        }

        .ekom-tab[aria-selected='true'] {
          border-bottom-color: var(--uui-color-interactive, #3544b1);
          font-weight: 700;
        }

        .ekom-status {
          color: var(--uui-color-text-alt, #515054);
        }

        .ekom-status[data-state='error'] {
          color: var(--uui-color-danger, #d42054);
        }
      </style>
      <div class="ekom-property-editor">
        <div class="ekom-tabs" role="tablist"></div>
        <div class="ekom-status" aria-live="polite"></div>
        <div class="ekom-editor"></div>
      </div>
    `;

    this.renderRoot.replaceChildren(template.content.cloneNode(true));

    this.tabsContainer = this.renderRoot.querySelector('.ekom-tabs') ?? undefined;
    this.status = this.renderRoot.querySelector('.ekom-status') ?? undefined;
    this.editorContainer = this.renderRoot.querySelector('.ekom-editor') ?? undefined;
    this.syncStatus();
  }

  private renderTabs(): void {
    if (this.tabsContainer == null) {
      return;
    }

    const fragment = document.createDocumentFragment();

    for (const tab of this.tabs) {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'ekom-tab';
      button.textContent = tab.text;
      button.setAttribute('role', 'tab');
      button.setAttribute('aria-selected', String(tab.value === this.currentTab?.value));
      button.addEventListener('click', () => this.setCurrentTab(tab));
      fragment.append(button);
    }

    this.tabsContainer.replaceChildren(fragment);
  }

  private syncStatus(): void {
    if (this.status == null) {
      return;
    }

    this.status.dataset.state = this.failed ? 'error' : this.loading ? 'loading' : 'idle';
    this.status.textContent = this.failed
      ? this.errorMessage
      : this.loading
        ? 'Loading...'
        : this.tabs.length === 0
          ? 'No tabs are available for this property.'
          : '';
  }

  private getConfigObject(): EkomPropertyConfig {
    return this.rawConfig?.toObject() ?? {};
  }

  private getWrappedConfig(): UmbPropertyEditorConfig {
    const preValues = this.wrappedDataType?.preValues;

    if (Array.isArray(preValues)) {
      return preValues;
    }

    if (preValues == null) {
      return [];
    }

    return Object.entries(preValues).map(([alias, value]) => ({
      alias,
      value,
    }) as UmbPropertyEditorConfigProperty);
  }

  private getStoredTab(): EkomTab | undefined {
    const raw = localStorage.getItem(currentTabStorageKey);

    if (raw == null) {
      return undefined;
    }

    try {
      const value = JSON.parse(raw) as string;
      return this.tabs.find(tab => tab.value === value);
    } catch {
      return undefined;
    }
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

  private getContentKey(): string | undefined {
    return window.location.pathname
      .split('/')
      .find(part => /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(part));
  }

  private extractGuid(value: unknown): string | undefined {
    if (typeof value === 'string' && value.length > 0) {
      return value;
    }

    if (value != null && typeof value === 'object' && 'guid' in value) {
      const guid = (value as { guid?: unknown }).guid;
      return typeof guid === 'string' ? guid : undefined;
    }

    return undefined;
  }

  private stringIsNullOrWhiteSpace(value: string | undefined): value is undefined {
    return value == null || value.trim().length === 0;
  }

  private normalizeValue(value: unknown): EkomPropertyValue {
    if (value != null && typeof value === 'object' && 'values' in value) {
      const typedValue = value as Partial<EkomPropertyValue>;

      return {
        values: {
          ...typedValue.values,
        },
        dtdGuid: typedValue.dtdGuid ?? emptyGuid,
        type: typedValue.type ?? 'Language',
      };
    }

    return {
      values: {},
      dtdGuid: emptyGuid,
      type: 'Language',
    };
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

customElements.define('ekom-property-editor', EkomPropertyEditorElement);

export default EkomPropertyEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-property-editor': EkomPropertyEditorElement;
  }
}
