// SPDX-License-Identifier: AGPL-3.0-only
import {steamConstructDefinitions} from './construct-registry.js';

/** @typedef {import('./construct-registry.js').SteamConstructDefinition} SteamConstructDefinition */
/** @type {Map<string, SteamConstructDefinition>} */
const definitionsByTag = new Map();
/** @type {Map<string, SteamConstructDefinition>} */
const definitionsByRenderer = new Map();
for (const definition of steamConstructDefinitions) {
  if ('tagName' in definition) definitionsByTag.set(definition.tagName, definition);
  if ('rendererKind' in definition) definitionsByRenderer.set(definition.rendererKind, definition);
}

/** @param {string} tagName @returns {SteamConstructDefinition | undefined} */
export function findSteamTagDefinition(tagName) {
  return definitionsByTag.get(tagName);
}

/** @param {string} rendererKind @returns {SteamConstructDefinition | undefined} */
export function findSteamRendererDefinition(rendererKind) {
  return definitionsByRenderer.get(rendererKind);
}
