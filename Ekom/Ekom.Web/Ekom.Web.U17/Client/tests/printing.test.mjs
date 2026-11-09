import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { webcrypto } from 'node:crypto';
import { runInNewContext } from 'node:vm';
import { JSDOM } from 'jsdom';
import ts from 'typescript';

const dom = new JSDOM('');
const exports = {};
const source = readFileSync(new URL('../src/manager/printing-section-view.element.ts', import.meta.url), 'utf8');
runInNewContext(ts.transpileModule(source, { compilerOptions: {
  module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022,
} }).outputText, { exports, document: dom.window.document, DOMParser: dom.window.DOMParser,
  Node: dom.window.Node, Element: dom.window.Element, HTMLElement: dom.window.HTMLElement,
  customElements: dom.window.customElements, window: dom.window, crypto: webcrypto,
  require: (name) => {
    assert.equal(name, '@umbraco-cms/backoffice/element-api');
    return { UmbElementMixin: (Base) => class extends Base {
      connectedCallback() { this.contextConnected = true; }
      disconnectedCallback() { this.contextConnected = false; }
      provideContext(token, context) { this.providedContext = { token, context }; }
    } };
  } });

test('preview removes active content and navigation while preserving print layout and embedded images', () => {
  const html = exports.createPreviewDocument(`<html><head><meta http-equiv="refresh" content="0;url=https://evil.test">
    <style>@media print { table { width:100% } }</style></head><body onload="alert(1)">
    <script>alert(1)</script><iframe src="https://evil.test"></iframe><svg onload="alert(1)"></svg>
    <a href="https://evil.test">Customer</a><table><tr><td onclick="alert(1)">Item</td></tr></table>
    <img src="https://evil.test" srcset="https://evil.test 2x"><img src="data:image/png;base64,AAAA"></body></html>`);
  const parsed = new JSDOM(html).window.document;
  assert.equal(parsed.querySelectorAll('script,iframe,svg,a,[onclick],[onload],[srcset]').length, 0);
  assert.equal(parsed.querySelectorAll('meta[http-equiv="refresh"]').length, 0);
  assert.equal(parsed.querySelector('table td').textContent, 'Item');
  assert.match(parsed.querySelector('style').textContent, /@media print/);
  assert.equal(parsed.querySelectorAll('img[src]').length, 1);
  assert.match(parsed.querySelector('meta[http-equiv="Content-Security-Policy"]').content, /default-src 'none'/);
});

test('uncertain jobs require explicit resolution, claimed jobs and readers cannot mutate', () => {
  for (const state of ['Claimed']) {
    assert.equal(exports.availableActions({ state, hasHtml: true }, true).join(','), 'preview,history');
  }
  assert.equal(exports.availableActions({ state: 'Submitted', hasHtml: true }, false).join(','), 'preview,history');
  assert.equal(exports.availableActions({ state: 'Submitted', hasHtml: false }, true).join(','), 'history');
  assert.equal(exports.availableActions({ state: 'RenderFailed', hasHtml: false }, true).join(','), 'history,retry,cancel');
  assert.equal(exports.availableActions({ state: 'NeedsReview', hasHtml: true }, true).join(','), 'preview,history,resolve-printed,resolve-reprint');
  assert.equal(exports.availableActions({ state: 'NeedsReview', hasHtml: true }, false).join(','), 'preview,history');
  assert.equal(exports.availableActions({ state: 'NeedsReview', hasHtml: false }, true).join(','), 'history,resolve-printed');
  assert.equal(exports.stateLabel('Submitted'), 'Submitted to printer');
});

// Model the context-host contract required by Umbraco's section view router.
test('printing view supports router context provision and context lifecycle', () => {
  const view = new exports.default();
  const token = Symbol('view');
  const context = {};
  view.provideContext(token, context);
  assert.equal(view.providedContext.context, context);
  assert.equal(view.providedContext.token, token);
  view.render = () => {};
  view.initialize = async () => {};
  view.connectedCallback();
  assert.equal(view.contextConnected, true);
  view.disconnectedCallback();
  assert.equal(view.contextConnected, false);
});

test('classic printing bundle loads without modern Umbraco imports', () => {
  const classicDom = new JSDOM('');
  const classicSource = readFileSync(new URL('../../../Ekom.Web/App_Plugins/Ekom/Manager/libs/printing-section-view.element.js', import.meta.url), 'utf8');
  const classicExports = {};
  runInNewContext(ts.transpileModule(classicSource, { compilerOptions: {
    module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022,
  } }).outputText, { exports: classicExports, HTMLElement: classicDom.window.HTMLElement,
    customElements: classicDom.window.customElements });
  const view = classicDom.window.document.createElement('ekom-printing-section-view');
  assert.ok(view.shadowRoot);
});

