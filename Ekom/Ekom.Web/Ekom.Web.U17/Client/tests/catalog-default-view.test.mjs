import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

const CATALOG = 'Ekom.CollectionView.Catalog';
const TABLE = 'Umb.CollectionView.Document.Table';
const GRID = 'Umb.CollectionView.Document.Grid';

function compile(file, modules, globals = {}) {
  const source = readFileSync(new URL(`../src/catalog/${file}.ts`, import.meta.url), 'utf8');
  const compiled = ts.transpileModule(source, {
    compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
  }).outputText;
  const exports = {};
  runInNewContext(compiled, { exports, require: name => {
    assert.ok(name in modules, `Unexpected import: ${name}`);
    return modules[name];
  }, ...globals });
  return exports;
}

/** A minimal observable: emits the current value on subscribe and again on every change. */
function state(value) {
  const listeners = [];
  return {
    get value() { return value; },
    set(next) { value = next; listeners.forEach(listener => listener(value)); },
    subscribe(listener) { listeners.push(listener); listener(value); },
  };
}

function setup({ stored = null, routed = false, config = {}, contentType = 'ekmCategory', views = [CATALOG, TABLE, GRID] } = {}) {
  const storage = new Map(stored == null ? [] : [['ekom.catalog.collectionView', stored]]);
  const window = { localStorage: {
    getItem: key => storage.get(key) ?? null,
    setItem: (key, value) => storage.set(key, value),
    removeItem: key => storage.delete(key),
  } };
  const pickerContext = compile('catalog-picker-context', {});
  const { useCatalogAsDefaultView } = compile('catalog-default-view', { './catalog-picker-context': pickerContext }, { window });

  const viewsState = state([]);
  const currentView = state(undefined);
  const setConfigCalls = [];
  const viewManager = {
    views: viewsState,
    currentView,
    getCurrentView: () => currentView.value,
    setCurrentView: view => currentView.set(view),
    setConfig: viewConfig => {
      setConfigCalls.push(viewConfig);
      // A routed manager rebuilds its routes and lands on the new fallback view.
      currentView.set(viewsState.value.find(view => view.alias === viewConfig.defaultViewAlias));
    },
  };
  if (routed) viewManager.routes = state([]);

  const collection = { getConfig: () => config, view: viewManager };
  const host = { observe: (source, callback) => source.subscribe(callback) };
  const manifests = views.map(alias => ({ alias }));
  const start = () => useCatalogAsDefaultView(host, collection, contentType);
  /** Umbraco resolves the available views and lands on its own table default. */
  const land = () => {
    viewsState.set(manifests);
    if (currentView.value == null) currentView.set(manifests.find(view => view.alias === TABLE));
  };
  const pick = alias => currentView.set(manifests.find(view => view.alias === alias));

  return { start, land, pick, storage, setConfigCalls, current: () => currentView.value?.alias, collection, host, useCatalogAsDefaultView };
}

test('catalog nodes land on the Catalog view instead of the table', () => {
  for (const contentType of ['ekmCatalog', 'ekmCategory']) {
    const collection = setup({ contentType });
    collection.land();
    collection.start();
    assert.equal(collection.current(), CATALOG);
    assert.deepEqual(collection.setConfigCalls, []);
  }
});

test('the Catalog view is chosen when it becomes available after Umbraco has landed', () => {
  const collection = setup();
  collection.start();
  assert.equal(collection.current(), undefined);
  collection.land();
  assert.equal(collection.current(), CATALOG);
});

test('pickers keep the Umbraco default view', () => {
  const collection = setup({ config: { selectionConfiguration: { selectOnly: true } } });
  collection.land();
  collection.start();
  assert.equal(collection.current(), TABLE);
  assert.equal(collection.storage.size, 0);
});

test('documents that are not catalog nodes keep the Umbraco default view', () => {
  for (const contentType of [null, 'ekmProduct', 'blogList']) {
    const collection = setup({ contentType });
    collection.land();
    collection.start();
    assert.equal(collection.current(), TABLE);
  }
});

test('a collection without the Catalog view is left alone', () => {
  const collection = setup({ views: [TABLE, GRID] });
  collection.land();
  collection.start();
  assert.equal(collection.current(), TABLE);
  assert.equal(collection.storage.size, 0);
});

test('the view a user picks is remembered, and picking Catalog restores the default', () => {
  const first = setup();
  first.land();
  first.start();
  first.pick(GRID);
  assert.equal(first.storage.get('ekom.catalog.collectionView'), GRID);

  const second = setup({ stored: GRID });
  second.land();
  second.start();
  assert.equal(second.current(), GRID);
  second.pick(CATALOG);
  assert.equal(second.storage.size, 0);
});

test('a remembered table view survives landing and can be changed again', () => {
  const collection = setup({ stored: TABLE });
  collection.land();
  collection.start();
  assert.equal(collection.current(), TABLE);
  assert.equal(collection.storage.get('ekom.catalog.collectionView'), TABLE);
  collection.pick(CATALOG);
  assert.equal(collection.storage.size, 0);
});

test('the landing view Umbraco chose is never stored as a preference', () => {
  const collection = setup();
  collection.start();
  collection.land();
  assert.equal(collection.storage.size, 0);
});

test('routed view managers get Catalog as their fallback view, keeping configured layouts', async () => {
  const layouts = [{ collectionView: CATALOG }, { collectionView: TABLE }];
  const collection = setup({ routed: true, config: { layouts } });
  collection.land();
  collection.start();
  assert.equal(collection.current(), TABLE, 'Routes change only after the other observers of the views have run');
  await Promise.resolve();
  assert.equal(collection.setConfigCalls.length, 1);
  assert.equal(collection.setConfigCalls[0].defaultViewAlias, CATALOG);
  assert.deepEqual(collection.setConfigCalls[0].viewsOverride, layouts);
  assert.equal(collection.current(), CATALOG);
});

test('a collection is only set up once', async () => {
  const collection = setup({ routed: true });
  collection.land();
  collection.start();
  await Promise.resolve();
  collection.pick(TABLE);
  collection.start();
  assert.equal(collection.setConfigCalls.length, 1);
  assert.equal(collection.current(), TABLE);
});

test('a routed manager that already shows the preferred view is not reconfigured', async () => {
  const collection = setup({ routed: true });
  collection.land();
  collection.start();
  collection.pick(CATALOG);
  await Promise.resolve();
  assert.deepEqual(collection.setConfigCalls, []);
});
