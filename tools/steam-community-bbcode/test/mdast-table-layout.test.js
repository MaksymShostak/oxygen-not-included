// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast, steamCommunityBbcodeToGfm} from '../src/index.js';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';

test('known table layout is retained as metadata and its target loss is diagnosed', () => {
  const source = '[table equalcells="1" noborder=1][tr][th]H[/th][/tr][tr][td]C[/td][/tr][/table]';
  const semantic = steamCommunityBbcodeToMdast(source, {profile: 'review'});
  const table = semantic.value.children[0];
  assert.ok(table?.type === 'table');
  assert.deepEqual(table.data?.['steamTableLayout'], {equalcells: true, noborder: true});
  const converted = steamCommunityBbcodeToGfm(source, {profile: 'review'});
  const tree = fromMarkdown(converted.value, {extensions: [gfm()], mdastExtensions: [gfmFromMarkdown()]});
  assert.equal(tree.children[0]?.type, 'table');
  assert.ok(converted.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_TABLE_LAYOUT_OMITTED' && diagnostic.fidelity === 'lossy'));
});

for (const attributes of ['noborder=1 noborder=0', 'noborder=2', 'onclick="alert(1)"', 'noborder="1']) {
  test(`ambiguous or unknown table attributes remain source: ${attributes}`, () => {
    const source = `[table ${attributes}][tr][th]H[/th][/tr][/table]`;
    const result = steamCommunityBbcodeToMdast(source);
    const paragraph = result.value.children[0];
    assert.ok(paragraph?.type === 'paragraph');
    assert.equal(paragraph.children[0]?.type === 'text' ? paragraph.children[0].value : '', source);
    assert.ok(result.diagnostics.length > 0);
  });
}
