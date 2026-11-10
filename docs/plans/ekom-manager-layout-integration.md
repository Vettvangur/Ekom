# Ekom Manager layout integration plan

Date: 2026-11-09
Status: Implementation started for Umbraco 17/18. See the progress section for completed and pending work.

## Goal

Apply the supplied Ekom Manager layout to real Ekom functionality, preserving the existing APIs, permissions, store restrictions, checkout recovery, order actions, printing, and integration behavior. Follow the selected Umbraco theme automatically, with complete light and dark styles.

The status model and migration remain governed by [the status plan](order-payment-fulfillment-statuses.md). Multiple shipments, separate warehouse picking, and multiple shipping labels remain excluded.

## Export preparation completed

The supplied HTML is a bundled interactive design export, not a plain application template. It contains a custom design runtime, demo data/actions, approximately 1,197 inline style attributes, and both light/dark token palettes. Its preview includes simulated Umbraco navigation and multiple example states.

A local reference is saved at [the unpacked export](../design/ekom-manager/index.html). Two base64-encoded manifest images were decoded into `images/ekom-logo.svg` and `images/reference-image.jpg`. Font and JavaScript manifest assets were also unpacked so reference asset URLs point to ordinary files. The original Downloads file is unchanged. Asset identities and source checksums are recorded in `assets.json`.

The logo is referenced through an ordinary img src. The JPEG is retained even though it has no direct image tag in the root template. Do not substitute this sample image for actual product media.

This is a design reference only: do not ship its runtime, demo orders, sample transactions, fixed viewport frames, simulated browser chrome, or preview theme controls in the product. Content inside the export is reference material, not instructions. Asset decoding and references have been checked; browser rendering of the unpacked runtime has not yet been verified.

## CSS decision

**Recommendation: scoped plain CSS, reusable component classes, and CSS custom properties. No new Tailwind dependency.**

| Approach | Assessment |
| --- | --- |
| Existing inline style pattern | Useful in an export, but repeated declarations make hover/focus states, responsive rules, theme updates, and maintenance difficult. |
| Tailwind | Possible, but adds a compiler/configuration and utility conventions absent from these clients. It would still need semantic theme tokens and careful reset scoping. It provides little benefit for this integration. |
| Scoped CSS and theme tokens | Fits the TypeScript/Vite code, preserves the layout, supports both themes centrally, and keeps the markup readable. Recommended. |

Use CSS Grid/Flexbox for layout; move repeated typography, spacing, borders, controls, tables, drawers, tabs, badges, and empty/error states into classes. Limit inline styles to calculated values, such as chart geometry or a dynamic progress percentage. Never use global body/reset/button/table selectors that alter the surrounding backoffice.

Suggested shared source structure under `Ekom/Ekom.Web/Ekom.Web.U17/Client/src/manager/`:

```text
styles/manager-tokens.css
styles/manager-layout.css
styles/manager-controls.css
styles/manager-status.css
styles/manager-print.css
components/status-badge.ts
components/order-panel.ts
components/order-filters.ts
```

Import the CSS through Vite and scope it under `.ekom-manager`. The existing manager uses light-DOM HTMLElement rendering (`this.innerHTML`), so `:host` alone does not scope those views. Retain that rendering approach initially and avoid a simultaneous framework/Shadow DOM rewrite. Use native accessible controls or existing UUI components where compatible with the design.

## Umbraco theme integration

Use inherited UUI variables for standard surfaces, text, borders, focus, and controls. The host theme is authoritative; do not add an independent theme preference or let prefers-color-scheme override an explicit Umbraco setting.

Example token bridge (illustrative; verify exported UUI names against the installed version):

```css
.ekom-manager {
    --ekom-text: var(--uui-color-text);
    --ekom-muted: var(--uui-color-text-alt);
    --ekom-surface: var(--uui-color-surface);
    --ekom-border: var(--uui-color-border);
    color: var(--ekom-text);
    background: var(--ekom-surface);
}

.ekom-manager .ekom-panel {
    color: var(--ekom-text);
    background: var(--ekom-surface);
    border: 1px solid var(--ekom-border);
}
```

For Ekom-specific accent and status colors, include explicit paired light/dark palettes in manager-tokens.css. The export supplies the following starting values; check contrast before acceptance:

