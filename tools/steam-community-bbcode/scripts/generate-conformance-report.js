// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {readFile, readdir, writeFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {parseArgs} from 'node:util';
import {toMarkdown} from 'mdast-util-to-markdown';
import {gfmToMarkdown} from 'mdast-util-gfm';
import {constructCases} from '../test/conformance/construct-cases.js';
import {executeConstructCase} from '../test/conformance/execute-cases.js';
import {reverseCases} from '../test/conformance/reverse-cases.js';
import {executeReverseCase} from '../test/conformance/execute-reverse-cases.js';
import {steamConstructDefinitions, steamRegistryVersion} from '../src/steam/construct-registry.js';
import {steamDialectProfileIds} from '../src/steam/registry-identifiers.js';
import {steamCommunityBbcodeToGfm} from '../src/index.js';
import {readSpecificationDocuments, validateSpecification} from './validate-specification.js';

const {values} = parseArgs({options: {check: {type: 'boolean', default: false}}});
validateSpecification(await readSpecificationDocuments());
const executions = steamDialectProfileIds.flatMap(profile => constructCases.map(fixture => executeConstructCase(fixture, profile)));
const reverseExecutions = reverseCases.map(executeReverseCase);
const reverseReport = {direction: 'gfm-to-steam', targetProfile: 'workshop-item',
  evidenceScope: 'Partial GFM syntax subset, independently executed against authored Steam syntax and semantic expectations; not live Steam rendering.',
  executedCaseCount: reverseExecutions.length,
  sourceNodeTypes: [...new Set(reverseExecutions.flatMap(result => result.sourceNodeTypes))].sort(),
  executions: reverseExecutions};
const contextOnly = steamConstructDefinitions.filter(d => 'observation' in d && d.observation === 'contextOnly');
const sourceIds = steamConstructDefinitions.filter(d => !contextOnly.some(c => c.id === d.id)).map(d => d.id);
assert.deepEqual(new Set(executions.flatMap(result => result.constructIds)), new Set(sourceIds), 'Registry and executed cases must have identical source-construct coverage.');

// Remote policies have no source syntax to malform. Verify their actual policy
// output across profiles and plain, formatted and opaque source contexts.
for (const profile of steamDialectProfileIds) {
  for (const source of ['Plain text', '[b]A[/b]B', '[noparse][b]A[/b][/noparse]']) {
    const result = steamCommunityBbcodeToGfm(source, {profile});
    for (const definition of contextOnly) {
      const policy = result.coverage.contextOnlyPolicies.find(p => p.constructId === definition.id);
      assert.ok(policy && policy.policy === 'preserve-source' && policy.fidelity === 'unsupported' && policy.reason);
      assert.ok(!result.coverage.constructs.some(c => c.constructId === definition.id));
    }
  }
}

const rows = steamConstructDefinitions.map(definition => {
  const cases = executions.filter(result => result.constructIds.some(id => id === definition.id));
  return {constructId: definition.id, syntaxKind: definition.syntaxKind, profiles: definition.profiles,
    provenance: definition.provenance, declaredMapping: definition.gfmMapping,
    ...('notes' in definition ? {notes: definition.notes} : {}),
    ...('observation' in definition && definition.observation === 'contextOnly'
      ? {observation: 'contextOnly', contextReason: definition.contextReason,
        tests: {policy: 'pass', sourceSyntax: 'not-applicable', executedContexts: 3 * steamDialectProfileIds.length}, observedFidelities: ['unsupported']}
      : {observation: 'sourceOccurrence', tests: {canonical: 'pass', nested: 'pass', escaping: 'pass', malformed: 'pass'},
        cases: cases.map(result => ({caseId: result.caseId, profile: result.profile})),
        observedFidelities: [...new Set(cases.map(result => result.fidelity))]}),
  };
});

