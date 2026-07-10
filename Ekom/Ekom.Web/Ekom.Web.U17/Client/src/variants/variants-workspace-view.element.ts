import { UmbElementMixin } from '@umbraco-cms/backoffice/element-api';
import { UMB_DOCUMENT_PUBLISHING_WORKSPACE_CONTEXT, UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';
import '@umbraco-cms/backoffice/imaging';
import '@umbraco-cms/backoffice/media';
import { UMB_NOTIFICATION_CONTEXT, type UmbNotificationColor } from '@umbraco-cms/backoffice/notification';
import { UMB_WORKSPACE_VIEW_CONTEXT } from '@umbraco-cms/backoffice/workspace';

type VariantProduct = {
  id: number;
  key: string;
  name: string;
  title: string;
  sku: string;
  variantCount: number;
  languages: VariantLanguage[];
  stores: VariantStore[];
  variantGroupFields: CustomFieldDefinition[];
  variantFields: CustomFieldDefinition[];
  groups: VariantGroup[];
};

type CustomFieldDefinition = {
  alias: string;
  label: string;
  required: boolean;
};

type CustomField = CustomFieldDefinition & {
  value: string;
};

type VariantLanguage = {
  isoCode?: string;
  cultureName?: string;
};

type VariantStore = {
  alias?: string;
  title?: string;
  currencies?: VariantCurrency[];
};

type VariantCurrency = {
  currencyValue?: string;
  currencySymbol?: string;
  isoCurrencySymbol?: string;
};

type CurrencyPrice = {
  Currency?: string;
  Price?: number;
  currency?: string;
  price?: number;
};

type StockItem = {
  storeAlias: string;
  value: number;
};

type VariantGroup = {
  id: number;
  key: string;
  name: string;
  title: string;
  titleValues: Record<string, string>;
  color: string;
  images: string;
  sortOrder: number;
  changed?: boolean;
  published: boolean;
  customFields: CustomField[];
  variants: VariantItem[];
};

type VariantItem = {
  id: number;
  key: string;
  name: string;
  title: string;
  titleValues: Record<string, string>;
  sku: string;
  images: string;
  priceValues: Record<string, CurrencyPrice[]>;
  stockValues: StockItem[];
  customFields: CustomField[];
  sortOrder: number;
  changed?: boolean;
  published: boolean;
};

type DrawerState =
  | { type: 'group'; groupId: number }
  | { type: 'variant'; groupId: number; variantId: number };

const EMPTY_GUID = '00000000-0000-0000-0000-000000000000';
const VARIANT_COUNT_HINT = 'ekom-variant-count';

type DocumentWorkspaceSaveContext = {
  requestSave?: () => Promise<void>;
  requestSubmit?: () => Promise<void>;
  saveAndPublish?: () => Promise<void>;
};

class EkomVariantsWorkspaceViewElement extends UmbElementMixin(HTMLElement) {
  private notificationContext?: typeof UMB_NOTIFICATION_CONTEXT.TYPE;
  private workspaceViewContext?: typeof UMB_WORKSPACE_VIEW_CONTEXT.TYPE;
  private product?: VariantProduct;
  private loading = true;
  private saving = false;
  private error = '';
  private productId = '';
  private activeLanguage = '';
  private nextDraftId = -1;
  private drawer?: DrawerState;
  private readonly expandedGroupIds = new Set<number>();
  private readonly selectedGroupIds = new Set<number>();
  private readonly selectedVariantIds = new Set<number>();
  private readonly deletedNodeIds = new Set<number>();
  private readonly groupSnapshots = new Map<number, string>();
  private readonly variantSnapshots = new Map<number, string>();
  private draggedGroupId?: number;
  private draggedVariant?: { groupId: number; variantId: number };
  private documentWorkspaceContext?: DocumentWorkspaceSaveContext;
  private originalRequestSave?: () => Promise<void>;
  private originalRequestSubmit?: () => Promise<void>;
  private originalSaveAndPublish?: () => Promise<void>;
  private publishingWorkspaceContext?: DocumentWorkspaceSaveContext;
  private originalPublishingSaveAndPublish?: () => Promise<void>;
  private readonly onKeydown = (event: KeyboardEvent): void => {
    if (event.key !== 'Escape' || this.drawer == null) {
      return;
    }

    event.stopPropagation();
    this.syncMediaPickers();
    this.drawer = undefined;
    this.render();
  };

  override connectedCallback(): void {
    super.connectedCallback();
    window.addEventListener('keydown', this.onKeydown);
    this.consumeContext(UMB_NOTIFICATION_CONTEXT, context => {
      this.notificationContext = context;
    });

    this.consumeContext(UMB_WORKSPACE_VIEW_CONTEXT, context => {
      this.workspaceViewContext = context;
      this.updateWorkspaceViewBadge();
    });

    this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, context => {
      if (context == null) {
        return;
      }

      const unique = context.getUnique();
      if (unique != null) {
        this.productId = unique;
      }

      this.patchDocumentSave(context as DocumentWorkspaceSaveContext);

      this.observe(context.unique, value => {
        if (value == null || value === this.productId) {
          return;
        }

        this.productId = value;
        void this.load();
      }, 'ekomVariantProductId');
    });

    this.consumeContext(UMB_DOCUMENT_PUBLISHING_WORKSPACE_CONTEXT, context => {
      if (context == null) {
        return;
      }

      this.patchPublishingSave(context as DocumentWorkspaceSaveContext);
    });

    this.productId = this.getProductIdFromUrl();
    this.render();
    if (this.productId) {
      void this.load();
    }
  }

  override disconnectedCallback(): void {
    window.removeEventListener('keydown', this.onKeydown);
    this.restoreDocumentSave();
    this.restorePublishingSave();
    super.disconnectedCallback();
  }

  private patchDocumentSave(context: DocumentWorkspaceSaveContext): void {
    if (this.documentWorkspaceContext === context) {
      return;
    }

    this.restoreDocumentSave();
    this.documentWorkspaceContext = context;

    if (context.requestSave != null) {
      this.originalRequestSave = context.requestSave.bind(context);
      context.requestSave = async () => {
        await this.originalRequestSave?.();
        await this.saveAfterDocumentSave();
      };
    }

    if (context.requestSubmit != null) {
      this.originalRequestSubmit = context.requestSubmit.bind(context);
      context.requestSubmit = async () => {
        await this.originalRequestSubmit?.();
        await this.saveAfterDocumentSave();
      };
    }

    if (context.saveAndPublish != null) {
      this.originalSaveAndPublish = context.saveAndPublish.bind(context);
      context.saveAndPublish = async () => {
        await this.originalSaveAndPublish?.();
        await this.saveAfterDocumentSave();
      };
    }
  }

  private restoreDocumentSave(): void {
    if (this.documentWorkspaceContext == null) {
      return;
    }

    if (this.originalRequestSave != null) {
      this.documentWorkspaceContext.requestSave = this.originalRequestSave;
    }

    if (this.originalRequestSubmit != null) {
      this.documentWorkspaceContext.requestSubmit = this.originalRequestSubmit;
    }

    if (this.originalSaveAndPublish != null) {
      this.documentWorkspaceContext.saveAndPublish = this.originalSaveAndPublish;
    }

    this.documentWorkspaceContext = undefined;
    this.originalRequestSave = undefined;
    this.originalRequestSubmit = undefined;
    this.originalSaveAndPublish = undefined;
  }

  private patchPublishingSave(context: DocumentWorkspaceSaveContext): void {
    if (this.publishingWorkspaceContext === context) {
      return;
    }

    this.restorePublishingSave();
    this.publishingWorkspaceContext = context;

    if (context.saveAndPublish != null) {
      this.originalPublishingSaveAndPublish = context.saveAndPublish.bind(context);
      context.saveAndPublish = async () => {
        await this.originalPublishingSaveAndPublish?.();
        await this.saveAfterDocumentSave();
      };
    }
  }

  private restorePublishingSave(): void {
    if (this.publishingWorkspaceContext == null) {
      return;
    }

    if (this.originalPublishingSaveAndPublish != null) {
      this.publishingWorkspaceContext.saveAndPublish = this.originalPublishingSaveAndPublish;
    }

    this.publishingWorkspaceContext = undefined;
    this.originalPublishingSaveAndPublish = undefined;
  }

  private async saveAfterDocumentSave(): Promise<void> {
    this.syncMediaPickers();

    if (this.product != null && this.hasChanges()) {
      await this.save();
    }
  }

  private async load(): Promise<void> {
    const productId = this.getProductId();

    if (!productId) {
      this.loading = false;
      this.error = 'Could not determine the current product id.';
      this.showError(this.error);
      this.render();
      return;
    }

    this.loading = true;
    this.error = '';
    this.render();

    try {
      this.product = await this.fetchJson<VariantProduct>(`/ekom/backoffice/Variants/${encodeURIComponent(productId)}`);
      this.activeLanguage = this.product.languages[0]?.isoCode ?? '';
      this.drawer = undefined;
      this.selectedGroupIds.clear();
      this.selectedVariantIds.clear();
      this.deletedNodeIds.clear();
      this.expandedGroupIds.clear();
      if (this.product.groups.length === 1) {
        this.expandedGroupIds.add(this.product.groups[0].id);
      }
      this.resetSnapshots();
      this.updateWorkspaceViewBadge();
    } catch (error) {
      this.product = undefined;
      this.updateWorkspaceViewBadge();
      this.error = getErrorMessage(error, 'Could not load variants.');
      this.showError(this.error);
    } finally {
      this.loading = false;
      this.render();
    }
  }

  private addGroup(): void {
    if (this.product == null) {
      return;
    }

    const id = this.nextDraftId--;
    const group: VariantGroup = {
      id,
      key: EMPTY_GUID,
      name: 'New group',
      title: '',
      titleValues: this.createEmptyTitleValues(),
      color: '',
      images: '',
      sortOrder: this.product.groups.length,
      published: false,
      customFields: createCustomFields(this.product.variantGroupFields),
      variants: [],
    };

    this.product.groups = [...this.product.groups, group];
    this.expandedGroupIds.add(id);
    this.drawer = { type: 'group', groupId: id };
    this.render();
  }

  private addVariant(groupId: number): void {
    const group = this.getGroup(groupId);

    if (group == null) {
      return;
    }

    const id = this.nextDraftId--;
    const variant: VariantItem = {
      id,
      key: EMPTY_GUID,
      name: 'New variant',
      title: '',
      titleValues: this.createEmptyTitleValues(),
      sku: '',
      images: '',
      priceValues: {},
      stockValues: this.createEmptyStockValues(),
      customFields: createCustomFields(this.product?.variantFields ?? []),
      sortOrder: group.variants.length,
      published: false,
    };

    group.variants = [...group.variants, variant];
    this.expandedGroupIds.add(groupId);
    this.drawer = { type: 'variant', groupId, variantId: id };
    this.render();
  }

  private async save(): Promise<void> {
    this.syncMediaPickers();

    if (!this.validateAllCustomFields()) {
      return;
    }

    if (this.product == null || !this.hasChanges()) {
      this.showNotification('default', 'Variants', 'No changes to save.');
      return;
    }

    const groups = this.product.groups
      .map(group => this.getChangedGroupForSave(group))
      .filter(group => group != null);

    this.saving = true;
    this.error = '';
    this.render();

    try {
      for (const id of this.deletedNodeIds) {
        await this.deleteJson(`/ekom/backoffice/Variants/${encodeURIComponent(String(id))}`);
      }

      if (groups.length > 0) {
        this.product = await this.postJson<VariantProduct>('/ekom/backoffice/Variants/Save', {
          productId: this.getProductId(),
          publish: true,
          groups,
        });
      } else {
        await this.load();
      }

      this.selectedGroupIds.clear();
      this.selectedVariantIds.clear();
      this.deletedNodeIds.clear();
      this.drawer = undefined;
      this.expandedGroupIds.clear();
      if (this.product?.groups.length === 1) {
        this.expandedGroupIds.add(this.product.groups[0].id);
      }
      this.resetSnapshots();
      this.updateWorkspaceViewBadge();
      this.showSuccess('Variant changes were saved.');
    } catch (error) {
      this.error = getErrorMessage(error, 'Action failed.');
      this.showError(this.error);
    } finally {
      this.saving = false;
      this.render();
    }
  }

  private deleteSelected(): void {
    if (this.product == null) {
      return;
    }

    const selectedCount = this.selectedGroupIds.size + this.selectedVariantIds.size;

    if (selectedCount === 0 || !window.confirm(`Delete ${selectedCount} selected item${selectedCount === 1 ? '' : 's'}?`)) {
      return;
    }

    for (const group of this.product.groups) {
      if (this.selectedGroupIds.has(group.id) && !isDraft(group.id)) {
        this.deletedNodeIds.add(group.id);
      }

      for (const variant of group.variants) {
        if (this.selectedVariantIds.has(variant.id) && !isDraft(variant.id)) {
          this.deletedNodeIds.add(variant.id);
        }
      }
    }

    this.product.groups = this.product.groups
      .filter(group => !this.selectedGroupIds.has(group.id))
      .map(group => ({
        ...group,
        variants: group.variants.filter(variant => !this.selectedVariantIds.has(variant.id)),
      }));

    this.selectedGroupIds.clear();
    this.selectedVariantIds.clear();
    this.drawer = undefined;
    this.render();
  }

  private deleteDrawerItem(): void {
    if (this.product == null || this.drawer == null) {
      return;
    }

    const drawer = this.drawer;
    const isGroup = drawer.type === 'group';
    const label = isGroup ? 'variant group' : 'variant';

    if (!window.confirm(`Delete this ${label}?`)) {
      return;
    }

    if (isGroup) {
      const group = this.getGroup(drawer.groupId);

      if (group != null && !isDraft(group.id)) {
        this.deletedNodeIds.add(group.id);
      }

      this.product.groups = this.product.groups.filter(item => item.id !== drawer.groupId);
    } else {
      const group = this.getGroup(drawer.groupId);
      const variant = this.getVariant(drawer.groupId, drawer.variantId);

      if (variant != null && !isDraft(variant.id)) {
        this.deletedNodeIds.add(variant.id);
      }

      if (group != null) {
        group.variants = group.variants.filter(item => item.id !== drawer.variantId);
      }
    }

    this.drawer = undefined;
    this.render();
  }

  private updateGroupTitle(groupId: number, value: string): void {
    const group = this.getGroup(groupId);

    if (group != null) {
      group.titleValues = { ...group.titleValues, [this.activeLanguage]: value };
      group.title = getFirstValue(group.titleValues) || group.name;
      this.updateSaveButtonState();
    }
  }

  private updateGroupImages(groupId: number, value: string): void {
    const group = this.getGroup(groupId);

    if (group != null) {
      group.images = value;
      this.updateSaveButtonState();
    }
  }

  private updateVariantTitle(groupId: number, variantId: number, value: string): void {
    const variant = this.getVariant(groupId, variantId);

    if (variant != null) {
      variant.titleValues = { ...variant.titleValues, [this.activeLanguage]: value };
      variant.title = getFirstValue(variant.titleValues) || variant.name;
      this.updateSaveButtonState();
    }
  }

  private updateVariant(groupId: number, variantId: number, field: 'sku' | 'images', value: string): void {
    const variant = this.getVariant(groupId, variantId);

    if (variant != null) {
      variant[field] = value;
      this.updateSaveButtonState();
    }
  }

  private updatePrice(groupId: number, variantId: number, storeAlias: string, currency: string, value: string): void {
    const variant = this.getVariant(groupId, variantId);

    if (variant == null) {
      return;
    }

    const prices = [...(variant.priceValues[storeAlias] ?? [])];
    const price = Number(value) || 0;
    const existing = prices.find(item => getCurrency(item) === currency);

    if (existing != null) {
      existing.Price = price;
      existing.price = price;
    } else {
      prices.push({ Currency: currency, Price: price });
    }

    variant.priceValues = { ...variant.priceValues, [storeAlias]: prices };
    this.updateSaveButtonState();
  }

  private updateStock(groupId: number, variantId: number, storeAlias: string, value: string): void {
    const variant = this.getVariant(groupId, variantId);

    if (variant == null) {
      return;
    }

    const stock = Number(value) || 0;
    let updated = false;
    const stocks = variant.stockValues.map(item => {
      if (item.storeAlias !== storeAlias) {
        return item;
      }

      updated = true;
      return { ...item, value: stock };
    });

    if (!updated) {
      stocks.push({ storeAlias, value: stock });
    }

    variant.stockValues = stocks;
    this.updateSaveButtonState();
  }

  private hasChanges(): boolean {
    return this.deletedNodeIds.size > 0 || (this.product?.groups ?? []).some(group => this.isGroupChanged(group) || group.variants.some(variant => this.isVariantChanged(variant)));
  }

  private updateWorkspaceViewBadge(): void {
    if (this.workspaceViewContext == null) {
      return;
    }

    if (this.workspaceViewContext.hints.has(VARIANT_COUNT_HINT)) {
      this.workspaceViewContext.hints.removeOne(VARIANT_COUNT_HINT);
    }

    const count = this.product?.variantCount ?? 0;
    if (count > 0) {
      this.workspaceViewContext.hints.addOne({
        unique: VARIANT_COUNT_HINT,
        text: String(count),
        color: 'default',
      });
    }
  }

  private getChangedGroupForSave(group: VariantGroup): VariantGroup | null {
    const variants = group.variants
      .filter(variant => this.isVariantChanged(variant))
      .map(variant => ({
        ...variant,
        priceValues: normalizePriceValues(variant.priceValues, this.product?.stores ?? []),
        changed: true,
      }));

    if (!this.isGroupChanged(group) && variants.length === 0) {
      return null;
    }

    return {
      ...group,
      changed: this.isGroupChanged(group),
      variants,
    };
  }

  private isGroupChanged(group: VariantGroup): boolean {
    return isDraft(group.id) || this.groupSnapshots.get(group.id) !== snapshotGroup(group);
  }

  private isVariantChanged(variant: VariantItem): boolean {
    return isDraft(variant.id) || this.variantSnapshots.get(variant.id) !== snapshotVariant(variant);
  }

  private getGroup(groupId: number): VariantGroup | undefined {
    return this.product?.groups.find(group => group.id === groupId);
  }

  private getVariant(groupId: number, variantId: number): VariantItem | undefined {
    return this.getGroup(groupId)?.variants.find(item => item.id === variantId);
  }

  private resetSnapshots(): void {
    this.groupSnapshots.clear();
    this.variantSnapshots.clear();

    for (const group of this.product?.groups ?? []) {
      this.groupSnapshots.set(group.id, snapshotGroup(group));

      for (const variant of group.variants) {
        this.variantSnapshots.set(variant.id, snapshotVariant(variant));
      }
    }
  }

  private createEmptyTitleValues(): Record<string, string> {
    const values: Record<string, string> = {};

    for (const language of this.product?.languages ?? []) {
      values[language.isoCode ?? ''] = '';
    }

    return values;
  }

  private createEmptyStockValues(): StockItem[] {
    return (this.product?.stores ?? []).map(store => ({ storeAlias: store.alias ?? '', value: 0 }));
  }

  private showSuccess(message: string): void {
    this.showNotification('positive', 'Success', message);
  }

  private showError(message: string): void {
    this.showNotification('danger', 'Error', message);
  }

  private showNotification(color: UmbNotificationColor, headline: string, message: string): void {
    if (this.notificationContext) {
      this.notificationContext.peek(color, {
        data: {
          headline,
          message,
        },
      });
      return;
    }

    if (color === 'danger') {
      console.error(`${headline}: ${message}`);
    }
  }

  private render(): void {
    this.innerHTML = `
      <style>${styles}</style>
      <section class="ekm-variant-editor">
        ${this.renderTopBar()}
        ${this.error ? `<p class="status status--error">${escapeHtml(this.error)}</p>` : ''}
        ${this.loading ? '<uui-loader></uui-loader><p>Loading variants...</p>' : this.renderBody()}
        ${this.renderDrawer()}
      </section>
    `;

    this.bindEvents();
  }

  private renderTopBar(): string {
    const selectedCount = this.selectedGroupIds.size + this.selectedVariantIds.size;
    const dirty = this.hasChanges();

    return `
      <div class="top-bar">
        <div class="selection-actions">
          ${selectedCount > 0 ? `
            <span>${selectedCount} selected</span>
            <uui-button look="secondary" color="danger" data-action="delete-selected" ${this.saving ? 'disabled' : ''}>Delete</uui-button>
          ` : ''}
        </div>
        <div class="main-actions">
          <uui-button look="primary" data-action="create-group" ${this.saving ? 'disabled' : ''}>Add group</uui-button>
          <uui-button look="primary" color="positive" data-action="save" ${this.saving || !dirty ? 'disabled' : ''}>Save variant changes</uui-button>
        </div>
      </div>
    `;
  }

  private renderBody(): string {
    if (this.product == null) {
      return '';
    }

    if (this.product.groups.length === 0) {
      return `
        <uui-box headline="No variants yet">
          <p>Create a variant group to start adding product variants.</p>
          <uui-button look="primary" data-action="create-group">Create variant group</uui-button>
        </uui-box>
      `;
    }

    return `
      <div class="group-list">
        ${this.product.groups.map(group => this.renderGroup(group)).join('')}
      </div>
    `;
  }

  private renderGroup(group: VariantGroup): string {
    const expanded = this.expandedGroupIds.has(group.id);
    const title = this.getTitle(group, 'New group');
    const thumbnailImage = getGroupThumbnailImage(group);

    return `
      <article class="group-card ${isDraft(group.id) ? 'is-draft' : ''}" draggable="true" data-drag-group-id="${group.id}">
        <div class="group-header">
          <span class="drag-handle" title="Drag to reorder" aria-hidden="true">⋮⋮</span>
          <input type="checkbox" data-select-group data-group-id="${group.id}" ${this.selectedGroupIds.has(group.id) ? 'checked' : ''} aria-label="Select group">
          <button type="button" class="group-toggle" data-action="toggle-group" data-group-id="${group.id}" aria-label="Toggle group">${expanded ? '▼' : '►'}</button>
          <button type="button" class="thumb thumb--button" data-action="toggle-group" data-group-id="${group.id}">${renderThumbnail(thumbnailImage, title)}</button>
          <button type="button" class="group-title" data-action="toggle-group" data-group-id="${group.id}">${escapeHtml(title)}</button>
          <span class="count">${group.variants.length} variants</span>
          ${isDraft(group.id) ? '<span class="badge">draft</span>' : ''}
          <div class="group-header-actions">
            <uui-button compact look="secondary" data-action="edit-group" data-group-id="${group.id}">Edit group</uui-button>
            <uui-button compact look="secondary" data-action="create-variant" data-group-id="${group.id}">Add variant</uui-button>
          </div>
        </div>
        ${expanded ? this.renderVariants(group) : ''}
      </article>
    `;
  }

  private renderVariants(group: VariantGroup): string {
    return `
      <div class="variant-table">
        <div class="variant-head">
          <span></span><span></span><span></span><span>Title</span><span>SKU</span><span class="variant-price-cell">Price</span><span class="variant-stock-cell">Stock</span><span></span>
        </div>
        ${group.variants.map(variant => this.renderVariant(group, variant)).join('')}
      </div>
    `;
  }

  private renderVariant(group: VariantGroup, variant: VariantItem): string {
    return `
      <div class="variant-row ${isDraft(variant.id) ? 'is-draft' : ''}" draggable="true" data-drag-group-id="${group.id}" data-drag-variant-id="${variant.id}">
        <span class="drag-handle" title="Drag to reorder" aria-hidden="true">⋮⋮</span>
        <input type="checkbox" data-select-variant data-variant-id="${variant.id}" ${this.selectedVariantIds.has(variant.id) ? 'checked' : ''} aria-label="Select variant">
        <span class="thumb">${renderThumbnail(getFirstImage(variant.images), this.getTitle(variant, 'New variant'))}</span>
        <strong>${escapeHtml(this.getTitle(variant, 'New variant'))}${isDraft(variant.id) ? ' <span class="badge">draft</span>' : ''}</strong>
        <span>${escapeHtml(variant.sku)}</span>
        <span class="variant-price-cell">${escapeHtml(this.getDefaultPrice(variant))}</span>
        <span class="variant-stock-cell">${escapeHtml(this.getTotalStock(variant))}</span>
        <uui-button compact look="secondary" data-action="edit-variant" data-group-id="${group.id}" data-variant-id="${variant.id}">Edit</uui-button>
      </div>
    `;
  }

  private renderDrawer(): string {
    if (this.drawer == null || this.product == null) {
      return '';
    }

    const group = this.getGroup(this.drawer.groupId);

    if (group == null) {
      return '';
    }

    if (this.drawer.type === 'group') {
      return this.renderGroupDrawer(group);
    }

    const variant = this.getVariant(this.drawer.groupId, this.drawer.variantId);
    return variant == null ? '' : this.renderVariantDrawer(group, variant);
  }

  private renderGroupDrawer(group: VariantGroup): string {
    return `
      <div class="drawer-backdrop" data-action="close-drawer"></div>
      <aside class="drawer">
        ${this.renderDrawerHeader(this.getTitle(group, 'New group'), 'variant group', group)}
        <div class="drawer-body">
          ${this.renderTitleField('Group title', group.titleValues?.[this.activeLanguage] ?? '', 'data-group-title', group.id)}
          ${this.renderCustomFields(group.customFields, group.id)}
          ${this.renderMediaPicker('Images', group.images, 'data-group-images', group.id)}
          <p class="hint">Group images apply to all variants unless a variant has its own.</p>
        </div>
        ${this.renderDrawerFooter()}
      </aside>
    `;
  }

  private renderVariantDrawer(group: VariantGroup, variant: VariantItem): string {
    return `
      <div class="drawer-backdrop" data-action="close-drawer"></div>
      <aside class="drawer">
        ${this.renderDrawerHeader(this.getTitle(variant, 'New variant'), `${this.getTitle(group, 'Group')} / variant`, variant)}
        <div class="drawer-body">
          ${this.renderTitleField('Title', variant.titleValues?.[this.activeLanguage] ?? '', 'data-variant-title', group.id, variant.id)}
          <label>SKU<input data-variant-field="sku" data-group-id="${group.id}" data-variant-id="${variant.id}" value="${escapeHtml(variant.sku)}"></label>
          ${this.renderCustomFields(variant.customFields, group.id, variant.id)}
          ${this.renderPriceTable(group.id, variant)}
          ${this.renderStockTable(group.id, variant)}
          ${this.renderMediaPicker('Images', variant.images, 'data-variant-images', group.id, variant.id)}
        </div>
        ${this.renderDrawerFooter()}
      </aside>
    `;
  }

  private renderDrawerHeader(title: string, subtitle: string, item: VariantGroup | VariantItem): string {
    const href = !isDraft(item.id) && item.key ? `/umbraco/section/content/workspace/document/edit/${item.key}` : '';

    return `
      <header class="drawer-header">
        <div class="drawer-title">
          <h2>${escapeHtml(title)}</h2>
          <p>${escapeHtml(subtitle)}</p>
        </div>
        <div class="drawer-header-actions">
          ${href ? `<uui-button compact look="secondary" href="${href}" target="_blank" rel="noopener" title="Open node in new tab" aria-label="Open node in new tab"><span class="edit-icon" aria-hidden="true">✎</span></uui-button>` : ''}
          <button type="button" class="close-button" data-action="close-drawer" aria-label="Close">×</button>
        </div>
      </header>
    `;
  }

  private renderDrawerFooter(): string {
    return `
      <footer class="drawer-footer">
        <div class="drawer-footer-left">
          <button type="button" class="danger-button" data-action="delete-drawer-item">Delete</button>
        </div>
        <div class="drawer-footer-right">
          <uui-button look="secondary" data-action="close-drawer">Close</uui-button>
          <uui-button look="primary" color="positive" data-action="save-drawer">Save</uui-button>
        </div>
      </footer>
    `;
  }

  private renderTitleField(label: string, value: string, attribute: string, groupId: number, variantId = 0): string {
    return `
      <label>${escapeHtml(label)}
        ${this.renderLanguageMiniTabs()}
        <input ${attribute} data-group-id="${groupId}" data-variant-id="${variantId}" value="${escapeHtml(value)}">
      </label>
    `;
  }

  private renderLanguageMiniTabs(): string {
    const languages = this.product?.languages ?? [];

    if (languages.length <= 1) {
      return '';
    }

    return `
      <div class="mini-tabs">
        ${languages.map(language => {
          const value = language.isoCode ?? '';
          return `<button type="button" data-action="set-language" data-tab-value="${escapeHtml(value)}" class="mini-tab ${value === this.activeLanguage ? 'active' : ''}">${escapeHtml(getLanguageLabel(language))}</button>`;
        }).join('')}
      </div>
    `;
  }

  private renderCustomFields(fields: CustomField[] | undefined, groupId: number, variantId = 0): string {
    if (!fields?.length) {
      return '';
    }

    return fields.map(field => `
      <label>${escapeHtml(field.label)}${field.required ? ' *' : ''}
        <input data-custom-field data-custom-field-alias="${escapeHtml(field.alias)}" data-group-id="${groupId}" data-variant-id="${variantId}" value="${escapeHtml(field.value ?? '')}" ${field.required ? 'required' : ''}>
      </label>
    `).join('');
  }

  private renderPriceTable(groupId: number, variant: VariantItem): string {
    return `
      <section>
        <h3>Prices</h3>
        <table>
          <thead><tr><th>Store</th><th>Currency</th><th>Price</th></tr></thead>
          <tbody>
            ${(this.product?.stores ?? []).flatMap(store => (store.currencies ?? []).map(currency => {
              const storeAlias = store.alias ?? '';
              const currencyValue = currency.currencyValue ?? '';
              const price = getPrice(variant.priceValues?.[storeAlias]?.find(item => getCurrency(item) === currencyValue));
              return `<tr><td>${escapeHtml(store.title ?? storeAlias)}</td><td>${escapeHtml(currency.isoCurrencySymbol ?? currencyValue)}</td><td><input class="numeric" type="number" min="0" step="any" data-price data-price-store="${escapeHtml(storeAlias)}" data-price-currency="${escapeHtml(currencyValue)}" data-group-id="${groupId}" data-variant-id="${variant.id}" value="${escapeHtml(price)}"></td></tr>`;
            })).join('')}
          </tbody>
        </table>
      </section>
    `;
  }

  private renderStockTable(groupId: number, variant: VariantItem): string {
    const stockItems = variant.stockValues.length > 0 ? variant.stockValues : this.createEmptyStockValues();

    if (stockItems.length <= 1) {
      const stock = stockItems[0]?.value ?? 0;
      const storeAlias = stockItems[0]?.storeAlias ?? '';
      return `<label>Stock<input class="numeric" type="number" min="0" step="any" data-stock data-stock-store="${escapeHtml(storeAlias)}" data-group-id="${groupId}" data-variant-id="${variant.id}" value="${escapeHtml(stock)}"></label>`;
    }

    return `
      <section>
        <h3>Stock</h3>
        <table>
          <thead><tr><th>Store</th><th>Stock</th></tr></thead>
          <tbody>
            ${stockItems.map(stock => `<tr><td>${escapeHtml(this.getStoreTitle(stock.storeAlias))}</td><td><input class="numeric" type="number" min="0" step="any" data-stock data-stock-store="${escapeHtml(stock.storeAlias)}" data-group-id="${groupId}" data-variant-id="${variant.id}" value="${escapeHtml(stock.value)}"></td></tr>`).join('')}
          </tbody>
        </table>
      </section>
    `;
  }

  private renderMediaPicker(label: string, value: string, attribute: string, groupId: number, variantId = 0): string {
    return `
      <div class="media-field">
        <span class="field-label">${escapeHtml(label)}</span>
        <umb-input-media ${attribute} max="100" value="${escapeHtml(value ?? '')}" data-group-id="${groupId}" data-variant-id="${variantId}" data-value="${escapeHtml(value ?? '')}"></umb-input-media>
      </div>
    `;
  }

  private bindEvents(): void {
    this.querySelectorAll('[data-action="create-group"]').forEach(button => {
      button.addEventListener('click', () => this.addGroup());
    });

    this.querySelector('[data-action="save"]')?.addEventListener('click', () => void this.save());
    this.querySelector('[data-action="delete-selected"]')?.addEventListener('click', () => this.deleteSelected());
    this.querySelector('[data-action="delete-drawer-item"]')?.addEventListener('click', () => this.deleteDrawerItem());

    this.querySelector('[data-action="save-drawer"]')?.addEventListener('click', () => {
      this.syncMediaPickers();

      if (!this.validateDrawerCustomFields()) {
        return;
      }

      this.drawer = undefined;
      this.render();
    });

    this.querySelectorAll('[data-action="close-drawer"]').forEach(button => {
      button.addEventListener('click', () => {
        this.syncMediaPickers();
        this.drawer = undefined;
        this.render();
      });
    });

    this.querySelectorAll('[data-action="set-language"]').forEach(button => {
      button.addEventListener('click', event => {
        event.preventDefault();
        event.stopPropagation();
        this.setActiveLanguage((button as HTMLElement).dataset.tabValue ?? '');
      });
    });

    this.querySelectorAll('[data-action="toggle-group"]').forEach(button => {
      button.addEventListener('click', () => this.toggleGroup(Number((button as HTMLElement).dataset.groupId)));
    });

    this.querySelectorAll('[data-action="edit-group"]').forEach(button => {
      button.addEventListener('click', () => {
        this.drawer = { type: 'group', groupId: Number((button as HTMLElement).dataset.groupId) };
        this.render();
      });
    });

    this.querySelectorAll('[data-action="create-variant"]').forEach(button => {
      button.addEventListener('click', () => this.addVariant(Number((button as HTMLElement).dataset.groupId)));
    });

    this.querySelectorAll('[data-action="edit-variant"]').forEach(button => {
      button.addEventListener('click', () => {
        const element = button as HTMLElement;
        this.drawer = { type: 'variant', groupId: Number(element.dataset.groupId), variantId: Number(element.dataset.variantId) };
        this.render();
      });
    });

    this.querySelectorAll<HTMLInputElement>('[data-select-group]').forEach(field => {
      field.addEventListener('change', () => {
        this.toggleSet(this.selectedGroupIds, Number(field.dataset.groupId), field.checked);
        this.render();
      });
    });

    this.querySelectorAll<HTMLInputElement>('[data-select-variant]').forEach(field => {
      field.addEventListener('change', () => {
        this.toggleSet(this.selectedVariantIds, Number(field.dataset.variantId), field.checked);
        this.render();
      });
    });

    this.querySelectorAll<HTMLInputElement>('[data-group-title]').forEach(field => {
      field.addEventListener('input', event => {
        event.stopPropagation();
        this.updateGroupTitle(Number(field.dataset.groupId), field.value);
      });
    });

    this.querySelectorAll<HTMLInputElement>('[data-variant-title]').forEach(field => {
      field.addEventListener('input', event => {
        event.stopPropagation();
        this.updateVariantTitle(Number(field.dataset.groupId), Number(field.dataset.variantId), field.value);
      });
    });

    this.querySelectorAll<HTMLInputElement>('[data-variant-field]').forEach(field => {
      field.addEventListener('input', event => {
        event.stopPropagation();
        this.updateVariant(Number(field.dataset.groupId), Number(field.dataset.variantId), 'sku', field.value);
      });
    });

    this.querySelectorAll<HTMLInputElement>('[data-custom-field]').forEach(field => {
      field.addEventListener('input', event => {
        event.stopPropagation();
        this.updateCustomField(Number(field.dataset.groupId), Number(field.dataset.variantId), field.dataset.customFieldAlias ?? '', field.value);
      });
    });

    this.querySelectorAll<HTMLInputElement>('[data-price]').forEach(field => {
      field.addEventListener('input', event => {
        event.stopPropagation();
        this.updatePrice(Number(field.dataset.groupId), Number(field.dataset.variantId), field.dataset.priceStore ?? '', field.dataset.priceCurrency ?? '', field.value);
      });
    });

    this.querySelectorAll<HTMLInputElement>('[data-stock]').forEach(field => {
      field.addEventListener('input', event => {
        event.stopPropagation();
        this.updateStock(Number(field.dataset.groupId), Number(field.dataset.variantId), field.dataset.stockStore ?? '', field.value);
      });
    });

    this.bindDragAndDrop();
    this.bindMediaPickers();
  }

  private bindDragAndDrop(): void {
    this.querySelectorAll<HTMLElement>('.group-card[data-drag-group-id]').forEach(card => {
      card.addEventListener('dragstart', event => {
        this.draggedGroupId = Number(card.dataset.dragGroupId);
        event.dataTransfer?.setData('text/plain', `group:${this.draggedGroupId}`);
        event.dataTransfer?.setDragImage(card, 20, 20);
      });

      card.addEventListener('dragover', event => {
        if (this.draggedGroupId != null) {
          event.preventDefault();
        }
      });

      card.addEventListener('drop', event => {
        event.preventDefault();
        this.reorderGroup(this.draggedGroupId, Number(card.dataset.dragGroupId));
        this.draggedGroupId = undefined;
      });

      card.addEventListener('dragend', () => {
        this.draggedGroupId = undefined;
      });
    });

    this.querySelectorAll<HTMLElement>('.variant-row[data-drag-variant-id]').forEach(row => {
      row.addEventListener('dragstart', event => {
        this.draggedVariant = {
          groupId: Number(row.dataset.dragGroupId),
          variantId: Number(row.dataset.dragVariantId),
        };
        event.stopPropagation();
        event.dataTransfer?.setData('text/plain', `variant:${this.draggedVariant.groupId}:${this.draggedVariant.variantId}`);
      });

      row.addEventListener('dragover', event => {
        if (this.draggedVariant?.groupId === Number(row.dataset.dragGroupId)) {
          event.preventDefault();
          event.stopPropagation();
        }
      });

      row.addEventListener('drop', event => {
        event.preventDefault();
        event.stopPropagation();
        this.reorderVariant(this.draggedVariant, Number(row.dataset.dragGroupId), Number(row.dataset.dragVariantId));
        this.draggedVariant = undefined;
      });

      row.addEventListener('dragend', event => {
        event.stopPropagation();
        this.draggedVariant = undefined;
      });
    });
  }

  private bindMediaPickers(): void {
    this.querySelectorAll<HTMLElement>('[data-group-images], [data-variant-images]').forEach(field => {
      const mediaPicker = field as HTMLElement & { value?: string; selection?: string[] };
      const value = field.dataset.value ?? '';
      const selection = splitImages(value);
      mediaPicker.value = selection.join(',');
      mediaPicker.selection = selection;
      void setupMediaPickerSorterDetailPatch(mediaPicker);
      field.addEventListener('change', async event => {
        const groupId = Number(field.dataset.groupId);
        const variantId = Number(field.dataset.variantId);
        event.stopPropagation();

        await Promise.resolve();

        const nextValue = getMediaPickerValue(mediaPicker);
        await clearMediaPickerSorterDetails(mediaPicker);

        if (variantId !== 0) {
          this.updateVariant(groupId, variantId, 'images', nextValue);
        } else {
          this.updateGroupImages(groupId, nextValue);
        }
      });
    });
  }

  private syncMediaPickers(): void {
    this.querySelectorAll<HTMLElement>('[data-group-images], [data-variant-images]').forEach(field => {
      const mediaPicker = field as HTMLElement & { value?: string; selection?: string[] };
      const groupId = Number(field.dataset.groupId);
      const variantId = Number(field.dataset.variantId);
      const nextValue = getMediaPickerValue(mediaPicker);

      if (variantId !== 0) {
        const variant = this.getVariant(groupId, variantId);

        if (variant != null && variant.images !== nextValue) {
          variant.images = nextValue;
        }

        return;
      }

      const group = this.getGroup(groupId);

      if (group != null && group.images !== nextValue) {
        group.images = nextValue;
      }
    });

    this.updateSaveButtonState();
  }

  private updateSaveButtonState(): void {
    const saveButton = this.querySelector<HTMLElement>('[data-action="save"]');

    if (saveButton != null) {
      saveButton.toggleAttribute('disabled', this.saving || !this.hasChanges());
    }
  }

  private updateCustomField(groupId: number, variantId: number, alias: string, value: string): void {
    const item = variantId !== 0
      ? this.getVariant(groupId, variantId)
      : this.getGroup(groupId);

    const field = item?.customFields?.find(customField => customField.alias === alias);
    if (field != null) {
      field.value = value;
    }

    this.updateSaveButtonState();
  }

  private validateDrawerCustomFields(): boolean {
    if (this.drawer == null) {
      return true;
    }

    const item = this.drawer.type === 'group'
      ? this.getGroup(this.drawer.groupId)
      : this.getVariant(this.drawer.groupId, this.drawer.variantId);

    return this.validateCustomFields(item?.customFields);
  }

  private validateAllCustomFields(): boolean {
    for (const group of this.product?.groups ?? []) {
      if (!this.validateCustomFields(group.customFields, false)) {
        return false;
      }

      for (const variant of group.variants) {
        if (!this.validateCustomFields(variant.customFields, false)) {
          return false;
        }
      }
    }

    return true;
  }

  private validateCustomFields(fields: CustomField[] | undefined, render = true): boolean {
    const missing = fields?.find(field => field.required && !field.value?.trim());

    if (missing == null) {
      return true;
    }

    this.showError(`${missing.label} is required.`);
    if (render) {
      this.render();
    }

    return false;
  }

  private reorderGroup(sourceId: number | undefined, targetId: number): void {
    if (this.product == null || sourceId == null || sourceId === targetId) {
      return;
    }

    this.product.groups = reorderById(this.product.groups, sourceId, targetId);
    this.product.groups.forEach((group, index) => {
      group.sortOrder = index;
    });
    this.render();
  }

  private reorderVariant(source: { groupId: number; variantId: number } | undefined, targetGroupId: number, targetVariantId: number): void {
    if (source == null || source.groupId !== targetGroupId || source.variantId === targetVariantId) {
      return;
    }

    const group = this.getGroup(source.groupId);

    if (group == null) {
      return;
    }

    group.variants = reorderById(group.variants, source.variantId, targetVariantId);
    group.variants.forEach((variant, index) => {
      variant.sortOrder = index;
    });
    this.render();
  }

  private setActiveLanguage(value: string): void {
    this.activeLanguage = value;

    this.querySelectorAll<HTMLElement>('[data-action="set-language"]').forEach(button => {
      button.classList.toggle('active', button.dataset.tabValue === value);
    });

    if (this.drawer == null) {
      return;
    }

    if (this.drawer.type === 'group') {
      const group = this.getGroup(this.drawer.groupId);
      const field = this.querySelector<HTMLInputElement>('[data-group-title]');

      if (group != null && field != null) {
        field.value = group.titleValues?.[value] ?? '';
      }

      return;
    }

    const variant = this.getVariant(this.drawer.groupId, this.drawer.variantId);
    const field = this.querySelector<HTMLInputElement>('[data-variant-title]');

    if (variant != null && field != null) {
      field.value = variant.titleValues?.[value] ?? '';
    }
  }

  private toggleGroup(groupId: number): void {
    if (this.expandedGroupIds.has(groupId)) {
      this.expandedGroupIds.delete(groupId);
    } else {
      this.expandedGroupIds.add(groupId);
    }

    this.render();
  }

  private toggleSet(set: Set<number>, id: number, selected: boolean): void {
    if (selected) {
      set.add(id);
    } else {
      set.delete(id);
    }
  }

  private getTitle(item: VariantGroup | VariantItem, fallback: string): string {
    return item.titleValues?.[this.activeLanguage] || getFirstValue(item.titleValues) || item.title || item.name || fallback;
  }

  private getDefaultPrice(variant: VariantItem): string {
    const store = this.product?.stores[0];
    const currency = store?.currencies?.[0];

    if (store == null || currency == null) {
      return '—';
    }

    const value = getPrice(variant.priceValues?.[store.alias ?? '']?.find(item => getCurrency(item) === (currency.currencyValue ?? '')));
    const symbol = currency.currencySymbol ?? currency.isoCurrencySymbol ?? currency.currencyValue ?? '';
    return `${formatNumber(value)} ${symbol}`.trim();
  }

  private getTotalStock(variant: VariantItem): string {
    if (variant.stockValues.length === 0) {
      return '0';
    }

    return formatNumber(variant.stockValues.reduce((total, stock) => total + (Number(stock.value) || 0), 0));
  }

  private getStoreTitle(alias: string): string {
    return this.product?.stores.find(store => store.alias === alias)?.title ?? alias;
  }

  private getProductId(): string {
    return this.productId || this.getProductIdFromUrl();
  }

  private getProductIdFromUrl(): string {
    const href = window.location.href;
    const guidMatch = href.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);

    if (guidMatch != null) {
      return guidMatch[0];
    }

    const editSegmentMatch = href.match(/\/edit\/([^/?#]+)/i);
    return editSegmentMatch?.[1] ?? '';
  }

  private async fetchJson<T>(url: string): Promise<T> {
    const response = await fetch(url, { credentials: 'same-origin', headers: { Accept: 'application/json' } });
    return parseJsonResponse<T>(response);
  }

  private async postJson<T = unknown>(url: string, body: unknown): Promise<T> {
    const response = await fetch(url, {
      method: 'POST',
      credentials: 'same-origin',
      headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
      body: JSON.stringify(body),
    });

    return parseJsonResponse<T>(response);
  }

  private async deleteJson(url: string): Promise<void> {
    const response = await fetch(url, { method: 'DELETE', credentials: 'same-origin', headers: { Accept: 'application/json' } });
    await parseJsonResponse(response);
  }
}

async function parseJsonResponse<T>(response: Response): Promise<T> {
  if (!response.ok) {
    throw new Error(await response.text() || 'Request failed.');
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return response.json() as Promise<T>;
}

function getErrorMessage(error: unknown, fallback: string): string {
  return error instanceof Error && error.message.length > 0 ? error.message : fallback;
}

function escapeHtml(value: unknown): string {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function getFirstValue(values: Record<string, string>): string {
  return Object.values(values ?? {}).find(value => value != null && value.trim().length > 0) ?? '';
}

function getLanguageLabel(language: VariantLanguage): string {
  const value = language.isoCode ?? language.cultureName ?? '';
  return value.split('-')[0].toUpperCase();
}

function getCurrency(price: CurrencyPrice): string {
  return price.Currency ?? price.currency ?? '';
}

function getPrice(price: CurrencyPrice | undefined): number {
  return price?.Price ?? price?.price ?? 0;
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat().format(value);
}

function splitImages(value: string): string[] {
  const rawValue = String(value ?? '').trim();

  if (rawValue.length === 0) {
    return [];
  }

  if (rawValue.startsWith('[')) {
    try {
      const parsed = JSON.parse(rawValue) as Array<Record<string, unknown>>;
      return parsed
        .map(item => String(item.mediaKey ?? item.key ?? '').trim())
        .filter(Boolean);
    } catch {
      return [];
    }
  }

  return rawValue.split(',').map(item => normalizeMediaIdentifier(item.trim())).filter(Boolean);
}

function getFirstImage(images: string): string {
  return splitImages(images)[0] ?? '';
}

function getGroupThumbnailImage(group: VariantGroup): string {
  const groupImage = getFirstImage(group.images);

  if (groupImage) {
    return groupImage;
  }

  for (const variant of group.variants) {
    const variantImage = getFirstImage(variant.images);

    if (variantImage) {
      return variantImage;
    }
  }

  return '';
}

function renderThumbnail(image: string, alt: string): string {
  if (!image) {
    return '-';
  }

  return `<umb-imaging-thumbnail unique="${escapeHtml(image)}" width="38" height="38" alt="${escapeHtml(alt)}"></umb-imaging-thumbnail>`;
}

function normalizeMediaIdentifier(value: string): string {
  const udiMatch = value.match(/umb:\/\/media\/(.+)$/i);
  return udiMatch?.[1] ?? value;
}

function getMediaPickerValue(mediaPicker: { value?: string; selection?: string[] }): string {
  if (Array.isArray(mediaPicker.selection)) {
    return normalizeMediaSelection(mediaPicker.selection).join(',');
  }

  return splitImages(mediaPicker.value ?? '').join(',');
}

const patchedMediaPickers = new WeakSet<HTMLElement>();

// Umbraco 17's umb-input-media sorter reads uui-card-media[detail] to find the
// selected media key, but the component does not render that attribute itself.
// Add it only while dragging so sorting works without showing GUIDs in the card UI.
async function setupMediaPickerSorterDetailPatch(mediaPicker: HTMLElement & { updateComplete?: Promise<unknown> }): Promise<void> {
  if (patchedMediaPickers.has(mediaPicker)) {
    await clearMediaPickerSorterDetails(mediaPicker);
    return;
  }

  patchedMediaPickers.add(mediaPicker);
  await clearMediaPickerSorterDetails(mediaPicker);

  const shadowRoot = mediaPicker.shadowRoot;

  if (shadowRoot == null) {
    return;
  }

  const setDetails = () => void setMediaPickerSorterDetails(mediaPicker);
  const clearDetails = () => window.setTimeout(() => void clearMediaPickerSorterDetails(mediaPicker));
  const observer = new MutationObserver(() => void clearMediaPickerSorterDetails(mediaPicker));
  observer.observe(shadowRoot, { childList: true, subtree: true });
  shadowRoot.addEventListener('pointerdown', setDetails, { capture: true });
  shadowRoot.addEventListener('dragstart', setDetails, { capture: true });
  shadowRoot.addEventListener('dragend', clearDetails, { capture: true });
  shadowRoot.addEventListener('drop', clearDetails, { capture: true });
}

async function setMediaPickerSorterDetails(mediaPicker: HTMLElement & { updateComplete?: Promise<unknown> }): Promise<void> {
  await mediaPicker.updateComplete;
  await new Promise(resolve => window.requestAnimationFrame(resolve));

  mediaPicker.shadowRoot?.querySelectorAll<HTMLElement>('uui-card-media[data-mark]').forEach(card => {
    const unique = card.dataset.mark?.split(':').pop() ?? '';

    if (unique) {
      card.setAttribute('detail', unique);
    }
  });
}

async function clearMediaPickerSorterDetails(mediaPicker: HTMLElement & { updateComplete?: Promise<unknown> }): Promise<void> {
  await mediaPicker.updateComplete;
  await new Promise(resolve => window.requestAnimationFrame(resolve));

  mediaPicker.shadowRoot?.querySelectorAll<HTMLElement>('uui-card-media[detail]').forEach(card => {
    card.removeAttribute('detail');
  });
}

function normalizeMediaSelection(selection: unknown[]): string[] {
  return selection
    .map(item => {
      if (typeof item === 'string') {
        return normalizeMediaIdentifier(item);
      }

      if (item != null && typeof item === 'object') {
        const record = item as Record<string, unknown>;
        return normalizeMediaIdentifier(String(record.udi ?? record.key ?? record.unique ?? record.id ?? ''));
      }

      return '';
    })
    .filter(Boolean);
}

function isDraft(id: number): boolean {
  return id <= 0;
}

function snapshotGroup(group: VariantGroup): string {
  return stableStringify({
    titleValues: group.titleValues,
    images: group.images,
    customFields: group.customFields,
    sortOrder: group.sortOrder,
  });
}

function snapshotVariant(variant: VariantItem): string {
  return stableStringify({
    titleValues: variant.titleValues,
    sku: variant.sku,
    images: variant.images,
    priceValues: variant.priceValues,
    stockValues: variant.stockValues,
    customFields: variant.customFields,
    sortOrder: variant.sortOrder,
  });
}

function createCustomFields(fields: CustomFieldDefinition[] | undefined): CustomField[] {
  return (fields ?? []).map(field => ({
    ...field,
    value: '',
  }));
}

function normalizePriceValues(priceValues: Record<string, CurrencyPrice[]>, stores: VariantStore[]): Record<string, CurrencyPrice[]> {
  const normalized: Record<string, CurrencyPrice[]> = {};

  for (const store of stores) {
    const storeAlias = store.alias ?? '';
    const pricesByCurrency = new Map<string, CurrencyPrice>();

    for (const [key, prices] of Object.entries(priceValues ?? {})) {
      if (key.toLowerCase() !== storeAlias.toLowerCase()) {
        continue;
      }

      for (const price of prices) {
        const currency = getCurrency(price);

        if (currency === '') {
          continue;
        }

        pricesByCurrency.set(currency.toLowerCase(), {
          Currency: currency,
          Price: getPrice(price),
        });
      }
    }

    if (pricesByCurrency.size > 0) {
      normalized[storeAlias] = [...pricesByCurrency.values()];
    }
  }

  return normalized;
}

function reorderById<T extends { id: number }>(items: T[], sourceId: number, targetId: number): T[] {
  const next = [...items];
  const sourceIndex = next.findIndex(item => item.id === sourceId);
  const targetIndex = next.findIndex(item => item.id === targetId);

  if (sourceIndex < 0 || targetIndex < 0) {
    return items;
  }

  const [item] = next.splice(sourceIndex, 1);
  next.splice(targetIndex, 0, item);
  return next;
}

function stableStringify(value: unknown): string {
  if (Array.isArray(value)) {
    return `[${value.map(stableStringify).join(',')}]`;
  }

  if (value != null && typeof value === 'object') {
    const record = value as Record<string, unknown>;
    return `{${Object.keys(record).sort().map(key => `${JSON.stringify(key)}:${stableStringify(record[key])}`).join(',')}}`;
  }

  return JSON.stringify(value);
}

const styles = `
  :host { display: block; padding: var(--uui-size-layout-1, 24px); background: #f4f3f5; color: #1b264f; font-family: Lato, Arial, sans-serif; }
  .ekm-variant-editor { display: grid; gap: 16px; }
  .top-bar, .selection-actions, .main-actions, .group-header, .group-header-actions { display: flex; gap: 12px; align-items: center; flex-wrap: wrap; }
  .top-bar { background: #f4f3f5; border-bottom: 1px solid #e2e1e6; box-shadow: 0 4px 10px rgba(27, 38, 79, .06); justify-content: space-between; margin: -24px -24px 0; padding: 16px 24px; position: sticky; top: 0; z-index: 10; }
  .selection-actions { min-height: 32px; }
  .main-actions { margin-left: auto; justify-content: flex-end; }
  .group-list { display: grid; gap: 14px; }
  .group-card { background: #fff; border: 1px solid #e2e1e6; border-radius: 6px; box-shadow: 0 1px 3px rgba(27, 38, 79, .06); overflow: hidden; }
  .group-card.is-draft, .variant-row.is-draft { background: #f8fbf9; }
  .group-header { padding: 12px 14px; }
  .drag-handle { color: #8b8994; cursor: grab; font-weight: 900; letter-spacing: -2px; user-select: none; }
  [draggable="true"]:active .drag-handle { cursor: grabbing; }
  .group-toggle, .group-title, .thumb--button, .close-button, .mini-tab { border: 0; background: transparent; color: inherit; cursor: pointer; font: inherit; }
  .group-title { font-weight: 900; text-align: left; }
  .thumb { display: inline-flex; width: 38px; height: 38px; align-items: center; justify-content: center; border: 1px solid #e2e1e6; border-radius: 4px; background: #f4f3f5; color: #686570; font-size: 11px; text-transform: uppercase; }
  .thumb umb-imaging-thumbnail { width: 100%; height: 100%; }
  .count, .hint { color: #686570; }
  .badge { border-radius: 999px; padding: 2px 8px; background: #ecf7f1; color: #188a4f; font-size: 11px; font-weight: 700; text-transform: uppercase; }
  .group-header-actions { margin-left: auto; }
  .variant-table { border-top: 1px solid #e2e1e6; }
  .variant-head, .variant-row { display: grid; grid-template-columns: 18px 24px 44px minmax(140px, 1.3fr) minmax(100px, .8fr) minmax(100px, .8fr) minmax(80px, .6fr) auto; gap: 12px; align-items: center; padding: 10px 14px; }
  .variant-head { background: #f8f7fa; color: #8b8994; font-size: 11px; font-weight: 900; letter-spacing: .04em; text-transform: uppercase; }
  .variant-row { border-top: 1px solid #e2e1e6; }
  .variant-price-cell, .variant-stock-cell { text-align: center; }
  input { box-sizing: border-box; width: 100%; border: 1px solid #c4c2cb; border-radius: 3px; padding: 8px; font: inherit; }
  input:focus { border-color: #1b264f; outline: none; }
  input[type="checkbox"] { width: 16px; height: 16px; accent-color: #188a4f; }
  .status { padding: 12px; background: #fff; border-radius: 3px; }
  .status--error { color: #d42054; }
  .drawer-backdrop { position: fixed; inset: 0; background: rgba(27, 38, 79, .35); z-index: 1000; }
  .drawer { position: fixed; top: 0; right: 0; bottom: 0; z-index: 1001; width: min(460px, 100vw); display: grid; grid-template-rows: auto 1fr auto; background: #fff; box-shadow: -6px 0 24px rgba(27, 38, 79, .18); animation: slide-in .18s ease-out; }
  .drawer-header, .drawer-footer { display: flex; gap: 12px; align-items: center; justify-content: space-between; padding: 18px; border-bottom: 1px solid #e2e1e6; }
  .drawer-footer { border-top: 1px solid #e2e1e6; border-bottom: 0; }
  .drawer-title { min-width: 0; }
  .drawer-header-actions { display: flex; gap: 8px; align-items: center; flex-shrink: 0; }
  .drawer-footer-left, .drawer-footer-right { display: flex; gap: 12px; align-items: center; }
  .drawer-footer-left { margin-right: auto; }
  .danger-button { background: #d42054; border: 1px solid #d42054; border-radius: 3px; color: #fff; cursor: pointer; font: inherit; font-weight: 700; padding: 8px 14px; }
  .danger-button:hover { background: #b51b46; border-color: #b51b46; }
  .drawer-header h2 { margin: 0; font-size: 20px; }
  .drawer-header p { margin: 4px 0 0; color: #686570; }
  .edit-icon { display: inline-block; font-size: 15px; line-height: 1; transform: translateY(-1px); }
  .drawer-body { display: grid; gap: 18px; align-content: start; overflow: auto; padding: 18px; }
  label, .media-field { display: grid; gap: 8px; font-weight: 700; }
  .field-label, h3 { font-weight: 900; margin: 0; }
  .drawer-body section h3 { font-size: 13px; margin-bottom: 10px; }
  .mini-tabs { display: inline-flex; gap: 4px; }
  .mini-tab { padding: 4px 8px; border-bottom: 2px solid transparent; color: #686570; font-weight: 700; }
  .mini-tab.active { border-bottom-color: #1b264f; color: #1b264f; }
  table { width: 100%; border-collapse: collapse; }
  th { background: #f8f7fa; color: #8b8994; font-size: 11px; letter-spacing: .04em; text-align: left; text-transform: uppercase; }
  th, td { border-bottom: 1px solid #e2e1e6; padding: 8px; }
  .numeric { text-align: right; }
  @keyframes slide-in { from { transform: translateX(100%); } to { transform: translateX(0); } }
`;

customElements.define('ekom-variants-workspace-view', EkomVariantsWorkspaceViewElement);

export default EkomVariantsWorkspaceViewElement;
