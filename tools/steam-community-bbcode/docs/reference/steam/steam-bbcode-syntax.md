[**steam-community-bbcode**](../index.md)

***

[steam-community-bbcode](../index.md) / steam/steam-bbcode-syntax

# steam/steam-bbcode-syntax

## Type Aliases

### ReadonlySourceSpan

> **ReadonlySourceSpan** = [`ReadonlySyntaxValue`](#readonlysyntaxvalue)\<[`SourceSpan`](../index-1.md#sourcespan)\>

#### Type Parameters

***

### ReadonlySyntaxValue

> **ReadonlySyntaxValue**\<`T`\> = `T` *extends* `object` ? `{ readonly [K in keyof T]: ReadonlySyntaxValue<T[K]> }` : `T`

#### Type Parameters

##### T

`T`

***

### SteamListItemBoundarySyntax

> **SteamListItemBoundarySyntax** = [`SteamSyntaxSource`](#steamsyntaxsource) & `Readonly`\<\{ `type`: `"steamListItemBoundary"`; \}\>

#### Type Parameters

***

### SteamOpaqueTagSyntax

> **SteamOpaqueTagSyntax** = [`SteamTagSource`](#steamtagsource) & `Readonly`\<\{ `tagName`: `"code"` \| `"noparse"`; `type`: `"steamOpaqueTag"`; `value`: `string`; \}\>

#### Type Parameters

***

### SteamSyntaxSource

> **SteamSyntaxSource** = `Readonly`\<\{ `rawSource`: `string`; `sourceSpan`: [`ReadonlySourceSpan`](#readonlysourcespan); \}\>

#### Type Parameters

***

### SteamTagSource

> **SteamTagSource** = [`SteamSyntaxSource`](#steamsyntaxsource) & `Readonly`\<\{ `closingTagName?`: `string`; `headerClosed`: `boolean`; `rawAttributes`: `string`; `tagName`: `string`; \}\>

#### Type Parameters

***

### SteamTagSyntax

> **SteamTagSyntax** = [`SteamTagSource`](#steamtagsource) & `Readonly`\<\{ `children`: readonly [`SteamBbcodeSyntaxNode`](../index-1.md#steambbcodesyntaxnode)[]; `type`: `"steamTag"`; \}\>

#### Type Parameters

***

### SteamTextSyntax

> **SteamTextSyntax** = [`SteamSyntaxSource`](#steamsyntaxsource) & `Readonly`\<\{ `type`: `"steamText"`; `value`: `string`; \}\>

#### Type Parameters

***

### SteamUnmatchedClosingTagSyntax

> **SteamUnmatchedClosingTagSyntax** = [`SteamSyntaxSource`](#steamsyntaxsource) & `Readonly`\<\{ `tagName`: `string`; `type`: `"steamUnmatchedClosingTag"`; \}\>

#### Type Parameters

## References

### SteamBbcodeSyntaxNode

Re-exports [SteamBbcodeSyntaxNode](../index-1.md#steambbcodesyntaxnode)
