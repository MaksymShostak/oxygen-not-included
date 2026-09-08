// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast} from '../src/index.js';

test('Steam inline semantics and opaque text survive inside native emphasis', () => {
  const source = '[b][u]A[spoiler]Secret[/spoiler][/u][/b][noparse][b]*literal*[/b][/noparse]';
  const result = steamCommunityBbcodeToMdast(source);
  assert.deepEqual(result.diagnostics, []);
  const paragraph = result.value.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  const strong = paragraph.children[0];
  assert.ok(strong?.type === 'strong');
  const underline = strong.children[0];
  assert.ok(underline?.type === 'steamUnderline');
  assert.equal(underline.children[1]?.type, 'steamSpoiler');
  const opaque = paragraph.children[1];
  assert.ok(opaque?.type === 'steamNoParse');
  assert.equal(opaque.value, '[b]*literal*[/b]');
  assert.equal(result.coverage.constructs.filter(outcome => outcome.constructId === 'steam.bbcode.b').length, 1);
});
