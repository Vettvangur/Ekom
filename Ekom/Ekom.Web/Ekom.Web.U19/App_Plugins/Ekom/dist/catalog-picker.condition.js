import { UMB_COLLECTION_CONTEXT as e } from "@umbraco-cms/backoffice/collection";
import { UmbConditionBase as a } from "@umbraco-cms/backoffice/extension-registry";
import { i as l } from "./catalog-picker-context.js";
class d extends a {
  constructor(s, o) {
    super(s, o), this.consumeContext(e, (i) => {
      const r = () => {
        this.permitted = i != null && (this.config.allowInPickers === !0 || !l(i.getConfig()));
      };
      r(), this.observe(i == null ? void 0 : i.selectOnly, r, "ekomCatalogPickerMode"), this.observe(i == null ? void 0 : i.filter, r, "ekomCatalogPickerConfig");
    });
  }
}
export {
  d as default
};
