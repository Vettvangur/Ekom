import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

function compile(file) {
  const source = readFileSync(new URL(`../src/catalog/${file}.ts`, import.meta.url), 'utf8');
  const compiled = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  }).outputText;
  const exports = {};
  runInNewContext(compiled, { exports, require: name => assert.fail(`Unexpected import: ${name}`) });
  return exports;
}

const model = compile('catalog-view-model');
const plain = value => JSON.parse(JSON.stringify(value));

test('stock label follows the out of stock, low stock and in stock thresholds', () => {
  assert.deepEqual(plain(model.stockLabel(0)), { text: 'Out of stock', tone: 'red' });
  assert.deepEqual(plain(model.stockLabel(-2)), { text: 'Out of stock', tone: 'red' });
  assert.deepEqual(plain(model.stockLabel(3)), { text: '3 left', tone: 'amber' });
  assert.deepEqual(plain(model.stockLabel(5)), { text: '5 in stock', tone: 'green' });
  assert.deepEqual(plain(model.stockLabel(2.5)), { text: '2.5 left', tone: 'amber' });
});

test('subcategory health reports the most important state first', () => {
  const health = (totalProductCount, liveCount, outOfStockCount) =>
    plain(model.healthLabel({ totalProductCount, liveCount, outOfStockCount }));

  assert.deepEqual(health(0, 0, 0), { text: 'Empty', tone: 'amber' });
  assert.deepEqual(health(4, 0, 4), { text: 'Nothing published', tone: 'amber' });
  assert.deepEqual(health(16, 12, 4), { text: '4 out of stock', tone: 'muted' });
  assert.deepEqual(health(4, 3, 0), { text: '3 live', tone: 'green' });
});

test('meta line pluralises products and subcategories', () => {
  assert.equal(model.metaLine(1, 1), '1 product · 1 subcategory');
  assert.equal(model.metaLine(23, 3), '23 products · 3 subcategories');
  assert.equal(model.metaLine(0, 0), '0 products · 0 subcategories');
});

test('select all toggles the visible keys and keeps selections from other pages', () => {
  assert.deepEqual(plain(model.toggleAll(['a', 'b'], ['x'])), ['x', 'a', 'b']);
  assert.deepEqual(plain(model.toggleAll(['a', 'b'], ['x', 'a', 'b'])), ['x']);
  assert.deepEqual(plain(model.toggleAll([], ['x'])), ['x']);
});

test('only the Umbraco dark theme alias switches to dark tokens', () => {
  assert.equal(model.themeFromAlias('umb-dark-theme'), 'dark');
  assert.equal(model.themeFromAlias('umb-light-theme'), 'light');
  assert.equal(model.themeFromAlias('umb-high-contrast-theme'), 'light');
  assert.equal(model.themeFromAlias(undefined), 'light');
});

test('unpublished products are labelled Draft', () => {
  assert.deepEqual(plain(model.publishLabel({ published: true })), { text: 'Published', tone: 'green' });
  assert.deepEqual(plain(model.publishLabel({ published: false })), { text: 'Draft', tone: 'amber' });
});

test('sort and status values are validated against the known options', () => {
  assert.equal(model.isSortOption('nameDesc'), true);
  assert.equal(model.isSortOption('priceAsc'), false);
  assert.equal(model.sortLabel('nameAsc'), 'Name A–Z');
  assert.equal(model.sortLabel('unknown'), 'Sort order asc');
  assert.equal(model.isStatus('oos'), true);
  assert.equal(model.isStatus('archived'), false);
});

test('pagination collapses long ranges around the current page', () => {
  assert.deepEqual(plain(model.paginationItems(1, 5)), [1, 2, 3, 4, 5]);
  assert.deepEqual(plain(model.paginationItems(6, 12)), [1, '…', 5, 6, 7, '…', 12]);
  assert.deepEqual(plain(model.paginationItems(1, 12)), [1, 2, '…', 12]);
});
