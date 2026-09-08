// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import fc from 'fast-check';
import {parseSteamBbcodeSyntax} from '../../src/steam/parse-steam-bbcode-syntax.js';

test('the native grammar preserves nested tags and Steam item boundaries', () => {
  const source = '[olist][*]Outer[b]bold[/b][olist][*]Inner[/olist][*]Next[/olist]';
  const result = parseSteamBbcodeSyntax(source);
  assert.deepEqual(result.diagnostics, []);
  assert.equal(result.cst?.name, 'document');
  assert.equal(result.tokens.map(token => token.image).join(''), source);
  assert.equal(result.tokens.filter(token => token.image === '[*]').length, 3);
  assert.equal(result.tokens.filter(token => token.image === '[olist').length, 2);
  assert.equal(result.cst?.location?.startOffset, 0);
  assert.equal(result.cst?.location?.endOffset, source.length - 1);
  const rootEntries = result.cst?.children['entry'];
  assert.equal(rootEntries?.length, 1, 'The outer list owns all nested content.');
  const outerEntry = rootEntries?.[0];
  assert.ok(outerEntry && 'children' in outerEntry);
  const outerTag = outerEntry.children['tag']?.[0];
  assert.ok(outerTag && 'children' in outerTag);
  assert.equal(outerTag.children['element']?.length, 6);
  const nestedEntry = outerTag.children['element']?.[3];
  assert.ok(nestedEntry && 'children' in nestedEntry);
  const nestedTag = nestedEntry.children['tag']?.[0];
  assert.ok(nestedTag && 'children' in nestedTag);
  assert.equal(nestedTag.location?.startOffset, source.indexOf('[olist]', 1));
  assert.equal(nestedTag.children['element']?.length, 2, 'The nested list contains one boundary and its text.');
});

test('opaque code and noparse preserve inner markup and backticks literally', () => {
  const source = '[code][b]`literal`[/b][/code][noparse][i]text[/i][/noparse]';
  const result = parseSteamBbcodeSyntax(source);
  assert.deepEqual(result.diagnostics, []);
  assert.equal(result.tokens.filter(token => token.tokenType.name === 'CodeText').map(token => token.image).join(''), '[b]`literal`[/b]');
  assert.equal(result.tokens.filter(token => token.tokenType.name === 'NoParseText').map(token => token.image).join(''), '[i]text[/i]');
  assert.equal(result.tokens.filter(token => token.tokenType.name === 'TagStart').length, 0);
});

for (const source of [
  '[quote=Author;123]Text[/quote]',
  '[table noborder=1 equalcells=1][tr][td]A[/td][/tr][/table]',
  '[video mp4=https://example.org/a.mp4 poster="https://example.org/a]b.png"][/video]',
  '[previewimg=420;sizeFull,floatLeft;image.png]Alt[/previewimg]',
  '[futuretag strange="a]b" flag=value]Text[/futuretag]',
  '[B]°C 😀\r\nx[/B]',
  '[CODE][noparse]literal[/noparse][/CODE]',
  '[hr]After[hr][/hr]End',
]) {
  test(`native tokens and ranges retain attribute/unknown syntax: ${source}`, () => {
    const result = parseSteamBbcodeSyntax(source);
    assert.deepEqual(result.diagnostics, []);
    assert.equal(result.tokens.map(token => token.image).join(''), source);
    for (const token of result.tokens) {
      assert.equal(source.slice(token.startOffset, (token.endOffset ?? -1) + 1), token.image);
    }
  });
}

for (const [source, expected] of [
  ['[b][i]text[/b][/i]', 'STEAM_MISMATCHED_CLOSING_TAG'],
  ['[b]unfinished', 'STEAM_UNCLOSED_TAG'],
  ['[code][b]unfinished[/b]', 'STEAM_UNCLOSED_TAG'],
  ['[noparse]unfinished', 'STEAM_UNCLOSED_TAG'],
  ['[/futuretag]tail', 'STEAM_UNMATCHED_CLOSING_TAG'],
  ['[b unfinished', 'STEAM_UNCLOSED_TAG_HEADER'],
]) {
  test(`malformed syntax is retained and diagnosed: ${source}`, () => {
    const result = parseSteamBbcodeSyntax(source ?? '');
    assert.equal(result.tokens.map(token => token.image).join(''), source);
    assert.ok(result.diagnostics.some(diagnostic => diagnostic.code === expected), `Missing ${expected}`);
  });
}

