// SPDX-License-Identifier: AGPL-3.0-only
/** @typedef {'exact' | 'equivalent' | 'approximate' | 'lossy' | 'unsupported'} ConversionFidelity */
/** @typedef {import('../steam/registry-identifiers.js').ConstructId | 'steam.bbcode.unknown'} DiagnosticConstructId */
/** @typedef {Readonly<{code: import('./diagnostic-codes.js').DiagnosticCode, severity: 'info' | 'warning' | 'error', fidelity: ConversionFidelity, message: string, sourceSpan?: import('../steam/steam-bbcode-syntax.js').ReadonlySourceSpan}>} ConversionDiagnosticDetail */
/** @typedef {ConversionDiagnosticDetail & (Readonly<{scope: 'construct', constructId: DiagnosticConstructId}> | Readonly<{scope: 'input'}> | Readonly<{scope: 'gfm-node', nodeType: import('mdast').Nodes['type']}>)} ConversionDiagnostic */
/** @typedef {Readonly<{constructId: DiagnosticConstructId, fidelity: ConversionFidelity, sourceSpan: import('../steam/steam-bbcode-syntax.js').ReadonlySourceSpan}>} ConstructConversionOutcome */
/** @typedef {Readonly<{constructId: import('../steam/registry-identifiers.js').ConstructId, fidelity: 'unsupported', policy: 'preserve-source', reason: string}>} ContextOnlyRendererPolicy */
/** @typedef {Readonly<{registryVersion: string, profile: import('../steam/registry-identifiers.js').SteamDialectProfileId, constructs: readonly ConstructConversionOutcome[], contextOnlyPolicies: readonly ContextOnlyRendererPolicy[]}>} ConversionCoverage */
/**
 * A document result and its observed source outcomes. Coverage here describes
 * this input; the separate generated conformance report describes the registry.
 * Input-wide failures have scope 'input' rather than an invented construct ID.
 *
 * @template T
 * @typedef {Readonly<{value: T, diagnostics: readonly ConversionDiagnostic[], coverage: ConversionCoverage}>} ConversionResult
 */
export {};

/** @typedef {ConversionDiagnosticDetail & Readonly<{scope: 'gfm-node', nodeType: import('mdast').Nodes['type']}>} UnsupportedGfmFeatureDiagnostic */
/** @typedef {Readonly<{nodeType: import('mdast').Nodes['type'], fidelity: ConversionFidelity, sourceSpan?: import('../steam/steam-bbcode-syntax.js').ReadonlySourceSpan}>} GfmNodeConversionOutcome */
/**
 * Partial reverse result. sourceNodes describes actual GFM occurrences; it is
 * independent of the forward registry report. Unsupported or degraded source
 * features also appear in unsupportedSourceNodes, never silently disappearing.
 * @template T
 * @typedef {ConversionResult<T> & Readonly<{unsupportedSourceNodes: readonly UnsupportedGfmFeatureDiagnostic[], coverage: ConversionCoverage & Readonly<{direction: 'gfm-to-steam', sourceNodes: readonly GfmNodeConversionOutcome[]}>}>} PartialConversionResult
 */
