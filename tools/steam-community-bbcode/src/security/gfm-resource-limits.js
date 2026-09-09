// SPDX-License-Identifier: AGPL-3.0-only
/**
 * Limits for the native GFM parse and partial Steam renderer. Byte dimensions
 * are UTF-8. Node count includes the root; depth counts edges from the root.
 * Input size is checked before parsing; tree limits before target rendering.
 * @typedef {object} GfmConversionResourceLimits
 * @property {number} maxInputBytes Maximum UTF-8 source size.
 * @property {number} maxNodeCount Maximum number of native MDAST nodes.
 * @property {number} maxNestingDepth Maximum native MDAST depth.
 * @property {number} maxOutputBytes Maximum UTF-8 target size.
 */
/** @type {Readonly<GfmConversionResourceLimits>} */
export const defaultGfmConversionResourceLimits = Object.freeze({
  maxInputBytes: 1_048_576,
  maxNodeCount: 100_000,
  maxNestingDepth: 128,
  maxOutputBytes: 8_388_608,
});

/**
 * Validate caller limits without borrowing Steam-parser-specific dimensions.
 * Depth is capped at 256 to bound the renderer's recursive call stack.
 * @param {Partial<GfmConversionResourceLimits>} overrides
 * @returns {Readonly<GfmConversionResourceLimits>}
 */
export function resolveGfmConversionResourceLimits(overrides) {
  if (!overrides || typeof overrides !== 'object' || Array.isArray(overrides)) throw new TypeError('Resource limits must be an object.');
  for (const key of Object.keys(overrides)) {
    if (!Object.hasOwn(defaultGfmConversionResourceLimits, key)) throw new TypeError(`Unknown resource limit: ${key}`);
  }
  const limits = {...defaultGfmConversionResourceLimits, ...overrides};
  for (const [key, value] of Object.entries(limits)) {
    if (!Number.isSafeInteger(value) || value < 1) throw new RangeError(`${key} must be a positive safe integer.`);
  }
  if (limits.maxNestingDepth > 256) throw new RangeError('maxNestingDepth cannot exceed the supported renderer depth of 256.');
  return Object.freeze(limits);
}
