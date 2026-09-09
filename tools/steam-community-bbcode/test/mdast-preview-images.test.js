// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import test from 'node:test';
import {steamCommunityBbcodeToMdast, steamCommunityBbcodeToGfm} from '../src/index.js';
import {fromMarkdown} from 'mdast-util-from-markdown';

test('a screenshot keeps its explicit URL and alt text while accounting for Steam presentation', () => {
  const source = '[screenshot=420;https://example.org/screen.png]A *literal* view[/screenshot]';
  const semantic = steamCommunityBbcodeToMdast(source, {profile: 'guide-section'});
  const paragraph = semantic.value.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  const preview = paragraph.children[0];
  assert.ok(preview?.type === 'steamPreviewImage');
  assert.deepEqual(preview.image, {kind: 'url', url: 'https://example.org/screen.png', steamImageId: '420'});
  assert.equal(preview.alt, 'A *literal* view');
  const result = steamCommunityBbcodeToGfm(source);
  const target = fromMarkdown(result.value).children[0];
  assert.ok(target?.type === 'paragraph');
  const image = target.children[0];
  assert.ok(image?.type === 'image');
  assert.equal(image.url, 'https://example.org/screen.png');
  assert.equal(image.alt, 'A *literal* view');
  assert.ok(result.diagnostics.some(d => d.code === 'STEAM_PREVIEW_IMAGE_PRESENTATION_OMITTED' && d.fidelity === 'approximate'));
});

test('a guide image remains an unresolved reference rather than a fabricated URL', () => {
  const source = '[previewimg=420;sizeFull,floatLeft;example.png]Alt text[/previewimg]';
  const semantic = steamCommunityBbcodeToMdast(source, {profile: 'guide-section'});
  const paragraph = semantic.value.children[0];
  assert.ok(paragraph?.type === 'paragraph');
  const preview = paragraph.children[0];
  assert.ok(preview?.type === 'steamPreviewImage');
  assert.deepEqual(preview.image, {kind: 'guideImage', steamImageId: '420', fileName: 'example.png'});
  assert.equal(preview.size, 'full');
  assert.equal(preview.alignment, 'left');
  const result = steamCommunityBbcodeToGfm(source);
  const target = fromMarkdown(result.value).children[0];
  assert.ok(target?.type === 'paragraph');
  assert.deepEqual(target.children.map(n => n.type === 'text' ? n.value : n.type), [source]);
  assert.ok(result.diagnostics.some(d => d.code === 'STEAM_GUIDE_IMAGE_UNRESOLVED' && d.fidelity === 'unsupported'));
});

for (const source of [
  '[screenshot]Keep[/screenshot]',
  '[screenshot=;https://example.org/i.png]Keep[/screenshot]',
  '[screenshot=not-an-id;https://example.org/i.png]Keep[/screenshot]',
  '[screenshot=420;]Keep[/screenshot]',
  '[screenshot=420;javascript:alert(1)]Keep[/screenshot]',
  '[screenshot=420;https://example.org/i.png;unknown]Keep[/screenshot]',
  '[previewimg=420;sizeGiant,floatLeft;example.png]Keep[/previewimg]',
  '[previewimg=420;sizeFull,floatCenter;example.png]Keep[/previewimg]',
  '[previewimg=420;sizeFull,floatLeft]Keep[/previewimg]',
  '[previewimg=420;sizeFull,floatLeft;]Keep[/previewimg]',
  '[previewimg=420;sizeFull;example.png]Keep[/previewimg]',
  '[previewimg=420;sizeFull,floatLeft,extra;example.png]Keep[/previewimg]',
  '[previewimg=420;sizeFull,floatLeft;example.png][b]Keep[/b][/previewimg]',
  '[previewimg=420;sizeFull,floatLeft;example.png]Plain [b]Keep[/b][/previewimg]',
  '[previewicon=420;sizeThumb,inline;example.png]Keep[/previewicon]',
]) {
  test(`unqualified image syntax retains source with an explanation: ${source}`, () => {
    const result = steamCommunityBbcodeToGfm(source);
    const target = fromMarkdown(result.value).children[0];
    assert.ok(target?.type === 'paragraph');
    assert.deepEqual(target.children.map(n => n.type === 'text' ? n.value : n.type), [source]);
    assert.ok(result.diagnostics.some(d => d.code === 'STEAM_CONSTRUCT_PRESERVED'));
  });
}

for (const [attributes, size, alignment] of [
  ['420;sizeThumb,floatRight;small.png', 'thumb', 'right'],
  ['420;sizeOriginal,inline;original.png', 'original', 'inline'],
]) {
  test(`guide image layout variants retain their identity without inventing a URL: ${attributes}`, () => {
    const source = `[previewimg=${attributes}]View[/previewimg]`;
    const semantic = steamCommunityBbcodeToMdast(source, {profile: 'guide-section'});
    const paragraph = semantic.value.children[0];
    assert.ok(paragraph?.type === 'paragraph');
    const preview = paragraph.children[0];
    assert.ok(preview?.type === 'steamPreviewImage');
    assert.equal(preview.size, size);
    assert.equal(preview.alignment, alignment);
    assert.equal(preview.image.kind, 'guideImage');
    const result = steamCommunityBbcodeToGfm(source, {profile: 'guide-section'});
    const target = fromMarkdown(result.value).children[0];
    assert.ok(target?.type === 'paragraph');
    assert.deepEqual(target.children.map(n => n.type === 'text' ? n.value : n.type), [source]);
    assert.ok(result.diagnostics.some(d => d.code === 'STEAM_GUIDE_IMAGE_UNRESOLVED'));
  });
}
