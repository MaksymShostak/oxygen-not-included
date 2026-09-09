// SPDX-License-Identifier: AGPL-3.0-only
import {steamRegistryVersion} from './construct-registry.js';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {encodeSteamLiteral} from './encode-steam-literal.js';
import {isAllowedResourceUrl} from '../security/resource-url.js';
import {Buffer} from 'node:buffer';
import {resolveGfmConversionResourceLimits} from '../security/gfm-resource-limits.js';

/** @typedef {Readonly<{resourceLimits?: Partial<import('../security/gfm-resource-limits.js').GfmConversionResourceLimits>}>} GfmToSteamBbcodeOptions */

/**
 * Convert a documented subset of GFM into Steam Community BBCode.
 * Unsupported source semantics are reported separately from forward coverage.
 * This pure function does not read files, fetch URLs or render arbitrary HTML.
 * @param {string} source
 * @param {GfmToSteamBbcodeOptions} [options]
 * @returns {import('../diagnostics/conversion-result.js').PartialConversionResult<string>}
 */
export function gfmToSteamCommunityBbcode(source, options = {}) {
  if (typeof source !== 'string') throw new TypeError('GFM source must be a string.');
  if (!options || typeof options !== 'object' || Array.isArray(options)) throw new TypeError('GFM conversion options must be an object.');
  for (const key of Object.keys(options)) {
    if (key !== 'resourceLimits') throw new TypeError(`Unknown GFM conversion option: ${key}`);
  }
  const limits = resolveGfmConversionResourceLimits(options.resourceLimits === undefined ? {} : options.resourceLimits);
  /** @param {import('../diagnostics/diagnostic-codes.js').DiagnosticCode} code @returns {import('../diagnostics/conversion-result.js').PartialConversionResult<string>} */
  function failure(code) {
    return {value: '', diagnostics: [{scope: 'input', code, severity: 'error', fidelity: 'unsupported',
      message: 'GFM conversion exceeded a resource limit; no partial target was emitted.'}], unsupportedSourceNodes: [],
    coverage: {direction: 'gfm-to-steam', registryVersion: steamRegistryVersion, profile: 'workshop-item',
      constructs: [], contextOnlyPolicies: [], sourceNodes: []}};
  }
  if (Buffer.byteLength(source, 'utf8') > limits.maxInputBytes) return failure('GFM_MAX_INPUT_BYTES_EXCEEDED');
  // The input contract is GFM syntax, independent of GitHub's optional second
  // autolinking pass. Use the native extension's public syntax composition.
  const syntaxExtensions = gfmFromMarkdown().map(({transforms: _rendererTransforms, ...syntax}) => syntax);
  const tree = fromMarkdown(source, {extensions: [gfm()], mdastExtensions: syntaxExtensions});
  /** @type {{node: import('mdast').Nodes, depth: number}[]} */
  const pending = [{node: tree, depth: 0}];
  let nodeCount = 0;
  while (pending.length) {
    const entry = pending.pop();
    if (!entry) break;
    if (++nodeCount > limits.maxNodeCount) return failure('GFM_MAX_NODE_COUNT_EXCEEDED');
    if (entry.depth > limits.maxNestingDepth) return failure('GFM_MAX_NESTING_DEPTH_EXCEEDED');
    if ('children' in entry.node) {
      for (const child of entry.node.children) pending.push({node: child, depth: entry.depth + 1});
    }
  }
  /** @type {import('../diagnostics/conversion-result.js').UnsupportedGfmFeatureDiagnostic[]} */
  const unsupportedSourceNodes = [];
  /** @type {import('../diagnostics/conversion-result.js').GfmNodeConversionOutcome[]} */
  const sourceNodes = [];

  /** @param {import('mdast').Nodes} node @param {import('../diagnostics/conversion-result.js').ConversionFidelity} fidelity */
  function record(node, fidelity) {
    sourceNodes.push({nodeType: node.type, fidelity, ...(node.position ? {sourceSpan: node.position} : {})});
  }

  /** @param {import('mdast').Nodes} node @returns {string} */
  function preserve(node) {
    unsupportedSourceNodes.push({scope: 'gfm-node', nodeType: node.type, fidelity: 'unsupported', severity: 'warning',
      code: 'GFM_NODE_UNSUPPORTED_PRESERVED', message: `GFM ${node.type} is outside the supported Steam subset; its source is retained as literal text.`,
      ...(node.position ? {sourceSpan: node.position} : {})});
    const pending = [node];
    while (pending.length) {
      const current = pending.pop();
      if (!current) break;
      record(current, 'unsupported');
      if ('children' in current) pending.push(...current.children);
    }
    const start = node.position?.start.offset;
    const end = node.position?.end.offset;
    if (start === undefined || end === undefined) throw new Error('Native GFM syntax node is missing source offsets.');
    return encodeSteamLiteral(source.slice(start, end));
  }

  /** @param {import('mdast').Nodes} node @returns {string} */
  function renderNode(node) {
    switch (node.type) {
      case 'root': record(node, 'equivalent'); return node.children.map(render).join('\n\n');
      case 'paragraph': record(node, 'equivalent'); return node.children.map(render).join('');
      case 'text': record(node, 'equivalent'); return encodeSteamLiteral(node.value);
      case 'heading': {
        if (node.depth > 3) return preserve(node);
        record(node, 'equivalent');
        return `[h${node.depth}]${node.children.map(render).join('')}[/h${node.depth}]`;
      }
      case 'strong': case 'emphasis': case 'delete': {
        record(node, 'equivalent');
        const tag = node.type === 'strong' ? 'b' : node.type === 'emphasis' ? 'i' : 'strike';
        return `[${tag}]${node.children.map(render).join('')}[/${tag}]`;
      }
      case 'blockquote':
        record(node, 'equivalent'); return `[quote]${node.children.map(render).join('\n\n')}[/quote]`;
      case 'thematicBreak': record(node, 'equivalent'); return '[hr][/hr]';
      case 'list': {
        if ((node.ordered && node.start != null && node.start !== 1) || node.children.some(item => item.checked != null)) return preserve(node);
        record(node, 'equivalent');
        const tag = node.ordered ? 'olist' : 'list';
        return `[${tag}]${node.children.map(render).join('')}[/${tag}]`;
      }
      case 'listItem': record(node, 'equivalent'); return '[*]' + node.children.map(render).join('\n\n');
      case 'code':
        if (node.lang || node.meta || /\[\/code\s*\]/iu.test(node.value)) return preserve(node);
        record(node, 'equivalent'); return `[code]${node.value}[/code]`;
      case 'link': case 'image': {
        // Destinations requiring Steam delimiter escaping have no qualified
        // encoding here. Retain the complete source instead of normalizing it
        // into an unverified URL or allowing it to inject a target tag.
        // Relative source destinations depend on a document base unavailable
        // in Steam. WHATWG URL owns the absolute-reference distinction.
        if (!URL.canParse(node.url) || !isAllowedResourceUrl(node.url, node.type) || /[\[\]"']/u.test(node.url) || node.title
            || (node.type === 'image' && node.alt)) return preserve(node);
        record(node, 'equivalent');
        return node.type === 'link' ? `[url="${node.url}"]${node.children.map(render).join('')}[/url]` : `[img]${node.url}[/img]`;
      }
      case 'table':
        if (node.align?.some(align => align !== null)
            || node.children.some(row => row.children.length !== node.children[0]?.children.length)) return preserve(node);
        record(node, 'equivalent');
        return '[table]' + node.children.map((row, index) => {
          record(row, 'equivalent');
          const tag = index === 0 ? 'th' : 'td';
          return '[tr]' + row.children.map(cell => {
            record(cell, 'equivalent');
            return `[${tag}]${cell.children.map(render).join('')}[/${tag}]`;
          }).join('') + '[/tr]';
        }).join('') + '[/table]';
      default: return preserve(node);
    }
  }
  const outputLimitExceeded = Symbol('outputLimitExceeded');
  /** @param {import('mdast').Nodes} node @returns {string} */
  function render(node) {
    const value = renderNode(node);
    if (Buffer.byteLength(value, 'utf8') > limits.maxOutputBytes) throw outputLimitExceeded;
    return value;
  }
  /** @type {string} */
  let value;
  try {
    value = render(tree);
  } catch (error) {
    if (error === outputLimitExceeded) return failure('GFM_MAX_OUTPUT_BYTES_EXCEEDED');
    throw error;
  }
  return {value, diagnostics: unsupportedSourceNodes, unsupportedSourceNodes, coverage: {
    direction: 'gfm-to-steam', registryVersion: steamRegistryVersion, profile: 'workshop-item',
    constructs: [], contextOnlyPolicies: [], sourceNodes,
  }};
}
