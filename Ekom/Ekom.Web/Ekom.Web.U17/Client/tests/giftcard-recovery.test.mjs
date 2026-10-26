import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

const browserWindow = { confirm: () => true };
function loadModule(path, modules = {}) {
  const source = readFileSync(new URL(path, import.meta.url), 'utf8');
  const compiled = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  }).outputText;
  const exports = {};
  runInNewContext(compiled, {
    exports, Error,
    require: name => { assert.ok(modules[name], `Unexpected module: ${name}`); return modules[name]; },
    HTMLElement: class {}, customElements: { define() {} }, window: browserWindow,
  });
  return exports;
}
const shared = loadModule('../src/manager/manager-shared.ts');
const { EkomOrdersSectionViewElement } = loadModule('../src/manager/orders-section-view.element.ts', {
  './manager-shared': shared,
  '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
  '@umbraco-cms/backoffice/notification': { UMB_NOTIFICATION_CONTEXT: Symbol('notification') },
});
const claim = {
  id: 'opaque-claim', cardReference: '****1234', claimReference: 'provider-claim', provider: 'Provider', account: 'Account',
  requestedAmount: 20, currency: 'ISK', rawReturnedAmount: '20.00', returnedAmountType: 'System.Decimal',
  rawExpiry: 'not a parsed date', cleanupState: 'ReleaseFailed', canRelease: true, releaseToken: 'claim-token',
};
const attempt = {
  attemptId: 'attempt-A', createdUtc: '2026-10-15T12:00:00Z', state: 'Unresolved', reason: 'Preparation failed',
  hasUnknownEffects: true, canReconcile: true, reconcileToken: 'attempt-token', claims: [claim],
};
const lock = { hasLock: true, canRelease: true, releaseToken: 'lock-token' };
function fixture() {
  browserWindow.confirm = () => true;
  const element = new EkomOrdersSectionViewElement();
  const calls = [];
  element.render = () => {};
  element.querySelector = () => ({ scrollTop: 480 });
  element.selectedOrder = { uniqueId: 'order-A', giftcards: [] };
  element.recoveryAttempts = [structuredClone(attempt)];
  element.orderLockStatus = lock;
  element.orderActions = [{ key: 'print', label: 'Print', enabled: true }];
  element.api.giftcardRecovery = async id => { calls.push(['history', id]); return [structuredClone(attempt)]; };
  element.api.orderInfo = async id => { calls.push(['order', id]); return { uniqueId: id, marker: 'refreshed' }; };
  element.api.orderLockStatus = async id => { calls.push(['lock', id]); return lock; };
  element.api.orderActions = async id => { calls.push(['actions', id]); return []; };
  element.api.orderLogs = async id => { calls.push(['logs', id]); return []; };
  element.api.releaseGiftcardRecovery = async (...args) => { calls.push(['release', ...args]); return { message: 'Hold released.', resolved: false }; };
  element.api.reconcileGiftcardRecovery = async (...args) => { calls.push(['reconcile', ...args]); return { message: 'Checked.', resolved: true }; };
  return { element, calls };
}
function deferred() {
  let resolve, reject;
  const promise = new Promise((res, rej) => { resolve = res; reject = rej; });
  return { promise, resolve, reject };
}

test('API uses escaped order route and only opaque claim ID and backend tokens', async () => {
  const api = new shared.EkomManagerApi();
  api.getJson = async path => path;
  assert.equal(await api.giftcardRecovery('a/b'), '/ekom/manager/giftcardRecovery/a%2Fb');
  const calls = [];
  api.postBody = async (path, body) => { calls.push([path, JSON.stringify(body)]); return { message: 'OK', resolved: true }; };
  await api.releaseGiftcardRecovery('a/b', 'attempt', 'opaque', 'token');
  await api.reconcileGiftcardRecovery('a/b', 'attempt', 'token');
  assert.deepEqual(calls, [
    ['/ekom/manager/giftcardRecovery/a%2Fb/release', '{"attemptId":"attempt","claimId":"opaque","token":"token"}'],
    ['/ekom/manager/giftcardRecovery/a%2Fb/reconcile', '{"attemptId":"attempt","token":"token"}'],
  ]);
});

