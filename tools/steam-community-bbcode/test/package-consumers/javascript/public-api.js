// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {parseSteamCommunityBbcode, steamCommunityBbcodeToMdast, steamCommunityBbcodeToGfm, gfmToSteamCommunityBbcode} from 'steam-community-bbcode';

const source = '[b]Packed[/b]';
const parsed = parseSteamCommunityBbcode(source);
assert.equal(parsed.profile, 'workshop-item');
assert.equal(parsed.source, source);
assert.deepEqual(parsed.diagnostics, []);
const tag = parsed.children[0];
assert.ok(tag?.type === 'steamTag');
assert.equal(tag.tagName, 'b');
assert.equal(tag.children[0]?.rawSource, 'Packed');
assert.ok(Object.isFrozen(parsed));
assert.ok(!('cst' in parsed) && !('tokens' in parsed));

const semantic = steamCommunityBbcodeToMdast(parsed);
const paragraph = semantic.value.children[0];
assert.ok(paragraph?.type === 'paragraph');
assert.equal(paragraph.children[0]?.type, 'strong');
const markdown = steamCommunityBbcodeToGfm('[u]Packed[/u]');
assert.equal(markdown.value, 'Packed\n');
assert.ok(markdown.diagnostics.some(d => d.code === 'STEAM_UNDERLINE_LOWERED_TO_TEXT'));
const video = steamCommunityBbcodeToMdast('[video mp4=https://example.org/video.mp4][/video]');
assert.equal(video.value.children[0]?.type, 'steamEmbeddedMedia');
const reverse = gfmToSteamCommunityBbcode('**Packed**');
assert.equal(reverse.value, '[b][noparse]Packed[/noparse][/b]');
assert.equal(reverse.coverage.direction, 'gfm-to-steam');
assert.deepEqual(reverse.unsupportedSourceNodes, []);
const unsupported = gfmToSteamCommunityBbcode('#### Deeper');
assert.equal(unsupported.unsupportedSourceNodes[0]?.nodeType, 'heading');
assert.equal(unsupported.value, '[noparse]#### Deeper[/noparse]');
const limited = gfmToSteamCommunityBbcode('😀', {resourceLimits: {maxInputBytes: 3}});
assert.equal(limited.value, '');
assert.equal(limited.diagnostics[0]?.code, 'GFM_MAX_INPUT_BYTES_EXCEEDED');

// These authored outcomes exercise the installed formatter, independent of
// the source checkout's test helpers or a converter-produced expected value.
assert.equal(steamCommunityBbcodeToGfm('[b][/b]').value, '');
/** @type {[string, string, string[]][]} */
const formattingCases = [
  ['[b]a[b]b[/b]c[/b]', 'strong', ['abc']],
  ['[strike] D [/strike]', 'delete', [' D ']],
  ['[b]a\n\nb[/b]', 'strong', ['a', 'b']],
];
for (const [input, style, text] of formattingCases) {
  const converted = steamCommunityBbcodeToGfm(input);
  const target = fromMarkdown(converted.value, {extensions: [gfm()], mdastExtensions: [gfmFromMarkdown()]});
  assert.equal(target.children.length, text.length);
  for (const [index, block] of target.children.entries()) {
    assert.ok(block.type === 'paragraph');
    assert.equal(block.children.length, 1);
    const mark = block.children[0];
    assert.ok(mark && 'children' in mark);
    assert.equal(mark.type, style);
    assert.deepEqual(mark.children.map(child => child.type === 'text' ? child.value : child.type), [text[index]]);
  }
  assert.deepEqual(converted.diagnostics, []);
}
const tableSource = '[table]\u00a0[tr][th]H[/th][/tr][tr][td]D[/td][/tr][/table]';
const adjacent = steamCommunityBbcodeToGfm('a[i][b]b[/b][/i][i]c[/i]');
assert.deepEqual(adjacent.diagnostics, []);
const adjacentTarget = fromMarkdown(adjacent.value, {extensions: [gfm()], mdastExtensions: [gfmFromMarkdown()]});
const adjacentParagraph = adjacentTarget.children[0];
assert.ok(adjacentParagraph?.type === 'paragraph');
assert.equal(adjacentParagraph.children.length, 3);
const [plain, boldItalic, italic] = adjacentParagraph.children;
assert.ok(plain?.type === 'text');
assert.equal(plain.value, 'a');
assert.ok(boldItalic?.type === 'strong');
assert.equal(boldItalic.children.length, 1);
const nestedItalic = boldItalic.children[0];
assert.ok(nestedItalic?.type === 'emphasis');
assert.deepEqual(nestedItalic.children.map(node => node.type === 'text' ? node.value : node.type), ['b']);
assert.ok(italic?.type === 'emphasis');
assert.deepEqual(italic.children.map(node => node.type === 'text' ? node.value : node.type), ['c']);
const tableResult = steamCommunityBbcodeToGfm(tableSource);
assert.ok(tableResult.diagnostics.some(d => d.code === 'STEAM_TABLE_STRUCTURE_PRESERVED'));
assert.ok(tableResult.coverage.constructs.every(outcome => outcome.fidelity === 'unsupported'));
const tableParagraph = fromMarkdown(tableResult.value).children[0];
assert.ok(tableParagraph?.type === 'paragraph');
assert.deepEqual(tableParagraph.children.map(child => child.type === 'text' ? child.value : child.type), [tableSource]);
