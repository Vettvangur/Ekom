import assert from 'node:assert/strict';
import test from 'node:test';
import { loadPrintFixture } from './fixtures/order-print.mjs';

test('custom data appears below actions and above billing, and in print', () => {
  const { element, order } = loadPrintFixture();
  element.orderActions = [{ key: 'custom-action', label: 'Test action', enabled: true }];
  order.customData = { 'Gift wrap': 'Yes - birthday card included', Warehouse: 'Reykjavik - A2' };
  const html = element.renderOrderDetails(order);
  assert.ok(html.indexOf('Order Actions') < html.indexOf('ekmOrderCustomData'));
  assert.ok(html.indexOf('ekmOrderCustomData') < html.indexOf('<h4>Billing'));
  assert.ok(html.includes('Yes - birthday card included'));
  assert.ok(element.renderPrintableOrder(order).includes('Reykjavik - A2'));
});

test('custom data is plain text and long values are not truncated', () => {
  const { element, order } = loadPrintFixture();
  const note = 'Leave with neighbour. '.repeat(100);
  order.customData = { '<script>key</script>': '<img src=x onerror=alert(1)> &amp;', Note: note };
  const html = element.renderOrderCustomData(order);
  assert.ok(!html.includes('<script>'));
  assert.ok(!html.includes('<img'));
  assert.ok(html.includes('&lt;Script&gt;'));
  assert.ok(html.includes('&amp;amp;'));
  assert.ok(html.includes(note));
});

test('missing and empty custom data do not render a panel or empty badges', () => {
  const { element, order } = loadPrintFixture();
  for (const data of [undefined, null, {}, { Empty: '', Whitespace: '  ' }]) {
    order.customData = data;
    assert.equal(element.renderOrderCustomData(order), '');
  }
  order.customData = { Empty: '', Zero: '0', False: 'false' };
  const html = element.renderOrderCustomData(order);
  assert.ok(!html.includes('Empty'));
  assert.ok(html.includes('>0</dd>'));
  assert.ok(html.includes('>false</dd>'));
});


test('custom data labels separate camel case and separators without changing stored data', () => {
  const { element, order } = loadPrintFixture();
  order.customData = { newsletterOptIn: 'false', giftReceipt: 'yes', dkInvoiceNumber: '244362', delivery_note: 'Leave outside', VATNumber: '123' };
  const before = JSON.stringify(order.customData);
  const html = element.renderOrderCustomData(order);
  for (const label of ['Newsletter Opt In', 'Gift Receipt', 'Dk Invoice Number', 'Delivery Note', 'VAT Number']) {
    assert.ok(html.includes(`>${label}</dt>`));
    assert.ok(element.renderPrintableOrder(order).includes(`>${label}</dt>`));
  }
  assert.ok(html.includes('>false</dd>'));
  assert.equal(JSON.stringify(order.customData), before);
});
