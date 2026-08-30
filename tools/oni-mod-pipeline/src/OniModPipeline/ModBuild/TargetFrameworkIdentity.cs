using System.Globalization;
using System.Runtime.Versioning;
using System.Text;

namespace MaksymShostak.OniModPipeline.ModBuild;

/// <summary>
/// Distinguishes the canonical target framework moniker used by MSBuild and
/// release tooling (for example, <c>netstandard2.1</c>) from the CLR framework
/// name stored in <see cref="TargetFrameworkAttribute"/> (for example,
/// <c>.NETStandard,Version=v2.1</c>).
/// </summary>
internal sealed record TargetFrameworkIdentity(
    string Moniker,
    string FrameworkName)
{
    internal static TargetFrameworkIdentity ParseFrameworkName(
        string frameworkName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(frameworkName);

        FrameworkName parsedFrameworkName;
        try
        {
            parsedFrameworkName = new FrameworkName(frameworkName);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                $"Target-framework name '{frameworkName}' is malformed.",
                exception);
        }

        if (!string.IsNullOrEmpty(parsedFrameworkName.Profile))
        {
            throw new InvalidDataException(
                $"Target-framework profile '{parsedFrameworkName.Profile}' in " +
                $"'{frameworkName}' has no supported canonical moniker mapping.");
        }

        var moniker = parsedFrameworkName.Identifier switch
        {
            ".NETStandard" =>
                $"netstandard{FormatDottedVersion(parsedFrameworkName.Version)}",
            ".NETCoreApp" when parsedFrameworkName.Version.Major >= 5 =>
                $"net{FormatDottedVersion(parsedFrameworkName.Version)}",
            ".NETCoreApp" =>
                $"netcoreapp{FormatDottedVersion(parsedFrameworkName.Version)}",
            ".NETFramework" =>
                $"net{FormatCompactVersion(parsedFrameworkName.Version)}",
            _ => throw new InvalidDataException(
                $"Target-framework identifier '{parsedFrameworkName.Identifier}' " +
                $"in '{frameworkName}' has no supported canonical moniker mapping.")
        };

        return new TargetFrameworkIdentity(moniker, frameworkName);
    }

    private static string FormatDottedVersion(Version version)
    {
        var builder = new StringBuilder();
        AppendVersionComponent(builder, version.Major, includeSeparator: false);
        AppendVersionComponent(builder, version.Minor, includeSeparator: true);
        if (version.Build >= 0)
        {
            AppendVersionComponent(builder, version.Build, includeSeparator: true);
        }

        if (version.Revision >= 0)
        {
            AppendVersionComponent(builder, version.Revision, includeSeparator: true);
        }

        return builder.ToString();
    }

    private static string FormatCompactVersion(Version version)
    {
        var builder = new StringBuilder();
        AppendVersionComponent(builder, version.Major, includeSeparator: false);
        AppendVersionComponent(builder, version.Minor, includeSeparator: false);
        if (version.Build >= 0)
        {
            AppendVersionComponent(builder, version.Build, includeSeparator: false);
        }

        if (version.Revision >= 0)
        {
            AppendVersionComponent(builder, version.Revision, includeSeparator: false);
        }

        return builder.ToString();
    }

    private static void AppendVersionComponent(
        StringBuilder builder,
        int component,
        bool includeSeparator)
    {
        if (includeSeparator)
        {
            builder.Append('.');
        }

        builder.Append(component.ToString(CultureInfo.InvariantCulture));
    }
}
