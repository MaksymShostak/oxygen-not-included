using System.Globalization;
using System.Text;

namespace MaksymShostak.OniModPipeline.ContentIntegrity;

internal static class CanonicalContentManifestSerializer
{
    private const string Header = "oni-release-content-manifest-v1\n";

    private static readonly UTF8Encoding StrictUtf8 = new(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    internal static byte[] Serialize(IReadOnlyList<ReleaseContentEntry> entries)
    {
        var canonicalEntries = Canonicalize(entries);
        using var stream = new MemoryStream();
        WriteUtf8(stream, Header);
        foreach (var entry in canonicalEntries)
        {
            WriteUtf8(stream, GetAreaName(entry.ContentArea));
            stream.WriteByte(0);
            WriteUtf8(stream, entry.RelativePath);
            stream.WriteByte(0);
            WriteUtf8(stream, entry.ByteLength.ToString(CultureInfo.InvariantCulture));
            stream.WriteByte(0);
            WriteUtf8(stream, entry.Sha256);
            stream.WriteByte(0);
            WriteUtf8(stream, GetRoleName(entry.Role));
            stream.WriteByte((byte)'\n');
        }

        return stream.ToArray();
    }

    internal static IReadOnlyList<ReleaseContentEntry> Canonicalize(
        IReadOnlyList<ReleaseContentEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);

        var canonicalEntries = new List<ReleaseContentEntry>(entries.Count);
        var portablePaths = new HashSet<(ContentArea Area, string Path)>(
            ContentPathIdentityComparer.Instance);
        foreach (var entry in entries)
        {
            if (entry is null)
            {
                throw new InvalidDataException("Release content entries must not be null.");
            }

            _ = GetAreaName(entry.ContentArea);
            _ = GetRoleName(entry.Role);
            if (entry.ByteLength < 0)
            {
                throw new InvalidDataException(
                    $"Release content path '{entry.RelativePath}' has a negative byte length.");
            }

            if (!IsLowercaseSha256(entry.Sha256))
            {
                throw new InvalidDataException(
                    $"Release content path '{entry.RelativePath}' must have a lowercase 64-hex SHA-256 digest.");
            }

            var normalizedPath = NormalizeRelativePath(entry.RelativePath);
            if (!portablePaths.Add((entry.ContentArea, normalizedPath)))
            {
                throw new InvalidDataException(
                    $"Release content area '{GetAreaName(entry.ContentArea)}' contains a portable path collision at '{normalizedPath}'.");
            }

            canonicalEntries.Add(entry with { RelativePath = normalizedPath });
        }

        return canonicalEntries
            .OrderBy(entry => entry.RelativePath, StringComparer.Ordinal)
            .ThenBy(entry => GetAreaName(entry.ContentArea), StringComparer.Ordinal)
            .ToArray();
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
        {
            throw new InvalidDataException("A release content relative path must not be empty.");
        }

        if (relativePath.IndexOfAny(['\0', '\r', '\n']) >= 0)
        {
            throw new InvalidDataException(
                "A release content relative path must not contain NUL, CR, or LF characters.");
        }

        string normalized;
        try
        {
            normalized = relativePath
                .Replace('\\', '/')
                .Normalize(NormalizationForm.FormC);
        }
        catch (ArgumentException exception)
        {
            throw new InvalidDataException(
                "A release content relative path must contain valid Unicode text.",
                exception);
        }

        if (normalized[0] == '/' ||
            normalized.Length >= 2 &&
            char.IsAsciiLetter(normalized[0]) &&
            normalized[1] == ':')
        {
            throw new InvalidDataException(
                $"Release content path '{normalized}' must be relative.");
        }

        var segments = normalized.Split('/');
        if (segments.Any(segment =>
            segment.Length == 0 || segment == "." || segment == ".."))
        {
            throw new InvalidDataException(
                $"Release content path '{normalized}' must use nonempty file-name segments without traversal.");
        }

        return normalized;
    }

    private static bool IsLowercaseSha256(string sha256) =>
        sha256 is { Length: 64 } &&
        sha256.All(character =>
            character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static string GetAreaName(ContentArea area)
    {
        try
        {
            return area.ToCanonicalName();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("Release content area is unknown.", exception);
        }
    }

    private static string GetRoleName(ContentRole role)
    {
        try
        {
            return role.ToCanonicalName();
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidDataException("Release content role is unknown.", exception);
        }
    }

    private static void WriteUtf8(Stream stream, string value)
    {
        try
        {
            stream.Write(StrictUtf8.GetBytes(value));
        }
        catch (EncoderFallbackException exception)
        {
            throw new InvalidDataException(
                "Canonical release content text must contain valid Unicode.",
                exception);
        }
    }

    private sealed class ContentPathIdentityComparer :
        IEqualityComparer<(ContentArea Area, string Path)>
    {
        internal static readonly ContentPathIdentityComparer Instance = new();

        public bool Equals(
            (ContentArea Area, string Path) left,
            (ContentArea Area, string Path) right) =>
            left.Area == right.Area &&
            StringComparer.OrdinalIgnoreCase.Equals(left.Path, right.Path);

        public int GetHashCode((ContentArea Area, string Path) value) =>
            HashCode.Combine(
                value.Area,
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Path));
    }
}
