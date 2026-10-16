import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';

const root = '../../../Ekom.Web/App_Plugins/Ekom/Manager/';
const manager = readFileSync(new URL(`${root}controllers/ekmManager.controller.js`, import.meta.url), 'utf8');
const detail = readFileSync(new URL(`${root}controllers/ekmOrder.controller.js`, import.meta.url), 'utf8');

function setup(isDetail) {
  const scope = { orderChangeStatus: { value: 'Processed' }, model: { editModel: { order: {} } } };
  const checkbox = { checked: false };
  const requests = [];
  const errors = [];
  let resolveConfig;
  let rejectConfig;
  let click;
  const config = new Promise((resolve, reject) => { resolveConfig = resolve; rejectConfig = reject; });
  const resources = {
    Config: () => config,
    ChangeOrderStatus: query => { requests.push(query); return Promise.resolve(); },
  };
  const context = {
    $scope: scope,
    resources,
    notificationsService: { error: (...args) => errors.push(args), success() {} },
    document: { getElementById: id => id === 'notifyOrderStatus' ? checkbox : { addEventListener: (_, handler) => { click = handler; } } },
    getCurrentOrderId: () => 'order-1',
    loadActivityLogs() {},
    eventsService: { emit() {} },
  };
  if (isDetail) {
    runInNewContext(detail.slice(detail.indexOf('    var changeOrderStatusButton ='), detail.indexOf('    $scope.isDefaultKey =')), context);
  } else {
    const initialization = manager.slice(manager.indexOf('    $scope.orderStatusConfigLoaded ='), manager.indexOf('    $scope.loading ='));
    const handler = manager.match(/\$scope\.onStatusChanged = function \(order\) \{[\s\S]*?\n    \};/)[0];
    runInNewContext(initialization + handler, context);
  }
  return {
    scope, checkbox, requests, errors,
    change: () => isDetail ? click() : scope.onStatusChanged({ uniqueId: 'order-1', orderStatusCol: 'Processed' }),
    resolveConfig: data => resolveConfig({ data }), rejectConfig,
  };
}

for (const isDetail of [false, true]) {
  const view = isDetail ? 'detail' : 'table';
  for (const value of [true, false, null, undefined]) {
    test(`${view}: configured ${String(value)} respects defaults and waits for Config`, async () => {
      const harness = setup(isDetail);
      harness.change();
      assert.equal(harness.requests.length, 0);
      harness.resolveConfig({ managerOrderStatusFireEventsByDefault: value });
      await Promise.resolve();
      assert.equal(harness.scope.orderStatusConfigLoaded, true);
      harness.change();
      assert.equal(harness.requests[0].notify, typeof value === 'boolean' ? value : !isDetail);
    });
  }

  test(`${view}: failed Config visibly blocks changes`, async () => {
    const harness = setup(isDetail);
    harness.rejectConfig(new Error('offline'));
    await Promise.resolve();
    assert.equal(harness.scope.orderStatusConfigLoaded, false);
    assert.equal(harness.errors.length, 1);
    harness.change();
    assert.equal(harness.requests.length, 0);
    assert.equal(harness.errors.length, 2);
  });
}

test('detail: checkbox remains user-overridable after initialization', async () => {
  for (const value of [true, false]) {
    const harness = setup(true);
    harness.resolveConfig({ managerOrderStatusFireEventsByDefault: value });
    await Promise.resolve();
    harness.checkbox.checked = !value;
    harness.change();
    assert.equal(harness.requests[0].notify, !value);
  }
});

test('legacy resource uses the Config endpoint, not the manager endpoint', () => {
  const source = readFileSync(new URL(`${root}resources/ekmManager.resources.js`, import.meta.url), 'utf8');
  assert.match(source, /return get\(Umbraco\.Sys\.ServerVariables\.ekom\.backofficeApiEndpoint \+ "Config"\)/);
});
