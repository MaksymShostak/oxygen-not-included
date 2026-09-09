// SPDX-License-Identifier: AGPL-3.0-only
import {parseSteamCommunityBbcode, isIssuedSteamBbcodeParseResult} from '../steam/parse-steam-bbcode.js';
import {findSteamTagDefinition, findSteamRendererDefinition} from '../steam/construct-definition.js';
import {steamRegistryVersion, steamConstructDefinitions} from '../steam/construct-registry.js';
import {primaryTagAttribute} from '../steam/primary-tag-attribute.js';
import {isAllowedResourceUrl} from '../security/resource-url.js';
import {rectangularSteamTableRows} from './steam-table-structure.js';
import {steamTableLayout} from './steam-table-layout.js';
import {steamYoutubePreviewAttributes, steamVideoAttributes} from './steam-media-attributes.js';
import {steamPreviewImageAttributes} from './steam-preview-image-attributes.js';
import {steamTextWithUrlWidgets} from './steam-url-widgets.js';
import {steamRendererTextDiagnostics} from './steam-renderer-text.js';

/** @typedef {import('../steam/parse-steam-bbcode.js').ParseSteamCommunityBbcodeOptions} SteamBbcodeToMdastOptions */
/** @typedef {import('../steam/steam-bbcode-syntax.js').SteamBbcodeSyntaxNode} SourceNode */
/** @typedef {import('../diagnostics/conversion-result.js').ConversionDiagnostic} ConversionDiagnostic */
/** @typedef {import('../diagnostics/conversion-result.js').ConstructConversionOutcome} ConstructConversionOutcome */
/** @typedef {import('./steam-mdast-nodes.js').SteamPhrasingContent} PhrasingContent */
/** @typedef {import('./steam-mdast-nodes.js').SteamFlowContent} RootContent */
/**
 * Interpret Steam source as MDAST while retaining diagnostics and per-input
 * outcomes. This layer does not serialize Markdown or perform external I/O.
 * Prepared inputs must be immutable results issued by this package's parser.
 * Their profile cannot change, and resource limits belong to the initial parse.
 *
 * @param {string | import('../steam/parse-steam-bbcode.js').SteamBbcodeParseResult} source
 * @param {SteamBbcodeToMdastOptions} [options]
 * @returns {import('../diagnostics/conversion-result.js').ConversionResult<import('./steam-mdast-nodes.js').SteamMdastRoot>}
 */
