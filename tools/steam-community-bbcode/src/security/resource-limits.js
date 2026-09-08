// SPDX-License-Identifier: AGPL-3.0-only
/**
 * Resource dimensions required by the Steam parser contract.
 *
 * The parser validates JavaScript callers and applies these limits before
 * transformation. A syntax node is a source element or unmatched closing tag;
 * parser bookkeeping and attribute tokens are not counted as document nodes.
 * Byte dimensions denote UTF-8 bytes, not JavaScript UTF-16 code units.
 *
 * @typedef {object} SteamParseResourceLimits
 * @property {number} maxInputBytes Maximum UTF-8 input size.
 * @property {number} maxNestingDepth Maximum nested Steam construct depth.
 * @property {number} maxNodeCount Maximum number of syntax nodes.
 * @property {number} maxAttributeBytes Maximum UTF-8 attribute size per construct.
 */
/** @type {Readonly<SteamParseResourceLimits>} */
export const defaultSteamParseResourceLimits = Object.freeze({
  maxInputBytes: 1_048_576,
  maxNestingDepth: 128,
  maxNodeCount: 100_000,
  maxAttributeBytes: 65_536,
});

/**
 * Resolve validated per-call limits. Nesting is capped at 256 because the native
 * recursive grammar consumes the JavaScript call stack. This is an engine bound,
 * separate from Steam UI character limits.
 *
 * @param {Partial<SteamParseResourceLimits>} overrides
 * @returns {Readonly<SteamParseResourceLimits>}
 */
export function resolveSteamParseResourceLimits(overrides) {
  if (!overrides || typeof overrides !== 'object' || Array.isArray(overrides)) throw new TypeError('Resource limits must be an object.');
  for (const key of Object.keys(overrides)) {
    if (!Object.hasOwn(defaultSteamParseResourceLimits, key)) throw new TypeError(`Unknown resource limit: ${key}`);
  }
  const limits = {...defaultSteamParseResourceLimits, ...overrides};
  for (const [key, value] of Object.entries(limits)) {
    if (!Number.isSafeInteger(value) || value < 1) throw new RangeError(`${key} must be a positive safe integer.`);
  }
  if (limits.maxNestingDepth > 256) throw new RangeError('maxNestingDepth cannot exceed the supported grammar depth of 256.');
  return Object.freeze(limits);
}
