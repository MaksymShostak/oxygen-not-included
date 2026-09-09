// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {toMarkdown} from 'mdast-util-to-markdown';
import {lowerSteamMdastToGfm} from '../src/gfm/lower-steam-mdast.js';

/**
 * This internal consumer accepts typed Steam MDAST with optional positions.
 * Authored trees qualify that boundary, not additional Steam source grammar.
 * @param {import('../src/index.js').SteamMdastRoot} value
 * @returns {import('../src/index.js').ConversionResult<import('../src/index.js').SteamMdastRoot>}
 */
function sourceResult(value) {
  return {value, diagnostics: [], coverage: {registryVersion: 'fixture', profile: 'workshop-item', constructs: [], contextOnlyPolicies: []}};
}

test('paragraph spoilers separate surrounding flow and escape their literal content', () => {
  const source = sourceResult({type: 'root', children: [{type: 'paragraph', children: [
    {type: 'text', value: 'Before'},
    {type: 'steamSpoiler', children: [{type: 'text', value: '<img src=x>\r\n*literal*'}]},
    {type: 'text', value: 'After'},
  ]}]});
  const original = structuredClone(source);
  const result = lowerSteamMdastToGfm(source);
  assert.deepEqual(result.value, {type: 'root', children: [
    {type: 'paragraph', children: [{type: 'text', value: 'Before'}]},
    {type: 'html', value: '<details>\n<summary>Spoiler</summary>'},
    {type: 'paragraph', children: [{type: 'text', value: '<img src=x>\n*literal*'}]},
    {type: 'html', value: '</details>'},
    {type: 'paragraph', children: [{type: 'text', value: 'After'}]},
  ]});
  const rendered = fromMarkdown(toMarkdown(result.value));
  assert.deepEqual(rendered.children.map(n => n.type), ['paragraph', 'html', 'paragraph', 'html', 'paragraph']);
  const content = rendered.children[2];
  assert.ok(content?.type === 'paragraph');
  assert.deepEqual(content.children.map(n => n.type === 'text' ? n.value : n.type), ['<img src=x>\n*literal*']);
  assert.deepEqual(result.diagnostics.map(d => [d.code, d.fidelity, d.sourceSpan]), [['STEAM_SPOILER_LOWERED_TO_DETAILS', 'approximate', undefined]]);
  assert.deepEqual(source, original);
});

test('a spoiler inside phrasing retains text and reports lost concealment without block HTML', () => {
  const result = lowerSteamMdastToGfm(sourceResult({type: 'root', children: [{type: 'heading', depth: 2, children: [
    {type: 'strong', children: [{type: 'steamSpoiler', children: [{type: 'steamNoParse', value: '[b]secret[/b]\rtext'}]}]},
  ]}]}));
  assert.deepEqual(result.value, {type: 'root', children: [{type: 'heading', depth: 2, children: [
    {type: 'strong', children: [{type: 'text', value: '[b]secret[/b]\ntext'}]},
  ]}]});
  assert.deepEqual(result.diagnostics.map(d => [d.code, d.fidelity]), [['STEAM_SPOILER_LOWERED_TO_TEXT', 'lossy']]);
  const output = toMarkdown(result.value);
  assert.ok(!output.includes('<details>'));
  assert.equal(fromMarkdown(output).children[0]?.type, 'heading');
});

test('embedded media retains an authored label through the native link contract', () => {
  const result = lowerSteamMdastToGfm(sourceResult({type: 'root', children: [{
    type: 'steamEmbeddedMedia', constructId: 'steam.bbcode.video', mediaKind: 'video',
    source: 'https://example.org/clip.mp4', children: [{type: 'emphasis', children: [{type: 'text', value: 'Play clip'}]}],
  }]}));
  assert.deepEqual(result.value, {type: 'root', children: [{type: 'paragraph', children: [{
    type: 'link', url: 'https://example.org/clip.mp4', children: [{type: 'emphasis', children: [{type: 'text', value: 'Play clip'}]}],
  }]}]});
  assert.deepEqual(result.diagnostics.map(d => [d.code, d.fidelity]), [['STEAM_MEDIA_EMBED_LOWERED_TO_LINK', 'lossy']]);
  const paragraph = fromMarkdown(toMarkdown(result.value)).children[0];
  assert.ok(paragraph?.type === 'paragraph');
  assert.equal(paragraph.children[0]?.type, 'link');
});

