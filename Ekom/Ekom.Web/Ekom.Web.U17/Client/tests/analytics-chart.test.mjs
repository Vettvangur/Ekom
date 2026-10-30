import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

const source = readFileSync(new URL('../src/manager/manager-shared.ts', import.meta.url), 'utf8');
const exports = {};
runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS } }).outputText,
  { exports, window: { devicePixelRatio: 2 } });

function render(points, options = {}) {
  const labels = [];
  const dots = [];
  const ctx = {
    scale() {}, clearRect() { labels.length = 0; dots.length = 0; },
    measureText(text) { return { width: text.length * 6 }; },
    fillText(text) { labels.push(text); },
    beginPath() {}, moveTo() {}, lineTo() {}, stroke() {}, fill() {}, fillRect() {},
    arc(x, y) { dots.push({ x, y }); },
  };
  const canvas = { style: {}, getContext: () => ctx,
    getBoundingClientRect: () => ({ width: 600, height: 320, left: 10, top: 20 }) };
  exports.renderLineChart(canvas, { points, labels: [] }, 'blue', options);
  return { canvas, labels, dots };
}

test('chart shows dates and values, and hovering each point displays its exact date and amount', () => {
  const points = [{ x: '2026-10-01', y: 125.25 }, { x: '2026-10-02', y: 250.5 }];
  const { canvas, labels, dots } = render(points);
  assert.ok(labels.includes('2026-10-01'));
  assert.ok(labels.includes('2026-10-02'));
  assert.ok(labels.includes('250.5'));
  const positions = [...dots];
  positions.forEach((point, index) => {
    canvas.onmousemove({ clientX: point.x + 10, clientY: point.y + 20 });
    assert.deepEqual(labels.slice(-2), [points[index].x, String(points[index].y)]);
    assert.equal(canvas.style.cursor, 'pointer');
  });
  canvas.onmouseleave();
  assert.equal(canvas.style.cursor, 'default');
  assert.equal(labels.length, 7);
});

test('empty, single-point, zero and negative series render finite coordinates', () => {
  for (const values of [[], [0], [-20, 0, 10]]) {
    const { labels, dots } = render(values.map(y => ({ x: '2026-10-01', y })));
    assert.ok(dots.every(({ x, y }) => Number.isFinite(x) && Number.isFinite(y)));
    if (!values.length) assert.ok(labels.includes('No data for this period'));
  }
});


test('most sold table renders API productCount and total, including zero values', () => {
  const analyticsExports = {};
  const analyticsSource = readFileSync(new URL('../src/manager/analytics-section-view.element.ts', import.meta.url), 'utf8');
  runInNewContext(ts.transpileModule(analyticsSource, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, {
    exports: analyticsExports,
    require: name => name === './manager-shared' ? exports : { UmbElementMixin: Base => Base },
    HTMLElement: class {}, customElements: { define() {} },
  });
  const view = new analyticsExports.EkomAnalyticsSectionViewElement();
  view.mostSoldProducts = { products: [{ title: 'Product', sku: 'SKU', productCount: 4, total: 37.5 }], totalPages: 1 };
  let html = view.renderMostSoldProducts();
  assert.ok(html.includes('>4</div>'));
  assert.ok(html.includes('>37.50</div>'));
  view.mostSoldProducts.products[0] = { productCount: 0, total: 0 };
  html = view.renderMostSoldProducts();
  assert.ok(html.includes('>0</div>'));
  assert.ok(html.includes('>0.00</div>'));
});


test('order chart ticks remain distinct whole numbers for low counts', () => {
  const { labels } = render([{ x: '2026-10-01', y: 1 }], { integer: true });
  assert.deepEqual(labels.slice(0, 5), ['4', '3', '2', '1', '0']);
});

test('monetary chart labels and tooltips use the store currency and precision', () => {
  const currency = { isoCurrencySymbol: 'ISK', currencyDecimalDigits: 0 };
  const { canvas, labels, dots } = render([{ x: '2026-10-01', y: 1500 }], { currency });
  assert.ok(labels[0].includes('ISK'));
  canvas.onmousemove({ clientX: dots[0].x + 10, clientY: dots[0].y + 20 });
  assert.equal(labels.at(-1), exports.formatAnalyticsAmount(1500, currency));
  assert.ok(!labels.at(-1).includes('.00'));
  assert.ok(exports.formatAnalyticsAmount(12.5, { isoCurrencySymbol: 'USD', currencyDecimalDigits: 2 }).includes('12.50'));
});

test('analytics observes size changes and disconnects the observer when removed', () => {
  let callback;
  let observed;
  let disconnected = false;
  class Observer {
    constructor(fn) { callback = fn; }
    observe(target) { observed = target; }
    disconnect() { disconnected = true; }
  }
  const module = {};
  const source = readFileSync(new URL('../src/manager/analytics-section-view.element.ts', import.meta.url), 'utf8');
  runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText, {
    exports: module, ResizeObserver: Observer,
    require: name => name === './manager-shared' ? exports : { UmbElementMixin: Base => Base },
    HTMLElement: class { connectedCallback() {} disconnectedCallback() {} }, customElements: { define() {} },
  });
  const view = new module.EkomAnalyticsSectionViewElement();
  view.render = () => {};
  view.initialize = async () => {};
  let redraws = 0;
  view.renderCharts = () => { redraws++; };
  view.connectedCallback();
  assert.equal(observed, view);
  callback();
  assert.equal(redraws, 1);
  view.disconnectedCallback();
  assert.equal(disconnected, true);
});
