// SPDX-License-Identifier: AGPL-3.0-only
// Public contracts are generated from checked JavaScript and the source registry.
/** @typedef {import('./security/resource-limits.js').SteamParseResourceLimits} SteamParseResourceLimits */
/** @typedef {import('unist').Position} SourceSpan Source coordinates use the unist contract. */
/** @typedef {import('./steam/registry-identifiers.js').SteamDialectProfileId} SteamDialectProfileId */
/** @typedef {import('./steam/registry-identifiers.js').ConstructId} ConstructId */
/** @typedef {import('./steam/parse-steam-bbcode.js').ParseSteamCommunityBbcodeOptions} ParseSteamCommunityBbcodeOptions */
/** @typedef {import('./steam/parse-steam-bbcode.js').SteamBbcodeParseResult} SteamBbcodeParseResult */
/** @typedef {import('./steam/steam-bbcode-syntax.js').SteamBbcodeSyntaxNode} SteamBbcodeSyntaxNode */
export {parseSteamCommunityBbcode} from './steam/parse-steam-bbcode.js';
/** @typedef {import('./mdast/steam-mdast-nodes.js').SteamMdastRoot} SteamMdastRoot */
/** @typedef {import('./mdast/steam-mdast-nodes.js').SteamUnderline} SteamUnderline */
/** @typedef {import('./mdast/steam-mdast-nodes.js').SteamSpoiler} SteamSpoiler */
/** @typedef {import('./mdast/steam-mdast-nodes.js').SteamNoParse} SteamNoParse */
/** @typedef {import('./mdast/steam-mdast-nodes.js').SteamColor} SteamColor */
/** @typedef {import('./mdast/steam-mdast-nodes.js').SteamBlockSpoiler} SteamBlockSpoiler */
/** @typedef {import('./mdast/steam-mdast-nodes.js').SteamAttributedBlockquote} SteamAttributedBlockquote */
/** @typedef {import('./mdast/steam-mdast-nodes.js').SteamPullQuote} SteamPullQuote */
/** @typedef {import('./mdast/steam-mdast-nodes.js').SteamEmbeddedMedia} SteamEmbeddedMedia */
/** @typedef {import('./mdast/steam-mdast-nodes.js').SteamPreviewImage} SteamPreviewImage */
/** @template T @typedef {import('./diagnostics/conversion-result.js').ConversionResult<T>} ConversionResult */
/** @typedef {import('./diagnostics/conversion-result.js').ConversionDiagnostic} ConversionDiagnostic */
/** @typedef {import('./diagnostics/diagnostic-codes.js').DiagnosticCode} DiagnosticCode */
/** @typedef {import('./diagnostics/conversion-result.js').ConversionFidelity} ConversionFidelity */
/** @typedef {import('./mdast/steam-bbcode-to-mdast.js').SteamBbcodeToMdastOptions} SteamBbcodeToMdastOptions */
export {steamCommunityBbcodeToMdast} from './mdast/steam-bbcode-to-mdast.js';
/** @typedef {import('./gfm/steam-bbcode-to-gfm.js').SteamBbcodeToGfmOptions} SteamBbcodeToGfmOptions */
export {steamCommunityBbcodeToGfm} from './gfm/steam-bbcode-to-gfm.js';
/** @template T @typedef {import('./diagnostics/conversion-result.js').PartialConversionResult<T>} PartialConversionResult */
/** @typedef {import('./diagnostics/conversion-result.js').UnsupportedGfmFeatureDiagnostic} UnsupportedGfmFeatureDiagnostic */
export {gfmToSteamCommunityBbcode} from './steam/gfm-to-steam-bbcode.js';
/** @typedef {import('./steam/gfm-to-steam-bbcode.js').GfmToSteamBbcodeOptions} GfmToSteamBbcodeOptions */
/** @typedef {import('./security/gfm-resource-limits.js').GfmConversionResourceLimits} GfmConversionResourceLimits */
