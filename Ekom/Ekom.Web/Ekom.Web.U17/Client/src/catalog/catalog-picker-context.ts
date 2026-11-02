import type { UmbCollectionConfiguration } from '@umbraco-cms/backoffice/collection';

/** Pickers explicitly configure selectOnly; bulk selection in a workspace does not. */
export function isCatalogPicker(config: UmbCollectionConfiguration | undefined): boolean {
  return config?.selectionConfiguration?.selectOnly === true;
}
