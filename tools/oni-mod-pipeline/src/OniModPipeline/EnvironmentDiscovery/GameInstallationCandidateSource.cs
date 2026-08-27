using Microsoft.Win32;
using System.Runtime.Versioning;

namespace MaksymShostak.OniModPipeline.EnvironmentDiscovery;

internal enum HostOperatingSystem
{
    Windows,
    MacOS,
    Linux
}

internal sealed class GameInstallationCandidateSource
{
    internal GameInstallationCandidateSource(
        HostOperatingSystem operatingSystem,
        string homeDirectory,
        string documentsDirectory,
        IReadOnlyList<string> steamRoots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(homeDirectory);
        ArgumentException.ThrowIfNullOrWhiteSpace(documentsDirectory);
        ArgumentNullException.ThrowIfNull(steamRoots);

        OperatingSystem = operatingSystem;
        HomeDirectory = Path.GetFullPath(homeDirectory);
        DocumentsDirectory = Path.GetFullPath(documentsDirectory);
        PathComparer = operatingSystem == HostOperatingSystem.Windows
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        SteamRoots = steamRoots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .OrderBy(path => path, PathComparer)
            .ToArray();

        NativeUserDataDirectory = Path.GetFullPath(operatingSystem switch
        {
            HostOperatingSystem.Windows => Path.Combine(
                DocumentsDirectory,
                "Klei",
                "OxygenNotIncluded"),
            HostOperatingSystem.MacOS => Path.Combine(
                HomeDirectory,
                "Library",
                "Application Support",
                "unity.Klei.Oxygen Not Included"),
            HostOperatingSystem.Linux => Path.Combine(
                HomeDirectory,
                ".config",
                "unity3d",
                "Klei",
                "Oxygen Not Included"),
            _ => throw new ArgumentOutOfRangeException(
                nameof(operatingSystem),
                operatingSystem,
                "Unsupported host operating system.")
        });
    }

    internal HostOperatingSystem OperatingSystem { get; }

    internal string HomeDirectory { get; }

    internal string DocumentsDirectory { get; }

    internal IReadOnlyList<string> SteamRoots { get; }

    internal StringComparer PathComparer { get; }

    internal string NativeUserDataDirectory { get; }

    internal string NativeDevelopmentModsDirectory =>
        Path.Combine(NativeUserDataDirectory, "mods", "Dev");

    internal string NativeLocalModsDirectory =>
        Path.Combine(NativeUserDataDirectory, "mods", "Local");

    internal static GameInstallationCandidateSource CreateDefault()
    {
        var operatingSystem = System.OperatingSystem.IsWindows()
            ? HostOperatingSystem.Windows
            : System.OperatingSystem.IsMacOS()
                ? HostOperatingSystem.MacOS
                : System.OperatingSystem.IsLinux()
                    ? HostOperatingSystem.Linux
                    : throw new PlatformNotSupportedException(
                        "ONI environment discovery supports Windows, macOS, and Linux.");
        var homeDirectory = GetRequiredUserDirectory(
            Environment.SpecialFolder.UserProfile,
            operatingSystem == HostOperatingSystem.Windows
                ? "USERPROFILE"
                : "HOME");
        var documentsDirectory = Environment.GetFolderPath(
            Environment.SpecialFolder.MyDocuments,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(documentsDirectory))
        {
            documentsDirectory = Path.Combine(homeDirectory, "Documents");
        }

        return new GameInstallationCandidateSource(
            operatingSystem,
            homeDirectory,
            documentsDirectory,
            GetDefaultSteamRoots(operatingSystem, homeDirectory));
    }

    internal IReadOnlyList<string> GetAutomaticGameDirectories(
        IReadOnlyList<string> steamLibraries)
    {
        ArgumentNullException.ThrowIfNull(steamLibraries);
        return steamLibraries
            .Select(library => Path.GetFullPath(Path.Combine(
                library,
                "steamapps",
                "common",
                "OxygenNotIncluded")))
            .Distinct(PathComparer)
            .OrderBy(path => path, PathComparer)
            .ToArray();
    }

