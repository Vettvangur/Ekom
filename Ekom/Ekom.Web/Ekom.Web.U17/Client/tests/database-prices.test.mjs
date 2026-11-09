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
    fetch: async (url, options) => { requests.push({ url, options }); return response; },
  });
  const editor = new exports.EkomPriceEditorElement();
  editor.alias = 'ekmDiscountPrice';
  editor.documentId = 'document-key';
  editor.renderPrices = () => {};
  editor.setStatus = (message, error) => { editor.message = message; editor.error = error; };
  return { editor, requests, fail: () => { response = { ok: false, status: 500 }; } };
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
