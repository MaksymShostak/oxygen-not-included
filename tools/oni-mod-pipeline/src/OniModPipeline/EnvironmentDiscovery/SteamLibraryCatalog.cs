namespace MaksymShostak.OniModPipeline.EnvironmentDiscovery;

internal sealed class SteamLibraryCatalog
{
    private readonly Func<string, string> resolveDirectoryIdentity;

    internal SteamLibraryCatalog()
        : this(ResolveDirectoryIdentity)
    {
    }

    internal SteamLibraryCatalog(Func<string, string> resolveDirectoryIdentity)
    {
        ArgumentNullException.ThrowIfNull(resolveDirectoryIdentity);
        this.resolveDirectoryIdentity = resolveDirectoryIdentity;
    }

    internal IReadOnlyList<string> DiscoverLibraries(
        IReadOnlyList<string> steamRoots)
    {
        ArgumentNullException.ThrowIfNull(steamRoots);
        var comparer = OperatingSystem.IsWindows()
            ? StringComparer.OrdinalIgnoreCase
            : StringComparer.Ordinal;
        var libraries = new HashSet<string>(comparer);
        foreach (var steamRootValue in steamRoots)
        {
            if (string.IsNullOrWhiteSpace(steamRootValue))
            {
                continue;
            }

            string steamRoot;
            try
            {
                steamRoot = Path.GetFullPath(steamRootValue);
            }
            catch (Exception exception) when (
                exception is ArgumentException or
                    NotSupportedException or
                    PathTooLongException)
            {
                continue;
            }

            libraries.Add(Path.GetFullPath(resolveDirectoryIdentity(steamRoot)));
            var catalogPath = Path.Combine(
                steamRoot,
                "steamapps",
                "libraryfolders.vdf");
            if (!File.Exists(catalogPath))
            {
                continue;
            }

            try
            {
                var document = VdfParser.Parse(File.ReadAllText(catalogPath));
                foreach (var libraryPath in FindLibraryPaths(document))
                {
                    if (!Path.IsPathFullyQualified(libraryPath))
                    {
                        continue;
                    }

                    libraries.Add(Path.GetFullPath(resolveDirectoryIdentity(
                        Path.GetFullPath(libraryPath))));
                }
            }
            catch (Exception exception) when (
                exception is FormatException or
                    IOException or
                    UnauthorizedAccessException or
                    ArgumentException or
                    NotSupportedException or
                    PathTooLongException)
            {
                // Keep the conventional root; malformed or inaccessible metadata
                // must not make discovery invoke or modify Steam.
            }
        }

        return libraries
            .OrderBy(path => path, comparer)
            .ToArray();
    }

    private static string ResolveDirectoryIdentity(string path)
    {
        var fullPath = Path.GetFullPath(path);
        if (!Directory.Exists(fullPath))
        {
            return fullPath;
        }

        var directory = new DirectoryInfo(fullPath);
        if ((directory.Attributes & FileAttributes.ReparsePoint) == 0 &&
            directory.LinkTarget is null)
        {
            return fullPath;
        }

        return directory.ResolveLinkTarget(returnFinalTarget: true) is { } target
            ? Path.GetFullPath(target.FullName)
            : fullPath;
    }

    private static IEnumerable<string> FindLibraryPaths(
        IReadOnlyList<VdfEntry> document)
    {
        foreach (var root in document.Where(entry => string.Equals(
            entry.Key,
            "libraryfolders",
            StringComparison.OrdinalIgnoreCase)))
        {
            foreach (var library in root.Children ?? [])
            {
                if (!library.Key.All(char.IsAsciiDigit))
                {
                    continue;
                }

                if (library.Value is { } legacyPath)
                {
                    yield return legacyPath;
                    continue;
                }

                var pathEntry = library.Children?.FirstOrDefault(entry =>
                    string.Equals(entry.Key, "path", StringComparison.OrdinalIgnoreCase));
                if (pathEntry?.Value is { } path)
                {
                    yield return path;
                }
            }
        }
    }

    private sealed record VdfEntry(
        string Key,
        string? Value,
        IReadOnlyList<VdfEntry>? Children);

    private enum VdfTokenKind
    {
        String,
        OpenBrace,
        CloseBrace
    }