    internal IReadOnlyList<string> GetAutomaticUserDataDirectories(
        IReadOnlyList<string> steamLibraries)
    {
        ArgumentNullException.ThrowIfNull(steamLibraries);
        var candidates = new List<string> { NativeUserDataDirectory };
        if (OperatingSystem == HostOperatingSystem.Linux)
        {
            candidates.AddRange(steamLibraries.Select(library => Path.Combine(
                library,
                "steamapps",
                "compatdata",
                "457140",
                "pfx",
                "drive_c",
                "users",
                "steamuser",
                "Documents",
                "Klei",
                "OxygenNotIncluded")));
        }

        return candidates
            .Select(Path.GetFullPath)
            .Distinct(PathComparer)
            .ToArray();
    }

    internal string GetManagedAssemblyDirectory(string gameDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameDirectory);
        var fullGameDirectory = Path.GetFullPath(gameDirectory);
        return OperatingSystem == HostOperatingSystem.MacOS
            ? Path.Combine(
                fullGameDirectory,
                "OxygenNotIncluded.app",
                "Contents",
                "Resources",
                "Data",
                "Managed")
            : Path.Combine(
                fullGameDirectory,
                "OxygenNotIncluded_Data",
                "Managed");
    }

    private static string GetRequiredUserDirectory(
        Environment.SpecialFolder specialFolder,
        string fallbackVariable)
    {
        var directory = Environment.GetFolderPath(
            specialFolder,
            Environment.SpecialFolderOption.DoNotVerify);
        if (string.IsNullOrWhiteSpace(directory))
        {
            directory = Environment.GetEnvironmentVariable(fallbackVariable);
        }

        if (string.IsNullOrWhiteSpace(directory))
        {
            throw new InvalidOperationException(
                $"The current user's {specialFolder} directory is unavailable.");
        }

        return Path.GetFullPath(directory);
    }

    private static IReadOnlyList<string> GetDefaultSteamRoots(
        HostOperatingSystem operatingSystem,
        string homeDirectory)
    {
        var roots = new List<string>();
        switch (operatingSystem)
        {
            case HostOperatingSystem.Windows:
                if (System.OperatingSystem.IsWindows())
                {
                    roots.AddRange(GetWindowsRegistrySteamRoots());
                }

                AddSteamRootFromEnvironment(roots, "ProgramFiles(x86)");
                AddSteamRootFromEnvironment(roots, "ProgramFiles");
                break;
            case HostOperatingSystem.MacOS:
                roots.Add(Path.Combine(
                    homeDirectory,
                    "Library",
                    "Application Support",
                    "Steam"));
                break;
            case HostOperatingSystem.Linux:
                roots.Add(Path.Combine(homeDirectory, ".steam", "steam"));
                roots.Add(Path.Combine(homeDirectory, ".local", "share", "Steam"));
                roots.Add(Path.Combine(
                    homeDirectory,
                    ".var",
                    "app",
                    "com.valvesoftware.Steam",
                    "data",
                    "Steam"));
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(operatingSystem),
                    operatingSystem,
                    "Unsupported host operating system.");
        }

        var comparer = operatingSystem == HostOperatingSystem.Windows
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        return roots
            .Where(path => !string.IsNullOrWhiteSpace(path))
            .Select(Path.GetFullPath)
            .Distinct(comparer)
            .OrderBy(path => path, comparer)
            .ToArray();
    }

    private static void AddSteamRootFromEnvironment(
        ICollection<string> roots,
        string variableName)
    {
        var programFiles = Environment.GetEnvironmentVariable(variableName);
        if (!string.IsNullOrWhiteSpace(programFiles))
        {
            roots.Add(Path.Combine(programFiles, "Steam"));
        }
    }

    [SupportedOSPlatform("windows")]
    private static IReadOnlyList<string> GetWindowsRegistrySteamRoots()
    {
        if (!System.OperatingSystem.IsWindows())
        {
            return [];
        }

        var roots = new List<string>();
        AddRegistryValue(
            roots,
            @"HKEY_CURRENT_USER\Software\Valve\Steam",
            "SteamPath");
        AddRegistryValue(
            roots,
            @"HKEY_LOCAL_MACHINE\SOFTWARE\WOW6432Node\Valve\Steam",
            "InstallPath");
        AddRegistryValue(
            roots,
            @"HKEY_LOCAL_MACHINE\SOFTWARE\Valve\Steam",
            "InstallPath");
        return roots;
    }

    [SupportedOSPlatform("windows")]
    private static void AddRegistryValue(
        ICollection<string> roots,
        string keyName,
        string valueName)
    {
        if (Registry.GetValue(keyName, valueName, null) is string value &&
            !string.IsNullOrWhiteSpace(value))
        {
            roots.Add(value);
        }
    }
}
