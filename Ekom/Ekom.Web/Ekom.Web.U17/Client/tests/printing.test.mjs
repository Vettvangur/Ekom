import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
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
  customElements: dom.window.customElements,
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

test('claimed and uncertain jobs cannot be cancelled or reprinted, and readers cannot mutate', () => {
  for (const state of ['Claimed', 'NeedsReview']) {
    assert.equal(exports.availableActions({ state, hasHtml: true }, true).join(','), 'preview,history');
  }
  assert.equal(exports.availableActions({ state: 'Submitted', hasHtml: true }, false).join(','), 'preview,history');
  assert.equal(exports.availableActions({ state: 'Submitted', hasHtml: false }, true).join(','), 'history');
  assert.equal(exports.availableActions({ state: 'RenderFailed', hasHtml: false }, true).join(','), 'history,retry,cancel');
  assert.equal(exports.stateLabel('Submitted'), 'Submitted to Windows');
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
