import type { UmbControllerHost } from '@umbraco-cms/backoffice/controller-api';
import type { UmbConditionConfigBase, UmbConditionControllerArguments } from '@umbraco-cms/backoffice/extension-api';
import { UmbConditionBase } from '@umbraco-cms/backoffice/extension-registry';

type PrintingConditionConfig = UmbConditionConfigBase<'Ekom.Condition.PrintingEnabled'>;
export default class PrintingEnabledCondition extends UmbConditionBase<PrintingConditionConfig> {
  constructor(host: UmbControllerHost, args: UmbConditionControllerArguments<PrintingConditionConfig>) {
    super(host, args);
    this.permitted = false;
    void fetch('/ekom/printer/manager/capabilities', { credentials: 'same-origin', headers: { Accept: 'application/json' } })
      .then(async response => { if (response.ok) this.permitted = (await response.json()).enabled === true; })
      .catch(() => { this.permitted = false; });
  }
}
declare global {
  interface UmbExtensionConditionConfigMap { EkomPrintingEnabledCondition: PrintingConditionConfig; }
}
