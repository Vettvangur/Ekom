import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { setImmediate } from 'node:timers/promises';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

const propertyContext = Symbol('property');
const datasetContext = Symbol('dataset');

class Observable {
  constructor(value) {
    this.value = value;
    this.listeners = new Set();
  }

  subscribe(callback) {
    this.listeners.add(callback);
    callback(this.value);
    return () => this.listeners.delete(callback);
  }

  next(value) {
    this.value = value;
    for (const listener of this.listeners) listener(value);
  }
}

class Element {
  isConnected = true;
  dataset = {};
  children = [];
  attributes = new Set();

  append(...children) { this.children.push(...children); }
  addEventListener() {}
  hasAttribute(name) { return this.attributes.has(name); }
  toggleAttribute(name, enabled) {
    if (enabled) this.attributes.add(name);
    else this.attributes.delete(name);
  }
}

const modules = {
  '@umbraco-cms/backoffice/event': { UmbChangeEvent: class {} },
  '@umbraco-cms/backoffice/property': {
    UMB_PROPERTY_CONTEXT: propertyContext,
    UMB_PROPERTY_DATASET_CONTEXT: datasetContext,
  },
  '@umbraco-cms/backoffice/element-api': {
    UmbElementMixin: Base => class extends Base {
      consumers = new Map();
      consumerRegistrations = [];
      observers = new Map();
      connectedCallback() {}
      consumeContext(token, callback) {
        this.consumerRegistrations.push(token);
        this.consumers.set(token, callback);
      }
      observe(source, callback, alias) {
        this.removeUmbControllerByAlias(alias);
        if (source) this.observers.set(alias, source.subscribe(callback));
      }
      removeUmbControllerByAlias(alias) {
        this.observers.get(alias)?.();
        this.observers.delete(alias);
      }
    },
  },
};

const source = readFileSync(new URL('../src/property-editors/range-editor.element.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
}).outputText;
const exports = {};
runInNewContext(compiled, {
  exports,
  require: name => {
    assert.ok(modules[name], `Unexpected module: ${name}`);
    return modules[name];
  },
  HTMLElement: Element,
  customElements: { define() {} },
  document: { createElement: () => new Element() },
});

async function createEditor(alias, type = 'Percentage') {
  const element = new exports.EkomRangeEditorElement();
  const labels = ['ISK', 'EUR'].map(currency => ({ dataset: { currencyLabel: currency }, textContent: currency }));
  const suffixes = labels.map(() => ({ textContent: '%', hidden: true }));
  element.editor = {
    querySelectorAll: selector => selector === '[data-percentage-suffix]' ? suffixes : [],
  };
  element.renderShell = () => {};
  element.loadStores = async () => {};
  element.dispatchEvent = () => assert.fail('Label updates must not emit a value change');
  element.connectedCallback();
  const aliasSource = new Observable(alias);
  const typeSource = new Observable(type);
  let typeLookups = 0;
  element.consumers.get(propertyContext)({ alias: aliasSource });
  element.consumers.get(datasetContext)({
    propertyValueByAlias: async requestedAlias => {
      assert.equal(requestedAlias, 'type');
      typeLookups++;
      return typeSource;
    },
  });
  await setImmediate();
  return { element, labels, suffixes, aliasSource, typeSource, typeLookups };
}

function assertUnits(fixture, percentage) {
  assert.deepEqual(fixture.labels.map(label => label.textContent), ['ISK', 'EUR']);
  assert.deepEqual(fixture.suffixes.map(suffix => suffix.hidden), [!percentage, !percentage]);
}

test('only the discount property observes Type and shows percentage suffixes', async () => {
  for (const alias of ['discount', 'startOfRange', 'endOfRange', 'otherRange']) {
    const fixture = await createEditor(alias);
    assertUnits(fixture, alias === 'discount');
    assert.equal(fixture.typeLookups, alias === 'discount' ? 1 : 0);
  }
});

