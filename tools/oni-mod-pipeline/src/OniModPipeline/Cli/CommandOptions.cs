using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using System.CommandLine;

namespace MaksymShostak.OniModPipeline.Cli;

internal sealed class CommandOptions
{
    private readonly Option<string?> modOption;
    private readonly Option<string?> gameDirectoryOption = new("--game-directory")
    {
        Description = "ONI installation root containing the required managed assemblies."
    };
    private readonly Option<string?> userDataDirectoryOption = new("--user-data-directory")
    {
        Description = "Existing ONI per-user data root containing the mods directory."
    };
    private readonly Option<string?> artifactsDirectoryOption = new("--artifacts-directory")
    {
        Description = "Dedicated absolute root for pipeline-generated artifacts."
    };
    private readonly Option<string> formatOption = new("--format")
    {
        Description = "Output format: human or json.",
        DefaultValueFactory = _ => "human"
    };

    internal CommandOptions(bool defaultModToCurrentDirectory = true)
    {
        modOption = new Option<string?>("--mod")
        {
            Description =
                "Mod directory, profile path, or descendant path used to discover oni-mod-pipeline.toml."
        };
        if (defaultModToCurrentDirectory)
        {
            modOption.DefaultValueFactory = _ => Directory.GetCurrentDirectory();
        }

        formatOption.Validators.Add(result =>
        {
            var value = result.GetValueOrDefault<string>();
            if (!string.Equals(value, "human", StringComparison.Ordinal) &&
                !string.Equals(value, "json", StringComparison.Ordinal))
            {
                result.AddError("Option '--format' accepts only 'human' or 'json'.");
            }
        });
    }

    internal void AddTo(Command command)
    {
        ArgumentNullException.ThrowIfNull(command);
        command.Options.Add(modOption);
        command.Options.Add(gameDirectoryOption);
        command.Options.Add(userDataDirectoryOption);
        command.Options.Add(artifactsDirectoryOption);
        command.Options.Add(formatOption);
    }

    internal string GetModPath(ParseResult parseResult) =>
        parseResult.GetValue(modOption) ?? Directory.GetCurrentDirectory();

    internal string? GetOptionalModPath(ParseResult parseResult) =>
        parseResult.GetValue(modOption);

    internal Option<string?> ModOption => modOption;

    internal EnvironmentDiscoveryRequest GetEnvironmentRequest(ParseResult parseResult) =>
        new(
            parseResult.GetValue(gameDirectoryOption),
            parseResult.GetValue(userDataDirectoryOption),
            parseResult.GetValue(artifactsDirectoryOption));

    internal OutputFormat GetOutputFormat(ParseResult parseResult) =>
        string.Equals(
            parseResult.GetValue(formatOption),
            "json",
            StringComparison.Ordinal)
            ? OutputFormat.Json
            : OutputFormat.Human;
}
