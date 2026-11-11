import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

function loadModule(path, modules = {}, globals = {}) {
  const source = readFileSync(new URL(path, import.meta.url), 'utf8');
  const compiled = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  }).outputText;
  const exports = {};
  runInNewContext(compiled, {
    exports,
    require: name => { assert.ok(modules[name], `Unexpected module: ${name}`); return modules[name]; },
    HTMLElement: class {},
    customElements: { define() {} },
    URLSearchParams,
    Error,
    ...globals,
  });
  return exports;
}

const shared = loadModule('../src/manager/manager-shared.ts');
shared.renderManagerContent = (root, html) => { root.innerHTML = html; };
const { EkomOrdersSectionViewElement } = loadModule('../src/manager/orders-section-view.element.ts', {
  './manager-shared': shared,
  '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
  '@umbraco-cms/backoffice/notification': { UMB_NOTIFICATION_CONTEXT: Symbol('notification') },
});

function createElement() {
  const element = new EkomOrdersSectionViewElement();
  element.render = () => {};
  element.showSuccess = () => {};
  element.showError = message => assert.fail(message);
  element.loadOrders = async () => {};
  element.loadOrderLogs = async () => {};
  element.loadOrderActions = async () => {};
  element.loadOrderLockStatus = async () => {};
  element.api.giftcardRecovery = async () => [];
  element.api.orderInfo = async uniqueId => ({ uniqueId, orderStatus: 'Pending' });
  element.querySelector = () => ({ value: 'Processed' });
  element.querySelectorAll = () => [];
  return element;
}

function checkboxIsChecked(element) {
  const html = element.renderOrderDetails(element.selectedOrder);
  return /data-field="notifyOrderStatus"[^>]*\bchecked\b/.test(html);
}

for (const [label, config, rowNotify, detailNotify] of [
  ['missing', {}, true, false],
  ['null', { managerOrderStatusFireEventsByDefault: null }, true, false],
  ['true', { managerOrderStatusFireEventsByDefault: true }, true, true],
  ['false', { managerOrderStatusFireEventsByDefault: false }, false, false],
]) {
  test(`${label} config applies the correct row and detail event defaults`, async () => {
    const element = createElement();
    element.api.config = async () => config;
    await element.loadConfig();
    await element.openOrder('order-A');
    assert.equal(checkboxIsChecked(element), detailNotify);
    const calls = [];
    element.api.changeOrderStatus = async (...args) => { calls.push(args); return true; };
    await element.changeRowStatus({ dataset: { orderId: 'order-A' }, value: 'Processed' });
    await element.saveOverlayStatus();
    assert.deepEqual(calls, [
      ['order-A', 'Processed', rowNotify],
      ['order-A', 'Processed', detailNotify],
    ]);
  });
}

for (const defaultValue of [true, false]) {
  test(`checkbox overrides ${defaultValue}, survives rerenders and saves, resets per opened order`, async () => {
    const element = createElement();
    element.api.config = async () => ({ managerOrderStatusFireEventsByDefault: defaultValue });
    await element.loadConfig();
    await element.openOrder('order-A');
    await element.handleFieldChange({ currentTarget: { dataset: { field: 'notifyOrderStatus' }, checked: !defaultValue } });
    element.bindEvents = () => {};
    EkomOrdersSectionViewElement.prototype.render.call(element);
    assert.equal(checkboxIsChecked(element), !defaultValue);
    element.api.changeOrderStatus = async (id, status, notify) => {
      assert.equal(notify, !defaultValue);
      return true;
    };
    await element.saveOverlayStatus();
    assert.equal(checkboxIsChecked(element), !defaultValue);
    await element.openOrder('order-B');
    assert.equal(checkboxIsChecked(element), defaultValue);
    await element.handleFieldChange({ currentTarget: { dataset: { field: 'notifyOrderStatus' }, checked: !defaultValue } });
    await element.openOrder('order-B');
    assert.equal(checkboxIsChecked(element), defaultValue, 'Reopening the same order also resets its override');
  });
}

