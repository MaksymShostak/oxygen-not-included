// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast} from '../src/index.js';

test('a rectangular Steam table uses native rows and cells with nested inline semantics', () => {
  const result = steamCommunityBbcodeToMdast('[table]\n[tr][th]Name[/th][th]Value[/th][/tr]\n[tr][td][b]A[/b][/td][td][u]B[/u][/td][/tr]\n[/table]', {profile: 'review'});
  assert.deepEqual(result.diagnostics, []);
  const table = result.value.children[0];
  assert.ok(table?.type === 'table');
  assert.equal(table.children.length, 2);
  assert.equal(table.children[0]?.children.length, 2);
  assert.equal(table.children[1]?.children[0]?.children[0]?.type, 'strong');
  assert.equal(table.children[1]?.children[1]?.children[0]?.type, 'steamUnderline');
  assert.equal(result.coverage.constructs.filter(outcome => outcome.constructId === 'steam.bbcode.tr').length, 2);
  assert.equal(result.coverage.constructs.filter(outcome => outcome.constructId === 'steam.bbcode.td').length, 2);
});

for (const body of [
  '',
  '[tr][/tr]',
  '[tr extra=1][th]A[/th][/tr]',
  '[tr][th extra=1]A[/th][/tr]',
  '[tr][td]No header[/td][/tr]',
  '[tr][th]A[/th][/tr][tr][th]Not a body cell[/th][/tr]',
  '[tr]Unowned row prose[th]A[/th][/tr]',
  '[tr][th]A[/th][/tr][tr][td]B[/td][td]C[/td][/tr]',
  '[tr][th]A[/th][/tr][tr][td][list][*]B[/list][/td][/tr]',
  'Unowned prose[tr][th]A[/th][/tr]',
  '[tr][th][code]Block code[/code][/th][/tr]',
  '[tr][th][h3]Heading[/h3][/th][/tr]',
  '[tr][th][hr][/hr][/th][/tr]',
]) {
  test(`unrepresentable table structure is preserved without inventing a GFM table: ${body}`, () => {
    const source = `[table]${body}[/table]`;
    const result = steamCommunityBbcodeToMdast(source);
    const paragraph = result.value.children[0];
    assert.ok(paragraph?.type === 'paragraph');
    assert.equal(paragraph.children[0]?.type === 'text' ? paragraph.children[0].value : '', source);
    assert.ok(result.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_TABLE_STRUCTURE_PRESERVED'));
    assert.ok(result.coverage.constructs.every(outcome => outcome.fidelity === 'unsupported'));
  });
}
