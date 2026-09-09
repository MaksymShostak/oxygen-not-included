// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {parseSteamCommunityBbcode} from '../src/index.js';

test('public parser returns source syntax with precise spans and the default profile', () => {
  const source = '[b]°C[/b]';
  const result = parseSteamCommunityBbcode(source);
  assert.equal(result.profile, 'workshop-item');
  assert.equal(result.source, source);
  assert.deepEqual(result.diagnostics, []);
  assert.deepEqual(result.children, [{
    type: 'steamTag', tagName: 'b', rawAttributes: '', headerClosed: true, closingTagName: 'b', rawSource: source,
    sourceSpan: {start: {line: 1, column: 1, offset: 0}, end: {line: 1, column: 10, offset: 9}},
    children: [{type: 'steamText', value: '°C', rawSource: '°C',
      sourceSpan: {start: {line: 1, column: 4, offset: 3}, end: {line: 1, column: 6, offset: 5}}}],
  }]);
});

test('opaque source and unknown attributes survive the public syntax boundary', () => {
  const source = '[futuretag key="a]b"][noparse][b]literal[/b][/noparse][/futuretag]';
  const result = parseSteamCommunityBbcode(source, {profile: 'guide-section'});
  assert.equal(result.profile, 'guide-section');
  const tag = result.children[0];
  assert.equal(tag?.type, 'steamTag');
  assert.ok(tag?.type === 'steamTag');
  assert.equal(tag.rawAttributes, ' key="a]b"');
  assert.equal(tag.rawSource, source);
  const opaque = tag.children[0];
  assert.ok(opaque?.type === 'steamOpaqueTag');
  assert.equal(opaque.tagName, 'noparse');
  assert.equal(opaque.value, '[b]literal[/b]');
  assert.ok(!('children' in opaque));
});

test('public parsing rejects invalid profiles and options instead of changing their meaning', () => {
  for (const options of [{profile: null}, {profile: 'workshop'}, {profil: 'workshop-item'}, null, []]) {
    assert.throws(() => Reflect.apply(parseSteamCommunityBbcode, undefined, ['', options]), {name: /^(TypeError|RangeError)$/});
  }
});

test('public source, syntax, spans and diagnostics cannot be mutated', () => {
  const parsed = parseSteamCommunityBbcode('[b]Text');
  const node = parsed.children[0];
  assert.ok(node);
  assert.equal(Reflect.set(parsed, 'source', 'changed'), false);
  assert.equal(Reflect.set(parsed.children, '0', {}), false);
  assert.equal(Reflect.set(node, 'rawSource', 'changed'), false);
  assert.equal(Reflect.set(node.sourceSpan.start, 'offset', 99), false);
  assert.equal(Reflect.set(parsed.diagnostics, '0', {}), false);
  assert.equal(Reflect.set(parsed.diagnostics[0] ?? {}, 'code', 'changed'), false);
});

test('unpaired unknown bracket labels do not absorb known formatting or its closer', () => {
  const source = '[i]Delivery [Fixed][/i] [sd] [b]Next[/b]';
  const parsed = parseSteamCommunityBbcode(source);
  const emphasis = parsed.children[0];
  assert.ok(emphasis?.type === 'steamTag');
  assert.equal(emphasis.closingTagName, 'i');
  assert.equal(emphasis.children[1]?.rawSource, '[Fixed]');
  assert.equal(parsed.children.at(-1)?.rawSource, '[b]Next[/b]');
  assert.equal(parsed.children.map(child => child.rawSource).join(''), source);
});

