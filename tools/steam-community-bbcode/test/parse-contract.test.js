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
