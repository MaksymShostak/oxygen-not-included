// SPDX-License-Identifier: AGPL-3.0-only
// Adapted from Hadden-Industries/owlapi scripts/qualify-public-registry.mjs
// at 2ac41c94e6630ca47ce110a484ec9af3b0b1f335.
// Changes (2026-09-09): converter consumers, configurable channel, bounded native
// fetches and an exact installed-integrity check; no sole-next/latest policy.
import assert from 'node:assert/strict';
import {spawnSync} from 'node:child_process';
import {mkdirSync, writeFileSync} from 'node:fs';
import {join, resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {parseArgs} from 'node:util';
import {assertPublicRegistryFacts, assertPublicRegistryMetadata, readCandidate, registry, sha256Buffer, verifyIntegrity} from './release-artifacts.js';

const root = fileURLToPath(new URL('../', import.meta.url));
const npmCli = process.env['npm_execpath'];
assert.ok(npmCli, 'Run npm run release:verify-registry.');
const {values} = parseArgs({options: {candidate: {type: 'string'}, tag: {type: 'string'}, output: {type: 'string'}}});
assert.ok(values.candidate && values.tag, 'Registry qualification requires --candidate and --tag.');
const candidate = readCandidate(resolve(values.candidate));
assert.equal(candidate.package.private, false, 'A private development candidate cannot have public registry qualification.');
const {name, version} = candidate.package;
const output = values.output ? resolve(values.output) : join(resolve(values.candidate), 'registry-verification');
mkdirSync(output, {recursive: true});

/** @param {string} url */
async function request(url) {
  const response = await fetch(url, {redirect: 'error', signal: AbortSignal.timeout(30000),
    headers: {'Cache-Control': 'no-cache'}});
  assert.ok(response.ok, `Fresh public registry read returned HTTP ${response.status}.`);
  return response;
}

const metadata = await (await request(`${registry}${name}/${encodeURIComponent(version)}`)).json();
const distTags = await (await request(`${registry}-/package/${name}/dist-tags`)).json();
// Validate the destination and metadata before requesting any archive URL.
const distribution = assertPublicRegistryMetadata({expectedName: name, expectedVersion: version, tag: values.tag, metadata, distTags});
const archive = Buffer.from(await (await request(distribution.tarballUrl)).arrayBuffer());
verifyIntegrity(archive, distribution.integrity);
const facts = assertPublicRegistryFacts({expectedName: name, expectedVersion: version, tag: values.tag, retainedSha256: candidate.tarball.sha256,
  registryTarballSha256: sha256Buffer(archive), metadata, distTags});
assert.equal(distribution.integrity, candidate.tarball.integrity);
writeFileSync(join(output, 'registry-metadata.json'), JSON.stringify({metadata, distTags}, null, 2) + '\n');
const result = spawnSync(process.execPath, [npmCli, 'run', 'test:package', '--', '--coordinate', facts.coordinate,
  '--integrity', facts.integrity, '--output', output, '--audit'], {cwd: root, encoding: 'utf8', windowsHide: true, maxBuffer: 32 * 1024 * 1024});
assert.equal(result.status, 0, result.error?.message ?? result.stdout + result.stderr);
console.log(result.stdout);
const report = {result: 'PASS', verifiedAt: new Date().toISOString(), registry, ...facts,
  installedConsumer: 'PASS', signatureAudit: 'PASS'};
writeFileSync(join(output, 'registry-verification.json'), JSON.stringify(report, null, 2) + '\n');
console.log(JSON.stringify(report, null, 2));
