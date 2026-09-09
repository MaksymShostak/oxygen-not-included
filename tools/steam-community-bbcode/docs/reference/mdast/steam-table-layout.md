[**steam-community-bbcode**](../index.md)

***

[steam-community-bbcode](../index.md) / mdast/steam-table-layout

# mdast/steam-table-layout

## Type Aliases

### SteamTableLayout

> **SteamTableLayout** = `object`

#### Type Parameters

#### Type Declaration

##### equalcells?

> `optional` **equalcells?**: `boolean`

##### noborder?

> `optional` **noborder?**: `boolean`

## Functions

### steamTableLayout()

> **steamTableLayout**(`rawAttributes`): [`SteamTableLayout`](#steamtablelayout) \| `undefined`

Recognize only the two sourced Steam layout settings. Duplicate keys and
unknown values remain ambiguous; silently choosing a winner would lose source.

#### Parameters

##### rawAttributes

`string`

#### Returns

[`SteamTableLayout`](#steamtablelayout) \| `undefined`
