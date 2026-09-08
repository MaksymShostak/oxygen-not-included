// SPDX-License-Identifier: AGPL-3.0-only
// Authored semantic expectations. These builders only abbreviate literal trees;
// none parses a fixture or calls the implementation to calculate an expectation.
/** @param {string} value */
const text = value => ({type: 'text', value});
/** @param {unknown[]} children */
const paragraph = children => ({type: 'paragraph', children});
/** @param {unknown[]} children */
const root = children => ({type: 'root', children});
const literal = 'A *literal* <tag>';
/** @typedef {{id: string, constructIds: readonly import('../../src/index.js').ConstructId[], source: string, expectedMdast: unknown, expectedGfm: unknown, fidelity: import('../../src/index.js').ConversionFidelity, diagnostics: readonly import('../../src/index.js').DiagnosticCode[], malformed: string, malformedPolicy?: 'not-recognized'}} ConstructCase */

/** @type {ConstructCase[]} */
export const constructCases = [];
for (const [tag, type] of /** @type {const} */ ([['b', 'strong'], ['i', 'emphasis'], ['strike', 'delete']])) {
  const tree = root([paragraph([{type, children: [text(literal)]}])]);
  constructCases.push({id: tag, constructIds: [`steam.bbcode.${tag}`], source: `[${tag}]${literal}[/${tag}]`,
    expectedMdast: tree, expectedGfm: tree, fidelity: 'exact', diagnostics: [], malformed: `[${tag}]A`});
}
for (const [tag, depth] of /** @type {const} */ ([['h1', 1], ['h2', 2], ['h3', 3]])) {
  const tree = root([{type: 'heading', depth, children: [text(literal)]}]);
  constructCases.push({id: tag, constructIds: [`steam.bbcode.${tag}`], source: `[${tag}]${literal}[/${tag}]`,
    expectedMdast: tree, expectedGfm: tree, fidelity: 'exact', diagnostics: [], malformed: `[${tag}]A`});
}
for (const [tag, ordered] of /** @type {const} */ ([['list', false], ['olist', true]])) {
  const tree = root([{type: 'list', ordered, children: [{type: 'listItem', children: [paragraph([text(literal)])]}]}]);
  constructCases.push({id: tag, constructIds: [`steam.bbcode.${tag}`, 'steam.bbcode.list-item'], source: `[${tag}][*]${literal}[/${tag}]`,
    expectedMdast: tree, expectedGfm: tree, fidelity: 'exact', diagnostics: [], malformed: `[${tag}][*]A`});
}
const quoted = root([{type: 'blockquote', children: [paragraph([text(literal)])]}]);
const plain = root([paragraph([text(literal)])]);
const table = root([{type: 'table', children: [
  {type: 'tableRow', children: [{type: 'tableCell', children: [text('H')]}]},
  {type: 'tableRow', children: [{type: 'tableCell', children: [text(literal)]}]},
]}]);
constructCases.push(
  {id: 'u', constructIds: ['steam.bbcode.u'], source: `[u]${literal}[/u]`,
    expectedMdast: root([paragraph([{type: 'steamUnderline', children: [text(literal)]}])]), expectedGfm: plain,
    fidelity: 'lossy', diagnostics: ['STEAM_UNDERLINE_LOWERED_TO_TEXT'], malformed: '[u]A'},
  {id: 'spoiler', constructIds: ['steam.bbcode.spoiler'], source: `[spoiler]${literal}[/spoiler]`,
    expectedMdast: root([{type: 'steamBlockSpoiler', children: [paragraph([text(literal)])]}]),
    expectedGfm: root([{type: 'html', value: '<details>\n<summary>Spoiler</summary>'}, paragraph([text(literal)]), {type: 'html', value: '</details>'}]),
    fidelity: 'approximate', diagnostics: ['STEAM_SPOILER_LOWERED_TO_DETAILS'], malformed: '[spoiler]A'},
  {id: 'noparse', constructIds: ['steam.bbcode.noparse'], source: '[noparse][b]*literal*[/b][/noparse]',
    expectedMdast: root([paragraph([{type: 'steamNoParse', value: '[b]*literal*[/b]'}])]), expectedGfm: root([paragraph([text('[b]*literal*[/b]')])]),
    fidelity: 'exact', diagnostics: [], malformed: '[noparse]A'},
  {id: 'code', constructIds: ['steam.bbcode.code'], source: '[code]a`b\n```[/code]',
    expectedMdast: root([{type: 'code', value: 'a`b\n```'}]), expectedGfm: root([{type: 'code', value: 'a`b\n```'}]),
    fidelity: 'exact', diagnostics: [], malformed: '[code]A'},
  {id: 'hr', constructIds: ['steam.bbcode.hr'], source: '[hr][/hr]',
    expectedMdast: root([{type: 'thematicBreak'}]), expectedGfm: root([{type: 'thematicBreak'}]),
    fidelity: 'exact', diagnostics: [], malformed: '[hr=unknown][/hr]'},
  {id: 'url', constructIds: ['steam.bbcode.url'], source: `[url=https://example.org/a?x=1&y=2]${literal}[/url]`,
    expectedMdast: root([paragraph([{type: 'link', url: 'https://example.org/a?x=1&y=2', children: [text(literal)]}])]),
    expectedGfm: root([paragraph([{type: 'link', url: 'https://example.org/a?x=1&y=2', children: [text(literal)]}])]),
    fidelity: 'exact', diagnostics: [], malformed: '[url=https://example.org]A'},
  {id: 'quote', constructIds: ['steam.bbcode.quote'], source: `[quote]${literal}[/quote]`,
    expectedMdast: quoted, expectedGfm: quoted, fidelity: 'exact', diagnostics: [], malformed: '[quote]A'},
  {id: 'p', constructIds: ['steam.bbcode.p'], source: `[p]${literal}[/p]`,
    expectedMdast: plain, expectedGfm: plain, fidelity: 'exact', diagnostics: [], malformed: '[p]A'},
  {id: 'pullquote', constructIds: ['steam.bbcode.pullquote'], source: `[pullquote]${literal}[/pullquote]`,
    expectedMdast: root([{type: 'steamPullQuote', children: [paragraph([text(literal)])]}]), expectedGfm: quoted,
    fidelity: 'approximate', diagnostics: ['STEAM_PULLQUOTE_LOWERED_TO_BLOCKQUOTE'], malformed: '[pullquote]A'},
  {id: 'color', constructIds: ['steam.bbcode.color'], source: `[color]${literal}[/color]`,
    expectedMdast: root([paragraph([{type: 'steamColor', children: [text(literal)]}])]), expectedGfm: plain,
    fidelity: 'lossy', diagnostics: ['STEAM_COLOR_LOWERED_TO_TEXT'], malformed: '[color]A'},
  {id: 'table', constructIds: ['steam.bbcode.table', 'steam.bbcode.tr', 'steam.bbcode.th', 'steam.bbcode.td'],
    source: `[table][tr][th]H[/th][/tr][tr][td]${literal}[/td][/tr][/table]`, expectedMdast: table, expectedGfm: table,
    fidelity: 'exact', diagnostics: [], malformed: '[table][tr][th]H[/th][/tr][tr][td]A'},
  {id: 'img', constructIds: ['steam.bbcode.img'], source: '[img]https://example.org/image.png[/img]',
    expectedMdast: root([paragraph([{type: 'image', url: 'https://example.org/image.png', alt: ''}])]),
    expectedGfm: root([paragraph([{type: 'image', url: 'https://example.org/image.png', alt: ''}])]),
    fidelity: 'equivalent', diagnostics: [], malformed: '[img]https://example.org/image.png'},
);
const youtube = '[previewyoutube=tax4e4hBBZc;leftthumb][/previewyoutube]';
const video = '[video mp4=https://example.org/video.mp4 autoplay=0][/video]';
const preview = `[previewimg=420;sizeFull,floatLeft;example.png]${literal}[/previewimg]`;
const icon = `[previewicon=420;sizeThumb,inline;example.png]${literal}[/previewicon]`;
const screenshot = `[screenshot=420;https://example.org/image.png]${literal}[/screenshot]`;
constructCases.push(
  {id: 'previewyoutube', constructIds: ['steam.bbcode.previewyoutube'], source: youtube,
    expectedMdast: root([{type: 'steamEmbeddedMedia', mediaKind: 'youtube', constructId: 'steam.bbcode.previewyoutube',
      source: 'https://www.youtube.com/watch?v=tax4e4hBBZc', layout: 'leftthumb', children: []}]),
    expectedGfm: root([paragraph([{type: 'link', url: 'https://www.youtube.com/watch?v=tax4e4hBBZc', children: [text('YouTube video')]}])]),
    fidelity: 'approximate', diagnostics: ['STEAM_MEDIA_EMBED_LOWERED_TO_LINK'], malformed: '[previewyoutube=bad;unknown][/previewyoutube]'},
  {id: 'video', constructIds: ['steam.bbcode.video'], source: video,
    expectedMdast: root([{type: 'steamEmbeddedMedia', mediaKind: 'video', constructId: 'steam.bbcode.video', source: 'https://example.org/video.mp4', autoplay: false, children: []}]),
    expectedGfm: root([paragraph([{type: 'link', url: 'https://example.org/video.mp4', children: [text('Video')]}])]),
    fidelity: 'lossy', diagnostics: ['STEAM_MEDIA_EMBED_LOWERED_TO_LINK'], malformed: '[video mp4=javascript:alert(1)][/video]'},
  {id: 'previewimg', constructIds: ['steam.bbcode.previewimg'], source: preview,
    expectedMdast: root([paragraph([{type: 'steamPreviewImage', constructId: 'steam.bbcode.previewimg',
      image: {kind: 'guideImage', steamImageId: '420', fileName: 'example.png'}, alt: literal, rawSource: preview, size: 'full', alignment: 'left'}])]),
    expectedGfm: root([paragraph([text(preview)])]), fidelity: 'unsupported', diagnostics: ['STEAM_GUIDE_IMAGE_UNRESOLVED'],
    malformed: '[previewimg=420;unknown;image.png]A[/previewimg]'},
  {id: 'previewicon', constructIds: ['steam.bbcode.previewicon'], source: icon,
    expectedMdast: root([paragraph([text(icon)])]), expectedGfm: root([paragraph([text(icon)])]),
    fidelity: 'unsupported', diagnostics: ['STEAM_CONSTRUCT_PRESERVED'], malformed: '[previewicon]A'},
  {id: 'screenshot', constructIds: ['steam.bbcode.screenshot'], source: screenshot,
    expectedMdast: root([paragraph([{type: 'steamPreviewImage', constructId: 'steam.bbcode.screenshot',
      image: {kind: 'url', url: 'https://example.org/image.png', steamImageId: '420'}, alt: literal, rawSource: screenshot}])]),
    expectedGfm: root([paragraph([{type: 'image', url: 'https://example.org/image.png', alt: literal}])]),
    fidelity: 'approximate', diagnostics: ['STEAM_PREVIEW_IMAGE_PRESENTATION_OMITTED'], malformed: '[screenshot=420;javascript:alert(1)]A[/screenshot]'},
);
for (const [kind, url, malformed] of /** @type {const} */ ([
  ['youtube-widget', 'https://www.youtube.com/watch?v=tax4e4hBBZc', 'https://www.youtube.com/ordinary-page'],
  ['store-widget', 'https://store.steampowered.com/app/457140/', 'https://store.steampowered.com.invalid/app/457140/'],
  ['ugc-widget', 'https://steamcommunity.com/sharedfiles/filedetails/?id=123', 'https://steamcommunity.com/sharedfiles/filedetails/?missing=123'],
  ['inventory-widget', 'https://steamcommunity.com/id/example/inventory/#440_2_123', 'https://steamcommunity.com/id/example/inventory/#invalid'],
  ['vimeo-widget', 'https://vimeo.com/123', 'https://vimeo.com/ordinary-page'],
  ['sketchfab-widget', 'https://sketchfab.com/3d-models/example', 'https://sketchfab.com/ordinary-page'],
])) {
  const constructId = /** @type {const} */ (`steam.renderer.${kind}`);
  constructCases.push({id: kind, constructIds: [constructId], source: url,
    expectedMdast: root([paragraph([{type: 'link', url, children: [text(url)], data: {steamUrlWidget: {kind, constructId}}}])]),
    expectedGfm: root([paragraph([{type: 'link', url, children: [text(url)]}])]), fidelity: 'approximate',
    diagnostics: ['STEAM_URL_WIDGET_LOWERED_TO_LINK'], malformed, malformedPolicy: 'not-recognized'});
}
constructCases.push(
  {id: 'emoticon', constructIds: ['steam.renderer.emoticon-expansion'], source: ':steamthumbsup:',
    expectedMdast: root([paragraph([text(':steamthumbsup:')])]), expectedGfm: root([paragraph([text(':steamthumbsup:')])]),
    fidelity: 'approximate', diagnostics: ['STEAM_EMOTICON_PRESERVED_AS_TEXT'], malformed: ':steamthumbsup', malformedPolicy: 'not-recognized'},
  {id: 'clan-image', constructIds: ['steam.renderer.clan-image-placeholder'], source: '{STEAM_CLAN_IMAGE}/image.png',
    expectedMdast: root([paragraph([text('{STEAM_CLAN_IMAGE}/image.png')])]), expectedGfm: root([paragraph([text('{STEAM_CLAN_IMAGE}/image.png')])]),
    fidelity: 'unsupported', diagnostics: ['STEAM_CLAN_IMAGE_UNRESOLVED'], malformed: '{STEAM_CLAN_IMAG}/image.png', malformedPolicy: 'not-recognized'},
);
