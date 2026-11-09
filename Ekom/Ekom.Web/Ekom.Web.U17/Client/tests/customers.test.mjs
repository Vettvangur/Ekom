import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';
import { JSDOM } from 'jsdom';

function module(path, modules = {}, globals = {}) {
  const exports = {};
  const source = readFileSync(new URL(path, import.meta.url), 'utf8');
  runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText,
    { exports, require: name => modules[name], HTMLElement: class {}, customElements: { define() {} }, URLSearchParams, ...globals });
  return exports;
}
function fixture() {
  const shared = module('../src/manager/manager-shared.ts');
  const { EkomCustomersSectionViewElement } = module('../src/manager/customers-section-view.element.ts', {
    './manager-shared': shared, '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
  });
  const view = new EkomCustomersSectionViewElement();
  view.render = () => {};
  return { view, state: shared.managerState };
}
const emptyResult = { customers: [], totals: { customers: 0 }, spend: [], missingIdentifiers: 0, totalPages: 0 };

test('Customers uses the exact shared store, date range and status without clearing order filters', async () => {
  const { view, state } = fixture();
  Object.assign(state.filters, { store: 'Iceland', dateFrom: '2026-03-01', dateTo: '2026-03-31', orderStatus: 'Dispatched', query: 'order query', couponCode: 'SAVE' });
  const calls = [];
  view.get = async (path, params) => { calls.push(params); return emptyResult; };
  await view.load();
  assert.equal(calls[0].get('store'), 'Iceland');
  assert.equal(calls[0].get('start'), '2026-03-01');
  assert.equal(calls[0].get('end'), '2026-03-31');
  assert.equal(calls[0].get('orderStatus'), 'Dispatched');
  assert.equal(state.filters.query, 'order query');
  assert.equal(state.filters.couponCode, 'SAVE');
});

test('a slower previous store response cannot overwrite the current customer list', async () => {
  const { view, state } = fixture();
  state.filters.store = 'old';
  let oldResolve;
  view.get = async () => new Promise(resolve => { oldResolve = resolve; });
  const old = view.load();
  state.filters.store = 'new';
  view.get = async () => ({ ...emptyResult, missingIdentifiers: 7 });
  await view.load();
  oldResolve({ ...emptyResult, missingIdentifiers: 99 });
  await old;
  assert.equal(view.result.missingIdentifiers, 7);
});

test('changing scope invalidates an in-flight customer detail request', async () => {
  const { view, state } = fixture();
  state.filters.store = 'old';
  let resolve;
  view.get = async () => new Promise(r => { resolve = r; });
  const detail = view.open('person');
  state.filters.store = 'new';
  view.get = async () => emptyResult;
  await view.load();
  resolve({ customer: { identity: 'person' } });
  await detail;
  assert.equal(view.detail, undefined);
});

test('customer snapshot text is escaped and missing reorder interval is not displayed as zero', () => {
  const { view } = fixture();
  const html = view.renderDetail({ customer: { name: '<script>bad</script>', identity: 'a', orders: 1, spend: [], firstOrder: '2026-01-01', daysSinceLastOrder: 1 }, orders: [], count: 0, page: 1, totalPages: 0, snapshot: { fields: { Phone: '<img onerror="bad">' }, date: '2026-01-01', orderNumber: '1' } });
  assert.ok(!html.includes('<script>'));
  assert.ok(html.includes('&lt;script&gt;'));
  assert.ok(!html.includes('<img onerror'));
  assert.ok(html.includes('Average reorder interval (days)</small><strong>—'));
});

for (const [name, elementPath, sharedPath, sharedImport] of [
  ['source', '../src/manager/customers-section-view.element.ts', '../src/manager/manager-shared.ts', './manager-shared'],
  ['U17 bundle', '../../App_Plugins/Ekom/dist/customers-section-view.element.js', '../../App_Plugins/Ekom/dist/manager-shared.js', './manager-shared.js'],
  ['U18 bundle', '../../../Ekom.Web.U18/App_Plugins/Ekom/dist/customers-section-view.element.js', '../../../Ekom.Web.U18/App_Plugins/Ekom/dist/manager-shared.js', './manager-shared.js'],
]) {
  test(`Customers ${name} exposes the constructor required by the Umbraco extension loader`, () => {
    const registered = new Map();
    const exports = module(elementPath, {
      [sharedImport]: module(sharedPath),
      '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
    }, { customElements: { define: (tag, constructor) => registered.set(tag, constructor) } });
    const constructor = exports.element ?? exports.default;
    assert.equal(typeof constructor, 'function');
    assert.equal(constructor, registered.get('ekom-customers-section-view'));
    assert.ok(new constructor() instanceof constructor);
  });
}

