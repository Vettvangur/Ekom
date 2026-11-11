import { readFileSync, appendFileSync } from 'node:fs';
import { pathToFileURL } from 'node:url';

export function selectReleaseConfig(branch, version) {
    if (branch === 'release/0.2' && /^0\.2\.\d+$/.test(version)) {
        return 'release-please-config.json';
    }
    if (branch === 'Ekom') {
        if (/^0\.2\.\d+$/.test(version)) return 'release-please-config-1.0.json';
        if (/^1\.\d+\.\d+$/.test(version)) return 'release-please-config.json';
    }
    throw new Error(`Unsupported Ekom release branch/version: ${branch} / ${version}`);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
    const manifest = JSON.parse(readFileSync('.release-please-manifest.json', 'utf8'));
    const file = selectReleaseConfig(process.env.RELEASE_BRANCH, manifest.Ekom);
    // Fail before calling the release action if the selected configuration is missing.
    JSON.parse(readFileSync(file, 'utf8'));
    appendFileSync(process.env.GITHUB_OUTPUT, `file=${file}\n`);
}