test('independent panel retains unresolved and resolved history without selected gift cards or checkout lock', () => {
  const { element } = fixture();
  element.orderLockStatus = { hasLock: false };
  element.recoveryAttempts.push({ ...attempt, attemptId: 'resolved-attempt', state: 'Resolved', hasUnknownEffects: false, canReconcile: false, reconcileToken: null, unavailableReason: 'Already resolved', claims: [{ ...claim, canRelease: false, releaseToken: null, cleanupState: 'Released', unavailableReason: 'Already released' }] });
  assert.equal(element.renderGiftcards(element.selectedOrder), '');
  assert.equal(element.renderOrderLockStatus(), '');
  const html = element.renderOrderDetails(element.selectedOrder);
  for (const text of ['Failed gift-card preparation', 'not accepted payment', 'attempt-A', 'resolved-attempt', 'Unresolved', 'Resolved', 'Already resolved', 'Already released', 'Release allowed after confirmation.', 'Reconciliation allowed after confirmation.']) assert.ok(html.includes(text), text);
  assert.match(html, /data-action="release-giftcard-recovery"[^>]*disabled/);
  assert.ok(html.indexOf('Failed gift-card preparation') < html.indexOf('Order Details'));
});

test('successful empty history hides the entire recovery section in the order view', async () => {
  const { element } = fixture();
  element.recoveryAttempts = undefined;
  const pending = deferred();
  element.api.giftcardRecovery = () => pending.promise;
  const loading = element.loadGiftcardRecovery('order-A');
  assert.match(element.renderGiftcardRecovery(), /Loading recovery history/);

  pending.resolve([]);
  await loading;

  assert.equal(element.recoveryError, false);
  assert.equal(element.renderGiftcardRecovery(), '');
  assert.doesNotMatch(element.renderOrderDetails(element.selectedOrder), /ekmGiftcardRecovery|Failed gift-card preparation|No failed gift-card preparation history/);
});

test('empty history never hides loading failures or recovery operation diagnostics', () => {
  for (const [key, value, expected] of [
    ['recoveryLoading', true, /Loading recovery history/],
    ['recoveryError', true, /We could not load gift-card recovery history/],
    ['recoveryMessage', 'Hold released.', /Hold released/],
    ['recoveryActionError', 'Please confirm again.', /Please confirm again/],
    ['recoveryRefreshError', true, /We could not refresh all order details/],
    ['orderActionsError', true, /We could not refresh order actions/],
  ]) {
    const { element } = fixture();
    element.recoveryAttempts = [];
    element[key] = value;
    assert.match(element.renderGiftcardRecovery(), expected, key);
  }
});

test('missing or malformed history remains a visible error even after a previous empty response', async () => {
  const { element } = fixture();
  element.recoveryAttempts = [];
  for (const response of [undefined, null, {}, [{ claims: null }]]) {
    element.api.giftcardRecovery = async () => response;
    await element.loadGiftcardRecovery('order-A');
    assert.equal(element.recoveryError, true);
    assert.match(element.renderGiftcardRecovery(), /role="alert">We could not load gift-card recovery history/);
  }
});