test('config uses the existing authenticated backoffice endpoint', async () => {
  const { EkomManagerApi } = loadModule('../src/manager/manager-shared.ts', {}, {
    fetch: async (url, options) => {
      assert.equal(url, '/ekom/backoffice/Config');
      assert.equal(options.credentials, 'same-origin');
      return { ok: true, json: async () => ({ managerOrderStatusFireEventsByDefault: false }) };
    },
  });
  assert.equal((await new EkomManagerApi().config()).managerOrderStatusFireEventsByDefault, false);
});

test('pending and failed config disable both status paths and failures remain visible after searching', async () => {
  const element = createElement();
  await element.openOrder('order-A');
  let rejectConfig;
  element.api.config = () => new Promise((resolve, reject) => { rejectConfig = reject; });
  element.api.changeOrderStatus = async () => assert.fail('Must not update status without config');
  const loading = element.loadConfig();
  await element.changeRowStatus({ dataset: { orderId: 'order-A' }, value: 'Processed' });
  await element.saveOverlayStatus();
  assert.doesNotMatch(element.renderOrderRow({ uniqueId: 'order-A' }), /data-action="change-row-status"/);
  assert.match(element.renderOrderDetails(element.selectedOrder), /data-action="save-fulfillment"[^>]*disabled/);
  rejectConfig(new Error('offline'));
  await loading;
  await element.changeRowStatus({ dataset: { orderId: 'order-A' }, value: 'Processed' });
  await element.saveOverlayStatus();
  element.bindEvents = () => {};
  EkomOrdersSectionViewElement.prototype.render.call(element);
  assert.match(element.innerHTML, /role="alert">Unable to load Ekom configuration/);
  assert.match(element.renderOrderDetails(element.selectedOrder), /role="alert">Unable to load Ekom configuration/);
  shared.managerState.filters.store = 'store';
  element.api.searchOrders = async () => ({ orders: [], count: 0, totalPages: 0 });
  await EkomOrdersSectionViewElement.prototype.loadOrders.call(element);
  assert.match(element.configError, /Order status updates are disabled/);
});

const conflictGuidance = 'Checkout is still processing. Wait for it to finish before changing the order status.';

function createStatusApi(responseStatus, requests, persistedOrder) {
  const { EkomManagerApi } = loadModule('../src/manager/manager-shared.ts', {}, {
    fetch: async (url, options) => {
      const request = new URL(url, 'https://example.test');
      requests.push({ request, options });
      if (request.pathname === '/ekom/manager/changeOrderStatus') {
        return new Response(JSON.stringify(responseStatus === 409 ? { message: conflictGuidance } : true), {
          status: responseStatus,
          headers: { 'Content-Type': 'application/json' },
        });
      }
      assert.equal(request.pathname, '/ekom/manager/SearchOrders');
      return new Response(JSON.stringify({ orders: [persistedOrder], count: 1, totalPages: 1 }));
    },
  });
  return new EkomManagerApi();
}

test('table 409 displays API guidance, never reports success, and reloads persisted status', async () => {
  const element = createElement();
  const requests = [];
  const errors = [];
  const persistedOrder = { uniqueId: 'order-A', orderStatusCol: 'Pending' };
  element.config = { managerOrderStatusFireEventsByDefault: false };
  element.result = { orders: [persistedOrder], count: 1, totalPages: 1 };
  element.api = createStatusApi(409, requests, persistedOrder);
  element.showSuccess = () => assert.fail('A conflict must not report success');
  element.showError = message => errors.push(message);
  element.loadOrders = EkomOrdersSectionViewElement.prototype.loadOrders;
  shared.managerState.filters.store = 'store';
  shared.managerState.statusList = [{ value: 'Pending', label: 'Pending' }, { value: 'Processed', label: 'Processed' }];
  let renderedRow = '';
  element.render = () => { renderedRow = element.renderOrderRow(element.result.orders[0]); };

  await element.changeRowStatus({ dataset: { orderId: 'order-A' }, value: 'Processed' });

  assert.deepEqual(errors, [conflictGuidance]);
  assert.equal(persistedOrder.orderStatusCol, 'Pending');
  assert.equal(element.result.orders[0].orderStatusCol, 'Pending');
  assert.match(renderedRow, /data-action="view-order"/);
  assert.doesNotMatch(renderedRow, /value="Processed" selected/);
  assert.equal(requests.filter(request => request.options.method === 'POST').length, 1, 'Conflict must not retry the status change');
  assert.equal(requests[0].options.method, 'POST');
  assert.equal(requests[0].options.credentials, 'same-origin');
  assert.equal(requests[0].request.searchParams.get('notify'), 'false');
});