test('live Type changes preserve values, inputs, and other editor labels', async () => {
  const discount = await createEditor('discount', 'Fixed');
  const range = await createEditor('startOfRange');
  discount.element.value = { main: [{ currency: 'is-IS', value: 20 }, { currency: 'en-IE', value: 15 }] };
  const before = JSON.stringify(discount.element.value);
  const originalLabels = [...discount.labels];

  discount.typeSource.next('Percentage');
  assertUnits(discount, true);
  discount.typeSource.next('Fixed');
  assertUnits(discount, false);
  assertUnits(range, false);
  assert.equal(JSON.stringify(discount.element.value), before);
  assert.deepEqual(discount.labels, originalLabels);
});

test('missing or unrecognized Type keeps currency labels', async () => {
  for (const type of [null, undefined, '', 'Unknown', 1, ['Percentage']]) {
    const fixture = await createEditor('discount', type ?? null);
    assertUnits(fixture, false);
  }
});

test('changing the property alias removes the Type observer', async () => {
  const fixture = await createEditor('discount');
  fixture.aliasSource.next('endOfRange');
  fixture.typeSource.next('Percentage');
  assertUnits(fixture, false);
  assert.equal(fixture.typeSource.listeners.size, 0);
});

test('readonly fields still update unit labels without changing their state', async () => {
  const fixture = await createEditor('discount', 'Fixed');
  fixture.element.querySelectorAll = () => [];
  fixture.element.readonly = true;
  fixture.typeSource.next('Percentage');
  assert.equal(fixture.element.readonly, true);
  assertUnits(fixture, true);
});

test('currency context is attached to new input labels without changing storage keys', async () => {
  const { element } = await createEditor('discount');
  const row = element.createRangeInput('main', { currencyValue: 'is-IS', isoCurrencySymbol: 'ISK' });
  const [label, input, suffix] = row.children;
  assert.equal(label.dataset.currencyLabel, 'ISK');
  assert.equal(label.htmlFor, input.id);
  assert.equal(input.dataset.currency, 'is-IS');
  assert.equal(input.dataset.store, 'main');
  assert.equal(label.textContent, 'ISK');
  assert.equal(suffix.textContent, '%');
  assert.equal(suffix.hidden, false);
});

test('fixed discount and ordinary range inputs keep the suffix hidden', async () => {
  for (const alias of ['discount', 'startOfRange', 'endOfRange']) {
    const { element } = await createEditor(alias, 'Fixed');
    const row = element.createRangeInput('main', { currencyValue: 'en-US', isoCurrencySymbol: 'USD' });
    const [label, input, suffix] = row.children;
    assert.equal(label.textContent, 'USD');
    assert.equal(input.dataset.currency, 'en-US');
    assert.equal(suffix.hidden, true);
  }
});

test('a late Type lookup cannot replace the current dataset observer', async () => {
  const fixture = await createEditor('discount');
  const obsoleteType = new Observable('Percentage');
  let resolveObsolete;
  fixture.element.consumers.get(datasetContext)({
    propertyValueByAlias: () => new Promise(resolve => { resolveObsolete = resolve; }),
  });
  const currentType = new Observable('Fixed');
  fixture.element.consumers.get(datasetContext)({ propertyValueByAlias: async () => currentType });
  await setImmediate();
  resolveObsolete(obsoleteType);
  await setImmediate();

  assertUnits(fixture, false);
  assert.equal(obsoleteType.listeners.size, 0);
  currentType.next('Percentage');
  assertUnits(fixture, true);
});

test('missing dataset context removes percentage labels and subscriptions', async () => {
  const fixture = await createEditor('discount');
  fixture.element.consumers.get(datasetContext)(undefined);
  assertUnits(fixture, false);
  assert.equal(fixture.typeSource.listeners.size, 0);
});

test('reconnecting does not register duplicate context consumers', async () => {
  const fixture = await createEditor('discount');
  fixture.element.connectedCallback();
  fixture.element.connectedCallback();
  assert.equal(fixture.element.consumerRegistrations.length, 2);
});
