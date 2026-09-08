// SPDX-License-Identifier: AGPL-3.0-only
/**
 * Source syntax records are a lossless projection of the native grammar. They
 * describe spelling, delimiters and nesting, not a generic document model.
 * Semantic documents belong to MDAST in the next layer.
 *
 * @template T
 * @typedef {T extends object ? {readonly [K in keyof T]: ReadonlySyntaxValue<T[K]>} : T} ReadonlySyntaxValue
 */
/** @typedef {ReadonlySyntaxValue<import('unist').Position>} ReadonlySourceSpan */
/** @typedef {Readonly<{rawSource: string, sourceSpan: ReadonlySourceSpan}>} SteamSyntaxSource */
/** @typedef {SteamSyntaxSource & Readonly<{type: 'steamText', value: string}>} SteamTextSyntax */
/** @typedef {SteamSyntaxSource & Readonly<{type: 'steamListItemBoundary'}>} SteamListItemBoundarySyntax */
/** @typedef {SteamSyntaxSource & Readonly<{type: 'steamUnmatchedClosingTag', tagName: string}>} SteamUnmatchedClosingTagSyntax */
/** @typedef {SteamSyntaxSource & Readonly<{tagName: string, rawAttributes: string, headerClosed: boolean, closingTagName?: string}>} SteamTagSource */
/** @typedef {SteamTagSource & Readonly<{type: 'steamTag', children: readonly SteamBbcodeSyntaxNode[]}>} SteamTagSyntax */
/** @typedef {SteamTagSource & Readonly<{type: 'steamOpaqueTag', tagName: 'code' | 'noparse', value: string}>} SteamOpaqueTagSyntax */
/** @typedef {SteamTextSyntax | SteamListItemBoundarySyntax | SteamUnmatchedClosingTagSyntax | SteamTagSyntax | SteamOpaqueTagSyntax} SteamBbcodeSyntaxNode */
export {};
