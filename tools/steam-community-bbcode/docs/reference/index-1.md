[**steam-community-bbcode**](index.md)

***

[steam-community-bbcode](index.md) / index

# index

## Interfaces

### GfmConversionResourceLimits

#### Properties

##### maxInputBytes

> **maxInputBytes**: `number`

Maximum UTF-8 source size.

##### maxNestingDepth

> **maxNestingDepth**: `number`

Maximum native MDAST depth.

##### maxNodeCount

> **maxNodeCount**: `number`

Maximum number of native MDAST nodes.

##### maxOutputBytes

> **maxOutputBytes**: `number`

Maximum UTF-8 target size.

***

### SourceSpan

Position of a node in a source document.

A position is a range between two points.

#### Properties

##### end

> **end**: `Point`

Place of the first character after the parsed source region.

##### start

> **start**: `Point`

Place of the first character of the parsed source region.

***

### SteamParseResourceLimits

#### Properties

##### maxAttributeBytes

> **maxAttributeBytes**: `number`

Maximum UTF-8 attribute size per construct.

##### maxInputBytes

> **maxInputBytes**: `number`

Maximum UTF-8 input size.

##### maxNestingDepth

> **maxNestingDepth**: `number`

Maximum nested Steam construct depth.

##### maxNodeCount

> **maxNodeCount**: `number`

Maximum number of syntax nodes.

## Type Aliases

### ConstructId

