// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {spawnSync} from 'node:child_process';
import {cpSync, mkdirSync, mkdtempSync, readFileSync, rmSync, writeFileSync} from 'node:fs';
import {dirname, join, resolve} from 'node:path';
import {fileURLToPath, pathToFileURL} from 'node:url';

const packageDirectory = fileURLToPath(new URL('../', import.meta.url));
const presentation = readFileSync(new URL('fixtures/github-rendering/presentation.html', import.meta.url), 'utf8');
const literalAutolinks = readFileSync(new URL('fixtures/github-rendering/literal-autolinks.html', import.meta.url), 'utf8');
const flowWhitespace = readFileSync(new URL('fixtures/github-rendering/flow-whitespace.html', import.meta.url), 'utf8');

/** @param {string | undefined} scenario @param {string[]} responses */
function runProbe(scenario, responses) {
  const artifacts = join(packageDirectory, 'artifacts');
  mkdirSync(artifacts, {recursive: true});
  const sandbox = mkdtempSync(join(artifacts, 'renderer-contract-'));
  try {
    cpSync(join(packageDirectory, 'src'), join(sandbox, 'src'), {recursive: true});
    cpSync(join(packageDirectory, 'scripts'), join(sandbox, 'scripts'), {recursive: true});
    const script = join(sandbox, 'scripts/qualify-github-rendering.js');
    const program = `
      import assert from 'node:assert/strict';
      const responses = ${JSON.stringify(responses)};
      let requests = 0;
      globalThis.fetch = async (url, options) => {
        assert.equal(url, 'https://api.github.com/markdown');
        const body = JSON.parse(options.body);
        assert.equal(body.mode, 'gfm');
        assert.equal(body.context, undefined);
        assert.equal(options.headers.Authorization, undefined);
        assert.ok(requests < responses.length, 'unexpected render request');
        return new Response(responses[requests++], {status: 200});
      };
      process.on('exit', code => {
        if (code === 0) assert.equal(requests, responses.length, 'every selected case must render');
      });
    `;
    const transport = join(sandbox, 'mock-renderer-fetch.mjs');
    writeFileSync(transport, program, 'utf8');
    return spawnSync(process.execPath, ['--import', pathToFileURL(transport).href, script, '--live', ...(scenario ? ['--scenario', scenario] : [])], {
      cwd: sandbox, encoding: 'utf8', timeout: 10_000, maxBuffer: 1024 * 1024, windowsHide: true,
    });
  } finally {
    assert.equal(dirname(sandbox), resolve(artifacts), 'cleanup must stay within the task artifact directory');
    rmSync(sandbox, {recursive: true, force: true});
  }
}

test('default renderer qualification verifies all three independent scenarios', () => {
  const result = runProbe(undefined, [presentation, literalAutolinks, flowWhitespace, flowWhitespace]);
  assert.equal(result.error, undefined);
  assert.equal(result.status, 0, result.stderr);
});

test('presentation qualification permits transport line endings and trailing newlines', () => {
  const result = runProbe('presentation', [presentation.replaceAll('\n', '\r\n') + '\r\n']);
  assert.equal(result.error, undefined);
  assert.equal(result.status, 0, result.stderr);
});

/** @type {[string, string, string][]} */
const wrongResponses = [
  ['presentation', 'empty successful response', ''],
  ['presentation', 'lost insertion markup', presentation.replace('<ins>Inserted</ins>', 'Inserted')],
  ['presentation', 'lost spoiler structure', presentation.replace('<details>', '<div>').replace('</details>', '</div>')],
  ['presentation', 'lost table structure', presentation.replace('<table role="table">', '<div>').replace('</table>', '</div>')],
  ['presentation', 'active script markup', presentation.replace('&lt;script&gt;alert(1)&lt;/script&gt;', '<script>alert(1)</script>')],
  ['literal-autolinks', 'new active literal link', literalAutolinks.replace('https://example.org/path', '<a href="https://example.org/path">https://example.org/path</a>')],
  ['literal-autolinks', 'deleted literal source', '<p>Escaped:</p>'],
];
for (const [scenario, name, html] of wrongResponses) {
  test(`renderer qualification rejects HTTP 200 with ${name}`, () => {
    const result = runProbe(scenario, [html]);
    assert.equal(result.error, undefined);
    assert.notEqual(result.status, 0, 'incorrect successful HTML must fail qualification');
    assert.match(result.stderr, /rendered HTML|render like/i);
  });
}

test('flow qualification preserves literal padding despite transport normalization', () => {
  const result = runProbe('flow-whitespace', [flowWhitespace.replace('  [sd]  [Fixed]  ', '[sd]  [Fixed]'), flowWhitespace]);
  assert.equal(result.error, undefined);
  assert.notEqual(result.status, 0);
  assert.match(result.stderr, /rendered HTML|render like/i);
});
