[**steam-community-bbcode**](../index.md)

***

[steam-community-bbcode](../index.md) / diagnostics/conversion-result

# diagnostics/conversion-result

## Type Aliases

### ConstructConversionOutcome

> **ConstructConversionOutcome** = `Readonly`\<\{ `constructId`: [`DiagnosticConstructId`](#diagnosticconstructid); `fidelity`: [`ConversionFidelity`](../index-1.md#conversionfidelity); `sourceSpan`: [`ReadonlySourceSpan`](../steam/steam-bbcode-syntax.md#readonlysourcespan); \}\>

#### Type Parameters

***

### ContextOnlyRendererPolicy

> **ContextOnlyRendererPolicy** = `Readonly`\<\{ `constructId`: [`ConstructId`](../index-1.md#constructid); `fidelity`: `"unsupported"`; `policy`: `"preserve-source"`; `reason`: `string`; \}\>

#### Type Parameters

***

### ConversionCoverage

> **ConversionCoverage** = `Readonly`\<\{ `constructs`: readonly [`ConstructConversionOutcome`](#constructconversionoutcome)[]; `contextOnlyPolicies`: readonly [`ContextOnlyRendererPolicy`](#contextonlyrendererpolicy)[]; `profile`: [`SteamDialectProfileId`](../index-1.md#steamdialectprofileid); `registryVersion`: `string`; \}\>

#### Type Parameters

***

### ConversionDiagnosticDetail

> **ConversionDiagnosticDetail** = `Readonly`\<\{ `code`: [`DiagnosticCode`](../index-1.md#diagnosticcode); `fidelity`: [`ConversionFidelity`](../index-1.md#conversionfidelity); `message`: `string`; `severity`: `"info"` \| `"warning"` \| `"error"`; `sourceSpan?`: [`ReadonlySourceSpan`](../steam/steam-bbcode-syntax.md#readonlysourcespan); \}\>

#### Type Parameters

***

### DiagnosticConstructId

> **DiagnosticConstructId** = [`ConstructId`](../index-1.md#constructid) \| `"steam.bbcode.unknown"`

#### Type Parameters

***

### GfmNodeConversionOutcome

> **GfmNodeConversionOutcome** = `Readonly`\<\{ `fidelity`: [`ConversionFidelity`](../index-1.md#conversionfidelity); `nodeType`: `Nodes`\[`"type"`\]; `sourceSpan?`: [`ReadonlySourceSpan`](../steam/steam-bbcode-syntax.md#readonlysourcespan); \}\>

#### Type Parameters

## References

### ConversionDiagnostic

Re-exports [ConversionDiagnostic](../index-1.md#conversiondiagnostic)

***

### ConversionFidelity

Re-exports [ConversionFidelity](../index-1.md#conversionfidelity)

***

### ConversionResult

Re-exports [ConversionResult](../index-1.md#conversionresult)

***

### PartialConversionResult

Re-exports [PartialConversionResult](../index-1.md#partialconversionresult)

***

### UnsupportedGfmFeatureDiagnostic

Re-exports [UnsupportedGfmFeatureDiagnostic](../index-1.md#unsupportedgfmfeaturediagnostic)
