// SPDX-License-Identifier: AGPL-3.0-only
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {createHash} from 'node:crypto';
import {mkdir, readFile, readdir, writeFile} from 'node:fs/promises';
import {fileURLToPath} from 'node:url';
import {fromMarkdown} from 'mdast-util-from-markdown';
import {gfmFromMarkdown} from 'mdast-util-gfm';
import {gfm} from 'micromark-extension-gfm';
import {steamCommunityBbcodeToGfm, gfmToSteamCommunityBbcode} from '../src/index.js';
import {semanticProjection} from '../test/conformance/execute-cases.js';
import {comparisonCases, providerIds, applicability} from './comparison/corpus.js';
import {classifyResult} from './comparison/classify-result.js';

/** @param {string} path */
const local = path => fileURLToPath(new URL(`../${path}`, import.meta.url));
const python = fileURLToPath(new URL('../../../.venv/Scripts/python.exe', import.meta.url));
const pythonEnvironment = {...process.env, PYTHONPATH: local('artifacts/comparison/python'), PYTHONIOENCODING: 'utf-8', PYTHONNOUSERSITE: '1'};
const dotnetPackage = 'artifacts/comparison/dotnet/.store/converter.markdowntobbcodesteam.tool/1.0.0.29/converter.markdowntobbcodesteam.tool/1.0.0.29/';
const syntax = gfmFromMarkdown().map(({transforms: _rendererTransforms, ...extension}) => extension);
/** @param {string} source */
const parseGfm = source => fromMarkdown(source, {extensions: [gfm()], mdastExtensions: syntax});
/** @typedef {{output: string, diagnostics: readonly string[], stderr: string, error?: string}} Execution */

/** @param {string} command @param {string[]} args @param {string} input @param {NodeJS.ProcessEnv} [env] @returns {Execution} */
function execute(command, args, input, env = process.env) {
  const result = spawnSync(command, args, {input, encoding: 'utf8', env, cwd: local('.'), windowsHide: true,
    timeout: 30000, maxBuffer: 8 * 1024 * 1024});
  const error = result.error?.message ?? (result.status === 0 ? undefined : `Exit ${result.status}; signal ${result.signal}`);
  return {output: result.stdout ?? '', stderr: result.stderr ?? '', diagnostics: [], ...(error === undefined ? {} : {error})};
}

/** @param {import('./comparison/corpus.js').ProviderId} provider @param {import('./comparison/corpus.js').ComparisonCase} fixture @returns {Promise<Execution>} */
async function convert(provider, fixture) {
  if (provider === 'candidate') {
    const result = fixture.direction === 'forward' ? steamCommunityBbcodeToGfm(fixture.source) : gfmToSteamCommunityBbcode(fixture.source);
    return {output: result.value,
      diagnostics: result.diagnostics.filter(diagnostic => ['approximate', 'lossy', 'unsupported'].includes(diagnostic.fidelity)).map(diagnostic => diagnostic.code), stderr: ''};
  }
  if (provider === 'bbcode-to-markdown') return execute(process.execPath, [local('scripts/comparison/node-provider.js')], fixture.source);
  if (provider === 'steamify' || provider === 'steam-editor-tools') return execute(python,
    ['-P', local('comparison/python/convert.py'), `${provider}-${fixture.direction}`], fixture.source, pythonEnvironment);
  // Use an actual file: the native -i option interprets either a path or text.
  // Separate process arguments preserve literal content and avoid shell parsing.
  const input = local(`artifacts/comparison/inputs/${fixture.id}.md`);
  await writeFile(input, fixture.source);
  return execute(local('artifacts/comparison/dotnet/markdown_to_bbcodesteam.exe'), ['-i', input], '');
}

await mkdir(local('artifacts/comparison/inputs'), {recursive: true});
await mkdir(local('comparison/results'), {recursive: true});
/** @type {{provider: import('./comparison/corpus.js').ProviderId, direction: string, caseId: string, applicability: {applicable: boolean, reason: string}, status: import('./comparison/classify-result.js').ComparisonStatus, execution: Execution, actualTree: unknown, consumerDiagnostics: readonly string[]}[]} */
const results = [];
for (const provider of providerIds) {
  for (const fixture of comparisonCases) {
    const capability = applicability(provider, fixture);
    /** @type {Execution} */
    let execution = {output: '', diagnostics: [], stderr: ''};
    /** @type {unknown} */
    let actualTree;
    /** @type {readonly string[]} */
    let consumerDiagnostics = [];
    if (capability.applicable) {
      try {
        execution = await convert(provider, fixture);
        if (execution.error === undefined) {
          if (fixture.direction === 'forward') actualTree = semanticProjection(parseGfm(execution.output));
          else {
            const consumed = steamCommunityBbcodeToGfm(execution.output);
            actualTree = semanticProjection(parseGfm(consumed.value));
            consumerDiagnostics = consumed.diagnostics.map(diagnostic => diagnostic.code);
          }
        }
      } catch (error) {
        execution = {...execution, error: error instanceof Error ? error.stack ?? error.message : String(error)};
      }
    }
    const status = classifyResult({applicable: capability.applicable, source: fixture.source,
      output: execution.output, expectedTree: fixture.expectedTree, actualTree, requiresDiagnosis: fixture.requiresDiagnosis,
      diagnostics: execution.diagnostics, ...(execution.error === undefined ? {} : {error: execution.error}),
      ...(fixture.expectedOutput === undefined ? {} : {expectedOutput: fixture.expectedOutput})});
    results.push({provider, direction: fixture.direction, caseId: fixture.id, applicability: capability, status,
      execution, actualTree: actualTree ?? null, consumerDiagnostics});
  }
  process.stdout.write(`${provider}: ${JSON.stringify(countStatuses(results.filter(result => result.provider === provider)))}\n`);
}

