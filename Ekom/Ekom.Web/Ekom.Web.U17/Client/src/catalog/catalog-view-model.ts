export type CatalogNode = {
  id: number;
  key: string;
  name: string;
  title: string;
  contentTypeAlias: string;
  productCount: number;
  subcategoryCount: number;
  status: 'Published' | 'Pending changes' | 'Unpublished';
  published: boolean;
  pendingChanges: boolean;
  totalProductCount: number;
  liveCount: number;
  outOfStockCount: number;
};

export type CatalogBreadcrumb = CatalogNode & {
  siblings: CatalogNode[];
};

export type CatalogProduct = CatalogNode & {
  sku: string;
  price: string;
  originalPrice: string;
  variantCount: number;
  stock: number;
  available: boolean;
  linked: boolean;
  image: string;
};

export type CatalogStatus = 'all' | 'published' | 'unpublished' | 'oos';

export type CatalogStatusCounts = {
  all: number;
  published: number;
  unpublished: number;
  outOfStock: number;
};

export type CatalogResponse = {
  current: CatalogNode;
  parent?: CatalogNode;
  breadcrumbs: CatalogBreadcrumb[];
  subcategories: CatalogNode[];
  products: CatalogProduct[];
  productCount: number;
  subcategoryCount: number;
  filteredProductCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
  status: CatalogStatus;
  statusCounts: CatalogStatusCounts;
};

export type Tone = 'green' | 'amber' | 'red' | 'grey' | 'muted';

export type Label = {
  text: string;
  tone: Tone;
};

export const DEFAULT_SORT = 'sortOrderAsc';

export const SORT_OPTIONS: ReadonlyArray<{ value: string; label: string }> = [
  { value: 'sortOrderAsc', label: 'Sort order asc' },
  { value: 'sortOrderDesc', label: 'Sort order desc' },
  { value: 'nameAsc', label: 'Name A–Z' },
  { value: 'nameDesc', label: 'Name Z–A' },
  { value: 'createdDesc', label: 'Created newest' },
  { value: 'createdAsc', label: 'Created oldest' },
  { value: 'updatedDesc', label: 'Updated newest' },
  { value: 'updatedAsc', label: 'Updated oldest' },
];

export const STATUS_CHIPS: ReadonlyArray<{ value: CatalogStatus; label: string; title: string; count: keyof CatalogStatusCounts }> = [
  { value: 'all', label: 'All', title: 'All products in this category', count: 'all' },
  { value: 'published', label: 'Published', title: 'Includes products with pending edits', count: 'published' },
  { value: 'unpublished', label: 'Unpublished', title: 'Draft and unpublished products', count: 'unpublished' },
  { value: 'oos', label: 'Out of stock', title: 'No stock on any variant', count: 'outOfStock' },
];

const LOW_STOCK_LIMIT = 5;

export function isSortOption(value: string): boolean {
  return SORT_OPTIONS.some(option => option.value === value);
}

export function sortLabel(value: string): string {
  return SORT_OPTIONS.find(option => option.value === value)?.label ?? SORT_OPTIONS[0].label;
}

export function isStatus(value: string): value is CatalogStatus {
  return STATUS_CHIPS.some(chip => chip.value === value);
}

/** Umbraco registers its dark theme as `umb-dark-theme`; every other theme is treated as light. */
export function themeFromAlias(alias: string | undefined | null): 'light' | 'dark' {
  return alias === 'umb-dark-theme' ? 'dark' : 'light';
}

export function plural(count: number, singular: string, pluralForm: string): string {
  return `${count} ${count === 1 ? singular : pluralForm}`;
}

export function metaLine(productCount: number, subcategoryCount: number): string {
  return `${plural(productCount, 'product', 'products')} · ${plural(subcategoryCount, 'subcategory', 'subcategories')}`;
}

export function stockLabel(stock: number): Label {
  if (!(stock > 0)) {
    return { text: 'Out of stock', tone: 'red' };
  }

  const units = formatUnits(stock);
  return stock < LOW_STOCK_LIMIT
    ? { text: `${units} left`, tone: 'amber' }
    : { text: `${units} in stock`, tone: 'green' };
}

/** One-line health summary for everything below a subcategory. */
export function healthLabel(node: Pick<CatalogNode, 'totalProductCount' | 'liveCount' | 'outOfStockCount'>): Label {
  if (node.totalProductCount === 0) {
    return { text: 'Empty', tone: 'amber' };
  }

  if (node.liveCount === 0) {
    return { text: 'Nothing published', tone: 'amber' };
  }

  if (node.outOfStockCount > 0) {
    return { text: `${node.outOfStockCount} out of stock`, tone: 'muted' };
  }

  return { text: `${node.liveCount} live`, tone: 'green' };
}

export function publishLabel(node: Pick<CatalogNode, 'published'>): Label {
  return node.published ? { text: 'Published', tone: 'green' } : { text: 'Draft', tone: 'amber' };
}

/** Selects every visible key, or clears them when they are all selected already. */
export function toggleAll(visibleKeys: readonly string[], selection: readonly string[]): string[] {
  if (visibleKeys.length > 0 && visibleKeys.every(key => selection.includes(key))) {
    return selection.filter(key => !visibleKeys.includes(key));
  }

  return [...new Set([...selection, ...visibleKeys])];
}

export function paginationItems(page: number, total: number): Array<number | '…'> {
  if (total <= 7) {
    return Array.from({ length: total }, (_, index) => index + 1);
  }

  const sorted = [...new Set([1, total, page - 1, page, page + 1])]
    .filter(item => item >= 1 && item <= total)
    .sort((a, b) => a - b);
  const result: Array<number | '…'> = [];

  for (const item of sorted) {
    const previous = result[result.length - 1];
    if (typeof previous === 'number' && item - previous > 1) {
      result.push('…');
    }

    result.push(item);
  }

  return result;
}

function formatUnits(stock: number): string {
  return Number.isInteger(stock) ? String(stock) : String(Math.round(stock * 100) / 100);
}
