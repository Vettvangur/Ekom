import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import { JSDOM } from 'jsdom';
import ts from 'typescript';

function fixture() {
  const dom = new JSDOM('<main></main>');
  const exports = {};
  runInNewContext(ts.transpileModule(readFileSync(new URL('../src/manager/manager-shared.ts', import.meta.url), 'utf8'), {
    compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 },
  }).outputText, { exports, HTMLElement: dom.window.HTMLElement, customElements: dom.window.customElements, Event: dom.window.Event, CustomEvent: dom.window.CustomEvent });
  exports.managerState.stores = [{ alias: 'main', title: 'Demo store', currencies: ['ISK', 'EUR', 'USD'], currency: { isoCurrencySymbol: 'ISK' } }, { alias: 'outlet', title: 'Outlet', currencies: ['ISK'] }];
  exports.managerState.filters.store = 'main';
  exports.ensureManagerCurrency();
  const host = dom.window.document.querySelector('main');
  host.innerHTML = exports.renderStoreCurrencySelector();
  exports.bindDateRange(host, () => {});
  return { dom, exports, host };
}

test('store groups expose currency chips and default to the configured currency', () => {
  const { dom, exports, host } = fixture();
  assert.equal(exports.managerState.filters.currency, 'ISK');
  assert.equal(host.querySelectorAll('section').length, 2);
  assert.equal(host.querySelectorAll('[aria-pressed=true]').length, 1);
  assert.equal(host.querySelector('summary .ekom-currency-code').textContent, 'ISK');
  dom.window.close();
});

test('selecting a currency commits it before the existing store-change handler and closes the popup', () => {
  const { dom, exports, host } = fixture();
  const observed = [];
  host.querySelector('select').addEventListener('change', event => observed.push([event.target.value, exports.managerState.filters.currency]));
  host.querySelector('details').open = true;
  host.querySelector('[data-header-currency=USD]').click();
  assert.deepEqual(observed, [['main', 'USD']]);
  assert.equal(host.querySelector('details').open, false);
  host.querySelector('[data-header-store=outlet]').click();
  assert.deepEqual(observed[1], ['outlet', 'ISK']);
  dom.window.close();
});

test('Escape closes the picker and returns focus to its trigger', () => {
  const { dom, host } = fixture();
  host.querySelector('details').open = true;
  host.querySelector('button').dispatchEvent(new dom.window.KeyboardEvent('keydown', { key:'Escape', bubbles:true }));
  assert.equal(host.querySelector('details').open, false);
  assert.equal(dom.window.document.activeElement, host.querySelector('summary'));
  dom.window.close();
});
