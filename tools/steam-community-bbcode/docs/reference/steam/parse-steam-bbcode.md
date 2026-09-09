[**steam-community-bbcode**](../index.md)

***

[steam-community-bbcode](../index.md) / steam/parse-steam-bbcode

# steam/parse-steam-bbcode

## Type Aliases

### SteamBbcodeParseDiagnostic

> **SteamBbcodeParseDiagnostic** = `Readonly`\<\{ `code`: [`SteamParseDiagnosticCode`](../diagnostics/diagnostic-codes.md#steamparsediagnosticcode); `message`: `string`; `sourceSpan?`: [`ReadonlySourceSpan`](steam-bbcode-syntax.md#readonlysourcespan); \}\>

#### Type Parameters

## Functions

### isIssuedSteamBbcodeParseResult()

> **isIssuedSteamBbcodeParseResult**(`value`): value is Readonly\<\{ children: readonly SteamBbcodeSyntaxNode\[\]; diagnostics: readonly Readonly\<\{ code: "STEAM\_MAX\_INPUT\_BYTES\_EXCEEDED" \| "STEAM\_MAX\_ATTRIBUTE\_BYTES\_EXCEEDED" \| "STEAM\_MAX\_NESTING\_DEPTH\_EXCEEDED" \| "STEAM\_MAX\_NODE\_COUNT\_EXCEEDED" \| "STEAM\_LEXICAL\_ERROR" \| "STEAM\_SYNTAX\_ERROR" \| "STEAM\_UNCLOSED\_TAG\_HEADER" \| "STEAM\_UNCLOSED\_ATTRIBUTE\_QUOTE" \| "STEAM\_UNCLOSED\_TAG" \| "STEAM\_MISMATCHED\_CLOSING\_TAG" \| "STEAM\_UNMATCHED\_CLOSING\_TAG"; message: string; sourceSpan?: \{ end: \{ column: number; line: number; offset?: number \}; start: \{ column: number; line: number; offset?: number \} \} \}\>\[\]; profile: "workshop-item" \| "ugc-description" \| "guide-section" \| "discussion" \| "review" \| "announcement"; source: string \}\>

Conversion reuses immutable results issued by this parser. A deserialized or
fabricated tree has not passed the parser's runtime limits and validation.

#### Parameters

##### value

`unknown`

#### Returns

value is Readonly\<\{ children: readonly SteamBbcodeSyntaxNode\[\]; diagnostics: readonly Readonly\<\{ code: "STEAM\_MAX\_INPUT\_BYTES\_EXCEEDED" \| "STEAM\_MAX\_ATTRIBUTE\_BYTES\_EXCEEDED" \| "STEAM\_MAX\_NESTING\_DEPTH\_EXCEEDED" \| "STEAM\_MAX\_NODE\_COUNT\_EXCEEDED" \| "STEAM\_LEXICAL\_ERROR" \| "STEAM\_SYNTAX\_ERROR" \| "STEAM\_UNCLOSED\_TAG\_HEADER" \| "STEAM\_UNCLOSED\_ATTRIBUTE\_QUOTE" \| "STEAM\_UNCLOSED\_TAG" \| "STEAM\_MISMATCHED\_CLOSING\_TAG" \| "STEAM\_UNMATCHED\_CLOSING\_TAG"; message: string; sourceSpan?: \{ end: \{ column: number; line: number; offset?: number \}; start: \{ column: number; line: number; offset?: number \} \} \}\>\[\]; profile: "workshop-item" \| "ugc-description" \| "guide-section" \| "discussion" \| "review" \| "announcement"; source: string \}\>

***

### parseSteamCommunityBbcode()

> **parseSteamCommunityBbcode**(`source`, `options?`): `Readonly`\<\{ `children`: readonly [`SteamBbcodeSyntaxNode`](../index-1.md#steambbcodesyntaxnode)[]; `diagnostics`: readonly `Readonly`\<\{ `code`: `"STEAM_MAX_INPUT_BYTES_EXCEEDED"` \| `"STEAM_MAX_ATTRIBUTE_BYTES_EXCEEDED"` \| `"STEAM_MAX_NESTING_DEPTH_EXCEEDED"` \| `"STEAM_MAX_NODE_COUNT_EXCEEDED"` \| `"STEAM_LEXICAL_ERROR"` \| `"STEAM_SYNTAX_ERROR"` \| `"STEAM_UNCLOSED_TAG_HEADER"` \| `"STEAM_UNCLOSED_ATTRIBUTE_QUOTE"` \| `"STEAM_UNCLOSED_TAG"` \| `"STEAM_MISMATCHED_CLOSING_TAG"` \| `"STEAM_UNMATCHED_CLOSING_TAG"`; `message`: `string`; `sourceSpan?`: \{ `end`: \{ `column`: `number`; `line`: `number`; `offset?`: `number`; \}; `start`: \{ `column`: `number`; `line`: `number`; `offset?`: `number`; \}; \}; \}\>[]; `profile`: `"workshop-item"` \| `"ugc-description"` \| `"guide-section"` \| `"discussion"` \| `"review"` \| `"announcement"`; `source`: `string`; \}\>

Parse source into readonly Steam syntax with structured diagnostics.
The default profile is the registry's Workshop-item profile. This is a syntax
operation; it does not serialize GFM or claim target representability.

#### Parameters

##### source

`string`

##### options?

`Readonly`\<\{ `profile?`: `"workshop-item"` \| `"ugc-description"` \| `"guide-section"` \| `"discussion"` \| `"review"` \| `"announcement"`; `resourceLimits?`: `Partial`\<[`SteamParseResourceLimits`](../index-1.md#steamparseresourcelimits)\>; \}\> = `{}`

#### Returns

`Readonly`\<\{ `children`: readonly [`SteamBbcodeSyntaxNode`](../index-1.md#steambbcodesyntaxnode)[]; `diagnostics`: readonly `Readonly`\<\{ `code`: `"STEAM_MAX_INPUT_BYTES_EXCEEDED"` \| `"STEAM_MAX_ATTRIBUTE_BYTES_EXCEEDED"` \| `"STEAM_MAX_NESTING_DEPTH_EXCEEDED"` \| `"STEAM_MAX_NODE_COUNT_EXCEEDED"` \| `"STEAM_LEXICAL_ERROR"` \| `"STEAM_SYNTAX_ERROR"` \| `"STEAM_UNCLOSED_TAG_HEADER"` \| `"STEAM_UNCLOSED_ATTRIBUTE_QUOTE"` \| `"STEAM_UNCLOSED_TAG"` \| `"STEAM_MISMATCHED_CLOSING_TAG"` \| `"STEAM_UNMATCHED_CLOSING_TAG"`; `message`: `string`; `sourceSpan?`: \{ `end`: \{ `column`: `number`; `line`: `number`; `offset?`: `number`; \}; `start`: \{ `column`: `number`; `line`: `number`; `offset?`: `number`; \}; \}; \}\>[]; `profile`: `"workshop-item"` \| `"ugc-description"` \| `"guide-section"` \| `"discussion"` \| `"review"` \| `"announcement"`; `source`: `string`; \}\>

## References

### ParseSteamCommunityBbcodeOptions

Re-exports [ParseSteamCommunityBbcodeOptions](../index-1.md#parsesteamcommunitybbcodeoptions)

***

### SteamBbcodeParseResult

Re-exports [SteamBbcodeParseResult](../index-1.md#steambbcodeparseresult)

***

### SteamBbcodeSyntaxNode

Re-exports [SteamBbcodeSyntaxNode](../index-1.md#steambbcodesyntaxnode)

***

### SteamDialectProfileId

Re-exports [SteamDialectProfileId](../index-1.md#steamdialectprofileid)
