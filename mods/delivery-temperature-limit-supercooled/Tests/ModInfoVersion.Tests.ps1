$ErrorActionPreference = 'Stop'

$targetPath = Join-Path $PSScriptRoot '..\Source\ILRepack.targets'
$tempRoot = [System.IO.Path]::GetFullPath([System.IO.Path]::GetTempPath())
$testRoot = Join-Path $tempRoot ('delivery-temperature-limit-' + [System.Guid]::NewGuid().ToString('N'))
$sourceDirectory = Join-Path $testRoot 'Source'
$manifestPath = Join-Path $testRoot 'mod_info.yaml'
$projectPath = Join-Path $sourceDirectory 'LineEndingRegression.proj'
$copiedTargetPath = Join-Path $sourceDirectory 'ILRepack.targets'
$utf8WithoutBom = [System.Text.UTF8Encoding]::new($false)

$projectSource = @'
<Project>
    <PropertyGroup>
        <AssemblyVersion>2026.7.6</AssemblyVersion>
    </PropertyGroup>
    <Import Project="ILRepack.targets" />
</Project>
'@

$initialManifest = "supportedContent: ALL`nminimumSupportedBuild: 596100`nversion: 0.0.0`nAPIVersion: 2`n"
$expectedManifest = "supportedContent: ALL`nminimumSupportedBuild: 596100`nversion: 2026.7.6`nAPIVersion: 2`n"

try
{
    [void][System.IO.Directory]::CreateDirectory($sourceDirectory)
    Copy-Item -LiteralPath $targetPath -Destination $copiedTargetPath
    [System.IO.File]::WriteAllText($projectPath, $projectSource, $utf8WithoutBom)
    [System.IO.File]::WriteAllText($manifestPath, $initialManifest, $utf8WithoutBom)

    dotnet msbuild $projectPath -target:UpdateModInfoVersion -nologo -verbosity:quiet
    if ($LASTEXITCODE -ne 0)
    {
        throw "UpdateModInfoVersion failed with exit code $LASTEXITCODE."
    }

    [byte[]]$actualBytes = [System.IO.File]::ReadAllBytes($manifestPath)
    [byte[]]$expectedBytes = $utf8WithoutBom.GetBytes($expectedManifest)
    $carriageReturnCount = $actualBytes.Where({ $_ -eq 13 }).Count

    if ($carriageReturnCount -ne 0)
    {
        throw "Expected an LF-only manifest, but UpdateModInfoVersion wrote $carriageReturnCount CR byte(s)."
    }

    if (-not [System.Linq.Enumerable]::SequenceEqual($actualBytes, $expectedBytes))
    {
        $actualHex = [System.Convert]::ToHexString($actualBytes)
        $expectedHex = [System.Convert]::ToHexString($expectedBytes)
        throw "UpdateModInfoVersion produced unexpected bytes. Expected $expectedHex but received $actualHex."
    }

    Write-Output 'PASS: UpdateModInfoVersion updates the version and preserves LF line endings.'
}
finally
{
    $resolvedTestRoot = [System.IO.Path]::GetFullPath($testRoot)
    if ($resolvedTestRoot.StartsWith($tempRoot, [System.StringComparison]::OrdinalIgnoreCase) -and
        $resolvedTestRoot.Length -gt $tempRoot.Length -and
        [System.IO.Directory]::Exists($resolvedTestRoot))
    {
        [System.IO.Directory]::Delete($resolvedTestRoot, $true)
    }
}
