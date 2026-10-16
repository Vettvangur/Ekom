import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

function loadModule(relativePath, modules = {}, globals = {}) {
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
    ...globals,
  });
  return exports;
}

function createFixture(globals = {}) {
  const shared = loadModule('../src/manager/manager-shared.ts');
  const { EkomOrdersSectionViewElement } = loadModule('../src/manager/orders-section-view.element.ts', {
    './manager-shared': shared,
    '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
    '@umbraco-cms/backoffice/notification': { UMB_NOTIFICATION_CONTEXT: Symbol('notification') },
  }, globals);
  const element = new EkomOrdersSectionViewElement();
  const notifications = [];
  element.render = () => {};
  element.showNotification = (...args) => notifications.push(args);
  return { shared, state: shared.managerState, element, notifications };
}

test('the filter overlay includes shipping providers and escaped coupon and gift card inputs', () => {
  const { state, element } = createFixture();
  state.filters.couponCode = 'SPRING"<10>';
  state.filters.giftcardCode = '0012"<3456>';
  state.filters.shippingProvider = 'shipping-key';
  state.shippingFilterProviders = [{ key: 'shipping-key', title: 'Delivery <Express>' }];
  const html = element.renderFilterOverlay();

  assert.ok(html.includes('data-filter-field="shippingProvider"'));
  assert.ok(html.includes('value="">All shipping providers</option>'));
  assert.ok(html.includes('value="shipping-key" selected>Delivery &lt;Express&gt;</option>'));
  assert.ok(html.includes('data-filter-field="couponCode" value="SPRING&quot;&lt;10&gt;"'));
  assert.ok(html.includes('Exact order-level coupon code'));
  assert.ok(html.includes('Gift card number:'));
  assert.ok(html.includes('data-filter-field="giftcardCode" value="0012&quot;&lt;3456&gt;"'));
  assert.ok(html.includes('Full gift card number'));
});

test('applying the overlay captures filters and trims the full gift card number without changing the store', () => {
  const { state, element } = createFixture();
  state.filters.store = 'main';
  element.querySelectorAll = () => [
    { dataset: { filterField: 'couponCode' }, value: 'SPRING10' },
    { dataset: { filterField: 'giftcardCode' }, value: '  001234567890  ' },
    { dataset: { filterField: 'shippingProvider' }, value: 'shipping-key' },
  ];
  element.applyFilterOverlay();

  assert.equal(state.filters.couponCode, 'SPRING10');
  assert.equal(state.filters.giftcardCode, '001234567890');
  assert.equal(state.filters.shippingProvider, 'shipping-key');
  assert.equal(state.filters.store, 'main');
});

test('search and both export modes forward the same new filters', async () => {
  const { shared, state } = createFixture();
  state.filters.couponCode = 'SPRING10';
  state.filters.giftcardCode = '001234567890';
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
    assert.equal(parameters.giftcardCode, '001234567890');
    assert.equal(parameters.shippingProvider, 'shipping-key');
    assert.equal(parameters.store, 'main');
  }
  assert.equal(calls[1].parameters.includeOrderLines, false);
  assert.equal(calls[2].parameters.includeOrderLines, true);
});

test('the gift card filter defaults to empty and can be cleared with whitespace', () => {
  const { state, element } = createFixture();
  assert.equal(state.filters.giftcardCode, '');
  state.filters.giftcardCode = '001234567890';
  element.querySelectorAll = () => [{ dataset: { filterField: 'giftcardCode' }, value: '   ' }];
  element.applyFilterOverlay();
  assert.equal(state.filters.giftcardCode, '');
});

test('reservation actions require confirmation and refresh the order, logs and available actions', async () => {
  const confirmations = [];
  const { element } = createFixture({ window: { confirm: message => { confirmations.push(message); return true; } } });
  const key = 'ekom.release-checkout:token';
  element.selectedOrder = { uniqueId: 'order-id', orderStatus: 'WaitingForPayment' };
  element.orderActions = [{ key, label: 'Release checkout reservations', enabled: true, confirmMessage: 'Release all reservations?' }];
  const calls = [];
  element.api.executeOrderAction = async (orderId, actionKey) => {
    calls.push(['execute', orderId, actionKey]);
    return { blob: async () => ({ text: async () => '{"message":"Reservations released successfully."}' }), headers: { get: () => '' } };
  };
  element.api.orderInfo = async orderId => {
    calls.push(['order', orderId]);
    return { uniqueId: orderId, orderStatus: 'Incomplete' };
  };
  element.loadOrderLogs = async orderId => { calls.push(['logs', orderId]); };
  element.loadOrderActions = async orderId => { calls.push(['actions', orderId]); };

  await element.executeOrderAction(key);

  assert.deepEqual(confirmations, ['Release all reservations?']);
  assert.deepEqual(calls, [['execute', 'order-id', key], ['order', 'order-id'], ['logs', 'order-id'], ['actions', 'order-id']]);
  assert.equal(element.selectedOrder.orderStatus, 'Incomplete');
  assert.equal(element.executingActionKey, '');
});

