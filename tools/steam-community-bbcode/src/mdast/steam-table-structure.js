// SPDX-License-Identifier: AGPL-3.0-only
import {findSteamTagDefinition} from '../steam/construct-definition.js';

/** @typedef {import('../steam/steam-bbcode-syntax.js').SteamTagSyntax} Tag */
/** @typedef {import('../steam/steam-bbcode-syntax.js').SteamBbcodeSyntaxNode} SourceNode */
/**
 * Check the source table's semantic shape before constructing native GFM table
 * nodes. No cells are silently padded, promoted to headers or flattened from
 * blocks. Rejected shapes retain their complete source at the caller.
 *
 * @param {Tag} table
 * @returns {{row: Tag, cells: Tag[]}[] | undefined}
 */
export function rectangularSteamTableRows(table) {
  /** @type {{row: Tag, cells: Tag[]}[]} */
  const rows = [];
  for (const row of table.children) {
    if (isLayout(row)) continue;
    if (row.type !== 'steamTag' || row.tagName !== 'tr' || !plainPair(row)) return undefined;
    /** @type {Tag[]} */
    const cells = [];
    for (const cell of row.children) {
      if (isLayout(cell)) continue;
      if (cell.type !== 'steamTag' || cell.tagName !== (rows.length === 0 ? 'th' : 'td') || !plainPair(cell)) return undefined;
      const pending = [...cell.children];
      while (pending.length) {
        const child = pending.pop();
        if (!child) break;
        if (child.type === 'steamTag' || child.type === 'steamOpaqueTag') {
          const model = findSteamTagDefinition(child.tagName)?.contentModel;
          if (model === 'flow' || model === 'listItems' || model === 'tableRows' || model === 'tableCells' || child.tagName === 'code' || child.tagName === 'hr' || /^h[123]$/u.test(child.tagName) || child.tagName === 'p') return undefined;
          if (child.type === 'steamTag') pending.push(...child.children);
        }
      }
      cells.push(cell);
    }
    if (!cells.length || (rows.length > 0 && cells.length !== rows[0]?.cells.length)) return undefined;
    rows.push({row, cells});
  }
  return rows.length ? rows : undefined;
}

/** @param {SourceNode} node */
function isLayout(node) {
  return node.type === 'steamText' && node.value.trim() === '';
}

/** @param {Tag} node */
function plainPair(node) {
  return node.headerClosed && node.closingTagName === node.tagName && node.rawAttributes.trim() === '';
}
