import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import test from 'node:test';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

for (const filename of ['orders.csv', 'orders-with-orderlines.csv']) {
  test(`${filename} downloads without reaching the backoffice router`, () => {
    const source = readFileSync(new URL('../src/manager/manager-shared.ts', import.meta.url), 'utf8');
    const compiled = ts.transpileModule(source, {
      compilerOptions: { target: ts.ScriptTarget.ES2022, module: ts.ModuleKind.CommonJS },
    }).outputText;
    const exports = {};
    const blob = new Blob(['orderId\n123']);
    const calls = [];
    const handlers = {};
    const anchor = {
      addEventListener: (type, handler) => { handlers[type] = handler; },
      click() {
        let stopped = false;
        const event = {
          stopPropagation() { stopped = true; },
          preventDefault() { assert.fail('Native download must remain enabled'); },
        };
        handlers.click?.(event);
        // A bubbling click would be handled as navigation by the backoffice router.
        assert.ok(stopped, 'Download click must not reach the router');
        assert.equal(this.href, 'blob:https://example.test/export');
        assert.equal(this.download, filename);
        calls.push('download');
      },
      remove() { calls.push('remove'); },
    };
    runInNewContext(compiled, {
      exports,
      document: {
        createElement: tag => { assert.equal(tag, 'a'); return anchor; },
        body: { append: element => { assert.equal(element, anchor); calls.push('append'); } },
      },
      URL: {
        createObjectURL: value => { assert.equal(value, blob); return 'blob:https://example.test/export'; },
        revokeObjectURL: url => { assert.equal(url, anchor.href); calls.push('revoke'); },
      },
    });

    exports.downloadBlob(blob, filename);

    assert.deepEqual(calls, ['append', 'download', 'remove', 'revoke']);
  });
}
