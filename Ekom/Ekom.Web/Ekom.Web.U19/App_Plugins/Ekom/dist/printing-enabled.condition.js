import { UmbConditionBase as a } from "@umbraco-cms/backoffice/extension-registry";
class r extends a {
  constructor(t, i) {
    super(t, i), this.permitted = !1, fetch("/ekom/printer/manager/capabilities", { credentials: "same-origin", headers: { Accept: "application/json" } }).then(async (e) => {
      e.ok && (this.permitted = (await e.json()).enabled === !0);
    }).catch(() => {
      this.permitted = !1;
    });
  }
}
export {
  r as default
};
