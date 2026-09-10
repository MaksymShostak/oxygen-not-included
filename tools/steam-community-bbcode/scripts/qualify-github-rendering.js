// SPDX-License-Identifier: AGPL-3.0-only
import {parseArgs} from 'node:util';
import {mkdir, mkdtemp, writeFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {join} from 'node:path';
import {fileURLToPath, pathToFileURL} from 'node:url';
import assert from 'node:assert/strict';
import {githubRenderingScenarios, normalizeGithubRenderedHtml} from './github-rendering-scenarios.js';

const {values} = parseArgs({options: {live: {type: 'boolean', default: false}, scenario: {type: 'string', default: 'all'}}, allowPositionals: false});
if (!values.live) throw new Error('This opt-in qualification contacts GitHub; invoke with --live to execute it.');
const scenarios = githubRenderingScenarios.filter(scenario => values.scenario === 'all' || scenario.name === values.scenario);
if (!scenarios.length) throw new Error('Unknown rendering scenario.');

// Preserve earlier observations. These are synthetic public requests to a
// read-only renderer, with no credentials or repository context.
await mkdir(new URL('../artifacts/github-rendering/', import.meta.url), {recursive: true});
const runDirectory = await mkdtemp(fileURLToPath(new URL('../artifacts/github-rendering/run-', import.meta.url)));
console.log(`Retaining renderer evidence in ${runDirectory}`);
for (const scenario of scenarios) {
  const directory = pathToFileURL(join(runDirectory, scenario.name) + '/');
  await mkdir(directory, {recursive: true});
  if (scenario.source) await writeFile(new URL('source.bbcode', directory), scenario.source, 'utf8');
  await writeFile(new URL('expected.html', directory), scenario.expectedHtml + '\n', 'utf8');
  const html = await renderMarkdown(scenario.markdown, directory);
  assert.equal(normalizeGithubRenderedHtml(html), normalizeGithubRenderedHtml(scenario.expectedHtml), `${scenario.name}: rendered HTML must preserve the independently specified target.`);
  if (scenario.expectedMarkdown) {
    const expectedHtml = await renderMarkdown(scenario.expectedMarkdown, directory, 'reference-');
    assert.equal(normalizeGithubRenderedHtml(html), normalizeGithubRenderedHtml(expectedHtml), 'Converted flow must render like the independently authored target, including tight-list structure and protected spaces.');
  }
  console.log(`PASS: ${scenario.name}`);
}

/** @param {string} markdown @param {URL} directory @param {string} [prefix] @returns {Promise<string>} */
async function renderMarkdown(markdown, directory, prefix = '') {
  const checkedAt = new Date().toISOString();
  const response = await fetch('https://api.github.com/markdown', {
    method: 'POST',
    headers: {'Accept': 'text/html', 'Content-Type': 'application/json', 'X-GitHub-Api-Version': '2026-03-10', 'User-Agent': 'steam-community-bbcode-qualification'},
    body: JSON.stringify({text: markdown, mode: 'gfm'}),
    signal: AbortSignal.timeout(20_000),
  });
  const html = await response.text();
  await writeFile(new URL(`${prefix}request.md`, directory), markdown, 'utf8');
  await writeFile(new URL(`${prefix}response.html`, directory), html, 'utf8');
  await writeFile(new URL(`${prefix}provenance.json`, directory), JSON.stringify({
    checkedAt, endpoint: 'https://api.github.com/markdown', apiVersion: '2026-03-10', mode: 'gfm',
    status: response.status, requestId: response.headers.get('x-github-request-id'),
    requestSha256: createHash('sha256').update(markdown).digest('hex'),
    responseSha256: createHash('sha256').update(html).digest('hex'),
  }, null, 2) + '\n', 'utf8');
  if (!response.ok) throw new Error(`GitHub Markdown rendering returned HTTP ${response.status}: ${html.slice(0, 300)}`);
  return html;
}
