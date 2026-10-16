import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

const browserWindow = { confirm: () => true };

function loadModule(relativePath, modules = {}) {
  const source = readFileSync(new URL(relativePath, import.meta.url), 'utf8');
  const compiled = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  }).outputText;
  const exports = {};
  runInNewContext(compiled, {
    exports,
    Error,
    require: name => { assert.ok(modules[name], `Unexpected module: ${name}`); return modules[name]; },
    HTMLElement: class {},
    customElements: { define() {} },
    window: browserWindow,
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
const locked = { hasLock: true, paymentOperationLocked: true, preparationLocked: true, reconciliationRequired: true, attemptId: 'attempt-1', attemptState: 'Uncertain', canRelease: true, releaseToken: 'release-token' };
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

test('gift-card summaries use saved usage and reservation evidence consistently in U13 and U17/U18', () => {
  const element = createElement();
  const { scope } = createLegacy();
  const future = new Date(Date.now() + 90 * 60000).toISOString();
  const past = '2000-01-01T12:00:00Z';
  const cases = [
    [{}, 'Selected'],
    [{ claimed: false }, 'Selected'],
    [{ validUntil: future }, 'Needs checking'],
    [{ validUntil: past }, 'Needs checking'],
    [{ claimId: 'hold-1', validUntil: future }, 'Reserved'],
    [{ claimId: 'hold-1', validUntil: past }, 'Reservation expired'],
    [{ claimId: 'hold-1' }, 'Needs checking'],
    [{ claimDate: past }, 'Used'],
    [{ claimDate: past, claimId: 'hold-1', validUntil: future }, 'Used'],
    [{ claimId: 'hold-1', validUntil: 'invalid date' }, 'Needs checking'],
    [{ claimed: true, validUntil: past }, 'Used'],
    [{ transactionId: 'transaction-1', claimed: false, validUntil: past }, 'Used'],
    [{ usedDate: past, claimed: false, validUntil: past }, 'Used'],
  ];
  for (const [evidence, expected] of cases) {
    const card = { code: 'Card-A', amount: 10, ...evidence };
    const html = element.renderGiftcards({ currency: 'USD', giftcards: [card] });
    assert.ok(html.includes(`<span>${expected}</span>`), JSON.stringify(evidence));
    assert.equal(scope.giftcardSummary(card).status, expected, JSON.stringify(evidence));
    if (expected === 'Used') {
      assert.equal(scope.giftcardSummary(card).warning, '');
      assert.equal(scope.giftcardSummary(card).expiryLabel, 'Saved expiry');
      assert.doesNotMatch(html.slice(0, html.indexOf('<details>')), /reference is missing|Reservation ends/);
    }
    assert.doesNotMatch(html, /Available|automatically released/);
  }
});

test('expiry labels, relative future time and provider uncertainty stay visible above More details', () => {
  const element = createElement();
  const { scope } = createLegacy();
  const future = new Date(Date.now() + 90 * 60000).toISOString();
  for (const claimId of [null, 'hold-1']) {
    const card = { code: 'Card-A', amount: 10, claimId, validUntil: future };
    const summary = scope.giftcardSummary(card);
    assert.equal(summary.expiryLabel, claimId ? 'Reservation ends' : 'Saved expiry');
    assert.match(summary.expiry, /\(in about 2 hours\)/);
    assert.ok(summary.expiry.includes(String(new Date(future).getFullYear())), 'Exact date accompanies relative time');
    const html = element.renderGiftcards({ currency: 'USD', giftcards: [card] });
    const visible = html.slice(0, html.indexOf('<details>'));
    assert.ok(visible.includes(summary.expiryLabel));
    assert.match(visible, /\(in about 2 hours\)/);
    if (!claimId) {
      const warning = 'An expiry time is saved, but the reservation reference is missing. Check the gift-card provider if an amount is still held.';
      assert.equal(summary.warning, warning);
      assert.ok(visible.includes(warning));
      assert.match(visible, /role="alert"/);
    }
  }
  const expired = { code: 'Card-A', amount: 10, claimId: 'hold-1', validUntil: '2000-01-01T12:00:00Z' };
  const html = element.renderGiftcards({ currency: 'USD', giftcards: [expired] });
  assert.match(html.slice(0, html.indexOf('<details>')), /does not confirm that the provider has released the hold/);
  assert.match(scope.giftcardSummary(expired).warning, /does not confirm/);
  assert.doesNotMatch(scope.giftcardSummary(expired).expiry, /in about/);
  const invalid = { ...expired, validUntil: '<invalid>' };
  assert.match(element.renderGiftcards({ currency: 'USD', giftcards: [invalid] }), /&lt;invalid&gt;/);
  assert.match(scope.giftcardSummary(invalid).warning, /could not be read/);
});

test('each collapsed More details contains every saved field, missing known fields and safe arbitrary values', () => {
  const element = createElement();
  const { scope } = createLegacy();
  const card = {
    code: '<Card-A>', amount: 0, claimed: false, claimId: '<hold>',
    claimDate: '2025-01-01T01:02:03Z', transactionId: 'transaction-1',
    usedDate: '2025-01-01T02:03:04Z', validUntil: '2025-01-02T01:02:03Z',
    provider_note: '<img src=x onerror=alert(1)>',
    '<unsafeLabel>': { nestedValue: ['<script>', false, 0, null], toString: 'saved metadata' },
    emptyList: [], emptyObject: {},
  };
  const html = element.renderGiftcards({ currency: 'USD', giftcards: [card] });
  const details = html.match(/<details><summary>More details<\/summary>([\s\S]*?)<\/details>/)[1];
  for (const label of ['Gift-card number', 'Amount', 'Reservation reference', 'Payment claim date', 'Transaction reference', 'Used on', 'Claimed', 'Saved expiry', 'Provider note']) assert.ok(details.includes(`<dt>${label}</dt>`), label);
  for (const text of ['&lt;hold&gt;', card.claimDate, card.usedDate, card.validUntil, '&lt;img src=x onerror=alert(1)&gt;', '&lt;unsafe label&gt;', 'Nested value: &lt;script&gt;; No; 0; Not recorded', 'Empty list', 'Empty object']) assert.ok(details.includes(text), text);
  assert.doesNotMatch(html, /<img|<script|<unsafeLabel>|<details open/);
  assert.match(details, /<dt>Claimed<\/dt><dd>No<\/dd>/);
  assert.match(details, /<dt>Amount<\/dt><dd>0<\/dd>/);
  const missing = element.renderGiftcards({ currency: 'USD', giftcards: [{ code: 'Card-B', amount: 10 }] });
  for (const label of ['Reservation reference', 'Payment claim date', 'Transaction reference', 'Used on', 'Claimed', 'Saved expiry']) assert.ok(missing.includes(`<dt>${label}</dt><dd>Not recorded</dd>`), label);
  const legacyFields = Array.from(scope.giftcardDetails(card));
  assert.equal(legacyFields.length, Object.keys(card).length);
  assert.equal(legacyFields.find(field => field.label === 'Provider note').value, card.provider_note);
  assert.equal(legacyFields.find(field => field.label === '<unsafe label>').value, 'Nested value: <script>; No; 0; Not recorded; To string: saved metadata');
  assert.equal(scope.giftcardDetails({}).filter(field => field.value === 'Not recorded').length, 8);
});

test('gift-card actions and their disabled reasons are inside their card but outside collapsed details on both frontends', () => {
  const element = createElement();
  const html = element.renderGiftcards(element.selectedOrder);
  for (const row of html.match(/<li>[\s\S]*?<\/li>/g)) {
    assert.match(row, /class="ekmOrderGiftcards__summary"/);
    const actionStart = row.indexOf('class="ekmOrderGiftcards__actions"');
    const detailsStart = row.indexOf('<details>');
    assert.ok(actionStart > 0 && actionStart < detailsStart);
    assert.match(row.slice(actionStart, detailsStart), /data-action="execute-order-action"/);
    assert.doesNotMatch(row.slice(detailsStart), /<button|disabledReason/);
  }
  const legacyCard = legacyTemplate.match(/<li ng-repeat="card in model.editModel.order.giftcards track by \$index">([\s\S]*?)<\/li>/)[1];
  const detailsStart = legacyCard.indexOf('<details>');
  assert.match(legacyCard.slice(0, detailsStart), /class="ekmOrderGiftcards__actions"[\s\S]*ng-click="executeOrderAction\(action\)"[\s\S]*\{\{ action.disabledReason \}\}/);
  assert.match(legacyCard.slice(0, detailsStart), /role="alert"[\s\S]*\{\{ giftcardSummary\(card\).warning \}\}/);
  assert.match(legacyCard.slice(detailsStart), /<summary>More details<\/summary>[\s\S]*ng-repeat="field in giftcardDetails\(card\) track by \$index"[\s\S]*\{\{ field.label \}\}[\s\S]*\{\{ field.value \}\}/);
  assert.doesNotMatch(legacyCard, /ng-bind-html|<details open/);
});

test('lock panel is beneath payment and above actions, displays blockers and escaped attempt details', () => {
  const element = createElement();
  element.orderLockStatus = { ...locked, attemptState: '<uncertain>' };
  const html = element.renderOrderDetails(element.selectedOrder);
  assert.ok(html.indexOf('Payment:') < html.indexOf('Checkout is locked'));
  assert.ok(html.indexOf('Checkout is locked') < html.indexOf('Order Actions'));
  for (const text of ['ownership retained', 'Payment operation', 'Checkout preparation', 'Reconciliation required', 'attempt-1', '&lt;uncertain&gt;', 'previous checkout did not finish', 'Release Lock']) assert.ok(html.includes(text), text);
  const panel = element.renderOrderLockStatus();
  assert.equal((panel.match(/<button\b/g) || []).length, 1);
  assert.doesNotMatch(panel, /Refresh status|<details open/);
  assert.match(panel, /<details><summary>Technical details<\/summary>[\s\S]*Attempt state:[\s\S]*<\/details>/);
  assert.ok(panel.indexOf('<details>') < panel.indexOf('ownership retained'));
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
  assert.match(element.renderOrderLockStatus(), /We could not check whether checkout is locked\. Reopen the order to try again\./);
  assert.equal(element.orderLockStatus, locked, 'Keep last known lock on failure');
  element.orderLockStatus = undefined;
  assert.doesNotMatch(element.renderOrderLockStatus(), /<button|unlocked/i);
});

test('a reconciliation-only block does not claim payment or preparation ownership is retained', () => {
  const element = createElement();
  element.orderLockStatus = { ...locked, paymentOperationLocked: false, preparationLocked: false };
  const html = element.renderOrderLockStatus();
  assert.match(html, /Checkout is locked/);
  assert.match(html, /previous payment needs checking/);
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

function createLegacy(resources = {}, confirm = () => true) {
  let controller;
  runInNewContext(legacyController, {
    angular: { module: () => ({ controller: (_name, dependencies) => { controller = dependencies.at(-1); } }) },
    document: { getElementById: () => null },
    window: { confirm },
  });
  const watchers = [];
  const scope = {
    model: { editModel: { order: { uniqueId: 'order-A', customerInformation: { customer: {}, shipping: {} } } } },
    $watch: (getter, callback) => watchers.push({ getter, callback }),
    $on: () => {},
  };
  controller(scope, { success() {}, error() {} }, {
    Config: async () => ({ data: {} }),
    OrderLogs: async () => ({ data: [] }),
    OrderInfo: async id => ({ data: { uniqueId: id, customerInformation: { customer: {}, shipping: {} } } }),
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
  assert.match(legacyTemplate, /We could not check whether checkout is locked\. Reopen the order to try again\./);
  for (const field of ['paymentOperationLocked', 'preparationLocked', 'reconciliationRequired', 'attemptId', 'attemptState']) assert.ok(legacyTemplate.includes(`orderLockStatus.${field}`));
  assert.ok(legacyTemplate.indexOf('Payment:') < legacyTemplate.indexOf('Checkout is locked'));
  assert.ok(legacyTemplate.indexOf('Checkout is locked') < legacyTemplate.indexOf('Order Actions'));
  const panel = legacyTemplate.match(/<section class="ekmOrderTracking" ng-if="orderLockStatus.hasLock">[\s\S]*?<\/section>/)[0];
  assert.equal((panel.match(/<button\b/g) || []).length, 1);
  assert.match(panel, /<details>\s*<summary>Technical details<\/summary>[\s\S]*Attempt state:[\s\S]*<\/details>/);
  assert.doesNotMatch(legacyTemplate, /Refresh status|ng-bind-html/);
  assert.ok(legacyTemplate.indexOf('ng-if="orderLockWarning"') < legacyTemplate.indexOf('ng-if="orderLockStatus.hasLock"'));
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
  api.postBody = async (path, body) => { assert.equal(path, '/ekom/manager/releaseOrderLock/a%2Fb'); assert.equal(JSON.stringify(body), '{"releaseToken":"token"}'); return { message: 'Released' }; };
  assert.equal((await api.releaseOrderLock('a/b', 'token')).message, 'Released');
  let resources, request;
  runInNewContext(readFileSync(new URL(`${legacyRoot}resources/ekmManager.resources.js`, import.meta.url), 'utf8'), {
    angular: { module: () => ({ factory: (_name, deps) => { resources = deps.at(-1)(input => { request = input; }); } }) },
    Umbraco: { Sys: { ServerVariables: { ekom: { managerEndpoint: '/configured/manager/' } } } },
  });
  resources.orderLockStatus('a/b');
  assert.equal(request.url, '/configured/manager/orderLockStatus/a%2Fb');
  assert.equal(request.method, 'GET');
  resources.releaseOrderLock('a/b', 'token');
  assert.equal(request.url, '/configured/manager/releaseOrderLock/a%2Fb');
  assert.equal(request.method, 'POST');
  assert.equal(JSON.stringify(request.data), '{"releaseToken":"token"}');
});

const confirmation = "Release this checkout lock? Confirm that no payment is currently being processed and the previous checkout has stopped. The customer's basket will be kept, and they can try checkout again. This does not cancel or refund a payment. Any gift-card hold that cannot be released must be checked separately.";

test('Release Lock confirms, prevents concurrent actions, refreshes all data and keeps escaped warning visible after unlock', async () => {
  const element = createElement();
  element.orderLockStatus = locked;
  const calls = [];
  const pending = deferred();
  element.showSuccess = message => calls.push(message);
  element.api.releaseOrderLock = async (id, token) => {
    assert.equal(id, 'order-A');
    assert.equal(token, 'release-token');
    calls.push('release');
    return pending.promise;
  };
  element.api.orderInfo = async id => { calls.push('info'); return { uniqueId: id, marker: 'refreshed' }; };
  element.api.orderActions = async () => { calls.push('actions'); return actions; };
  element.api.orderLogs = async () => { calls.push('logs'); return []; };
  element.api.orderLockStatus = async () => { calls.push('lock'); return { ...locked, hasLock: false }; };
  browserWindow.confirm = message => { assert.equal(message, confirmation); return true; };
  const execution = element.releaseOrderLock();
  assert.match(element.renderOrderLockStatus(), /disabled>Release Lock/);
  assert.match(element.renderOrderActions(), /disabled>Print/);
  await element.releaseOrderLock();
  await element.executeOrderAction('release-A');
  assert.deepEqual(calls, ['release']);
  pending.resolve({ message: 'Checkout reset.', warning: 'Check <external> hold separately.' });
  await execution;
  for (const call of ['info', 'actions', 'logs', 'lock', 'Checkout reset.']) assert.ok(calls.includes(call));
  assert.equal(element.selectedOrder.marker, 'refreshed');
  assert.equal(element.releasingOrderLock, false);
  assert.equal(element.orderLockStatus.hasLock, false);
  assert.match(element.renderOrderLockStatus(), /role="alert">Check &lt;external&gt; hold separately/);
  assert.doesNotMatch(element.renderOrderLockStatus(), /<button|<external>/);
  browserWindow.confirm = () => true;
});

test('Release Lock is a no-op when unavailable, missing a token, busy, unknown, or cancelled', async () => {
  const element = createElement();
  element.api.releaseOrderLock = async () => assert.fail('Must not release');
  for (const status of [{ ...locked, canRelease: false }, { ...locked, canRelease: undefined }, { ...locked, releaseToken: null }, { ...locked, hasLock: false }]) {
    element.orderLockStatus = status;
    await element.releaseOrderLock();
  }
  element.orderLockStatus = { ...locked, canRelease: false, releaseUnavailableReason: 'Please <check> the payment.' };
  assert.match(element.renderOrderLockStatus(), /Please &lt;check&gt; the payment/);
  element.orderLockStatus = locked;
  for (const key of ['orderLockLoading', 'orderLockError', 'releasingOrderLock', 'executingActionKey']) {
    element[key] = true;
    await element.releaseOrderLock();
    element[key] = false;
  }
  browserWindow.confirm = message => { assert.equal(message, confirmation); return false; };
  await element.releaseOrderLock();
  browserWindow.confirm = () => true;
});

test('Release Lock ignores obsolete success and failure even after A B A', async () => {
  for (const fail of [false, true]) {
    const element = createElement();
    element.orderLockStatus = locked;
    const pending = deferred();
    element.api.releaseOrderLock = () => pending.promise;
    element.api.orderInfo = async () => assert.fail('No obsolete refresh');
    element.showSuccess = () => assert.fail('No obsolete notification');
    const execution = element.releaseOrderLock();
    element.selectedOrder = { uniqueId: 'order-B' };
    element.orderRequestId++;
    element.selectedOrder = { uniqueId: 'order-A', marker: 'current' };
    element.orderRequestId++;
    element.releasingOrderLock = true;
    element.orderLockWarning = 'current warning';
    fail ? pending.reject(new Error('old failure')) : pending.resolve({ message: 'old', warning: 'obsolete' });
    await execution;
    assert.equal(element.selectedOrder.marker, 'current');
    assert.equal(element.orderLockWarning, 'current warning');
    assert.equal(element.orderLockReleaseError, '');
    assert.equal(element.releasingOrderLock, true);
  }
});

test('U13 release confirms, prevents concurrent actions, refreshes and retains provider warning', async () => {
  const calls = [];
  const pending = deferred();
  const { scope } = createLegacy({
    releaseOrderLock: (id, token) => { assert.equal(id, 'order-A'); assert.equal(token, 'release-token'); calls.push('release'); return pending.promise; },
    OrderInfo: async id => { calls.push('info'); return { data: { uniqueId: id, customerInformation: { customer: {}, shipping: {} } } }; },
    OrderActions: async () => { calls.push('actions'); return { data: actions }; },
    OrderLogs: async () => { calls.push('logs'); return { data: [] }; },
    orderLockStatus: async () => { calls.push('lock'); return { data: calls.includes('release') ? { ...locked, hasLock: false } : locked }; },
    ExecuteOrderAction: () => assert.fail('No concurrent card action'),
  }, message => { assert.equal(message, confirmation); return true; });
  await scope.refreshOrderLockStatus();
  calls.length = 0;
  const execution = scope.releaseOrderLock();
  assert.equal(scope.releasingOrderLock, true);
  await scope.releaseOrderLock();
  await scope.executeOrderAction(actions[1]);
  assert.deepEqual(calls, ['release']);
  pending.resolve({ data: { message: 'Checkout reset.', warning: 'Check external hold separately.' } });
  await execution;
  for (const call of ['info', 'actions', 'logs', 'lock']) assert.ok(calls.includes(call));
  assert.equal(scope.orderLockStatus.hasLock, false);
  assert.equal(scope.orderLockWarning, 'Check external hold separately.');
  assert.equal(scope.releasingOrderLock, false);
});

test('U13 release does nothing when unavailable, busy or cancelled', async () => {
  let confirmations = 0;
  const { scope } = createLegacy({ releaseOrderLock: () => assert.fail('Must not release') }, message => { confirmations++; assert.equal(message, confirmation); return false; });
  await scope.refreshOrderLockStatus();
  for (const status of [{ ...locked, canRelease: false }, { ...locked, canRelease: undefined }, { ...locked, releaseToken: null }, { ...locked, hasLock: false }]) {
    scope.orderLockStatus = status;
    await scope.releaseOrderLock();
  }
  scope.orderLockStatus = locked;
  for (const key of ['orderLockLoading', 'orderLockError', 'releasingOrderLock', 'executingOrderActionKey']) {
    scope[key] = true;
    await scope.releaseOrderLock();
    scope[key] = false;
  }
  assert.equal(confirmations, 0);
  await scope.releaseOrderLock();
  assert.equal(confirmations, 1);
});

test('U13 obsolete release responses do not affect reopened A B A order', async () => {
  for (const fail of [false, true]) {
    const pending = deferred();
    const { scope, changeOrder } = createLegacy({
      releaseOrderLock: () => pending.promise,
      OrderInfo: () => assert.fail('No obsolete refresh'),
    });
    await scope.refreshOrderLockStatus();
    const execution = scope.releaseOrderLock();
    for (const [id, old] of [['order-B', 'order-A'], ['order-A', 'order-B']]) {
      scope.model.editModel.order.uniqueId = id;
      changeOrder(id, old);
    }
    scope.orderLockWarning = 'current';
    scope.releasingOrderLock = true;
    fail ? pending.reject(new Error('old')) : pending.resolve({ data: { message: 'old', warning: 'obsolete' } });
    await execution;
    assert.equal(scope.orderLockWarning, 'current');
    assert.equal(scope.orderLockReleaseError, '');
    assert.equal(scope.releasingOrderLock, true);
  }
});

test('release failures stay visible and clear the busy state on both frontends', async () => {
  const element = createElement();
  element.orderLockStatus = locked;
  element.api.releaseOrderLock = async () => { throw new Error('Please check the payment.'); };
  await element.releaseOrderLock();
  assert.equal(element.releasingOrderLock, false);
  assert.match(element.renderOrderLockStatus(), /role="alert">Please check the payment/);
  const { scope } = createLegacy({ releaseOrderLock: async () => { throw { data: { message: 'Please check the payment.' } }; } });
  await scope.refreshOrderLockStatus();
  await scope.releaseOrderLock();
  assert.equal(scope.releasingOrderLock, false);
  assert.equal(scope.orderLockReleaseError, 'Please check the payment.');
  assert.match(legacyTemplate, /ng-if="orderLockReleaseError" role="alert">\{\{ orderLockReleaseError \}\}/);
});

test('release post-refresh order information cannot replace a reopened A B A order', async () => {
  const element = createElement();
  element.orderLockStatus = locked;
  element.showSuccess = () => {};
  const pending = deferred();
  let requested = false;
  element.api.releaseOrderLock = async () => ({ message: 'Checkout reset.' });
  element.api.orderInfo = () => { requested = true; return pending.promise; };
  element.api.orderLogs = async () => [];
  element.api.orderActions = async () => [];
  element.api.orderLockStatus = async () => ({ ...locked, hasLock: false });
  const execution = element.releaseOrderLock();
  for (let i = 0; i < 5 && !requested; i++) await Promise.resolve();
  assert.equal(requested, true);
  element.selectedOrder = { uniqueId: 'order-B' };
  element.orderRequestId++;
  element.selectedOrder = { uniqueId: 'order-A', marker: 'current' };
  element.orderRequestId++;
  element.orderLockWarning = 'current warning';
  pending.resolve({ uniqueId: 'order-A', marker: 'obsolete' });
  await execution;
  assert.equal(element.selectedOrder.marker, 'current');
  assert.equal(element.orderLockWarning, 'current warning');
});

test('failed release refreshes confirmation tokens without automatically repeating the reset', async () => {
  const updated = { ...locked, releaseToken: 'updated-token' };
  const element = createElement();
  element.orderLockStatus = locked;
  let posts = 0;
  element.api.releaseOrderLock = async () => { posts++; throw new Error('Checkout changed. Please confirm again.'); };
  element.api.orderInfo = async id => ({ uniqueId: id });
  element.api.orderLogs = async () => [];
  element.api.orderActions = async () => actions;
  element.api.orderLockStatus = async () => updated;

  await element.releaseOrderLock();

  assert.equal(posts, 1);
  assert.equal(element.orderLockStatus.releaseToken, 'updated-token');
  assert.match(element.orderLockReleaseError, /Please confirm again/);
  let legacyPosts = 0;
  const { scope } = createLegacy({
    releaseOrderLock: async () => { legacyPosts++; throw { data: { message: 'Please confirm again.' } }; },
    orderLockStatus: async () => ({ data: legacyPosts ? updated : locked }),
  });
  await scope.refreshOrderLockStatus();
  await scope.releaseOrderLock();
  assert.equal(legacyPosts, 1);
  assert.equal(scope.orderLockStatus.releaseToken, 'updated-token');
  assert.equal(scope.orderLockReleaseError, 'Please confirm again.');
});

test('a confirmed reset with a failed details refresh is not reported as a failed release', async () => {
  const element = createElement();
  element.orderLockStatus = locked;
  element.showSuccess = () => {};
  element.api.releaseOrderLock = async () => ({ message: 'Checkout unlocked.', warning: 'Check the gift-card provider.' });
  element.api.orderInfo = async () => { throw new Error('Order details unavailable'); };
  element.api.orderLogs = async () => [];
  element.api.orderActions = async () => [];
  element.api.orderLockStatus = async () => ({ ...locked, hasLock: false, releaseToken: null, canRelease: false });

  await element.releaseOrderLock();

  assert.match(element.orderLockReleaseError, /Checkout was unlocked/);
  assert.match(element.renderOrderLockStatus(), /Check the gift-card provider/);
  assert.equal(element.releasingOrderLock, false);
});
