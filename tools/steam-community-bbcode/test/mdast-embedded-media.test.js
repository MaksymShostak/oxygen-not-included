// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast, steamCommunityBbcodeToGfm} from '../src/index.js';
import {fromMarkdown} from 'mdast-util-from-markdown';

test('a YouTube preview retains its identifier and layout, then becomes a diagnosed link', () => {
  // Parameter order and layout are from the community guide's authored example.
  const source = '[previewyoutube=tax4e4hBBZc;leftthumb][/previewyoutube]';
  const semantic = steamCommunityBbcodeToMdast(source, {profile: 'guide-section'});
  const media = semantic.value.children[0];
  assert.ok(media?.type === 'steamEmbeddedMedia');
  assert.equal(media.mediaKind, 'youtube');
  assert.equal(media.source, 'https://www.youtube.com/watch?v=tax4e4hBBZc');
  assert.equal(media.mediaKind === 'youtube' ? media.layout : undefined, 'leftthumb');
  assert.equal(semantic.diagnostics.length, 0);
  const result = steamCommunityBbcodeToGfm(source, {profile: 'guide-section'});
  const paragraph = fromMarkdown(result.value).children[0];
  assert.ok(paragraph?.type === 'paragraph');
  const link = paragraph.children[0];
  assert.ok(link?.type === 'link');
  assert.equal(link.url, 'https://www.youtube.com/watch?v=tax4e4hBBZc');
  assert.ok(result.diagnostics.some(d => d.code === 'STEAM_MEDIA_EMBED_LOWERED_TO_LINK' && d.fidelity === 'approximate'));
  assert.equal(result.coverage.constructs[0]?.fidelity, 'approximate');
});

test('video parameters preserve source and poster URLs without claiming Steam playback', () => {
  const source = '[video mp4="https://example.org/clip.mp4?a=1&b=2" poster=https://example.org/poster.png autoplay=0][/video]';
  const semantic = steamCommunityBbcodeToMdast(source, {profile: 'ugc-description'});
  const media = semantic.value.children[0];
  assert.ok(media?.type === 'steamEmbeddedMedia' && media.mediaKind === 'video');
  assert.equal(media.source, 'https://example.org/clip.mp4?a=1&b=2');
  assert.equal(media.poster, 'https://example.org/poster.png');
  assert.equal(media.autoplay, false);
  const result = steamCommunityBbcodeToGfm(source);
  const paragraph = fromMarkdown(result.value).children[0];
  assert.ok(paragraph?.type === 'paragraph');
  const link = paragraph.children[0];
  assert.ok(link?.type === 'link');
  assert.equal(link.url, media.source);
  assert.ok(result.diagnostics.some(d => d.code === 'STEAM_MEDIA_EMBED_LOWERED_TO_LINK' && d.fidelity === 'lossy'));
});

for (const source of [
  '[previewyoutube=tax4e4hBBZc;unknown][/previewyoutube]',
  '[previewyoutube=javascript:alert(1);full][/previewyoutube]',
  '[previewyoutube=tax4e4hBBZc;full;extra][/previewyoutube]',
  '[previewyoutube=tax4e4hBBZc;full]Keep this text[/previewyoutube]',
  '[video mp4=javascript:alert(1)][/video]',
  '[video mp4=https://example.org/v poster=data:image/png,AA][/video]',
  '[video mp4=https://example.org/a mp4=https://example.org/b][/video]',
  '[video mp4=https://example.org/a autoplay=2][/video]',
  '[video mp4=https://example.org/a onclick=alert(1)][/video]',
  '[video poster=https://example.org/p][/video]',
]) {
  test(`an unqualified media form preserves all source: ${source}`, () => {
    const result = steamCommunityBbcodeToGfm(source);
    const paragraph = fromMarkdown(result.value).children[0];
    assert.ok(paragraph?.type === 'paragraph');
    assert.deepEqual(paragraph.children.map(n => n.type === 'text' ? n.value : n.type), [source]);
    assert.ok(result.diagnostics.some(d => d.code === 'STEAM_CONSTRUCT_PRESERVED'));
  });
}
