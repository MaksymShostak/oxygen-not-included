// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import test from 'node:test';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {steamCommunityBbcodeToGfm} from '../src/index.js';

const bytes = await readFile(new URL('./fixtures/steamify/original-description.bbcode', import.meta.url));
assert.equal(createHash('sha256').update(bytes).digest('hex'), 'd2d80f2e374a2eb7360d480cd139c458c1404f6ae4592e4131c009e452eb8a3c');
const source = bytes.toString('utf8');
const converted = steamCommunityBbcodeToGfm(source);
const tree = fromMarkdown(converted.value, {extensions: [gfm()], mdastExtensions: [gfmFromMarkdown()]});
/** @param {import('mdast').Nodes} node @returns {Generator<import('mdast').Nodes>} */
function* nodes(node) {
  yield node;
  if ('children' in node) for (const child of node.children) yield* nodes(child);
}
const all = [...nodes(tree)];

test('Steamify actual: deterministic', () => {
  for (let i = 0; i < 10; i += 1) assert.equal(steamCommunityBbcodeToGfm(source).value, converted.value);
});
test('Steamify actual: CRLF and LF equivalent', () => {
  assert.equal(steamCommunityBbcodeToGfm(source.replace(/\n/gu, '\r\n')).value, converted.value);
});
for (const [tag, kind, expected] of /** @type {const} */ ([['h1', 'heading', 6], ['b', 'strong', 29], ['i', 'emphasis', 5], ['list', 'list', 4]])) {
  test(`Steamify actual: ${tag} count`, () => {
    assert.equal(source.split(`[${tag}]`).length - 1, expected);
    assert.equal(all.filter(node => node.type === kind).length, expected);
  });
}
test('Steamify actual: list items', () => {
  assert.equal(source.split('[*]').length - 1, 18);
  assert.equal(all.filter(node => node.type === 'listItem').length, 18);
});
for (const url of ['SHYLION_LINK', 'FAST_TRACK_LINK']) {
  test(`Steamify actual: link ${url}`, () => {
    assert.ok(source.includes(`[url=${url}]`));
    assert.ok(all.some(node => node.type === 'link' && node.url === url));
  });
}
test('Steamify actual: nested italic link', () => {
  const link = all.find(node => node.type === 'link' && node.url === 'FAST_TRACK_LINK');
  assert.ok(link?.type === 'link');
  const emphasis = link.children[0];
  assert.ok(emphasis?.type === 'emphasis');
  assert.equal(emphasis.children[0]?.type === 'text' ? emphasis.children[0].value : '', 'Fast Track');
});
for (const literal of ['[sd]', '[Fixed]', '[keep existing Base Game + DLC compatibility badge row]', '°C', '—']) {
  test(`Steamify actual: literal ${literal}`, () => {
    assert.ok(source.includes(literal));
    assert.ok(all.some(node => node.type === 'text' && node.value.includes(literal)));
  });
}
test('Steamify actual: all prose in order', () => {
  // Frozen historical oracle removes only this fixture's known wrappers. It is
  // independent of the native grammar and MDAST conversion under test.
  const expected = source.replace(/\[\/?(?:h1|b|i|list)\]|\[\*\]|\[url=[^\]]*\]|\[\/url\]/gu, '');
  assert.equal(normalize(visibleText(tree)), normalize(expected));
});

/** @param {string} value */
function normalize(value) { return value.replace(/\s+/gu, ' ').trim(); }
/** @param {import('mdast').Nodes} node @returns {string} */
function visibleText(node) {
  if (node.type === 'text' || node.type === 'code' || node.type === 'inlineCode') return node.value;
  if ('children' in node) {
    const separator = ['root', 'blockquote', 'list', 'listItem', 'table', 'tableRow'].includes(node.type) ? '\n' : '';
    return node.children.map(visibleText).join(separator);
  }
  return '';
}