test('opaque and horizontal-rule whitespace retains normalized tag names and complete source positions', () => {
  const parts = ['[CODE \r\n]A\rB\nC[/CoDe \t]', '[hr \t][/HR ]', '[noparse \t]X[/NoParse \t]', '[/B \t]'];
  const result = parseSteamCommunityBbcode(parts.join(''));
  assert.deepEqual(result.children.map(node => node.rawSource), parts);
  assert.deepEqual(result.children.map(node => node.type), ['steamOpaqueTag', 'steamTag', 'steamOpaqueTag', 'steamUnmatchedClosingTag']);
  assert.deepEqual(result.children.map(node => 'tagName' in node ? node.tagName : ''), ['code', 'hr', 'noparse', 'b']);
  const first = result.children[0];
  assert.ok(first?.type === 'steamOpaqueTag');
  assert.equal(first.value, 'A\rB\nC');
  assert.equal(first.rawAttributes, ' \r\n');
  assert.deepEqual(result.children.map(node => node.sourceSpan), [
    {start: {line: 1, column: 1, offset: 0}, end: {line: 4, column: 11, offset: 23}},
    {start: {line: 4, column: 11, offset: 23}, end: {line: 4, column: 23, offset: 35}},
    {start: {line: 4, column: 23, offset: 35}, end: {line: 4, column: 47, offset: 59}},
    {start: {line: 4, column: 47, offset: 59}, end: {line: 4, column: 53, offset: 65}},
  ]);
  assert.deepEqual(result.diagnostics.map(d => d.code), ['STEAM_UNMATCHED_CLOSING_TAG']);
  assert.deepEqual(result.diagnostics[0]?.sourceSpan, result.children[3]?.sourceSpan);
});

test('adjacent opaque and boundary constructs release depth and respect the exact node quota', () => {
  const source = '[code]A[/code][noparse]B[/noparse][hr][hr][/hr]';
  const result = parseSteamCommunityBbcode(source, {resourceLimits: {maxNestingDepth: 1, maxNodeCount: 4}});
  assert.deepEqual(result.diagnostics, []);
  assert.equal(result.children.length, 4);
  assert.equal(result.children.map(node => node.rawSource).join(''), source);
  assert.equal(parseSteamCommunityBbcode(source, {resourceLimits: {maxNodeCount: 3}}).diagnostics[0]?.code, 'STEAM_MAX_NODE_COUNT_EXCEEDED');
});

test('parser diagnostic positions and endpoints are immutable', () => {
  const diagnostic = parseSteamCommunityBbcode('[/B]').diagnostics[0];
  assert.ok(diagnostic?.sourceSpan);
  assert.equal(Reflect.set(diagnostic, 'message', 'changed'), false);
  assert.equal(Reflect.set(diagnostic.sourceSpan, 'start', {}), false);
  assert.equal(Reflect.set(diagnostic.sourceSpan.start, 'line', 99), false);
  assert.equal(Reflect.set(diagnostic.sourceSpan.end, 'offset', 99), false);
});

test('unfinished quoted attributes retain source and explain each syntax problem', () => {
  for (const quote of ['"', "'"]) {
    const source = `[url=${quote}https://example.org/unfinished`;
    const parsed = parseSteamCommunityBbcode(source);
    assert.equal(parsed.source, source);
    assert.equal(parsed.children.map(node => node.rawSource).join(''), source);
    assert.deepEqual(parsed.diagnostics.map(d => d.code).sort(),
      ['STEAM_UNCLOSED_ATTRIBUTE_QUOTE', 'STEAM_UNCLOSED_TAG', 'STEAM_UNCLOSED_TAG_HEADER']);
    for (const diagnostic of parsed.diagnostics) {
      assert.notEqual(diagnostic.message.trim(), '', `Missing explanation: ${diagnostic.code}`);
      assert.ok(diagnostic.sourceSpan);
    }
  }
});

test('an unfinished header counts only its attribute bytes and rejects overflow without a partial tree', () => {
  // The attribute text is =" followed by a two-byte UTF-8 character: four bytes.
  const source = 'prefix [url="é';
  const accepted = parseSteamCommunityBbcode(source, {resourceLimits: {maxAttributeBytes: 4}});
  assert.equal(accepted.children.map(node => node.rawSource).join(''), source);
  assert.ok(accepted.diagnostics.some(d => d.code === 'STEAM_UNCLOSED_ATTRIBUTE_QUOTE'));
  assert.ok(accepted.diagnostics.every(d => d.code !== 'STEAM_MAX_ATTRIBUTE_BYTES_EXCEEDED'));

  const rejected = parseSteamCommunityBbcode(source, {resourceLimits: {maxAttributeBytes: 3}});
  assert.equal(rejected.source, source);
  assert.deepEqual(rejected.children, []);
  assert.deepEqual(rejected.diagnostics.map(d => d.code), ['STEAM_MAX_ATTRIBUTE_BYTES_EXCEEDED']);
  assert.notEqual(rejected.diagnostics[0]?.message.trim(), '');
});