test('declining a gift card release confirmation does not send the action', async () => {
  const { element } = createFixture({ window: { confirm: () => false } });
  const key = 'ekom.release-giftcards:token';
  element.selectedOrder = { uniqueId: 'order-id' };
  element.orderActions = [{ key, confirmMessage: 'Release the gift card reservation?' }];
  element.api.executeOrderAction = async () => assert.fail('Declined action must not execute');

  await element.executeOrderAction(key);

  assert.equal(element.executingActionKey, '');
});

test('legacy gift card input trims state and reaches search and both export query strings', async () => {
  const registrations = {};
  const calls = [];
  const handlers = {};
  const angular = {
    module: () => ({
      factory: (name, dependencies) => { registrations[name] = dependencies.at(-1); },
      controller: (name, dependencies) => { registrations[name] = dependencies.at(-1); },
    }),
    noop() {},
    isDate: value => value instanceof Date,
    isString: value => typeof value === 'string',
    forEach: (object, callback) => Object.entries(object).forEach(([key, value]) => callback(value, key)),
    element: () => ({ ready() {} }),
  };
  const document = {
    body: { appendChild() {}, classList: { remove() {} } },
    createElement: () => ({}),
  };
  const base = '../../../Ekom.Web/App_Plugins/Ekom/Manager/';
  for (const file of [
    'services/ekmManager.state.js',
    'controllers/ekmFilter.controller.js',
    'controllers/ekmManager.controller.js',
    'resources/ekmManager.resources.js',
  ]) {
    runInNewContext(readFileSync(new URL(base + file, import.meta.url), 'utf8'), {
      angular,
      document,
      URL: { createObjectURL: () => 'blob:export' },
      Umbraco: { Sys: { ServerVariables: { ekom: { managerEndpoint: '/ekom/manager/', backofficeApiEndpoint: '/ekom/backoffice/' } } } },
    });
  }
  const state = registrations['Ekom.Manager.State']();
  assert.equal(state.getState().filters.giftcardCode, '');
  const resources = registrations['Ekom.Manager.Resources'](async request => {
    calls.push(request);
    if (request.url === '/ekom/backoffice/Config') return { data: {} };
    return { data: { count: 1, orders: [] } };
  });
  const events = {
    on: (name, callback) => { handlers[name] = callback; },
    emit: (name, args) => handlers[name](null, args),
  };
  const dashboard = { $on() {} };
  registrations['Ekom.Manager.Dashboard'](
    dashboard, { error: () => assert.fail('Unexpected legacy request error') }, resources,
    { path: () => '/orders' }, { on() {} }, events, state, {}, () => {}, {},
  );
  const filter = {};
  registrations['Ekom.Manager.Filter'](filter, events, state);
  filter.filters.giftcardCode = '  001234567890  ';
  filter.onGiftcardCodeChanged();
  assert.equal(state.getState().filters.giftcardCode, '001234567890');
  await dashboard.GetData();
  await dashboard.ExportCsv(null, false);
  await dashboard.ExportCsv(null, true);

  const configCalls = calls.filter(call => call.url === '/ekom/backoffice/Config');
  const orderCalls = calls.filter(call => call.url !== '/ekom/backoffice/Config');
  assert.equal(configCalls.length, 1);
  assert.equal(orderCalls.length, 4);
  for (const call of orderCalls) {
    assert.equal(new URL(call.url, 'https://example.test').searchParams.get('giftcardCode'), '001234567890');
  }
  assert.equal(new URL(orderCalls[2].url, 'https://example.test').searchParams.get('includeOrderLines'), 'false');
  assert.equal(new URL(orderCalls[3].url, 'https://example.test').searchParams.get('includeOrderLines'), 'true');
  filter.filters.giftcardCode = '   ';
  filter.onGiftcardCodeChanged();
  assert.equal(state.getState().filters.giftcardCode, '');

  const html = readFileSync(new URL(base + 'views/overlays/ekmFilter.html', import.meta.url), 'utf8');
  assert.ok(html.includes('Gift card number:'));
  assert.ok(html.includes('ng-model="filters.giftcardCode"'));
  assert.ok(html.includes('ng-change="onGiftcardCodeChanged()"'));
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