for (const notify of [true, false]) {
  test(`detail 409 restores saved status and preserves Fire events override ${notify}`, async () => {
    const element = createElement();
    const requests = [];
    const errors = [];
    const select = { value: 'Processed' };
    element.config = { managerOrderStatusFireEventsByDefault: !notify };
    element.selectedOrder = { uniqueId: 'order-A', orderStatus: 'Pending' };
    await element.handleFieldChange({ currentTarget: { dataset: { field: 'notifyOrderStatus' }, checked: notify } });
    element.querySelector = () => select;
    element.api = createStatusApi(409, requests);
    element.showSuccess = () => assert.fail('A conflict must not report success');
    element.showError = message => errors.push(message);
    element.loadOrders = async () => assert.fail('A rejected detail update must not take the success path');

    await element.saveOverlayStatus();

    assert.deepEqual(errors, [conflictGuidance]);
    assert.equal(element.selectedOrder.orderStatus, 'Pending');
    assert.equal(select.value, 'Pending');
    assert.equal(element.notifyOrderStatus, notify);
    assert.equal(checkboxIsChecked(element), notify);
    assert.equal(requests.length, 1);
    assert.equal(requests[0].request.searchParams.get('notify'), String(notify));
    assert.equal(requests[0].request.searchParams.get('orderStatus'), 'Processed');
  });
}

for (const view of ['table', 'detail']) {
  test(`${view} successful status update still uses the API and reports success`, async () => {
    const element = createElement();
    const requests = [];
    const successes = [];
    const order = { uniqueId: 'order-A', orderStatus: 'Pending', orderStatusCol: 'Pending' };
    element.config = { managerOrderStatusFireEventsByDefault: true };
    element.selectedOrder = order;
    element.result = { orders: [order], count: 1, totalPages: 1 };
    element.api = createStatusApi(200, requests);
    element.showSuccess = message => successes.push(message);
    if (view === 'table') {
      await element.changeRowStatus({ dataset: { orderId: 'order-A' }, value: 'Processed' });
      assert.equal(order.orderStatusCol, 'Processed');
    } else {
      await element.saveOverlayStatus();
      assert.equal(order.orderStatus, 'Processed');
    }
    assert.deepEqual(successes, ['Order status updated.']);
    assert.equal(requests.length, 1);
    assert.equal(requests[0].request.searchParams.get('orderStatus'), 'Processed');
  });
}


test('status-save refreshes activity and actions without intermediate drawer renders', async () => {
  const element = createElement();
  element.stateSaving = true;
  let renders = 0;
  element.render = () => renders++;
  element.api.orderLogs = async () => [{ message: 'Updated' }];
  element.api.orderActions = async () => [];
  await Promise.all([
    EkomOrdersSectionViewElement.prototype.loadOrderLogs.call(element, 'order-A'),
    EkomOrdersSectionViewElement.prototype.loadOrderActions.call(element, 'order-A'),
  ]);
  assert.equal(renders, 0);
  assert.equal(element.orderLogs[0].message, 'Updated');
  assert.equal(element.orderLogsLoading, false);
  assert.equal(element.orderActionsLoading, false);
});


