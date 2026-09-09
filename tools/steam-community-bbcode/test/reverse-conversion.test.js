// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {gfmToSteamCommunityBbcode, parseSteamCommunityBbcode, steamCommunityBbcodeToGfm} from '../src/index.js';
import {semanticProjection} from './conformance/execute-cases.js';

/** @param {string} steam */
function targetTree(steam) {
  const syntaxExtensions = gfmFromMarkdown().map(({transforms: _rendererTransforms, ...syntax}) => syntax);
  return semanticProjection(fromMarkdown(steamCommunityBbcodeToGfm(steam).value, {extensions: [gfm()], mdastExtensions: syntaxExtensions}));
}

test('reverse preserves heading and inline semantics while protecting literal BBCode', () => {
  const result = gfmToSteamCommunityBbcode('# A **bold** and *italic* ~~gone~~\n\nPlain [b]literal[/b].');
  assert.ok(result.value.startsWith('[h1]'));
  const syntax = parseSteamCommunityBbcode(result.value);
  assert.deepEqual(syntax.diagnostics, []);
  const target = fromMarkdown(steamCommunityBbcodeToGfm(result.value).value, {extensions: [gfm()], mdastExtensions: gfmFromMarkdown()});
  assert.deepEqual(semanticProjection(target), {type: 'root', children: [
    {type: 'heading', depth: 1, children: [{type: 'text', value: 'A '}, {type: 'strong', children: [{type: 'text', value: 'bold'}]},
      {type: 'text', value: ' and '}, {type: 'emphasis', children: [{type: 'text', value: 'italic'}]},
      {type: 'text', value: ' '}, {type: 'delete', children: [{type: 'text', value: 'gone'}]}]},
    {type: 'paragraph', children: [{type: 'text', value: 'Plain [b]literal[/b].'}]},
  ]});
  assert.deepEqual(result.unsupportedSourceNodes, []);
  assert.equal(result.coverage.direction, 'gfm-to-steam');
  assert.ok(result.coverage.sourceNodes.some(node => node.nodeType === 'strong' && node.fidelity === 'equivalent'));
});

for (const [nodeType, source] of [
  ['heading', '#### Deeper'], ['html', '<script>alert("[b]not a tag[/b]")</script>'],
  ['inlineCode', '`x [b] y`'], ['list', '- [x] Complete'], ['list', '7. Starts at seven'],
  ['code', '```js\nconst x = 1;\n```'], ['code', '```\n[/CoDe ] [b]literal[/b]\n```'],
  ['image', '![Visible alt](https://example.org/image.png)'],
  ['link', '[Label](https://example.org "Title")'], ['link', '[bad](javascript:alert%281%29)'],
  ['link', '[bad](https://example.org/%22[img])'],
  ['link', '[Relative](./guide.md)'], ['image', '![](../image.png)'],
  ['link', '[Relative host](//example.org/guide)'],
  ['table', '| A | B |\n| :- | -: |\n| C | D |'],
  ['table', '| A | B |\n| - | - |\n| C |'],
]) {
  test(`reverse preserves unsupported ${nodeType}: ${source}`, () => {
    assert.ok(source && nodeType);
    const result = gfmToSteamCommunityBbcode(source);
    assert.ok(result.unsupportedSourceNodes.some(d => d.nodeType === nodeType && d.fidelity === 'unsupported' && d.sourceSpan));
    assert.deepEqual(parseSteamCommunityBbcode(result.value).diagnostics, []);
    assert.deepEqual(targetTree(result.value), {type: 'root', children: [{type: 'paragraph', children: [{type: 'text', value: source}]}]});
  });
}

test('reverse literal encoding cannot be terminated by any case of noparse or injected active tags', () => {
  const text = '[/NoPaRsE ] [img]javascript:evil[/img] [*] [b]bold[/b] 😀';
  const result = gfmToSteamCommunityBbcode(text);
  const syntax = parseSteamCommunityBbcode(result.value);
  assert.deepEqual(syntax.diagnostics, []);
  assert.ok(syntax.children.every(n => n.type === 'steamOpaqueTag' && n.tagName === 'noparse'));
  assert.deepEqual(targetTree(result.value), {type: 'root', children: [{type: 'paragraph', children: [{type: 'text', value: text}]}]});
});

for (const [source, limits, code] of [
    ['😀', {maxInputBytes: 3}, 'GFM_MAX_INPUT_BYTES_EXCEEDED'],
    ['*a*', {maxNodeCount: 2}, 'GFM_MAX_NODE_COUNT_EXCEEDED'],
    ['> > Deep', {maxNestingDepth: 2}, 'GFM_MAX_NESTING_DEPTH_EXCEEDED'],
    ['[', {maxOutputBytes: 3}, 'GFM_MAX_OUTPUT_BYTES_EXCEEDED'],
]) {
  test(`reverse enforces ${code} with an input-scoped failure`, () => {
    assert.ok(typeof source === 'string' && limits && typeof limits === 'object');
    const result = gfmToSteamCommunityBbcode(source, {resourceLimits: limits});
    assert.equal(result.value, '');
    assert.ok(result.diagnostics.some(d => d.scope === 'input' && d.severity === 'error' && d.code === code));
    assert.deepEqual(result.coverage.sourceNodes, []);
  });
}