test('all ledger values and attribute IDs are escaped; raw evidence is displayed without interpreting it', () => {
  const { element } = fixture();
  element.recoveryAttempts = [{ ...attempt, attemptId: '<attempt>', reason: '<reason>', state: '<state>', unavailableReason: '<blocked>', canReconcile: false, claims: [{ ...claim,
    id: 'opaque"<id>', cardReference: '****<1234>', claimReference: '<claim>', provider: '<provider>', account: '<account>',
    rawReturnedAmount: '<raw amount>', returnedAmountType: '<type>', rawExpiry: '<expiry>', cleanupState: '<cleanup>',
    canRelease: false, unavailableReason: '<release blocked>',
  }] }];
  const html = element.renderGiftcardRecovery();
  for (const value of ['attempt', 'reason', 'state', 'blocked', '1234', 'claim', 'provider', 'account', 'raw amount', 'type', 'expiry', 'cleanup', 'release blocked']) assert.ok(html.includes(`&lt;${value}&gt;`), value);
  assert.match(html, /data-claim-id="opaque&quot;&lt;id&gt;"/);
  assert.match(html, /Requested amount<\/dt><dd>20 ISK/);
  assert.doesNotMatch(html, /<claim>|<expiry>|claim-token|attempt-token|<input/);
});

test('unknown effects without a known claim require reconciliation and never offer release', async () => {
  const { element, calls } = fixture();
  for (const claims of [[], [{ ...claim, claimReference: '' }]]) {
    element.recoveryAttempts = [{ ...attempt, claims }];
    const html = element.renderGiftcardRecovery();
    assert.match(html, /Unknown provider effects need reconciliation/);
    assert.doesNotMatch(html, /data-action="release-giftcard-recovery"/);
    assert.match(html, /data-action="reconcile-giftcard-recovery"/);
    await element.executeGiftcardRecovery(attempt.attemptId, claim.id);
  }
  assert.equal(calls.length, 0);
});

test('release confirms with masked card and claim, blocks concurrent actions and refreshes all evidence without resetting status override', async () => {
  const { element, calls } = fixture();
  element.notifyOrderStatusOverride = false;
  const pending = deferred();
  element.api.releaseGiftcardRecovery = (...args) => { calls.push(['release', ...args]); return pending.promise; };
  browserWindow.confirm = message => {
    for (const text of ['****1234', 'provider-claim', 'audited recovery', 'does not refund an accepted payment or retry payment or checkout']) assert.ok(message.includes(text), text);
    return true;
  };
  const execution = element.executeGiftcardRecovery(attempt.attemptId, claim.id);
  assert.equal(element.recoveryScrollTop, 480);
  assert.match(element.renderGiftcardRecovery(), /disabled>Release failed-preparation hold \(Working\.\.\.\)/);
  assert.match(element.renderOrderActions(), /disabled>Print/);
  assert.match(element.renderOrderLockStatus(), /disabled>Release Lock/);
  await element.executeGiftcardRecovery(attempt.attemptId);
  await element.executeOrderAction('print');
  await element.releaseOrderLock();
  assert.deepEqual(calls, [['release', 'order-A', 'attempt-A', 'opaque-claim', 'claim-token']]);
  pending.resolve({ message: 'Released <hold>.', resolved: false });
  await execution;
  for (const name of ['history', 'order', 'actions', 'lock', 'logs']) assert.ok(calls.some(call => call[0] === name), name);
  assert.equal(element.selectedOrder.marker, 'refreshed');
  assert.equal(element.notifyOrderStatusOverride, false);
  assert.equal(element.recoveryOperation, '');
  assert.match(element.renderGiftcardRecovery(), /Released &lt;hold&gt;\. Recovery remains unresolved/);
});

test('reconciliation has its own confirmation and reports backend resolution, not accepted payment success', async () => {
  const { element, calls } = fixture();
  browserWindow.confirm = message => {
    assert.match(message, /known claims and unknown provider effects/);
    assert.match(message, /does not refund an accepted payment or retry payment or checkout/);
    return true;
  };
  await element.executeGiftcardRecovery(attempt.attemptId);
  assert.deepEqual(calls[0], ['reconcile', 'order-A', 'attempt-A', 'attempt-token']);
  assert.match(element.renderGiftcardRecovery(), /Checked\. Failed preparation resolved/);
});

