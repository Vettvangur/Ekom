import type { UmbCollectionConfiguration } from '@umbraco-cms/backoffice/collection';
import { isCatalogPicker } from './catalog-picker-context';

export const CATALOG_COLLECTION_VIEW_ALIAS = 'Ekom.CollectionView.Catalog';

/** Document types whose children are browsed with the Catalog view. */
const CATALOG_CONTENT_TYPE_ALIASES = new Set(['ekmCatalog', 'ekmCategory']);
const VIEW_STORAGE_KEY = 'ekom.catalog.collectionView';

export type CatalogCollectionViewManifest = { alias: string };

/** The parts of Umbraco's collection view manager that choosing a landing view relies on. */
export type CatalogCollectionViewManager = {
  views: unknown;
  currentView: unknown;
  getCurrentView(): CatalogCollectionViewManifest | undefined;
  setCurrentView(view: never): void;
  setConfig(config: never): void;
};

export type CatalogCollectionContext = {
  getConfig(): UmbCollectionConfiguration | undefined;
  view: CatalogCollectionViewManager;
};

export type CatalogDefaultViewHost = {
  observe(source: never, callback: (value: never) => void, alias: string): unknown;
};

const handledCollections = new WeakSet<object>();

export function isCatalogContentType(alias: string | undefined): boolean {
  return alias != null && CATALOG_CONTENT_TYPE_ALIASES.has(alias);
}

/**
 * Umbraco lands every document collection on its table view. For Ekom catalog nodes, land on the
 * Catalog view instead, unless the user last chose another view. Pickers are left untouched, so
 * they keep Umbraco's own default.
 */
export function useCatalogAsDefaultView(
  host: CatalogDefaultViewHost,
  collection: CatalogCollectionContext,
  contentTypeAlias: string | undefined,
): void {
  if (!isCatalogContentType(contentTypeAlias) || isCatalogPicker(collection.getConfig())) return;
  if (handledCollections.has(collection)) return;
  handledCollections.add(collection);

  const viewManager = collection.view;
  const preferredAlias = getStoredViewAlias() ?? CATALOG_COLLECTION_VIEW_ALIAS;
  let landed = false;
  let tracking = false;

  host.observe(
    viewManager.views as never,
    ((views: Array<CatalogCollectionViewManifest> | undefined) => {
      if (landed || isCatalogPicker(collection.getConfig())) return;

      const preferred = views?.find(view => view.alias === preferredAlias);
      if (preferred == null) return;
      landed = true;

      if (viewManager.getCurrentView()?.alias === preferred.alias) {
        tracking = true;
        return;
      }

      if (isRouted(viewManager)) {
        // Umbraco 17 and 18 route each view; the landing view is the fallback of those routes.
        // Wait until every observer of this views update has run. Umbraco's view chooser resolves the
        // current view against its own copy of the views, and hides itself when that copy is behind.
        void Promise.resolve().then(() => {
          if (viewManager.getCurrentView()?.alias === preferred.alias) return;

          const layouts = collection.getConfig()?.layouts;
          viewManager.setConfig({
            defaultViewAlias: preferred.alias,
            ...(layouts != null && layouts.length > 0 ? { viewsOverride: layouts } : {}),
          } as never);
        });
      } else {
        viewManager.setCurrentView(preferred as never);
      }
    }) as never,
    'ekomCatalogDefaultViewLanding',
  );

  host.observe(
    viewManager.currentView as never,
    ((view: CatalogCollectionViewManifest | undefined) => {
      if (view == null) return;

      // Ignore Umbraco's own landing view; only remember what the user picks after ours is shown.
      if (!tracking) {
        tracking = landed && view.alias === preferredAlias;
        return;
      }

      storeViewAlias(view.alias);
    }) as never,
    'ekomCatalogDefaultViewPreference',
  );
}

function isRouted(viewManager: CatalogCollectionViewManager): boolean {
  // Routed managers assign `routes` in their constructor. Newer ones only keep a deprecated getter.
  return Object.prototype.hasOwnProperty.call(viewManager, 'routes');
}

function getStoredViewAlias(): string | undefined {
  try {
    return window.localStorage.getItem(VIEW_STORAGE_KEY) || undefined;
  } catch {
    return undefined;
  }
}

function storeViewAlias(alias: string): void {
  try {
    if (alias === CATALOG_COLLECTION_VIEW_ALIAS) {
      window.localStorage.removeItem(VIEW_STORAGE_KEY);
    } else {
      window.localStorage.setItem(VIEW_STORAGE_KEY, alias);
    }
  } catch {
    // The preference is a convenience; the Catalog view stays the default without storage.
  }
}
