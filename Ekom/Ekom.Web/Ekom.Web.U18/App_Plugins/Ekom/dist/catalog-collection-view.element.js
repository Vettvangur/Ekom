var O = Object.defineProperty;
var z = (r, a, e) => a in r ? O(r, a, { enumerable: !0, configurable: !0, writable: !0, value: e }) : r[a] = e;
var l = (r, a, e) => z(r, typeof a != "symbol" ? a + "" : a, e);
import { UMB_COLLECTION_CONTEXT as M } from "@umbraco-cms/backoffice/collection";
import { UmbEntityContext as U } from "@umbraco-cms/backoffice/entity";
import { UmbExtensionElementInitializer as q } from "@umbraco-cms/backoffice/extension-api";
import { umbExtensionsRegistry as N } from "@umbraco-cms/backoffice/extension-registry";
import { css as A, svg as C, html as i, nothing as c, repeat as b } from "@umbraco-cms/backoffice/external/lit";
import { UmbImagingRepository as R } from "@umbraco-cms/backoffice/imaging";
import { UmbLitElement as P } from "@umbraco-cms/backoffice/lit-element";
import { UMB_THEME_CONTEXT as H } from "@umbraco-cms/backoffice/themes";
import { i as F } from "./catalog-picker-context.js";
const D = A`
  :host {
    --ekm-bg: #f0f1f4;
    --ekm-sec: #f4f5f7;
    --ekm-sf: #ffffff;
    --ekm-sf2: #f6f7f9;
    --ekm-bd: #e1e3e8;
    --ekm-bd2: #eceef1;
    --ekm-tx: #191b24;
    --ekm-mu: #676b7b;
    --ekm-ac: #10694a;
    --ekm-acs: #e7f3ed;
    --ekm-tile: #eef0f3;
    --ekm-shadow: rgba(20, 22, 35, 0.12);
    --ekm-bd-hover: rgba(28, 31, 43, 0.3);
    --ekm-green-bg: #e7f3ed;
    --ekm-green-fg: #0b5238;
    --ekm-green-dot: #2f9e6b;
    --ekm-grey-bg: #eef0f3;
    --ekm-grey-fg: #4b5160;
    --ekm-grey-dot: #9aa0ad;
    --ekm-amber-bg: #fff4e2;
    --ekm-amber-fg: #7a4e06;
    --ekm-amber-dot: #c98a14;
    --ekm-red-bg: #fdeceb;
    --ekm-red-fg: #8c1d18;
    --ekm-red-dot: #b3261e;
    --ekm-on-accent: #ffffff;
    --ekm-mono: 'IBM Plex Mono', ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;

    background: var(--ekm-sec);
    border: 1px solid var(--ekm-bd);
    border-radius: 12px;
    box-sizing: border-box;
    color: var(--ekm-tx);
    display: block;
    font-family: Lato, 'Helvetica Neue', Helvetica, Arial, sans-serif;
    min-height: 100%;
  }

  :host([data-theme='dark']) {
    --ekm-bg: #1e232c;
    --ekm-sec: #222831;
    --ekm-sf: #262c36;
    --ekm-sf2: #2d343f;
    --ekm-bd: #3a414d;
    --ekm-bd2: #323945;
    --ekm-tx: #eef1f5;
    --ekm-mu: #aab2bf;
    --ekm-ac: #4cc38f;
    --ekm-acs: #1f3a31;
    --ekm-tile: #303743;
    --ekm-shadow: rgba(0, 0, 0, 0.35);
    --ekm-bd-hover: rgba(238, 241, 245, 0.4);
    --ekm-green-bg: #1f3a31;
    --ekm-green-fg: #8fe0bc;
    --ekm-green-dot: #4cc38f;
    --ekm-grey-bg: #333a46;
    --ekm-grey-fg: #c3c9d3;
    --ekm-grey-dot: #8f97a5;
    --ekm-amber-bg: #413318;
    --ekm-amber-fg: #ecc784;
    --ekm-amber-dot: #ecc784;
    --ekm-red-bg: #46211f;
    --ekm-red-fg: #f3aaa3;
    --ekm-red-dot: #e5756d;
    --ekm-on-accent: #0e1512;
  }

  *,
  *::before,
  *::after {
    box-sizing: border-box;
  }

  button {
    color: inherit;
    cursor: pointer;
    font: inherit;
  }

  button:disabled {
    cursor: default;
    opacity: 0.45;
  }

  a {
    color: inherit;
    text-decoration: none;
  }

  :focus-visible {
    outline: 2px solid var(--ekm-ac);
    outline-offset: 2px;
  }

  .shell {
    animation: fadein 0.18s ease-out;
    padding: 24px;
  }

  .shell.busy .content {
    opacity: 0.6;
    transition: opacity 0.15s 0.12s;
  }

  .visually-hidden {
    clip: rect(0, 0, 0, 0);
    height: 1px;
    margin: -1px;
    overflow: hidden;
    position: absolute;
    width: 1px;
  }

  /* Breadcrumb */
  .crumbs {
    align-items: center;
    display: flex;
    flex-wrap: wrap;
    font: 400 14px/1.2 Lato, sans-serif;
    gap: 4px 10px;
    margin-bottom: 14px;
  }

  .crumb {
    align-items: center;
    display: inline-flex;
    gap: 8px;
    position: relative;
  }

  .crumb-link {
    background: none;
    border: 0;
    color: var(--ekm-ac);
    padding: 4px 0;
  }

  .crumb-link.root {
    color: var(--ekm-tx);
  }

  .crumb-link.current {
    color: var(--ekm-tx);
    font-weight: 700;
  }

  .crumb-link:hover:not(.current) {
    text-decoration: underline;
  }

  .crumb-caret {
    background: none;
    border: 0;
    border-radius: 4px;
    color: var(--ekm-mu);
    font-size: 10px;
    line-height: 1;
    padding: 5px 4px;
  }

  .crumb-caret:hover {
    color: var(--ekm-tx);
  }

  .sep {
    color: var(--ekm-mu);
    opacity: 0.45;
  }

  .menu {
    animation: fadein 0.12s ease-out;
    background: var(--ekm-sf);
    border: 1px solid var(--ekm-bd);
    border-radius: 8px;
    box-shadow: 0 10px 28px var(--ekm-shadow);
    padding: 6px;
    position: absolute;
    top: calc(100% + 6px);
    z-index: 30;
  }

  .crumb-menu {
    left: 0;
    max-height: 360px;
    overflow-y: auto;
    width: 280px;
  }

  .menu-head {
    color: var(--ekm-mu);
    font: 700 11px/1 var(--ekm-mono);
    letter-spacing: 0.12em;
    overflow: hidden;
    padding: 8px 10px 6px;
    text-overflow: ellipsis;
    text-transform: uppercase;
    white-space: nowrap;
  }

  .menu-item {
    align-items: center;
    background: none;
    border: 0;
    border-radius: 6px;
    display: flex;
    font: 400 14px/1.2 Lato, sans-serif;
    gap: 9px;
    padding: 8px 10px;
    text-align: left;
    width: 100%;
  }

  .menu-item:hover {
    background: var(--ekm-sf2);
  }

  .menu-item svg {
    color: var(--ekm-mu);
    flex: 0 0 auto;
  }

  .menu-item .grow {
    flex: 1 1 auto;
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .menu-item .count {
    color: var(--ekm-mu);
    font: 500 12px/1 var(--ekm-mono);
  }

  .menu-item .tick {
    color: var(--ekm-ac);
    font-weight: 700;
    width: 12px;
  }

  .menu-item.active {
    font-weight: 700;
  }

  /* Header */
  .header {
    align-items: center;
    display: flex;
    flex-wrap: wrap;
    gap: 14px 16px;
    margin-bottom: 16px;
  }

  .back {
    align-items: center;
    background: var(--ekm-sf);
    border: 1px solid var(--ekm-bd);
    border-radius: 8px;
    display: inline-flex;
    flex: 0 0 auto;
    font-size: 18px;
    height: 44px;
    justify-content: center;
    width: 44px;
  }

  .back:hover:not(:disabled) {
    border-color: var(--ekm-bd-hover);
  }

  .heading {
    flex: 1 1 auto;
    min-width: 0;
  }

  h1 {
    font: 900 26px/1.15 Lato, sans-serif;
    letter-spacing: -0.01em;
    margin: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .meta {
    color: var(--ekm-mu);
    font: 400 14px/1.3 Lato, sans-serif;
    margin: 3px 0 0;
  }

  .tools {
    align-items: center;
    display: flex;
    gap: 10px;
  }

  .sort {
    position: relative;
  }

  .control {
    align-items: center;
    background: var(--ekm-sf);
    border: 1px solid var(--ekm-bd);
    border-radius: 8px;
    display: inline-flex;
    font: 400 14px/1 Lato, sans-serif;
    gap: 10px;
    height: 36px;
    padding: 0 12px;
    white-space: nowrap;
  }

  .control:hover {
    border-color: var(--ekm-bd-hover);
  }

  .control.open {
    border-color: var(--ekm-ac);
  }

  .control .caret {
    color: var(--ekm-mu);
    font-size: 9px;
  }

  .sort-menu {
    right: 0;
    width: 220px;
  }

  /* Checkboxes */
  .box {
    align-items: center;
    background: var(--ekm-sf);
    border: 1px solid var(--ekm-bd);
    border-radius: 4px;
    color: var(--ekm-on-accent);
    display: inline-flex;
    flex: 0 0 auto;
    font: 700 10px/1 Lato, sans-serif;
    height: 16px;
    justify-content: center;
    padding: 0;
    width: 16px;
  }

  .box.on {
    background: var(--ekm-ac);
    border-color: var(--ekm-ac);
  }

  /* Filter chips */
  .filters {
    align-items: center;
    display: flex;
    flex-wrap: wrap;
    gap: 8px;
    margin-bottom: 18px;
  }

  .chip {
    align-items: center;
    background: var(--ekm-sf);
    border: 1px solid var(--ekm-bd);
    border-radius: 20px;
    color: var(--ekm-tx);
    display: inline-flex;
    font: 700 13px/1 Lato, sans-serif;
    gap: 7px;
    height: 30px;
    padding: 0 11px;
  }

  .chip:hover {
    border-color: var(--ekm-bd-hover);
  }

  .chip .count {
    color: var(--ekm-mu);
    font: 500 12px/1 var(--ekm-mono);
  }

  .chip.on {
    background: var(--ekm-acs);
    border-color: var(--ekm-ac);
    color: var(--ekm-ac);
  }

  .chip.on .count {
    color: var(--ekm-ac);
  }

  .showing {
    color: var(--ekm-mu);
    font: 400 13px/1 Lato, sans-serif;
    margin-left: 4px;
  }

  /* Sections */
  .section {
    margin-top: 20px;
  }

  .section:first-of-type {
    margin-top: 0;
  }

  h2 {
    color: var(--ekm-mu);
    font: 700 11px/1 var(--ekm-mono);
    letter-spacing: 0.12em;
    margin: 0 0 10px;
    text-transform: uppercase;
  }

  h2 span {
    color: var(--ekm-tx);
    margin-left: 6px;
  }

  /* Subcategory cards */
  .subcats {
    display: flex;
    flex-wrap: wrap;
    gap: 10px;
  }

  .subcat {
    align-items: center;
    background: var(--ekm-sf);
    border: 1px solid var(--ekm-bd);
    border-radius: 10px;
    cursor: pointer;
    display: flex;
    flex: 0 0 auto;
    gap: 12px;
    max-width: 100%;
    min-width: 0;
    padding: 10px 14px 10px 10px;
  }

  .subcat:hover {
    background: var(--ekm-sf2);
    border-color: var(--ekm-bd-hover);
  }

  .subcat.selected {
    background: var(--ekm-acs);
    border-color: var(--ekm-ac);
  }

  .subcat-icon {
    align-items: center;
    background: var(--ekm-sf2);
    border: 1px solid var(--ekm-bd2);
    border-radius: 7px;
    color: var(--ekm-mu);
    display: inline-flex;
    flex: 0 0 auto;
    height: 34px;
    justify-content: center;
    width: 34px;
  }

  .subcat-text {
    display: grid;
    gap: 4px;
    min-width: 0;
  }

  .subcat-name {
    align-items: center;
    display: flex;
    font: 700 14px/1.2 Lato, sans-serif;
    gap: 7px;
  }

  .dot {
    border-radius: 50%;
    flex: 0 0 auto;
    height: 7px;
    width: 7px;
  }

  .dot.green {
    background: var(--ekm-green-dot);
  }

  .dot.grey {
    background: var(--ekm-grey-dot);
  }

  .subcat-meta {
    color: var(--ekm-mu);
    font: 400 12px/1.2 Lato, sans-serif;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .subcat-meta .green {
    color: var(--ekm-ac);
  }

  .subcat-meta .amber {
    color: var(--ekm-amber-fg);
    font-weight: 700;
  }

  .chevron {
    color: var(--ekm-mu);
    margin-left: 6px;
  }

  /* Product grid */
  .grid {
    display: grid;
    gap: 14px;
    grid-template-columns: repeat(auto-fill, minmax(220px, 1fr));
  }

  .card {
    background: var(--ekm-sf);
    border: 1px solid var(--ekm-bd);
    border-radius: 10px;
    position: relative;
  }

  .card:hover {
    border-color: var(--ekm-bd-hover);
  }

  .card.selected {
    border-color: var(--ekm-ac);
    box-shadow: 0 0 0 2px var(--ekm-acs);
  }

  .card .box {
    border-radius: 5px;
    box-shadow: 0 1px 3px var(--ekm-shadow);
    font-size: 11px;
    height: 22px;
    left: 10px;
    position: absolute;
    top: 10px;
    width: 22px;
    z-index: 2;
  }

  .card-actions {
    background: var(--ekm-sf);
    border: 1px solid var(--ekm-bd);
    border-radius: 5px;
    box-shadow: 0 1px 3px var(--ekm-shadow);
    position: absolute;
    right: 10px;
    top: 10px;
    z-index: 2;
    --uui-button-height: 22px;
    --uui-button-padding-left-factor: 0.8;
    --uui-button-padding-right-factor: 0.8;
    --uui-button-font-size: 12px;
  }

  .card-actions:empty {
    display: none;
  }

  .tile {
    align-items: center;
    aspect-ratio: 1 / 1;
    background: var(--ekm-tile);
    border-bottom: 1px solid var(--ekm-bd2);
    border-radius: 10px 10px 0 0;
    color: var(--ekm-mu);
    display: flex;
    justify-content: center;
    overflow: hidden;
    padding: 14px;
  }

  .tile img {
    display: block;
    height: 100%;
    object-fit: contain;
    object-position: center;
    width: 100%;
  }

  .tile svg {
    opacity: 0.6;
  }

  .card-body {
    display: flex;
    flex-direction: column;
    gap: 8px;
    padding: 12px 14px 14px;
  }

  .name {
    -webkit-box-orient: vertical;
    -webkit-line-clamp: 2;
    display: -webkit-box;
    font: 700 14px/1.3 Lato, sans-serif;
    line-clamp: 2;
    min-height: 36px;
    overflow: hidden;
  }

  .name:hover {
    text-decoration: underline;
  }

  .row {
    align-items: baseline;
    display: flex;
    gap: 8px;
    justify-content: space-between;
  }

  .sku {
    color: var(--ekm-mu);
    font: 400 12px/1.2 var(--ekm-mono);
    min-width: 0;
    overflow: hidden;
    text-overflow: ellipsis;
    white-space: nowrap;
  }

  .prices {
    align-items: baseline;
    display: inline-flex;
    flex: 0 0 auto;
    gap: 6px;
  }

  .price {
    font: 700 14px/1 Lato, sans-serif;
  }

  .was {
    color: var(--ekm-mu);
    font: 400 12px/1 Lato, sans-serif;
    text-decoration: line-through;
  }

  .variants {
    color: var(--ekm-mu);
    font: 400 12px/1 Lato, sans-serif;
  }

  .foot {
    align-items: center;
    display: flex;
    flex-wrap: wrap;
    gap: 6px 8px;
    justify-content: space-between;
    margin-top: 2px;
  }

  .badges {
    align-items: center;
    display: inline-flex;
    flex-wrap: wrap;
    gap: 6px;
  }

  .badge {
    align-items: center;
    border-radius: 20px;
    display: inline-flex;
    font: 700 11px/1.3 Lato, sans-serif;
    gap: 6px;
    padding: 3px 8px;
  }

  .badge i {
    border-radius: 50%;
    height: 6px;
    width: 6px;
  }

  .badge.green {
    background: var(--ekm-green-bg);
    color: var(--ekm-green-fg);
  }

  .badge.green i {
    background: var(--ekm-green-dot);
  }

  .badge.amber {
    background: var(--ekm-amber-bg);
    color: var(--ekm-amber-fg);
  }

  .badge.amber i {
    background: var(--ekm-amber-dot);
  }

  .badge.grey {
    background: var(--ekm-grey-bg);
    color: var(--ekm-grey-fg);
  }

  .badge.edits {
    padding: 3px 7px;
  }

  .stock {
    font: 700 12px/1 Lato, sans-serif;
  }

  .stock.green {
    color: var(--ekm-ac);
  }

  .stock.amber {
    color: var(--ekm-amber-fg);
  }

  .stock.red {
    color: var(--ekm-red-dot);
  }

  /* States */
  .empty {
    border: 1px dashed var(--ekm-bd);
    border-radius: 10px;
    color: var(--ekm-mu);
    display: grid;
    font: 400 14px/1.4 Lato, sans-serif;
    gap: 8px;
    justify-items: center;
    padding: 44px 24px;
    text-align: center;
  }

  .empty strong {
    color: var(--ekm-tx);
    font: 700 15px/1.3 Lato, sans-serif;
  }

  .empty .actions {
    display: flex;
    gap: 8px;
    margin-top: 6px;
  }

  .empty.error strong {
    color: var(--ekm-red-dot);
  }

  .skeleton {
    animation: shim 1.2s linear infinite;
    background: linear-gradient(90deg, var(--ekm-bd2) 0%, var(--ekm-sf2) 40%, var(--ekm-bd2) 80%);
    background-size: 320px 100%;
    border-radius: 6px;
    display: block;
  }

  .skeleton-card {
    background: var(--ekm-sf);
    border: 1px solid var(--ekm-bd);
    border-radius: 10px;
    overflow: hidden;
  }

  .skeleton-card .skeleton.image {
    aspect-ratio: 1 / 1;
    border-radius: 0;
  }

  .skeleton-card .lines {
    display: grid;
    gap: 10px;
    padding: 12px 14px 14px;
  }

  /* Pagination */
  .pagination {
    align-items: center;
    color: var(--ekm-mu);
    display: flex;
    flex-wrap: wrap;
    font: 400 13px/1 Lato, sans-serif;
    gap: 16px;
    justify-content: center;
    margin-top: 24px;
  }

  .pagination div {
    align-items: center;
    display: flex;
    gap: 6px;
  }

  .pagination button {
    background: var(--ekm-sf);
    border: 1px solid var(--ekm-bd);
    border-radius: 8px;
    font: 700 13px/1 Lato, sans-serif;
    height: 34px;
    min-width: 34px;
    padding: 0 10px;
  }

  .pagination button:hover:not(:disabled) {
    border-color: var(--ekm-bd-hover);
  }

  .pagination .current {
    background: var(--ekm-acs);
    border-color: var(--ekm-ac);
    color: var(--ekm-ac);
  }

  @keyframes fadein {
    from {
      opacity: 0;
    }
    to {
      opacity: 1;
    }
  }

  @keyframes shim {
    from {
      background-position: -320px 0;
    }
    to {
      background-position: 320px 0;
    }
  }

  @media (max-width: 1100px) {
    .shell {
      padding: 16px;
    }

    h1 {
      font-size: 22px;
    }

    .grid {
      gap: 10px;
      grid-template-columns: repeat(auto-fill, minmax(170px, 1fr));
    }

    .subcat {
      flex: 1 1 220px;
    }
  }

  @media (prefers-reduced-motion: reduce) {
    .shell,
    .menu,
    .skeleton {
      animation: none;
    }
  }
`, f = "sortOrderAsc", h = [
  { value: "sortOrderAsc", label: "Sort order asc" },
  { value: "sortOrderDesc", label: "Sort order desc" },
  { value: "nameAsc", label: "Name A–Z" },
  { value: "nameDesc", label: "Name Z–A" },
  { value: "createdDesc", label: "Created newest" },
  { value: "createdAsc", label: "Created oldest" },
  { value: "updatedDesc", label: "Updated newest" },
  { value: "updatedAsc", label: "Updated oldest" }
], k = [
  { value: "all", label: "All", title: "All products in this category", count: "all" },
  { value: "published", label: "Published", title: "Includes products with pending edits", count: "published" },
  { value: "unpublished", label: "Unpublished", title: "Draft and unpublished products", count: "unpublished" },
  { value: "oos", label: "Out of stock", title: "No stock on any variant", count: "outOfStock" }
], _ = 5;
function T(r) {
  return h.some((a) => a.value === r);
}
function j(r) {
  var a;
  return ((a = h.find((e) => e.value === r)) == null ? void 0 : a.label) ?? h[0].label;
}
function K(r) {
  return r === "umb-dark-theme" ? "dark" : "light";
}
function m(r, a, e) {
  return `${r} ${r === 1 ? a : e}`;
}
function v(r, a) {
  return `${m(r, "product", "products")} · ${m(a, "subcategory", "subcategories")}`;
}
function V(r) {
  if (!(r > 0))
    return { text: "Out of stock", tone: "red" };
  const a = Q(r);
  return r < _ ? { text: `${a} left`, tone: "amber" } : { text: `${a} in stock`, tone: "green" };
}
function B(r) {
  return r.totalProductCount === 0 ? { text: "Empty", tone: "amber" } : r.liveCount === 0 ? { text: "Nothing published", tone: "amber" } : r.outOfStockCount > 0 ? { text: `${r.outOfStockCount} out of stock`, tone: "muted" } : { text: `${r.liveCount} live`, tone: "green" };
}
function Y(r) {
  return r.published ? { text: "Published", tone: "green" } : { text: "Draft", tone: "amber" };
}
function W(r, a) {
  return r.length > 0 && r.every((e) => a.includes(e)) ? a.filter((e) => !r.includes(e)) : [.../* @__PURE__ */ new Set([...a, ...r])];
}
function G(r, a) {
  if (a <= 7)
    return Array.from({ length: a }, (s, o) => o + 1);
  const e = [.../* @__PURE__ */ new Set([1, a, r - 1, r, r + 1])].filter((s) => s >= 1 && s <= a).sort((s, o) => s - o), t = [];
  for (const s of e) {
    const o = t[t.length - 1];
    typeof o == "number" && s - o > 1 && t.push("…"), t.push(s);
  }
  return t;
}
function Q(r) {
  return Number.isInteger(r) ? String(r) : String(Math.round(r * 100) / 100);
}
const X = 16, x = "ekomCatalogNode", Z = 4, J = "(max-width: 1100px)", ee = "ekomCatalogParent:", I = "ekomCatalogSort", g = "ekomCatalog", y = 480, te = "document", se = "umb-collection-default, umb-document-collection", w = C`<svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M3 7a2 2 0 0 1 2-2h4l2 2.5h8a2 2 0 0 1 2 2V17a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2z"/></svg>`, oe = C`<svg width="44" height="44" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.5" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><rect x="3" y="3" width="18" height="18" rx="2.5"/><circle cx="9" cy="9" r="1.8"/><path d="m21 15-4.6-4.6a1.5 1.5 0 0 0-2.1 0L5 20"/></svg>`;
class re extends P {
  constructor() {
    super(...arguments);
    l(this, "entityContext", new U(this));
  }
  set unique(e) {
    this.entityContext.setEntityType(te), this.entityContext.setUnique(e);
  }
  render() {
    return i`<slot></slot>`;
  }
}
class L extends P {
  constructor() {
    super();
    l(this, "pickerMode", !1);
    l(this, "initialized", !1);
    l(this, "pickerView");
    l(this, "nodeId", "");
    l(this, "query", "");
    l(this, "sort", S());
    l(this, "status", "all");
    l(this, "page", 1);
    l(this, "pageSize", X);
    l(this, "loading", !0);
    l(this, "busy", !1);
    l(this, "error", "");
    l(this, "data");
    l(this, "openMenu");
    l(this, "selected", /* @__PURE__ */ new Set());
    l(this, "requestId", 0);
    l(this, "request");
    l(this, "revealTimer");
    l(this, "resizeTimer");
    l(this, "collectionPaginationObserver");
    l(this, "selection");
    l(this, "imaging", new R(this));
    l(this, "thumbnails", /* @__PURE__ */ new Map());
    l(this, "onPopState", () => this.restoreNodeFromHistory());
    l(this, "onDocumentClick", () => this.setOpenMenu(void 0));
    l(this, "onKeyDown", (e) => {
      e.key === "Escape" && this.setOpenMenu(void 0);
    });
    l(this, "onResize", () => {
      this.resizeTimer != null && window.clearTimeout(this.resizeTimer), this.resizeTimer = window.setTimeout(() => {
        this.updatePageSizeForViewport() && this.nodeId && this.data != null && !this.loading && this.load(!1);
      }, 150);
    });
    this.consumeContext(H, (e) => {
      this.observe(e == null ? void 0 : e.theme, (t) => this.setAttribute("data-theme", K(t)), "ekomCatalogTheme");
    });
  }
  connectedCallback() {
    super.connectedCallback(), this.style.setProperty("display", "block", "important"), this.style.setProperty("visibility", "visible", "important"), this.consumeContext(M, (e) => {
      var s;
      if (e == null) return;
      if (this.pickerMode = F(e.getConfig()), this.pickerMode) {
        this.initializePickerView(), this.requestUpdate();
        return;
      }
      this.initialized || (this.initialized = !0, window.addEventListener("popstate", this.onPopState), window.addEventListener("resize", this.onResize), document.addEventListener("click", this.onDocumentClick), document.addEventListener("keydown", this.onKeyDown), this.revealCollectionHost(), this.revealTimer = window.setTimeout(() => this.revealCollectionHost(), 250), this.nodeId = this.getNodeIdFromUrl(), this.sort = S(), this.restorePageFromHistory());
      const t = (s = e == null ? void 0 : e.getConfig()) == null ? void 0 : s.unique;
      this.getCatalogNodeFromUrl() == null && t != null && t !== this.nodeId && (this.nodeId = t, this.restorePageFromHistory()), (e == null ? void 0 : e.selection) != null && (this.selection = e.selection, this.selection.setSelectable(!0), this.selection.setMultiple(!0), this.observe(e.selection.selection, (o) => {
        this.selected = new Set((o ?? []).filter((n) => typeof n == "string")), this.requestUpdate();
      }, "ekomCatalogCollectionSelection")), this.observe(e == null ? void 0 : e.filter, (o) => this.setQueryFromCollectionFilter(o), "ekomCatalogCollectionFilter"), this.requestUpdate(), this.nodeId && this.load();
    });
  }
  initializePickerView() {
    this.pickerView == null && (this.pickerView = new q(
      this,
      N,
      "Umb.CollectionView.Document.Table",
      (e, t) => {
        e && t.component != null ? this.replaceChildren(t.component) : this.textContent = "The Umbraco document table view is unavailable.";
      }
    ));
  }
  disconnectedCallback() {
    var e, t, s;
    (e = this.pickerView) == null || e.destroy(), this.pickerView = void 0, this.initialized = !1, (t = this.request) == null || t.abort(), window.removeEventListener("popstate", this.onPopState), window.removeEventListener("resize", this.onResize), document.removeEventListener("click", this.onDocumentClick), document.removeEventListener("keydown", this.onKeyDown), this.revealTimer != null && window.clearTimeout(this.revealTimer), this.resizeTimer != null && window.clearTimeout(this.resizeTimer), (s = this.collectionPaginationObserver) == null || s.disconnect(), this.collectionPaginationObserver = void 0, super.disconnectedCallback();
  }
  async load(e = !0) {
    var o;
    if (this.pickerMode) return;
    if (!this.nodeId) {
      this.loading = !1, this.error = "Unable to determine the current catalog node.", this.requestUpdate();
      return;
    }
    (o = this.request) == null || o.abort();
    const t = new AbortController(), s = ++this.requestId;
    this.request = t, this.loading = e, this.busy = !e, this.error = "", this.updatePageSizeForViewport(), this.requestUpdate();
    try {
      const n = new URLSearchParams({
        query: this.query,
        sort: this.sort,
        status: this.status,
        page: String(this.page),
        pageSize: String(this.pageSize)
      }), d = await this.fetchJson(`/ekom/backoffice/CatalogCollection/${encodeURIComponent(this.nodeId)}?${n}`, t.signal);
      if (s !== this.requestId) return;
      this.data = d, this.page = d.page, this.pageSize = d.pageSize, this.storeParentNode(d), this.loadThumbnails(d.products);
    } catch (n) {
      if (s !== this.requestId || t.signal.aborted || n instanceof $ && n.status === 404 && this.redirectToParent())
        return;
      this.error = n instanceof Error ? n.message : "Unable to load catalog.";
    }
    this.loading = !1, this.busy = !1, this.requestUpdate();
  }
  /** One request for every image on the page instead of one per card. */
  async loadThumbnails(e) {
    const t = [...new Set(e.map((s) => s.image).filter((s) => s && !this.thumbnails.has(s)))];
    if (t.length !== 0) {
      try {
        const { data: s } = await this.imaging.requestResizedItems(t, {
          width: y,
          height: y,
          mode: "Max"
        });
        for (const o of s ?? [])
          o.url && this.thumbnails.set(o.unique, o.url);
      } catch {
      }
      this.requestUpdate();
    }
  }
  drillTo(e) {
    var o;
    const t = e.key || String(e.id), s = history.state != null && typeof history.state == "object" ? history.state : {};
    history.pushState({
      ...s,
      [g]: {
        nodeId: t,
        page: 1
      }
    }, "", this.getDocumentHref(t)), this.nodeId = t, this.query = "", this.status = "all", this.page = 1, (o = this.selection) == null || o.clearSelection(), this.load();
  }
  restoreNodeFromHistory() {
    var t;
    const e = this.getNodeIdFromUrl();
    e && (this.nodeId = e, this.query = "", this.status = "all", this.page = 1, this.restorePageFromHistory(), (t = this.selection) == null || t.clearSelection(), this.load());
  }
  storeParentNode(e) {
    var s;
    const t = this.getParentStorageKey(e.current.key || String(e.current.id));
    (s = e.parent) != null && s.key ? window.sessionStorage.setItem(t, e.parent.key) : window.sessionStorage.removeItem(t);
  }
  redirectToParent() {
    var t, s;
    const e = ((s = (t = this.data) == null ? void 0 : t.parent) == null ? void 0 : s.key) ?? window.sessionStorage.getItem(this.getParentStorageKey(this.nodeId));
    return e ? (window.location.replace(this.getDocumentHref(e)), !0) : !1;
  }
  getParentStorageKey(e) {
    return `${ee}${e}`;
  }
  restorePageFromHistory() {
    const e = history.state, t = e == null ? void 0 : e[g];
    if (t == null || typeof t != "object")
      return;
    const { nodeId: s, page: o } = t;
    s === this.nodeId && typeof o == "number" && Number.isInteger(o) && o > 0 && (this.page = o);
  }
  storePageInHistory() {
    if (!this.nodeId)
      return;
    const e = history.state != null && typeof history.state == "object" ? history.state : {};
    history.replaceState({
      ...e,
      [g]: {
        nodeId: this.nodeId,
        page: this.page
      }
    }, "", window.location.href);
  }
  setQueryFromCollectionFilter(e) {
    const t = "filter" in (e ?? {}) ? e.filter : void 0, s = typeof t == "string" ? t : "";
    s !== this.query && (this.query = s, this.page = 1, this.storePageInHistory(), this.nodeId && this.load(this.data == null));
  }
  setSort(e) {
    this.setOpenMenu(void 0), T(e) && (ie(e), e !== this.sort && (this.sort = e, this.page = 1, this.storePageInHistory(), this.load(!1)));
  }
  setStatus(e) {
    e !== this.status && (this.status = e, this.page = 1, this.storePageInHistory(), this.load(!1));
  }
  setPage(e) {
    e < 1 || e === this.page || this.data != null && e > this.data.totalPages || (this.page = e, this.storePageInHistory(), this.load(!1));
  }
  setOpenMenu(e) {
    e !== this.openMenu && (this.openMenu = e, this.requestUpdate());
  }
  toggleMenu(e, t) {
    e.stopPropagation(), this.setOpenMenu(this.openMenu === t ? void 0 : t);
  }
  updatePageSizeForViewport() {
    const e = window.matchMedia(J).matches, t = e ? 10 : 14, s = e ? 170 : 220, o = Math.max(0, this.clientWidth - (e ? 32 : 48));
    if (o === 0)
      return !1;
    const d = Math.max(1, Math.floor((o + t) / (s + t))) * Z;
    return d === this.pageSize ? !1 : (this.pageSize = d, !0);
  }
  visibleKeys() {
    const e = this.data;
    return e == null ? [] : [...(this.query ? [] : e.subcategories).map((s) => s.key), ...e.products.map((s) => s.key)];
  }
  toggleKey(e, t) {
    var s;
    e.preventDefault(), e.stopPropagation(), (s = this.selection) == null || s.toggleSelect(t);
  }
  toggleVisible() {
    this.selection != null && this.selection.setSelection(W(this.visibleKeys(), this.selection.getSelection()));
  }
  clearFilter() {
    this.setStatus("all");
  }
  revealCollectionHost() {
    if (!this.pickerMode) {
      for (const e of this.getComposedAncestors(se)) {
        const t = e.shadowRoot, s = t == null ? void 0 : t.querySelector("#router"), o = t == null ? void 0 : t.querySelector("umb-body-layout"), n = t == null ? void 0 : t.querySelector("#empty-state");
        s == null || s.style.setProperty("visibility", "visible", "important"), n == null || n.style.setProperty("display", "none", "important"), o == null || o.classList.add("has-items");
      }
      for (const e of this.getComposedShadowRoots())
        this.hideDefaultCollectionPagination(e), this.observeDefaultCollectionPagination(e);
    }
  }
  hideDefaultCollectionPagination(e) {
    var t;
    (t = e.querySelector("umb-collection-pagination")) == null || t.style.setProperty("display", "none", "important");
  }
  observeDefaultCollectionPagination(e) {
    this.collectionPaginationObserver == null && (this.collectionPaginationObserver = new MutationObserver(() => this.revealCollectionHost())), this.collectionPaginationObserver.observe(e, { childList: !0, subtree: !0 });
  }
  getComposedAncestors(e) {
    const t = [];
    let s = this;
    for (; s != null; )
      s instanceof HTMLElement && s.matches(e) && t.push(s), s = s.parentNode ?? (s.getRootNode() instanceof ShadowRoot ? s.getRootNode().host : null);
    return t;
  }
  getComposedShadowRoots() {
    const e = /* @__PURE__ */ new Set();
    let t = this;
    for (; t != null; ) {
      const s = t.getRootNode();
      s instanceof ShadowRoot && e.add(s), t = t.parentNode ?? (s instanceof ShadowRoot ? s.host : null);
    }
    return [...e];
  }
  updated() {
    this.pickerMode || this.revealCollectionHost();
  }
  render() {
    return this.pickerMode ? i`<slot></slot>` : i`<div class="shell ${this.busy ? "busy" : ""}" aria-busy=${this.loading || this.busy ? "true" : "false"}>${this.renderContent()}</div>`;
  }
  renderContent() {
    if (this.loading)
      return this.renderLoading();
    const e = this.data;
    if (this.error || e == null)
      return i`
        <div class="empty error" role="alert">
          <strong>Could not load the catalog</strong>
          <span>${this.error || "No catalog data."}</span>
          <div class="actions">
            ${(e == null ? void 0 : e.parent) != null ? i`<button type="button" class="control" @click=${() => this.drillTo(e.parent)}>Back</button>` : c}
            <button type="button" class="control" @click=${() => void this.load()}>Retry</button>
          </div>
        </div>
      `;
    const t = !this.query && e.subcategories.length > 0;
    return i`
      ${this.renderBreadcrumbs(e)}
      ${this.renderHeader(e)}
      <div class="content">
        ${e.productCount > 0 ? this.renderFilters(e) : c}
        ${t ? this.renderSubcategories(e.subcategories) : c}
        ${this.renderProducts(e)}
        ${this.renderPagination(e)}
      </div>
    `;
  }
  renderLoading() {
    return i`
      <div role="status" aria-label="Loading catalog">
        <span class="skeleton" style="height:14px;width:220px;margin-bottom:18px"></span>
        <div class="header">
          <span class="skeleton" style="height:44px;width:44px;border-radius:8px"></span>
          <div class="heading">
            <span class="skeleton" style="height:28px;width:200px"></span>
            <span class="skeleton" style="height:14px;width:160px;margin-top:8px"></span>
          </div>
        </div>
        <section class="section">
          <span class="skeleton" style="height:11px;width:120px;margin-bottom:10px"></span>
          <div class="subcats">
            ${[0, 1, 2, 3].map(() => i`<span class="skeleton" style="height:56px;width:230px;border-radius:10px"></span>`)}
          </div>
        </section>
        <section class="section">
          <span class="skeleton" style="height:11px;width:100px;margin-bottom:10px"></span>
          <div class="grid">
            ${Array.from({ length: 12 }, () => i`
              <div class="skeleton-card">
                <span class="skeleton image"></span>
                <div class="lines">
                  <span class="skeleton" style="height:16px;width:76%"></span>
                  <span class="skeleton" style="height:12px;width:58%"></span>
                  <span class="skeleton" style="height:22px;width:88%"></span>
                </div>
              </div>
            `)}
          </div>
        </section>
        <span class="visually-hidden">Loading catalog…</span>
      </div>
    `;
  }
  renderBreadcrumbs(e) {
    if (e.breadcrumbs.length <= 1)
      return c;
    const t = e.breadcrumbs.length - 1;
    return i`
      <nav class="crumbs" aria-label="Catalog path">
        ${e.breadcrumbs.map((s, o) => i`
          ${o > 0 ? i`<span class="sep" aria-hidden="true">/</span>` : c}
          ${this.renderBreadcrumb(s, o, t, e.breadcrumbs[o - 1])}
        `)}
      </nav>
    `;
  }
  renderBreadcrumb(e, t, s, o) {
    const n = t === s, d = `crumb:${e.key}`, p = this.openMenu === d, E = e.contentTypeAlias === "ekmCategory" && e.siblings.length > 1;
    return i`
      <span class="crumb">
        <button
          type="button"
          class="crumb-link ${n ? "current" : ""} ${t === 0 ? "root" : ""}"
          aria-current=${n ? "page" : c}
          @click=${() => n ? void 0 : this.drillTo(e)}>${e.name}</button>
        ${E ? i`
          <button
            type="button"
            class="crumb-caret"
            aria-haspopup="menu"
            aria-expanded=${p ? "true" : "false"}
            aria-label="Show sibling categories for ${e.name}"
            @click=${(u) => this.toggleMenu(u, d)}>${p ? "▲" : "▼"}</button>
          ${p ? i`
            <div class="menu crumb-menu" role="menu">
              ${o != null ? i`<div class="menu-head">In ${o.name}</div>` : c}
              ${e.siblings.map((u) => i`
                <button
                  type="button"
                  role="menuitem"
                  class="menu-item ${u.key === e.key ? "active" : ""}"
                  @click=${() => u.key === e.key ? this.setOpenMenu(void 0) : this.drillTo(u)}>
                  ${w}
                  <span class="grow">${u.name}</span>
                  <span class="count">${u.totalProductCount}</span>
                  <span class="tick">${u.key === e.key ? "✓" : ""}</span>
                </button>
              `)}
            </div>
          ` : c}
        ` : c}
      </span>
    `;
  }
  renderHeader(e) {
    const t = this.visibleKeys(), s = t.length > 0 && t.every((n) => this.selected.has(n)), o = this.openMenu === "sort";
    return i`
      <header class="header">
        ${e.parent != null ? i`<button type="button" class="back" aria-label="Back to ${e.parent.name}" @click=${() => this.drillTo(e.parent)}>←</button>` : c}
        <div class="heading">
          <h1>${e.current.name}</h1>
          <p class="meta">${v(e.current.totalProductCount, e.subcategoryCount)}</p>
        </div>
        <div class="tools">
          <div class="sort">
            <button
              type="button"
              class="control ${o ? "open" : ""}"
              aria-haspopup="menu"
              aria-expanded=${o ? "true" : "false"}
              @click=${(n) => this.toggleMenu(n, "sort")}>${j(this.sort)}<span class="caret" aria-hidden="true">▼</span></button>
            ${o ? i`
              <div class="menu sort-menu" role="menu">
                ${h.map((n) => i`
                  <button type="button" role="menuitemradio" aria-checked=${n.value === this.sort ? "true" : "false"} class="menu-item ${n.value === this.sort ? "active" : ""}" @click=${() => this.setSort(n.value)}>
                    <span class="grow">${n.label}</span>
                    <span class="tick">${n.value === this.sort ? "✓" : ""}</span>
                  </button>
                `)}
              </div>
            ` : c}
          </div>
          <button type="button" class="control" role="checkbox" aria-checked=${s ? "true" : "false"} ?disabled=${t.length === 0} @click=${() => this.toggleVisible()}>
            <span class="box ${s ? "on" : ""}" aria-hidden="true">${s ? "✓" : ""}</span>Select all
          </button>
        </div>
      </header>
    `;
  }
  renderFilters(e) {
    return i`
      <div class="filters">
        ${k.map((t) => i`
          <button type="button" class="chip ${this.status === t.value ? "on" : ""}" title=${t.title} aria-pressed=${this.status === t.value ? "true" : "false"} @click=${() => this.setStatus(t.value)}>
            ${t.label}<span class="count">${e.statusCounts[t.count]}</span>
          </button>
        `)}
        ${this.status !== "all" ? i`<span class="showing">Showing ${e.filteredProductCount} of ${e.statusCounts.all}</span>` : c}
      </div>
    `;
  }
  renderSubcategories(e) {
    return i`
      <section class="section">
        <h2>Subcategories<span>${e.length}</span></h2>
        <div class="subcats">
          ${b(e, (t) => t.key, (t) => this.renderSubcategory(t))}
        </div>
      </section>
    `;
  }
  renderSubcategory(e) {
    const t = this.selected.has(e.key), s = B(e);
    return i`
      <a class="subcat ${t ? "selected" : ""}" href=${this.getDocumentHref(e.key)} @click=${(o) => {
      o.preventDefault(), this.drillTo(e);
    }}>
        <button type="button" class="box ${t ? "on" : ""}" role="checkbox" aria-checked=${t ? "true" : "false"} aria-label="Select ${e.name}" @click=${(o) => this.toggleKey(o, e.key)}>${t ? "✓" : ""}</button>
        <span class="subcat-icon">${w}</span>
        <span class="subcat-text">
          <span class="subcat-name">${e.name}<span class="dot ${e.published ? "green" : "grey"}" title=${e.published ? "Category is published" : "Category is not published"}></span></span>
          <span class="subcat-meta">${v(e.totalProductCount, e.subcategoryCount)} · <span class=${s.tone}>${s.text}</span></span>
        </span>
        <span class="chevron" aria-hidden="true">›</span>
      </a>
    `;
  }
  renderProducts(e) {
    return i`
      <section class="section">
        <h2>Products<span>${e.filteredProductCount}</span></h2>
        ${e.products.length === 0 ? this.renderEmptyProducts(e) : i`<div class="grid">${b(e.products, (t) => t.key, (t) => this.renderProduct(t))}</div>`}
      </section>
    `;
  }
  renderEmptyProducts(e) {
    if (this.query)
      return i`<div class="empty"><strong>No products match “${this.query}”</strong><span>Try another name or SKU, or clear the search above.</span></div>`;
    if (this.status !== "all" && e.productCount > 0) {
      const t = k.find((s) => s.value === this.status);
      return i`
        <div class="empty">
          <strong>No ${t == null ? void 0 : t.label.toLowerCase()} products here</strong>
          <span>None of the ${m(e.productCount, "product", "products")} in this category match the filter.</span>
          <div class="actions"><button type="button" class="control" @click=${() => this.clearFilter()}>Clear filter</button></div>
        </div>
      `;
    }
    return i`
      <div class="empty">
        <strong>No products in this category yet</strong>
        <span>Use Create to add a product here, or move products in from another category.</span>
      </div>
    `;
  }
  renderProduct(e) {
    const t = this.selected.has(e.key), s = `/umbraco/section/content/workspace/document/edit/${e.key}`, o = e.image ? this.thumbnails.get(e.image) : void 0, n = Y(e), d = V(e.stock);
    return i`
      <article class="card ${t ? "selected" : ""}">
        <button type="button" class="box ${t ? "on" : ""}" role="checkbox" aria-checked=${t ? "true" : "false"} aria-label="Select ${e.name}" @click=${(p) => this.toggleKey(p, e.key)}>${t ? "✓" : ""}</button>
        <ekom-catalog-entity class="card-actions" .unique=${e.key}>
          <umb-entity-actions-dropdown compact label="Actions for ${e.name}">
            <uui-symbol-more slot="label"></uui-symbol-more>
          </umb-entity-actions-dropdown>
        </ekom-catalog-entity>
        <a class="tile" href=${s} tabindex="-1" aria-hidden="true">
          ${o ? i`<img src=${o} alt="" loading="lazy" decoding="async" />` : oe}
        </a>
        <div class="card-body">
          <a class="name" href=${s} title=${e.name}>${e.name}</a>
          <div class="row">
            <span class="sku" title=${e.sku}>${e.sku || "No SKU"}</span>
            <span class="prices">
              ${e.originalPrice ? i`<span class="was">${e.originalPrice}</span>` : c}
              <span class="price">${e.price}</span>
            </span>
          </div>
          <span class="variants">${e.variantCount > 0 ? m(e.variantCount, "variant", "variants") : "No variants"}</span>
          <div class="foot">
            <span class="badges">
              <span class="badge ${n.tone}"><i></i>${n.text}</span>
              ${e.pendingChanges ? i`<span class="badge amber edits" title="Published with unpublished edits">Edits</span>` : c}
              ${e.linked ? i`<span class="badge grey edits" title="Lives in another category and is shown here through its categories picker">Linked</span>` : c}
            </span>
            <span class="stock ${d.tone}">${d.text}</span>
          </div>
        </div>
      </article>
    `;
  }
  renderPagination(e) {
    if (e.filteredProductCount === 0 || e.totalPages <= 1)
      return c;
    const t = (e.page - 1) * e.pageSize + 1, s = Math.min(e.page * e.pageSize, e.filteredProductCount);
    return i`
      <footer class="pagination">
        <div>
          <button type="button" ?disabled=${e.page <= 1} @click=${() => this.setPage(e.page - 1)}>‹ Prev</button>
          ${G(e.page, e.totalPages).map((o) => o === "…" ? i`<span>…</span>` : i`<button type="button" class=${o === e.page ? "current" : ""} aria-current=${o === e.page ? "page" : c} @click=${() => this.setPage(o)}>${o}</button>`)}
          <button type="button" ?disabled=${e.page >= e.totalPages} @click=${() => this.setPage(e.page + 1)}>Next ›</button>
        </div>
        <span>${t}–${s} of ${e.filteredProductCount}</span>
      </footer>
    `;
  }
  getNodeIdFromUrl() {
    const e = this.getCatalogNodeFromUrl();
    if (e != null)
      return e;
    const t = window.location.href, s = t.match(/[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/i);
    if (s != null)
      return s[0];
    const o = t.match(/\/edit\/([^/?#]+)/i);
    return (o == null ? void 0 : o[1]) ?? "";
  }
  getDocumentHref(e) {
    const t = new URL(window.location.href);
    return t.searchParams.delete(x), t.pathname.match(/\/edit\/[^/]+/i) != null ? (t.pathname = t.pathname.replace(/\/edit\/[^/]+/i, `/edit/${e}`), `${t.pathname}${t.search}${t.hash}`) : `/umbraco/section/content/workspace/document/edit/${e}`;
  }
  getCatalogNodeFromUrl() {
    const e = new URL(window.location.href).searchParams.get(x);
    return e && e.trim().length > 0 ? e : null;
  }
  async fetchJson(e, t) {
    const s = await fetch(e, { credentials: "same-origin", headers: { Accept: "application/json" }, signal: t });
    if (!s.ok)
      throw new $(await s.text() || `Request failed (${s.status}).`, s.status);
    return await s.json();
  }
}
l(L, "styles", [D]);
class $ extends Error {
  constructor(a, e) {
    super(a), this.status = e;
  }
}
function S() {
  try {
    const r = window.localStorage.getItem(I);
    return r != null && T(r) ? r : f;
  } catch {
    return f;
  }
}
function ie(r) {
  try {
    window.localStorage.setItem(I, r);
  } catch {
  }
}
customElements.define("ekom-catalog-entity", re);
customElements.define("ekom-catalog-collection-view", L);
export {
  L as default
};