test('customer search works inside the backoffice form with Search and Enter', () => {
  const dom = new JSDOM('<form id="backoffice"><ekom-customers-section-view></ekom-customers-section-view></form>');
  const shared = module('../src/manager/manager-shared.ts');
  const { EkomCustomersSectionViewElement } = module('../src/manager/customers-section-view.element.ts', {
    './manager-shared': shared, '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
  }, { HTMLElement: dom.window.HTMLElement });
  EkomCustomersSectionViewElement.prototype.initialize = async () => {};
  dom.window.customElements.define('ekom-customers-section-view', EkomCustomersSectionViewElement);
  const view = dom.window.document.querySelector('ekom-customers-section-view');
  // Registration connects the element; prevent initialization from issuing network requests.
  view.initialize = async () => {};
  let outerSubmits = 0;
  dom.window.document.querySelector('form').addEventListener('submit', event => { event.preventDefault(); outerSubmits++; });
  const queries = [];
  view.load = async () => { queries.push({ query: view.query, page: view.page }); };
  view.render();
  view.page = 3;
  view.querySelector('[name=query]').value = ' Emma ';
  Array.from(view.querySelectorAll('button')).find(button => button.textContent === 'Search').click();
  assert.deepEqual(queries, [{ query: 'Emma', page: 1 }]);
  view.querySelector('[name=query]').value = '';
  view.querySelector('[name=query]').dispatchEvent(new dom.window.KeyboardEvent('keydown', { key: 'Enter', bubbles: true, cancelable: true }));
  assert.deepEqual(queries[1], { query: '', page: 1 });
  assert.equal(outerSubmits, 0);
  dom.window.close();
});

test('customer details close on backdrop and Escape outside the dialog, but not on content clicks', () => {
  const dom = new JSDOM('<main></main>');
  const shared = module('../src/manager/manager-shared.ts');
  const { EkomCustomersSectionViewElement } = module('../src/manager/customers-section-view.element.ts', {
    './manager-shared': shared,
    '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => class extends Base { connectedCallback() {} disconnectedCallback() {} } },
  }, { HTMLElement: dom.window.HTMLElement, document: dom.window.document });
  EkomCustomersSectionViewElement.prototype.initialize = async () => {};
  dom.window.customElements.define('ekom-customers-section-view', EkomCustomersSectionViewElement);
  const view = dom.window.document.createElement('ekom-customers-section-view');
  dom.window.document.querySelector('main').appendChild(view);
  const detail = { customer: { identity: 'test', orders: 1, spend: [], firstOrder: '2026-01-01', daysSinceLastOrder: 1 }, orders: [], count: 0, page: 1, totalPages: 0, snapshot: { fields: {}, date: '2026-01-01', orderNumber: '1' } };
  view.detail = detail; view.render();
  view.querySelector('[role=dialog]').click();
  assert.equal(view.detail, detail);
  view.querySelector('.customer-overlay').click();
  assert.equal(view.detail, undefined);
  view.detail = detail; view.render();
  dom.window.document.dispatchEvent(new dom.window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
  assert.equal(view.detail, undefined);
  view.remove(); view.detail = detail;
  dom.window.document.dispatchEvent(new dom.window.KeyboardEvent('keydown', { key: 'Escape', bubbles: true }));
  assert.equal(view.detail, detail);
  dom.window.close();
});

test('customer list shows loading until a slow request completes, and distinguishes empty results from errors', async () => {
  const { view, state } = fixture();
  view.render = Object.getPrototypeOf(view).render.bind(view);
  view.bind = () => {};
  state.filters.store = 'main';
  let resolve;
  view.get = () => new Promise(r => { resolve = r; });
  const request = view.load();
  assert.match(view.innerHTML, /role="status"/);
  assert.match(view.innerHTML, /Loading customers/);
  assert.doesNotMatch(view.innerHTML, /No customers found/);
  resolve(emptyResult);
  await request;
  assert.doesNotMatch(view.innerHTML, /Loading customers/);
  assert.match(view.innerHTML, /No customers found/);
  view.get = async () => { throw new Error('Request failed'); };
  await view.load();
  assert.doesNotMatch(view.innerHTML, /No customers found|Loading customers/);
  assert.match(view.innerHTML, /role="alert"/);
});
