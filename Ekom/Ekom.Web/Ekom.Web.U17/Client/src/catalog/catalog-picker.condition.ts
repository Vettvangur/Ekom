import { UMB_COLLECTION_CONTEXT } from '@umbraco-cms/backoffice/collection';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import { UMB_DOCUMENT_WORKSPACE_CONTEXT } from '@umbraco-cms/backoffice/document';
import type { UmbConditionConfigBase, UmbConditionControllerArguments } from '@umbraco-cms/backoffice/extension-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { useCatalogAsDefaultView, type CatalogCollectionContext, type CatalogDefaultViewHost } from './catalog-default-view';
import { isCatalogPicker } from './catalog-picker-context';

export type CatalogPickerConditionConfig = UmbConditionConfigBase<'Ekom.Condition.CatalogPicker'> & {
  allowInPickers?: boolean;
};

export default class CatalogPickerCondition extends UmbConditionBase<CatalogPickerConditionConfig> {
  #collection?: CatalogCollectionContext;
  #contentTypeAlias?: string;

  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<CatalogPickerConditionConfig>) {
    super(host, args);
    this.consumeContext(UMB_COLLECTION_CONTEXT, context => {
      const update = (): void => {
        this.permitted = context != null && (this.config.allowInPickers === true || !isCatalogPicker(context.getConfig()));
      };
      update();
      this.#collection = context as unknown as CatalogCollectionContext | undefined;
      this.#useCatalogAsDefaultView();
      // Re-evaluate configuration changes, but never mistake ordinary bulk selection for a picker.
      this.observe(context?.selectOnly, update, 'ekomCatalogPickerMode');
      this.observe(context?.filter, update, 'ekomCatalogPickerConfig');
    });
    // This condition runs once per document collection, which makes it the place to pick the landing view.
    this.consumeContext(UMB_DOCUMENT_WORKSPACE_CONTEXT, workspace => {
      this.observe(
        workspace?.structure.ownerContentType,
        contentType => {
          this.#contentTypeAlias = contentType?.alias;
          this.#useCatalogAsDefaultView();
        },
        'ekomCatalogContentType',
      );
    });
  }

  #useCatalogAsDefaultView(): void {
    if (this.#collection == null) return;
    useCatalogAsDefaultView(this as unknown as CatalogDefaultViewHost, this.#collection, this.#contentTypeAlias);
  }
}

declare global {
  interface UmbExtensionConditionConfigMap {
    EkomCatalogPickerCondition: CatalogPickerConditionConfig;
  }
}
