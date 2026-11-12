var _ = (e) => {
  throw TypeError(e);
};
var h = (e, t, i) => t.has(e) || _("Cannot " + i);
var g = (e, t, i) => (h(e, t, "read from private field"), i ? i.call(e) : t.get(e)), d = (e, t, i) => t.has(e) ? _("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), c = (e, t, i, a) => (h(e, t, "write to private field"), a ? a.call(e, i) : t.set(e, i), i), w = (e, t, i) => (h(e, t, "access private method"), i);
import { UMB_COLLECTION_CONTEXT as I } from "@umbraco-cms/backoffice/collection";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT as p } from "@umbraco-cms/backoffice/document";
import { UmbConditionBase as L } from "@umbraco-cms/backoffice/extension-registry";
import { i as A } from "./catalog-picker-context.js";
const S = "Ekom.CollectionView.Catalog", P = /* @__PURE__ */ new Set(["ekmCatalog", "ekmCategory"]), O = "ekom.catalog.collectionView", k = /* @__PURE__ */ new WeakSet();
function y(e) {
  return e != null && P.has(e);
}
function b(e, t, i) {
  if (!y(i) || A(t.getConfig()) || k.has(t)) return;
  k.add(t);
  const a = t.view, r = N() ?? S;
  let o = !1, m = !1;
  e.observe(
    a.views,
    (s) => {
      var V, T;
      if (o || A(t.getConfig())) return;
      const f = s == null ? void 0 : s.find((n) => n.alias === r);
      if (f != null) {
        if (o = !0, ((V = a.getCurrentView()) == null ? void 0 : V.alias) === f.alias) {
          m = !0;
          return;
        }
        if (v(a)) {
          const n = (T = t.getConfig()) == null ? void 0 : T.layouts;
          a.setConfig({
            defaultViewAlias: f.alias,
            ...n != null && n.length > 0 ? { viewsOverride: n } : {}
          });
        } else
          a.setCurrentView(f);
      }
    },
    "ekomCatalogDefaultViewLanding"
  ), e.observe(
    a.currentView,
    (s) => {
      if (s != null) {
        if (!m) {
          m = o && s.alias === r;
          return;
        }
        D(s.alias);
      }
    },
    "ekomCatalogDefaultViewPreference"
  );
}
function v(e) {
  return Object.prototype.hasOwnProperty.call(e, "routes");
}
function N() {
  try {
    return window.localStorage.getItem(O) || void 0;
  } catch {
    return;
  }
}
function D(e) {
  try {
    e === S ? window.localStorage.removeItem(O) : window.localStorage.setItem(O, e);
  } catch {
  }
}
var l, u, C, E;
class R extends L {
  constructor(i, a) {
    super(i, a);
    d(this, C);
    d(this, l);
    d(this, u);
    this.consumeContext(I, (r) => {
      const o = () => {
        this.permitted = r != null && (this.config.allowInPickers === !0 || !A(r.getConfig()));
      };
      o(), c(this, l, r), w(this, C, E).call(this), this.observe(r == null ? void 0 : r.selectOnly, o, "ekomCatalogPickerMode"), this.observe(r == null ? void 0 : r.filter, o, "ekomCatalogPickerConfig");
    }), this.consumeContext(p, (r) => {
      this.observe(
        r == null ? void 0 : r.structure.ownerContentType,
        (o) => {
          c(this, u, o == null ? void 0 : o.alias), w(this, C, E).call(this);
        },
        "ekomCatalogContentType"
      );
    });
  }
}
l = new WeakMap(), u = new WeakMap(), C = new WeakSet(), E = function() {
  g(this, l) != null && b(this, g(this, l), g(this, u));
};
export {
  R as default
};
