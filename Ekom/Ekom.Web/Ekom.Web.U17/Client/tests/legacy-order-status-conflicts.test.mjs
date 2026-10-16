import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';

const roots = {
  packaged: '../../../Ekom.Web/App_Plugins/Ekom/Manager/',
  sample: '../../../../../Samples/U10/Ekom.Site/App_Plugins/Ekom/Manager/',
};

function setup(root, isDetail, failure) {
  const source = readFileSync(new URL(`${root}controllers/${isDetail ? 'ekmOrder' : 'ekmManager'}.controller.js`, import.meta.url), 'utf8');
  const savedStatus = { value: 'ReadyForDispatch' };
  const scope = {
    orderStatusConfigLoaded: true,
    orderChangeStatus: { value: 'Processed' },
    model: { editModel: { order: { orderStatus: savedStatus.value } } },
    getStatus: value => value === savedStatus.value ? savedStatus : null,
  };
  const checkbox = { checked: false };
  const errors = [];
  const successes = [];
  const requests = [];
  const order = { uniqueId: 'order-1', orderStatusCol: 'Processed' };
  let reloads = 0;
  let activityLoads = 0;
  let changedEvents = 0;
  let click;
  scope.GetData = () => {
    reloads++;
    order.orderStatusCol = savedStatus.value;
  };
  const context = {
    $scope: scope,
    resources: {
      Config: () => Promise.resolve({ data: { managerOrderStatusFireEventsByDefault: true } }),
      ChangeOrderStatus: query => {
        requests.push(query);
        return failure ? Promise.reject(failure) : Promise.resolve();
      },
    },
    notificationsService: { error: (...args) => errors.push(args), success: (...args) => successes.push(args) },
    document: { getElementById: id => id === 'notifyOrderStatus' ? checkbox : { addEventListener: (_, handler) => { click = handler; } } },
    orderStatusFireEventsByDefault: true,
    getCurrentOrderId: () => order.uniqueId,
    loadActivityLogs: () => activityLoads++,
    eventsService: { emit: () => changedEvents++ },
  };
  const code = isDetail
    ? source.slice(source.indexOf('    var changeOrderStatusButton ='), source.indexOf('    $scope.isDefaultKey ='))
    : source.match(/\$scope\.onStatusChanged = function \(order\) \{[\s\S]*?\n    \};/)[0];
  runInNewContext(code, context);
  return {
    scope, checkbox, errors, successes, requests, order,
    get reloads() { return reloads; },
    get activityLoads() { return activityLoads; },
    get changedEvents() { return changedEvents; },
    change: () => isDetail ? click() : scope.onStatusChanged(order),
  };
}

for (const [copy, root] of Object.entries(roots)) {
  for (const isDetail of [false, true]) {
    const name = `${copy} ${isDetail ? 'detail' : 'table'}`;
    const guidance = 'Checkout is still in progress. Wait for checkout to finish, then retry.';
    const failures = [
      [{ status: 409, data: { message: guidance } }, guidance],
      [{ status: 409, data: { message: '   ' } }, 'Error updating order status.'],
      [{ status: 409, data: { message: { exception: 'stack trace' } } }, 'Error updating order status.'],
      [{ status: 409, data: '<html>Server error</html>' }, 'Error updating order status.'],
      [{ status: 500, data: { message: 'Exception: stack trace' } }, 'Error updating order status.'],
      [{ status: 500, data: '<html>Server error</html>' }, 'Error updating order status.'],
      [{ status: 0 }, 'Error updating order status.'],
    ];

    for (const [failure, message] of failures) {
      test(`${name}: ${failure.status} ${JSON.stringify(failure.data)} restores status and shows safe error`, async () => {
        const harness = setup(root, isDetail, failure);
        await Promise.resolve();
        harness.checkbox.checked = false;
        harness.change();
        await Promise.resolve();

        assert.deepEqual(harness.errors, [['Error', message]]);
        assert.equal(harness.successes.length, 0);
        assert.equal(harness.activityLoads, 0);
        assert.equal(harness.changedEvents, 0);
        if (isDetail) {
          assert.equal(harness.scope.orderChangeStatus.value, 'ReadyForDispatch');
          assert.equal(harness.scope.model.editModel.order.orderStatus, 'ReadyForDispatch');
          assert.equal(harness.checkbox.checked, false);
          assert.equal(harness.requests[0].notify, false);
        } else {
          assert.equal(harness.reloads, 1);
          assert.equal(harness.order.orderStatusCol, 'ReadyForDispatch');
        }
      });
    }

    test(`${name}: success retains the new status without recovery reload`, async () => {
      const harness = setup(root, isDetail);
      await Promise.resolve();
      harness.change();
      await Promise.resolve();
      assert.equal(harness.errors.length, 0);
      assert.equal(harness.successes.length, 1);
      assert.equal(harness.reloads, 0);
      if (isDetail) {
        assert.equal(harness.scope.model.editModel.order.orderStatus, 'Processed');
        assert.equal(harness.activityLoads, 1);
        assert.equal(harness.changedEvents, 1);
      }
    });
  }
}
