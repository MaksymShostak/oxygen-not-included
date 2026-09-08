// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast, steamCommunityBbcodeToGfm} from '../src/index.js';
import {fromMarkdown} from 'mdast-util-from-markdown';

test('attribution and historical color survive in dedicated MDAST extensions', () => {
  const result = steamCommunityBbcodeToMdast('[quote="Ada;42"][color=#123456]Text[/color][/quote][pullquote]Aside[/pullquote]');
  const quote = result.value.children[0];
  assert.ok(quote?.type === 'steamAttributedBlockquote');
  assert.equal(quote.author, 'Ada');
  assert.equal(quote.steamCommentId, '42');
  const paragraph = quote.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  const color = paragraph.children[0];
  assert.ok(color?.type === 'steamColor');
  assert.equal(color.color, '#123456');
  assert.equal(result.value.children[1]?.type, 'steamPullQuote');
});

test('block spoilers retain nested lists and quotes before target lowering', () => {
  const source = '[spoiler][list][*]Hidden[/list][quote]Quoted[/quote][/spoiler]';
  const result = steamCommunityBbcodeToMdast(source);
  const spoiler = result.value.children[0];
  assert.ok(spoiler?.type === 'steamBlockSpoiler');
  assert.equal(spoiler.children[0]?.type, 'list');
  assert.equal(spoiler.children[1]?.type, 'blockquote');
  const gfm = steamCommunityBbcodeToGfm(source);
  const tree = fromMarkdown(gfm.value);
  assert.equal(tree.children[0]?.type === 'html' ? tree.children[0].value : '', '<details>\n<summary>Spoiler</summary>');
  assert.equal(tree.children[1]?.type, 'list');
  assert.equal(tree.children[2]?.type, 'blockquote');
  assert.ok(gfm.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_SPOILER_LOWERED_TO_DETAILS'));
});

test('target lowering retains quoted text and attribution while reporting presentation loss', () => {
  const result = steamCommunityBbcodeToGfm('[quote=Ada;42][color=red]Text[/color][/quote][pullquote]Aside[/pullquote]');
  assert.ok(result.value.includes('Ada'));
  assert.ok(result.value.includes('Text'));
  assert.ok(result.value.includes('Aside'));
  assert.ok(result.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_QUOTE_METADATA_LOWERED'));
  assert.ok(result.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_COLOR_LOWERED_TO_TEXT' && diagnostic.fidelity === 'lossy'));
  assert.ok(result.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_PULLQUOTE_LOWERED_TO_BLOCKQUOTE'));
});
