// SPDX-License-Identifier: AGPL-3.0-only
import {findSteamTagDefinition} from '../steam/construct-definition.js';

/** @typedef {import('../mdast/steam-mdast-nodes.js').SteamMdastRoot} SteamMdastRoot */
/** @typedef {import('../mdast/steam-mdast-nodes.js').SteamPhrasingContent} SteamPhrasingContent */
/** @typedef {import('../mdast/steam-mdast-nodes.js').SteamFlowContent} SteamFlowContent */
/** @typedef {import('../diagnostics/conversion-result.js').ConversionDiagnostic} ConversionDiagnostic */
/**
 * Lower narrowly typed Steam extensions to the standard MDAST consumer contract.
 * A source spelling is never interpreted as Markdown or untrusted raw HTML.
 * Target policy diagnostics update the corresponding per-construct outcome.
 *
 * @param {import('../diagnostics/conversion-result.js').ConversionResult<SteamMdastRoot>} source
 * @returns {import('../diagnostics/conversion-result.js').ConversionResult<import('mdast').Root>}
 */
export function lowerSteamMdastToGfm(source) {
  /** @type {ConversionDiagnostic[]} */
  const diagnostics = [...source.diagnostics];
  const constructs = [...source.coverage.constructs];
  const constructIndex = new Map(constructs.map((outcome, index) => [`${outcome.constructId}:${outcome.sourceSpan.start.offset}`, index]));

  /**
   * @param {Pick<import('mdast').Node, 'position'>} node @param {string} tagName
   * @param {import('../diagnostics/conversion-result.js').ConversionFidelity} fidelity @param {import('../diagnostics/diagnostic-codes.js').DiagnosticCode} code @param {string} message
   */
  function recordTargetOutcome(node, tagName, fidelity, code, message) {
    const definition = findSteamTagDefinition(tagName);
    if (!definition) throw new Error('The source registry has no definition for a Steam presentation node.');
    recordConstructOutcome(node, definition.id, fidelity, code, message);
  }

  /**
   * @param {Pick<import('mdast').Node, 'position'>} node @param {import('../steam/registry-identifiers.js').ConstructId} constructId
   * @param {import('../diagnostics/conversion-result.js').ConversionFidelity} fidelity @param {import('../diagnostics/diagnostic-codes.js').DiagnosticCode} code @param {string} message
   */
  function recordConstructOutcome(node, constructId, fidelity, code, message) {
    diagnostics.push({scope: 'construct', constructId, severity: 'warning', fidelity, code, message,
      ...(node.position ? {sourceSpan: node.position} : {})});
    const index = constructIndex.get(`${constructId}:${node.position?.start.offset}`);
    const outcome = index === undefined ? undefined : constructs[index];
    if (index !== undefined && outcome) constructs[index] = {...outcome, fidelity};
  }

  /** @param {SteamPhrasingContent} node @returns {import('mdast').PhrasingContent[]} */
  function phrasing(node) {
    switch (node.type) {
      case 'steamPreviewImage':
        if (node.image.kind === 'guideImage') {
          recordConstructOutcome(node, node.constructId, 'unsupported', 'STEAM_GUIDE_IMAGE_UNRESOLVED', 'The guide image identifier has no URL in this document; source is retained without fetching or inventing an image.');
          return [{type: 'text', value: gfmLineEndings(node.rawSource)}];
        }
        recordConstructOutcome(node, node.constructId, 'approximate', 'STEAM_PREVIEW_IMAGE_PRESENTATION_OMITTED', 'The image and alternative text are retained; Steam preview navigation and layout are omitted.');
        return [{type: 'image', url: node.image.url, alt: gfmLineEndings(node.alt)}];
      case 'steamNoParse': return [{type: 'text', value: gfmLineEndings(node.value), ...(node.position ? {position: node.position} : {})}];
      case 'text': case 'inlineCode': return [{...node, value: gfmLineEndings(node.value)}];
      case 'steamUnderline':
        recordTargetOutcome(node, 'u', 'lossy', 'STEAM_UNDERLINE_LOWERED_TO_TEXT', 'Underline presentation is omitted; its content is retained.');
        return node.children.flatMap(phrasing);
      case 'steamSpoiler':
        recordTargetOutcome(node, 'spoiler', 'lossy', 'STEAM_SPOILER_LOWERED_TO_TEXT', 'Spoiler concealment is omitted in this target context; its content is retained.');
        return node.children.flatMap(phrasing);
      case 'steamColor':
        recordTargetOutcome(node, 'color', 'lossy', 'STEAM_COLOR_LOWERED_TO_TEXT', 'Historical color presentation is omitted; its content is retained.');
        return node.children.flatMap(phrasing);
      case 'link':
        if (node.data?.steamUrlWidget) recordConstructOutcome(node, node.data.steamUrlWidget.constructId, 'approximate', 'STEAM_URL_WIDGET_LOWERED_TO_LINK', 'Steam URL-widget presentation becomes an ordinary link; current remote rendering is not inferred.');
        return [{...node, children: node.children.flatMap(phrasing)}];
      case 'strong': case 'emphasis': case 'delete': case 'linkReference':
        return [{...node, children: node.children.flatMap(phrasing)}];
      default: return [node];
    }
  }

  /** @param {import('../mdast/steam-mdast-nodes.js').SteamTableRow} node @returns {import('mdast').TableRow} */
  function tableRow(node) {
    return {...node, children: node.children.map(cell => ({...cell, children: cell.children.flatMap(phrasing)}))};
  }

  /** @param {import('../mdast/steam-mdast-nodes.js').SteamListItem} node @returns {import('mdast').ListItem} */
  function listItem(node) {
    return {...node, children: node.children.flatMap(flow)};
  }

  /** @param {SteamFlowContent} node @returns {(import('mdast').BlockContent | import('mdast').DefinitionContent)[]} */
  function flow(node) {
    switch (node.type) {
      case 'steamEmbeddedMedia':
        recordConstructOutcome(node, node.constructId, node.mediaKind === 'video' ? 'lossy' : 'approximate', 'STEAM_MEDIA_EMBED_LOWERED_TO_LINK', 'Steam media-widget presentation becomes an ordinary link; playback, poster and layout are not retained.');
        return [{type: 'paragraph', children: [{type: 'link', url: node.source,
          children: node.children.length ? node.children.flatMap(phrasing) : [{type: 'text', value: node.mediaKind === 'youtube' ? 'YouTube video' : 'Video'}]}]}];
      case 'steamBlockSpoiler':
        recordTargetOutcome(node, 'spoiler', 'approximate', 'STEAM_SPOILER_LOWERED_TO_DETAILS', 'Steam spoiler presentation becomes a GitHub collapsible block.');
        return [{type: 'html', value: '<details>\n<summary>Spoiler</summary>'}, ...node.children.flatMap(flow), {type: 'html', value: '</details>'}];
      case 'steamAttributedBlockquote':
        recordTargetOutcome(node, 'quote', node.steamCommentId === undefined ? 'equivalent' : 'lossy', 'STEAM_QUOTE_METADATA_LOWERED',
          node.steamCommentId === undefined ? 'Quote attribution becomes visible text inside the blockquote.' : 'Quote attribution is retained; Steam comment navigation metadata is omitted.');
        return [{type: 'blockquote', children: [{type: 'paragraph', children: [{type: 'text', value: `Originally posted by ${node.author}:`}]}, ...node.children.flatMap(flow)]}];
      case 'steamPullQuote':
        recordTargetOutcome(node, 'pullquote', 'approximate', 'STEAM_PULLQUOTE_LOWERED_TO_BLOCKQUOTE', 'Pull-quote presentation becomes an ordinary blockquote.');
        return [{type: 'blockquote', children: node.children.flatMap(flow)}];
      case 'code': return [{...node, value: gfmLineEndings(node.value)}];
      case 'paragraph': return paragraph(node);
      case 'heading': return [{...node, children: node.children.flatMap(phrasing)}];
      case 'blockquote': case 'footnoteDefinition': return [{...node, children: node.children.flatMap(flow)}];
      case 'list': return [{...node, children: node.children.map(listItem)}];
      case 'table':
        if (node.data?.steamTableLayout && Object.values(node.data.steamTableLayout).some(Boolean)) {
          recordTargetOutcome(node, 'table', 'lossy', 'STEAM_TABLE_LAYOUT_OMITTED', 'GFM tables do not retain Steam border or equal-width presentation.');
        }
        return [{...node, children: node.children.map(tableRow)}];
      default: return [node];
    }
  }

  /** @param {import('../mdast/steam-mdast-nodes.js').SteamParagraph} node @returns {import('mdast').BlockContent[]} */
  function paragraph(node) {
    /** @type {import('mdast').BlockContent[]} */
    const blocks = [];
    /** @type {import('mdast').PhrasingContent[]} */
    let inline = [];
    function flush() {
      if (inline.length) blocks.push({...node, children: inline});
      inline = [];
    }
    for (const child of node.children) {
      if (child.type !== 'steamSpoiler') { inline.push(...phrasing(child)); continue; }
      flush();
      recordTargetOutcome(child, 'spoiler', 'approximate', 'STEAM_SPOILER_LOWERED_TO_DETAILS', 'Steam spoiler presentation becomes a GitHub collapsible block.');
      // Only constant, documented HTML is emitted. Source text stays in native
      // MDAST and the Markdown serializer owns escaping and blank-line syntax.
      blocks.push({type: 'html', value: '<details>\n<summary>Spoiler</summary>'});
      blocks.push(...paragraph({type: 'paragraph', children: child.children}));
      blocks.push({type: 'html', value: '</details>'});
    }
    flush();
    return blocks;
  }

  /** @param {SteamMdastRoot['children'][number]} node @returns {import('mdast').RootContent[]} */
  function rootChild(node) {
    switch (node.type) {
      case 'paragraph': case 'heading': case 'blockquote': case 'footnoteDefinition': case 'list': case 'table':
      case 'steamAttributedBlockquote': case 'steamBlockSpoiler': case 'steamPullQuote':
      case 'steamEmbeddedMedia':
      case 'code': case 'definition': case 'thematicBreak': return flow(node);
      case 'listItem': return [listItem(node)];
      case 'tableRow': return [tableRow(node)];
      case 'tableCell': return [{...node, children: node.children.flatMap(phrasing)}];
      case 'yaml': return [node];
      default: return phrasing(node);
    }
  }

  const value = {...source.value, children: source.value.children.flatMap(rootChild)};
  return {value, diagnostics, coverage: {...source.coverage, constructs}};
}

/**
 * GFM output uses LF. Normalize textual line endings at the target boundary;
 * source syntax and its UTF-16 positions retain the caller's original bytes.
 * @param {string} value
 */
function gfmLineEndings(value) { return value.replace(/\r\n?/gu, '\n'); }
