using MaksymShostak.OniModPipeline.ContentIntegrity;

namespace MaksymShostak.OniModPipeline.ModBuild;

internal sealed record AssemblyVersionInfo(
    string AssemblyVersion,
    string? FileVersion,
    string? InformationalVersion);

internal sealed record BuildResult(
    string RunRoot,
    string? PrimaryOutputPath,
    IReadOnlyList<FileDigest> Inputs,
    IReadOnlyList<FileDigest> Outputs,
    IReadOnlyList<FileDigest> MergeInputs,
    IReadOnlyList<FileDigest> GameReferences,
    string SourceCommit,
    string ReleaseVersion,
    string DotnetSdkVersion,
    IReadOnlyList<string> StructuredBuildArguments,
    AssemblyVersionInfo? PrimaryAssemblyVersion,
    bool SourceBytesUnchanged,
    string? PrimaryTargetFrameworkMoniker);