/** @param {readonly {status: string}[]} observations */
function countStatuses(observations) {
  /** @type {Record<string, number>} */
  const counts = {};
  for (const {status} of observations) counts[status] = (counts[status] ?? 0) + 1;
  return counts;
}

// Hash authored inputs and installed native package identities. Keep full pip
// reports in ignored artifacts; they can contain host paths unrelated to results.
const inputPaths = ['package.json', 'package-lock.json', 'scripts/compare-alternatives.js',
  'comparison/node/package.json', 'comparison/node/package-lock.json', 'comparison/python/convert.py',
  'comparison/python/requirements-windows-py314.txt', 'comparison/dotnet/NuGet.Config'];
for (const directory of ['src', 'spec', 'scripts/comparison', 'test/conformance']) {
  for (const path of await readdir(local(directory), {recursive: true})) {
    if (/\.(js|json)$/u.test(path)) inputPaths.push(`${directory}/${path.replaceAll('\\', '/')}`);
  }
}
/** @type {Record<string, string>} */
const inputs = {};
for (const path of inputPaths.sort()) inputs[path] = createHash('sha256').update((await readFile(local(path), 'utf8')).replace(/\r\n?/gu, '\n')).digest('hex');
/** @param {string} path */
const sha256 = async path => createHash('sha256').update(await readFile(local(path))).digest('hex');
const nativeIdentityPaths = ['comparison/node/node_modules/bbcode-to-markdown/package.json',
  'comparison/node/node_modules/bbcode-to-markdown/LICENSE',
  'artifacts/comparison/python/steamify-2.0.1.dist-info/METADATA',
  'artifacts/comparison/python/steamify-2.0.1.dist-info/licenses/LICENSE',
  'artifacts/comparison/python/steam_editor_tools-0.5.1.dist-info/METADATA',
  'artifacts/comparison/python/steam_editor_tools-0.5.1.dist-info/licenses/LICENSE',
  `${dotnetPackage}Converter.MarkdownToBBCodeSteam.Tool.nuspec`,
  `${dotnetPackage}converter.markdowntobbcodesteam.tool.1.0.0.29.nupkg.sha512`,
  `${dotnetPackage}tools/net10.0/any/Converter.MarkdownToBBCodeSteam.Tool.runtimeconfig.json`];
/** @type {Record<string, string>} */
const nativeIdentities = {};
for (const path of nativeIdentityPaths) nativeIdentities[path] = await sha256(path);
const report = {schemaVersion: 1, license: 'AGPL-3.0-only', executedAt: new Date().toISOString(),
  scope: 'Windows x64 Python 3.14 local execution; unmodified public APIs. Selected common cases, not complete project coverage or live Steam rendering.',
  oracle: 'Authored conformance trees and Steam spellings. Native GFM syntax parser; reverse outputs use the candidate Steam consumer, whose gaps can affect external classifications. Review raw outputs before asserting a comparator defect.',
  interpretation: 'Exact/equivalent are equally successful semantics. PASS_DIAGNOSED_LOSS requires expected fallback semantics and a per-input diagnostic. FAIL statuses mean mismatch with this corpus contract, not necessarily an upstream bug. No weighted score or global superiority claim.',
  environment: {platform: process.platform, architecture: process.arch, node: process.version,
    python: execute(python, ['--version'], '', pythonEnvironment), dotnetRuntimes: execute('dotnet', ['--list-runtimes'], '')},
  versions: {candidate: '1.0.0 private development package', steamify: '2.0.1', 'bbcode-to-markdown': '1.0.3',
    'steam-editor-tools': '0.5.1', butr: '1.0.0.29'},
  inputs, canonicalLfInputsSha256: createHash('sha256').update(JSON.stringify(inputs)).digest('hex'), nativeIdentities,
  nugetPackageSha512: (await readFile(local(`${dotnetPackage}converter.markdowntobbcodesteam.tool.1.0.0.29.nupkg.sha512`), 'utf8')).trim(),
  corpus: comparisonCases, results,
  summary: providerIds.map(provider => ({provider, forward: countStatuses(results.filter(result => result.provider === provider && result.direction === 'forward')),
    reverse: countStatuses(results.filter(result => result.provider === provider && result.direction === 'reverse'))})),
  historicalSteamifyObligations: {fixture: 'test/fixtures/steamify/historical-results.json',
    sha256: await sha256('test/fixtures/steamify/historical-results.json'), descriptionChecks: 16, probeChecks: 17,
    qualification: 'Existing steamify-description-regression and steamify-probes-regression tests independently exercise all 33 obligations against the candidate. They run through npm test; archived comparator outputs are not relabelled as fresh executions here.'},
};
await writeFile(local('comparison/results/windows-x64.json'), `${JSON.stringify(report, null, 2)}\n`);
// Preserve comparative failures as data; fail the command if our own accepted
// corpus no longer passes. External failures never make this command fail.
assert.ok(results.filter(result => result.provider === 'candidate').every(result => result.status.startsWith('PASS_')),
  'Candidate no longer meets the independently authored comparison corpus; inspect retained raw results.');
