// SPDX-License-Identifier: AGPL-3.0-only
import {parseSteamNamedAttributes} from '../steam/parse-steam-bbcode-syntax.js';

/** @typedef {{noborder?: boolean, equalcells?: boolean}} SteamTableLayout */
/**
 * Recognize only the two sourced Steam layout settings. Duplicate keys and
 * unknown values remain ambiguous; silently choosing a winner would lose source.
 * @param {string} rawAttributes @returns {SteamTableLayout | undefined}
 */
export function steamTableLayout(rawAttributes) {
  const attributes = parseSteamNamedAttributes(rawAttributes);
  if (!attributes) return undefined;
  /** @type {SteamTableLayout} */
  const layout = {};
  /** @type {Set<string>} */
  const names = new Set();
  for (const attribute of attributes) {
    if (names.has(attribute.name) || (attribute.value !== '0' && attribute.value !== '1')) return undefined;
    names.add(attribute.name);
    if (attribute.name === 'noborder') layout.noborder = attribute.value === '1';
    else if (attribute.name === 'equalcells') layout.equalcells = attribute.value === '1';
    else return undefined;
  }
  return layout;
}
