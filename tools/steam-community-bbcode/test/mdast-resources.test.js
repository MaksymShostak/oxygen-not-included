// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast, gfmToSteamCommunityBbcode} from '../src/index.js';
import {isAllowedResourceUrl} from '../src/security/resource-url.js';

test('URL policy rejects C0, DEL and C1 controls before WHATWG normalization', () => {
  for (const code of [0x00, 0x01, 0x09, 0x0a, 0x0d, 0x1f, 0x7f, 0x80, 0x9f]) {
    for (const kind of /** @type {const} */ (['link', 'image'])) {
      assert.equal(isAllowedResourceUrl(`https://example.org/a${String.fromCharCode(code)}b`, kind), false);
    }
  }
  for (const code of [0x20, 0x7e, 0xa0, 0x20ac]) {
    assert.equal(isAllowedResourceUrl(`https://example.org/a${String.fromCharCode(code)}b`, 'link'), true);
  }
});

test('HTTP and relative resources retain spelling; mailto is an active link only', () => {
  for (const destination of ['http://example.org/a', './guide.md', '//example.org/a', 'mailto:person@example.org']) {
    const result = steamCommunityBbcodeToMdast(`[url=${destination}]Label[/url][img]${destination}[/img]`);
    const paragraph = result.value.children[0];
    assert.ok(paragraph?.type === 'paragraph');
    const [link, image] = paragraph.children;
    assert.ok(link?.type === 'link');
    assert.equal(link.url, destination);
    if (destination.startsWith('mailto:')) {
      assert.ok(image?.type === 'text');
      assert.equal(image.value, `[img]${destination}[/img]`);
      assert.equal(result.diagnostics[0]?.code, 'STEAM_UNSAFE_URL_PRESERVED');
    } else {
      assert.ok(image?.type === 'image');
      assert.equal(image.url, destination);
      assert.deepEqual(result.diagnostics, []);
    }
  }
  for (const source of ['[Label](http://example.org/a)', '[Email](mailto:person@example.org)', '![](http://example.org/a.png)']) {
    const result = gfmToSteamCommunityBbcode(source);
    assert.deepEqual(result.diagnostics, []);
    const semantic = steamCommunityBbcodeToMdast(result.value);
    const paragraph = semantic.value.children[0];
    assert.ok(paragraph?.type === 'paragraph');
    assert.equal(paragraph.children[0]?.type, source.startsWith('!') ? 'image' : 'link');
  }
  assert.equal(gfmToSteamCommunityBbcode('![](mailto:person@example.org)').unsupportedSourceNodes[0]?.nodeType, 'image');
});

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

test('single-quoted destinations accept spacing around the primary attribute', () => {
  const result = steamCommunityBbcodeToMdast("[url = 'https://example.org/a%20b']Label[/url]");
  const paragraph = result.value.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  const link = paragraph.children[0];
  assert.ok(link?.type === 'link');
  assert.equal(link.url, 'https://example.org/a%20b');
  assert.equal(link.children[0]?.type === 'text' ? link.children[0].value : '', 'Label');
  assert.deepEqual(result.diagnostics, []);
});

for (const attribute of ['="https://example.org/a"tail', "='https://example.org/a'tail", "=https://example.org/a'b'", '="https://example.org/"b"']) {
  test(`ambiguous resource quoting never becomes an active destination: ${attribute}`, () => {
    const source = `[url${attribute}]Label[/url]`;
    const result = steamCommunityBbcodeToMdast(source);
    const paragraph = result.value.children[0];
    assert.ok(paragraph?.type === 'paragraph');
    assert.deepEqual(paragraph.children.map(child => child.type === 'text' ? child.value : child.type), [source]);
    assert.ok(result.diagnostics.length > 0);
  });
}

test('a resource body cannot absorb active markup into its destination', () => {
  for (const source of ['[url]https://example.org/[b]path[/b][/url]', '[img]https://example.org/[b]image[/b][/img]']) {
    const result = steamCommunityBbcodeToMdast(source);
    const paragraph = result.value.children[0];
    assert.ok(paragraph?.type === 'paragraph');
    assert.deepEqual(paragraph.children.map(child => child.type === 'text' ? child.value : child.type), [source]);
    assert.ok(result.diagnostics.some(d => d.code === 'STEAM_CONSTRUCT_PRESERVED' && d.message.length > 0));
  }
});
