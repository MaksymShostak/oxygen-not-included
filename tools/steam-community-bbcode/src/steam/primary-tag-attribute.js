// SPDX-License-Identifier: AGPL-3.0-only
/**
 * Interpret the already lexed single positional attribute used by url/quote.
 * This is the Steam tag's scalar contract, not a general attribute parser.
 * Additional or malformed quoting is left to literal-preservation policy.
 *
 * @param {string} rawAttributes
 * @returns {string | undefined}
 */
export function primaryTagAttribute(rawAttributes) {
  const attribute = rawAttributes.trim();
  if (!attribute.startsWith('=')) return undefined;
  const value = attribute.slice(1).trim();
  if (value.startsWith('"') || value.startsWith("'")) {
    const quote = value[0];
    if (value.length < 2 || value.at(-1) !== quote) return undefined;
    const body = value.slice(1, -1);
    return body.includes(quote ?? '') ? undefined : body;
  }
  return /["']/u.test(value) ? undefined : value;
}
