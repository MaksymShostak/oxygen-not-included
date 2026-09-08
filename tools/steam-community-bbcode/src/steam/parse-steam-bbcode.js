// SPDX-License-Identifier: AGPL-3.0-only
import {defaultSteamDialectProfile, steamDialectProfileIds} from './registry-identifiers.js';
import {parseSteamBbcodeSyntax} from './parse-steam-bbcode-syntax.js';

/** @typedef {import('./registry-identifiers.js').SteamDialectProfileId} SteamDialectProfileId */
/** @typedef {import('./steam-bbcode-syntax.js').SteamBbcodeSyntaxNode} SteamBbcodeSyntaxNode */
/** @typedef {Readonly<{code: import('../diagnostics/diagnostic-codes.js').SteamParseDiagnosticCode, message: string, sourceSpan?: import('./steam-bbcode-syntax.js').ReadonlySourceSpan}>} SteamBbcodeParseDiagnostic */
/** @typedef {Readonly<{profile?: SteamDialectProfileId, resourceLimits?: Partial<import('../security/resource-limits.js').SteamParseResourceLimits>}>} ParseSteamCommunityBbcodeOptions */
/** @typedef {Readonly<{source: string, profile: SteamDialectProfileId, children: readonly SteamBbcodeSyntaxNode[], diagnostics: readonly SteamBbcodeParseDiagnostic[]}>} SteamBbcodeParseResult */

/** @type {WeakSet<object>} Identities only; the issuing factory owns their shape. */
const issuedParseResults = new WeakSet();

/**
 * Parse source into readonly Steam syntax with structured diagnostics.
 * The default profile is the registry's Workshop-item profile. This is a syntax
 * operation; it does not serialize GFM or claim target representability.
 *
 * @param {string} source
 * @param {ParseSteamCommunityBbcodeOptions} [options]
 * @returns {SteamBbcodeParseResult}
 */
export function parseSteamCommunityBbcode(source, options = {}) {
  if (!options || typeof options !== 'object' || Array.isArray(options)) throw new TypeError('Parse options must be an object.');
  for (const key of Object.keys(options)) {
    if (key !== 'profile' && key !== 'resourceLimits') throw new TypeError(`Unknown parse option: ${key}`);
  }
  const profile = options.profile === undefined ? defaultSteamDialectProfile : options.profile;
  if (!steamDialectProfileIds.some(candidate => candidate === profile)) throw new RangeError('Unknown Steam dialect profile.');
  const parsed = parseSteamBbcodeSyntax(source, options.resourceLimits);
  const diagnostics = parsed.diagnostics.map(diagnostic => Object.freeze({
    ...diagnostic,
    ...(diagnostic.sourceSpan ? {sourceSpan: Object.freeze({
      start: Object.freeze(diagnostic.sourceSpan.start), end: Object.freeze(diagnostic.sourceSpan.end),
    })} : {}),
  }));
  const result = Object.freeze({source, profile, children: Object.freeze(parsed.children), diagnostics: Object.freeze(diagnostics)});
  issuedParseResults.add(result);
  return result;
}

/**
 * Conversion reuses immutable results issued by this parser. A deserialized or
 * fabricated tree has not passed the parser's runtime limits and validation.
 *
 * @param {unknown} value
 * @returns {value is SteamBbcodeParseResult}
 */
export function isIssuedSteamBbcodeParseResult(value) {
  return typeof value === 'object' && value !== null && issuedParseResults.has(value);
}
