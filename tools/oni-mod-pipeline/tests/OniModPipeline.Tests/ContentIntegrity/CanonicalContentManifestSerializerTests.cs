using MaksymShostak.OniModPipeline.ContentIntegrity;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.ContentIntegrity;

[TestClass]
[DoNotParallelize]
public sealed class CanonicalContentManifestSerializerTests
{
    private const string FirstSha256 =
        "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string SecondSha256 =
        "abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789";

    [TestMethod]
    public void Serialize_WhenGivenGoldenEntries_ProducesSpecifiedDigest()
    {
        ReleaseContentEntry[] entries =
        [
            new(
                ContentArea.WorkshopContent,
                "mod.yaml",
                42,
                FirstSha256,
                ContentRole.Runtime),
            new(
                ContentArea.WorkshopListing,
                "description.bbcode",
                17,
                SecondSha256,
                ContentRole.Description)
        ];

        var bytes = CanonicalContentManifestSerializer.Serialize(entries);
        var digest = Convert.ToHexStringLower(SHA256.HashData(bytes));

        Assert.AreEqual(
            "c599e8d8dd6d307064e20c85381dfe17a1c1b340b688bb3a3cbab40741a2b8ca",
            digest);
    }

    [TestMethod]
    public void Serialize_WhenCurrentCultureHasDifferentSortRules_UsesOrdinalPathOrder()
    {
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;
        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
            ReleaseContentEntry[] entries =
            [
                Entry("ä.txt", 2),
                Entry("z.txt", 1)
            ];

            var serialized = Encoding.UTF8.GetString(
                CanonicalContentManifestSerializer.Serialize(entries));

            Assert.IsTrue(
                serialized.IndexOf("z.txt", StringComparison.Ordinal) <
                serialized.IndexOf("ä.txt", StringComparison.Ordinal));
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [TestMethod]
    public void Serialize_WhenPathUsesBackslashes_NormalizesToForwardSlashes()
    {
        var serialized = Encoding.UTF8.GetString(
            CanonicalContentManifestSerializer.Serialize(
                [Entry("nested\\payload.dll", 1)]));

        StringAssert.Contains(serialized, "nested/payload.dll");
        Assert.IsFalse(serialized.Contains('\\', StringComparison.Ordinal));
    }

    [TestMethod]
    public void Serialize_WhenPathUsesDecomposedUnicode_EmitsNfc()
    {
        var serialized = Encoding.UTF8.GetString(
            CanonicalContentManifestSerializer.Serialize(
                [Entry("cafe\u0301.txt", 1)]));

        StringAssert.Contains(serialized, "café.txt");
        Assert.IsFalse(serialized.Contains("cafe\u0301.txt", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Serialize_WhenPathContainsRecordDelimiter_RejectsEntry()
    {
        foreach (var path in new[] { "bad\0path", "bad\npath", "bad\rpath" })
        {
            Assert.ThrowsExactly<InvalidDataException>(() =>
                CanonicalContentManifestSerializer.Serialize([Entry(path, 1)]));
        }
    }

    [TestMethod]
    public void Serialize_WhenPathsDifferOnlyByPortableCase_RejectsCollision()
    {
        ReleaseContentEntry[] entries =
        [
            Entry("Payload.dll", 1),
            Entry("payload.dll", 2)
        ];

        Assert.ThrowsExactly<InvalidDataException>(() =>
            CanonicalContentManifestSerializer.Serialize(entries));
    }

    [TestMethod]
    public void Serialize_WhenPathsCollideAfterUnicodeNormalization_RejectsCollision()
    {
        ReleaseContentEntry[] entries =
        [
            Entry("café.txt", 1),
            Entry("cafe\u0301.txt", 2)
        ];

        Assert.ThrowsExactly<InvalidDataException>(() =>
            CanonicalContentManifestSerializer.Serialize(entries));
    }

    [TestMethod]
    public void Serialize_WhenPathsCollideAfterSeparatorNormalization_RejectsCollision()
    {
        ReleaseContentEntry[] entries =
        [
            Entry("nested/payload.dll", 1),
            Entry("nested\\payload.dll", 2)
        ];

        Assert.ThrowsExactly<InvalidDataException>(() =>
            CanonicalContentManifestSerializer.Serialize(entries));
    }

    [TestMethod]
    public void Serialize_WhenEqualPathsBelongToDifferentAreas_AllowsAndSortsByArea()
    {
        ReleaseContentEntry[] entries =
        [
            Entry("shared.txt", 2, ContentArea.WorkshopListing, ContentRole.Description),
            Entry("shared.txt", 1, ContentArea.WorkshopContent, ContentRole.Runtime)
        ];

        var serialized = Encoding.UTF8.GetString(
            CanonicalContentManifestSerializer.Serialize(entries));

        Assert.IsTrue(
            serialized.IndexOf("workshop-content", StringComparison.Ordinal) <
            serialized.IndexOf("workshop-listing", StringComparison.Ordinal));
    }

    [TestMethod]
    public void Serialize_WhenHashIsNotLowercase64Hex_RejectsEntry()
    {
        foreach (var sha256 in new[]
        {
            FirstSha256.ToUpperInvariant(),
            FirstSha256[..^1],
            $"{FirstSha256[..^1]}g"
        })
        {
            var entry = new ReleaseContentEntry(
                ContentArea.WorkshopContent,
                "payload.dll",
                1,
                sha256,
                ContentRole.Runtime);

            Assert.ThrowsExactly<InvalidDataException>(() =>
                CanonicalContentManifestSerializer.Serialize([entry]));
        }
    }

    [TestMethod]
    public void Serialize_WhenByteLengthExceedsInt32_UsesInvariantInt64Text()
    {
        const long byteLength = (long)int.MaxValue + 42;

        var serialized = Encoding.UTF8.GetString(
            CanonicalContentManifestSerializer.Serialize(
                [Entry("large.bin", byteLength)]));

        StringAssert.Contains(serialized, "\02147483689\0");
    }

    [TestMethod]
    public void Serialize_WhenByteLengthIsNegative_RejectsEntry()
    {
        Assert.ThrowsExactly<InvalidDataException>(() =>
            CanonicalContentManifestSerializer.Serialize([Entry("payload.dll", -1)]));
    }

    [TestMethod]
    public void Serialize_WhenEnumValueIsUnknown_RejectsEntry()
    {
        var unknownArea = Entry("area.bin", 1) with { ContentArea = (ContentArea)999 };
        var unknownRole = Entry("role.bin", 1) with { Role = (ContentRole)999 };

        Assert.ThrowsExactly<InvalidDataException>(() =>
            CanonicalContentManifestSerializer.Serialize([unknownArea]));
        Assert.ThrowsExactly<InvalidDataException>(() =>
            CanonicalContentManifestSerializer.Serialize([unknownRole]));
    }

    [TestMethod]
    public void ReleaseContentManifest_WhenSerializedAsJson_UsesCamelCaseAndMappedEnums()
    {
        var manifest = new ReleaseContentManifest(
            1,
            [Entry("payload.dll", 1)],
            SecondSha256);
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };

        var json = JsonSerializer.Serialize(manifest, options);

        StringAssert.Contains(json, "\"schemaVersion\":1");
        StringAssert.Contains(json, "\"contentArea\":\"workshop-content\"");
        StringAssert.Contains(json, "\"role\":\"runtime\"");
        StringAssert.Contains(json, $"\"contentDigest\":\"{SecondSha256}\"");
    }

    private static ReleaseContentEntry Entry(
        string path,
        long byteLength,
        ContentArea area = ContentArea.WorkshopContent,
        ContentRole role = ContentRole.Runtime) =>
        new(area, path, byteLength, FirstSha256, role);
}
