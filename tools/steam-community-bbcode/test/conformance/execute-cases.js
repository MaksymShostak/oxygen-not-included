// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {steamCommunityBbcodeToMdast, steamCommunityBbcodeToGfm} from '../../src/index.js';

/** @param {unknown} value @returns {value is unknown[]} */
function isUnknownArray(value) {
  return Array.isArray(value);
}

/**
 * Compare semantic fields while ignoring source coordinates and conventional
 * parser defaults (null, list spread, implicit start=1, unaligned table cells).
 * All content, nesting, destinations and Steam metadata remain in the comparison.
 * This projection is an assertion aid, not an AST validator or converter.
 * @param {unknown} value @returns {unknown}
 */
export function semanticProjection(value) {
  return JSON.parse(JSON.stringify(value, (key, /** @type {unknown} */ field) => {
    if (key === 'position' || key === 'spread' || field === null || (key === 'start' && field === 1)
        || (key === 'align' && isUnknownArray(field) && field.every(item => item === null))) return undefined;
    return field;
  }));
}

/** @param {unknown} tree @returns {unknown[]} */
function rootChildren(tree) {
  assert.ok(tree && typeof tree === 'object' && 'type' in tree && tree.type === 'root' && 'children' in tree && isUnknownArray(tree.children));
  return tree.children;
}

/** @param {string} markdown */
function parseGfm(markdown) {
  // Public Extension composition deliberately selects syntax compilation. The
  // package's post-parse autolinker models additional GitHub surfaces and ignores
  // escaped text. It is qualified separately; the live Markdown API preserves
  // the escapes. Do not treat that renderer pass as GFM syntax parsing.
  const syntaxExtensions = gfmFromMarkdown().map(({transforms: _rendererTransforms, ...syntax}) => syntax);
  return fromMarkdown(markdown, {extensions: [gfm()], mdastExtensions: syntaxExtensions});
}

/**
 * Execute the public pipeline and native target consumer against authored trees.
 * The same assertions drive tests and generated reports, so no record can claim
 * a pass without running the fixture. A failure prevents report generation.
 * @param {import('./construct-cases.js').ConstructCase} fixture
 * @param {import('../../src/index.js').SteamDialectProfileId} [profile]
 */
export function executeConstructCase(fixture, profile = 'workshop-item') {
  const semantic = steamCommunityBbcodeToMdast(fixture.source, {profile});
  const result = steamCommunityBbcodeToGfm(fixture.source, {profile});
  assert.equal(result.coverage.profile, profile);
  assert.deepEqual(semanticProjection(semantic.value), fixture.expectedMdast, `${fixture.id}: canonical Steam-aware MDAST`);
  assert.deepEqual(semanticProjection(parseGfm(result.value)), fixture.expectedGfm, `${fixture.id}: canonical GFM semantics`);
  assert.deepEqual(result.diagnostics.map(d => d.code), fixture.diagnostics, `${fixture.id}: diagnostic policy`);
  for (const constructId of fixture.constructIds) {
    const occurrences = result.coverage.constructs.filter(c => c.constructId === constructId);
    assert.ok(occurrences.length > 0, `${fixture.id}: no occurrence for ${constructId}`);
    assert.ok(occurrences.every(c => c.fidelity === fixture.fidelity), `${fixture.id}: target fidelity for ${constructId}`);
  }

  const nestedSource = `[quote]${fixture.source}[/quote]`;
  const quote = /** @param {unknown} tree */ tree => ({type: 'root', children: [{type: 'blockquote', children: rootChildren(tree)}]});
  assert.deepEqual(semanticProjection(steamCommunityBbcodeToMdast(nestedSource, {profile}).value), quote(fixture.expectedMdast), `${fixture.id}: nested MDAST`);
  assert.deepEqual(semanticProjection(parseGfm(steamCommunityBbcodeToGfm(nestedSource, {profile}).value)), quote(fixture.expectedGfm), `${fixture.id}: nested GFM`);

  const literal = '[b]* _ ` <script> & \\[/b]';
  const escapedSource = `[p][noparse]${literal}[/noparse][/p]${fixture.source}[p][noparse]${literal}[/noparse][/p]`;
  const literalParagraph = {type: 'paragraph', children: [{type: 'text', value: literal}]};
  const escapedTarget = {type: 'root', children: [literalParagraph, ...rootChildren(fixture.expectedGfm), literalParagraph]};
  assert.deepEqual(semanticProjection(parseGfm(steamCommunityBbcodeToGfm(escapedSource, {profile}).value)), escapedTarget, `${fixture.id}: literal/escaping context`);

  const malformed = steamCommunityBbcodeToGfm(fixture.malformed, {profile});
  assert.deepEqual(semanticProjection(parseGfm(malformed.value)), {type: 'root', children: [{type: 'paragraph', children: [{type: 'text', value: fixture.malformed}]}]}, `${fixture.id}: malformed source preserved`);
  if (fixture.malformedPolicy === 'not-recognized') {
    assert.ok(!malformed.coverage.constructs.some(c => fixture.constructIds.some(id => id === c.constructId)), `${fixture.id}: malformed lookalike must not become the construct`);
  } else assert.ok(malformed.diagnostics.length > 0, `${fixture.id}: malformed syntax needs an explanation`);
  return {caseId: fixture.id, constructIds: fixture.constructIds, profile: result.coverage.profile,
    canonical: 'pass', nested: 'pass', escaping: 'pass', malformed: 'pass', fidelity: fixture.fidelity, diagnostics: fixture.diagnostics};
}