test('unavailable, tokenless, stale, pending and cancelled recovery actions never POST', async () => {
  const { element, calls } = fixture();
  for (const key of ['recoveryLoading', 'recoveryError', 'recoveryRefreshError', 'orderLockLoading', 'orderLockError', 'orderActionsLoading', 'orderActionsError', 'releasingOrderLock', 'executingActionKey', 'recoveryOperation']) {
    element[key] = true;
    await element.executeGiftcardRecovery(attempt.attemptId, claim.id);
    await element.executeGiftcardRecovery(attempt.attemptId);
    element[key] = false;
  }
  for (const change of [{ canRelease: false }, { releaseToken: null }]) {
    element.recoveryAttempts = [{ ...attempt, claims: [{ ...claim, ...change }] }];
    await element.executeGiftcardRecovery(attempt.attemptId, claim.id);
  }
  for (const change of [{ canReconcile: false }, { reconcileToken: null }]) {
    element.recoveryAttempts = [{ ...attempt, ...change }];
    await element.executeGiftcardRecovery(attempt.attemptId);
  }
  element.recoveryAttempts = [attempt];
  browserWindow.confirm = () => false;
  await element.executeGiftcardRecovery(attempt.attemptId, claim.id);
  await element.executeGiftcardRecovery(attempt.attemptId);
  await element.executeGiftcardRecovery('missing', claim.id);
  await element.executeGiftcardRecovery(attempt.attemptId, 'provider-claim');
  assert.equal(calls.length, 0);
});

test('history load failures keep last known evidence visibly stale and fail closed, never invent empty history', async () => {
  const { element, calls } = fixture();
  for (const response of [new Error('offline'), null, {}]) {
    element.api.giftcardRecovery = async () => { if (response instanceof Error) throw response; return response; };
    await element.loadGiftcardRecovery('order-A');
    assert.equal(element.recoveryError, true);
    const html = element.renderGiftcardRecovery();
    assert.match(html, /role="alert">We could not load gift-card recovery history/);
    assert.match(html, /attempt-A/);
    assert.doesNotMatch(html, /No failed gift-card preparation history/);
    await element.executeGiftcardRecovery(attempt.attemptId, claim.id);
  }
  element.recoveryAttempts = undefined;
  assert.doesNotMatch(element.renderGiftcardRecovery(), /No failed gift-card preparation history/);
  assert.equal(calls.length, 0);
});

test('failed POST refreshes tokens once without automatically retrying and retains escaped error', async () => {
  const { element, calls } = fixture();
  element.api.releaseGiftcardRecovery = async () => { calls.push(['release']); throw new Error('Please <confirm> again.'); };
  element.api.giftcardRecovery = async () => [{ ...attempt, claims: [{ ...claim, releaseToken: 'fresh-token' }] }];
  await element.executeGiftcardRecovery(attempt.attemptId, claim.id);
  assert.equal(calls.filter(call => call[0] === 'release').length, 1);
  assert.equal(element.recoveryAttempts[0].claims[0].releaseToken, 'fresh-token');
  assert.match(element.renderGiftcardRecovery(), /role="alert">Please &lt;confirm&gt; again/);
  assert.equal(element.recoveryOperation, '');
});

test('successful operation with failed refresh remains successful but visibly blocks another operation', async () => {
  for (const method of ['orderInfo', 'giftcardRecovery', 'orderLockStatus', 'orderActions']) {
    const { element, calls } = fixture();
    element.api[method] = async () => { throw new Error('offline'); };
    await element.executeGiftcardRecovery(attempt.attemptId, claim.id);
    const html = element.renderGiftcardRecovery() + element.renderOrderLockStatus();
    assert.match(html, /Hold released/);
    assert.match(html, /role="alert"/);
    assert.equal(element.recoveryBusy, true, method);
    await element.executeGiftcardRecovery(attempt.attemptId, claim.id);
    assert.equal(calls.filter(call => call[0] === 'release').length, 1, method);
  }
});

