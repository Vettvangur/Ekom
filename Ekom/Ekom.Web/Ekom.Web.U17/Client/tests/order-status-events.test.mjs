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
  assert.match(element.renderOrderRow({ uniqueId: 'order-A' }), /data-action="change-row-status"[^>]*disabled/);
  assert.match(element.renderOrderDetails(element.selectedOrder), /data-action="save-overlay-status"[^>]*disabled/);
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
  assert.match(renderedRow, /value="Pending" selected/);
  assert.doesNotMatch(renderedRow, /value="Processed" selected/);
  assert.equal(requests.length, 2, 'Conflict reloads orders without retrying the status change');
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
