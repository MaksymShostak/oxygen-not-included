// SPDX-License-Identifier: AGPL-3.0-only
/**
 * WHATWG URL owns parsing, including relative references. The local policy
 * restricts active schemes and rejects controls/backslashes before native
 * normalization can hide them. The reserved base is for parsing only: neither
 * it nor a source destination is fetched, and relative spelling is retained.
 *
 * @param {string} value
 * @param {'link' | 'image'} kind
 * @returns {boolean}
 */
export function isAllowedResourceUrl(value, kind) {
  if (!value || /[\p{Control}\\]/u.test(value)) return false;
  const parsed = URL.parse(value, 'https://steam-bbcode.invalid/');
  return parsed !== null && (parsed.protocol === 'https:' || parsed.protocol === 'http:' || (kind === 'link' && parsed.protocol === 'mailto:'));
}
