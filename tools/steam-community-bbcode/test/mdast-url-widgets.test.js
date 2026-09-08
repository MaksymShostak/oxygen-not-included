// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast, steamCommunityBbcodeToGfm} from '../src/index.js';
import {fromMarkdown} from 'mdast-util-from-markdown';

for (const [kind, url] of [
  ['youtube-widget', 'https://www.youtube.com/watch?v=tax4e4hBBZc'],
  ['store-widget', 'https://store.steampowered.com/app/457140/'],
  ['ugc-widget', 'https://steamcommunity.com/sharedfiles/filedetails/?id=123'],
  ['inventory-widget', 'https://steamcommunity.com/id/example/inventory/#440_2_123'],
  ['vimeo-widget', 'https://vimeo.com/123'],
  ['sketchfab-widget', 'https://sketchfab.com/3d-models/example'],
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
