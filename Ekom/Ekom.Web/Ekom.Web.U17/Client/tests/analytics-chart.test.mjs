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
    createLinearGradient() { return { addColorStop() {} }; }, closePath() {},
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

test('analytics amounts use the store currency culture, matching Orders', () => {
  const isk = { isoCurrencySymbol: 'ISK', currencyValue: 'is-IS', currencyDecimalDigits: 0 };
  assert.equal(exports.formatAnalyticsAmount(24774950, isk).replace(/\s/g, ' '), '24.774.950 kr.');
  assert.equal(exports.formatAnalyticsAmount(12.5, { isoCurrencySymbol: 'USD', currencyValue: 'en-US', currencyDecimalDigits: 2 }), '$12.50');
  // The store's format wins over browser locale data, which lacks some cultures (Icelandic in Chrome).
  const store = { alias: 'main', title: 'Main', currency: { isoCurrencySymbol: 'ISK' }, currencyFormats: [
    { code: 'ISK', symbol: 'kr.', groupSeparator: '.', decimalSeparator: ',', decimalDigits: 0, positivePattern: 3 },
    { code: 'EUR', symbol: '€', groupSeparator: '.', decimalSeparator: ',', decimalDigits: 2, positivePattern: 3 },
    { code: 'GBP', symbol: '£', groupSeparator: ',', decimalSeparator: '.', decimalDigits: 2, positivePattern: 0 },
  ] };
  assert.equal(exports.formatAnalyticsAmount(24794150, exports.storeCurrency(store, 'ISK')), '24.794.150 kr.');
  assert.equal(exports.formatAnalyticsAmount(24794150, exports.storeCurrency(store, undefined)), '24.794.150 kr.');
  assert.equal(exports.formatAnalyticsAmount(999.6, exports.storeCurrency(store, 'ISK')), '1.000 kr.');
  assert.equal(exports.formatAnalyticsAmount(-1500, exports.storeCurrency(store, 'ISK')), '-1.500 kr.');
  assert.equal(exports.formatAnalyticsAmount(-0.2, exports.storeCurrency(store, 'ISK')), '0 kr.');
  assert.equal(exports.formatAnalyticsAmount(1234.5, exports.storeCurrency(store, 'EUR')), '1.234,50 €');
  assert.equal(exports.formatAnalyticsAmount(1234.5, exports.storeCurrency(store, 'GBP')), '£1,234.50');
  assert.ok(exports.formatAnalyticsAmount(5, exports.storeCurrency(store, 'USD')).includes('USD'));
  for (const currencyValue of ['ISK', 'not a culture', '']) {
    assert.ok(exports.formatAnalyticsAmount(1500, { isoCurrencySymbol: 'ISK', currencyValue, currencyDecimalDigits: 0 }).includes('ISK'));
  }
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
    exports: module, ResizeObserver: Observer, window: { clearTimeout() {}, clearInterval() {} },
    require: name => name === './manager-shared' ? exports : { UmbElementMixin: Base => Base },
    HTMLElement: class { connectedCallback() {} disconnectedCallback() {} hasAttribute() { return false; } }, customElements: { define() {} },
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

test('column hover and drill-through work on empty average days with comparison values', () => {
  let selected = -1;
  const { canvas, labels } = render([{ x: '2026-10-01', y: null }, { x: '2026-10-02', y: 20 }], {
    columnHover: true, onSelect: index => { selected = index; },
    comparison: { points: [{ x: '2026-09-29', y: 10 }, { x: '2026-09-30', y: null }] },
  });
  const event = { clientX: 100, clientY: 100 };
  canvas.onmousemove(event);
  assert.deepEqual(labels.slice(-4), ['2026-10-01', '—', 'Previous: 2026-09-29', '10']);
  canvas.onclick(event);
  assert.equal(selected, 0);
});


test('template area chart retains null gaps and exact column selection without permanent point markers', () => {
  let selected = -1;
  const { canvas, dots, labels } = render([{ x:'a', y:10 }, { x:'b', y:null }, { x:'c', y:20 }], {
    templateStyle:true, columnHover:true, onSelect:index => { selected=index; },
  });
  assert.equal(dots.length, 0);
  canvas.onmousemove({ clientX:326, clientY:100 });
  assert.equal(labels.at(-1), '—');
  canvas.onclick({ clientX:326, clientY:100 });
  assert.equal(selected,1);
});

test('compact template axes keep the exact amount in the tooltip', () => {
  const { canvas, labels } = render([{ x:'1 Sep', y:18543.25 }, { x:'2 Sep', y:62000 }], {
    templateStyle:true, columnHover:true,
    formatAxisValue:value => `${value/1000}k`,
    formatValue:value => `ISK ${value}`,
  });
  assert.ok(labels.includes('64k'));
  canvas.onmousemove({ clientX:12, clientY:100 });
  assert.deepEqual(labels.slice(-2), ['1 Sep', 'ISK 18543.25']);
});

test('loading placeholders follow the manager design and announce only the status line', () => {
  const count = (html, pattern) => (html.match(pattern) || []).length;
  const year = exports.dateRangePresets().find(preset => preset.label === 'This year');
  Object.assign(exports.managerState.filters, { dateFrom: year.from, dateTo: year.to });
  const section = exports.renderSectionLoading('Analytics', 3, 300);
  assert.match(section, /role="status"><span class="ekom-loading-spinner" aria-hidden="true"><\/span>Loading Analytics for This year…/);
  assert.equal(count(section, /class="ekom-loading-card"/g), 3);
  assert.match(section, /class="ekom-loading-panel" style="height:300px"/);
  assert.equal(count(section, /role="status"/g), 1);
  assert.equal(count(exports.renderSectionLoading('Customers', 5, 220), /class="ekom-loading-card"/g), 5);

  Object.assign(exports.managerState.filters, { dateFrom: '2020-02-03', dateTo: '2020-02-09' });
  assert.match(exports.renderLoadingStatus('<Customers>'), /Loading &lt;Customers&gt; for 2020-02-03 – 2020-02-09…/);

  const orders = exports.renderOrderRowsLoading();
  assert.equal(count(orders, /class="ekom-list-row ekom-list-skeleton-row" aria-hidden="true"/g), 6);
  assert.equal(count(orders, /class="ekom-skeleton"/g), 36);
  assert.match(orders, /role="status">Loading orders…/);

  const rows = exports.renderRowsLoading('Calculating…');
  assert.equal(count(rows, /<div aria-hidden="true">/g), 4);
  assert.match(rows, /<p role="status">Calculating…<\/p>/);
  assert.match(exports.managerStyles, /@keyframes ekom-shim/);
  assert.match(exports.managerStyles, /prefers-reduced-motion: reduce\) \{ \.ekom-skeleton, \.ekom-loading-spinner \{ animation: none; \}/);
});

test('dates follow the culture configured on the selected store, not the browser', () => {
  const icelandic = { culture: 'is-IS', shortDate: 'd.M.yyyy', shortTime: 'HH:mm', monthDay: 'd. MMMM', yearMonth: 'MMMM yyyy', dateSeparator: '.', timeSeparator: ':', amDesignator: 'f.h.', pmDesignator: 'e.h.',
    monthNames: ['janúar', 'febrúar', 'mars', 'apríl', 'maí', 'júní', 'júlí', 'ágúst', 'september', 'október', 'nóvember', 'desember'],
    abbreviatedMonthNames: ['jan.', 'feb.', 'mar.', 'apr.', 'maí', 'jún.', 'júl.', 'ágú.', 'sep.', 'okt.', 'nóv.', 'des.'],
    shortestDayNames: ['su', 'má', 'þr', 'mi', 'fi', 'fö', 'la'] };
  const american = { ...icelandic, culture: 'en-US', shortDate: 'M/d/yyyy', shortTime: 'h:mm tt', monthDay: 'MMMM d', dateSeparator: '/', amDesignator: 'AM', pmDesignator: 'PM',
    monthNames: ['January', 'February', 'March', 'April', 'May', 'June', 'July', 'August', 'September', 'October', 'November', 'December'],
    abbreviatedMonthNames: ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'] };
  const state = exports.managerState;
  const previous = { stores: state.stores, store: state.filters.store };
  try {
    state.stores = [{ alias: 'is', title: 'IS', dateFormat: icelandic }, { alias: 'us', title: 'US', dateFormat: american }, { alias: 'plain', title: 'Plain' }];
    const moment = new Date(2026, 10, 5, 14, 7, 9);

    state.filters.store = 'is';
    assert.equal(exports.formatDate(moment), '5.11.2026 14:07');
    assert.equal(exports.formatDay('2026-11-05'), '5.11.2026');
    assert.equal(exports.formatDay('2026-01-01'), '1.1.2026', 'a plain day is never shifted by the time zone');
    assert.equal(exports.formatDayPart('2026-11-05', 'monthDay'), '5. nóv.');
    assert.equal(exports.formatDayPart('2026-11-05', 'yearMonth', false), 'nóvember 2026');
    assert.equal(exports.formatDayPart('2026-11-05', 'month'), 'nóv.');
    Object.assign(state.filters, { dateFrom: '2020-02-03', dateTo: '2020-02-09' });
    assert.equal(exports.dateRangeLabel(), '3.2.2020 – 9.2.2020');

    state.filters.store = 'us';
    assert.equal(exports.formatDate(moment), '11/5/2026 2:07 PM');
    assert.equal(exports.formatDate(new Date(2026, 0, 2, 0, 5)), '1/2/2026 12:05 AM');
    assert.equal(exports.formatDayPart('2026-11-05', 'monthDay'), 'Nov 5');

    assert.equal(exports.formatDatePattern({ year: 2026, month: 3, day: 9, hour: 7, minute: 4, second: 2 }, "dd/MM/yy 'kl.' HH:mm:ss \\d", { ...icelandic, dateSeparator: '-' }), '09-03-26 kl. 07:04:02 d');

    // Without a store format the browser's own formatting is kept.
    state.filters.store = 'plain';
    assert.equal(exports.storeDateFormat(), undefined);
    assert.equal(exports.dateRangeLabel(), '2020-02-03 – 2020-02-09');
    assert.equal(exports.formatDate(''), '');
    assert.equal(exports.formatDate('not a date'), 'not a date');
  } finally {
    state.stores = previous.stores;
    state.filters.store = previous.store;
  }
});
