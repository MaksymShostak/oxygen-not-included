// SPDX-License-Identifier: AGPL-3.0-only
import {gfmFromMarkdown} from 'mdast-util-gfm';

/**
 * Native GFM tree transforms model GitHub's additional post-parse autolinker.
 * Some GitHub surfaces apply it even to escaped text;
 * the Markdown REST endpoint does not. Report the difference without changing
 * visible source text or inserting invisible characters/HTML workarounds.
 * This is a targeted renderer check, not a general semantic-equivalence proof.
 * Apply the exported native transforms to an isolated copy of the canonical
 * target tree. Reparsing escaped Markdown adds expensive, unnecessary parser
 * work; syntax escaping remains qualified by the actual consumer tests.
 * @param {import('mdast').Root} intended
 * @returns {import('../diagnostics/conversion-result.js').ConversionDiagnostic[]}
 */
export function rendererAutolinkDiagnostics(intended) {
  let rendered = structuredClone(intended);
  for (const extension of gfmFromMarkdown()) {
    for (const transform of extension.transforms ?? []) {
      rendered = transform(rendered) || rendered;
    }
  }
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
