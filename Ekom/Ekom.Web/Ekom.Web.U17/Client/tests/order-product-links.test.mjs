import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

function loadModule(relativePath, modules = {}) {
  const source = readFileSync(new URL(relativePath, import.meta.url), 'utf8');
  const compiled = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  }).outputText;
  const exports = {};
  runInNewContext(compiled, {
    exports,
    require: name => {
      assert.ok(modules[name], `Unexpected module: ${name}`);
      return modules[name];
    },
    URL,
    HTMLElement: class {},
    customElements: { define() {} },
  });
  return exports;
}

const shared = loadModule('../src/manager/manager-shared.ts');
const { EkomOrdersSectionViewElement } = loadModule('../src/manager/orders-section-view.element.ts', {
  './manager-shared': shared,
  '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
  '@umbraco-cms/backoffice/notification': { UMB_NOTIFICATION_CONTEXT: Symbol('notification') },
});
const legacyRoot = '../../../Ekom.Web/App_Plugins/Ekom/Manager/';
const legacyController = readFileSync(new URL(`${legacyRoot}controllers/ekmOrder.controller.js`, import.meta.url), 'utf8');
const legacyTemplate = readFileSync(new URL(`${legacyRoot}views/overlays/ekmOrder.html`, import.meta.url), 'utf8');
const legacyFunction = legacyController.match(/\$scope\.getProductUrl = (function \(product\) \{[\s\S]*?\n    \});/);
assert.ok(legacyFunction);
const getLegacyProductUrl = runInNewContext(`(${legacyFunction[1]})`, { URL });

function renderProduct(url, title = 'Product') {
  const order = {
    orderLines: [{ key: 'line', product: { title, sku: 'SKU-1', url }, quantity: 2, variant: { title: 'Blue', sku: 'BLUE-1' } }],
  };
  const before = JSON.stringify(order);
  const html = new EkomOrdersSectionViewElement().renderOrderLines(order);
  assert.equal(JSON.stringify(order), before, 'Rendering must not mutate the saved order');
  return html;
}

test('safe product URLs link only the title in a new tab on both supported frontends', () => {
  for (const url of ['https://shop.example/product', 'http://shop.example/product', '/products/item', '//shop.example/item', 'products/item']) {
    const html = renderProduct(url);
    assert.ok(html.includes(`<a href="${url}" target="_blank" rel="noopener noreferrer">Product</a> (SKU-1)`));
    assert.ok(html.includes('Blue (BLUE-1)</small>'));
    assert.equal(getLegacyProductUrl({ url }), url);
  }
});

test('missing, blank, malformed, and unsafe URLs retain plain product titles', () => {
  for (const url of [undefined, null, '', '   ', 123, {}, 'javascript:alert(1)', 'JaVaScRiPt:alert(1)', 'java\nscript:alert(1)', 'java\tscript:alert(1)', 'data:text/html,test', 'vbscript:test', 'file:///product', 'http://[']) {
    const html = renderProduct(url);
    assert.ok(!html.includes('<a '), `Unexpected link for ${String(url)}`);
    assert.ok(html.includes('Product (SKU-1)'));
    assert.equal(getLegacyProductUrl({ url }), null);
  }
});

test('titles and URL attributes are escaped and surrounding URL whitespace is trimmed', () => {
  const html = renderProduct('  /product?q="quoted"&tag=<value>  ', '<img src=x onerror="alert(1)">');
  assert.ok(html.includes('href="/product?q=&quot;quoted&quot;&amp;tag=&lt;value&gt;"'));
  assert.ok(html.includes('>&lt;img src=x onerror=&quot;alert(1)&quot;&gt;</a>'));
  assert.ok(!html.includes('<img'));
  assert.equal(getLegacyProductUrl({ url: '  /product  ' }), '/product');
});

test('the legacy template uses conditional escaped bindings and keeps the SKU outside the link', () => {
  assert.match(legacyTemplate, /<a ng-if="getProductUrl\(orderLine\.product\)" ng-href="\{\{ getProductUrl\(orderLine\.product\) \}\}" target="_blank" rel="noopener noreferrer">\{\{ orderLine\.product\.title \}\}<\/a>/);
  assert.ok(legacyTemplate.includes('<span ng-if="!getProductUrl(orderLine.product)">{{ orderLine.product.title }}</span> ({{ orderLine.product.sku }})'));
});
