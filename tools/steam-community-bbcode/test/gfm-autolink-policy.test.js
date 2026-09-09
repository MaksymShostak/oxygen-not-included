// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {steamCommunityBbcodeToGfm} from '../src/index.js';
import {rendererAutolinkDiagnostics} from '../src/gfm/renderer-autolink-diagnostics.js';

test('literal URL escaping survives syntax parsing and reports additional GitHub autolinking', () => {
  const source = '[noparse]https://example.org/path[/noparse]';
  const result = steamCommunityBbcodeToGfm(source);
  const syntax = gfmFromMarkdown().map(({transforms: _rendererTransforms, ...extension}) => extension);
  const parsed = fromMarkdown(result.value, {extensions: [gfm()], mdastExtensions: syntax});
  const paragraph = parsed.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  assert.deepEqual(paragraph.children.map(n => n.type), ['text']);
  const rendered = fromMarkdown(result.value, {extensions: [gfm()], mdastExtensions: [gfmFromMarkdown()]});
  const renderedParagraph = rendered.children[0];
  assert.ok(renderedParagraph?.type === 'paragraph');
  assert.equal(renderedParagraph.children[0]?.type, 'link');
  assert.ok(result.diagnostics.some(d => d.code === 'GFM_RENDERER_AUTOLINK_POSSIBLE' && d.scope === 'input'));
  const explicit = steamCommunityBbcodeToGfm('[url=https://example.org/path]Link[/url]');
  assert.ok(!explicit.diagnostics.some(d => d.code === 'GFM_RENDERER_AUTOLINK_POSSIBLE'));
});

/** @type {[string, boolean][]} */
const rendererCases = [
  ['[noparse]www[/noparse][noparse].example.org[/noparse]', true],
  ['[b][noparse]person@example.org[/noparse][/b]', true],
  ['[table][tr][th]H[/th][/tr][tr][td][noparse]www.example.org[/noparse][/td][/tr][/table]', true],
  ['[url=https://example.org][noparse]www.example.org[/noparse][/url]', false],
  ['[code]www.example.org person@example.org[/code]', false],
  ['[noparse]www.[/noparse][b]example[/b][noparse].org[/noparse]', true],
  ['[noparse]www[/noparse][b].example.org[/b]', false],
];
for (const [source, possible] of rendererCases) {
  test(`additional autolink diagnosis respects native text and resource boundaries: ${source}`, () => {
    const result = steamCommunityBbcodeToGfm(source);
    assert.equal(result.diagnostics.some(d => d.code === 'GFM_RENDERER_AUTOLINK_POSSIBLE'), possible);
  });
}

test('the native renderer transform diagnoses a copied tree without changing its literal content', () => {
  /** @type {import('mdast').Root} */
  const intended = {type: 'root', children: [{type: 'paragraph', children: [{type: 'text', value: 'www.example.org'}]}]};
  const original = structuredClone(intended);
  const result = rendererAutolinkDiagnostics(intended);
  assert.deepEqual(result.map(d => d.code), ['GFM_RENDERER_AUTOLINK_POSSIBLE']);
  assert.deepEqual(intended, original);
});
