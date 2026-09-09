// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {steamCommunityBbcodeToGfm} from '../src/index.js';

/** @typedef {[character: string, marks: number]} StyledCharacter */
/**
 * Read each character's actual presentation from the native consumer. Bold=1,
 * italic=2 and deletion=4 commute; no expected source is derived from this tree.
 * @param {string} markdown
 * @returns {StyledCharacter[]}
 */
function characterStyles(markdown) {
  const root = fromMarkdown(markdown, {extensions: [gfm()], mdastExtensions: [gfmFromMarkdown()]});
  assert.equal(root.children.length, 1);
  const paragraph = root.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  /** @type {StyledCharacter[]} */
  const characters = [];
  /** @param {import('mdast').PhrasingContent[]} nodes @param {number} marks */
  function visit(nodes, marks) {
    for (const node of nodes) {
      if (node.type === 'text') {
        for (const character of node.value) characters.push([character, marks]);
      } else {
        assert.ok(node.type === 'strong' || node.type === 'emphasis' || node.type === 'delete');
        visit(node.children, marks | ({strong: 1, emphasis: 2, delete: 4}[node.type]));
      }
    }
  }
  visit(paragraph.children, 0);
  return characters;
}

test('GFM formatting preserves equivalent adjacent style order', () => {
  const result = steamCommunityBbcodeToGfm('[b][i]a[/i][/b][i][b]b[/b][/i]');
  assert.deepEqual(characterStyles(result.value), [['a', 3], ['b', 3]]);
  assert.deepEqual(result.diagnostics, []);
  assert.ok(result.coverage.constructs.every(outcome => outcome.fidelity === 'exact'));
});

test('GFM formatting preserves the independently reported mixed-mark boundary', () => {
  const result = steamCommunityBbcodeToGfm('a[i][b]b[/b][/i][i]c[/i]');
  assert.deepEqual(characterStyles(result.value), [['a', 0], ['b', 3], ['c', 2]]);
  assert.deepEqual(result.diagnostics, []);
  assert.ok(result.coverage.constructs.every(outcome => outcome.fidelity === 'exact'));
});

test('GFM formatting retains outer source deletion around bold within a word', () => {
  const result = steamCommunityBbcodeToGfm('a[strike][b]b[/b][/strike]c');
  assert.deepEqual(characterStyles(result.value), [['a', 0], ['b', 5], ['c', 0]]);
  assert.deepEqual(result.diagnostics, []);
  assert.ok(result.coverage.constructs.every(outcome => outcome.fidelity === 'exact'));
});

/** @type {[string, string, string, number][]} */
const styles = [
  ['plain', '', '', 0],
  ['bold', '[b]', '[/b]', 1],
  ['italic', '[i]', '[/i]', 2],
  ['bold italic', '[b][i]', '[/i][/b]', 3],
  ['italic bold', '[i][b]', '[/b][/i]', 3],
  ['deletion', '[strike]', '[/strike]', 4],
  ['bold deletion', '[b][strike]', '[/strike][/b]', 5],
  ['italic deletion', '[i][strike]', '[/strike][/i]', 6],
  ['all marks', '[b][i][strike]', '[/strike][/i][/b]', 7],
];
for (const [left, openLeft, closeLeft, leftMarks] of styles) {
  for (const [right, openRight, closeRight, rightMarks] of styles) {
    test(`GFM adjacent presentation: ${left} then ${right}`, () => {
      const source = `a${openLeft}b${closeLeft}${openRight}c${closeRight}d`;
      const result = steamCommunityBbcodeToGfm(source);
      assert.deepEqual(characterStyles(result.value), [['a', 0], ['b', leftMarks], ['c', rightMarks], ['d', 0]], source);
      assert.deepEqual(result.diagnostics, []);
      assert.ok(result.coverage.constructs.every(outcome => outcome.fidelity === 'exact'));
    });
  }
}
