// SPDX-License-Identifier: AGPL-3.0-only
import {primaryTagAttribute} from '../steam/primary-tag-attribute.js';
import {parseSteamNamedAttributes} from '../steam/parse-steam-bbcode-syntax.js';
import {isAllowedResourceUrl} from '../security/resource-url.js';

/**
 * Interpret the documented identifier/layout fields after lexical parsing.
 * This validates the identifier alphabet, not the existence of a YouTube video.
 * @param {string} rawAttributes
 * @returns {{source: string, layout: 'leftthumb' | 'rightthumb' | 'full'} | undefined}
 */
export function steamYoutubePreviewAttributes(rawAttributes) {
  const attribute = primaryTagAttribute(rawAttributes);
  if (attribute === undefined) return undefined;
  const fields = attribute.split(';');
  const [identifier, layout] = fields;
  if (fields.length !== 2 || !identifier || !/^[A-Za-z0-9_-]+$/u.test(identifier)) return undefined;
  if (layout !== 'leftthumb' && layout !== 'rightthumb' && layout !== 'full') return undefined;
  const destination = new URL('https://www.youtube.com/watch');
  destination.searchParams.set('v', identifier);
  return {source: destination.href, layout};
}

/**
 * Only the source-documented video fields are interpreted. Duplicate or unknown
 * fields preserve the whole source instead of choosing a winner or dropping it.
 * @param {string} rawAttributes
 * @returns {{source: string, poster?: string, autoplay?: boolean} | undefined}
 */
export function steamVideoAttributes(rawAttributes) {
  const attributes = parseSteamNamedAttributes(rawAttributes);
  if (!attributes) return undefined;
  /** @type {Set<string>} */
  const seen = new Set();
  /** @type {string | undefined} */
  let source;
  /** @type {string | undefined} */
  let poster;
  /** @type {boolean | undefined} */
  let autoplay;
  for (const {name, value} of attributes) {
    if (seen.has(name)) return undefined;
    seen.add(name);
    switch (name) {
      case 'mp4':
        if (!isAllowedResourceUrl(value, 'link')) return undefined;
        source = value;
        break;
      case 'poster':
        if (!isAllowedResourceUrl(value, 'image')) return undefined;
        poster = value;
        break;
      case 'autoplay':
        if (value !== '0' && value !== '1') return undefined;
        autoplay = value === '1';
        break;
      default: return undefined;
    }
  }
  return source === undefined ? undefined : {source,
    ...(poster === undefined ? {} : {poster}), ...(autoplay === undefined ? {} : {autoplay})};
}
