// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToGfm} from '../src/index.js';
import {fromMarkdown} from 'mdast-util-from-markdown';

test('Steam emoticon tokens retain text and source spans without inventing image assets', () => {
  const source = '😀\r\n:steamthumbsup: and ːsteamthumbsupː';
  const result = steamCommunityBbcodeToGfm(source, {profile: 'review'});
  const occurrences = result.coverage.constructs.filter(c => c.constructId === 'steam.renderer.emoticon-expansion');
  assert.equal(occurrences.length, 2);
  assert.deepEqual(occurrences.map(c => source.slice(c.sourceSpan.start.offset, c.sourceSpan.end.offset)), [':steamthumbsup:', 'ːsteamthumbsupː']);
  assert.deepEqual(occurrences[0]?.sourceSpan.start, {line: 2, column: 1, offset: 4});
  assert.equal(result.diagnostics.filter(d => d.code === 'STEAM_EMOTICON_PRESERVED_AS_TEXT').length, 2);
  const paragraph = fromMarkdown(result.value).children[0];
  assert.ok(paragraph?.type === 'paragraph');
  assert.deepEqual(paragraph.children.map(n => n.type === 'text' ? n.value : n.type), [source.replace('\r\n', '\n')]);
});

test('clan placeholders remain unresolved in prose and never become active relative images', () => {
  for (const source of ['{STEAM_CLAN_IMAGE}/image.png', '[img]{STEAM_CLAN_IMAGE}/image.png[/img]']) {
    const result = steamCommunityBbcodeToGfm(source);
    const paragraph = fromMarkdown(result.value).children[0];
    assert.ok(paragraph?.type === 'paragraph');
    assert.deepEqual(paragraph.children.map(n => n.type === 'text' ? n.value : n.type), [source]);
    assert.ok(result.diagnostics.some(d => d.code === 'STEAM_CLAN_IMAGE_UNRESOLVED'));
    assert.ok(result.coverage.constructs.some(c => c.constructId === 'steam.renderer.clan-image-placeholder' && c.fidelity === 'unsupported'));
  }
});

test('renderer token handling respects code and noparse', () => {
  for (const tag of ['code', 'noparse']) {
    const result = steamCommunityBbcodeToGfm(`[${tag}]:steamthumbsup: {STEAM_CLAN_IMAGE}/x[/${tag}]`);
    assert.ok(!result.coverage.constructs.some(c => c.constructId.startsWith('steam.renderer.')));
  }
});
