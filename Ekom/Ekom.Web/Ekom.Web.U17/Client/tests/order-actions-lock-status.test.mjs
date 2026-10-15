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
    require: name => { assert.ok(modules[name], `Unexpected module: ${name}`); return modules[name]; },
    HTMLElement: class {},
    customElements: { define() {} },
    window: { confirm: () => true },
  });
  return exports;
}

const shared = loadModule('../src/manager/manager-shared.ts');
const { EkomOrdersSectionViewElement } = loadModule('../src/manager/orders-section-view.element.ts', {
  './manager-shared': shared,
  '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
  '@umbraco-cms/backoffice/notification': { UMB_NOTIFICATION_CONTEXT: Symbol('notification') },
});
const legacyRoot = '../../../Ekom.Web/App_Plugins/Ekom/Manager/';
const legacyController = readFileSync(new URL(`${legacyRoot}controllers/ekmOrder.controller.js`, import.meta.url), 'utf8');
const legacyTemplate = readFileSync(new URL(`${legacyRoot}views/overlays/ekmOrder.html`, import.meta.url), 'utf8');
const locked = { hasLock: true, paymentOperationLocked: true, preparationLocked: true, reconciliationRequired: true, attemptId: 'attempt-1', attemptState: 'Uncertain' };
const actions = [
  { key: 'global', label: 'Print', enabled: true },
  { key: 'release-A', label: 'Release A', giftcardCode: 'Card-A', enabled: true, confirmMessage: 'Release this hold?' },
  { key: 'release-B', label: 'Release B', giftcardCode: 'Card-B', enabled: false, disabledReason: '<unsafe> & "no hold"' },
  { key: 'wrong-case', label: 'Wrong case', giftcardCode: 'card-a', enabled: true },
  { key: 'missing-card', label: 'Missing card', giftcardCode: 'Card-C', enabled: true },
];

function createElement() {
  const element = new EkomOrdersSectionViewElement();
  element.render = () => {};
  element.orderActions = actions;
  element.selectedOrder = { uniqueId: 'order-A', giftcards: [{ code: 'Card-A', amount: 10 }, { code: 'Card-B', amount: 20 }], currency: 'USD' };
  return element;
}

