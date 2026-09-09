// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import fc from 'fast-check';
import {toMarkdown} from 'mdast-util-to-markdown';
import {gfmToSteamCommunityBbcode, parseSteamCommunityBbcode} from '../src/index.js';

test('reverse literal text survives native Markdown escaping without activating Steam tags', () => {
  fc.assert(fc.property(fc.array(fc.constantFrom('a', ' ', '[', ']', '/', '"', '*', '`', '\\', '😀', '\u00a0', '<script>', '[/NoPaRsE ]'), {maxLength: 60}), parts => {
    const literal = 'Start ' + parts.join('') + ' end';
    const markdown = toMarkdown({type: 'root', children: [{type: 'paragraph', children: [{type: 'text', value: literal}]}]});
    const result = gfmToSteamCommunityBbcode(markdown);
    assert.deepEqual(result.unsupportedSourceNodes, []);
    const syntax = parseSteamCommunityBbcode(result.value);
    assert.deepEqual(syntax.diagnostics, []);
    assert.equal(syntax.children.map(node => {
      assert.ok(node.type === 'steamOpaqueTag' && node.tagName === 'noparse');
      return node.value;
    }).join(''), literal);
  }), {seed: 20260909, numRuns: 1000});
});

test('reverse bounded arbitrary Markdown returns a result with complete source accounting or an input failure', () => {
  const fragment = fc.oneof(fc.string({maxLength: 30}), fc.constantFrom('\n', '\r\n', '```', '[', ']', '*', '> ', '- ', '\u0000', '\ud800', '😀'));
  fc.assert(fc.property(fc.array(fragment, {maxLength: 70}), parts => {
    const result = gfmToSteamCommunityBbcode(parts.join(''), {resourceLimits: {maxNodeCount: 128, maxNestingDepth: 16, maxOutputBytes: 65_536}});
    if (result.diagnostics.some(item => item.scope === 'input' && item.severity === 'error')) {
      assert.equal(result.value, '');
      assert.deepEqual(result.coverage.sourceNodes, []);
    } else assert.ok(result.coverage.sourceNodes.some(node => node.nodeType === 'root'));
  }), {seed: 20260910, numRuns: 1000});
});
