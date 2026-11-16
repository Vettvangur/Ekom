import assert from 'node:assert/strict';
import test from 'node:test';
import { JSDOM } from 'jsdom';
import { loadPrintFixture } from './fixtures/order-print.mjs';

// The other order tests stub the document, which leaves stored values as they are. Decoding
// needs a real one.
function fixture() {
  return loadPrintFixture({ document: new JSDOM('').window.document });
}

// "Þórður & Sons' <b>" as HtmlEncoder.Default stores it, and the same value encoded once more
// the way the storefront order API stores provider custom data.
const text = "Þórður & Sons' <b>";
const once = '&#xDE;&#xF3;r&#xF0;ur &amp; Sons&#x27; &lt;b&gt;';
const twice = '&amp;#xDE;&amp;#xF3;r&amp;#xF0;ur &amp;amp; Sons&amp;#x27; &amp;lt;b&amp;gt;';
const shown = 'Þórður &amp; Sons&#39; &lt;b&gt;';

function assertDecoded(html, label) {
  assert.ok(html.includes(shown), `${label} shows the decoded text`);
  assert.ok(!html.includes('&#x'), `${label} shows no encoded characters`);
  assert.ok(!html.includes('&amp;amp;') && !html.includes('&amp;#'), `${label} is not double-encoded`);
  assert.ok(!html.includes('<b>'), `${label} never becomes markup`);
}

test('order custom data is shown decoded and stays plain text', () => {
  const { element, order } = fixture();
  order.customData = { Note: once, Legacy: twice, Script: '&lt;img src=x onerror=alert(1)&gt;' };
  const before = JSON.stringify(order.customData);
  const html = element.renderOrderCustomData(order);
  assert.equal(html.split(shown).length - 1, 2);
  assertDecoded(html, 'custom data');
  assert.ok(!html.includes('<img'));
  assert.ok(html.includes('&lt;img src=x onerror=alert(1)&gt;'));
  assertDecoded(element.renderPrintableOrder(order), 'printed custom data');
  assert.equal(JSON.stringify(order.customData), before);
});

test('provider custom data is shown decoded however many times it was encoded', () => {
  const { element, order } = fixture();
  order.shippingProvider.customData = { customshippingNote: twice };
  order.paymentProvider.customData = { custompaymentReference: once };
  assertDecoded(element.renderShipment(order), 'shipping custom data');
  assertDecoded(element.renderPaymentDetails(order), 'payment custom data');
  const print = element.renderPrintableOrder(order);
  assert.equal(print.split(shown).length - 1, 2);
  assertDecoded(print, 'printed provider data');
});

test('customer information and order line properties are shown decoded', () => {
  const { element, order } = fixture();
  const customer = order.customerInformation.customer;
  Object.assign(customer, { name: once, email: 'jon&#x2B;test@example.com', phone: '&#x2B;354 555 0100', address: once, city: 'Akranesb&#xE6;r', country: 'IS', properties: { customerCompany: twice } });
  order.customerInformation.shipping = {};
  order.orderLines = [{ ...order.orderLines[0], orderLineInfo: { properties: { orderlineEngraving: once } } }];
  const html = element.renderOrderDetails(order);
  // Sidebar name, billing address name and street, delivery address, extra customer data and the order line.
  assert.equal(html.split(shown).length - 1, 6);
  assertDecoded(html, 'order details');
  assert.ok(html.includes('<p>jon+test@example.com</p>'));
  assert.ok(html.includes('<p>+354 555 0100</p>'));
  assert.ok(html.includes('<span>Akranesbær</span>'));
});

test('the order list shows the customer decoded', () => {
  const { element } = fixture();
  const html = element.renderOrderRow({ uniqueId: 'a', referenceId: '1', customerName: once, customerEmail: 'jon&#x2B;test@example.com' });
  assertDecoded(html, 'order row');
  assert.ok(html.includes('<span>jon+test@example.com</span>'));
});

test('editors start from decoded values, so saving does not encode them again', () => {
  const { element, order } = fixture();
  Object.assign(order.customerInformation.customer, { name: once, properties: { customerCompany: twice } });
  element.selectedOrder = order;
  element.render = () => {};
  element.openCustomerEditor();
  const fields = Object.fromEntries(element.customerEditModel.customer.map(field => [field.key, field.value]));
  assert.equal(fields.customerName, text);
  assert.equal(fields.customerCompany, text);
  assert.ok(element.renderCustomerFields(element.customerEditModel.customer, 'customer').includes(`value="${shown}"`));
});

test('text without encoded characters is left alone', () => {
  const { element, order } = fixture();
  order.customData = { Note: '5 < 6 & 7 > 2', Code: 'R&D; 100% &c' };
  const html = element.renderOrderCustomData(order);
  assert.ok(html.includes('5 &lt; 6 &amp; 7 &gt; 2'));
  assert.ok(html.includes('R&amp;D; 100% &amp;c'));
});
