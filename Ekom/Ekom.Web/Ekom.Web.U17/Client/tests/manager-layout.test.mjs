import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { loadPrintFixture } from './fixtures/order-print.mjs';

const status = { orderStatus: 'Open', paymentStatus: 'Paid', fulfillmentStatus: 'Unfulfilled', requiresReview: false, legacyStatus: 'ReadyForDispatch' };

test('order list distinguishes paid checkout from fulfilled goods in the new design', () => {
  const { element } = loadPrintFixture();
  const html = element.renderOrderRow({ uniqueId: 'id', referenceId: 42, status, customerName: '<script>alert(1)</script>', formattedTotal: '10 ISK' });
  const document = new JSDOM(html).window.document;
  assert.deepEqual(Array.from(document.querySelectorAll('.ekom-badge'), node => node.textContent), ['Paid', 'Unfulfilled']);
  assert.equal(document.querySelector('script'), null);
  assert.ok(document.querySelector('[data-action="view-order"]')); 
});

test('missing historical evidence does not invent paid or fulfilled statuses', () => {
  const { element } = loadPrintFixture();
  const html = element.renderOrderRow({ uniqueId: 'id', status: { ...status, orderStatus: 'Cancelled', paymentStatus: null, fulfillmentStatus: null, requiresReview: true } });
  const document = new JSDOM(html).window.document;
  assert.deepEqual(Array.from(document.querySelectorAll('.ekom-badge'), node => node.textContent), ['Needs review', 'Needs review']);
});

test('switching order tabs preserves pending editor values and uses accessible selection state', () => {
  const { element, order } = loadPrintFixture();
  order.status = status;
  const document = new JSDOM(element.renderOrderDetails(order)).window.document;
  element.querySelectorAll = document.querySelectorAll.bind(document);
  const pending = document.createElement('input');
  pending.value = 'Unsaved note';
  document.querySelector('#ekom-order-panel-order').append(pending);
  element.selectOrderTab('activity');
  assert.equal(document.querySelector('#ekom-order-panel-order').hidden, true);
  assert.equal(document.querySelector('#ekom-order-panel-activity').hidden, false);
  assert.equal(document.querySelector('#ekom-order-tab-activity').getAttribute('aria-selected'), 'true');
  assert.equal(document.activeElement.id, 'ekom-order-tab-activity');
  element.selectOrderTab('order');
  assert.equal(pending.value, 'Unsaved note');
  element.selectOrderTab('invalid');
  assert.equal(element.orderTab, 'order');
});

test('checkout recovery stays outside hidden tabs and the three design tabs retain real actions', () => {
  const { element, order } = loadPrintFixture();
  element.orderLockStatus = { hasLock: true, paymentOperationLocked: true, canRelease: false };
  const document = new JSDOM(element.renderOrderDetails(order)).window.document;
  assert.equal(document.querySelectorAll('[role="tabpanel"]').length, 3);
  const release = document.querySelector('[data-action="release-order-lock"]');
  assert.ok(release);
  assert.equal(release.closest('[hidden]'), null);
  assert.ok(document.querySelector('[data-action="open-customer-editor"]'));
  assert.ok(document.querySelector('[data-action="open-shipping-provider-editor"]'));
  assert.ok(document.querySelector('[data-action="print-order"]'));
});
