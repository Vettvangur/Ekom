import assert from 'node:assert/strict';
import test from 'node:test';
import { loadPrintFixture } from './fixtures/order-print.mjs';

const captured = {
  paymentId: 'b83d0000-0000-0000-0000-0000000041c7', attemptId: '52bd0000-0000-0000-0000-00000000e810', provider: 'Borgun',
  status: 'Paid', amount: 24990, currency: 'ISK', transactionId: 'TX-7741-0926-55813', createdUtc: '2026-11-12T14:05:40Z',
  capturedUtc: '2026-11-12T14:06:18Z', authorizationCode: '123456', maskedCardNumber: '5587-40**-****-0014', cardBrand: 'Mastercard',
  details: [{ key: 'rrn', label: 'RRN', value: '000000020512', copyable: true }],
  events: [
    { type: 'Requested', amount: 24990, currency: 'ISK', occurredUtc: '2026-11-12T14:05:40Z', actor: 'Customer' },
    { type: 'Captured', amount: 24990, currency: 'ISK', occurredUtc: '2026-11-12T14:06:18Z', actor: 'Borgun', transactionId: 'TX-7741-0926-55813' },
  ],
};
const declined = {
  paymentId: '7c1e0000-0000-0000-0000-00000000a9f2', provider: 'Borgun', status: 'Failed', amount: 24990, currency: 'ISK',
  createdUtc: '2026-11-12T14:02:11Z', message: 'Declined: <b>insufficient funds</b>', details: [],
  events: [{ type: 'Failed', amount: 24990, currency: 'ISK', occurredUtc: '2026-11-12T14:03:02Z', actor: 'Borgun', message: 'Declined' }],
};

test('payment card shows the recorded payment and every attempt', () => {
  const { element, order } = loadPrintFixture();
  element.orderPayments = {
    current: captured, attempts: [captured, declined],
    manualChanges: [{ type: 'Manual', amount: 0, occurredUtc: '2026-11-12T15:00:00Z', actor: 'anna', manualStatus: 'Paid' }],
  };
  const html = element.renderPaymentDetails(order);
  for (const text of ['Transaction ID', 'TX-7741-0926-55813', 'Authorization code', '123456', 'Mastercard 5587-40**-****-0014', 'RRN', '000000020512'])
    assert.ok(html.includes(text), text);
  assert.ok(!html.includes('<dt>Paid date</dt>'));
  assert.ok(html.includes('Payment attempts (2)'));
  assert.ok(html.includes('data-copy="b83d0000-0000-0000-0000-0000000041c7"'));
  assert.ok(html.includes('Set to'));
  assert.ok(html.includes('by anna'));
  assert.ok(html.includes('Declined: &lt;b&gt;insufficient funds&lt;/b&gt;'));
  assert.ok(!html.includes('<b>insufficient'));
});

test('an authorised payment says it is not captured', () => {
  const { element, order } = loadPrintFixture();
  const authorised = { ...captured, status: 'Authorized', capturedUtc: null, authorizedUtc: '2026-11-12T14:06:00Z' };
  element.orderPayments = { current: authorised, attempts: [authorised], manualChanges: [] };
  const html = element.renderPaymentDetails(order);
  assert.ok(html.includes('<dt>Captured</dt><dd><span>Not captured</span></dd>'));
  assert.ok(html.includes('<dt>Authorised</dt>'));
});

test('orders without recorded payments keep the previous card and show no attempts list', () => {
  const { element, order } = loadPrintFixture();
  element.orderPayments = null;
  const html = element.renderPaymentDetails(order);
  assert.ok(html.includes('<dt>Paid date</dt>'));
  assert.ok(!html.includes('Payment attempts'));
  assert.ok(!html.includes('Transaction ID'));
});
