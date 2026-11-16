import assert from 'node:assert/strict';
import test from 'node:test';
import { loadPrintFixture } from './fixtures/order-print.mjs';

const created = {
  shipmentId: '9d2f0000-0000-0000-0000-00000000a111', kind: 'Outbound', state: 'Created', provider: 'DHL Express - Home delivery',
  carrierAlias: 'dhl-express', accountReference: 'demostore', serviceId: 'EXP', serviceName: 'DHL Express Worldwide',
  trackingNumber: '1Z999AA10123456784', trackingUrl: 'https://carrier.example/track/1Z999AA10123456784',
  carrierShipmentId: '1Z999AA10123456784', shipmentReference: 'DS-1259', carrierStatusText: 'Shipment information received',
  attempts: 2, pickupLocationName: 'Kringlan', pickupLocationAddress: 'Kringlunni 4', pickupLocationPostalCode: '103', pickupLocationCity: 'Reykjavik',
  createdUtc: '2026-04-10T20:17:00Z', updatedUtc: '2026-04-10T20:18:45Z',
  details: [{ key: 'pieces', label: 'Pieces', value: '2', copyable: false }],
  events: [
    { type: 'State', state: 'Submitting', occurredUtc: '2026-04-10T20:17:10Z', actor: 'dhl-express' },
    { type: 'State', state: 'Failed', occurredUtc: '2026-04-10T20:17:20Z', actor: 'dhl-express', message: 'Rejected: <b>postal code</b>' },
    { type: 'State', state: 'Created', occurredUtc: '2026-04-10T20:18:45Z', actor: 'dhl-express' },
    { type: 'Manual', state: 'Created', occurredUtc: '2026-04-11T08:00:00Z', actor: 'anna' },
  ],
};
const deleted = {
  shipmentId: '9d2f0000-0000-0000-0000-00000000a000', kind: 'Outbound', state: 'Deleted', carrierAlias: 'dhl-express',
  trackingNumber: 'OLD-TRACK-1', attempts: 1, createdUtc: '2026-04-09T10:00:00Z', updatedUtc: '2026-04-09T11:00:00Z', details: [],
  events: [{ type: 'State', state: 'Deleted', occurredUtc: '2026-04-09T11:00:00Z', actor: 'anna' }],
};

test('shipment box shows the recorded shipment, its facts and its history', () => {
  const { element, order } = loadPrintFixture();
  order.shippingProvider.customData = {
    customshippingTrackingNumber: 'STALE-NUMBER', customshippingShipmentState: 'Failed', customshippingServiceId: 'EXP',
    customshippingGiftNote: 'Leave at the door',
  };
  element.orderShipments = { current: created, shipments: [created, deleted] };
  const html = element.renderShipment(order);
  for (const text of ['1Z999AA10123456784', 'Track at DHL Express', 'DHL Express Worldwide', 'Kringlan, Kringlunni 4, 103 Reykjavik',
    'Shipment information received', '<dt>Attempts</dt><dd>2</dd>', '<dt>Shipment reference</dt>', 'DS-1259', '<dt>Pieces</dt>',
    'Shipment history (4)', 'Changed by hand', 'Earlier shipments', 'OLD-TRACK-1', 'data-action="open-shipment-tracking-editor"'])
    assert.ok(html.includes(text), text);
  // The record wins over what is still on the order's shipping custom data.
  assert.ok(!html.includes('STALE-NUMBER'));
  assert.ok(!html.includes('<dt>ServiceId</dt>'));
  // Custom data that is not about the shipment stays in the list.
  assert.ok(html.includes('<dt>Gift Note</dt>'));
  assert.ok(html.includes('Leave at the door'));
  assert.ok(html.includes('ekom-shipment-state--ok'));
  assert.ok(html.includes('ekom-shipment-state--muted'));
  assert.ok(html.includes('Rejected: &lt;b&gt;postal code&lt;/b&gt;'));
  assert.ok(!html.includes('<b>postal'));
});

test('a failed shipment shows why', () => {
  const { element, order } = loadPrintFixture();
  const failed = { ...created, state: 'OutcomeUnknown', trackingNumber: null, trackingUrl: null, lastError: 'The connection dropped.' };
  element.orderShipments = { current: failed, shipments: [failed] };
  const html = element.renderShipment(order);
  assert.ok(html.includes('Outcome unknown'));
  assert.ok(html.includes('ekom-shipment-state--alert'));
  assert.ok(html.includes('<p class="ekom-shipment-error" role="alert">The connection dropped.</p>'));
  assert.ok(html.includes('Not available'));
  assert.ok(!html.includes('Track at'));
});

test('a tracking link that is not a web address is never linked', () => {
  const { element, order } = loadPrintFixture();
  const unsafe = { ...created, trackingUrl: 'javascript:alert(1)' };
  element.orderShipments = { current: unsafe, shipments: [unsafe] };
  assert.ok(!element.renderShipment(order).includes('javascript:'));
});

test('orders without a recorded shipment keep reading the shipping custom data', () => {
  const { element, order } = loadPrintFixture();
  order.shippingProvider.customData = { customshippingTrackingNumber: 'LEGACY-1', customshippingShipmentState: 'Created' };
  element.orderShipments = null;
  const html = element.renderShipment(order);
  assert.ok(html.includes('LEGACY-1'));
  assert.ok(html.includes('<dt>TrackingNumber</dt>'));
  assert.ok(!html.includes('Shipment history'));
});
