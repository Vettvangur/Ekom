import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

export function loadPrintFixture() {
  function load(name, modules = {}) {
    const source = readFileSync(new URL(`../../src/manager/${name}.ts`, import.meta.url), 'utf8');
    const code = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS } }).outputText;
    const exports = {};
    runInNewContext(code, { exports, document: { createElement: () => ({ set innerHTML(value) { this.value = value; } }) }, require: name => modules[name], HTMLElement: class {}, customElements: { define() {} } });
    return { exports, code };
  }
  const shared = load('manager-shared');
  const view = load('orders-section-view.element', {
    './manager-shared': shared.exports,
    '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
    '@umbraco-cms/backoffice/notification': {},
  });
  const element = new view.exports.EkomOrdersSectionViewElement();
  shared.exports.managerState.statusList = [{ value: 'paid', label: 'Paid' }];
  const money = { currencyString: '1,234.00 ISK' };
  const order = {
    referenceId: 'PRINT-100', uniqueId: 'order-print-test', orderStatus: 'paid',
    createDate: '2026-11-02T10:00:00Z', paidDate: '2026-11-02T10:01:00Z',
    storeAlias: 'Test store', currency: 'ISK', chargedAmount: money, chargedVat: money,
    subTotal: { withVat: money }, discountAmount: money, coupon: 'AUTUMN',
    customerInformation: { customer: { name: 'Test Customer', address: 'Example street 10', properties: { customerNote: 'Full customer note' } }, shipping: { name: 'Recipient', address: 'Delivery street 20' } },
    paymentProvider: { title: 'Card', price: { withVat: money } },
    shippingProvider: { title: 'Delivery', price: { withVat: money }, customData: { customshippingReference: 'SHIP-' + '0123456789'.repeat(35) } },
    tracking: { source: 'DO-NOT-PRINT-TRACKING' }, consent: { source: 'DO-NOT-PRINT-CONSENT' },
    orderLines: Array.from({ length: 60 }, (_, index) => ({
      key: `line-${index}`, quantity: 2,
      product: { title: `Product ${index + 1} with a long descriptive name`, sku: `SKU-${index + 1}`, price: { withVat: money } },
      variant: { title: 'Blue / Large', sku: 'VARIANT' },
      orderLineInfo: { properties: { orderlineNote: 'Keep this line information' } },
      amount: { withVat: money, vat: money, discountAmount: money },
    })),
  };
  return { element, order, sharedCode: shared.code };
}
