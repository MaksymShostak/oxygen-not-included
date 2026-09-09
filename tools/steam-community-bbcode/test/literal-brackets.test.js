// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {parseSteamCommunityBbcode, steamCommunityBbcodeToGfm} from '../src/index.js';

/** @param {import('mdast').Nodes} node @returns {string} */
function visibleText(node) {
  return 'children' in node ? node.children.map(visibleText).join('') : 'value' in node ? node.value : '';
}

/** @type {readonly [string, string][]} */
const literalLabels = [
  ['[sd] QooLiO', '[sd] QooLiO'],
  ['[i]Delivery Temperature Limit [Fixed][/i]', 'Delivery Temperature Limit [Fixed]'],
  ['[url=https://example.org][i]Delivery Temperature Limit [Fixed][/i][/url]', 'Delivery Temperature Limit [Fixed]'],
  ['[FutureLabel] Next [b]bold[/b]', '[FutureLabel] Next bold'],
];
for (const [source, expectedText] of literalLabels) {
  test(`literal-bracket labels have one accurate preservation diagnostic: ${source}`, () => {
    const parsed = parseSteamCommunityBbcode(source);
    assert.deepEqual(parsed.diagnostics, []);
    assert.equal(parsed.children.map(child => child.rawSource).join(''), source);
    const result = steamCommunityBbcodeToGfm(source);
    assert.deepEqual(result.diagnostics.map(diagnostic => diagnostic.code), ['STEAM_UNKNOWN_CONSTRUCT']);
    const diagnostic = result.diagnostics[0];
    assert.ok(diagnostic?.sourceSpan);
    assert.match(source.slice(diagnostic.sourceSpan.start.offset, diagnostic.sourceSpan.end.offset), /^\[(?:sd|Fixed|FutureLabel)\]$/);
    const paragraph = fromMarkdown(result.value).children[0];
    assert.ok(paragraph?.type === 'paragraph');
    assert.equal(visibleText(paragraph), expectedText);
    if (source.startsWith('[url=')) {
      const link = paragraph.children[0];
      assert.ok(link?.type === 'link');
      assert.equal(link.url, 'https://example.org');
      assert.equal(link.children[0]?.type, 'emphasis');
    }
    if (source.startsWith('[i]')) assert.equal(paragraph.children[0]?.type, 'emphasis');
    if (source.startsWith('[FutureLabel]')) assert.equal(paragraph.children[1]?.type, 'strong');
  });
}

test('literal-bracket controls retain structural diagnostics for known and malformed source', () => {
  for (const [source, code] of [
    ['[b]unfinished', 'STEAM_UNCLOSED_TAG'],
    ['[code]unfinished', 'STEAM_UNCLOSED_TAG'],
    ['[noparse]unfinished', 'STEAM_UNCLOSED_TAG'],
    ['[b]text[/i]', 'STEAM_MISMATCHED_CLOSING_TAG'],
    ['[future unfinished', 'STEAM_UNCLOSED_TAG_HEADER'],
    ['[/future]tail', 'STEAM_UNMATCHED_CLOSING_TAG'],
  ]) {
    assert.ok(parseSteamCommunityBbcode(source ?? '').diagnostics.some(diagnostic => diagnostic.code === code), source);
  }
});

test('literal-bracket paired unknown source retains its whole body without activating nested tags', () => {
  const source = '[future][b]literal[/b][/future]';
  const result = steamCommunityBbcodeToGfm(source);
  assert.deepEqual(result.diagnostics.map(diagnostic => diagnostic.code), ['STEAM_UNKNOWN_CONSTRUCT']);
  const paragraph = fromMarkdown(result.value).children[0];
  assert.ok(paragraph?.type === 'paragraph');
  assert.equal(paragraph.children.length, 1);
  const text = paragraph.children[0];
  assert.ok(text?.type === 'text');
  assert.equal(text.value, source);
});

test('literal-bracket labels inside noparse remain opaque without warnings', () => {
  const result = steamCommunityBbcodeToGfm('[noparse][sd] [Fixed] [b]literal[/b][/noparse]');
  assert.deepEqual(result.diagnostics, []);
  const paragraph = fromMarkdown(result.value).children[0];
  assert.ok(paragraph?.type === 'paragraph');
  const text = paragraph.children[0];
  assert.ok(text?.type === 'text');
  assert.equal(text.value, '[sd] [Fixed] [b]literal[/b]');
});
