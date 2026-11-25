using System.Reflection;
using System.Runtime.Loader;

namespace DeliveryTemperatureLimit.Tests.OniModPipelineIntegration;

/// <summary>Loads the exact merged build and its installed game types for managed contract tests.</summary>
internal sealed class InstalledModAssemblyContext : AssemblyLoadContext, IDisposable
{
    private readonly string managed = Environment.GetEnvironmentVariable("ONI_MANAGED_ASSEMBLY_DIRECTORY")!;
    internal Assembly Candidate { get; }
    internal InstalledModAssemblyContext() : this(FindBuild()) { }

    internal InstalledModAssemblyContext(string path) : base("InstalledModContracts", isCollectible: true) =>
        Candidate = LoadFromAssemblyPath(path);

    private static string FindBuild()
    {
        var builds = PipelineProvenanceBoundAssemblyLocator.CreateForCurrentPipelineEnvironment().ProbeExactPipelineBuildDataRows();
        if (builds.Count == 0) Skip.Test("This installed contract needs the pipeline build result.");
        return builds.Single().AssemblyPath;
    }

    protected override Assembly? Load(AssemblyName name)
    {
        if (name.Name is null or "mscorlib" or "netstandard" or "System" || name.Name.StartsWith("System.", StringComparison.Ordinal))
            return null;
        var path = Path.Combine(managed, name.Name + ".dll");
        return File.Exists(path) ? LoadFromAssemblyPath(path) : null;
    }

    public void Dispose() => Unload();
}
