import { UmbChangeEvent } from '@umbraco-cms/backoffice/event';
import type {
  ManifestPropertyEditorUi,
  UmbPropertyEditorConfigCollection,
  UmbPropertyEditorUiElement,
} from '@umbraco-cms/backoffice/property-editor';

type WarehouseDefinition = {
  key: string;
  code: string;
  name: string;
  sortOrder: number;
  visible: boolean;
};

export class EkomWarehouseEditorElement extends HTMLElement implements UmbPropertyEditorUiElement {
  manifest?: ManifestPropertyEditorUi;
  name?: string;
  dataSourceAlias?: string;
  config?: UmbPropertyEditorConfigCollection;

  private editor?: HTMLDivElement;
  private warehouses: WarehouseDefinition[] = [];
  private draggedWarehouseKey?: string;

  get value(): WarehouseDefinition[] {
    return this.warehouses;
  }

  set value(value: unknown) {
    this.warehouses = this.normalizeValue(value);
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
    this.addEventListener('focusout', this.onFocusOut);
    this.renderShell();
    this.renderRows();
  }

  override disconnectedCallback(): void {
    this.removeEventListener('focusout', this.onFocusOut);
  }

  private renderShell(): void {
    this.innerHTML = `
      <style>
        :host { display: block; }
        .warehouse-editor { display: grid; gap: var(--uui-size-space-3, 12px); max-width: 1000px; }
        .header, .row { display: grid; grid-template-columns: 56px minmax(120px, 1fr) minmax(180px, 2fr) 90px 80px; gap: var(--uui-size-space-2, 8px); align-items: center; }
        .header { font-weight: 700; }
        .header > :nth-child(4) { text-align: center; }
        input { box-sizing: border-box; width: 100%; min-height: 32px; border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px); background: var(--uui-color-surface, #fff); color: var(--uui-color-text, #1b264f); font: inherit; }
        input[type='checkbox'] { width: auto; min-height: auto; justify-self: center; }
        button { border: 0; border-radius: var(--uui-border-radius, 3px); padding: var(--uui-size-space-2, 8px) var(--uui-size-space-3, 12px); background: var(--uui-color-interactive, #3544b1); color: var(--uui-color-interactive-contrast, #fff); cursor: pointer; font: inherit; font-weight: 600; }
        button[data-kind='danger'] { background: var(--uui-color-danger, #d42054); color: var(--uui-color-danger-contrast, #fff); }
        button:disabled, input:disabled { cursor: not-allowed; opacity: 0.55; }
        .drag-handle { min-width: 32px; cursor: grab; }
        .drag-handle:active { cursor: grabbing; }
        @media (max-width: 700px) { .header { display: none; } .row { grid-template-columns: 56px 1fr 1fr; padding: var(--uui-size-space-3, 12px); border: 1px solid var(--uui-color-border, #d8d7d9); border-radius: var(--uui-border-radius, 3px); } .row input[data-field='name'] { grid-column: span 2; } }
      </style>
      <div class="warehouse-editor"></div>
    `;

    this.editor = this.querySelector('.warehouse-editor') ?? undefined;
  }

  private renderRows(): void {
    if (this.editor == null) {
      return;
    }

    const fragment = document.createDocumentFragment();
    if (this.warehouses.length > 0) {
      const header = document.createElement('div');
      header.className = 'header';

      for (const label of ['', 'Code', 'Name', 'Visible', 'Actions']) {
        const cell = document.createElement('div');
        cell.textContent = label;
        header.append(cell);
      }

      fragment.append(header);
    }
    this.warehouses.forEach((warehouse, index) => fragment.append(this.createRow(warehouse, index)));

    const addButton = document.createElement('button');
    addButton.type = 'button';
    addButton.textContent = 'Add warehouse';
    addButton.addEventListener('click', () => this.addWarehouse());
    fragment.append(addButton);

    this.editor.replaceChildren(fragment);
    this.syncDisabledState();
  }

  private createRow(warehouse: WarehouseDefinition, index: number): HTMLDivElement {
    const row = document.createElement('div');
    row.className = 'row';
    row.dataset.key = warehouse.key;
    row.addEventListener('dragover', event => this.onDragOver(event));
    row.addEventListener('drop', event => this.onDrop(event, warehouse.key));

    const dragHandle = document.createElement('button');
    dragHandle.type = 'button';
    dragHandle.className = 'drag-handle';
    dragHandle.textContent = '↕';
    dragHandle.title = 'Drag to reorder';
    dragHandle.setAttribute('aria-label', `Drag ${warehouse.name || warehouse.code || 'warehouse'} to reorder`);
    dragHandle.draggable = !this.readonly;
    dragHandle.addEventListener('dragstart', event => this.onDragStart(event, warehouse.key));
    dragHandle.addEventListener('dragend', () => this.clearDragState());
    row.append(dragHandle);

    row.append(
      this.createInput('code', 'text', warehouse.code, value => value),
      this.createInput('name', 'text', warehouse.name, value => value),
    );

    const visible = document.createElement('input');
    visible.type = 'checkbox';
    visible.checked = warehouse.visible;
    visible.dataset.field = 'visible';
    visible.addEventListener('change', () => this.updateWarehouse(index, 'visible', visible.checked));
    row.append(visible);

    const removeButton = document.createElement('button');
    removeButton.type = 'button';
    removeButton.textContent = 'Remove';
    removeButton.dataset.kind = 'danger';
    removeButton.addEventListener('click', () => this.removeWarehouse(index));
    row.append(removeButton);

    return row;
  }

