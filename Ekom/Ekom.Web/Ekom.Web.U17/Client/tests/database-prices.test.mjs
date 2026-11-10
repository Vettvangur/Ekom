import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

function fixture() {
  const source = readFileSync(new URL('../src/property-editors/price-editor.element.ts', import.meta.url), 'utf8');
  const code = ts.transpileModule(source, { compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS } }).outputText;
  const exports = {};
  const requests = [];
  let response = { ok: true };
  runInNewContext(code, {
    exports, URL, console, crypto: { randomUUID: () => `edit-${Math.random()}` },
    require: name => name.includes('element-api') ? { UmbElementMixin: Base => Base } : { UmbChangeEvent: class {} },
    HTMLElement: class {
      hasAttribute() { return false; }
      querySelectorAll() { return []; }
      dispatchEvent() { this.changes = (this.changes ?? 0) + 1; }
    },
    customElements: { define() {} },
    fetch: async (url, options) => { requests.push({ url, options }); return await response; },
  });
  const editor = new exports.EkomPriceEditorElement();
  editor.alias = 'ekmDiscountPrice';
  editor.documentId = 'document-key';
  editor.renderPrices = () => {};
  editor.setStatus = (message, error) => { editor.message = message; editor.error = error; };
  return { editor, requests, defer: () => { let resolve; response = new Promise(done => { resolve = done; }); return () => resolve({ ok: true }); }, fail: () => { response = { ok: false, status: 500 }; } };
}

function loadFixture(editor, data = { prices: [] }) {
  editor.fetchJson = async url => url.endsWith('/Config') ? { useDatabasePrices: true }
    : url.includes('/Stores/') ? [{ alias: 'Web', currencies: [{ currencyValue: 'en-US' }, { currencyValue: 'en-GB' }] }]
    : data;
}

test('loading live prices does not change node values or mark the document dirty', async () => {
  const { editor, requests } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100 }] };
  loadFixture(editor, { prices: [{ storeAlias: 'Web', currency: 'en-US', price: 120 }] });
  await editor.loadStores();
  assert.equal(editor.getPrice('Web', 'en-US'), 120);
  assert.equal(editor.value.Web[0].Price, 100);
  assert.equal(editor.changes, undefined);
  assert.equal(requests.length, 0);
});

test('editing a database price stages one currency and never sends a live write', async () => {
  const { editor, requests } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100 }] };
  loadFixture(editor, { prices: [{ storeAlias: 'Web', currency: 'en-GB', price: 90 }] });
  await editor.loadStores();
  editor.setPrice('Web', 'en-US', '125');
  assert.equal(editor.value.Web[0].Price, 125);
  assert.ok(editor.value.Web[0].EditId);
  assert.equal(editor.value.Web.length, 1); // Do not persist unrelated overlaid currencies or default zeroes.
  assert.equal(editor.getPrice('Web', 'en-GB'), 90);
  assert.equal(editor.changes, 1);
  assert.equal(requests.length, 0);
  assert.equal(editor.message, ''); // Successful staging does not add instructional UI text.
});

test('pending edits survive a refresh of live table overrides', async () => {
  const { editor } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 125, EditId: 'draft' }] };
  loadFixture(editor, { prices: [{ storeAlias: 'Web', currency: 'en-US', price: 160 }] });
  await editor.loadStores();
  assert.equal(editor.getPrice('Web', 'en-US'), 125);
  assert.equal(editor.value.Web[0].EditId, 'draft');
  assert.equal(editor.changes, undefined);
});

test('a saved field with cleared edit markers displays the latest ERP price', async () => {
  const { editor } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 125 }] };
  loadFixture(editor, { prices: [{ storeAlias: 'Web', currency: 'en-US', price: 160 }] });
  await editor.loadStores();
  assert.equal(editor.getPrice('Web', 'en-US'), 160);
  assert.equal(editor.value.Web[0].Price, 125);
  assert.equal(editor.changes, undefined);
});