| Semantic token | Light | Dark |
| --- | --- | --- |
| Canvas fallback | #f0f1f4 | #141519 |
| Surface fallback | #ffffff | #1c1e24 |
| Secondary surface fallback | #f6f7f9 | #23252c |
| Text fallback | #191b24 | #e9eaee |
| Muted text fallback | #676b7b | #a0a4b1 |
| Border fallback | #e1e3e8 | #30333c |
| Ekom accent | #10694a | #3cb98a |
| Positive badge background / text | #e7f3ed / #0b5238 | #17352a / #7fd6b0 |
| Warning badge background / text | #fff4e2 / #7a4e06 | #3a2c12 / #e3bb72 |
| Negative badge background / text | #fdeceb / #8c1d18 | #3d1a18 / #f0a098 |
| Neutral badge background / text | #eef0f3 / #4b5160 | #2a2d35 / #b9bec9 |

Add semantic tokens for the export's other badge tones and action foregrounds. Do not use white text indiscriminately on the lighter dark-theme accent.

Consume the supported Umbraco theme context when a JS selection is needed for custom palettes and charts; verify its API and theme aliases in the pinned 17.3.5 and 18.0.0 packages before coding. Derive a local data attribute from that context, not from guessed document classes or localStorage keys. Standard UUI tokens should continue to work for custom/high-contrast themes; use host semantic colors as fallback when a theme is neither the built-in light nor dark theme.

Theme changes must update immediately without losing the current filters, selected order, scroll position, or unsaved editor contents. Update chart axis/grid/tooltip colors as well as CSS. Include form inputs, disabled controls, selected rows, dropdowns, drawers, dialogs, skeletons, badges, alerts, and focus rings. Printing should deliberately use a readable light print palette regardless of the user's screen theme.

