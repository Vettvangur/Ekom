import { test } from 'node:test';
import assert from 'node:assert/strict';
import { mkdtemp, mkdir, writeFile, readFile, readdir, rm } from 'node:fs/promises';
import { tmpdir } from 'node:os';
import { join } from 'node:path';
import { stageVersionedAssets } from '../../../AssetPackaging/stage-versioned-assets.mjs';

test('each release versions the manifest, views and relative chunk graph without changing source assets', async () => {
  const root = await mkdtemp(join(tmpdir(), 'ekom-assets-'));
  try {
    const source = join(root, 'source');
    const output = join(root, 'output');
    await mkdir(join(source, 'dist'), { recursive: true });
    const manifest = JSON.stringify({ extensions: [{ type: 'bundle', js: '/App_Plugins/Ekom/dist/manifests.js' }] });
    await writeFile(join(source, 'umbraco-package.json'), manifest);
    await writeFile(join(source, 'dist/manifests.js'), 'export const manifests = [{element:"/App_Plugins/Ekom/dist/customers.js"}];');
    await writeFile(join(source, 'dist/customers.js'), 'import "./shared.js"; export const lazy = () => import("./lazy.js");');
    await writeFile(join(source, 'dist/shared.js'), 'export const css = "/App_Plugins/Ekom/dist/style.css";');
    await writeFile(join(source, 'dist/lazy.js'), 'export default 1;');
    await writeFile(join(source, 'dist/style.css'), 'body {}');
    for (const version of ['1.2.3', '1.2.4-preview.1']) {
      await stageVersionedAssets(source, output, version);
      const prefix = `/App_Plugins/Ekom/releases/${version}/dist/`;
      const packagedManifest = JSON.parse(await readFile(join(output, 'umbraco-package.json'), 'utf8'));
      assert.equal(packagedManifest.extensions[0].js, prefix + 'manifests.js');
      const dist = join(output, 'releases', version, 'dist');
      assert.match(await readFile(join(dist, 'manifests.js'), 'utf8'), new RegExp(prefix + 'customers.js'));
      const customer = await readFile(join(dist, 'customers.js'), 'utf8');
      for (const [, dependency] of customer.matchAll(/"\.\/(.*?)"/g)) {
        assert.ok(await readFile(join(dist, dependency)));
      }
      assert.ok((await readFile(join(dist, 'shared.js'), 'utf8')).includes(prefix + 'style.css'));
      assert.deepEqual(await readdir(join(output, 'releases')), [version]);
      assert.deepEqual((await readdir(output)).sort(), ['releases', 'umbraco-package.json']);
    }
    assert.equal(await readFile(join(source, 'umbraco-package.json'), 'utf8'), manifest);
    await assert.rejects(stageVersionedAssets(source, output, '../invalid'), /Invalid/);
    await assert.rejects(stageVersionedAssets(source, source, '1.2.3'), /separate directory/);
  } finally {
    await rm(root, { recursive: true, force: true });
  }
});
