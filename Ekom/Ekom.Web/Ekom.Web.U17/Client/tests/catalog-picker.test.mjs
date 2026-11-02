import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

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

const pickerContext = compile('catalog-picker-context', {});
const collectionToken = Symbol('collection');
class Controller {
  permitted = false;
  consumers = new Map();
  observers = new Map();
  constructor(host, args) { this.config = args?.config; }
  consumeContext(token, callback) { this.consumers.set(token, callback); }
  observe(source, callback, alias) {
    this.observers.set(alias, () => callback(source?.value));
    if (source) callback(source.value);
  }
}
const { default: Condition } = compile('catalog-picker.condition', {
  '@umbraco-cms/backoffice/collection': { UMB_COLLECTION_CONTEXT: collectionToken },
  '@umbraco-cms/backoffice/extension-registry': { UmbConditionBase: Controller },
  './catalog-picker-context': pickerContext,
});

function createCondition(config, allowInPickers = false) {
  const condition = new Condition({}, { config: { allowInPickers } });
  condition.consumers.get(collectionToken)({ getConfig: () => config, selectOnly: { value: true }, filter: { value: {} } });
  return condition;
}

test('picker exclusion follows explicit configuration, not the bulk selection state', () => {
  for (const config of [undefined, {}, { selectionConfiguration: { multiple: true } }, { selectionConfiguration: { selectOnly: false } }]) {
    assert.equal(createCondition(config).permitted, true);
  }
  assert.equal(createCondition({ selectionConfiguration: { selectOnly: true } }).permitted, false);
});

test('picker availability can be explicitly enabled', () => {
  assert.equal(createCondition({ selectionConfiguration: { selectOnly: true } }, true).permitted, true);
});

test('condition responds to configuration changes and missing collection context', () => {
  const config = {};
  const condition = createCondition(config);
  config.selectionConfiguration = { selectOnly: true };
  condition.observers.get('ekomCatalogPickerConfig')();
  assert.equal(condition.permitted, false);
  config.selectionConfiguration.selectOnly = false;
  condition.observers.get('ekomCatalogPickerMode')();
  assert.equal(condition.permitted, true);
  condition.consumers.get(collectionToken)(undefined);
  assert.equal(condition.permitted, false);
});

function createView(config) {
  const calls = { listeners: [], history: [], loads: [], selection: [], nativeViews: [] };
  const nativeTable = { tagName: 'UMB-DOCUMENT-TABLE-COLLECTION-VIEW' };
  class Element {
    style = { setProperty() {} };
    children = [];
    replaceChildren(...children) { this.children = children; }
  }
  class NativeViewInitializer {
    constructor(host, registry, alias, callback) {
      Object.assign(this, { host, alias, component: nativeTable, callback });
      calls.nativeViews.push(this);
    }
    destroy() { this.destroyed = true; }
  }
  const modules = {
    '@umbraco-cms/backoffice/collection': { UMB_COLLECTION_CONTEXT: collectionToken },
    '@umbraco-cms/backoffice/element-api': { UmbElementMixin: Base => class extends Base {
      consumers = new Map();
      connectedCallback() {}
      disconnectedCallback() {}
      consumeContext(token, callback) { this.consumers.set(token, callback); }
      observe() {}
    } },
    '@umbraco-cms/backoffice/extension-api': { UmbExtensionElementInitializer: NativeViewInitializer },
    '@umbraco-cms/backoffice/extension-registry': { umbExtensionsRegistry: {} },
    '@umbraco-cms/backoffice/imaging': {},
    './catalog-picker-context': pickerContext,
  };
  const window = {
    location: { href: 'https://localhost/umbraco/section/content/workspace/document/edit/11111111-1111-1111-1111-111111111111/collection/catalog' },
    localStorage: { getItem() { return null; }, setItem() {} },
    sessionStorage: { getItem() { return null; }, setItem() {}, removeItem() {} },
    addEventListener(name) { calls.listeners.push(name); },
    removeEventListener() {}, setTimeout() { return 1; }, clearTimeout() {},
  };
  const history = { state: null, pushState(...args) { calls.history.push(args); }, replaceState(...args) { calls.history.push(args); } };
  const { default: View } = compile('catalog-collection-view.element', modules, {
    HTMLElement: Element, customElements: { define() {} }, window, history, URL, URLSearchParams,
  });
  const view = new View();
  view.render = () => {};
  view.revealCollectionHost = () => {};
  view.load = async () => { calls.loads.push(view.nodeId); };
  const selection = {
    setSelectable(value) { calls.selection.push(['selectable', value]); },
    setMultiple(value) { calls.selection.push(['multiple', value]); },
    clearSelection() { calls.selection.push(['clear']); },
  };
  view.connectedCallback();
  assert.deepEqual(calls.loads, [], 'Do not load the underlying document before the collection context is known');
  view.consumers.get(collectionToken)({ getConfig: () => config, selection });
  return { view, calls, nativeTable };
}

test('picker opt-in mounts the native table under the existing collection context', () => {
  const { view, calls, nativeTable } = createView({ unique: 'picker-category', selectionConfiguration: { selectOnly: true, multiple: false } });
  assert.equal(calls.nativeViews.length, 1);
  assert.equal(calls.nativeViews[0].alias, 'Umb.CollectionView.Document.Table');
  assert.equal(calls.nativeViews[0].host, view);
  calls.nativeViews[0].callback(true, calls.nativeViews[0]);
  assert.equal(view.children[0], nativeTable);
  assert.deepEqual(calls.loads, []);
  assert.deepEqual(calls.listeners, []);
  assert.deepEqual(calls.history, []);
  assert.deepEqual(calls.selection, [], 'The native table owns selection, including single selection and filters');
});

test('picker initialization is idempotent and its native controller is disposed', () => {
  const { view, calls } = createView({ selectionConfiguration: { selectOnly: true } });
  view.initializePickerView();
  assert.equal(calls.nativeViews.length, 1);
  view.disconnectedCallback();
  assert.equal(calls.nativeViews[0].destroyed, true);
  assert.equal(view.pickerView, undefined);
});

test('unavailable native table reports an error without starting custom catalog navigation', () => {
  const { view, calls } = createView({ selectionConfiguration: { selectOnly: true } });
  calls.nativeViews[0].callback(false, calls.nativeViews[0]);
  assert.match(view.textContent, /unavailable/);
  assert.deepEqual(calls.loads, []);
  assert.deepEqual(calls.listeners, []);
});

test('workspace catalog retains loading, multi-selection, history, drilling and pagination', () => {
  const { view, calls } = createView({ unique: 'workspace-category' });
  assert.deepEqual(calls.loads, ['workspace-category']);
  assert.deepEqual(calls.listeners, ['popstate', 'resize']);
  assert.deepEqual(calls.selection, [['selectable', true], ['multiple', true]]);
  assert.deepEqual(calls.nativeViews, []);
  view.drillTo({ key: 'child-category' });
  assert.equal(view.nodeId, 'child-category');
  assert.match(calls.history[0][2], /edit\/child-category/);
  assert.equal(calls.loads.at(-1), 'child-category');
  assert.deepEqual(calls.selection.at(-1), ['clear']);
  view.setPage(2);
  assert.equal(view.page, 2);
  assert.equal(calls.history.at(-1)[0].ekomCatalog.page, 2);
});
