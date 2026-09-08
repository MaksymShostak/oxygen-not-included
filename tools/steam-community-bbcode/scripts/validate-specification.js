// SPDX-License-Identifier: AGPL-3.0-only
import {readFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import {pathToFileURL} from 'node:url';
import {Ajv} from 'ajv';
import formats from 'ajv-formats';

/** @typedef {{registryVersion: string, sources: Array<{id: string}>}} SourceCollection */
/** @typedef {{registryVersion: string, defaultProfile: string, profiles: Array<{id: string}>}} ProfileCollection */
/** @typedef {{registryVersion: string, constructs: Array<{id: string, provenance: Array<{sourceId: string}>, profiles: Record<string, unknown>}>}} ConstructCollection */
/** @typedef {{sources: unknown, profiles: unknown, constructs: unknown}} SpecificationDocuments */

const ajv = new Ajv({strict: true, allErrors: true});
formats.default(ajv, {mode: 'full'});
for (const name of ['common', 'sources', 'profiles', 'constructs']) {
  // Ajv compiles and validates the schema itself. JSON.parse owns JSON syntax.
  /** @type {import('ajv').AnySchemaObject} */
  const schema = JSON.parse(await readFile(new URL(`../spec/schema/${name}.schema.json`, import.meta.url), 'utf8'));
  ajv.addSchema(schema);
}

/**
 * Validate maintainer-owned registry documents with native JSON Schema.
 * This is build tooling and does not attest Steam rendering or conversion.
 *
 * @param {SpecificationDocuments} documents
 */
export function validateSpecification(documents) {
  /** @type {import('ajv').ValidateFunction<SourceCollection> | undefined} */
  const validateSources = ajv.getSchema('https://steam-community-bbcode.invalid/schema/sources.schema.json');
  /** @type {import('ajv').ValidateFunction<ProfileCollection> | undefined} */
  const validateProfiles = ajv.getSchema('https://steam-community-bbcode.invalid/schema/profiles.schema.json');
  /** @type {import('ajv').ValidateFunction<ConstructCollection> | undefined} */
  const validateConstructs = ajv.getSchema('https://steam-community-bbcode.invalid/schema/constructs.schema.json');
  if (!validateSources || !validateProfiles || !validateConstructs) throw new Error('Missing specification schema.');
  if (!validateSources(documents.sources)) throw new Error(`sources: ${ajv.errorsText(validateSources.errors)}`);
  if (!validateProfiles(documents.profiles)) throw new Error(`profiles: ${ajv.errorsText(validateProfiles.errors)}`);
  if (!validateConstructs(documents.constructs)) throw new Error(`constructs: ${ajv.errorsText(validateConstructs.errors)}`);
  const {sources, profiles, constructs} = documents;
  if (sources.registryVersion !== profiles.registryVersion || sources.registryVersion !== constructs.registryVersion) {
    throw new Error('Registry versions differ.');
  }
  // Cross-file identities and references are domain constraints, not a second
  // implementation of JSON syntax, schema shape, URI or calendar validation.
  const sourceIds = uniqueIdentities(sources.sources, 'source');
  const profileIds = uniqueIdentities(profiles.profiles, 'profile');
  uniqueIdentities(constructs.constructs, 'construct');
  if (!profileIds.has(profiles.defaultProfile)) throw new Error(`Unknown default profile: ${profiles.defaultProfile}`);
  for (const construct of constructs.constructs) {
    for (const provenance of construct.provenance) {
      if (!sourceIds.has(provenance.sourceId)) throw new Error(`Unknown source: ${provenance.sourceId}`);
    }
    for (const profileId of Object.keys(construct.profiles)) {
      if (!profileIds.has(profileId)) throw new Error(`Unknown construct profile: ${profileId}`);
    }
  }
  return {sources, profiles, constructs};
}

/** @param {Array<{id: string}>} entries @param {string} kind */
function uniqueIdentities(entries, kind) {
  /** @type {Set<string>} */
  const identities = new Set();
  for (const entry of entries) {
    if (identities.has(entry.id)) throw new Error(`Duplicate ${kind} identity: ${entry.id}`);
    identities.add(entry.id);
  }
  return identities;
}

/** @returns {Promise<SpecificationDocuments>} */
export async function readSpecificationDocuments() {
  const [sources, profiles, constructs] = await Promise.all(['sources', 'profiles', 'constructs'].map(async name => {
    /** @type {unknown} */
    const document = JSON.parse(await readFile(new URL(`../spec/${name}.json`, import.meta.url), 'utf8'));
    return document;
  }));
  return {sources, profiles, constructs};
}

if (process.argv[1] && import.meta.url === pathToFileURL(resolve(process.argv[1])).href) {
  validateSpecification(await readSpecificationDocuments());
  console.log('Specification schemas and references validated; conformance is not assessed.');
}