> **ConstructId** = *typeof* [`steamConstructIds`](steam/registry-identifiers.md#steamconstructids)\[`number`\]

#### Type Parameters

***

### ConversionDiagnostic

> **ConversionDiagnostic** = [`ConversionDiagnosticDetail`](diagnostics/conversion-result.md#conversiondiagnosticdetail) & `Readonly`\<\{ `constructId`: [`DiagnosticConstructId`](diagnostics/conversion-result.md#diagnosticconstructid); `scope`: `"construct"`; \}\> \| `Readonly`\<\{ `scope`: `"input"`; \}\> \| `Readonly`\<\{ `nodeType`: `Nodes`\[`"type"`\]; `scope`: `"gfm-node"`; \}\>

#### Type Parameters

***

### ConversionFidelity

> **ConversionFidelity** = `"exact"` \| `"equivalent"` \| `"approximate"` \| `"lossy"` \| `"unsupported"`

#### Type Parameters

***

### ConversionResult

> **ConversionResult**\<`T`\> = `Readonly`\<\{ `coverage`: [`ConversionCoverage`](diagnostics/conversion-result.md#conversioncoverage); `diagnostics`: readonly [`ConversionDiagnostic`](#conversiondiagnostic)[]; `value`: `T`; \}\>

#### Type Parameters

##### T

`T`

***

### DiagnosticCode

> **DiagnosticCode** = *typeof* [`conversionDiagnosticCodes`](diagnostics/diagnostic-codes.md#conversiondiagnosticcodes)\[`number`\]

#### Type Parameters

***

### GfmToSteamBbcodeOptions

> **GfmToSteamBbcodeOptions** = `Readonly`\<\{ `resourceLimits?`: `Partial`\<[`GfmConversionResourceLimits`](#gfmconversionresourcelimits)\>; \}\>

#### Type Parameters

***

### ParseSteamCommunityBbcodeOptions

> **ParseSteamCommunityBbcodeOptions** = `Readonly`\<\{ `profile?`: [`SteamDialectProfileId`](steam/parse-steam-bbcode.md#steamdialectprofileid); `resourceLimits?`: `Partial`\<[`SteamParseResourceLimits`](#steamparseresourcelimits)\>; \}\>

#### Type Parameters

***

### PartialConversionResult

> **PartialConversionResult**\<`T`\> = [`ConversionResult`](#conversionresult)\<`T`\> & `Readonly`\<\{ `coverage`: [`ConversionCoverage`](diagnostics/conversion-result.md#conversioncoverage) & `Readonly`\<\{ `direction`: `"gfm-to-steam"`; `sourceNodes`: readonly [`GfmNodeConversionOutcome`](diagnostics/conversion-result.md#gfmnodeconversionoutcome)[]; \}\>; `unsupportedSourceNodes`: readonly [`UnsupportedGfmFeatureDiagnostic`](#unsupportedgfmfeaturediagnostic)[]; \}\>

#### Type Parameters

##### T

`T`

***

### SteamAttributedBlockquote

> **SteamAttributedBlockquote** = `Node` & `object`

#### Type Declaration

##### author

> **author**: `string`

##### children

> **children**: [`SteamFlowContent`](mdast/steam-mdast-nodes.md#steamflowcontent)[]

##### steamCommentId?

> `optional` **steamCommentId?**: `string`

##### type

> **type**: `"steamAttributedBlockquote"`

#### Type Parameters

***

### SteamBbcodeParseResult

> **SteamBbcodeParseResult** = `Readonly`\<\{ `children`: readonly [`SteamBbcodeSyntaxNode`](steam/parse-steam-bbcode.md#steambbcodesyntaxnode)[]; `diagnostics`: readonly [`SteamBbcodeParseDiagnostic`](steam/parse-steam-bbcode.md#steambbcodeparsediagnostic)[]; `profile`: [`SteamDialectProfileId`](steam/parse-steam-bbcode.md#steamdialectprofileid); `source`: `string`; \}\>

#### Type Parameters

***

### SteamBbcodeSyntaxNode

> **SteamBbcodeSyntaxNode** = [`SteamTextSyntax`](steam/steam-bbcode-syntax.md#steamtextsyntax) \| [`SteamListItemBoundarySyntax`](steam/steam-bbcode-syntax.md#steamlistitemboundarysyntax) \| [`SteamUnmatchedClosingTagSyntax`](steam/steam-bbcode-syntax.md#steamunmatchedclosingtagsyntax) \| [`SteamTagSyntax`](steam/steam-bbcode-syntax.md#steamtagsyntax) \| [`SteamOpaqueTagSyntax`](steam/steam-bbcode-syntax.md#steamopaquetagsyntax)

#### Type Parameters

***

### SteamBlockSpoiler

> **SteamBlockSpoiler** = `Node` & `object`

#### Type Declaration

##### children

> **children**: [`SteamFlowContent`](mdast/steam-mdast-nodes.md#steamflowcontent)[]

##### type

> **type**: `"steamBlockSpoiler"`

#### Type Parameters

***

### SteamColor

> **SteamColor** = `Node` & `object`

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](mdast/steam-mdast-nodes.md#steamphrasingcontent)[]

##### color?

> `optional` **color?**: `string`

##### type

> **type**: `"steamColor"`

#### Type Parameters

***

### SteamDialectProfileId

> **SteamDialectProfileId** = *typeof* [`steamDialectProfileIds`](steam/registry-identifiers.md#steamdialectprofileids)\[`number`\]

#### Type Parameters

***

### SteamEmbeddedMedia

> **SteamEmbeddedMedia** = `Node` & `object` & \{ `layout?`: `"leftthumb"` \| `"rightthumb"` \| `"full"`; `mediaKind`: `"youtube"`; \} \| \{ `autoplay?`: `boolean`; `mediaKind`: `"video"`; `poster?`: `string`; \}

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](mdast/steam-mdast-nodes.md#steamphrasingcontent)[]

##### constructId

> **constructId**: [`ConstructId`](#constructid)

##### source

> **source**: `string`

##### type

> **type**: `"steamEmbeddedMedia"`

#### Type Parameters

***

### SteamMdastRoot

> **SteamMdastRoot** = `Omit`\<`Root`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: ([`SteamFlowContent`](mdast/steam-mdast-nodes.md#steamflowcontent) \| [`SteamPhrasingContent`](mdast/steam-mdast-nodes.md#steamphrasingcontent) \| [`SteamListItem`](mdast/steam-mdast-nodes.md#steamlistitem) \| [`SteamTableRow`](mdast/steam-mdast-nodes.md#steamtablerow) \| [`SteamTableCell`](mdast/steam-mdast-nodes.md#steamtablecell) \| `FrontmatterContent`)[]

#### Type Parameters

***

### SteamNoParse

> **SteamNoParse** = `Literal` & `object`

#### Type Declaration

##### type

> **type**: `"steamNoParse"`

##### value

> **value**: `string`

#### Type Parameters

***

### SteamPreviewImage

> **SteamPreviewImage** = `Node` & `object`

#### Type Declaration

##### alignment?

> `optional` **alignment?**: `"left"` \| `"right"` \| `"inline"`

##### alt

> **alt**: `string`

##### constructId

> **constructId**: [`ConstructId`](#constructid)

##### image

> **image**: \{ `kind`: `"url"`; `steamImageId`: `string`; `url`: `string`; \} \| \{ `fileName`: `string`; `kind`: `"guideImage"`; `steamImageId`: `string`; \}

##### rawSource

> **rawSource**: `string`

##### size?

> `optional` **size?**: `"thumb"` \| `"full"` \| `"original"`

##### type

> **type**: `"steamPreviewImage"`

#### Type Parameters

***

### SteamPullQuote

> **SteamPullQuote** = `Node` & `object`

#### Type Declaration

##### children

> **children**: [`SteamFlowContent`](mdast/steam-mdast-nodes.md#steamflowcontent)[]

##### type

> **type**: `"steamPullQuote"`

#### Type Parameters

***

### SteamSpoiler

> **SteamSpoiler** = `Node` & `object`

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](mdast/steam-mdast-nodes.md#steamphrasingcontent)[]

##### type

> **type**: `"steamSpoiler"`

#### Type Parameters

***

### SteamUnderline

> **SteamUnderline** = `Node` & `object`

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](mdast/steam-mdast-nodes.md#steamphrasingcontent)[]

##### type

> **type**: `"steamUnderline"`

#### Type Parameters

***

### UnsupportedGfmFeatureDiagnostic

> **UnsupportedGfmFeatureDiagnostic** = [`ConversionDiagnosticDetail`](diagnostics/conversion-result.md#conversiondiagnosticdetail) & `Readonly`\<\{ `nodeType`: `Nodes`\[`"type"`\]; `scope`: `"gfm-node"`; \}\>

#### Type Parameters

## Functions

### gfmToSteamCommunityBbcode()

> **gfmToSteamCommunityBbcode**(`source`, `options?`): [`PartialConversionResult`](#partialconversionresult)\<`string`\>

Convert a documented subset of GFM into Steam Community BBCode.
Unsupported source semantics are reported separately from forward coverage.
This pure function does not read files, fetch URLs or render arbitrary HTML.

#### Parameters

##### source

`string`

##### options?

`Readonly`\<\{ `resourceLimits?`: `Partial`\<[`GfmConversionResourceLimits`](#gfmconversionresourcelimits)\>; \}\> = `{}`

#### Returns

[`PartialConversionResult`](#partialconversionresult)\<`string`\>

***

### steamCommunityBbcodeToGfm()

> **steamCommunityBbcodeToGfm**(`source`, `options?`): `Readonly`\<\{ `coverage`: [`ConversionCoverage`](diagnostics/conversion-result.md#conversioncoverage); `diagnostics`: readonly [`ConversionDiagnostic`](#conversiondiagnostic)[]; `value`: `string`; \}\>

Convert Steam source to GFM and retain per-input diagnostics and fidelity.
Markdown syntax and escaping belong to the maintained native serializer.

#### Parameters

##### source

`string`

##### options?

`Readonly`\<\{ `profile?`: `"workshop-item"` \| `"ugc-description"` \| `"guide-section"` \| `"discussion"` \| `"review"` \| `"announcement"`; `resourceLimits?`: `Partial`\<[`SteamParseResourceLimits`](#steamparseresourcelimits)\>; \}\> = `{}`

#### Returns

`Readonly`\<\{ `coverage`: [`ConversionCoverage`](diagnostics/conversion-result.md#conversioncoverage); `diagnostics`: readonly [`ConversionDiagnostic`](#conversiondiagnostic)[]; `value`: `string`; \}\>

***

### steamCommunityBbcodeToMdast()

> **steamCommunityBbcodeToMdast**(`source`, `options?`): `Readonly`\<\{ `coverage`: [`ConversionCoverage`](diagnostics/conversion-result.md#conversioncoverage); `diagnostics`: readonly [`ConversionDiagnostic`](#conversiondiagnostic)[]; `value`: [`SteamMdastRoot`](#steammdastroot); \}\>

Interpret Steam source as MDAST while retaining diagnostics and per-input
outcomes. This layer does not serialize Markdown or perform external I/O.
Prepared inputs must be immutable results issued by this package's parser.
Their profile cannot change, and resource limits belong to the initial parse.

#### Parameters

##### source

`string` \| `Readonly`\<\{ `children`: readonly [`SteamBbcodeSyntaxNode`](#steambbcodesyntaxnode)[]; `diagnostics`: readonly `Readonly`\<\{ `code`: `"STEAM_MAX_INPUT_BYTES_EXCEEDED"` \| `"STEAM_MAX_ATTRIBUTE_BYTES_EXCEEDED"` \| `"STEAM_MAX_NESTING_DEPTH_EXCEEDED"` \| `"STEAM_MAX_NODE_COUNT_EXCEEDED"` \| `"STEAM_LEXICAL_ERROR"` \| `"STEAM_SYNTAX_ERROR"` \| `"STEAM_UNCLOSED_TAG_HEADER"` \| `"STEAM_UNCLOSED_ATTRIBUTE_QUOTE"` \| `"STEAM_UNCLOSED_TAG"` \| `"STEAM_MISMATCHED_CLOSING_TAG"` \| `"STEAM_UNMATCHED_CLOSING_TAG"`; `message`: `string`; `sourceSpan?`: \{ `end`: \{ `column`: `number`; `line`: `number`; `offset?`: `number`; \}; `start`: \{ `column`: `number`; `line`: `number`; `offset?`: `number`; \}; \}; \}\>[]; `profile`: `"workshop-item"` \| `"ugc-description"` \| `"guide-section"` \| `"discussion"` \| `"review"` \| `"announcement"`; `source`: `string`; \}\>

##### options?

`Readonly`\<\{ `profile?`: `"workshop-item"` \| `"ugc-description"` \| `"guide-section"` \| `"discussion"` \| `"review"` \| `"announcement"`; `resourceLimits?`: `Partial`\<[`SteamParseResourceLimits`](#steamparseresourcelimits)\>; \}\> = `{}`

#### Returns

`Readonly`\<\{ `coverage`: [`ConversionCoverage`](diagnostics/conversion-result.md#conversioncoverage); `diagnostics`: readonly [`ConversionDiagnostic`](#conversiondiagnostic)[]; `value`: [`SteamMdastRoot`](#steammdastroot); \}\>

## References

### parseSteamCommunityBbcode

Re-exports [parseSteamCommunityBbcode](steam/parse-steam-bbcode.md#parsesteamcommunitybbcode)

***

### SteamBbcodeToGfmOptions

Renames and re-exports [ParseSteamCommunityBbcodeOptions](#parsesteamcommunitybbcodeoptions)

***

### SteamBbcodeToMdastOptions

Renames and re-exports [ParseSteamCommunityBbcodeOptions](#parsesteamcommunitybbcodeoptions)
