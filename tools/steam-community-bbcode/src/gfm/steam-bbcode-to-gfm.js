// SPDX-License-Identifier: AGPL-3.0-only
import {steamCommunityBbcodeToMdast} from '../mdast/steam-bbcode-to-mdast.js';
import {toMarkdown} from 'mdast-util-to-markdown';
import {gfmToMarkdown} from 'mdast-util-gfm';
import {lowerSteamMdastToGfm} from './lower-steam-mdast.js';
import {rendererAutolinkDiagnostics} from './renderer-autolink-diagnostics.js';

/** @typedef {import('../mdast/steam-bbcode-to-mdast.js').SteamBbcodeToMdastOptions} SteamBbcodeToGfmOptions */
/**
 * Convert Steam source to GFM and retain per-input diagnostics and fidelity.
 * Markdown syntax and escaping belong to the maintained native serializer.
 * @param {string} source
 * @param {SteamBbcodeToGfmOptions} [options]
 * @returns {import('../diagnostics/conversion-result.js').ConversionResult<string>}
 */
export function steamCommunityBbcodeToGfm(source, options = {}) {
  if (typeof source !== 'string') throw new TypeError('The GFM conversion source must be a string.');
  const result = lowerSteamMdastToGfm(steamCommunityBbcodeToMdast(source, options));
  const value = toMarkdown(result.value, {extensions: [gfmToMarkdown()], fences: true});
  return {...result, value, diagnostics: [...result.diagnostics, ...rendererAutolinkDiagnostics(value, result.value)]};
}
