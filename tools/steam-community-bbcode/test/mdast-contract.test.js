// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast, parseSteamCommunityBbcode} from '../src/index.js';

test('source Markdown punctuation remains literal MDAST text', () => {
  const source = '*literal* <script> [sd]';
  const result = steamCommunityBbcodeToMdast(source);
  assert.equal(result.value.type, 'root');
  const paragraph = result.value.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  assert.ok(paragraph.children.length > 0);
  assert.ok(paragraph.children.every(child => child.type === 'text'));
  assert.equal(paragraph.children.map(child => child.type === 'text' ? child.value : '').join(''), source);
  assert.ok(result.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_UNKNOWN_CONSTRUCT'));
});

test('headings and nested emphasis use the native MDAST node model', () => {
  const source = '[h2]Title[/h2][b]Strong [i]inner[/i][/b]';
  const result = steamCommunityBbcodeToMdast(parseSteamCommunityBbcode(source));
  assert.deepEqual(result.diagnostics, []);
  const heading = result.value.children[0];
  assert.ok(heading?.type === 'heading');
  assert.equal(heading.depth, 2);
  assert.deepEqual(heading.children.map(child => child.type === 'text' ? child.value : ''), ['Title']);
  const paragraph = result.value.children[1];
  assert.ok(paragraph?.type === 'paragraph');
  const strong = paragraph.children[0];
  assert.ok(strong?.type === 'strong');
  assert.equal(strong.children[1]?.type, 'emphasis');
});

test('prepared source must be issued by the parser and retain its parsing contract', () => {
  const forged = {source: 'X', profile: 'workshop-item', children: [], diagnostics: []};
  assert.throws(() => Reflect.apply(steamCommunityBbcodeToMdast, undefined, [forged]), TypeError);
  const issued = parseSteamCommunityBbcode('X');
  assert.throws(() => steamCommunityBbcodeToMdast(issued, {profile: 'review'}), RangeError);
  assert.throws(() => steamCommunityBbcodeToMdast(issued, {resourceLimits: {maxInputBytes: 1}}), TypeError);
});