  private createInput<TKey extends 'code' | 'name'>(
    field: TKey,
    type: string,
    value: string,
    transform: (value: string) => WarehouseDefinition[TKey],
  ): HTMLInputElement {
    const input = document.createElement('input');
    input.type = type;
    input.value = value;
    input.dataset.field = field;

    input.addEventListener('input', () => {
      const index = this.warehouses.findIndex(warehouse => warehouse.key === input.closest<HTMLElement>('.row')?.dataset.key);

      if (index !== -1) {
        this.updateWarehouse(index, field, transform(input.value), false);
      }
    });

    return input;
  }

  private addWarehouse(): void {
    if (this.readonly) {
      return;
    }

    this.warehouses = [...this.warehouses, {
      key: crypto.randomUUID(),
      code: '',
      name: '',
      sortOrder: this.warehouses.length,
      visible: true,
    }];
    this.renumberWarehouses();
    this.renderRows();
    this.emitChange();
  }

  private removeWarehouse(index: number): void {
    if (this.readonly) {
      return;
    }

    this.warehouses = this.warehouses.filter((_, warehouseIndex) => warehouseIndex !== index);
    this.renumberWarehouses();
    this.renderRows();
    this.emitChange();
  }

  private updateWarehouse<TKey extends 'code' | 'name' | 'visible'>(
    index: number,
    field: TKey,
    value: WarehouseDefinition[TKey],
    emitChange = true,
  ): void {
    if (this.readonly || this.warehouses[index] == null) {
      return;
    }

    this.warehouses = this.warehouses.map((warehouse, warehouseIndex) => warehouseIndex === index
      ? { ...warehouse, [field]: value }
      : warehouse);
    if (emitChange) {
      this.emitChange();
    }
  }

  private normalizeValue(value: unknown): WarehouseDefinition[] {
    const parsedValue = typeof value === 'string' ? this.tryParseJson(value) : value;

    if (!Array.isArray(parsedValue)) {
      return [];
    }

    return parsedValue.flatMap(item => {
      if (!this.isRecord(item)) {
        return [];
      }

      return [{
        key: typeof item.key === 'string' && item.key.length > 0 ? item.key : crypto.randomUUID(),
        code: item.code == null ? '' : String(item.code),
        name: item.name == null ? '' : String(item.name),
        sortOrder: this.parseSortOrder(item.sortOrder),
        visible: typeof item.visible === 'boolean'
          ? item.visible
          : typeof item.enabled === 'boolean'
            ? item.enabled
            : true,
      }];
    }).sort((left, right) => left.sortOrder - right.sortOrder || left.name.localeCompare(right.name));
  }

  private syncDisabledState(): void {
    for (const input of this.querySelectorAll<HTMLInputElement>('input')) {
      input.disabled = this.readonly;
    }

    for (const button of this.querySelectorAll<HTMLButtonElement>('button')) {
      button.disabled = this.readonly;

      if (button.classList.contains('drag-handle')) {
        button.draggable = !this.readonly;
      }
    }

    if (this.readonly) {
      this.clearDragState();
    }
  }

  private onDragStart(event: DragEvent, warehouseKey: string): void {
    if (this.readonly || event.dataTransfer == null) {
      event.preventDefault();
      return;
    }

    this.draggedWarehouseKey = warehouseKey;
    event.dataTransfer.effectAllowed = 'move';
    event.dataTransfer.setData('text/plain', warehouseKey);
  }

  private onDragOver(event: DragEvent): void {
    if (this.readonly || this.draggedWarehouseKey == null) {
      return;
    }

    event.preventDefault();
    event.dataTransfer!.dropEffect = 'move';
  }

  private onDrop(event: DragEvent, targetWarehouseKey: string): void {
    event.preventDefault();

    const sourceWarehouseKey = this.draggedWarehouseKey ?? event.dataTransfer?.getData('text/plain');
    this.clearDragState();

    if (this.readonly || sourceWarehouseKey == null || sourceWarehouseKey === targetWarehouseKey) {
      return;
    }

    const sourceIndex = this.warehouses.findIndex(warehouse => warehouse.key === sourceWarehouseKey);
    const targetIndex = this.warehouses.findIndex(warehouse => warehouse.key === targetWarehouseKey);

    if (sourceIndex < 0 || targetIndex < 0) {
      return;
    }

    const reorderedWarehouses = [...this.warehouses];
    const [warehouse] = reorderedWarehouses.splice(sourceIndex, 1);
    reorderedWarehouses.splice(targetIndex, 0, warehouse);
    this.warehouses = reorderedWarehouses;
    this.renumberWarehouses();
    this.renderRows();
    this.emitChange();
  }

  private clearDragState(): void {
    this.draggedWarehouseKey = undefined;
  }

  private onFocusOut = (event: FocusEvent): void => {
    if (event.relatedTarget instanceof Node && this.contains(event.relatedTarget)) {
      return;
    }

    this.emitChange();
  };

  private renumberWarehouses(): void {
    this.warehouses = this.warehouses.map((warehouse, index) => ({ ...warehouse, sortOrder: index }));
  }

  private parseSortOrder(value: unknown): number {
    const parsed = Number(value);
    return Number.isFinite(parsed) ? Math.trunc(parsed) : 0;
  }

  private tryParseJson(value: string): unknown {
    try {
      return JSON.parse(value) as unknown;
    } catch {
      return [];
    }
  }

  private isRecord(value: unknown): value is Record<string, unknown> {
    return value != null && typeof value === 'object' && !Array.isArray(value);
  }

  private emitChange(): void {
    this.dispatchEvent(new UmbChangeEvent());
  }
}

customElements.define('ekom-warehouse-editor', EkomWarehouseEditorElement);

export default EkomWarehouseEditorElement;

declare global {
  interface HTMLElementTagNameMap {
    'ekom-warehouse-editor': EkomWarehouseEditorElement;
  }
}
