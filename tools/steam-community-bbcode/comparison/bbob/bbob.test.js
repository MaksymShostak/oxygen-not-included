// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {parse} from '@bbob/parser';

const options = {contextFreeTags: ['code', 'noparse'], caseFreeTags: true, enableEscapeTags: false};

/**
 * Drop vendor methods/positions and coalesce adjacent text for structural
 * comparison only. Expected trees below are authored independently from Steam
 * examples. This is not a parser or a production normalization implementation.
 * @param {unknown} value
 * @returns {unknown}
 */
function structure(value) {
  if (Array.isArray(value)) {
    /** @type {unknown[]} */
    const result = [];
    for (const child of value) {
      const projected = structure(child);
      const previous = result.at(-1);
      if (typeof previous === 'string' && typeof projected === 'string') result[result.length - 1] = previous + projected;
      else result.push(projected);
    }
    return result;
  }
  if (value && typeof value === 'object' && 'tag' in value && 'attrs' in value && 'content' in value) {
    return {tag: value.tag, attrs: value.attrs, content: structure(value.content)};
  }
  return value;
}

test('ordinary nested tags and case-insensitive pairs preserve hierarchy', () => {
  assert.deepEqual(structure(parse('[B]bold [i]italic[/i][/B]', options)), [
    {tag: 'B', attrs: {}, content: ['bold ', {tag: 'i', attrs: {}, content: ['italic']}]},
  ]);
});

test('Steam item boundaries are distinct siblings without invented closing tags', () => {
  assert.deepEqual(structure(parse('[list][*]One[*]Two[/list]', options)), [
    {tag: 'list', attrs: {}, content: [
      {tag: '*', attrs: {}, content: []}, 'One', {tag: '*', attrs: {}, content: []}, 'Two',
    ]},
  ]);
});

for (const tag of ['list', 'olist']) {
  test(`nested ${tag} keeps child list inside the parent item interval`, () => {
    assert.deepEqual(structure(parse(`[${tag}][*]Parent[${tag}][*]Child[/${tag}][*]Next[/${tag}]`, options)), [
      {tag, attrs: {}, content: [{tag: '*', attrs: {}, content: []}, 'Parent',
        {tag, attrs: {}, content: [{tag: '*', attrs: {}, content: []}, 'Child']},
        {tag: '*', attrs: {}, content: []}, 'Next']},
    ]);
  });
}

for (const tag of ['code', 'noparse']) {
  test(`${tag} keeps tags, punctuation, backticks, Unicode and line endings opaque`, () => {
    const literal = '[b]*literal*[/b]\r\n```\n\\[i]°C 😀[/i]';
    assert.deepEqual(structure(parse(`[${tag}]${literal}[/${tag}]`, options)), [
      {tag, attrs: {}, content: [literal]},
    ]);
  });
}

for (const [source, tag, attrs] of [
  ['[quote=author]text[/quote]', 'quote', {author: 'author'}],
  ['[quote=author;123]text[/quote]', 'quote', {'author;123': 'author;123'}],
  ['[url="https://example.com/a?x=1"]text[/url]', 'url', {'https://example.com/a?x=1': 'https://example.com/a?x=1'}],
  ['[table noborder=1 equalcells=1]text[/table]', 'table', {noborder: '1', equalcells: '1'}],
  ['[video mp4=https://example.com/a.mp4 poster=https://example.com/a.png]text[/video]', 'video', {mp4: 'https://example.com/a.mp4', poster: 'https://example.com/a.png'}],
  ['[previewimg=420;sizeFull,floatLeft;example.png]text[/previewimg]', 'previewimg', {'420;sizeFull,floatLeft;example.png': '420;sizeFull,floatLeft;example.png'}],
]) {
  test(`Steam attributes remain available: ${String(tag)}`, () => {
    assert.equal(typeof source, 'string');
    assert.deepEqual(structure(parse(/** @type {string} */ (source), options)), [{tag, attrs, content: ['text']}]);
  });
}

test('unknown tags retain the exact source range including attribute spelling', () => {
  const source = 'before [futuretag  foo="bar baz"]text[/futuretag] after';
  const tree = parse(source, options);
  const node = tree.find(item => typeof item === 'object' && item.tag === 'futuretag');
  assert.ok(node?.start && node.end);
  assert.equal(source.slice(node.start.from, node.end.to), '[futuretag  foo="bar baz"]text[/futuretag]');
});

test('unclosed opaque regions are identifiable without reparsing or rewriting input', () => {
  for (const tag of ['code', 'noparse']) {
    const source = `[${tag}][b]literal[/b]`;
    const node = parse(source, options)[0];
    assert.ok(node?.start);
    assert.equal(node.tag, tag);
    assert.equal(node.end, undefined);
    // The semantic validator must preserve this suffix literally and diagnose it.
    // BBob itself parses later tags; accepting that behavior as Steam would be wrong.
    assert.equal(source.slice(node.start.from), source);
  }
});

test('mismatched pairs expose the actual closing source token for rejection', () => {
  const source = '[b][i]text[/b][/i]';
  const node = parse(source, options)[0];
  assert.ok(node?.start && node.end);
  assert.equal(node.tag, 'b');
  assert.equal(source.slice(node.end.from, node.end.to), '[/i]');
  // BBob's recovery tree must never be trusted as a valid Steam tree here.
});

test('unexpected close and unmatched opening retain their lexical evidence', () => {
  assert.deepEqual(structure(parse('text[/b]', options)), ['text[/b]']);
  const node = parse('[b]missing', options)[0];
  assert.equal(node?.tag, 'b');
  assert.equal(node?.end, undefined);
});

test('plain Unicode and LF/CRLF source text survives tokenization', () => {
  for (const newline of ['\n', '\r\n']) {
    const source = `°C 😀 — [sd]${newline}*literal*`;
    // A bracketed word may be generic syntax; its span keeps the literal source
    // available to the Steam registry's unknown-construct policy.
    const tree = parse(source, options);
    assert.equal(tree.map(node => String(node)).join(''), source);
  }
});
