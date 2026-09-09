// SPDX-License-Identifier: AGPL-3.0-only
// Adapted from Hadden-Industries/owlapi scripts/release-artifacts.mjs and
// scripts/qualify-public-registry.mjs at 2ac41c94e6630ca47ce110a484ec9af3b0b1f335.
// Changes (2026-09-09): converter coordinate, explicit release channel, JSDoc,
// native npm archive consumers, and no single-prerelease/immutable-release policy.
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFileSync} from 'node:fs';
import {basename, join} from 'node:path';

export const registry = 'https://registry.npmjs.org/';

/** @param {Buffer} buffer */
export const sha256Buffer = buffer => createHash('sha256').update(buffer).digest('hex');
/** @param {string} path */
export const sha256File = path => sha256Buffer(readFileSync(path));
/** @param {string} left @param {string} right */
const compareCodeUnits = (left, right) => left < right ? -1 : left > right ? 1 : 0;
/** @param {{fileName: string, sha256: string}[]} entries */
export const formatSha256Sums = entries =>
  `${[...entries].sort((left, right) => compareCodeUnits(left.fileName, right.fileName))
    .map(({fileName, sha256}) => `${sha256}  ${fileName}`).join('\n')}\n`;

/** @param {unknown} value @returns {asserts value is Record<string, unknown>} */
function assertRecord(value) {
  assert.ok(value !== null && typeof value === 'object' && !Array.isArray(value), 'Expected a registry JSON object.');
}

/** @param {{expectedName: string, expectedVersion: string, tag: string, metadata: unknown, distTags: unknown}} facts */
export function assertPublicRegistryMetadata({expectedName, expectedVersion, tag, metadata, distTags}) {
  assertRecord(metadata);
  assertRecord(distTags);
  if (metadata['name'] !== expectedName || metadata['version'] !== expectedVersion) {
    throw new Error('Public registry metadata has the wrong package coordinate.');
  }
  if (distTags[tag] !== expectedVersion) {
    throw new Error('The requested distribution tag does not identify the expected version.');
  }
  const dist = metadata['dist'];
  assertRecord(dist);
  const integrity = dist['integrity'];
  const tarballUrl = dist['tarball'];
  if (typeof integrity !== 'string' || !integrity || typeof tarballUrl !== 'string' || !tarballUrl.startsWith(`${registry}${expectedName}/-/`)) {
    throw new Error('Public registry distribution metadata is incomplete or has the wrong origin.');
  }
  return {coordinate: `${expectedName}@${expectedVersion}`, channel: tag, integrity, tarballUrl};
}

/**
 * @param {{expectedName: string, expectedVersion: string, tag: string,
 * retainedSha256: string, metadata: unknown, distTags: unknown,
 * registryTarballSha256: string}} facts
 */
export function assertPublicRegistryFacts({expectedName, expectedVersion, tag, retainedSha256, metadata, distTags, registryTarballSha256}) {
  const distribution = assertPublicRegistryMetadata({expectedName, expectedVersion, tag, metadata, distTags});
  if (registryTarballSha256 !== retainedSha256) {
    throw new Error('Public registry bytes differ from the retained candidate.');
  }
  return {...distribution, tarballSha256: registryTarballSha256};
}

/** @param {Buffer} buffer @param {string} integrity */
export function verifyIntegrity(buffer, integrity) {
  if (!/^sha512-[A-Za-z0-9+/]+={0,2}$/u.test(integrity)) {
    throw new Error('Registry integrity is not an exact sha512 SRI value.');
  }
  const actual = `sha512-${createHash('sha512').update(buffer).digest('base64')}`;
  if (actual !== integrity) throw new Error('Registry tarball fails its published sha512 integrity.');
}

/**
 * @typedef {{schemaVersion: 1,
 * package: {name: string, version: string, private: boolean, license: string},
 * tarball: {filename: string, sha256: string, integrity: string},
 * source: {commit: string, dirty: boolean, node: string, npm: string},
 * checks: {installedConsumers: string, productionAudit: string}}} ReleaseCandidate
 */

/** Read the retained native npm candidate and verify the archive before use. @param {string} directory */
export function readCandidate(directory) {
  /** @type {ReleaseCandidate} */
  const candidate = JSON.parse(readFileSync(join(directory, 'candidate.json'), 'utf8'));
  assert.equal(candidate.schemaVersion, 1);
  assert.equal(candidate.package.name, 'steam-community-bbcode');
  assert.equal(candidate.package.license, 'AGPL-3.0-only');
  assert.equal(typeof candidate.package.private, 'boolean');
  assert.equal(basename(candidate.tarball.filename), candidate.tarball.filename);
  assert.equal(candidate.tarball.filename, `${candidate.package.name}-${candidate.package.version}.tgz`);
  const archive = readFileSync(join(directory, candidate.tarball.filename));
  assert.equal(sha256Buffer(archive), candidate.tarball.sha256, 'Retained candidate archive bytes changed.');
  verifyIntegrity(archive, candidate.tarball.integrity);
  return candidate;
}
