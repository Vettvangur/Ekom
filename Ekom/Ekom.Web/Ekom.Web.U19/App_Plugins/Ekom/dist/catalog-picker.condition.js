var k = (e) => {
  throw TypeError(e);
};
var h = (e, t, i) => t.has(e) || k("Cannot " + i);
var g = (e, t, i) => (h(e, t, "read from private field"), i ? i.call(e) : t.get(e)), d = (e, t, i) => t.has(e) ? k("Cannot add the same private member more than once") : t instanceof WeakSet ? t.add(e) : t.set(e, i), w = (e, t, i, a) => (h(e, t, "write to private field"), a ? a.call(e, i) : t.set(e, i), i), c = (e, t, i) => (h(e, t, "access private method"), i);
import { UMB_COLLECTION_CONTEXT as P } from "@umbraco-cms/backoffice/collection";
import { UMB_DOCUMENT_WORKSPACE_CONTEXT as p } from "@umbraco-cms/backoffice/document";
import { UmbConditionBase as v } from "@umbraco-cms/backoffice/extension-registry";
import { i as A } from "./catalog-picker-context.js";
const I = "Ekom.CollectionView.Catalog", L = /* @__PURE__ */ new Set(["ekmCatalog", "ekmCategory"]), O = "ekom.catalog.collectionView", S = /* @__PURE__ */ new WeakSet();
function y(e) {
  return e != null && L.has(e);
}
function b(e, t, i) {
  if (!y(i) || A(t.getConfig()) || S.has(t)) return;
  S.add(t);
  const a = t.view, r = D() ?? I;
  let o = !1, m = !1;
  e.observe(
    a.views,
    (s) => {
      var E;
      if (o || A(t.getConfig())) return;
      const n = s == null ? void 0 : s.find((u) => u.alias === r);
      if (n != null) {
        if (o = !0, ((E = a.getCurrentView()) == null ? void 0 : E.alias) === n.alias) {
          m = !0;
          return;
        }
        N(a) ? Promise.resolve().then(() => {
          var T, _;
          if (((T = a.getCurrentView()) == null ? void 0 : T.alias) === n.alias) return;
          const u = (_ = t.getConfig()) == null ? void 0 : _.layouts;
          a.setConfig({
            defaultViewAlias: n.alias,
            ...u != null && u.length > 0 ? { viewsOverride: u } : {}
          });
        }) : a.setCurrentView(n);
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
        M(s.alias);
      }
    },
    "ekomCatalogDefaultViewPreference"
  );
}
function N(e) {
  return Object.prototype.hasOwnProperty.call(e, "routes");
}
function D() {
  try {
    return window.localStorage.getItem(O) || void 0;
  } catch {
    return;
  }
}
function M(e) {
  try {
    e === I ? window.localStorage.removeItem(O) : window.localStorage.setItem(O, e);
  } catch {
  }
}
var l, C, f, V;
class K extends v {
  constructor(i, a) {
    super(i, a);
    d(this, f);
    d(this, l);
    d(this, C);
    this.consumeContext(P, (r) => {
      const o = () => {
        this.permitted = r != null && (this.config.allowInPickers === !0 || !A(r.getConfig()));
      };
      o(), w(this, l, r), c(this, f, V).call(this), this.observe(r == null ? void 0 : r.selectOnly, o, "ekomCatalogPickerMode"), this.observe(r == null ? void 0 : r.filter, o, "ekomCatalogPickerConfig");
    }), this.consumeContext(p, (r) => {
      this.observe(
        r == null ? void 0 : r.structure.ownerContentType,
        (o) => {
          w(this, C, o == null ? void 0 : o.alias), c(this, f, V).call(this);
        },
        "ekomCatalogContentType"
      );
    });
  }
}
l = new WeakMap(), C = new WeakMap(), f = new WeakSet(), V = function() {
  g(this, l) != null && b(this, g(this, l), g(this, C));
};
export {
  K as default
};