test('polling preserves unchanged rows and pauses for editing, hidden pages and active requests', async () => {
  const page = { hidden: false, activeElement: null };
  const timers = [];
  const cleared = [];
  const { EkomOrdersSectionViewElement: View } = loadModule('../src/manager/orders-section-view.element.ts', {
    './manager-shared': shared,
    '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
    '@umbraco-cms/backoffice/notification': { UMB_NOTIFICATION_CONTEXT: Symbol('notification') },
  }, { document: page, window: { setInterval: (fn, ms) => { timers.push({ fn, ms }); return timers.length; }, clearInterval: id => cleared.push(id) } });
  const view = new View();
  view.isConnected = true;
  view.contains = () => false;
  view.querySelector = () => null;
  view.loading = false;
  let renders = 0, requests = 0;
  view.render = () => renders++;
  shared.managerState.filters.store = 'Store';
  shared.managerState.filters.currency = 'USD';
  view.api.searchOrders = async filters => { requests++; assert.equal(filters.currency, 'USD'); return { orders: [], count: 0, totalPages: 0 }; };
  view.api.orderViewCounts = async (filters, views) => {
    requests++;
    assert.equal(filters.currency, 'USD');
    assert.deepEqual(views, shared.orderViews.map(view => view.key).filter(key => key !== filters.orderStatus));
    return views.map(view => ({ view, count: 0 }));
  };
  view.viewCounts = Object.fromEntries(shared.orderViews.map(view => [view.key, 0]));
  view.setPolling(2);
  assert.equal(timers[0].ms, 2000);
  await view.pollOrders();
  assert.equal(requests, 2, 'one list search plus one request for every other view count');
  assert.equal(renders, 1, 'unchanged results must not redraw');
  view.overlay = 'order'; await view.pollOrders();
  view.overlay = ''; page.hidden = true; await view.pollOrders();
  page.hidden = false; view.loading = true; await view.pollOrders();
  assert.equal(requests, 2);
  view.loading = false;
  view.api.searchOrders = async () => ({ orders: [], count: 1, totalPages: 1 });
  await view.pollOrders();
  assert.equal(view.result.count, 1);
  assert.equal(renders, 2);
  view.setPolling(0);
  assert.ok(cleared.includes(1));
});

test('order arrow navigation respects direction, editing and unsaved status', () => {
  const { EkomOrdersSectionViewElement: View } = loadModule('../src/manager/orders-section-view.element.ts', {
    './manager-shared': shared,
    '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
    '@umbraco-cms/backoffice/notification': { UMB_NOTIFICATION_CONTEXT: Symbol('notification') },
  });
  const view = new View();
  view.overlay = 'order';
  view.selectedOrder = { status: { fulfillmentStatus: 'Picked' } };
  const clicked = [];
  let pending = 'Picked', editing = false;
  view.querySelector = selector => selector === 'details[open]' ? null
    : selector.includes('fulfillmentState') ? { value: pending }
    : { disabled: false, click: () => clicked.push(selector.includes('Previous') ? 'previous' : 'next') };
  const press = key => view.handleKeyDown({ key, composedPath: () => [{ closest: () => editing }], preventDefault() {} });
  press('ArrowLeft'); press('ArrowUp'); press('ArrowRight'); press('ArrowDown');
  assert.deepEqual(clicked, ['previous', 'previous', 'next', 'next']);
  editing = true; press('ArrowRight'); editing = false;
  pending = 'Packed'; press('ArrowRight'); pending = 'Picked';
  view.customerEditorOpen = true; press('ArrowRight'); view.customerEditorOpen = false;
  view.stateSaving = true; press('ArrowRight');
  assert.equal(clicked.length, 4);
});

test('inline fulfillment uses revision and checkbox, refreshes after success and conflicts', async () => {
  const view = createElement();
  view.config = { managerOrderStatusFireEventsByDefault: true };
  view.result = { orders: [{ uniqueId: 'order-1', status: { orderStatus: 'Open', fulfillmentStatus: 'Picked', revision: 7 } }] };
  let checked = false, calls = [], refreshes = 0, errors = [];
  view.loadOrders = async () => { refreshes++; };
  view.showError = message => errors.push(message);
  view.api.changeOrderStates = async (...args) => calls.push(args);
  const button = { disabled: false, dataset: { orderId: 'order-1', value: 'Fulfilled' }, closest: () => ({ querySelector: () => ({ checked }), querySelectorAll: () => [] }) };
  await view.changeRowFulfillment(button);
  assert.deepEqual(calls[0], ['order-1', 7, 'Fulfill', false]);
  assert.equal(refreshes, 1);
  assert.equal(view.stateSaving, false);
  checked = true;
  view.api.changeOrderStates = async (...args) => { calls.push(args); throw new Error('Order changed; reload.'); };
  await view.changeRowFulfillment(button);
  assert.equal(calls[1][3], true);
  assert.equal(errors.length, 1);
  assert.equal(refreshes, 2);
  view.result.orders[0].status.orderStatus = 'Closed';
  await view.changeRowFulfillment(button);
  assert.equal(calls.length, 2);
});