test('attention shortcut clears other filters and preserves uncertain-print action restrictions', () => {
  const view = new exports.default();
  view.loading = false;
  view.capabilities = { canManage: true };
  view.search = 'HV-1';
  view.station = 'warehouse';
  view.jobs = { total: 30, page: 1, pageSize: 25, jobs: [
    { id: 'one', orderNumber: '<unsafe>', storeAlias: 'main', stationAlias: 'warehouse', state: 'NeedsReview', copies: 1, createdUtc: '2026-11-06T13:00:00Z', hasHtml: true },
  ] };
  let loads = 0;
  view.load = async () => { loads++; };
  view.render();
  assert.match(view.shadowRoot.textContent, /1 job on this page needs attention/);
  assert.equal(view.shadowRoot.querySelector('unsafe'), null);
  assert.equal(view.shadowRoot.querySelector('[data-action="reprint"]'), null);
  view.shadowRoot.querySelector('[data-attention]').click();
  assert.equal(view.state, 'attention');
  assert.equal(view.station, '');
  assert.equal(view.search, '');
  assert.equal(view.page, 1);
  assert.equal(loads, 1);
});

test('station card distinguishes an incompatible agent and explains printing rules', () => {
  const view = new exports.default();
  view.loading = false;
  view.stations = [{ alias: 'warehouse', stores: ['main'], statuses: ['OfflinePayment'], policy: 'OncePerOrder', copies: 1, enabled: true,
    health: { online: true, compatible: false, printerName: 'Printer', agentVersion: 'old' } }];
  view.render();
  const card = view.shadowRoot.querySelector('.station-card');
  assert.match(card.textContent, /Update required/);
  assert.match(card.textContent, /Offline Payment/);
  assert.match(card.textContent, /Once per order/);
  assert.match(view.shadowRoot.querySelector('.empty').textContent, /Picking slips appear here/);
});

test('review replacement requires confirmation and retries a lost response with the same request ID', async () => {
  const view = new exports.default();
  view.capabilities = { canManage: true, csrfHeader: 'X-CSRF', csrfToken: 'csrf' };
  view.render = () => {};
  view.load = async () => {};
  const requests = [];
  view.json = async (path, options) => {
    requests.push({ path, ...options, body: JSON.parse(options.body) });
    if (requests.length === 1) throw new Error('Response lost');
    return {};
  };
  dom.window.confirm = message => {
    assert.match(message, /clear any pending copy/);
    assert.match(message, /delayed original may still print/);
    return false;
  };
  await view.action('job', 'resolve-reprint');
  assert.equal(requests.length, 0);
  dom.window.confirm = () => true;
  await view.action('job', 'resolve-reprint');
  await view.action('job', 'resolve-reprint');
  assert.equal(requests.length, 2);
  assert.equal(requests[0].path, '/jobs/job/actions');
  assert.equal(requests[0].body.action, 'resolve-reprint');
  assert.equal(requests[0].body.requestId, requests[1].body.requestId);
  assert.equal(requests[0].headers['X-CSRF'], 'csrf');
});

test('explicit stations show the capture API instead of a missing-status warning', () => {
  const view = new exports.default();
  view.loading = false;
  view.stations = [{ alias: 'warehouse', stores: ['main'], statuses: [], triggerMode: 'ExplicitCapture', policy: 'OncePerOrder', copies: 1, enabled: true,
    health: { online: true, compatible: true } }];
  view.render();
  const card = view.shadowRoot.querySelector('.station-card');
  assert.match(card.textContent, /Explicit capture API/);
  assert.doesNotMatch(card.textContent, /No statuses configured/);
});

test('printing dialog closes on Escape or backdrop, but not on its content or padding', () => {
  const view = new exports.default(); view.render();
  const dialog = view.shadowRoot.querySelector('dialog');
  let closes = 0; dialog.close = () => { closes++; };
  dialog.getBoundingClientRect = () => ({ left: 100, right: 500, top: 100, bottom: 500 });
  dialog.dispatchEvent(new dom.window.MouseEvent('click', { clientX: 200, clientY: 200, bubbles: true }));
  dialog.querySelector('[data-detail]').dispatchEvent(new dom.window.MouseEvent('click', { clientX: 0, clientY: 0, bubbles: true }));
  assert.equal(closes, 0);
  dialog.dispatchEvent(new dom.window.MouseEvent('click', { clientX: 50, clientY: 200, bubbles: true }));
  dialog.dispatchEvent(new dom.window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
  dialog.dispatchEvent(new dom.window.Event('cancel', { cancelable: true }));
  assert.equal(closes, 3);
});
