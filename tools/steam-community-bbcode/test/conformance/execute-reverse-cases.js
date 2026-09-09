// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {gfmToSteamCommunityBbcode, parseSteamCommunityBbcode, steamCommunityBbcodeToGfm} from '../../src/index.js';
import {semanticProjection} from './execute-cases.js';

/** @param {string} source */
function parseGfm(source) {
  const syntaxExtensions = gfmFromMarkdown().map(({transforms: _rendererTransforms, ...syntax}) => syntax);
  return fromMarkdown(source, {extensions: [gfm()], mdastExtensions: syntaxExtensions});
}

/**
 * Run an authored reverse fixture through the public API and actual consumers.
 * Node accounting comes from the native source tree, independently of reported
 * coverage; target syntax and semantic expectations are authored in the fixture.
 * @param {import('./reverse-cases.js').ReverseCase} fixture
 */
export function executeReverseCase(fixture) {
  /** @type {import('mdast').Nodes[]} */
  const pending = [parseGfm(fixture.source)];
  /** @type {import('mdast').Nodes[]} */
  const nodes = [];
  while (pending.length) {
    const node = pending.pop();
    if (!node) break;
    nodes.push(node);
    if ('children' in node) for (const child of node.children) pending.push(child);
  }
  for (const type of fixture.unsupported) assert.ok(nodes.some(node => node.type === type), `${fixture.id}: native source precondition ${type}`);
  const result = gfmToSteamCommunityBbcode(fixture.source);
  const syntax = parseSteamCommunityBbcode(result.value);
  assert.deepEqual(syntax.diagnostics, [], `${fixture.id}: valid target syntax`);
  assert.equal(result.coverage.direction, 'gfm-to-steam');
  assert.deepEqual(result.coverage.constructs, []);
  assert.deepEqual(result.diagnostics, result.unsupportedSourceNodes);
  assert.deepEqual(result.unsupportedSourceNodes.map(item => item.nodeType).sort(), [...fixture.unsupported].sort());
  for (const diagnostic of result.unsupportedSourceNodes) {
    assert.equal(diagnostic.code, 'GFM_NODE_UNSUPPORTED_PRESERVED');
    assert.equal(diagnostic.fidelity, 'unsupported');
    assert.ok(diagnostic.sourceSpan);
  }
  assert.deepEqual(result.coverage.sourceNodes.map(node => JSON.stringify([node.nodeType, node.sourceSpan])).sort(),
    nodes.map(node => JSON.stringify([node.type, node.position])).sort(), `${fixture.id}: every native source node accounted for once`);
  if (fixture.unsupported.length) {
    const literal = syntax.children.map(node => {
      assert.ok(node.type === 'steamText' || (node.type === 'steamOpaqueTag' && node.tagName === 'noparse'), `${fixture.id}: no active markup in preserved source`);
      return node.value;
    }).join('');
    assert.equal(literal, fixture.source, `${fixture.id}: complete literal source preserved`);
  } else {
    assert.ok(fixture.expectedSteam !== undefined && fixture.expectedGfm);
    assert.equal(result.value, fixture.expectedSteam, `${fixture.id}: authored target syntax`);
    assert.deepEqual(semanticProjection(parseGfm(steamCommunityBbcodeToGfm(result.value).value)), fixture.expectedGfm, `${fixture.id}: authored semantic tree`);
    assert.ok(result.coverage.sourceNodes.every(node => node.fidelity === 'equivalent'));
  }
  return {caseId: fixture.id, policy: fixture.policy, status: 'pass',
    fidelity: fixture.unsupported.length ? 'unsupported' : 'equivalent',
    sourceNodeTypes: [...new Set(nodes.map(node => node.type))].sort(),
    unsupportedSourceNodeTypes: fixture.unsupported, sourceNodeCount: nodes.length};
}
