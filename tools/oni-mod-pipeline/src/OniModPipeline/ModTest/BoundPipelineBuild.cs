namespace MaksymShostak.OniModPipeline.ModTest;

/// <summary>
/// The exact pipeline build an automated test run is bound to inspect.
/// </summary>
/// <remarks>
/// Only a caller that produced the build in the same operation, such as release
/// preparation, may bind it. Standalone test runs never select a build.
/// </remarks>
/// <param name="BuildResultPath">The build's exact <c>build-result.json</c>.</param>
/// <param name="ArtifactsDirectory">The pipeline artifacts root containing that build.</param>
internal sealed record BoundPipelineBuild(
    string BuildResultPath,
    string ArtifactsDirectory);