export function steamCommunityBbcodeToMdast(source, options = {}) {
  const parsed = conversionSource(source, options);
  /** @type {ConversionDiagnostic[]} */
  const diagnostics = parsed.diagnostics.map(diagnostic => ({...diagnostic, scope: 'input',
    severity: diagnostic.code.startsWith('STEAM_MAX_') ? 'error' : 'warning', fidelity: 'unsupported'}));
  /** @type {ConstructConversionOutcome[]} */
  const constructs = [];

  /** @param {SourceNode} node @param {import('../diagnostics/conversion-result.js').ConversionFidelity} fidelity */
  function record(node, fidelity) {
    const definition = nodeDefinition(node);
    constructs.push({constructId: definition?.id ?? 'steam.bbcode.unknown', fidelity, sourceSpan: node.sourceSpan});
  }

  /**
   * Literal preservation accounts for nested constructs too: recognizing a tag
   * inside a preserved region does not imply that its formatting was converted.
   * @param {SourceNode} node @param {import('../diagnostics/diagnostic-codes.js').DiagnosticCode} code @param {string} message
   * @returns {import('mdast').Text}
   */
  function preserve(node, code, message) {
    const definition = nodeDefinition(node);
    diagnostics.push({code, message, scope: 'construct', severity: 'warning',
      constructId: definition?.id ?? 'steam.bbcode.unknown', fidelity: 'unsupported', sourceSpan: node.sourceSpan});
    const pending = [node];
    while (pending.length) {
      const current = pending.pop();
      if (!current) break;
      if (current.type !== 'steamText') record(current, 'unsupported');
      if (current.type === 'steamTag') pending.push(...current.children);
    }
    return {type: 'text', value: node.rawSource, position: copyPosition(node.sourceSpan)};
  }

  /** @param {SourceNode} node @param {boolean} [insideLink] @returns {PhrasingContent} */
  function phrasing(node, insideLink = false) {
    if (node.type === 'steamText') return {type: 'text', value: node.value, position: copyPosition(node.sourceSpan)};
    if (!nodeDefinition(node)) return preserve(node, 'STEAM_UNKNOWN_CONSTRUCT', 'Unrecognized Steam source is preserved literally.');
    if (node.type === 'steamTag' && node.headerClosed && node.closingTagName === node.tagName && (node.tagName === 'previewimg' || node.tagName === 'screenshot')) {
      const attributes = steamPreviewImageAttributes(node.tagName, node.rawAttributes);
      const definition = nodeDefinition(node);
      if (attributes && definition && node.children.every(child => child.type === 'steamText')) {
        record(node, 'exact');
        return {type: 'steamPreviewImage', constructId: definition.id, ...attributes,
          alt: node.children.map(child => child.rawSource).join(''), rawSource: node.rawSource, position: copyPosition(node.sourceSpan)};
      }
    }
    if (node.type === 'steamTag' && node.headerClosed && node.closingTagName === node.tagName && (node.tagName === 'url' || node.tagName === 'img')) {
      if (node.tagName === 'url' && insideLink) return preserve(node, 'STEAM_NESTED_LINK_PRESERVED', 'A GFM link label cannot contain another active link.');
      const kind = node.tagName === 'url' ? 'link' : 'image';
      const body = node.children.every(child => child.type === 'steamText') ? node.children.map(child => child.rawSource).join('') : undefined;
      const destination = node.rawAttributes.trim() === '' ? body : kind === 'link' ? primaryTagAttribute(node.rawAttributes) : undefined;
      if (destination === undefined) return preserve(node, 'STEAM_CONSTRUCT_PRESERVED', 'This resource needs one unambiguous destination.');
      if (destination.includes('{STEAM_CLAN_IMAGE}')) {
        const definition = findSteamRendererDefinition('clan-image-placeholder');
        if (!definition) throw new Error('Missing clan-image registry definition.');
        constructs.push({constructId: definition.id, fidelity: 'unsupported', sourceSpan: node.sourceSpan});
        diagnostics.push({scope: 'construct', constructId: definition.id, sourceSpan: node.sourceSpan, severity: 'warning',
          code: 'STEAM_CLAN_IMAGE_UNRESOLVED', fidelity: 'unsupported', message: 'A clan-image placeholder cannot become an active resource URL without a qualified resolution context.'});
        return preserve(node, 'STEAM_CONSTRUCT_PRESERVED', 'The resource retains its unresolved Steam placeholder as literal source.');
      }
      if (!isAllowedResourceUrl(destination, kind)) return preserve(node, 'STEAM_UNSAFE_URL_PRESERVED', 'The resource destination is not allowed; source is retained without an active URL.');
      record(node, kind === 'image' ? 'equivalent' : 'exact');
      const position = copyPosition(node.sourceSpan);
      return kind === 'image' ? {type: 'image', url: destination, alt: '', position}
        : {type: 'link', url: destination, children: phrasingChildren(node.children, true), position};
    }
    if (node.type === 'steamOpaqueTag' && node.tagName === 'noparse' && validUnattributedPair(node)) {
      record(node, 'exact');
      return {type: 'steamNoParse', value: node.value, position: copyPosition(node.sourceSpan)};
    }
    if (node.type === 'steamTag' && node.tagName === 'color' && node.headerClosed && node.closingTagName === node.tagName) {
      const color = node.rawAttributes.trim() === '' ? undefined : primaryTagAttribute(node.rawAttributes);
      if (color !== undefined || node.rawAttributes.trim() === '') {
        record(node, 'exact');
        return {type: 'steamColor', ...(color !== undefined ? {color} : {}), children: phrasingChildren(node.children, insideLink), position: copyPosition(node.sourceSpan)};
      }
    }
    if (node.type === 'steamTag' && validUnattributedPair(node)) {
      if (node.tagName === 'b' || node.tagName === 'i' || node.tagName === 'strike') {
        record(node, 'exact');
        return {type: node.tagName === 'b' ? 'strong' : node.tagName === 'i' ? 'emphasis' : 'delete', children: phrasingChildren(node.children, insideLink), position: copyPosition(node.sourceSpan)};
      }
      if (node.tagName === 'u' || node.tagName === 'spoiler') {
        record(node, 'exact');
        return {type: node.tagName === 'u' ? 'steamUnderline' : 'steamSpoiler', children: phrasingChildren(node.children, insideLink), position: copyPosition(node.sourceSpan)};
      }
    }
    return preserve(node, 'STEAM_CONSTRUCT_PRESERVED', 'This construct is preserved literally in this content context.');
  }

  /** @param {readonly SourceNode[]} nodes @param {boolean} [insideLink] @returns {PhrasingContent[]} */
  function phrasingChildren(nodes, insideLink = false) {
    return nodes.flatMap(node => {
      if (node.type !== 'steamText' || insideLink) return [phrasing(node, insideLink)];
      for (const diagnostic of steamRendererTextDiagnostics(node)) {
        diagnostics.push(diagnostic);
        if (diagnostic.scope === 'construct' && diagnostic.sourceSpan) constructs.push({constructId: diagnostic.constructId,
          fidelity: diagnostic.fidelity, sourceSpan: diagnostic.sourceSpan});
      }
      const children = steamTextWithUrlWidgets(node);
      for (const child of children) {
        if (child.type === 'link' && child.data?.steamUrlWidget && child.position) {
          constructs.push({constructId: child.data.steamUrlWidget.constructId, fidelity: 'exact', sourceSpan: child.position});
        }
      }
      return children;
    });
  }

  /** @param {import('../steam/steam-bbcode-syntax.js').SteamTagSyntax} node @returns {import('./steam-mdast-nodes.js').SteamList | undefined} */
  function list(node) {
    /** @type {{marker: SourceNode, body: SourceNode[]}[]} */
    const items = [];
    for (const child of node.children) {
      if (child.type === 'steamListItemBoundary') items.push({marker: child, body: []});
      else {
        const last = items.at(-1);
        if (last) last.body.push(child);
        else if (child.type !== 'steamText' || !/^[ \t\r\n]*$/u.test(child.value)) return undefined;
      }
    }
    if (!items.length) return undefined;
    record(node, 'exact');
    return {type: 'list', ordered: node.tagName === 'olist', spread: false, position: copyPosition(node.sourceSpan),
      children: items.map(item => {
        record(item.marker, 'exact');
        return {type: 'listItem', spread: false, children: flow(item.body, true)};
      })};
  }

  /** @param {SourceNode} node @returns {RootContent | undefined} */
  function block(node) {
    if (node.type === 'steamOpaqueTag' && node.tagName === 'code' && validUnattributedPair(node)) {
      record(node, 'exact');
      return {type: 'code', value: node.value, position: copyPosition(node.sourceSpan)};
    }
    if (node.type === 'steamTag' && node.tagName === 'quote' && node.headerClosed && node.closingTagName === 'quote' && node.rawAttributes.trim() !== '') {
      const attribution = primaryTagAttribute(node.rawAttributes);
      if (attribution === undefined) return undefined;
      const separator = attribution.indexOf(';');
      record(node, 'exact');
      return {type: 'steamAttributedBlockquote', author: separator < 0 ? attribution : attribution.slice(0, separator),
        ...(separator < 0 ? {} : {steamCommentId: attribution.slice(separator + 1)}),
        children: flow(node.children), position: copyPosition(node.sourceSpan)};
    }
    if (node.type !== 'steamTag' || !node.headerClosed || node.closingTagName !== node.tagName) return undefined;
    if (node.tagName === 'previewyoutube') {
      const attributes = steamYoutubePreviewAttributes(node.rawAttributes);
      const definition = nodeDefinition(node);
      if (!attributes || !definition || !node.children.every(child => child.type === 'steamText' && child.value.trim() === '')) return undefined;
      record(node, 'exact');
      return {type: 'steamEmbeddedMedia', mediaKind: 'youtube', constructId: definition.id, ...attributes,
        children: [], position: copyPosition(node.sourceSpan)};
    }
    if (node.tagName === 'video') {
      const attributes = steamVideoAttributes(node.rawAttributes);
      const definition = nodeDefinition(node);
      if (!attributes || !definition || !node.children.every(child => child.type === 'steamText' && child.value.trim() === '')) return undefined;
      record(node, 'exact');
      return {type: 'steamEmbeddedMedia', mediaKind: 'video', constructId: definition.id, ...attributes,
        children: [], position: copyPosition(node.sourceSpan)};
    }
    if (node.tagName === 'table') {
      const layout = steamTableLayout(node.rawAttributes);
      const rows = layout ? rectangularSteamTableRows(node) : undefined;
      if (!rows) return {type: 'paragraph', children: [preserve(node, 'STEAM_TABLE_STRUCTURE_PRESERVED', 'This table or its attributes cannot be represented as a rectangular GFM table without changing its meaning.')]};
      record(node, 'exact');
      return {type: 'table', position: copyPosition(node.sourceSpan), ...(layout && Object.keys(layout).length ? {data: {steamTableLayout: layout}} : {}), children: rows.map(({row, cells}) => {
        record(row, 'exact');
        return {type: 'tableRow', position: copyPosition(row.sourceSpan), children: cells.map(cell => {
          record(cell, 'exact');
          return {type: 'tableCell', children: phrasingChildren(cell.children), position: copyPosition(cell.sourceSpan)};
        })};
      })};
    }
    if (!validUnattributedPair(node)) return undefined;
    if (node.tagName === 'list' || node.tagName === 'olist') {
      const converted = list(node);
      return converted ?? {type: 'paragraph', children: [preserve(node, 'STEAM_LIST_CONTENT_PRESERVED', 'A list needs item boundaries and cannot silently discard leading prose.')]};
    }
    const position = copyPosition(node.sourceSpan);
    switch (node.tagName) {
      case 'spoiler':
        record(node, 'exact');
        return {type: 'steamBlockSpoiler', children: flow(node.children), position};
      case 'pullquote':
        record(node, 'exact');
        return {type: 'steamPullQuote', children: flow(node.children), position};
      case 'h1': case 'h2': case 'h3':
        record(node, 'exact');
        return {type: 'heading', depth: node.tagName === 'h1' ? 1 : node.tagName === 'h2' ? 2 : 3,
          children: phrasingChildren(node.children), position};
      case 'p':
        record(node, 'exact');
        return {type: 'paragraph', children: phrasingChildren(node.children), position};
      case 'quote':
        record(node, 'exact');
        return {type: 'blockquote', children: flow(node.children), position};
      case 'hr':
        record(node, 'exact');
        return {type: 'thematicBreak', position};
      default: return undefined;
    }
  }

  /** @param {readonly SourceNode[]} nodes @param {boolean} [withinListItem] @returns {RootContent[]} */
  function flow(nodes, withinListItem = false) {
    /** @type {RootContent[]} */
    const children = [];
    /** @type {WeakSet<import('mdast').Text>} */
    const ordinaryTextNodes = new WeakSet();
    /** @type {PhrasingContent[]} */
    let inline = [];
    let startsAtFlowBoundary = withinListItem;

    /** @param {number} start @param {1 | -1} step @param {RegExp} pattern */
    function trimLayoutEdge(start, step, pattern) {
      for (let index = start; index >= 0 && index < inline.length; index += step) {
        const child = inline[index];
        if (child?.type !== 'text' || !ordinaryTextNodes.has(child)) break;
        child.value = child.value.replace(pattern, '');
        if (child.value !== '') break;
      }
    }

    /** @param {boolean} [endsAtFlowBoundary] */
    function flush(endsAtFlowBoundary = false) {
      if (!inline.length) return;
      // Only ordinary source text adjacent to an explicit flow boundary is
      // layout. Opaque regions and preserved malformed/unknown source never
      // enter this set. Source syntax and all original positions stay intact.
      if (startsAtFlowBoundary) trimLayoutEdge(0, 1, /^[ \t\r\n]+/u);
      if (endsAtFlowBoundary) trimLayoutEdge(inline.length - 1, -1, /[ \t\r\n]+$/u);
      inline = inline.filter(child => child.type !== 'text' || child.value !== '');
      // Source indentation between block structures is layout, not an empty
      // semantic paragraph. Opaque text is never normalized by this boundary.
      if (!inline.every(child => child.type === 'text' && /^[ \t\r\n]*$/u.test(child.value))) children.push({type: 'paragraph', children: inline});
      inline = [];
    }
    for (const node of nodes) {
      const converted = block(node);
      if (converted) {
        flush(true);
        children.push(converted);
        startsAtFlowBoundary = true;
      } else {
        const inlineNodes = phrasingChildren([node]);
        if (node.type === 'steamText') {
          for (const child of inlineNodes) if (child.type === 'text') ordinaryTextNodes.add(child);
        }
        inline.push(...inlineNodes);
      }
    }
    flush(withinListItem);
    return children;
  }

  /** @type {import('../diagnostics/conversion-result.js').ContextOnlyRendererPolicy[]} */
  const contextOnlyPolicies = steamConstructDefinitions.flatMap(definition =>
    'observation' in definition && definition.observation === 'contextOnly' ? [{constructId: definition.id,
      fidelity: 'unsupported', policy: 'preserve-source', reason: definition.contextReason}] : []);
  return {value: {type: 'root', children: flow(parsed.children)}, diagnostics,
    coverage: {registryVersion: steamRegistryVersion, profile: parsed.profile, constructs, contextOnlyPolicies}};
}

