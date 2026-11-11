import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

function loadModule(path, modules = {}, globals = {}) {
  const source = readFileSync(new URL(path, import.meta.url), 'utf8');
  const compiled = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  }).outputText;
  const exports = {};
  runInNewContext(compiled, {
    exports,
    require: name => { assert.ok(modules[name], `Unexpected module: ${name}`); return modules[name]; },
    HTMLElement: class {},
    customElements: { define() {} },
    URLSearchParams,
    Error,
    ...globals,
  });
  return exports;
}

const shared = loadModule('../src/manager/manager-shared.ts');
shared.renderManagerContent = (root, html) => { root.innerHTML = html; };
const { EkomOrdersSectionViewElement } = loadModule('../src/manager/orders-section-view.element.ts', {
  './manager-shared': shared,
  '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => Base },
  '@umbraco-cms/backoffice/notification': { UMB_NOTIFICATION_CONTEXT: Symbol('notification') },
});


function element() {
  const view = new EkomOrdersSectionViewElement();
  view.render = () => {};
  view.selectedOrder = { uniqueId: 'order-A', referenceId: 12 };
  view.activeReview = { id: 'review-A', title: '<Failed>', message: '<script>bad</script>', source: 'accounting', createdUtc: '2026-11-10T12:00:00Z' };
  view.loadOrders = view.loadOrderLogs = view.loadOrderReviews = async () => {};
  return view;
}

test('review dialog escapes integration data and defaults to resolving', () => {
  const view = element();
  const html = view.renderReviewDialog();
  assert.ok(html.includes('&lt;Failed&gt;'));
  assert.ok(!html.includes('<script>'));
  assert.match(html, /data-review-resolved checked/);
  assert.match(html, /Mark as reviewed/);
});

test('note-only submission keeps the alert open and refreshes lists and logs', async () => {
  const view = element();
  view.querySelector = selector => selector.includes('comment') ? { value: 'Checked invoice' } : { checked: false };
  const calls = [];
  view.api.reviewOrder = async (...args) => calls.push(args);
  await view.saveReview();
  assert.deepEqual(calls, [['order-A', 'review-A', 'Checked invoice', false]]);
  assert.equal(view.activeReview, undefined);
  assert.equal(view.reviewSaving, false);
});

test('empty notes do not send a request and failed saves keep the dialog', async () => {
  const view = element();
  view.querySelector = selector => selector.includes('comment') ? { value: '' } : { checked: false };
  view.api.reviewOrder = async () => assert.fail('must not save');
  await view.saveReview();
  assert.match(view.reviewError, /Describe/);
  view.querySelector = selector => selector.includes('comment') ? { value: 'Note' } : { checked: true };
  view.api.reviewOrder = async () => { throw new Error('Unable to save'); };
  await view.saveReview();
  assert.equal(view.activeReview.id, 'review-A');
  assert.equal(view.reviewSaving, false);
  assert.match(view.reviewError, /Unable to save/);
});

test('typing a review note survives a late background log response', async () => {
  const view = element();
  let inputHandler;
  const textarea = { value: '', addEventListener: (name, handler) => { if (name === 'input') inputHandler = handler; } };
  shared.bindDateRange = () => {};
  view.querySelector = selector => selector === '[data-review-comment]' ? textarea : null;
  view.querySelectorAll = () => [];
  view.bindEvents();
  textarea.value = 'Invoice checked; waiting for accounting';
  inputHandler({ target: textarea });
  let finish;
  view.api.orderLogs = () => new Promise(resolve => { finish = resolve; });
  delete view.loadOrderLogs;
  let rendered;
  view.render = () => { rendered = view.renderReviewDialog(); };
  const loading = view.loadOrderLogs('order-A');
  finish([]);
  await loading;
  assert.ok(rendered.includes(textarea.value));
});

test('a stale-review conflict retains the note and dialog', async () => {
  const view = element();
  view.querySelector = selector => selector.includes('comment') ? { value: 'Investigation notes' } : { checked: false };
  view.api.reviewOrder = async () => { throw new Error('This alert has already been resolved. Your note was not saved.'); };
  await view.saveReview();
  assert.equal(view.activeReview.id, 'review-A');
  assert.equal(view.reviewComment, 'Investigation notes');
  assert.match(view.renderReviewDialog(), /Investigation notes/);
  assert.match(view.reviewError, /already been resolved/);
});

test('Needs review is hidden at zero and visible when there are alerts', () => {
  const view = element();
  view.viewCounts = { NeedsReview: 0 };
  assert.match(view.renderOrders(), /data-view="NeedsReview" hidden/);
  view.viewCounts.NeedsReview = 2;
  assert.doesNotMatch(view.renderOrders(), /data-view="NeedsReview" hidden/);
});
