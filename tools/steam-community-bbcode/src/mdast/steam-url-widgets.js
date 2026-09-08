// SPDX-License-Identifier: AGPL-3.0-only
import {LinkifyIt} from 'linkify-it';
import {findSteamRendererDefinition} from '../steam/construct-definition.js';
import {sourceTextPositionCursor} from '../steam/source-text-position-cursor.js';

// Native supported configuration: no fuzzy addresses, alternate protocols,
// custom regex fragments or source rewriting. URL parsing below owns hosts.
const scanner = new LinkifyIt({fuzzyLink: false, fuzzyEmail: false})
  .add('ftp:', null).add('mailto:', null).add('//', null);

/** @typedef {NonNullable<ReturnType<typeof steamUrlWidgetKind>>} SteamUrlWidgetKind */
/** @typedef {{kind: SteamUrlWidgetKind, constructId: import('../steam/registry-identifiers.js').ConstructId}} SteamUrlWidget */

/**
 * Recognize only the recorded URL families, not current remote playback. The
 * registry separately qualifies surface applicability and strength of evidence.
 * @param {string} value
 */
function steamUrlWidgetKind(value) {
  const url = URL.parse(value);
  if (!url || (url.protocol !== 'http:' && url.protocol !== 'https:') || url.username || url.password) return undefined;
  const host = url.hostname;
  if (((host === 'youtube.com' || host === 'www.youtube.com') && url.pathname === '/watch' && url.searchParams.get('v'))
      || (host === 'youtu.be' && /^\/[A-Za-z0-9_-]+$/u.test(url.pathname))) return 'youtube-widget';
  if (host === 'store.steampowered.com' && /^\/app\/\d+(?:\/|$)/u.test(url.pathname)) return 'store-widget';
  if (host === 'steamcommunity.com') {
    if (/^\/sharedfiles\/filedetails\/?$/u.test(url.pathname) && /^\d+$/u.test(url.searchParams.get('id') ?? '')) return 'ugc-widget';
    if (/^\/(?:id|profiles)\/[^/]+\/inventory\/?$/u.test(url.pathname) && /^#\d+_\d+_\d+$/u.test(url.hash)) return 'inventory-widget';
  }
  if (host === 'vimeo.com' && /^\/\d+\/?$/u.test(url.pathname)) return 'vimeo-widget';
  if (host === 'sketchfab.com' && /^\/3d-models\/[^/]+\/?$/u.test(url.pathname)) return 'sketchfab-widget';
  return undefined;
}

/**
 * Split one literal source text node into native text/link nodes, retaining
 * UTF-16 source spans. Opaque regions and active link labels never call this.
 * @param {import('../steam/steam-bbcode-syntax.js').SteamTextSyntax} node
 * @returns {(import('mdast').Text | import('./steam-mdast-nodes.js').SteamLink)[]}
 */
export function steamTextWithUrlWidgets(node) {
  /** @type {(import('mdast').Text | import('./steam-mdast-nodes.js').SteamLink)[]} */
  const children = [];
  let cursor = 0;
  const consume = sourceTextPositionCursor(node);
  for (const match of scanner.match(node.value) ?? []) {
    const kind = steamUrlWidgetKind(match.raw);
    const definition = kind && findSteamRendererDefinition(kind);
    if (!kind || !definition) continue;
    if (cursor < match.index) {
      const value = node.value.slice(cursor, match.index);
      children.push({type: 'text', value, position: consume(match.index)});
    }
    const position = consume(match.lastIndex);
    cursor = match.lastIndex;
    children.push({type: 'link', url: match.raw, children: [{type: 'text', value: match.raw, position}], position,
      data: {steamUrlWidget: {kind, constructId: definition.id}}});
  }
  if (cursor < node.value.length || children.length === 0) {
    const value = node.value.slice(cursor);
    children.push({type: 'text', value, position: consume(node.value.length)});
  }
  return children;
}
