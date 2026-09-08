// SPDX-License-Identifier: AGPL-3.0-only
/**
 * Preserve native node contracts and widen only the appropriate child slots.
 * Explicit content unions keep block nodes out of phrasing content and allow
 * Steam extensions to nest inside ordinary emphasis, links and table cells.
 * No global mdast augmentation changes the downstream standard-GFM contract.
 */
/** @typedef {import('mdast').Literal & {type: 'steamNoParse', value: string}} SteamNoParse */
/** @typedef {import('mdast').Node & {type: 'steamUnderline', children: SteamPhrasingContent[]}} SteamUnderline */
/** @typedef {import('mdast').Node & {type: 'steamSpoiler', children: SteamPhrasingContent[]}} SteamSpoiler */
/** @typedef {import('mdast').Node & {type: 'steamColor', color?: string, children: SteamPhrasingContent[]}} SteamColor */
/** @typedef {import('mdast').Node & {type: 'steamBlockSpoiler', children: SteamFlowContent[]}} SteamBlockSpoiler */
/** @typedef {import('mdast').Node & {type: 'steamAttributedBlockquote', author: string, steamCommentId?: string, children: SteamFlowContent[]}} SteamAttributedBlockquote */
/** @typedef {import('mdast').Node & {type: 'steamPullQuote', children: SteamFlowContent[]}} SteamPullQuote */
/**
 * A qualified source widget. The URL is a destination, not fetched content;
 * Steam-specific presentation remains explicit until the target policy runs.
 * @typedef {import('mdast').Node & {type: 'steamEmbeddedMedia', constructId: import('../steam/registry-identifiers.js').ConstructId, source: string, children: SteamPhrasingContent[]} & ({mediaKind: 'youtube', layout?: 'leftthumb' | 'rightthumb' | 'full'} | {mediaKind: 'video', poster?: string, autoplay?: boolean})} SteamEmbeddedMedia
 */
/** @typedef {Omit<import('mdast').Strong, 'children'> & {children: SteamPhrasingContent[]}} SteamStrong */
/**
 * A preview is a leaf resource like native MDAST Image. Its textual alternative
 * is not parsed as inline formatting. Guide image identifiers are not URLs.
 * @typedef {import('mdast').Node & {type: 'steamPreviewImage', constructId: import('../steam/registry-identifiers.js').ConstructId, image: {kind: 'url', url: string, steamImageId: string} | {kind: 'guideImage', steamImageId: string, fileName: string}, alt: string, rawSource: string, size?: 'thumb' | 'full' | 'original', alignment?: 'left' | 'right' | 'inline'}} SteamPreviewImage
 */
/** @typedef {Omit<import('mdast').Emphasis, 'children'> & {children: SteamPhrasingContent[]}} SteamEmphasis */
/** @typedef {Omit<import('mdast').Delete, 'children'> & {children: SteamPhrasingContent[]}} SteamDelete */
/** @typedef {Omit<import('mdast').Link, 'children' | 'data'> & {children: SteamPhrasingContent[], data?: import('mdast').LinkData & {steamUrlWidget?: import('./steam-url-widgets.js').SteamUrlWidget}}} SteamLink */
/** @typedef {Omit<import('mdast').LinkReference, 'children'> & {children: SteamPhrasingContent[]}} SteamLinkReference */
/** @typedef {Exclude<import('mdast').PhrasingContent, {children: unknown[]}> | SteamStrong | SteamEmphasis | SteamDelete | SteamLink | SteamLinkReference | SteamNoParse | SteamUnderline | SteamSpoiler | SteamColor | SteamPreviewImage} SteamPhrasingContent */
/** @typedef {Omit<import('mdast').Paragraph, 'children'> & {children: SteamPhrasingContent[]}} SteamParagraph */
/** @typedef {Omit<import('mdast').Heading, 'children'> & {children: SteamPhrasingContent[]}} SteamHeading */
/** @typedef {Omit<import('mdast').Blockquote, 'children'> & {children: SteamFlowContent[]}} SteamBlockquote */
/** @typedef {Omit<import('mdast').FootnoteDefinition, 'children'> & {children: SteamFlowContent[]}} SteamFootnoteDefinition */
/** @typedef {Omit<import('mdast').List, 'children'> & {children: SteamListItem[]}} SteamList */
/** @typedef {Omit<import('mdast').ListItem, 'children'> & {children: SteamFlowContent[]}} SteamListItem */
/** @typedef {Omit<import('mdast').Table, 'children' | 'data'> & {children: SteamTableRow[], data?: import('mdast').TableData & {steamTableLayout?: import('./steam-table-layout.js').SteamTableLayout}}} SteamTable */
/** @typedef {Omit<import('mdast').TableRow, 'children'> & {children: SteamTableCell[]}} SteamTableRow */
/** @typedef {Omit<import('mdast').TableCell, 'children'> & {children: SteamPhrasingContent[]}} SteamTableCell */
/** @typedef {Exclude<import('mdast').BlockContent | import('mdast').DefinitionContent, {children: unknown[]}> | SteamParagraph | SteamHeading | SteamBlockquote | SteamFootnoteDefinition | SteamList | SteamTable | SteamBlockSpoiler | SteamAttributedBlockquote | SteamPullQuote | SteamEmbeddedMedia} SteamFlowContent */
/** @typedef {Omit<import('mdast').Root, 'children'> & {children: (SteamFlowContent | SteamPhrasingContent | SteamListItem | SteamTableRow | SteamTableCell | import('mdast').FrontmatterContent)[]}} SteamMdastRoot */
export {};
