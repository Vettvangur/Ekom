import { css } from '@umbraco-cms/backoffice/external/lit';

/**
 * Tokens and component styles of the catalog viewer. The light and dark sets are switched by the
 * host's `data-theme` attribute, which follows Umbraco's theme context.
 */
export const catalogStyles = css`
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
`;
