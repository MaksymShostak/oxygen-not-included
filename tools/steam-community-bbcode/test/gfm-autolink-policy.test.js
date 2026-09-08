// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {steamCommunityBbcodeToGfm} from '../src/index.js';

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
