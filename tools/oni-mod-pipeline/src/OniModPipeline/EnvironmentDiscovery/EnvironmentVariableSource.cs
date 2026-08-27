namespace MaksymShostak.OniModPipeline.EnvironmentDiscovery;

internal sealed class EnvironmentVariableSource
{
    internal const string GameDirectoryVariable = "ONI_GAME_DIRECTORY";
    internal const string UserDataDirectoryVariable = "ONI_USER_DATA_DIRECTORY";
    internal const string ArtifactsDirectoryVariable =
        "ONI_MOD_PIPELINE_ARTIFACTS_DIRECTORY";

    private readonly Func<string, string?> readVariable;

    internal EnvironmentVariableSource()
        : this(Environment.GetEnvironmentVariable)
    {
    }

    internal EnvironmentVariableSource(IReadOnlyDictionary<string, string?> values)
        : this(name => values.TryGetValue(name, out var value) ? value : null)
    {
        ArgumentNullException.ThrowIfNull(values);
    }

    private EnvironmentVariableSource(Func<string, string?> readVariable)
    {
        this.readVariable = readVariable;
    }

    internal string? Get(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        var value = readVariable(name);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}
