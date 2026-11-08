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
    URLSearchParams,
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
  assert.ok(legacyTemplate.includes('<span ng-if="!getProductUrl(orderLine.product)">{{ orderLine.product.title }}</span><span ng-if="orderLine.product.sku && orderLine.product.sku.trim()"> ({{ orderLine.product.sku }})</span>'));
});

const imageFunction = legacyController.match(/\$scope\.getOrderLineImageUrl = (function \(line, width\) \{[\s\S]*?\n    \});/);
assert.ok(imageFunction);
const getLegacyImageUrl = runInNewContext(`(${imageFunction[1]})`, { URLSearchParams, $scope: { getProductUrl: getLegacyProductUrl } });

test('order thumbnails prefer variant images and fall back to valid product images', () => {
  for (const [variantImages, productImages, expected] of [
    [[{ url: '/variant.jpg' }], [{ url: '/product.jpg' }], '/variant.jpg'],
    [[], [{ url: '/product.jpg' }], '/product.jpg'],
    [[{ url: 'javascript:alert(1)' }], [{ url: '/product.jpg' }], '/product.jpg'],
    [null, [{ url: '' }, { url: ' /second.jpg ' }], '/second.jpg'],
    [undefined, undefined, null],
    [{ url: '/invalid-shape.jpg' }, [], null],
    [[null, {}], [{ url: 'data:text/html,bad' }], null],
  ]) {
    const line = { product: { title: 'Product', images: productImages }, variant: { images: variantImages } };
    const html = new EkomOrdersSectionViewElement().renderOrderLines({ orderLines: [line] });
    assert.ok(html.includes('class="ekmOrderLineThumbnail"'));
    assert.ok(html.includes('<svg'));
    if (expected) assert.ok(html.includes(`src="${expected}?width=56" width="56" height="56" alt="" loading="lazy"`));
    else assert.ok(!html.includes('<img'));
    assert.equal(getLegacyImageUrl(line), expected);
  }
});

test('failed or already broken thumbnails reveal the placeholder', () => {
  const image = { complete: false, naturalWidth: 0, hidden: false, addEventListener(event, handler) { this[event] = handler; } };
  const element = new EkomOrdersSectionViewElement();
  element.querySelector = () => null;
  element.querySelectorAll = selector => selector === '.ekmOrderLineThumbnail img' ? [image] : [];
  element.bindEvents();
  assert.equal(image.hidden, false);
  image.error();
  assert.equal(image.hidden, true);
  image.hidden = false;
  image.complete = true;
  element.bindEvents();
  assert.equal(image.hidden, true);
});


test('picture thumbnails request 1x and 2x widths without losing existing query or fragments', () => {
  const line = { product: { title: 'Product', images: [{ url: '/media/photo.jpg?quality=80&width=900#image' }] } };
  const html = new EkomOrdersSectionViewElement().renderOrderLines({ orderLines: [line] });
  assert.ok(html.includes('<picture><source srcset="/media/photo.jpg?quality=80&amp;width=56#image 1x, /media/photo.jpg?quality=80&amp;width=112#image 2x">'));
  assert.ok(html.includes('loading="lazy" decoding="async"'));
  assert.equal(getLegacyImageUrl(line, 56), '/media/photo.jpg?quality=80&width=56#image');
  assert.equal(getLegacyImageUrl(line, 112), '/media/photo.jpg?quality=80&width=112#image');
  assert.ok(!html.includes('width=900'));
  assert.ok(legacyTemplate.includes('<picture ng-if="getOrderLineImageUrl(orderLine)">'));
  assert.ok(legacyTemplate.includes('ng-attr-srcset="{{ getOrderLineImageUrl(orderLine, 56) }} 1x, {{ getOrderLineImageUrl(orderLine, 112) }} 2x"'));
});

test('legacy custom-data labels match readable modern labels', () => {
  const match = legacyController.match(/\$scope\.formatOrderCustomDataKey = (function \(key\) \{[\s\S]*?\n    \});/);
  assert.ok(match);
  const formatKey = runInNewContext(`(${match[1]})`);
  for (const [key, expected] of [['newsletterOptIn', 'Newsletter Opt In'], ['giftReceipt', 'Gift Receipt'], ['dkInvoiceNumber', 'Dk Invoice Number'], ['VATNumber', 'VAT Number'], ['delivery_note', 'Delivery Note']]) {
    assert.equal(formatKey(key), expected);
  }
  assert.ok(legacyTemplate.includes('ng-bind="formatOrderCustomDataKey(entry[0])"'));
});
