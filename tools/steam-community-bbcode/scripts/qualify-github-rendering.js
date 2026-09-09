// SPDX-License-Identifier: AGPL-3.0-only
import {parseArgs} from 'node:util';
import {mkdir, writeFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
import {steamCommunityBbcodeToGfm} from '../src/index.js';

const {values} = parseArgs({options: {live: {type: 'boolean', default: false}, scenario: {type: 'string', default: 'presentation'}}, allowPositionals: false});
if (!values.live) throw new Error('This opt-in qualification contacts GitHub; invoke with --live to execute it.');
if (values.scenario !== 'presentation' && values.scenario !== 'literal-autolinks' && values.scenario !== 'flow-whitespace') throw new Error('Unknown rendering scenario.');

// Only synthetic public probe text is sent to the read-only rendering endpoint.
// No repository context, credentials, user document or publication is involved.
const flowSource = '[h1]Title[/h1]\n\nBody\n\n[hr][/hr]\n[list]\n[*] One\n[*] Two\n[/list]\n[noparse]  [sd]  [Fixed]  [/noparse]';
// Independent authored target: one paragraph, a tight two-item list, then
// literal bracket labels whose explicit two-space padding remains content.
const expectedFlowMarkdown = '# Title\n\nBody\n\n***\n\n* One\n* Two\n\n&#32;&#32;[sd]  [Fixed]&#32;&#32;\n';
const markdown = values.scenario === 'flow-whitespace' ? steamCommunityBbcodeToGfm(flowSource).value : (values.scenario === 'literal-autolinks' ? [
  'Escaped: https\\://example.org/path',
  'Entity: https&#x3A;//example.org/path',
  steamCommunityBbcodeToGfm('[noparse]https://example.org/path[/noparse]').value,
  steamCommunityBbcodeToGfm('[img]https://example.org/image.png').value,
] : [
  'Candidate U: <u>Underlined</u>',
  'Candidate INS: <ins>Inserted</ins>',
  steamCommunityBbcodeToGfm('[spoiler][b]Hidden[/b] *literal* <script>alert(1)</script>[/spoiler]').value,
  steamCommunityBbcodeToGfm('[table][tr][th]H[/th][/tr][tr][td][strike]Old[/strike][/td][/tr][/table]').value,
]).join('\n\n');
const checkedAt = new Date().toISOString();
const response = await fetch('https://api.github.com/markdown', {
  method: 'POST',
  headers: {'Accept': 'text/html', 'Content-Type': 'application/json', 'X-GitHub-Api-Version': '2026-03-10', 'User-Agent': 'steam-community-bbcode-qualification'},
  body: JSON.stringify({text: markdown, mode: 'gfm'}),
  signal: AbortSignal.timeout(20_000),
});
const html = await response.text();
if (!response.ok) throw new Error(`GitHub Markdown rendering returned HTTP ${response.status}: ${html.slice(0, 300)}`);
const directory = new URL(values.scenario === 'presentation' ? '../artifacts/github-rendering/' : `../artifacts/github-rendering/${values.scenario}/`, import.meta.url);
await mkdir(directory, {recursive: true});
await writeFile(new URL('response.html', directory), html, 'utf8');
await writeFile(new URL('request.md', directory), markdown, 'utf8');
await writeFile(new URL('provenance.json', directory), JSON.stringify({
  checkedAt, endpoint: 'https://api.github.com/markdown', apiVersion: '2026-03-10', mode: 'gfm',
  status: response.status, requestId: response.headers.get('x-github-request-id'),
  requestSha256: createHash('sha256').update(markdown).digest('hex'),
  responseSha256: createHash('sha256').update(html).digest('hex'),
}, null, 2) + '\n', 'utf8');
if (values.scenario === 'flow-whitespace') {
  const expectedResponse = await fetch('https://api.github.com/markdown', {
    method: 'POST',
    headers: {'Accept': 'text/html', 'Content-Type': 'application/json', 'X-GitHub-Api-Version': '2026-03-10', 'User-Agent': 'steam-community-bbcode-qualification'},
    body: JSON.stringify({text: expectedFlowMarkdown, mode: 'gfm'}),
    signal: AbortSignal.timeout(20_000),
  });
  const expectedHtml = await expectedResponse.text();
  await writeFile(new URL('source.bbcode', directory), flowSource, 'utf8');
  await writeFile(new URL('expected.md', directory), expectedFlowMarkdown, 'utf8');
  await writeFile(new URL('expected.html', directory), expectedHtml, 'utf8');
  await writeFile(new URL('expected-provenance.json', directory), JSON.stringify({
    checkedAt: new Date().toISOString(), endpoint: 'https://api.github.com/markdown', apiVersion: '2026-03-10', mode: 'gfm',
    status: expectedResponse.status, requestId: expectedResponse.headers.get('x-github-request-id'),
    requestSha256: createHash('sha256').update(expectedFlowMarkdown).digest('hex'),
    responseSha256: createHash('sha256').update(expectedHtml).digest('hex'),
  }, null, 2) + '\n', 'utf8');
  if (!expectedResponse.ok) throw new Error(`GitHub expected Markdown rendering returned HTTP ${expectedResponse.status}.`);
  assert.equal(html, expectedHtml, 'Converted flow must render like the independently authored target, including tight-list structure and protected spaces.');
}
console.log(html);
console.log(`Retained renderer evidence in ${directory.pathname}`);