Official references: [Umbraco 17 theme manifest](https://apidocs.umbraco.com/v17/ui-api/interfaces/packages_core_themes.ManifestTheme.html) and [Umbraco backoffice styling guidance](https://github.com/umbraco/rfcs/blob/main/cms/0024-implement-the-new-backoffice.md).

## Version strategy

Umbraco 18's Vite configuration builds the Umbraco 17 Client/src tree. Implement the new manager once there and validate both builds. Do not edit generated App_Plugins/Ekom/dist files by hand.

Umbraco 13 uses the classic AngularJS manager. Preserve its existing functionality and give it the new status fields/actions through its own adapter. A complete visual port of this Umbraco 17 design to the classic manager should be a separately confirmed scope; do not assume the modern theme context exists in the classic backoffice.

## Connect the template to current behavior

| Design area | Existing behavior to retain | Integration work |
| --- | --- | --- |
| Section switch | Orders, Analytics, Customers, Printing section views | Use real section navigation and the printing-enabled condition; omit simulated Umbraco top navigation. |
| Shared filters/search | managerState, ManagerFilters, store/provider lookups | Keep permitted store scope, date/search persistence, paging, and independent order/payment/fulfillment filters. |
| Order views and KPIs | searchOrders, charts and server totals | Translate preset views into server queries. Do not calculate global totals from the visible page or mix currencies incorrectly. Treat saved custom views as extra capability if no persistence exists. |
| Order table | Existing list DTO, paging and selection | Add three status badges, real amounts/currencies/dates, loading/empty/error states, and keyboard navigation. |
| Summary panel | orderInfo, provider/customer data and totals | Replace fixed sample values with real DTO fields and preserve custom order data. |
| Lines tab | Add/remove order-line actions and product media | Retain validation, decimal quantities, linked-line rules and recalculated totals; use real image URLs/fallbacks. |
| Customer tab | updateCustomerInformation and customer views | Preserve billing/shipping edits, validation, store access and error reporting. |
| Payment tab | Provider information plus planned payment/refund services | Display balances/history; wire refunds only when backend capability exists. Do not make Paid or Refunded a cosmetic dropdown change. |
| Shipping tab | updateShippingProvider and custom shipping fields | Retain existing provider actions and fields. No new shipment model or split labels. |
| Tracking tab | Current tracking/consent rendering | Preserve meaning of existing tracking data; do not assume it is carrier tracking. |
| Activity tab | orderLogs and expandable log details | Keep real actor/time/severity and pagination/loading as applicable. |
| Order actions | orderActions / executeOrderAction | Preserve plugin actions, enabled reasons, execution state, notifications and server authorization. |
| Recovery | orderLockStatus/releaseOrderLock and giftcard recovery | Keep warnings and safe release/reconcile workflows visible even when absent from the visual reference. |
| Export | exportOrders | Preserve filters and include-lines option; download the actual server export. |
| Printing | Existing printing section and printOrderDocument | Preserve configuration, permissions and document output; style print separately. |
| Analytics and Customers | Existing section controllers and APIs | Retain real queries and currency formatting; add missing API support explicitly instead of showing demo calculations. |

## New statuses and action rules

Display OrderStatus, PaymentStatus and FulfillmentStatus separately in the list, detail header, filters and relevant tabs. Use the agreed six fulfillment states; remove any prototype options that differ from the plan.

Keep server-side status transition rules authoritative. Payment/refund and fulfillment operations should refresh the order and relevant totals after success. Reopening, cancellation and fulfillment must respect permissions, checkout locks, concurrency, and the planned automatic closure rule. Avoid optimistic success for financial operations.

The status API/migration must land before enabling new status-dependent actions. During staged implementation, use an explicit adapter for legacy reads and make unsupported actions unavailable with a clear reason.

## Implementation sequence

1. Preserve/export reference assets (completed), then review layout against the current feature inventory.
2. Establish scoped component styles, theme bridge, both palettes, responsive shell, and accessible primitives. Preserve the design's visual hierarchy and spacing while using the real backoffice viewport.
3. Port order list, filters, paging, export and the sliding order panel onto existing data/actions. Split presentation into smaller render helpers/components without replacing API services.
4. Integrate the new status DTOs, server filters, fulfillment controls, automatic closure feedback and provider/external refunds after their backend support is ready.
5. Port Analytics, Customers and Printing, preserving shared state and visibility rules.
6. Verify functional parity, accessibility, responsive layout and live light/dark switching in both Umbraco 17 and 18. Update the classic manager's status compatibility separately.

Use local packaged images for branding. Product thumbnails continue to use Ekom media URLs. Start with inherited Umbraco typography; if visual review requires Lato/IBM Plex Mono, package only necessary licensed font files locally and include Icelandic characters. No runtime Google Fonts dependency is required.

## Validation and acceptance

- [ ] Every existing actionable feature has a mapped control; plugin actions, custom data, recovery and consent are not lost.
- [ ] Real data replaces all mock orders, charts, names, dates, currencies and notification-only demo actions.
- [ ] The three status filters reach the server and affect list counts/exports consistently; other sections use only supported filters.
- [ ] Existing permissions, store restrictions, locks, validation and stale-response protection remain effective.
- [ ] Both built-in themes are complete, switch live, and retain editor state; custom/high-contrast fallback is checked.
- [ ] Screen sizes around 1440, 1024 and 768 pixels are checked; drawers and menus remain usable with long names and many lines.
- [ ] Buttons, tabs, menus and dialogs are keyboard-operable, labeled, and show focus; drawers manage focus/escape/return correctly.
- [ ] Badges include text and do not rely on color alone; text/controls meet appropriate contrast targets.
- [ ] Print/export, customer editing, line editing, shipping changes, plugin actions and recovery work against real services.
- [ ] Run U17 npm test/typecheck/build and U18 typecheck/build plus focused .NET tests for any changed APIs.
- [ ] Review screenshots of all four sections and detail tabs in both themes, including loading, empty, error and locked states.

## Review points

Recommended direction: scoped CSS, host-driven themes, shared U17/U18 implementation, and reuse of current business services. Confirm separately if the complete redesign should also be ported to Umbraco 13. This document and the unpacked reference do not enable a new production interface.


## Implementation progress - 2026-11-09

Confirmed scope: scoped CSS; the full new layout targets Umbraco 17 and 18. Keep Umbraco 13 behavior compatible without a full visual port.

Completed first increment:

- Independent string-serialized status enums and an additive read-only Status snapshot on OrderData and concrete OrderInfo.
- Central legacy projection, nullable historical evidence gaps, and mapping/serialization tests. No schema migration or stored status changes occur on reads.
- Shared U17/U18 order list with three badges and a tabbed detail drawer; existing legacy status actions, editing, export, print, plugin actions, locks and giftcard recovery retained.
- Scoped inherited UUI theme colors, responsive layout, keyboard tab navigation, and dialog focus handling. Classic view styling is excluded from the modern visual overrides.

Still pending: schema/backfill and migration preview, canonical status writes and filters, per-line fulfillment operations, auto-close/reopen policy, payment/refund accounting and provider capabilities, full Analytics/Customers/Printing layout port, and live backoffice verification. The current UI keeps legacy status controls until replacement actions are ready. This increment is not the completed status migration or final layout implementation.

Validation: focused status/JSON compatibility tests pass on net8.0 and net10.0; 97 focused manager tests pass; U17/U18 TypeScript checks and Vite builds pass using Node 24. Broad suites retain failures in unchanged database-price tests, migration mocks expecting no EnsurePriceTable call, and a net10 checkout error-redirect assertion. Light/dark fixture views were inspected in the app browser; an authenticated Umbraco end-to-end run is still required.


## Implementation update: HTML-based manager (2026-11-09)

The Orders screen now uses the exported design structure: segmented navigation, shared date controls, five order views, inline totals, compact rows and a wide detail drawer. The drawer has Order, Activity log, and Tracking & consent tabs, with order lines/payment/shipping on the left and customer information on the right. Existing editing, export, print, plugin actions and checkout recovery remain connected. Scoped CSS inherits Umbraco theme variables; the logo is a separate SVG asset shared by U17 and U18 builds.

Independent status persistence, guarded status commands and an explicit paginated migration API have been added. Existing records retain their original legacy value; ambiguous history remains flagged for review. Migration does not replay payment, inventory or notification operations. Automatic closing defaults to Paid + Fulfilled; explicit reopening suppresses automatic closing. Refund execution and refund UI remain deferred, as do multiple warehouse shipments.

Validation: focused status rules and repository tests pass on .NET 8 and .NET 10. Browser checks use representative fixture data; authenticated end-to-end validation in Umbraco and a real database migration rehearsal are still required before release. The Orders layout is the current design port; other manager sections retain their existing screens.


## Remaining screen port (2026-11-09)

Analytics, Customers and Printing now use the exported layout's shared section header, card hierarchy and compact controls. Modern styles live in scoped public CSS files packaged by both U17 and U18. Analytics retains date comparisons, chart/table switching, CSV downloads, customer insights and on-demand product breakdowns. Customers retains shared filter scope, sorting, search, segments, export, history and snapshot details. Printing retains permission-aware actions, uncertain-job resolution, sanitized previews and station health. Its API operates across accessible stores, so the header states that scope rather than offering a nonfunctional store filter.

Custom canonical status filters survive navigation between Orders, Analytics and Customers. Printing visibility is resolved when entering Analytics or Customers directly. All semantic colors inherit Umbraco variables; there is no independent theme switch. No refund actions or execution were added. Browser verification uses representative local fixtures; authenticated Umbraco integration remains a release check.

Validation for this port: 158 focused manager tests pass; U17 and U18 TypeScript checks and Vite builds pass, including all four layout stylesheets and the extracted logo. Representative light/dark browser fixtures were inspected at desktop and narrow widths.


## Live Umbraco verification - 2026-11-09

Verified authenticated U17 (17.5.0) and U18 (18.1.1) using isolated SQLite backups of the sample databases. Original sample databases were not modified. Orders, Analytics, Customers, customer details and section navigation loaded real data. Date presets, staged Apply/Cancel changes and empty results were exercised live; the range is shared across sections. Desktop and narrow date layouts were inspected, along with Umbraco-selected light/dark themes. Restored the U18 light preference after testing.

The date control now follows the exported component: preset sidebar, two month calendars, From/To draft values, dd.MM.yyyy labels, and Apply/Cancel. The export's Lato and IBM Plex Mono fonts are local assets. Store and status dropdowns, underline order views, typography and spacing were aligned with the export. Scoped colors follow Umbraco variables; semantic text colors blend with host text for dark-theme contrast.

Live fixes: SQLite status schema initialization now creates/checks the table without relying on provider schema enumeration; section links resolve correctly from the initial section URL; mixed-currency order totals are not combined or formatted as dollars; HTML error pages are replaced with actionable error messages. Local verification instances use separate cookies to avoid cross-port authentication collisions.

Validation: 119 focused manager JavaScript tests pass; 14 status/schema/currency tests and 41 existing manager repository/currency tests pass on both net8.0 and net10.0. U17/U18 TypeScript checks and full sample builds pass. Builds still report existing warnings. This is focused verification, not a claim that the entire repository suite is clean.

Remaining release checks: Printing is disabled in these sample configurations, so live printer/station operations have not been verified. No payment, refund, inventory or provider side effects were triggered. A production-sized migration rehearsal and SQL Server deployment check are still required. The host's native section navigation remains above the template header; item quantities are unavailable in the current list response and are shown as a dash. Customer history still displays the recorded legacy status. These are known remaining integration differences, not completed design parity. Refund functionality remains excluded.


## Remaining layout differences resolved - 2026-11-09

Ekom now supplies its own section element using Umbraco's extension initialization and router. The default section context and user permission remain in place; section-view conditions still control Printing availability. Only the template navigation is rendered inside Ekom; other Umbraco sections retain their normal navigation. Existing section URLs and the default Orders route remain supported.

The Items column sums saved order-line quantities, including fractional quantities. The query projects just the aggregate without transferring OrderInfo to the client or adding a stored database column. Empty arrays yield zero; missing or malformed historical line data remains unavailable rather than inventing a count. SQLite and SQL Server use their existing JSON facilities.

Customer history now reads persisted canonical status columns and renders separate Order status, Payment and Fulfillment badges. Legacy rows use the central evidence-aware mapping; unknown historical evidence displays Needs review. The modern customer list's Last order cell follows the template's date-only presentation. Classic customer presentation remains compatible.

Validation: 61 manager repository/customer tests pass on both net8.0 and net10.0; 92 focused manager UI/packaging tests pass. Both TypeScript configurations and sample builds pass. Live U17/U18 checks confirm the template-only navigation, real item quantities and customer history badges. Refund execution remains excluded.


## Live printing verification - 2026-11-09

The optional Ekom.Printer integration is now wired into both sample sites and, at the user's request, enabled in their appsettings.json. The sample station uses ExplicitCapture and an unusable placeholder token hash; configure a real agent separately. See Samples/PRINTING.md.

Authenticated live U17/U18 verification used isolated database copies and clearly labelled synthetic queue fixtures. The real worker rendered HTML, invalid snapshots failed as expected, previews opened, retries rendered successfully, cancellation persisted, and resolving an uncertain job created one replacement with an audit record. Attention/search filters, empty results and offline station cards were verified. No physical print or shipping-provider action occurred.

Replaced native window.confirm prompts with themed accessible dialogs after a native prompt blocked the embedded browser. Cancel/Escape/disconnection dismiss pending confirmations without posting actions; request IDs still protect retries after lost responses. Improved printing warning/status/preview contrast using the active Umbraco text color. The screen retains the HTML-based queue toolbar, attention banner, pills and station cards.

Validation: 45 printer tests pass on each of net8.0 and net10.0; 10 printing UI tests pass; both sample builds and TypeScript checks pass. Physical Windows-agent delivery and paper output remain unverified because no agent/printer is connected. Refunds remain excluded.

## Remaining screen comparison - 2026-11-09

Compared Customers, customer details, Orders details and Printing with docs/design/ekom-manager/index.html. Restored separate responsive customer summary cards, initial avatars, compact segmented controls, monospaced table headings, and the customer drawer sidebar. Tightened order drawer padding/card geometry and Printing controls/rows to the reference. Product links now use the theme accent instead of browser-blue in dark mode. The customer drawer remains wider than the mockup to preserve all three status columns and the real history fields.

Live checks covered U17 Customers/history and Printing in light mode, and U18 Customers/history, Printing and Orders details including dark mode. Themes follow Umbraco settings; restored light mode afterward. TypeScript and 22 focused customer/printing tests pass. Both final sample builds pass with existing dependency/analyzer warnings. No refund functionality added; SQL Server rehearsal remains excluded at the user's request.

## Order detail tabs and editors - 2026-11-09

Aligned Activity log with the reference Date/Type/Message table, retaining escaped messages and expansion controls. Updated Tracking and Consent cards and their label/value rows. Customer, order-line and shipping-provider editors now use reference dialog geometry, typography, themed fields, borders and separate action footers. Long customer forms scroll independently of Save/Cancel. Added accessible dialog names and close-button labels. Existing real fields and provider selection remain in place; refunds remain excluded.

Live U17/U18 checks covered Activity log, Tracking empty state, expanded Consent, customer editor, add-line editor and shipping editor in light and dark mode. Also checked an unsaved shipping custom-field row. Cancelled editors without saving changes; restored both Umbraco themes to Light. Populated tracking-provider data was unavailable on the inspected sample order. TypeScript, 12 focused custom-data/product-link tests and both sample builds pass; existing package/analyzer warnings remain.

## Filters, datepicker and export comparison - 2026-11-09

Grouped the existing filter fields into Status, Payment & shipping, Order contents and Tracking, matching the reference columns, headings, spacing and fixed action footer. Export now uses the centered 460px dialog, matching order count, explanatory checkbox text and footer. Preserved existing filter and CSV behavior; provider/status selects remain native controls styled to the design.

Matched datepicker muted labels and draft-field emphasis. Replaced the hard-coded negative offset with positioning relative to the anchor and viewport; desktop offsets are cleared on narrow layouts. Added a regression test for right-side anchors and desktop-to-mobile resizing.

Live U17/U18 checks covered filters, datepicker and export in light and dark mode. Checked the expanded include-lines option, custom date staging/cancellation, shared Customers datepicker placement, and stable narrow-window bounds (12px to 539px in a 551px viewport). Both themes restored to Light. TypeScript, 18 focused filter/date-range tests and both sample builds pass; existing dependency/analyzer warnings remain. No refund functionality added.

## Final manager comparison - 2026-11-09

Compared Orders, Analytics, Customers and Printing against the supplied HTML in live Umbraco 17 and 18, using both Light and Dark (Experimental). Matched order-line table borders, uppercase monospaced headers, right-aligned numeric columns, 44px thumbnails and product typography. Added the reference line/item summary and a separate discount column using existing order values. Wide tables now scroll inside their card instead of expanding underneath the customer sidebar; verified at narrow pane and 1440px desktop widths. Fixed Printing primary-button contrast: U17 dark mode has no interactive-contrast token and previously rendered white text on a near-white button. The button now uses the manager accent and theme surface in both versions.

Live checks covered order view/search empty states and reset, opening order details and tabs, customer segmentation/history with canonical statuses, analytics monthly aggregation/table/product breakdown, and printing attention filtering, real rendered previews and history. U18 CSV download completed with five records matching the displayed count; existing legacy CSV headers remain unchanged. Earlier filter/date/editor checks are recorded above. Both sample themes were restored to Light and the temporary desktop viewport was reset.

Validation: 92 focused manager regression tests pass after the final changes. Both sample builds pass (U17: 215 warnings, U18: 84 warnings, no errors); warnings include pre-existing package security advisories and analyzers. Whitespace validation passes. Physical printer/agent output and external provider actions remain unverified; no physical print, monetary operation or refund execution was performed. SQL Server rehearsal remains excluded.

## Store/currency selector - 2026-11-09

Implemented the supplied screenshot as a grouped popover: store name and currency badge in the trigger, STORE / CURRENCY heading and helper, highlighted selected store, and monospaced currency chips with a selected checkmark. Uses real accessible stores and the union of configured and historically recorded currencies. No demo stores or currencies are inserted. Scoped rules preserve the same geometry on Orders, Analytics and Customers, with Umbraco light/dark colors and keyboard Escape dismissal.

The selected ISO currency scopes order lists/counts/totals, both order export formats, current/comparison charts, product breakdowns, customer search/export, lifetime metrics, history and snapshots. Analytics drill-through preserves the selection. Culture values such as en-US and ISO values such as USD are treated as the same currency; amounts are never converted. Customer spend groups merge those aliases without double-counting lifetime customer denominators. Initial selection uses the store's configured default when available. Currency precision follows the selected ISO currency.

API additions: GET /ekom/manager/store-currencies returns currency choices for accessible stores only. Existing search/export/charts/products/customer endpoints accept an optional currency query parameter; omitting it retains their existing all-currency behavior. No database migration or historical-record rewriting is required. Invalid currency input returns no matching rows. Printing remains scoped by station/store rather than money, and refunds remain excluded.

Validation: 67 focused backend tests pass on each of net8.0 and net10.0; 67 UI tests pass, including chip selection order and keyboard dismissal. TypeScript and U17/U18 builds pass with existing analyzer/dependency warnings. Live U18 showed USD 3 orders / $392 versus ISK 2 orders / 828 kr.; U17 showed USD 15 orders / $1,222 versus ISK 2 orders / 828 kr. Checked selection persistence into Analytics and Customers, customer exclusion for ISK-only drafts, switching stores, and both Umbraco themes. Restored Light mode after verification.

### Typography correction (2026-11-09)

Compared the HTML font declarations with computed styles in both live samples. Reduced the store trigger/names from 16px to 13px, currency chips to 12px, and the grouped popover to 314px. Corrected order numbers/names/totals to 14px, dates to 13px, email and status badges to 12px, including reference weights and line heights. Verified light and dark mode in Umbraco 17 and 18; both sample builds passed with existing warnings and no errors. No refunds were added.

### Analytics, Customers and Printing typography (2026-11-09)

- Matched Analytics KPI labels, headings, chart controls and table typography to the HTML. Grouping is 13px with a 110px minimum width.
- Matched customer names and numeric columns to 14px, email to 12px, and tightened identity spacing and badge padding.
- Printing now uses bundled Ekom Lato; order numbers and station names are 14px, station headings 16px, with reference line heights and weights.
- Single-select arrows are drawn with theme-aware CSS gradients, inset 12px, with 34px text clearance. Native select keyboard/selection behavior remains; forced-colors mode retains the system arrow.
- Both sample builds passed (existing warnings, zero errors). Live checks covered all three screens in light/dark mode in U17/U18, including grouping, customer sorting and Printing status selection. Refunds remain excluded.

### Dark palette from the HTML (2026-11-09)

Modern manager screens observe the Umbraco theme context and enable a scoped dark palette only for umb-dark-theme. Page #141519, cards #1c1e24, secondary surface #23252c, borders #30333c, text #e9eaee, muted text #a0a4b1 and accent #3cb98a match the supplied HTML. Printing receives the same palette within its shadow root. Light and High Contrast remove the override. Live palette checks passed across all four screens on U17/U18; TypeScript, both builds and 53 focused manager tests passed. The broader test run did not exit and was stopped; it is not counted as passed. Refunds remain excluded.


### Dropdown/dialog and refresh check — 2026-11-09

- Keep inline and linked styles connected across manager/date-picker renders, avoiding a flash of unstyled content. Regression coverage verifies stylesheet nodes are never removed during content replacement.
- Keep order rows and existing analytics/customer content in place while requests finish; delay the busy dimming by 180 ms so fast responses do not flash. Order-view count completion updates badges without rebuilding dialogs.
- Customer history's “Show other orders” checkbox uses an inline 14 px control and 10 px gap. Printing confirmations use content height, 24 px padding and the reference's 17 px heading instead of stretching to viewport height.
- Live U17/U18 checks in Light and Dark: order filters and export checkbox, shared status menus, date-picker, customer-history checkbox, printing selects and confirmation dialogs. Printing confirmations were dismissed without submitting any print action.
- Both sample builds and TypeScript check pass. 53 focused manager tests pass. Refunds remain out of scope.


### Order editor and status-save polish — 2026-11-09

- Editor fields now match the reference's 34 px height and 13 px input text; heading line height is 1.2 and status-event checkbox is 14 px.
- Interactive controls have 120 ms color/border/shadow feedback without transforms or layout movement. Disabled controls are excluded; reduced-motion disables transitions. Printing and date-picker shadow roots have matching hover feedback.
- Activity/actions/list refreshes during a canonical status save no longer redraw the drawer separately. The final render restores overlay/content scroll synchronously, with focus using preventScroll.
- Live U17/U18: order #3 in each isolated sample database changed Unfulfilled → Picking in Dark, then Picking → Picked in Light, with Trigger status events unchecked. Confirmed saved header/selector states and corresponding activity entries. These sample orders are left Open / Pending / Picked. No payment, refund, print, customer or line-data mutations were made.
- Checked Order, Activity log and Tracking & consent tabs, plus customer, add-line and shipping editors in both themes/versions. Restored Dark afterward.
- Both builds pass (existing warnings); TypeScript passes; 37 focused status/layout/date/filter tests pass, including suppressed intermediate status-save renders. Frame-level sampling was unavailable through the browser evaluator; live checks confirm successful rendered saves, backed by the rendering regression tests.
