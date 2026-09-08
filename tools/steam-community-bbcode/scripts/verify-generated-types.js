// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {readFile, readdir} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';

const root = fileURLToPath(new URL('../', import.meta.url));
const npmCli = process.env['npm_execpath'];
assert.ok(npmCli, 'Run through npm so its selected CLI owns build commands.');

/** Build using the package entry point, with install hooks still absent. */
function build() {
  const result = spawnSync(process.execPath, [/** @type {string} */ (npmCli), 'run', 'build'],
    {cwd: root, encoding: 'utf8', windowsHide: true});
  assert.equal(result.status, 0, result.error?.message ?? result.stdout + result.stderr);
}

/** @returns {Promise<Array<[string, string]>>} Exact emitted file identities. */
async function declarations() {
  const base = new URL('../types/', import.meta.url);
  const names = (await readdir(base, {recursive: true})).sort();
  /** @type {Array<[string, string]>} */
  const output = [];
  for (const name of names) {
    if (name.endsWith('.d.ts') || name.endsWith('.d.ts.map')) {
      output.push([name, await readFile(new URL(name.replaceAll('\\', '/'), base), 'utf8')]);
    }
  }
  assert.ok(output.length > 0, 'Declaration generation must produce actual artifacts.');
  return output;
}

build();
const first = await declarations();
build();
assert.deepEqual(await declarations(), first, 'Two clean builds must emit identical declarations and maps.');
console.log(`Verified ${first.length} declaration artifacts across two clean builds.`);