test('typed root fragments lower their Steam children while retaining native container fields', () => {
  /** @type {import('../src/index.js').SteamMdastRoot['children']} */
  const fragments = [
    {type: 'listItem', spread: false, children: [{type: 'paragraph', children: [{type: 'steamUnderline', children: [{type: 'text', value: 'Item'}]}]}]},
    {type: 'tableRow', children: [{type: 'tableCell', children: [{type: 'steamUnderline', children: [{type: 'text', value: 'Cell'}]}]}]},
    {type: 'tableCell', children: [{type: 'steamUnderline', children: [{type: 'text', value: 'Cell'}]}]},
    {type: 'yaml', value: 'title: Example'},
    {type: 'inlineCode', value: 'A\r\nB'},
    {type: 'linkReference', identifier: 'guide', referenceType: 'full', children: [{type: 'steamUnderline', children: [{type: 'text', value: 'Guide'}]}]},
    {type: 'footnoteDefinition', identifier: 'note', children: [{type: 'paragraph', children: [{type: 'steamUnderline', children: [{type: 'text', value: 'Note'}]}]}]},
    {type: 'code', value: 'A\r\nB'},
    {type: 'definition', identifier: 'guide', url: 'https://example.org'},
    {type: 'thematicBreak'},
  ];
  /** @type {import('mdast').RootContent[]} */
  const expected = [
    {type: 'listItem', spread: false, children: [{type: 'paragraph', children: [{type: 'text', value: 'Item'}]}]},
    {type: 'tableRow', children: [{type: 'tableCell', children: [{type: 'text', value: 'Cell'}]}]},
    {type: 'tableCell', children: [{type: 'text', value: 'Cell'}]},
    {type: 'yaml', value: 'title: Example'},
    {type: 'inlineCode', value: 'A\nB'},
    {type: 'linkReference', identifier: 'guide', referenceType: 'full', children: [{type: 'text', value: 'Guide'}]},
    {type: 'footnoteDefinition', identifier: 'note', children: [{type: 'paragraph', children: [{type: 'text', value: 'Note'}]}]},
    {type: 'code', value: 'A\nB'},
    {type: 'definition', identifier: 'guide', url: 'https://example.org'},
    {type: 'thematicBreak'},
  ];
  for (const [index, fragment] of fragments.entries()) {
    const source = sourceResult({type: 'root', children: [fragment]});
    const before = structuredClone(source);
    assert.deepEqual(lowerSteamMdastToGfm(source).value, {type: 'root', children: [expected[index]]});
    assert.deepEqual(source, before);
  }
});

test('a standalone phrasing spoiler creates no empty surrounding paragraphs', () => {
  const result = lowerSteamMdastToGfm(sourceResult({type: 'root', children: [{type: 'paragraph', children: [
    {type: 'steamSpoiler', children: [{type: 'text', value: 'Secret'}]},
  ]}]}));
  assert.deepEqual(result.value.children.map(node => node.type), ['html', 'paragraph', 'html']);
});

test('positioned extension diagnostics retain their exact source occurrence', () => {
  const position = {start: {line: 2, column: 3, offset: 8}, end: {line: 2, column: 11, offset: 16}};
  const source = sourceResult({type: 'root', children: [{type: 'paragraph', children: [
    {type: 'steamNoParse', value: 'Raw', position},
    {type: 'steamUnderline', children: [{type: 'text', value: 'U'}], position},
  ]}]});
  const result = lowerSteamMdastToGfm(source);
  const paragraph = result.value.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  assert.deepEqual(paragraph.children[0]?.position, position);
  assert.deepEqual(result.diagnostics[0]?.sourceSpan, position);
  assert.equal(result.diagnostics[0]?.scope, 'construct');
  assert.equal(result.diagnostics[0]?.severity, 'warning');
  assert.deepEqual(result.coverage.constructs, [], 'missing source accounting must not be fabricated');
});
