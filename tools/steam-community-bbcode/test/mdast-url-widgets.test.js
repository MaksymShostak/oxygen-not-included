// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast, steamCommunityBbcodeToGfm} from '../src/index.js';
import {fromMarkdown} from 'mdast-util-from-markdown';

for (const [kind, url] of [
  ['youtube-widget', 'https://www.youtube.com/watch?v=tax4e4hBBZc'],
  ['youtube-widget', 'http://youtube.com/watch?v=tax4e4hBBZc'],
  ['youtube-widget', 'https://youtu.be/A_b-19'],
  ['store-widget', 'https://store.steampowered.com/app/457140/'],
  ['store-widget', 'http://store.steampowered.com/app/12'],
  ['ugc-widget', 'https://steamcommunity.com/sharedfiles/filedetails/?id=123'],
  ['ugc-widget', 'https://steamcommunity.com/sharedfiles/filedetails?id=123'],
  ['inventory-widget', 'https://steamcommunity.com/id/example/inventory/#440_2_123'],
  ['inventory-widget', 'https://steamcommunity.com/profiles/123/inventory#440_22_123'],
  ['vimeo-widget', 'https://vimeo.com/123'],
  ['vimeo-widget', 'https://vimeo.com/123/'],
  ['sketchfab-widget', 'https://sketchfab.com/3d-models/example'],
  ['sketchfab-widget', 'https://sketchfab.com/3d-models/example/'],
]) {
  test(`a plain ${kind} URL retains its link destination and accounts for widget loss`, () => {
    const source = `😀 *literal*\r\n(${url}).`;
    const semantic = steamCommunityBbcodeToMdast(source);
    const paragraph = semantic.value.children[0];
    assert.ok(paragraph?.type === 'paragraph');
    const link = paragraph.children.find(n => n.type === 'link');
    assert.ok(link?.type === 'link');
    assert.equal(link.url, url);
    assert.equal(link.data?.steamUrlWidget?.kind, kind);
    assert.deepEqual(link.position?.start, {line: 2, column: 2, offset: 15});
    const outcome = semantic.coverage.constructs.find(c => c.constructId === `steam.renderer.${kind}`);
    assert.ok(outcome);
    assert.equal(source.slice(outcome.sourceSpan.start.offset, outcome.sourceSpan.end.offset), url);
    const result = steamCommunityBbcodeToGfm(source);
    const target = fromMarkdown(result.value).children[0];
    assert.ok(target?.type === 'paragraph');
    assert.ok(target.children.some(n => n.type === 'link' && n.url === url));
    assert.ok(!target.children.some(n => n.type === 'emphasis'));
    assert.ok(result.diagnostics.some(d => d.code === 'STEAM_URL_WIDGET_LOWERED_TO_LINK' && d.fidelity === 'approximate'));
  });
}

test('opaque and linked text do not acquire nested URL widgets; deceptive hosts remain ordinary text', () => {
  const url = 'https://store.steampowered.com/app/457140/';
  for (const source of [`[noparse]${url}[/noparse]`, `[code]${url}[/code]`, `[url=https://example.org]${url}[/url]`,
    'https://store.steampowered.com.evil.invalid/app/457140/', 'https://www.youtube.com/ordinary-page']) {
    const result = steamCommunityBbcodeToMdast(source);
    assert.ok(!result.coverage.constructs.some(c => c.constructId.startsWith('steam.renderer.')));
  }
});

test('widget recognition requires the recorded host, complete path and identifier family', () => {
  for (const url of [
    'https://example.org/watch?v=abc', 'https://youtube.com/watch', 'https://youtube.com/watch?v=',
    'https://youtube.com/other?v=abc', 'https://youtu.be/', 'https://youtu.be/a/b',
    'https://youtu.be/a.b', 'https://youtube.com.evil.invalid/watch?v=abc',
    'https://user@youtube.com/watch?v=abc', 'https://:secret@youtube.com/watch?v=abc',
    'https://store.steampowered.com/not/app/123/', 'https://store.steampowered.com/app/abc/',
    'https://store.steampowered.com/app/123abc', 'https://store.steampowered.com/app/',
    'https://steamcommunity.com/sharedfiles/filedetails/?id=abc',
    'https://steamcommunity.com/sharedfiles/filedetails/',
    'https://steamcommunity.com/sharedfiles/filedetails/extra?id=123',
    'https://steamcommunity.com/not/sharedfiles/filedetails?id=123',
    'https://steamcommunity.com/sharedfiles/filedetails?id=123abc',
    'https://steamcommunity.com/id/example/inventory/#440_2_123abc',
    'https://steamcommunity.com/id/example/inventory/#440_2',
    'https://steamcommunity.com/id/example/inventory/#440_x_123',
    'https://steamcommunity.com/id/example/inventory/',
    'https://steamcommunity.com/id/example/other/#440_2_123',
    'https://steamcommunity.com/id//inventory/#440_2_123',
    'https://steamcommunity.com/id/example/inventory/extra#440_2_123',
    'https://vimeo.com/123abc', 'https://vimeo.com/extra/123', 'https://vimeo.com/123/extra',
    'https://sketchfab.com/3d-models/', 'https://sketchfab.com/3d-models/a/b',
    'https://sketchfab.com/not/3d-models/a',
    'ftp://vimeo.com/123', '//vimeo.com/123', 'vimeo.com/123', 'mailto:vimeo.com/123',
  ]) {
    const result = steamCommunityBbcodeToMdast(url);
    assert.deepEqual(result.coverage.constructs, [], url);
    const paragraph = result.value.children[0];
    assert.ok(paragraph?.type === 'paragraph');
    assert.deepEqual(paragraph.children.map(child => child.type === 'text' ? child.value : child.type), [url]);
  }
});

test('multiple widgets retain all intervening prose, labels and source endpoints', () => {
  const first = 'https://vimeo.com/12';
  const second = 'https://youtu.be/Ab-12';
  for (const [prefix, suffix] of [['', ''], ['before ', ' after']]) {
    const source = `${prefix}${first} and ${second}${suffix}`;
    const result = steamCommunityBbcodeToMdast(source);
    const paragraph = result.value.children[0];
    assert.ok(paragraph?.type === 'paragraph');
    assert.equal(paragraph.children.map(child => child.type === 'text' ? child.value : child.type === 'link' ? child.url : child.type).join(''), source);
    const links = paragraph.children.filter(child => child.type === 'link');
    assert.deepEqual(links.map(link => link.url), [first, second]);
    for (const link of links) {
      const offset = source.indexOf(link.url);
      const position = {start: {line: 1, column: offset + 1, offset},
        end: {line: 1, column: offset + link.url.length + 1, offset: offset + link.url.length}};
      assert.deepEqual(link.position, position);
      assert.deepEqual(link.children, [{type: 'text', value: link.url, position}]);
    }
  }
});
