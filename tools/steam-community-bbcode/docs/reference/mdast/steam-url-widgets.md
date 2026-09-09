[**steam-community-bbcode**](../index.md)

***

[steam-community-bbcode](../index.md) / mdast/steam-url-widgets

# mdast/steam-url-widgets

## Type Aliases

### SteamUrlWidget

> **SteamUrlWidget** = `object`

#### Type Parameters

#### Type Declaration

##### constructId

> **constructId**: [`ConstructId`](../index-1.md#constructid)

##### kind

> **kind**: [`SteamUrlWidgetKind`](#steamurlwidgetkind-1)

***

### SteamUrlWidgetKind

> **SteamUrlWidgetKind** = `"youtube-widget"` \| `"store-widget"` \| `"ugc-widget"` \| `"inventory-widget"` \| `"vimeo-widget"` \| `"sketchfab-widget"`

#### Type Parameters

## Functions

### steamTextWithUrlWidgets()

> **steamTextWithUrlWidgets**(`node`): (`Text` \| [`SteamLink`](steam-mdast-nodes.md#steamlink))[]

Split one literal source text node into native text/link nodes, retaining
UTF-16 source spans. Opaque regions and active link labels never call this.

#### Parameters

##### node

[`SteamTextSyntax`](../steam/steam-bbcode-syntax.md#steamtextsyntax)

#### Returns

(`Text` \| [`SteamLink`](steam-mdast-nodes.md#steamlink))[]
