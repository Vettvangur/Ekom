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

function renderOrder(coupon) {
  const order = {
    coupon,
    orderLines: [],
    discountAmount: { currencyString: '1,500 ISK' },
    chargedAmount: { currencyString: '8,500 ISK' },
  };
  const before = JSON.stringify(order);
  const html = new EkomOrdersSectionViewElement().renderOrderLines(order);
  assert.equal(JSON.stringify(order), before, 'Rendering must not change order data');
  return html;
}

test('the discount total shows the saved coupon code and unchanged amount', () => {
  const html = renderOrder('Spring10');
  assert.ok(html.includes('>Discount (Spring10)</div>'));
  assert.ok(html.includes('>-1,500 ISK</div>'));
  assert.ok(html.includes('>8,500 ISK</strong>'));
  assert.ok(html.includes('>Discount</div>'), 'The line-discount column heading stays unchanged');
});

test('missing, blank, and non-string coupon values keep the plain discount label', () => {
  for (const coupon of [undefined, null, '', '   ', 123, {}]) {
    const html = renderOrder(coupon);
    assert.ok(!html.includes('Discount ('), `Unexpected coupon label for ${String(coupon)}`);
    assert.ok(html.includes('>Discount</div>'));
    assert.ok(html.includes('>-1,500 ISK</div>'));
  }
});

test('coupon codes are escaped as text rather than rendered as HTML', () => {
  const html = renderOrder('<img src=x onerror="alert(1)">&\'');
  assert.ok(html.includes('Discount (&lt;img src=x onerror=&quot;alert(1)&quot;&gt;&amp;&#39;)'));
  assert.ok(!html.includes('<img'));
});

test('surrounding whitespace is removed without changing code casing', () => {
  assert.ok(renderOrder('  Spring10  ').includes('>Discount (Spring10)</div>'));
});
