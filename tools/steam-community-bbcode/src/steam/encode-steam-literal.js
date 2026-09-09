// SPDX-License-Identifier: AGPL-3.0-only
/**
 * Encode literal text into opaque Steam regions. Each opening bracket has its
 * own region, so even a case-varied noparse closing delimiter cannot terminate
 * the region containing user text. This encodes text; it never parses markup.
 * @param {string} value
 * @returns {string}
 */
export function encodeSteamLiteral(value) {
  return value.split('[').map((part, index) =>
    (index === 0 ? '' : '[noparse][[/noparse]') + (part ? `[noparse]${part}[/noparse]` : '')).join('');
}