/** @param {string | import('../steam/parse-steam-bbcode.js').SteamBbcodeParseResult} source @param {SteamBbcodeToMdastOptions} options */
function conversionSource(source, options) {
  if (typeof source === 'string') return parseSteamCommunityBbcode(source, options);
  if (!isIssuedSteamBbcodeParseResult(source)) throw new TypeError('Prepared source must be a result issued by parseSteamCommunityBbcode.');
  if (!options || typeof options !== 'object' || Array.isArray(options)) throw new TypeError('Conversion options must be an object.');
  for (const key of Object.keys(options)) {
    if (key !== 'profile' && key !== 'resourceLimits') throw new TypeError(`Unknown conversion option: ${key}`);
  }
  if ('resourceLimits' in options) throw new TypeError('Resource limits must be supplied when parsing the source string.');
  if (options.profile !== undefined && options.profile !== source.profile) throw new RangeError('Prepared source profile cannot be changed.');
  return source;
}

/** @param {SourceNode} node */
function nodeDefinition(node) {
  return node.type === 'steamListItemBoundary' ? findSteamTagDefinition('*') : 'tagName' in node ? findSteamTagDefinition(node.tagName) : undefined;
}

/** @param {import('../steam/steam-bbcode-syntax.js').SteamTagSource} node */
function validUnattributedPair(node) {
  return node.headerClosed && node.closingTagName === node.tagName && node.rawAttributes.trim() === '';
}

/** @param {import('../steam/steam-bbcode-syntax.js').ReadonlySourceSpan} position @returns {import('unist').Position} */
function copyPosition(position) {
  return {start: {...position.start}, end: {...position.end}};
}
