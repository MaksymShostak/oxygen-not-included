// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {spawnSync} from 'node:child_process';
import {fileURLToPath} from 'node:url';

const childProgram = `
  import {readFileSync} from 'node:fs';
  import {steamCommunityBbcodeToGfm} from './src/index.js';
  const source = readFileSync(0, 'utf8');
  const start = performance.now();
  const result = steamCommunityBbcodeToGfm(source);
  process.stdout.write(JSON.stringify({
    elapsedMs: performance.now() - start,
    value: result.value,
    diagnostics: result.diagnostics,
  }));
`;

/** @type {[string, string, string][]} */
const workloads = [
  ['opaque 16000', '[noparse]' + '<[x]>'.repeat(16000) + '[/noparse]', '<\\[x]>'.repeat(16000) + '\n'],
  ['opaque 32000', '[noparse]' + '<[x]>'.repeat(32000) + '[/noparse]', '<\\[x]>'.repeat(32000) + '\n'],
  ['adjacent bold 40000', '[b]x[/b]'.repeat(40000), '**' + 'x'.repeat(40000) + '**\n'],
];
for (const [name, input, expected] of workloads) {
  test(`forward availability: ${name}`, context => {
    const child = spawnSync(process.execPath, ['--input-type=module', '--eval', childProgram], {
      cwd: fileURLToPath(new URL('../../', import.meta.url)), input, encoding: 'utf8',
      timeout: 5000, maxBuffer: 1024 * 1024,
    });
    assert.equal(child.error, undefined, 'conversion must finish within the fixed subprocess budget');
    assert.equal(child.status, 0, child.stderr);
    assert.equal(child.stderr, '');
    /** @type {{elapsedMs: number, value: string, diagnostics: unknown[]}} */
    const result = JSON.parse(child.stdout);
    assert.equal(result.value, expected);
    assert.deepEqual(result.diagnostics, []);
    context.diagnostic(`inputBytes=${Buffer.byteLength(input)} elapsedMs=${result.elapsedMs}`);
  });
}
