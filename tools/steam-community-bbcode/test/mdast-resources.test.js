// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast} from '../src/index.js';

test('attributed links retain nested formatting and images; bare URLs retain their label', () => {
  const result = steamCommunityBbcodeToMdast('[url=FAST_TRACK_LINK][i]Fast Track[/i][/url] [url="https://example.org/a?x=1&y=2"][img]https://example.org/badge.png[/img][/url] [url]https://example.org[/url]');
  assert.deepEqual(result.diagnostics, []);
  const paragraph = result.value.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  const links = paragraph.children.filter(child => child.type === 'link');
  assert.equal(links.length, 3);
  assert.equal(links[0]?.url, 'FAST_TRACK_LINK');
  assert.equal(links[0]?.children[0]?.type, 'emphasis');
  assert.equal(links[1]?.url, 'https://example.org/a?x=1&y=2');
  const image = links[1]?.children[0];
  assert.ok(image?.type === 'image');
  assert.equal(image.url, 'https://example.org/badge.png');
  assert.equal(links[2]?.url, 'https://example.org');
  assert.equal(links[2]?.children[0]?.type === 'text' ? links[2].children[0].value : '', 'https://example.org');
});

for (const destination of ['javascript:alert(1)', 'JaVaScRiPt:alert(1)', 'data:text/html,<script>alert(1)</script>', 'java\nscript:alert(1)', '\0https://example.org', 'file:///C:/private', '\\example.org\\image.png']) {
  test(`unsafe resource remains literal: ${JSON.stringify(destination)}`, () => {
    const source = `[url=${destination}]visible[/url][img]${destination}[/img]`;
    const result = steamCommunityBbcodeToMdast(source);
    const paragraph = result.value.children[0];
    assert.ok(paragraph?.type === 'paragraph');
    assert.ok(paragraph.children.every(child => child.type === 'text'));
    assert.equal(paragraph.children.map(child => child.type === 'text' ? child.value : '').join(''), source);
    assert.equal(result.diagnostics.filter(diagnostic => diagnostic.code === 'STEAM_UNSAFE_URL_PRESERVED').length, 2);
  });
}

test('a nested link is preserved within its outer label with explicit accounting', () => {
  const result = steamCommunityBbcodeToMdast('[url=https://outer.example]A[url=https://inner.example]B[/url][/url]');
  const paragraph = result.value.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  const link = paragraph.children[0];
  assert.ok(link?.type === 'link');
  assert.equal(link.children.some(child => child.type === 'link'), false);
  assert.ok(result.diagnostics.some(diagnostic => diagnostic.code === 'STEAM_NESTED_LINK_PRESERVED'));
});