function deferred() {
  let resolve, reject;
  const promise = new Promise((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
}

test('each card renders only its exact matching actions, never in Order Actions', () => {
  const element = createElement();
  const html = element.renderGiftcards(element.selectedOrder);
  const rows = html.match(/<li>[\s\S]*?<\/li>/g);
  assert.equal(rows.length, 2);
  assert.match(rows[0], /Release A/);
  assert.doesNotMatch(rows[0], /Release B|Wrong case|Missing card|Print/);
  assert.match(rows[1], /Release B/);
  assert.doesNotMatch(rows[1], /Release A/);
  assert.match(element.renderOrderActions(), /Print/);
  assert.doesNotMatch(element.renderOrderActions(), /Release|Wrong case|Missing card/);
  element.orderActions = actions.slice(1);
  assert.equal(element.renderOrderActions(), '');
});

test('disabled reasons are visible escaped text and card actions show their loading state', () => {
  const element = createElement();
  element.executingActionKey = 'release-A';
  const html = element.renderGiftcards(element.selectedOrder);
  assert.match(html, /disabled>Release A \(Working\.\.\.\)/);
  assert.match(html, /disabled>Release B/);
  assert.match(html, /<small[^>]*>&lt;unsafe&gt; &amp; &quot;no hold&quot;<\/small>/);
  assert.doesNotMatch(html, /<unsafe>/);
});

test('lock panel is beneath payment and above actions, displays blockers and escaped attempt details', () => {
  const element = createElement();
  element.orderLockStatus = { ...locked, attemptState: '<uncertain>' };
  const html = element.renderOrderDetails(element.selectedOrder);
  assert.ok(html.indexOf('Payment:') < html.indexOf('Checkout lock'));
  assert.ok(html.indexOf('Checkout lock') < html.indexOf('Order Actions'));
  for (const text of ['ownership retained', 'Payment operation', 'Checkout preparation', 'Reconciliation required', 'attempt-1', '&lt;uncertain&gt;', 'does not automatically expire', 'Interrupted or uncertain', 'Refresh status']) assert.ok(html.includes(text), text);
  assert.doesNotMatch(html, /force unlock|ownerToken|heartbeat|stale/i);
  element.orderLockStatus = { ...locked, hasLock: false };
  assert.equal(element.renderOrderLockStatus(), '');
});

test('refresh updates lock status, keeps failures visible, and never calls failure unlocked', async () => {
  const element = createElement();
  let calls = 0;
  element.api.orderLockStatus = async id => { assert.equal(id, 'order-A'); calls++; return locked; };
  await element.loadOrderLockStatus('order-A');
  assert.equal(calls, 1);
  assert.equal(element.orderLockStatus, locked);
  element.api.orderLockStatus = async () => { throw new Error('offline'); };
  await element.loadOrderLockStatus('order-A');
  assert.match(element.renderOrderLockStatus(), /role="alert"/);
  assert.match(element.renderOrderLockStatus(), /Current ownership is unknown/);
  assert.equal(element.orderLockStatus, locked, 'Keep last known lock on failure');
  element.orderLockStatus = undefined;
  assert.match(element.renderOrderLockStatus(), /Refresh status/);
});

test('a reconciliation-only block does not claim payment or preparation ownership is retained', () => {
  const element = createElement();
  element.orderLockStatus = { ...locked, paymentOperationLocked: false, preparationLocked: false };
  const html = element.renderOrderLockStatus();
  assert.match(html, /Checkout lock/);
  assert.match(html, /Reconciliation required/);
  assert.doesNotMatch(html, /ownership retained/);
});

test('lock responses and errors for old orders cannot overwrite the selected order', async () => {
  for (const fail of [false, true]) {
    const element = createElement();
    const pending = deferred();
    element.api.orderLockStatus = () => pending.promise;
    const old = element.loadOrderLockStatus('order-A');
    element.selectedOrder = { uniqueId: 'order-B' };
    element.orderRequestId++;
    element.api.orderLockStatus = async () => ({ ...locked, hasLock: false });
    await element.loadOrderLockStatus('order-B');
    fail ? pending.reject(new Error('old failure')) : pending.resolve(locked);
    await old;
    assert.equal(element.orderLockStatus.hasLock, false);
    assert.equal(element.orderLockError, false);
    assert.equal(element.orderLockLoading, false);
  }
});

test('opening another order clears lock and card state before fetching and ignores old order info', async () => {
  const element = createElement();
  element.orderLockStatus = locked;
  const old = deferred();
  element.api.orderInfo = id => id === 'old' ? old.promise : Promise.resolve({ uniqueId: id });
  element.api.orderLogs = async () => [];
  element.api.orderActions = async () => [];
  element.api.orderLockStatus = async () => ({ ...locked, hasLock: false });
  const first = element.openOrder('old');
  assert.equal(element.orderLockStatus, undefined);
  assert.equal(element.orderActions.length, 0);
  await element.openOrder('new');
  old.resolve({ uniqueId: 'old' });
  await first;
  assert.equal(element.selectedOrder.uniqueId, 'new');
  assert.equal(element.orderLockStatus.hasLock, false);
});

test('successful generic card actions refetch order, actions, logs, and lock status; disabled actions do not execute', async () => {
  const element = createElement();
  const calls = [];
  element.showSuccess = () => {};
  element.api.executeOrderAction = async (id, key) => {
    calls.push(key);
    return { blob: async () => ({ text: async () => '{}' }), headers: { get: () => '' } };
  };
  element.api.orderInfo = async id => { calls.push('info'); return { uniqueId: id }; };
  element.api.orderActions = async () => { calls.push('actions'); return actions; };
  element.api.orderLogs = async () => { calls.push('logs'); return []; };
  element.api.orderLockStatus = async () => { calls.push('lock'); return locked; };
  await element.executeOrderAction('release-B');
  assert.equal(calls.length, 0);
  await element.executeOrderAction('release-A');
  for (const call of ['release-A', 'info', 'actions', 'logs', 'lock']) assert.ok(calls.includes(call), call);
  assert.equal(element.executingActionKey, '');
});

function createLegacy(resources = {}) {
  let controller;
  runInNewContext(legacyController, {
    angular: { module: () => ({ controller: (_name, dependencies) => { controller = dependencies.at(-1); } }) },
    document: { getElementById: () => null },
    window: { confirm: () => true },
  });
  const watchers = [];
  const scope = {
    model: { editModel: { order: { uniqueId: 'order-A', customerInformation: { customer: {}, shipping: {} } } } },
    $watch: (getter, callback) => watchers.push({ getter, callback }),
    $on: () => {},
  };
  controller(scope, { success() {}, error() {} }, {
    OrderLogs: async () => ({ data: [] }),
    OrderActions: async () => ({ data: actions }),
    orderLockStatus: async () => ({ data: locked }),
    ...resources,
  }, [], {}, { getState: () => ({ statusList: [] }) }, () => {});
  return { scope, changeOrder: watchers.find(watcher => typeof watcher.getter === 'function').callback };
}

test('U13 filters card actions exactly and uses escaped text bindings for disabled reasons', async () => {
  const { scope } = createLegacy();
  await Promise.resolve();
  assert.deepEqual(Array.from(scope.getGlobalOrderActions(), action => action.key), ['global']);
  assert.deepEqual(Array.from(scope.getGiftcardActions({ code: 'Card-A' }), action => action.key), ['release-A']);
  assert.deepEqual(Array.from(scope.getGiftcardActions({ code: 'Card-B' }), action => action.key), ['release-B']);
  assert.match(legacyTemplate, /ng-repeat="action in getGiftcardActions\(card\) track by action.key"/);
  assert.match(legacyTemplate, /ng-repeat="action in getGlobalOrderActions\(\) track by action.key"/);
  assert.match(legacyTemplate, /ng-if="action.enabled === false && action.disabledReason">\{\{ action.disabledReason \}\}/);
  assert.doesNotMatch(legacyTemplate, /ng-bind-html/);
  assert.match(legacyTemplate, /Working\.\.\./);
});

test('U13 lock refresh handles errors, clears state on order switch, and ignores old responses', async () => {
  const old = deferred();
  const current = deferred();
  let calls = 0;
  const { scope, changeOrder } = createLegacy({ orderLockStatus: () => { calls++; return calls === 1 ? old.promise : current.promise; } });
  assert.equal(calls, 1, 'Fetched when order is opened');
  scope.model.editModel.order.uniqueId = 'order-B';
  scope.orderLockStatus = locked;
  changeOrder('order-B', 'order-A');
  assert.equal(scope.orderLockStatus, null);
  const refresh = scope.refreshOrderLockStatus();
  current.resolve({ data: { ...locked, hasLock: false } });
  await refresh;
  old.resolve({ data: locked });
  await old.promise;
  await Promise.resolve();
  assert.equal(calls, 3);
  assert.equal(scope.orderLockStatus.hasLock, false);
  const failure = createLegacy({ orderLockStatus: async () => { throw new Error('offline'); } });
  await failure.scope.refreshOrderLockStatus();
  assert.equal(failure.scope.orderLockError, true);
  assert.equal(failure.scope.orderLockStatus, null);
  assert.match(legacyTemplate, /ng-if="orderLockStatus.hasLock"/);
  assert.match(legacyTemplate, /Current ownership is unknown/);
  for (const field of ['paymentOperationLocked', 'preparationLocked', 'reconciliationRequired', 'attemptId', 'attemptState']) assert.ok(legacyTemplate.includes(`orderLockStatus.${field}`));
  assert.ok(legacyTemplate.indexOf('Payment:') < legacyTemplate.indexOf('Checkout lock'));
  assert.ok(legacyTemplate.indexOf('Checkout lock') < legacyTemplate.indexOf('Order Actions'));
});

test('U13 generic card execution retains refresh flow including lock status', async () => {
  let lockCalls = 0;
  let executeCalls = 0;
  const { scope } = createLegacy({
    orderLockStatus: async () => { lockCalls++; return { data: locked }; },
    OrderInfo: async id => ({ data: { uniqueId: id, customerInformation: { customer: {}, shipping: {} } } }),
    ExecuteOrderAction: async (id, key) => {
      assert.equal(id, 'order-A');
      assert.equal(key, 'release-A');
      executeCalls++;
      return { headers: () => '', data: { text: async () => '{"message":"Released"}' } };
    },
  });
  await scope.executeOrderAction(actions[2]);
  assert.equal(executeCalls, 0);
  await scope.executeOrderAction(actions[1]);
  assert.equal(executeCalls, 1);
  assert.equal(lockCalls, 2, 'Initial load and successful-action refresh');
  assert.equal(scope.executingOrderActionKey, null);
});

test('U13 obsolete post-action refresh cannot overwrite a reopened order or restart follow-up loads', async () => {
  const pending = deferred();
  let requested;
  let actionLoads = 0;
  let lockLoads = 0;
  const { scope, changeOrder } = createLegacy({
    OrderInfo: () => { requested = true; return pending.promise; },
    OrderActions: async () => { actionLoads++; return { data: actions }; },
    orderLockStatus: async () => { lockLoads++; return { data: locked }; },
    ExecuteOrderAction: async () => ({ headers: () => '', data: { text: async () => '{}' } }),
  });
  const execution = scope.executeOrderAction(actions[1]);
  for (let i = 0; i < 5 && !requested; i++) await Promise.resolve();
  assert.equal(requested, true);

  scope.model.editModel.order = { uniqueId: 'order-B', customerInformation: { customer: {}, shipping: {} } };
  changeOrder('order-B', 'order-A');
  scope.model.editModel.order = { uniqueId: 'order-A', marker: 'current', customerInformation: { customer: {}, shipping: {} } };
  changeOrder('order-A', 'order-B');
  const actionLoadsBeforeOldResponse = actionLoads;
  const lockLoadsBeforeOldResponse = lockLoads;
  pending.resolve({ data: { uniqueId: 'order-A', marker: 'obsolete', customerInformation: { customer: {}, shipping: {} } } });
  await execution;

  assert.equal(scope.model.editModel.order.marker, 'current');
  assert.equal(actionLoads, actionLoadsBeforeOldResponse);
  assert.equal(lockLoads, lockLoadsBeforeOldResponse);
});

test('both API wrappers use the supplied backend route and escape order IDs', async () => {
  const api = new shared.EkomManagerApi();
  api.getJson = async path => path;
  assert.equal(await api.orderLockStatus('a/b'), '/ekom/manager/orderLockStatus/a%2Fb');
  let resources, request;
  runInNewContext(readFileSync(new URL(`${legacyRoot}resources/ekmManager.resources.js`, import.meta.url), 'utf8'), {
    angular: { module: () => ({ factory: (_name, deps) => { resources = deps.at(-1)(input => { request = input; }); } }) },
    Umbraco: { Sys: { ServerVariables: { ekom: { managerEndpoint: '/configured/manager/' } } } },
  });
  resources.orderLockStatus('a/b');
  assert.equal(request.url, '/configured/manager/orderLockStatus/a%2Fb');
  assert.equal(request.method, 'GET');
});
