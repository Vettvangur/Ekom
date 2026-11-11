import { test } from 'node:test';
import assert from 'node:assert/strict';
import { readFileSync, existsSync } from 'node:fs';
import { selectReleaseConfig } from './select-release-config.mjs';

test('main transitions once to 1.0.0, then resumes normal versioning', () => {
    assert.equal(selectReleaseConfig('Ekom', '0.2.378'), 'release-please-config-1.0.json');
    for (const version of ['1.0.0', '1.0.1', '1.2.0']) {
        assert.equal(selectReleaseConfig('Ekom', version), 'release-please-config.json');
    }
});
test('maintenance always uses its patch-only configuration', () => {
    for (const version of ['0.2.378', '0.2.379', '0.2.999']) {
        assert.equal(selectReleaseConfig('release/0.2', version), 'release-please-config.json');
    }
});
test('rejects other branches and incompatible versions', () => {
    for (const [branch, version] of [
        ['feature/example', '0.2.378'], ['release/0.2', '1.0.0'],
        ['Ekom', '2.0.0'], ['Ekom', '1.0.0-rc.1'], ['Ekom', undefined],
    ]) assert.throws(() => selectReleaseConfig(branch, version));
});
test('bootstrap changes only Ekom and maintenance cannot release plugins', () => {
    const normal = JSON.parse(readFileSync('release-please-config.json', 'utf8'));
    if (existsSync('release-please-config-1.0.json')) {
        const bootstrap = JSON.parse(readFileSync('release-please-config-1.0.json', 'utf8'));
        assert.equal(bootstrap.packages.Ekom['release-as'], '1.0.0');
        delete bootstrap.packages.Ekom['release-as'];
        assert.deepEqual(bootstrap, normal);
        assert.equal(normal.packages.Ekom.versioning, 'default');
    } else {
        assert.deepEqual(Object.keys(normal.packages), ['Ekom']);
        assert.equal(normal.packages.Ekom.versioning, 'always-bump-patch');
        assert.equal(normal.packages.Ekom['release-as'], undefined);
    }
});
