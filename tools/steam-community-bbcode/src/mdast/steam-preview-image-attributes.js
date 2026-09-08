// SPDX-License-Identifier: AGPL-3.0-only
import {primaryTagAttribute} from '../steam/primary-tag-attribute.js';
import {isAllowedResourceUrl} from '../security/resource-url.js';

/**
 * Interpret only the two complete parameter examples in the source guide.
 * The guide mentions screenshot sizing but does not specify its field order;
 * unqualified forms remain literal rather than borrowing previewimg's grammar.
 * @param {'previewimg' | 'screenshot'} tagName @param {string} rawAttributes
 * @returns {Pick<import('./steam-mdast-nodes.js').SteamPreviewImage, 'image' | 'size' | 'alignment'> | undefined}
 */
export function steamPreviewImageAttributes(tagName, rawAttributes) {
  const attribute = primaryTagAttribute(rawAttributes);
  if (attribute === undefined) return undefined;
  const fields = attribute.split(';');
  const [steamImageId, details, fileName] = fields;
  if (!steamImageId || !/^\d+$/u.test(steamImageId) || !details) return undefined;
  if (tagName === 'screenshot') {
    if (fields.length !== 2 || !isAllowedResourceUrl(details, 'image')) return undefined;
    return {image: {kind: 'url', url: details, steamImageId}};
  }
  if (fields.length !== 3 || !fileName) return undefined;
  const layout = details.split(',');
  const [rawSize, rawAlignment] = layout;
  if (layout.length !== 2) return undefined;
  const size = rawSize === 'sizeThumb' ? 'thumb' : rawSize === 'sizeFull' ? 'full' : rawSize === 'sizeOriginal' ? 'original' : undefined;
  const alignment = rawAlignment === 'floatLeft' ? 'left' : rawAlignment === 'floatRight' ? 'right' : rawAlignment === 'inline' ? 'inline' : undefined;
  if (size === undefined || alignment === undefined) return undefined;
  return {image: {kind: 'guideImage', steamImageId, fileName}, size, alignment};
}