test('reverse accepts exact UTF-8 and tree limits and rejects invalid JavaScript options', () => {
  const exact = gfmToSteamCommunityBbcode('😀', {resourceLimits: {
    maxInputBytes: 4, maxNodeCount: 3, maxNestingDepth: 2, maxOutputBytes: 23,
  }});
  assert.equal(exact.value, '[noparse]😀[/noparse]');
  assert.deepEqual(exact.diagnostics, []);
  for (const options of [null, [], {unknown: true}, {resourceLimits: null},
    {resourceLimits: {maxInputBytes: 0}}, {resourceLimits: {maxOutputBytes: Infinity}},
    {resourceLimits: {maxNestingDepth: 257}}, {resourceLimits: {unknown: 1}}]) {
    assert.throws(() => Reflect.apply(gfmToSteamCommunityBbcode, undefined, ['x', options]));
  }
});

test('reverse qualifies the supported renderer depth and reports the next level', () => {
  const source = '> '.repeat(254) + 'X';
  const permitted = gfmToSteamCommunityBbcode(source, {resourceLimits: {maxNestingDepth: 256}});
  assert.deepEqual(permitted.diagnostics, []);
  assert.deepEqual(parseSteamCommunityBbcode(permitted.value, {resourceLimits: {maxNestingDepth: 256}}).diagnostics, []);
  const exceeded = gfmToSteamCommunityBbcode('> ' + source, {resourceLimits: {maxNestingDepth: 256}});
  assert.equal(exceeded.value, '');
  assert.equal(exceeded.diagnostics[0]?.code, 'GFM_MAX_NESTING_DEPTH_EXCEEDED');
});

test('reverse maps nested lists and blockquotes with real item boundaries', () => {
  const result = gfmToSteamCommunityBbcode('> Quote\n>\n> 1. First\n>    - Nested\n> 2. Second\n\n---');
  assert.ok(result.value.includes('[olist]'));
  assert.ok(result.value.includes('[*]'));
  assert.deepEqual(parseSteamCommunityBbcode(result.value).diagnostics, []);
  assert.deepEqual(targetTree(result.value), {type: 'root', children: [
    {type: 'blockquote', children: [
      {type: 'paragraph', children: [{type: 'text', value: 'Quote'}]},
      {type: 'list', ordered: true, children: [
        {type: 'listItem', children: [{type: 'paragraph', children: [{type: 'text', value: 'First'}]},
          {type: 'list', ordered: false, children: [{type: 'listItem', children: [{type: 'paragraph', children: [{type: 'text', value: 'Nested'}]}]}]}]},
        {type: 'listItem', children: [{type: 'paragraph', children: [{type: 'text', value: 'Second'}]}]},
      ]},
    ]},
    {type: 'thematicBreak'},
  ]});
  assert.deepEqual(result.unsupportedSourceNodes, []);
});

test('reverse maps code payload, links, empty-alt images and rectangular tables', () => {
  const source = '```\n[b]literal[/b]\n```\n\n[Label *inside*](https://example.org/a?q=1&b=2) ![](https://example.org/a.png)\n\n| H | J |\n| - | - |\n| **A** | B |';
  const result = gfmToSteamCommunityBbcode(source);
  assert.ok(result.value.includes('[code][b]literal[/b][/code]'));
  assert.ok(result.value.includes('[table]'));
  assert.deepEqual(targetTree(result.value), {type: 'root', children: [
    {type: 'code', value: '[b]literal[/b]'},
    {type: 'paragraph', children: [{type: 'link', url: 'https://example.org/a?q=1&b=2', children: [{type: 'text', value: 'Label '}, {type: 'emphasis', children: [{type: 'text', value: 'inside'}]}]}, {type: 'text', value: ' '}, {type: 'image', url: 'https://example.org/a.png', alt: ''}]},
    {type: 'table', children: [
      {type: 'tableRow', children: [{type: 'tableCell', children: [{type: 'text', value: 'H'}]}, {type: 'tableCell', children: [{type: 'text', value: 'J'}]}]},
      {type: 'tableRow', children: [{type: 'tableCell', children: [{type: 'strong', children: [{type: 'text', value: 'A'}]}]}, {type: 'tableCell', children: [{type: 'text', value: 'B'}]}]},
    ]},
  ]});
  assert.deepEqual(result.unsupportedSourceNodes, []);
});
