// SPDX-License-Identifier: AGPL-3.0-only
/**
 * Project successive slices of an already parsed text token to unist positions.
 * Callers consume increasing UTF-16 offsets and do not split a CRLF pair. Each
 * source code unit is visited once; this does not parse any markup or URLs.
 * @param {import('./steam-bbcode-syntax.js').SteamTextSyntax} node
 */
export function sourceTextPositionCursor(node) {
  let cursor = 0;
  const point = {...node.sourceSpan.start};
  /** @param {number} end @returns {import('unist').Position} */
  return function consume(end) {
    const start = {...point};
    for (let i = cursor; i < end; i++) {
      if (node.value[i] === '\r') {
        if (node.value[i + 1] === '\n' && i + 1 < end) i++;
        point.line++;
        point.column = 1;
      } else if (node.value[i] === '\n') {
        point.line++;
        point.column = 1;
      } else point.column++;
    }
    point.offset = (node.sourceSpan.start.offset ?? 0) + end;
    cursor = end;
    return {start, end: {...point}};
  };
}
