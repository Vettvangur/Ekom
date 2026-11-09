import { readdir, readFile, mkdir, rm, writeFile, copyFile } from 'node:fs/promises';
import { resolve, join, dirname, extname, sep } from 'node:path';
import { pathToFileURL } from 'node:url';

// Keep the discovery manifest at its well-known location. Everything it loads
// lives below the package version, including relative imports and shared chunks.
export async function stageVersionedAssets(source, destination, version) {
  if (!/^\d+\.\d+\.\d+(?:-[0-9A-Za-z.-]+)?(?:\+[0-9A-Za-z.-]+)?$/.test(version)) {
    throw new Error(`Invalid Ekom asset package version: ${version}`);
  }
  source = resolve(source);
  destination = resolve(destination);
  if (destination === source || source.startsWith(destination + sep) || destination.startsWith(source + sep)) {
    throw new Error('Asset staging must use a separate directory.');
  }
  const prefix = `/App_Plugins/Ekom/releases/${version}/`;
  const rewrite = text => text.replace(/\/App_Plugins\/Ekom\//gi, prefix);
  const manifest = JSON.parse(await readFile(join(source, 'umbraco-package.json'), 'utf8'));
  await rm(destination, { recursive: true, force: true });
  await mkdir(destination, { recursive: true });
  await writeFile(join(destination, 'umbraco-package.json'), rewrite(JSON.stringify(manifest, null, 2)) + '\n');

  async function copyDirectory(relative = '') {
    for (const entry of await readdir(join(source, relative), { withFileTypes: true })) {
      // Never nest a second discovery manifest or an earlier staged release.
      if (entry.name === 'umbraco-package.json' || (!relative && entry.name === 'releases')) continue;
      const path = join(relative, entry.name);
      if (entry.isDirectory()) {
        await copyDirectory(path);
      } else if (entry.isFile()) {
        const target = join(destination, 'releases', version, path);
        await mkdir(dirname(target), { recursive: true });
        if (['.js', '.css', '.html', '.json', '.map'].includes(extname(path))) {
          await writeFile(target, rewrite(await readFile(join(source, path), 'utf8')));
        } else {
          await copyFile(join(source, path), target);
        }
      }
    }
  }
  await copyDirectory();
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  const [source, destination, version] = process.argv.slice(2);
  if (!source || !destination || !version) throw new Error('Expected source, staging directory and package version.');
  await stageVersionedAssets(source, destination, version);
}
