// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast} from '../src/index.js';

for (const tag of ['list', 'olist']) {
  test(`native ${tag} hierarchy retains a nested child and following sibling`, () => {
    const source = `[${tag}]\n[*]Parent\n[${tag}][*]Child[/${tag}]\n[*]Sibling[/${tag}]`;
    const result = steamCommunityBbcodeToMdast(source);
    assert.deepEqual(result.diagnostics, []);
    const list = result.value.children[0];
    assert.ok(list?.type === 'list');
    assert.equal(list.ordered, tag === 'olist');
    assert.equal(list.children.length, 2);
    const parent = list.children[0];
    assert.equal(parent?.children[0]?.type, 'paragraph');
    const nested = parent?.children[1];
    assert.ok(nested?.type === 'list');
    assert.equal(nested.ordered, list.ordered);
    assert.equal(nested.children.length, 1);
    const sibling = list.children[1]?.children[0];
    assert.ok(sibling?.type === 'paragraph');
    assert.deepEqual(sibling.children.map(child => child.type === 'text' ? child.value : ''), ['Sibling']);
    assert.equal(result.coverage.constructs.filter(outcome => outcome.constructId === 'steam.bbcode.list-item').length, 3);
  });
}

test('code remains an exact opaque value beside explicit blocks and strikethrough', () => {
  const result = steamCommunityBbcodeToMdast('[p][strike]Old[/strike][/p][hr][/hr][quote]Outer[quote]Inner[/quote][/quote][code]\n```\ntext\n[/code]');
  assert.deepEqual(result.diagnostics, []);
  const [paragraph, rule, quote, code] = result.value.children;
  assert.ok(paragraph?.type === 'paragraph');
  assert.equal(paragraph.children[0]?.type, 'delete');
  assert.equal(rule?.type, 'thematicBreak');
  assert.ok(quote?.type === 'blockquote');
  assert.equal(quote.children[1]?.type, 'blockquote');
  assert.ok(code?.type === 'code');
  assert.equal(code.value, '\n```\ntext\n');
});

test('orphan list markers and non-item list prose are retained with a diagnostic', () => {
  const source = '[list]before[*]item[/list]';
  const result = steamCommunityBbcodeToMdast(source);
  assert.ok(result.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_LIST_CONTENT_PRESERVED'));
  const paragraph = result.value.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  assert.equal(paragraph.children[0]?.type === 'text' ? paragraph.children[0].value : '', source);
  assert.ok(steamCommunityBbcodeToMdast('[*]orphan').diagnostics.length > 0);
});
