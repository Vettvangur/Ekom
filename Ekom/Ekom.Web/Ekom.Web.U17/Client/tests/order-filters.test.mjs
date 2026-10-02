import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

function loadModule(relativePath, modules = {}) {
  const source = readFileSync(new URL(relativePath, import.meta.url), 'utf8');
  const compiled = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  }).outputText;
  const exports = {};
  runInNewContext(compiled, {
    exports,
    require: name => {
      assert.ok(modules[name], `Unexpected module: ${name}`);
      return modules[name];
    },
    HTMLElement: class {},
    customElements: { define() {} },
  });
  return exports;
}

function createFixture() {
  const shared = loadModule('../src/manager/manager-shared.ts');
  const { EkomOrdersSectionViewElement } = loadModule('../src/manager/orders-section-view.element.ts', {
    './manager-shared': shared,
    '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
    '@umbraco-cms/backoffice/notification': { UMB_NOTIFICATION_CONTEXT: Symbol('notification') },
  });
  const element = new EkomOrdersSectionViewElement();
  const notifications = [];
  element.render = () => {};
  element.showNotification = (...args) => notifications.push(args);
  return { shared, state: shared.managerState, element, notifications };
}

test('the filter overlay includes shipping providers and escaped order-level coupon input', () => {
  const { state, element } = createFixture();
  state.filters.couponCode = 'SPRING"<10>';
  state.filters.shippingProvider = 'shipping-key';
  state.shippingFilterProviders = [{ key: 'shipping-key', title: 'Delivery <Express>' }];
  const html = element.renderFilterOverlay();

  assert.ok(html.includes('data-filter-field="shippingProvider"'));
  assert.ok(html.includes('value="">All shipping providers</option>'));
  assert.ok(html.includes('value="shipping-key" selected>Delivery &lt;Express&gt;</option>'));
  assert.ok(html.includes('data-filter-field="couponCode" value="SPRING&quot;&lt;10&gt;"'));
  assert.ok(html.includes('Exact order-level coupon code'));
});

test('applying the overlay captures both new filters without changing the store', () => {
  const { state, element } = createFixture();
  state.filters.store = 'main';
  element.querySelectorAll = () => [
    { dataset: { filterField: 'couponCode' }, value: 'SPRING10' },
    { dataset: { filterField: 'shippingProvider' }, value: 'shipping-key' },
  ];
  element.applyFilterOverlay();

  assert.equal(state.filters.couponCode, 'SPRING10');
  assert.equal(state.filters.shippingProvider, 'shipping-key');
  assert.equal(state.filters.store, 'main');
});

test('search and both export modes forward the same new filters', async () => {
  const { shared, state } = createFixture();
  state.filters.couponCode = 'SPRING10';
  state.filters.shippingProvider = 'shipping-key';
  state.filters.store = 'main';
  const api = new shared.EkomManagerApi();
  const calls = [];
  api.getJson = async (url, parameters) => { calls.push({ url, parameters }); return {}; };
  api.getBlob = async (url, parameters) => { calls.push({ url, parameters }); return {}; };

  await api.searchOrders(state.filters, 2);
  await api.exportOrders(state.filters, 50, false);
  await api.exportOrders(state.filters, 50, true);

  assert.equal(calls.length, 3);
  for (const { parameters } of calls) {
    assert.equal(parameters.couponCode, 'SPRING10');
    assert.equal(parameters.shippingProvider, 'shipping-key');
    assert.equal(parameters.store, 'main');
  }
  assert.equal(calls[1].parameters.includeOrderLines, false);
  assert.equal(calls[2].parameters.includeOrderLines, true);
});

test('changing stores reloads options and clears both provider filters, not the coupon', async () => {
  const { state, element } = createFixture();
  state.filters.store = 'other';
  state.filters.paymentProvider = 'old-payment';
  state.filters.shippingProvider = 'old-shipping';
  state.filters.couponCode = 'SPRING10';
  element.shippingProviders = [{ key: 'editing-provider', title: 'Order editor provider' }];
  element.api.paymentProviders = async store => {
    assert.equal(store, 'other');
    return [{ key: 'new-payment', title: 'Card' }];
  };
  element.api.shippingProviders = async store => {
    assert.equal(store, 'other');
    return [{ key: 'new-shipping', title: 'Delivery' }];
  };
  await element.loadFilterProviders(true);

  assert.equal(state.filters.paymentProvider, '');
  assert.equal(state.filters.shippingProvider, '');
  assert.equal(state.filters.couponCode, 'SPRING10');
  assert.equal(state.shippingFilterProviders[0].key, 'new-shipping');
  assert.equal(element.shippingProviders[0].key, 'editing-provider');
});

