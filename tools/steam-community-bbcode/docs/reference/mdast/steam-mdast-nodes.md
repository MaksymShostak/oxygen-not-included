[**steam-community-bbcode**](../index.md)

***

[steam-community-bbcode](../index.md) / mdast/steam-mdast-nodes

# mdast/steam-mdast-nodes

## Type Aliases

### SteamBlockquote

> **SteamBlockquote** = `Omit`\<`Blockquote`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamFlowContent`](#steamflowcontent)[]

#### Type Parameters

***

### SteamDelete

> **SteamDelete** = `Omit`\<`Delete`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](#steamphrasingcontent)[]

#### Type Parameters

***

### SteamEmphasis

> **SteamEmphasis** = `Omit`\<`Emphasis`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](#steamphrasingcontent)[]

#### Type Parameters

***

### SteamFlowContent

> **SteamFlowContent** = `Exclude`\<`BlockContent` \| `DefinitionContent`, \{ `children`: `unknown`[]; \}\> \| [`SteamParagraph`](#steamparagraph) \| [`SteamHeading`](#steamheading) \| [`SteamBlockquote`](#steamblockquote) \| [`SteamFootnoteDefinition`](#steamfootnotedefinition) \| [`SteamList`](#steamlist) \| [`SteamTable`](#steamtable) \| [`SteamBlockSpoiler`](../index-1.md#steamblockspoiler) \| [`SteamAttributedBlockquote`](../index-1.md#steamattributedblockquote) \| [`SteamPullQuote`](../index-1.md#steampullquote) \| [`SteamEmbeddedMedia`](../index-1.md#steamembeddedmedia)

#### Type Parameters

***

### SteamFootnoteDefinition

> **SteamFootnoteDefinition** = `Omit`\<`FootnoteDefinition`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamFlowContent`](#steamflowcontent)[]

#### Type Parameters

***

### SteamHeading

> **SteamHeading** = `Omit`\<`Heading`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](#steamphrasingcontent)[]

#### Type Parameters

***

### SteamLink

> **SteamLink** = `Omit`\<`Link`, `"children"` \| `"data"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](#steamphrasingcontent)[]

##### data?

> `optional` **data?**: `LinkData` & `object`

###### Type Declaration

###### steamUrlWidget?

> `optional` **steamUrlWidget?**: [`SteamUrlWidget`](steam-url-widgets.md#steamurlwidget)

#### Type Parameters

***

### SteamLinkReference

> **SteamLinkReference** = `Omit`\<`LinkReference`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](#steamphrasingcontent)[]

#### Type Parameters

***

### SteamList

> **SteamList** = `Omit`\<`List`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamListItem`](#steamlistitem)[]

#### Type Parameters

***

### SteamListItem

> **SteamListItem** = `Omit`\<`ListItem`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamFlowContent`](#steamflowcontent)[]

#### Type Parameters

***

### SteamParagraph

> **SteamParagraph** = `Omit`\<`Paragraph`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](#steamphrasingcontent)[]

#### Type Parameters

***

### SteamPhrasingContent

> **SteamPhrasingContent** = `Exclude`\<`PhrasingContent`, \{ `children`: `unknown`[]; \}\> \| [`SteamStrong`](#steamstrong) \| [`SteamEmphasis`](#steamemphasis) \| [`SteamDelete`](#steamdelete) \| [`SteamLink`](#steamlink) \| [`SteamLinkReference`](#steamlinkreference) \| [`SteamNoParse`](../index-1.md#steamnoparse) \| [`SteamUnderline`](../index-1.md#steamunderline) \| [`SteamSpoiler`](../index-1.md#steamspoiler) \| [`SteamColor`](../index-1.md#steamcolor) \| [`SteamPreviewImage`](../index-1.md#steampreviewimage)

#### Type Parameters

***

### SteamStrong

> **SteamStrong** = `Omit`\<`Strong`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](#steamphrasingcontent)[]

#### Type Parameters

***

### SteamTable

> **SteamTable** = `Omit`\<`Table`, `"children"` \| `"data"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamTableRow`](#steamtablerow)[]

##### data?

> `optional` **data?**: `TableData` & `object`

###### Type Declaration

###### steamTableLayout?

> `optional` **steamTableLayout?**: [`SteamTableLayout`](steam-table-layout.md#steamtablelayout)

#### Type Parameters

***

### SteamTableCell

> **SteamTableCell** = `Omit`\<`TableCell`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamPhrasingContent`](#steamphrasingcontent)[]

#### Type Parameters

***

### SteamTableRow

> **SteamTableRow** = `Omit`\<`TableRow`, `"children"`\> & `object`

#### Type Declaration

##### children

> **children**: [`SteamTableCell`](#steamtablecell)[]

#### Type Parameters

## References

### SteamAttributedBlockquote

Re-exports [SteamAttributedBlockquote](../index-1.md#steamattributedblockquote)

***

### SteamBlockSpoiler

Re-exports [SteamBlockSpoiler](../index-1.md#steamblockspoiler)

***

### SteamColor

Re-exports [SteamColor](../index-1.md#steamcolor)

***

### SteamEmbeddedMedia

Re-exports [SteamEmbeddedMedia](../index-1.md#steamembeddedmedia)

***

### SteamMdastRoot

Re-exports [SteamMdastRoot](../index-1.md#steammdastroot)

***

### SteamNoParse

Re-exports [SteamNoParse](../index-1.md#steamnoparse)

***

### SteamPreviewImage

Re-exports [SteamPreviewImage](../index-1.md#steampreviewimage)

***

### SteamPullQuote

Re-exports [SteamPullQuote](../index-1.md#steampullquote)

***

### SteamSpoiler

Re-exports [SteamSpoiler](../index-1.md#steamspoiler)

***

### SteamUnderline

Re-exports [SteamUnderline](../index-1.md#steamunderline)