test('resource limits stop excessive depth, node count, attributes and UTF-8 bytes', () => {
  assert.ok(parseSteamBbcodeSyntax('[b][i]text[/i][/b]', {maxNestingDepth: 1}).diagnostics.some(diagnostic => diagnostic.code === 'STEAM_MAX_NESTING_DEPTH_EXCEEDED'));
  assert.ok(parseSteamBbcodeSyntax('[*][*][*]', {maxNodeCount: 2}).diagnostics.some(diagnostic => diagnostic.code === 'STEAM_MAX_NODE_COUNT_EXCEEDED'));
  assert.ok(parseSteamBbcodeSyntax('[url=abcdef]X[/url]', {maxAttributeBytes: 3}).diagnostics.some(diagnostic => diagnostic.code === 'STEAM_MAX_ATTRIBUTE_BYTES_EXCEEDED'));
  assert.ok(parseSteamBbcodeSyntax('😀', {maxInputBytes: 3}).diagnostics.some(diagnostic => diagnostic.code === 'STEAM_MAX_INPUT_BYTES_EXCEEDED'));
  assert.deepEqual(parseSteamBbcodeSyntax('😀', {maxInputBytes: 4}).diagnostics, []);
});

test('a later parse leaves previous CSTs, tokens and diagnostics unchanged', () => {
  const first = parseSteamBbcodeSyntax('[b]First[/b]');
  const before = JSON.stringify(first);
  parseSteamBbcodeSyntax('[i]Second');
  assert.equal(JSON.stringify(first), before);
  assert.deepEqual(parseSteamBbcodeSyntax('[b]Third[/b]').diagnostics, []);
});

test('supported recursion depth is executable and the next level stops with a diagnostic', () => {
  const atLimit = '[b]'.repeat(256) + 'X' + '[/b]'.repeat(256);
  const permitted = parseSteamBbcodeSyntax(atLimit, {maxNestingDepth: 256});
  assert.ok(permitted.cst);
  assert.deepEqual(permitted.diagnostics, []);
  const exceeded = parseSteamBbcodeSyntax('[b]' + atLimit + '[/b]', {maxNestingDepth: 256});
  assert.equal(exceeded.cst, undefined);
  assert.ok(exceeded.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_MAX_NESTING_DEPTH_EXCEEDED'));
});

test('attribute quota uses UTF-8 bytes for complete and unfinished headers', () => {
  assert.deepEqual(parseSteamBbcodeSyntax('[url=é]X[/url]', {maxAttributeBytes: 3}).diagnostics, []);
  for (const source of ['[url=é]X[/url]', '[url=é']) {
    assert.ok(parseSteamBbcodeSyntax(source, {maxAttributeBytes: 2}).diagnostics.some(diagnostic => diagnostic.code === 'STEAM_MAX_ATTRIBUTE_BYTES_EXCEEDED'));
  }
});

test('diagnostic unist spans end after LF, CRLF, CR and surrogate pairs', () => {
  for (const ending of ['\n', '\r\n', '\r']) {
    const source = '[b]😀' + ending;
    const diagnostic = parseSteamBbcodeSyntax(source).diagnostics.find(item => item.code === 'STEAM_UNCLOSED_TAG');
    assert.deepEqual(diagnostic?.sourceSpan, {
      start: {line: 1, column: 1, offset: 0},
      end: {line: 2, column: 1, offset: source.length},
    });
  }
});

test('JavaScript callers receive validation errors for wrong inputs and invalid limits', () => {
  for (const args of [[null], [12], ['', null], ['', {maxInputBytes: 0}], ['', {maxNodeCount: 1.5}], ['', {maxDepth: 1}], ['', {maxNestingDepth: 257}], ['', {maxInputBytes: '10'}]]) {
    assert.throws(() => Reflect.apply(parseSteamBbcodeSyntax, undefined, args), {name: /^(TypeError|RangeError)$/});
  }
});

test('bounded hostile text remains lossless across maintained generated/shrunk cases', () => {
  const fragment = fc.oneof(
    fc.string({maxLength: 20}),
    fc.constantFrom('[b]', '[/b]', '[code]', '[/code]', '[noparse]', '[/noparse]', '[hr]', '[*]', '[', ']', '="', "='", '😀', '\r\n', '\u0000', '\ud800'),
  );
  fc.assert(fc.property(fc.array(fragment, {maxLength: 80}), fragments => {
    const source = fragments.join('');
    const result = parseSteamBbcodeSyntax(source);
    assert.equal(result.source, source);
    assert.equal(result.tokens.map(token => token.image).join(''), source);
    assert.ok(result.cst, 'Every bounded input has a syntactic representation.');
    assert.equal(result.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_LEXICAL_ERROR' || diagnostic.code === 'STEAM_SYNTAX_ERROR'), false);
  }), {seed: 20260908, numRuns: 3000, endOnFailure: false});
});
