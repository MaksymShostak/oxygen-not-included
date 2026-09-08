// SPDX-License-Identifier: AGPL-3.0-only
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';

/**
 * The native GFM consumer models GitHub's additional post-parse autolinker as
 * well as syntax parsing. Some GitHub surfaces apply it even to escaped text;
 * the Markdown REST endpoint does not. Report the difference without changing
 * visible source text or inserting invisible characters/HTML workarounds.
 * This is a targeted renderer check, not a general semantic-equivalence proof.
 * @param {string} markdown @param {import('mdast').Root} intended
 * @returns {import('../diagnostics/conversion-result.js').ConversionDiagnostic[]}
 */
export function rendererAutolinkDiagnostics(markdown, intended) {
  const rendered = fromMarkdown(markdown, {extensions: [gfm()], mdastExtensions: [gfmFromMarkdown()]});
  const added = linkCount(rendered) - linkCount(intended);
  return added > 0 ? [{scope: 'input', severity: 'warning', fidelity: 'approximate', code: 'GFM_RENDERER_AUTOLINK_POSSIBLE',
    message: `GFM syntax retains literal text, but GitHub surfaces with additional autolinking may turn ${added} literal address(es) into links.`}] : [];
}

/** @param {import('mdast').Root} tree */
function linkCount(tree) {
  /** @type {import('mdast').Nodes[]} */
  const pending = [tree];
  let count = 0;
  while (pending.length) {
    const node = pending.pop();
    if (!node) break;
    if (node.type === 'link') count++;
    if ('children' in node) pending.push(...node.children);
  }
  return count;
}
