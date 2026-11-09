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
    exports, URL, console,
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

test('database mode overlays prices without changing the node value or emitting an editor change', async () => {
  const { editor } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100 }, { Currency: 'en-GB', Price: 80 }] };
  editor.fetchJson = async url => {
    if (url.endsWith('/Config')) return { useDatabasePrices: true };
    if (url.includes('/Stores/')) return [{ alias: 'Web', currencies: [{ currencyValue: 'en-US' }, { currencyValue: 'en-GB' }] }];
    assert.ok(url.endsWith('/Prices/document-key/ekmDiscountPrice'));
    return [{ storeAlias: 'web', currency: 'en-us', price: 120 }];
  };
  await editor.loadStores();
  assert.equal(editor.getPrice('Web', 'en-US'), 120);
  assert.equal(editor.getPrice('Web', 'en-GB'), 80);
  assert.equal(editor.rawValue.Web[0].Price, 100);
  assert.equal(editor.changes, undefined);
});

test('immediate save writes only the changed currency and reports failure without emitting node changes', async () => {
  const { editor, requests, fail } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100 }] };
  editor.databasePrices = true;
  await editor.savePrice('Web', 'en-US', '125');
  assert.deepEqual(JSON.parse(requests[0].options.body), { storeAlias: 'Web', currency: 'en-US', price: 125 });
  assert.equal(editor.getPrice('Web', 'en-US'), 125);
  assert.equal(editor.changes, undefined);
  fail();
  await editor.savePrice('Web', 'en-US', '150');
  assert.equal(editor.getPrice('Web', 'en-US'), 125);
  assert.equal(editor.error, true);
  assert.equal(editor.saving, false);
});

test('node mode still emits normal property changes', () => {
  const { editor, requests } = fixture();
  editor.setPrice('Web', 'en-US', '55');
  assert.equal(editor.value.Web[0].Price, 55);
  assert.equal(editor.changes, 1);
  assert.equal(requests.length, 0);
});


function loadFixture(editor, rows) {
  editor.fetchJson = async url => url.endsWith('/Config') ? { useDatabasePrices: true }
    : url.includes('/Stores/') ? [{ alias: 'Web', currencies: [{ currencyValue: 'en-US' }] }]
    : rows();
}

test('switching documents discards overrides and accepts the new node fallback', async () => {
  const { editor } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100 }] };
  loadFixture(editor, () => editor.documentId === 'document-key'
    ? [{ storeAlias: 'Web', currency: 'en-US', price: 120 }] : []);
  await editor.loadStores();
  editor.updateDocumentId('second-document');
  editor.value = { Web: [{ Currency: 'en-US', Price: 75 }] };
  await editor.loadStores();
  assert.equal(editor.getPrice('Web', 'en-US'), 75);
  assert.equal(editor.changes, undefined);
});

test('reload after deleting a saved override restores the original node fallback', async () => {
  const { editor } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100 }] };
  let rows = [{ storeAlias: 'Web', currency: 'en-US', price: 120 }];
  loadFixture(editor, () => rows);
  await editor.loadStores();
  await editor.savePrice('Web', 'en-US', '150');
  assert.equal(editor.rawValue.Web[0].Price, 100);
  rows = [];
  await editor.loadStores();
  assert.equal(editor.getPrice('Web', 'en-US'), 100);
});

test('late node values preserve current database overrides', async () => {
  const { editor } = fixture();
  loadFixture(editor, () => [{ storeAlias: 'Web', currency: 'en-US', price: 120 }]);
  await editor.loadStores();
  editor.value = { Web: [{ Currency: 'en-US', Price: 75 }] };
  assert.equal(editor.getPrice('Web', 'en-US'), 120);
  assert.equal(editor.rawValue.Web[0].Price, 75);
});

test('a save finishing after navigation cannot alter the next document', async () => {
  const { editor, defer } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100 }] };
  loadFixture(editor, () => []);
  await editor.loadStores();
  const finish = defer();
  const saving = editor.savePrice('Web', 'en-US', '150');
  editor.updateDocumentId('second-document');
  editor.value = { Web: [{ Currency: 'en-US', Price: 75 }] };
  await editor.loadStores();
  finish();
  await saving;
  assert.equal(editor.getPrice('Web', 'en-US'), 75);
  assert.equal(editor.changes, undefined);
});


test('reloading the same document during a save does not leave the editor disabled', async () => {
  const { editor, defer } = fixture();
  editor.value = { Web: [{ Currency: 'en-US', Price: 100 }] };
  loadFixture(editor, () => []);
  await editor.loadStores();
  const finish = defer();
  const saving = editor.savePrice('Web', 'en-US', '150');
  await editor.loadStores();
  finish();
  await saving;
  assert.equal(editor.saving, false);
  assert.equal(editor.getPrice('Web', 'en-US'), 150);
});


test('clearing workspace identity blocks saves and ignores stale URL fallback', async () => {
  const { editor, requests } = fixture();
  editor.getDocumentIdFromUrl = () => 'document-key';
  loadFixture(editor, () => []);
  await editor.loadStores();
  editor.updateDocumentId(null);
  editor.value = { Web: [{ Currency: 'en-US', Price: 75 }] };
  await editor.savePrice('Web', 'en-US', '80');
  assert.equal(editor.documentId, '');
  assert.equal(requests.length, 0);
  assert.match(editor.message, /Save the document/);
  editor.updateDocumentId('new-document');
  await editor.loadStores();
  await editor.savePrice('Web', 'en-US', '80');
  assert.match(requests[0].url, /Prices\/new-document\//);
});

test('clearing identity invalidates a pending save response', async () => {
  const { editor, defer } = fixture();
  loadFixture(editor, () => []);
  await editor.loadStores();
  const finish = defer();
  const pending = editor.savePrice('Web', 'en-US', '150');
  editor.updateDocumentId(undefined);
  editor.value = { Web: [{ Currency: 'en-US', Price: 75 }] };
  finish();
  await pending;
  assert.equal(editor.getPrice('Web', 'en-US'), 75);
  assert.equal(editor.documentId, '');
});


test('initial node fallback received before identity survives workspace initialization', async () => {
  const { editor } = fixture();
  editor.documentId = '';
  editor.value = { Web: [{ Currency: 'en-US', Price: 75 }] };
  loadFixture(editor, () => []);
  editor.updateDocumentId('first-document');
  await editor.loadStores();
  assert.equal(editor.getPrice('Web', 'en-US'), 75);
});