test('deliberately setting the old node value still records an edit after ERP changes', async () => {
  const { editor } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100 }] };
  loadFixture(editor, { prices: [{ storeAlias: 'Web', currency: 'en-US', price: 150 }] });
  await editor.loadStores();
  editor.setPrice('Web', 'en-US', '100');
  assert.equal(editor.value.Web[0].Price, 100);
  assert.ok(editor.value.Web[0].EditId);
  assert.equal(editor.changes, 1);
});

test('node mode continues to emit ordinary field changes', () => {
  const { editor, requests } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100 }] };
  editor.setPrice('Web', 'en-US', '55');
  assert.equal(editor.value.Web[0].Price, 55);
  assert.equal(editor.value.Web[0].EditId, undefined);
  assert.equal(editor.changes, 1);
  assert.equal(requests.length, 0);
});

test('switching documents clears pending edits and displayed overrides', async () => {
  const { editor } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100, EditId: 'draft' }] };
  loadFixture(editor);
  await editor.loadStores();
  editor.updateDocumentId('second');
  editor.value = { Web: [{ Currency: 'en-US', Price: 75 }] };
  await editor.loadStores();
  assert.equal(editor.getPrice('Web', 'en-US'), 75);
  assert.equal(editor.value.Web[0].EditId, undefined);
});

test('clearing document identity prevents further edits', () => {
  const { editor } = fixture();
  editor.updateDocumentId(null);
  editor.setPrice('Web', 'en-US', '50');
  assert.equal(editor.changes, undefined);
});

test('save response clears pending edits and reloads the new live price', async () => {
  const { editor, requests } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100 }] };
  const data = { prices: [{ storeAlias: 'Web', currency: 'en-US', price: 100 }] };
  loadFixture(editor, data);
  await editor.loadStores();
  editor.setPrice('Web', 'en-US', '125');
  data.prices[0].price = 125;
  editor.value = { Web: [{ Currency: 'en-US', Price: 125 }] };
  await editor.loadStores();
  assert.equal(editor.getPrice('Web', 'en-US'), 125);
  assert.equal(editor.value.Web[0].EditId, undefined);
  assert.equal(requests.length, 0);
});

test('Umbraco 13 stages field edits for document saving without a PUT request', async () => {
  const source = readFileSync(new URL('../../../Ekom.Web/App_Plugins/Ekom/DataTypes/PriceEditor/ekmPrice.controller.js', import.meta.url), 'utf8');
  let controller;
  let submitted;
  let ready;
  const loaded = new Promise(resolve => { ready = resolve; });
  const requests = [];
  const scope = { model: { alias: 'price', value: { Web: [{ Currency: 'en-US', Price: 100 }] } },
    $on: (_, handler) => { submitted = handler; } };
  Object.defineProperty(scope, 'priceStatus', { set: value => { if (value === '') ready(); } });
  runInNewContext(source, {
    angular: { module: () => ({ controller: (_, definition) => { controller = definition.at(-1); } }),
      copy: value => value == null ? value : JSON.parse(JSON.stringify(value)) },
    Umbraco: { Sys: { ServerVariables: { ekom: { backofficeApiEndpoint: '/api/' } } } },
    window: { crypto: { randomUUID: () => 'edit' } }, console,
  });
  controller(scope, { getStoresByNode: async () => [{ alias: 'Web', currencies: [{ currencyValue: 'en-US' }, { currencyValue: 'en-GB' }] }] },
    { section: 'content', id: '200' }, { get: async url => {
      requests.push(url);
      return { data: url.endsWith('Config') ? { useDatabasePrices: true }
        : { prices: [{ storeAlias: 'Web', currency: 'en-US', price: 150 }] } };
    }, put: () => assert.fail('Field edits must not write live prices') });
  await loaded;
  assert.equal(scope.prices.Web[0].Price, 150);
  scope.changePrice('Web', 'en-US', 125);
  submitted();
  assert.equal(scope.model.value.Web[0].Price, 125);
  assert.equal(scope.model.value.Web[0].EditId, 'edit');
  assert.equal(scope.model.value.Web.length, 1);
  assert.equal(requests.length, 2);
});