// Canonical LF text makes the evidence identity stable across native Windows
// and Linux checkouts. This is explicitly not an npm archive byte digest.
const files = ['package.json', 'package-lock.json', 'scripts/generate-conformance-report.js'];
for (const directory of ['src', 'spec', 'test/conformance']) {
  for (const name of await readdir(new URL(`../${directory}/`, import.meta.url), {recursive: true})) {
    if (name.endsWith('.js') || name.endsWith('.json')) files.push(`${directory}/${name.replaceAll('\\', '/')}`);
  }
}
const digest = createHash('sha256');
for (const path of files.sort()) {
  const content = (await readFile(new URL(`../${path}`, import.meta.url), 'utf8')).replace(/\r\n?/gu, '\n');
  digest.update(JSON.stringify([path, content]));
}
const report = {license: 'AGPL-3.0-only', schemaVersion: 1, registryVersion: steamRegistryVersion,
  evidenceScope: 'Executed conversion policies; Steam rendering and release readiness are separate qualifications.',
  targetContract: 'GFM syntax; additional GitHub autolinking is checked separately and diagnosed per input.',
  canonicalLfInputsSha256: digest.digest('hex'), registryConstructCount: rows.length,
  sourceConstructCount: sourceIds.length, contextOnlyConstructCount: contextOnly.length,
  executedCaseProfiles: executions.length, profileCount: steamDialectProfileIds.length, rows, executions,
  reverse: reverseReport};

