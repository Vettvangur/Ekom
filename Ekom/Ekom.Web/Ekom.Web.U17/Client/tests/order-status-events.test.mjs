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