test('old history loads and POST responses cannot replace a reopened A B A order, including failures', async () => {
  for (const kind of ['load', 'post', 'refresh']) {
    for (const fail of [false, true]) {
      const { element } = fixture();
      const pending = deferred();
      let started = false;
      if (kind === 'load') element.api.giftcardRecovery = () => pending.promise;
      if (kind === 'post') element.api.releaseGiftcardRecovery = () => pending.promise;
      if (kind === 'refresh') element.api.orderInfo = () => { started = true; return pending.promise; };
      const operation = kind === 'load' ? element.loadGiftcardRecovery('order-A') : element.executeGiftcardRecovery(attempt.attemptId, claim.id);
      if (kind === 'refresh') {
        for (let i = 0; i < 10 && !started; i++) await Promise.resolve();
        assert.equal(started, true);
      }
      element.orderRequestId += 2;
      element.selectedOrder = { uniqueId: 'order-A', marker: 'current' };
      element.recoveryAttempts = [{ ...attempt, attemptId: 'current-attempt' }];
      element.recoveryMessage = 'current message';
      element.recoveryActionError = 'current error';
      element.recoveryOperation = 'current operation';
      fail ? pending.reject(new Error('old failure')) : pending.resolve(kind === 'load' ? [] : kind === 'refresh' ? { uniqueId: 'order-A', marker: 'obsolete' } : { message: 'old message', resolved: true });
      await operation;
      assert.equal(element.selectedOrder.marker, 'current');
      assert.equal(element.recoveryAttempts[0].attemptId, 'current-attempt');
      assert.equal(element.recoveryMessage, 'current message');
      assert.equal(element.recoveryActionError, 'current error');
      assert.equal(element.recoveryOperation, 'current operation');
    }
  }
});

test('opening a different order clears previous recovery state and loads its history', async () => {
  const { element, calls } = fixture();
  element.recoveryError = true;
  element.recoveryActionError = 'old error';
  element.recoveryMessage = 'old result';
  const opening = element.openOrder('order-B');
  assert.equal(element.recoveryAttempts, undefined);
  assert.equal(element.recoveryError, false);
  assert.equal(element.recoveryMessage, '');
  assert.equal(element.recoveryActionError, '');
  await opening;
  assert.ok(calls.some(call => call[0] === 'history' && call[1] === 'order-B'));
  assert.equal(element.selectedOrder.uniqueId, 'order-B');
});

test('U18 builds the same shared modern recovery implementation', () => {
  const config = readFileSync(new URL('../../../Ekom.Web.U18/Client/vite.config.ts', import.meta.url), 'utf8');
  assert.match(config, /Ekom\.Web\.U17\/Client\/src/);
});

test('every render during recovery and its refresh restores the captured overlay scroll position', async () => {
  const { element } = fixture();
  delete element.render;
  element.bindEvents = () => {};
  element.renderOrders = () => '';
  element.renderOverlay = () => '';
  let overlay = { scrollTop: 480 };
  let renders = 0;
  Object.defineProperty(element, 'innerHTML', {
    set() { renders++; overlay = { scrollTop: 0 }; },
  });
  element.querySelector = () => overlay;
  await element.executeGiftcardRecovery(attempt.attemptId, claim.id);
  assert.ok(renders > 4, 'Includes asynchronous history, lock, action and log renders');
  assert.equal(overlay.scrollTop, 480);
  assert.equal(element.recoveryScrollTop, undefined);
});

test('newer same-order history loads win over old successes and failures', async () => {
  for (const fail of [false, true]) {
    const { element } = fixture();
    const pending = deferred();
    element.api.giftcardRecovery = () => pending.promise;
    const old = element.loadGiftcardRecovery('order-A');
    element.api.giftcardRecovery = async () => [];
    await element.loadGiftcardRecovery('order-A');
    fail ? pending.reject(new Error('old failure')) : pending.resolve([attempt]);
    await old;
    assert.equal(element.recoveryAttempts.length, 0);
    assert.equal(element.recoveryError, false);
    assert.equal(element.recoveryLoading, false);
  }
});
