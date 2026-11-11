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
  shared.renderManagerContent = (root, html) => { root.innerHTML = html; };
  const { EkomCustomersSectionViewElement } = module('../src/manager/customers-section-view.element.ts', {
    './manager-shared': shared, '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
  });
  const view = new EkomCustomersSectionViewElement();
  view.render = () => {};
  view.querySelector = () => null;
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
  assert.match(view.innerHTML, /Loading Customers for /);
  assert.equal((view.innerHTML.match(/class="ekom-loading-card"/g) || []).length, 5);
  assert.match(view.innerHTML, /class="customer-panel" hidden/);
  assert.doesNotMatch(view.innerHTML, /No customers found/);
  resolve(emptyResult);
  await request;
  assert.doesNotMatch(view.innerHTML, /Loading Customers|class="ekom-loading"|class="customer-panel" hidden/);
  assert.match(view.innerHTML, /No customers found/);
  view.get = async () => { throw new Error('Request failed'); };
  await view.load();
  assert.doesNotMatch(view.innerHTML, /No customers found|Loading Customers|class="ekom-loading"/);
  assert.match(view.innerHTML, /role="alert"/);
});


test('canonical custom status scope remains selected in the shared filter', () => {
  const shared = module('../src/manager/manager-shared.ts');
  const value = 'states:Open;Paid;Picked,Packed';
  const document = new JSDOM(`<select>${shared.renderOrderViewOptions(value)}</select>`).window.document;
  assert.equal(document.querySelector('select').value, value);
  assert.equal(document.querySelector('option:checked').textContent, 'Custom status filters');
});

test('modern customer history shows independent statuses including evidence gaps', () => {
  const { view } = fixture();
  const detail = { customer: { identity: 'a', orders: 1, spend: [], firstOrder: '2026-01-01' }, orders: [{ uniqueId: 'x', orderNumber: '1', orderStatusCol: 'ReadyForDispatch', status: { orderStatus: 'Open', paymentStatus: 'Paid', fulfillmentStatus: 'Picked' } }, { uniqueId: 'y', orderStatusCol: 'Closed', status: { orderStatus: 'Closed', paymentStatus: null, fulfillmentStatus: null } }], count: 2, page: 1, totalPages: 1, snapshot: { fields: {} } };
  const dom = new JSDOM(view.renderDetail(detail));
  const badges = [...dom.window.document.querySelectorAll('.ekom-badge')].map(el => el.textContent);
  assert.deepEqual(badges, ['Open', 'Paid', 'Picked', 'Closed', 'Needs review', 'Needs review']);
  assert.ok(!dom.window.document.body.textContent.includes('ReadyForDispatch'));
  dom.window.close();
});

test('the customer drawer opens on a loader, keeps its content while paging, and never sticks open', async () => {
  const { view, state } = fixture();
  view.render = Object.getPrototypeOf(view).render.bind(view);
  view.bind = () => {};
  view.isConnected = true;
  view.querySelectorAll = () => [];
  state.filters.store = 'main';
  view.result = { ...emptyResult, customers: [{ identity: 'A@EXAMPLE.COM', name: 'Anna <b>', email: 'a@example.com', orders: 1, spend: [] }] };
  const detail = { customer: { identity: 'A@EXAMPLE.COM', name: 'Anna', orders: 1, spend: [], firstOrder: '2026-01-01', daysSinceLastOrder: 1 }, orders: [], count: 0, page: 1, totalPages: 0, snapshot: { fields: {}, date: '2026-01-01', orderNumber: '1' } };
  let resolve, reject;
  view.get = () => new Promise((res, rej) => { resolve = res; reject = rej; });

  const opening = view.open('A@EXAMPLE.COM');
  assert.match(view.innerHTML, /aria-busy="true" class="customer-dialog"/);
  assert.match(view.innerHTML, /<h2>Anna &lt;b&gt;<\/h2><p class="ekom-loading-status" role="status">.*Loading customer…/);
  assert.match(view.innerHTML, /data-close/);
  resolve(detail);
  await opening;
  assert.doesNotMatch(view.innerHTML, /Loading customer…|class="customer-dialog" aria-busy/);
  assert.match(view.innerHTML, /Latest order snapshot/);

  // Paging inside the open drawer keeps the customer on screen instead of swapping to the loader.
  const paging = view.open('A@EXAMPLE.COM', 2);
  assert.match(view.innerHTML, /Latest order snapshot/);
  assert.doesNotMatch(view.innerHTML, /Loading customer…/);
  resolve(detail);
  await paging;

  view.close();
  const failing = view.open('A@EXAMPLE.COM');
  assert.match(view.innerHTML, /Loading customer…/);
  reject(new Error('offline'));
  await failing;
  assert.doesNotMatch(view.innerHTML, /Loading customer…|customer-dialog/);
  assert.match(view.innerHTML, /role="alert"/);

  const closing = view.open('A@EXAMPLE.COM');
  view.close();
  resolve(detail);
  await closing;
  assert.doesNotMatch(view.innerHTML, /customer-dialog/);
});

test('a customer handed over from an order opens once the list has loaded, by the configured identity', async () => {
  for (const [identityField, expected] of [['CustomerEmail', 'anna@example.com'], ['CustomerUsername', 'anna']]) {
    const { view, state } = fixture();
    const opened = [];
    view.api.stores = async () => [{ alias: 'main', title: 'Main' }];
    view.api.printingEnabled = async () => false;
    view.get = async () => ({ ...emptyResult, identityField });
    view.open = async identity => { opened.push(identity); };
    state.filters.store = 'main';
    state.pendingCustomer = { email: ' anna@example.com ', username: 'anna' };
    await view.initialize();
    assert.deepEqual(opened, [expected]);
    assert.equal(state.pendingCustomer, undefined);
    await view.initialize();
    assert.deepEqual(opened, [expected], 'the hand-over is used once');
  }
});
