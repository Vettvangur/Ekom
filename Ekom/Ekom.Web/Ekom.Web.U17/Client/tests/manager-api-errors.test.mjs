import assert from 'node:assert/strict';
import test from 'node:test';
import { readFileSync } from 'node:fs';
import { runInNewContext } from 'node:vm';
import ts from 'typescript';

function api(response) {
  const exports = {};
  const source = readFileSync(new URL('../src/manager/manager-shared.ts', import.meta.url), 'utf8');
  runInNewContext(ts.transpileModule(source, { compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022 } }).outputText,
    { exports, URLSearchParams, fetch: async () => response });
  return new exports.EkomManagerApi();
}

test('expired login redirects show a useful message instead of the HTML error page', async () => {
  await assert.rejects(api({ ok: false, status: 404, redirected: true, url: 'https://localhost/Account/Login?ReturnUrl=x' }).stores(), /session has expired/);
});

test('HTML server failures do not display page markup in the manager', async () => {
  await assert.rejects(api({ ok: false, status: 500, headers: new Headers({ 'content-type': 'text/html' }) }).stores(), /Request failed. Status 500/);
});
