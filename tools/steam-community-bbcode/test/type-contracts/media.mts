// SPDX-License-Identifier: AGPL-3.0-only
import type {SteamEmbeddedMedia, SteamPreviewImage, SteamMdastRoot} from 'steam-community-bbcode';

const video: SteamEmbeddedMedia = {type: 'steamEmbeddedMedia', mediaKind: 'youtube',
  constructId: 'steam.bbcode.previewyoutube', source: 'https://www.youtube.com/watch?v=tax4e4hBBZc',
  layout: 'full', children: []};
const root: SteamMdastRoot = {type: 'root', children: [video]};
// @ts-expect-error YouTube layout is a closed domain value.
const badLayout: SteamEmbeddedMedia = {...video, layout: 'arbitrary-css'};
// @ts-expect-error A media URL is required; children are not a source URL.
const noSource: SteamEmbeddedMedia = {type: 'steamEmbeddedMedia', mediaKind: 'youtube', constructId: 'steam.bbcode.previewyoutube', children: []};
void [root, badLayout, noSource];

const hosted: SteamEmbeddedMedia = {type: 'steamEmbeddedMedia', mediaKind: 'video', constructId: 'steam.bbcode.video',
  source: 'https://example.org/video.mp4', poster: 'https://example.org/poster.png', autoplay: false, children: []};
// @ts-expect-error Playback intent is boolean, not a string copied from a tag.
const badAutoplay: SteamEmbeddedMedia = {...hosted, autoplay: '0'};
void [hosted, badAutoplay];

const preview: SteamPreviewImage = {type: 'steamPreviewImage', constructId: 'steam.bbcode.previewimg',
  image: {kind: 'guideImage', steamImageId: '420', fileName: 'example.png'}, alt: 'A view', rawSource: '[previewimg...]', size: 'full'};
const imageRoot: SteamMdastRoot = {type: 'root', children: [{type: 'paragraph', children: [preview]}]};
// @ts-expect-error An unresolved guide reference cannot masquerade as a URL.
const invalidReference: SteamPreviewImage = {...preview, image: {kind: 'url', steamImageId: '420', fileName: 'image.png'}};
void [imageRoot, invalidReference];
