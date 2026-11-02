import assert from 'node:assert/strict';
import test from 'node:test';
import { loadPrintFixture } from './fixtures/order-print.mjs';

test('print contains all order lines and saved order data independently of overlay state', () => {
  const { element, order } = loadPrintFixture();
  const html = element.renderPrintableOrder(order);
  for (const value of ['PRINT-100', 'Order status: Paid', 'Test Customer', 'Recipient', 'Delivery street 20', 'Full customer note', 'SHIP-', 'Product 1 ', 'Product 60 ', 'Blue / Large', 'Keep this line information', 'Discount (AUTUMN)', 'Sub Total', 'Total']) {
    assert.ok(html.includes(value), value);
  }
  for (let index = 1; index <= 60; index++) assert.ok(html.includes(`Product ${index} with a long descriptive name`));
  assert.equal(element.trackingExpanded, false);
  assert.equal(element.consentExpanded, false);
});

test('print excludes operational panels, tracking, consent, activity logs and backoffice shell', () => {
  const { element, order } = loadPrintFixture();
  element.orderLogs = [{ message: 'DO-NOT-PRINT-ACTIVITY' }];
  const html = element.renderPrintableOrder(order);
  for (const value of ['DO-NOT-PRINT', 'Order Actions', 'Failed gift-card preparation', 'ekmOverlay', 'Fire events', 'Activity log', '<select']) {
    assert.ok(!html.includes(value), value);
  }
});

test('print escapes order fields and includes the same-as-billing fallback', () => {
  const { element, order } = loadPrintFixture();
  order.referenceId = '<script>alert(1)</script>';
  order.customerInformation.shipping = {};
  const html = element.renderPrintableOrder(order);
  assert.ok(html.includes('&lt;script&gt;'));
  assert.ok(!html.includes('<script>'));
  assert.ok(html.includes('Same as billing address'));
});
