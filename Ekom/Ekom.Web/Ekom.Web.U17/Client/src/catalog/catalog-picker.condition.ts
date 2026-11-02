import { UMB_COLLECTION_CONTEXT } from '@umbraco-cms/backoffice/collection';
import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import type { UmbConditionConfigBase, UmbConditionControllerArguments } from '@umbraco-cms/backoffice/extension-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';
import { isCatalogPicker } from './catalog-picker-context';

export type CatalogPickerConditionConfig = UmbConditionConfigBase<'Ekom.Condition.CatalogPicker'> & {
  allowInPickers?: boolean;
};

export default class CatalogPickerCondition extends UmbConditionBase<CatalogPickerConditionConfig> {
  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<CatalogPickerConditionConfig>) {
    super(host, args);
    this.consumeContext(UMB_COLLECTION_CONTEXT, context => {
      const update = (): void => {
        this.permitted = context != null && (this.config.allowInPickers === true || !isCatalogPicker(context.getConfig()));
      };
      update();
      // Re-evaluate configuration changes, but never mistake ordinary bulk selection for a picker.
      this.observe(context?.selectOnly, update, 'ekomCatalogPickerMode');
      this.observe(context?.filter, update, 'ekomCatalogPickerConfig');
    });
  }
}

declare global {
  interface UmbExtensionConditionConfigMap {
    EkomCatalogPickerCondition: CatalogPickerConditionConfig;
  }
}