/** @param {string} value @returns {import('mdast').TableCell} */
const cell = value => ({type: 'tableCell', children: [{type: 'text', value}]});
/** @type {import('mdast').Root} */
const document = {type: 'root', children: [
  {type: 'html', value: '<!-- SPDX-License-Identifier: AGPL-3.0-only; generated by scripts/generate-conformance-report.js -->'},
  {type: 'heading', depth: 1, children: [{type: 'text', value: 'Steam conversion policy matrices'}]},
  {type: 'paragraph', children: [{type: 'text', value: `${rows.length} registry entries have an executed policy: ${sourceIds.length} observable source constructs and ${contextOnly.length} context-only renderer rules. ${executions.length} case/profile executions check canonical, nested, escaping and malformed contexts. These checks do not prove current Steam rendering, support for every parameter variant, or release readiness.`}]},
  {type: 'paragraph', children: [{type: 'text', value: 'A profile status records source evidence about Steam. An absent profile is unclassified. Conversion recognizes the complete registry in each selected profile; passing conversion tests is not evidence that Steam accepts that syntax on that surface. Unsupported fidelity below denotes an explained fallback.'}]},
  {type: 'paragraph', children: [{type: 'text', value: 'Target trees are checked against GFM syntax. Some GitHub surfaces additionally autolink escaped text; the full native consumer detects additional links and emits GFM_RENDERER_AUTOLINK_POSSIBLE. The live Markdown REST endpoint is a separate retained check.'}]},
  {type: 'table', children: [
    {type: 'tableRow', children: ['Construct', 'Policy tests', 'Observed target fidelity', 'Steam profile evidence'].map(cell)},
    ...rows.map(row => ({type: /** @type {const} */ ('tableRow'), children: [row.constructId,
      row.observation === 'contextOnly' ? 'Context policy passed; source syntax not observable' : 'Canonical, nested, escaping, malformed passed',
      row.observedFidelities.join(', '), Object.entries(row.profiles).map(([profile, status]) => `${profile}: ${status}`).join('; ') || 'Unclassified'].map(cell)})),
  ]},
  {type: 'heading', depth: 2, children: [{type: 'text', value: 'Partial GFM-to-Steam'}]},
  {type: 'paragraph', children: [{type: 'text', value: `${reverseReport.executedCaseCount} independently authored reverse cases exercise ${reverseReport.sourceNodeTypes.length} native MDAST node kinds. Equivalent mappings have exact Steam syntax and semantic expectations. Unsupported cases retain literal source, emit explicit diagnostics and verify that no active Steam markup is injected. Every source node is accounted for independently against the native GFM tree. Forward registry results do not contribute to these reverse classifications.`}]},
  {type: 'paragraph', children: [{type: 'text', value: 'The target is the qualified Workshop-item subset. Table alignment, task state, heading levels 4-6, raw HTML, inline code, metadata and relative resources are preserved where no equivalent mapping is qualified. GFM reference definitions/references and hard-break source also remain literal. Code without metadata is supported only when its payload cannot close the Steam code region. Conventional list spacing is normalized; structure and paragraph separation are retained. These executions do not establish live Steam rendering or complete GFM reversibility.'}]},
  {type: 'table', children: [
    {type: 'tableRow', children: ['Case', 'Policy', 'Observed fidelity', 'Result'].map(cell)},
    ...reverseExecutions.map(result => ({type: /** @type {const} */ ('tableRow'), children: [result.caseId, result.policy, result.fidelity, result.status].map(cell)})),
  ]},
  {type: 'paragraph', children: [{type: 'text', value: 'Machine-readable provenance, exact case identities, input fingerprint and independent reverse results are in '}, {type: 'link', url: '../coverage.json', children: [{type: 'text', value: 'coverage.json'}]}, {type: 'text', value: '. Edit the registry or authored conformance fixtures, then run npm run conformance:generate. npm run conformance:check executes both directions again and rejects stale reports.'}]},
]};
/** @type {import('mdast').Root} */
const examples = {type: 'root', children: [
  {type: 'html', value: '<!-- SPDX-License-Identifier: AGPL-3.0-only; generated by scripts/generate-conformance-report.js -->'},
  {type: 'heading', depth: 1, children: [{type: 'text', value: 'Conversion semantics by construct'}]},
  {type: 'paragraph', children: [{type: 'text', value: 'These examples use the already qualified canonical corpus and the default Workshop profile. Each output is actual serialized GFM whose semantics were independently checked by that corpus. Fidelity describes the selected example, not every attribute variant or live Steam rendering. Some examples contain several constructs so their required context remains visible.'}]},
  {type: 'paragraph', children: [{type: 'text', value: 'Ordinary source punctuation is literal. Unknown syntax and unsupported source regions retain text with diagnostics. Layout whitespace adjoining converted blocks and list boundaries is normalized; opaque code/noparse and meaningful inline padding remain protected. The native serializer owns escaping and fence lengths.'}]},
  {type: 'paragraph', children: [{type: 'text', value: 'See the '}, {type: 'link', url: 'steam-support-matrix.md', children: [{type: 'text', value: 'generated support matrix'}]}, {type: 'text', value: ' for profile provenance, executed contexts and partial reverse policies.'}]},
]};
for (const definition of steamConstructDefinitions) {
  examples.children.push({type: 'heading', depth: 2, children: [{type: 'text', value: definition.id}]});
  if ('observation' in definition && definition.observation === 'contextOnly') {
    examples.children.push({type: 'paragraph', children: [{type: 'text', value: `Context-only policy: ${definition.contextReason} No source occurrence or syntax example can be inferred. The result carries an unsupported preserve-source policy without inventing occurrences.`}]});
    continue;
  }
  const fixture = constructCases.find(candidate => candidate.constructIds.includes(definition.id));
  assert.ok(fixture, `${definition.id} needs a qualified example.`);
  const result = steamCommunityBbcodeToGfm(fixture.source);
  examples.children.push(
    {type: 'paragraph', children: [{type: 'text', value: `Example policy: ${fixture.fidelity}. Steam source:`}]},
    {type: 'code', lang: 'bbcode', value: fixture.source},
    {type: 'paragraph', children: [{type: 'text', value: 'Generated GFM:'}]},
    {type: 'code', lang: 'markdown', value: result.value},
    {type: 'paragraph', children: [{type: 'text', value: result.diagnostics.length
      ? `Diagnostics: ${result.diagnostics.map(diagnostic => `${diagnostic.code}: ${diagnostic.message}`).join(' ')}`
      : 'No conversion diagnostic is emitted for this qualified example.'}]},
  );
}
for (const [path, content] of [['coverage.json', JSON.stringify(report, null, 2) + '\n'],
  ['docs/steam-support-matrix.md', toMarkdown(document, {extensions: [gfmToMarkdown()]})],
  ['docs/conversion-semantics.md', toMarkdown(examples, {fences: true})]]) {
  assert.ok(path && content);
  const target = new URL(`../${path}`, import.meta.url);
  if (values.check) assert.equal(await readFile(target, 'utf8'), content, `${path} is stale; run npm run conformance:generate.`);
  else await writeFile(target, content, 'utf8');
}
console.log(`${rows.length} registry policies accounted for by ${executions.length} source case/profile executions and ${contextOnly.length} separately checked context-only policies.`);
console.log(`${reverseReport.executedCaseCount} independent reverse cases cover ${reverseReport.sourceNodeTypes.length} native GFM node kinds with explicit subset policies.`);