test('loading options preserves valid shipping selections and removes unavailable ones', async () => {
  const { state, element } = createFixture();
  state.filters.store = 'main';
  state.filters.shippingProvider = 'available';
  element.api.paymentProviders = async () => [];
  element.api.shippingProviders = async () => [{ key: 'available', title: 'Delivery' }];
  await element.loadFilterProviders();
  assert.equal(state.filters.shippingProvider, 'available');

  element.api.shippingProviders = async () => [];
  await element.loadFilterProviders();
  assert.equal(state.filters.shippingProvider, '');
});

test('without a selected store, provider filters and options are cleared without requests', async () => {
  const { state, element } = createFixture();
  state.filters.paymentProvider = 'old-payment';
  state.filters.shippingProvider = 'old-shipping';
  state.shippingFilterProviders = [{ key: 'old-shipping', title: 'Delivery' }];
  element.api.paymentProviders = async () => assert.fail('Unexpected provider request');
  element.api.shippingProviders = async () => assert.fail('Unexpected provider request');
  await element.loadFilterProviders();

  assert.equal(state.filters.paymentProvider, '');
  assert.equal(state.filters.shippingProvider, '');
  assert.equal(state.shippingFilterProviders.length, 0);
});

test('late provider responses from the previous store cannot replace current options', async () => {
  const { state, element } = createFixture();
  let resolveOld;
  const oldProviders = new Promise(resolve => { resolveOld = resolve; });
  element.api.paymentProviders = async () => [];
  element.api.shippingProviders = store => store === 'old'
    ? oldProviders
    : Promise.resolve([{ key: 'current', title: 'Current delivery' }]);
  state.filters.store = 'old';
  const oldRequest = element.loadFilterProviders();
  state.filters.store = 'new';
  await element.loadFilterProviders(true);
  resolveOld([{ key: 'obsolete', title: 'Previous delivery' }]);
  await oldRequest;

  assert.equal(state.shippingFilterProviders[0].key, 'current');
});

test('old options are removed and filter controls disabled while a new store loads', async () => {
  const { state, element } = createFixture();
  state.filters.store = 'new';
  state.shippingFilterProviders = [{ key: 'old-provider', title: 'Old delivery' }];
  let resolveProviders;
  element.api.paymentProviders = async () => [];
  element.api.shippingProviders = () => new Promise(resolve => { resolveProviders = resolve; });
  const pending = element.loadFilterProviders(true);

  assert.equal(state.shippingFilterProviders.length, 0);
  const html = element.renderFilterOverlay();
  assert.ok(!html.includes('old-provider'));
  assert.ok(html.includes('data-filter-field="shippingProvider" disabled'));
  assert.ok(html.includes('data-action="apply-filter" disabled'));
  resolveProviders([{ key: 'new-provider', title: 'New delivery' }]);
  await pending;
  assert.equal(element.filterProvidersLoading, false);
});

test('shipping option failure does not prevent loading orders during initialization', async () => {
  const { state, element, notifications } = createFixture();
  element.api.statusList = async () => [];
  element.api.stores = async () => [{ alias: 'main', title: 'Main store' }];
  element.api.paymentProviders = async () => [{ key: 'card', title: 'Card' }];
  element.api.shippingProviders = async () => { throw new Error('Unavailable'); };
  let ordersLoaded = false;
  element.loadOrders = async () => { ordersLoaded = true; };
  await element.initialize();

  assert.equal(ordersLoaded, true);
  assert.equal(state.paymentProviders[0].key, 'card');
  assert.equal(state.shippingFilterProviders.length, 0);
  assert.equal(notifications[0][0], 'warning');
});

test('shipping option failure during a store change still reloads the order list', async () => {
  const { state, element, notifications } = createFixture();
  state.filters.store = 'old';
  state.filters.shippingProvider = 'old-provider';
  element.api.paymentProviders = async () => [];
  element.api.shippingProviders = async () => { throw new Error('Unavailable'); };
  let loadedStore;
  element.loadOrders = async () => { loadedStore = state.filters.store; };
  await element.handleFieldChange({ currentTarget: { dataset: { field: 'store' }, value: 'new' } });

  assert.equal(loadedStore, 'new');
  assert.equal(state.filters.shippingProvider, '');
  assert.equal(state.shippingFilterProviders.length, 0);
  assert.equal(element.filterProvidersLoading, false);
  assert.equal(notifications.length, 1);
});
