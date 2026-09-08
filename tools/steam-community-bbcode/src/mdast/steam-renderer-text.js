// SPDX-License-Identifier: AGPL-3.0-only
import {findSteamRendererDefinition} from '../steam/construct-definition.js';
import {sourceTextPositionCursor} from '../steam/source-text-position-cursor.js';

/**
 * Recognize literal renderer tokens after BBCode parsing. An emoticon spelling
 * does not establish ownership/existence of its remote asset. Clan placeholders
 * cannot serve as image destinations without a qualified resolution context.
 * @param {import('../steam/steam-bbcode-syntax.js').SteamTextSyntax} node
 * @returns {import('../diagnostics/conversion-result.js').ConversionDiagnostic[]}
 */
export function steamRendererTextDiagnostics(node) {
  /** @type {import('../diagnostics/conversion-result.js').ConversionDiagnostic[]} */
  const diagnostics = [];
  const consume = sourceTextPositionCursor(node);
  for (const match of node.value.matchAll(/([:ː])[A-Za-z_][A-Za-z0-9_]*\1|\{STEAM_CLAN_IMAGE\}\//gu)) {
    const clanImage = match[0].startsWith('{');
    const definition = findSteamRendererDefinition(clanImage ? 'clan-image-placeholder' : 'emoticon-expansion');
    if (!definition) throw new Error('Missing renderer-token registry definition.');
    consume(match.index);
    const sourceSpan = consume(match.index + match[0].length);
    diagnostics.push({scope: 'construct', constructId: definition.id, sourceSpan, severity: 'warning',
      fidelity: clanImage ? 'unsupported' : 'approximate',
      code: clanImage ? 'STEAM_CLAN_IMAGE_UNRESOLVED' : 'STEAM_EMOTICON_PRESERVED_AS_TEXT',
      message: clanImage ? 'The Steam clan-image placeholder has no qualified resolution context; its source is retained.'
        : 'The Steam emoticon token is retained as text; its remote image and hover presentation are not reproduced.'});
  }
  return diagnostics;
}