test('fulfillment menus dismiss outside, retain inside clicks, and Escape preserves the order drawer', () => {
  const element = createElement();
  let focused = false;
  const menu = { open: true, querySelector: () => ({ focus: () => { focused = true; } }) };
  element.overlay = 'order';
  element.querySelectorAll = () => [menu];
  element.dismissFulfillmentMenus({ composedPath: () => [{}, menu] });
  assert.equal(menu.open, true);
  element.dismissFulfillmentMenus({ composedPath: () => [{}] });
  assert.equal(menu.open, false);
  menu.open = true;
  element.querySelector = () => menu;
  let prevented = false;
  let stopped = false;
  element.handleKeyDown({ key: 'Escape', preventDefault: () => { prevented = true; }, stopPropagation: () => { stopped = true; } });
  assert.equal(menu.open, false);
  assert.equal(element.overlay, 'order');
  assert.equal(focused, true);
  assert.equal(prevented, true);
  assert.equal(stopped, true);
});


test('detail fulfillment saves directly without a blocking native confirmation', async () => {
  const element = createElement();
  element.config = {};
  element.selectedOrder = { uniqueId: 'order-A', status: { revision: 7, fulfillmentStatus: 'Picked' } };
  const calls = [];
  element.api.changeOrderStates = async (...args) => { calls.push(args); return { revision: 8, fulfillmentStatus: 'Fulfilled' }; };
  element.api.orderInfo = async () => ({ uniqueId: 'order-A', status: { revision: 8, fulfillmentStatus: 'Fulfilled' } });
  await element.changeState('Fulfill');
  assert.equal(calls.length, 1);
  assert.equal(calls[0][2], 'Fulfill');
  assert.equal(element.selectedOrder.status.fulfillmentStatus, 'Fulfilled');
  assert.equal(element.stateSaving, false);
});


test('manual payment table uses revision and event choice and detail preserves unsaved fulfillment', async () => {
  const element = createElement();
  element.config = {};
  const order = { uniqueId: 'order-A', referenceId: 3, status: { orderStatus: 'Open', paymentStatus: 'Pending', fulfillmentStatus: 'Picked', revision: 7 } };
  element.result = { orders: [order] };
  const calls = [];
  element.api.changeOrderStates = async (...args) => { calls.push(args); };
  let refreshes = 0;
  element.loadOrders = async () => { refreshes++; };
  const menu = { querySelector: () => ({ checked: false }), querySelectorAll: () => [], removeAttribute() {} };
  const button = { dataset: { orderId: 'order-A', value: 'Paid', detail: 'false' }, closest: () => menu };
  await element.changePayment(button);
  assert.deepEqual(calls, [['order-A', 7, 'PaymentPaid', false]]);
  assert.equal(refreshes, 1);
  button.dataset.value = 'Refunded';
  await element.changePayment(button);
  assert.equal(calls.length, 1);
  element.selectedOrder = order;
  button.dataset.detail = 'true';
  button.dataset.value = 'Paid';
  element.querySelector = () => ({ value: 'Packed' });
  let error = '';
  element.showError = message => { error = message; };
  await element.changePayment(button);
  assert.match(error, /Save your fulfillment change/);
  assert.equal(calls.length, 1);
  const html = element.renderPaymentMenu(order);
  assert.ok(html.includes('Payment') || html.includes('payment'));
  assert.ok(!html.includes('Refunded'));
});
