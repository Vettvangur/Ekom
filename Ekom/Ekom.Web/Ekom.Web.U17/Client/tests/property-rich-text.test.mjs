import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

const source = readFileSync(new URL('../src/property-editors/property-editor.element.ts', import.meta.url), 'utf8');
const compiled = ts.transpileModule(source, {
  compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
}).outputText;
const exports = {};
const modules = { '@umbraco-cms/backoffice/lit-element': { UmbLitElement: class {} } };
runInNewContext(compiled, {
  exports,
  structuredClone,
  require: name => modules[name] ?? {},
  customElements: { define() {} },
});

function createEditor(value, alias = 'Umbraco.RichText') {
  const element = new exports.EkomPropertyEditorElement();
  element.wrappedDataType = { propertyEditorAlias: alias };
  element.currentTab = { value: 'is-IS', text: 'Icelandic' };
  element.editor = {};
  element.value = { values: { 'is-IS': value, 'en-US': '<p>English</p>' }, type: 'Language' };
  return element;
}

const richText = {
  markup: '<p>Endurn\u00e6r\u00f0u &nbsp;</p>',
  blocks: { layout: {}, contentData: [{ key: 'block-key' }], settingsData: [], expose: [] },
};

test('serialized rich text loads and preserves blocks and other languages on edit', () => {
  const serialized = JSON.stringify(richText);
  const element = createEditor(serialized);
  assert.equal(JSON.stringify(element.editor.value), serialized);
  assert.equal(element.value.values['is-IS'], serialized, 'loading does not rewrite stored values');
  element.emitChange = () => {};
  element.emitTitleChanged = () => {};
  element.editor.value.markup = '<p>Updated</p>';
  element.onWrappedEditorChange({ stopPropagation() {} });
  assert.equal(element.value.values['is-IS'].markup, '<p>Updated</p>');
  assert.equal(JSON.stringify(element.value.values['is-IS'].blocks), JSON.stringify(richText.blocks));
  assert.equal(element.value.values['en-US'], '<p>English</p>');
});

test('legacy HTML and empty strings load for modern and legacy aliases', () => {
  for (const alias of ['Umbraco.RichText', 'Umbraco.TinyMCE']) {
    for (const markup of ['<p>Legacy HTML</p>', '']) {
      assert.equal(createEditor(markup, alias).editor.value.markup, markup);
    }
  }
});

test('existing objects and unrelated editors retain their values', () => {
  assert.equal(createEditor(richText).editor.value, richText);
  const serialized = JSON.stringify(richText);
  assert.equal(createEditor(serialized, 'Umbraco.TextBox').editor.value, serialized);
  assert.equal(createEditor(undefined).editor.value, undefined);
});

test('tab switches receive the correct rich-text value', () => {
  const element = createEditor(JSON.stringify(richText));
  element.currentTab = { value: 'en-US', text: 'English' };
  element.syncCurrentEditorValue();
  assert.equal(element.editor.value.markup, '<p>English</p>');
});

test('unwrapped rich text defaults across stores and survives editing and reloading', () => {
  for (const raw of [richText, JSON.stringify(richText), '<p>Legacy</p>']) {
    const element = createEditor(undefined);
    element.internalValue.type = 'Store';
    element.tabs = [{ value: 'main' }, { value: 'outlet' }];
    element.currentTab = element.tabs[0];
    element.value = raw;
    const expectedMarkup = typeof raw === 'string' && !raw.startsWith('{') ? raw : richText.markup;
    assert.equal(element.editor.value.markup, expectedMarkup);
    assert.equal('values' in Object(element.value), false, 'loading does not create a wrapper');
    element.editor.value.markup = '<p>Edited main store</p>';
    element.emitChange = () => {};
    element.emitTitleChanged = () => {};
    element.onWrappedEditorChange({ stopPropagation() {} });
    const saved = JSON.parse(JSON.stringify(element.value));
    assert.equal(saved.type, 'Store');
    assert.equal(saved.values.main.markup, '<p>Edited main store</p>');
    assert.equal(saved.values.outlet.markup, expectedMarkup);
    if (typeof raw !== 'string' || raw.startsWith('{')) {
      assert.equal(JSON.stringify(saved.values.outlet.blocks), JSON.stringify(richText.blocks));
    }
    element.value = saved;
    element.currentTab = element.tabs[1];
    element.syncCurrentEditorValue();
    assert.equal(element.editor.value.markup, expectedMarkup);
  }
});

test('unwrapped defaults do not leak between stores or into replacement values', () => {
  const element = createEditor(undefined);
  element.internalValue.type = 'Store';
  element.value = richText;
  element.editor.value.markup = 'unsaved mutation';
  element.currentTab = { value: 'other' };
  element.syncCurrentEditorValue();
  assert.equal(element.editor.value.markup, richText.markup);
  element.value = { type: 'Store', values: { other: { markup: '' } } };
  assert.equal(element.editor.value.markup, '');
  element.value = { type: 'Store', values: {} };
  assert.equal(element.editor.value, undefined);
});

test('unwrapped rich text is not used as a default for unrelated editors or language properties', () => {
  const element = createEditor(undefined);
  element.value = richText;
  assert.equal(element.editor.value, undefined);
  element.internalValue.type = 'Store';
  element.wrappedDataType.propertyEditorAlias = 'Umbraco.TextBox';
  element.syncCurrentEditorValue();
  assert.equal(element.editor.value, undefined);
});

test('loading unwrapped rich text never marks the workspace changed', async () => {
  const element = new exports.EkomPropertyEditorElement();
  element.isConnected = true;
  element.rawConfig = { toObject: () => ({ dataType: 'wrapped-type', useLanguages: false }) };
  element.fetchJson = async () => ({ propertyEditorAlias: 'Umbraco.RichText' });
  element.loadStoreTabs = async () => [{ value: 'main' }, { value: 'outlet' }];
  element.getContentKey = () => undefined;
  element.getStoredTab = () => undefined;
  element.syncStatus = () => {};
  element.renderTabs = () => {};
  element.tryAutofillFromNodeName = () => {};
  element.renderCurrentEditor = async () => {};
  const savedChanges = [];
  element.emitChange = () => {
    savedChanges.push(JSON.parse(JSON.stringify(element.value)));
    // The workspace can echo the updated property back through the setter.
    element.value = element.value;
  };
  element.value = JSON.stringify(richText);
  assert.equal(savedChanges.length, 0, 'wait for store configuration before converting');
  await element.load();
  assert.equal(savedChanges.length, 0);
  assert.equal(JSON.stringify(element.getCurrentEditorValue()), JSON.stringify(richText));
  assert.equal(JSON.stringify(element.value), JSON.stringify(richText));
  await element.load();
  assert.equal(savedChanges.length, 0, 'reloading does not mark it changed');
});