    private sealed record VdfToken(VdfTokenKind Kind, string? Value = null);

    private static class VdfParser
    {
        internal static IReadOnlyList<VdfEntry> Parse(string text)
        {
            ArgumentNullException.ThrowIfNull(text);
            var tokens = Tokenize(text);
            var index = 0;
            var entries = ParseEntries(tokens, ref index, expectCloseBrace: false);
            if (index != tokens.Count)
            {
                throw new FormatException("Unexpected trailing VDF token.");
            }

            return entries;
        }

        private static IReadOnlyList<VdfEntry> ParseEntries(
            IReadOnlyList<VdfToken> tokens,
            ref int index,
            bool expectCloseBrace)
        {
            var entries = new List<VdfEntry>();
            while (index < tokens.Count)
            {
                if (tokens[index].Kind == VdfTokenKind.CloseBrace)
                {
                    if (!expectCloseBrace)
                    {
                        throw new FormatException("Unexpected VDF closing brace.");
                    }

                    index++;
                    return entries;
                }

                var key = RequireString(tokens, ref index, "VDF entry key");
                if (index >= tokens.Count)
                {
                    throw new FormatException("VDF entry has no value.");
                }

                if (tokens[index].Kind == VdfTokenKind.String)
                {
                    entries.Add(new VdfEntry(
                        key,
                        RequireString(tokens, ref index, "VDF entry value"),
                        null));
                    continue;
                }

                if (tokens[index].Kind != VdfTokenKind.OpenBrace)
                {
                    throw new FormatException("VDF entry has an invalid value.");
                }

                index++;
                entries.Add(new VdfEntry(
                    key,
                    null,
                    ParseEntries(tokens, ref index, expectCloseBrace: true)));
            }

            if (expectCloseBrace)
            {
                throw new FormatException("VDF object is missing a closing brace.");
            }

            return entries;
        }

        private static string RequireString(
            IReadOnlyList<VdfToken> tokens,
            ref int index,
            string description)
        {
            if (index >= tokens.Count || tokens[index].Kind != VdfTokenKind.String)
            {
                throw new FormatException($"Expected {description}.");
            }

            return tokens[index++].Value!;
        }

        private static IReadOnlyList<VdfToken> Tokenize(string text)
        {
            var tokens = new List<VdfToken>();
            for (var index = 0; index < text.Length;)
            {
                if (char.IsWhiteSpace(text[index]))
                {
                    index++;
                    continue;
                }

                if (text[index] == '/' &&
                    index + 1 < text.Length &&
                    text[index + 1] == '/')
                {
                    index += 2;
                    while (index < text.Length && text[index] is not '\r' and not '\n')
                    {
                        index++;
                    }

                    continue;
                }

                if (text[index] == '{')
                {
                    tokens.Add(new VdfToken(VdfTokenKind.OpenBrace));
                    index++;
                    continue;
                }

                if (text[index] == '}')
                {
                    tokens.Add(new VdfToken(VdfTokenKind.CloseBrace));
                    index++;
                    continue;
                }

                if (text[index] != '"')
                {
                    throw new FormatException("VDF keys and values must be quoted.");
                }

                index++;
                var value = new System.Text.StringBuilder();
                var terminated = false;
                while (index < text.Length)
                {
                    var character = text[index++];
                    if (character == '"')
                    {
                        terminated = true;
                        break;
                    }

                    if (character != '\\')
                    {
                        value.Append(character);
                        continue;
                    }

                    if (index >= text.Length)
                    {
                        throw new FormatException("VDF string ends with an escape character.");
                    }

                    var escaped = text[index++];
                    value.Append(escaped switch
                    {
                        'n' => '\n',
                        'r' => '\r',
                        't' => '\t',
                        '\\' => '\\',
                        '"' => '"',
                        _ => throw new FormatException(
                            $"Unsupported VDF escape sequence '\\{escaped}'.")
                    });
                }

                if (!terminated)
                {
                    throw new FormatException("VDF string is missing a closing quote.");
                }

                tokens.Add(new VdfToken(VdfTokenKind.String, value.ToString()));
            }

            return tokens;
        }
    }
}
