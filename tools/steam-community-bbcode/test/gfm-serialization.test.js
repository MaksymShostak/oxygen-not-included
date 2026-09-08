// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {steamCommunityBbcodeToGfm} from '../src/index.js';

/** @param {string} markdown */
function targetTree(markdown) {
  return fromMarkdown(markdown, {extensions: [gfm()], mdastExtensions: [gfmFromMarkdown()]});
}

test('the GFM parser observes literal Markdown and HTML-shaped source without formatting', () => {
  const source = '*literal* <script>alert(1)</script> [sd]';
  const result = steamCommunityBbcodeToGfm(source);
  const paragraph = targetTree(result.value).children[0];
  assert.ok(paragraph?.type === 'paragraph');
  assert.deepEqual(paragraph.children.map(child => child.type), ['text']);
  assert.equal(paragraph.children[0]?.type === 'text' ? paragraph.children[0].value : '', source);
});

test('maintained GFM serialization preserves nested lists, deletion and table cells', () => {
  const result = steamCommunityBbcodeToGfm('[olist][*]Parent[olist][*]Child[/olist][*]Sibling[/olist][table][tr][th]H[/th][/tr][tr][td][strike]Old[/strike][/td][/tr][/table]');
  const [list, table] = targetTree(result.value).children;
  assert.ok(list?.type === 'list');
  assert.equal(list.ordered, true);
  assert.equal(list.children.length, 2);
  assert.equal(list.children[0]?.children[1]?.type, 'list');
  assert.ok(table?.type === 'table');
  assert.equal(table.children[1]?.children[0]?.children[0]?.type, 'delete');
});

for (const literal of ['a`b', '\n```\ntext\n', '~~~\n[code]raw\n```']) {
  test(`code fences chosen by the serializer preserve content: ${JSON.stringify(literal)}`, () => {
    const result = steamCommunityBbcodeToGfm(`[code]${literal}[/code]`);
    const code = targetTree(result.value).children[0];
    assert.ok(code?.type === 'code');
    assert.equal(code.value, literal);
  });
}

test('noparse is lowered to literal text only at the GFM boundary', () => {
  const result = steamCommunityBbcodeToGfm('[noparse][b]*literal*[/b][/noparse]');
  const paragraph = targetTree(result.value).children[0];
  assert.ok(paragraph?.type === 'paragraph');
  assert.equal(paragraph.children.length, 1);
  const text = paragraph.children[0];
  assert.ok(text?.type === 'text');
  assert.equal(text.value, '[b]*literal*[/b]');
});
