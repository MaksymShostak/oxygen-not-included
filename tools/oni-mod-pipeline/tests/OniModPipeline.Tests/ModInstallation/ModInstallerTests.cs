using MaksymShostak.OniModPipeline.ContentIntegrity;
using MaksymShostak.OniModPipeline.Diagnostics;
using MaksymShostak.OniModPipeline.EnvironmentDiscovery;
using MaksymShostak.OniModPipeline.ModBuild;
using MaksymShostak.OniModPipeline.ModInstallation;
using MaksymShostak.OniModPipeline.ModProfiles;
using MaksymShostak.OniModPipeline.ReleaseCandidates;
using MaksymShostak.OniModPipeline.Serialization;
using MaksymShostak.OniModPipeline.Tests.ReleaseCandidates;
using MaksymShostak.OniModPipeline.WorkshopContent;
using System.Text.Json;

namespace MaksymShostak.OniModPipeline.Tests.ModInstallation;

[TestClass]
public sealed class ModInstallerTests
{
    [TestMethod]
    public async Task InstallCandidateAsync_WhenDestinationExistsWithoutMarker_ReturnsOnip4001AndPreservesBytes()
    {
        await using var fixture = await InstallerFixture.CreateAsync();
        Directory.CreateDirectory(fixture.Destination);
        var preservedPath = Path.Combine(fixture.Destination, "preserve.txt");
        await File.WriteAllTextAsync(preservedPath, "user-owned bytes");

        var result = await fixture.Installer.InstallCandidateAsync(
            fixture.CandidateDirectory,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.UnownedInstallDestination, result.Diagnostics[0].Id);
        Assert.AreEqual("user-owned bytes", await File.ReadAllTextAsync(preservedPath));
        Assert.IsFalse(File.Exists(fixture.InstallationReceiptPath));
        fixture.AssertNoTransientSiblings();
    }

    [TestMethod]
    public async Task InstallCandidateAsync_WhenMarkerStaticIdDiffers_ReturnsOnip4001AndPreservesBytes()
    {
        await using var fixture = await InstallerFixture.CreateAsync();
        Directory.CreateDirectory(fixture.Destination);
        await fixture.WriteOwnerAsync(
            fixture.Destination,
            new OwnershipMarker(1, "Another.Mod", "ExampleMod", "old-digest"));
        var preservedPath = Path.Combine(fixture.Destination, "old.dll");
        await File.WriteAllTextAsync(preservedPath, "old bytes");

        var result = await fixture.Installer.InstallCandidateAsync(
            fixture.CandidateDirectory,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.UnownedInstallDestination, result.Diagnostics[0].Id);
        Assert.AreEqual("old bytes", await File.ReadAllTextAsync(preservedPath));
        Assert.IsFalse(File.Exists(fixture.InstallationReceiptPath));
        fixture.AssertNoTransientSiblings();
    }

    [TestMethod]
    public async Task InstallCandidateAsync_WhenOwnedDestinationExists_ReplacesItThroughVerifiedSiblingStaging()
    {
        await using var fixture = await InstallerFixture.CreateAsync();
        Directory.CreateDirectory(fixture.Destination);
        await fixture.WriteOwnerAsync(
            fixture.Destination,
            new OwnershipMarker(1, "Example.Mod", "ExampleMod", "old-digest"));
        await File.WriteAllTextAsync(
            Path.Combine(fixture.Destination, "old.dll"),
            "old bytes");

        var result = await fixture.Installer.InstallCandidateAsync(
            fixture.CandidateDirectory,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, InstallerFixture.Render(result.Diagnostics));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Destination, "old.dll")));
        CollectionAssert.AreEqual(
            new[]
            {
                ".oni-mod-pipeline-owner.json",
                "Example.dll",
                "mod.yaml",
                "mod_info.yaml"
            },
            Directory.EnumerateFiles(fixture.Destination)
                .Select(Path.GetFileName)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());
        fixture.AssertNoTransientSiblings();
    }

    [TestMethod]
    public async Task InstallCandidateAsync_WhenSwapFails_RestoresOwnedPreviousInstallation()
    {
        await using var fixture = await InstallerFixture.CreateAsync(
            operationFailure: InstallationOperationFailure.Swap);
        Directory.CreateDirectory(fixture.Destination);
        await fixture.WriteOwnerAsync(
            fixture.Destination,
            new OwnershipMarker(1, "Example.Mod", "ExampleMod", "old-digest"));
        var oldPath = Path.Combine(fixture.Destination, "old.dll");
        await File.WriteAllTextAsync(oldPath, "old bytes");

        var result = await fixture.Installer.InstallCandidateAsync(
            fixture.CandidateDirectory,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(PipelineExitCode.InstallationFailed, result.ExitCode);
        Assert.AreEqual("old bytes", await File.ReadAllTextAsync(oldPath));
        Assert.IsFalse(File.Exists(fixture.InstallationReceiptPath));
        fixture.AssertNoTransientSiblings();
    }

    [TestMethod]
    public async Task InstallCandidateAsync_WhenCandidateContentDigestDiffers_FailsBeforeTouchingTarget()
    {
        await using var fixture = await InstallerFixture.CreateAsync();
        await File.AppendAllTextAsync(
            Path.Combine(fixture.CandidateDirectory, "workshop-content", "mod.yaml"),
            "tamper");

        var result = await fixture.Installer.InstallCandidateAsync(
            fixture.CandidateDirectory,
            InstallTarget.Local,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.InstalledContentMismatch, result.Diagnostics[0].Id);
        Assert.IsFalse(Directory.Exists(fixture.LocalDestination));
        Assert.IsFalse(File.Exists(fixture.InstallationReceiptPath));
        fixture.AssertNoTransientSiblings(InstallTarget.Local);
    }

    [TestMethod]
    public async Task InstallCandidateAsync_WhenBytesChangeDuringCopy_RejectsBeforeSwap()
    {
        await using var fixture = await InstallerFixture.CreateAsync(
            operationFailure: InstallationOperationFailure.TamperDuringCopy);

        var result = await fixture.Installer.InstallCandidateAsync(
            fixture.CandidateDirectory,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.InstalledContentMismatch, result.Diagnostics[0].Id);
        Assert.IsFalse(Directory.Exists(fixture.Destination));
        Assert.IsFalse(File.Exists(fixture.InstallationReceiptPath));
        fixture.AssertNoTransientSiblings();
    }

    [TestMethod]
    public async Task InstallCandidateAsync_WhenInstalledBytesDiffer_RollsBackNewDestination()
    {
        await using var fixture = await InstallerFixture.CreateAsync(
            operationFailure: InstallationOperationFailure.TamperAfterSwap);

        var result = await fixture.Installer.InstallCandidateAsync(
            fixture.CandidateDirectory,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.InstalledContentMismatch, result.Diagnostics[0].Id);
        Assert.IsFalse(Directory.Exists(fixture.Destination));
        Assert.IsFalse(File.Exists(fixture.InstallationReceiptPath));
        fixture.AssertNoTransientSiblings();
    }

    [TestMethod]
    public async Task InstallCandidateAsync_WhenReceiptAlreadyExists_FailsBeforeTouchingDestination()
    {
        await using var fixture = await InstallerFixture.CreateAsync();
        await File.WriteAllTextAsync(fixture.InstallationReceiptPath, "existing receipt\n");
        Directory.CreateDirectory(fixture.Destination);
        await fixture.WriteOwnerAsync(
            fixture.Destination,
            new OwnershipMarker(1, "Example.Mod", "ExampleMod", "old-digest"));
        var oldPath = Path.Combine(fixture.Destination, "old.dll");
        await File.WriteAllTextAsync(oldPath, "old bytes");

        var result = await fixture.Installer.InstallCandidateAsync(
            fixture.CandidateDirectory,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.InstallationReceiptExists, result.Diagnostics[0].Id);
        Assert.AreEqual("old bytes", await File.ReadAllTextAsync(oldPath));
        Assert.AreEqual("existing receipt\n", await File.ReadAllTextAsync(
            fixture.InstallationReceiptPath));
        fixture.AssertNoTransientSiblings();
    }

    [TestMethod]
    public async Task InstallCandidateAsync_WhenSuccessful_WritesOneDigestBoundReceiptAndMarker()
    {
        await using var fixture = await InstallerFixture.CreateAsync();

        var result = await fixture.Installer.InstallCandidateAsync(
            fixture.CandidateDirectory,
            InstallTarget.Local,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, InstallerFixture.Render(result.Diagnostics));
        Assert.IsNotNull(result.Value);
        Assert.AreEqual(fixture.LocalDestination, result.Value.AbsoluteTargetPath);
        Assert.IsTrue(result.Value.InstallationReceiptWritten);
        Assert.IsTrue(File.Exists(fixture.InstallationReceiptPath));
        var receipt = await fixture.ReadJsonAsync<InstallationReceipt>(
            fixture.InstallationReceiptPath);
        Assert.AreEqual("Example.Mod", receipt.StaticId);
        Assert.AreEqual("1.2.3", receipt.Version);
        Assert.AreEqual(result.Value.ContentDigest, receipt.ContentDigest);
        Assert.AreEqual(InstallTarget.Local, receipt.Target);
        Assert.AreEqual(fixture.LocalDestination, receipt.AbsoluteTargetPath);
        Assert.AreEqual(InstallerFixture.InstalledAt, receipt.InstalledAtUtc);
        Assert.IsTrue(receipt.InstalledFilesVerified);
        var marker = await fixture.ReadJsonAsync<OwnershipMarker>(
            Path.Combine(fixture.LocalDestination, ".oni-mod-pipeline-owner.json"));
        Assert.AreEqual(receipt.ContentDigest, marker.InstalledContentDigest);
        Assert.IsFalse(File.Exists(Path.Combine(fixture.LocalDestination, "preview.png")));
        fixture.AssertNoTransientSiblings(InstallTarget.Local);
    }

    [TestMethod]
    public async Task InstallCandidateAsync_WhenSubscribedCopySharesStaticId_ReturnsNonBlockingOnip2005()
    {
        await using var fixture = await InstallerFixture.CreateAsync();
        var subscribedDirectory = Path.Combine(
            fixture.Environment.UserDataDirectory,
            "mods",
            "Steam",
            "1234567890");
        Directory.CreateDirectory(subscribedDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(subscribedDirectory, "mod.yaml"),
            "title: Subscribed Copy\nstaticID: Example.Mod\n");

        var result = await fixture.Installer.InstallCandidateAsync(
            fixture.CandidateDirectory,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess);
        var warning = result.Diagnostics.Single(diagnostic =>
            diagnostic.Id == DiagnosticIds.DuplicateInstalledMod);
        Assert.AreEqual(DiagnosticSeverity.Warning, warning.Severity);
        Assert.Contains(subscribedDirectory, warning.Evidence, StringComparison.Ordinal);
    }

    [TestMethod]
    public async Task InstallCandidateAsync_WhenSubscribedMetadataIsMalformed_DoesNotBlockInstallation()
    {
        await using var fixture = await InstallerFixture.CreateAsync();
        var subscribedDirectory = Path.Combine(
            fixture.Environment.UserDataDirectory,
            "mods",
            "Steam",
            "1234567890");
        Directory.CreateDirectory(subscribedDirectory);
        await File.WriteAllTextAsync(
            Path.Combine(subscribedDirectory, "mod.yaml"),
            "staticID: [unterminated\n");

        var result = await fixture.Installer.InstallCandidateAsync(
            fixture.CandidateDirectory,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, InstallerFixture.Render(result.Diagnostics));
        Assert.IsEmpty(result.Diagnostics);
        Assert.IsTrue(Directory.Exists(fixture.Destination));
    }

    [TestMethod]
    [DataRow(".")]
    [DataRow("..")]
    [DataRow("nested/name")]
    [DataRow("CON")]
    [DataRow("nul.txt")]
    public async Task InstallBuildAsync_WhenManagedDirectoryNameIsNotPortable_RejectsWithoutMutation(
        string unsafeDirectoryName)
    {
        await using var fixture = await InstallerFixture.CreateAsync();
        var build = await fixture.CreateBuildResultAsync();
        var unsafeProfile = fixture.Profile with
        {
            LocalInstall = new LocalInstallProfile(unsafeDirectoryName)
        };

        var result = await fixture.Installer.InstallBuildAsync(
            unsafeProfile,
            fixture.Metadata,
            build.Path,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.UnownedInstallDestination, result.Diagnostics[0].Id);
        Assert.IsFalse(Directory.Exists(fixture.Environment.DevelopmentModsDirectory));
    }

    [TestMethod]
    public async Task InstallBuildAsync_WhenEnvironmentTargetIsNotExactDerivedRoot_RejectsWithoutMutation()
    {
        await using var fixture = await InstallerFixture.CreateAsync();
        var build = await fixture.CreateBuildResultAsync();
        var unsafeEnvironment = fixture.Environment with
        {
            DevelopmentModsDirectory = fixture.Environment.UserDataDirectory
        };

        var result = await fixture.Installer.InstallBuildAsync(
            fixture.Profile,
            fixture.Metadata,
            build.Path,
            InstallTarget.Dev,
            unsafeEnvironment,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.UnownedInstallDestination, result.Diagnostics[0].Id);
        Assert.IsFalse(Directory.Exists(Path.Combine(
            fixture.Environment.UserDataDirectory,
            "ExampleMod")));
    }

    [TestMethod]
    public async Task InstallBuildAsync_WhenRecordedInputChanged_RejectsBeforeTargetMutation()
    {
        await using var fixture = await InstallerFixture.CreateAsync();
        var build = await fixture.CreateBuildResultAsync();
        await File.AppendAllTextAsync(fixture.Profile.ManifestPath, "changed");

        var result = await fixture.Installer.InstallBuildAsync(
            fixture.Profile,
            fixture.Metadata,
            build.Path,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(DiagnosticIds.InstalledContentMismatch, result.Diagnostics[0].Id);
        Assert.IsFalse(Directory.Exists(fixture.Destination));
    }

    [TestMethod]
    public async Task InstallBuildAsync_WhenSuccessful_NeverCreatesCandidateReceipt()
    {
        await using var fixture = await InstallerFixture.CreateAsync();
        var build = await fixture.CreateBuildResultAsync();

        var result = await fixture.Installer.InstallBuildAsync(
            fixture.Profile,
            fixture.Metadata,
            build.Path,
            InstallTarget.Dev,
            fixture.Environment,
            CancellationToken.None);

        Assert.IsTrue(result.IsSuccess, InstallerFixture.Render(result.Diagnostics));
        Assert.IsFalse(result.Value!.InstallationReceiptWritten);
        Assert.IsFalse(File.Exists(fixture.InstallationReceiptPath));
        Assert.IsTrue(File.Exists(Path.Combine(fixture.Destination, "Example.dll")));
        Assert.IsFalse(File.Exists(Path.Combine(fixture.Destination, "preview.png")));
        fixture.AssertNoTransientSiblings();
    }
}

internal enum InstallationOperationFailure
{
    None,
    Swap,
    TamperDuringCopy,
    TamperAfterSwap
}

internal sealed class InstallerFixture : IAsyncDisposable
{
    internal static readonly DateTimeOffset InstalledAt =
        new(2026, 8, 27, 18, 30, 0, TimeSpan.Zero);

    private readonly PreparationFixture preparation;

    private InstallerFixture(
        PreparationFixture preparation,
        InstallationOperationFailure operationFailure)
    {
        this.preparation = preparation;
        Environment = preparation.Environment;
        Profile = preparation.Profile;
        Metadata = preparation.Metadata;
        CandidateDirectory = preparation.Layout.CandidateDirectory;
        InstallationReceiptPath = preparation.Layout.InstallationReceiptPath;
        Destination = Path.Combine(
            Environment.DevelopmentModsDirectory,
            "ExampleMod");
        LocalDestination = Path.Combine(
            Environment.LocalModsDirectory,
            "ExampleMod");
        var operations = new FixtureInstallationOperations(operationFailure);
        Installer = new ModInstaller(
            new ContentHasher(),
            new WorkshopContentAssembler(),
            operations,
            new FixedTimeProvider(InstalledAt),
            () => Guid.Parse("33333333-3333-3333-3333-333333333333"));
    }

    internal PipelineEnvironment Environment { get; }
    internal ModProfile Profile { get; }
    internal OniMetadata Metadata { get; }
    internal string CandidateDirectory { get; }
    internal string InstallationReceiptPath { get; }
    internal string Destination { get; }
    internal string LocalDestination { get; }
    internal ModInstaller Installer { get; }

    internal static async Task<InstallerFixture> CreateAsync(
        InstallationOperationFailure operationFailure =
            InstallationOperationFailure.None)
    {
        var preparation = new PreparationFixture();
        var prepared = await preparation.Preparer.PrepareAsync(
            preparation.Request,
            CancellationToken.None);
        Assert.IsTrue(prepared.IsSuccess, Render(prepared.Diagnostics));
        return new InstallerFixture(preparation, operationFailure);
    }

    internal async Task<(string Path, BuildResult Result)> CreateBuildResultAsync()
    {
        var runRoot = Path.Combine(
            Environment.ArtifactsDirectory,
            "builds",
            Metadata.StaticId,
            $"dev-{Guid.NewGuid():N}");
        var result = await preparation.Builder.BuildAsync(
            new BuildRequest(
                Profile,
                Environment,
                "Release",
                runRoot,
                Metadata.Version,
                PreparationFixture.Commit),
            CancellationToken.None);
        Assert.IsTrue(result.IsSuccess, Render(result.Diagnostics));
        var path = Path.Combine(runRoot, "build-result.json");
        await new Utf8ArtifactWriter().WriteJsonAtomicallyAsync(
            path,
            result.Value,
            CancellationToken.None);
        return (path, result.Value!);
    }

    internal async Task WriteOwnerAsync(string directory, OwnershipMarker marker) =>
        await new Utf8ArtifactWriter().WriteJsonAtomicallyAsync(
            Path.Combine(directory, ".oni-mod-pipeline-owner.json"),
            marker,
            CancellationToken.None);

    internal async Task<T> ReadJsonAsync<T>(string path) =>
        JsonSerializer.Deserialize<T>(
            await File.ReadAllBytesAsync(path),
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            })!;

    internal void AssertNoTransientSiblings(
        InstallTarget target = InstallTarget.Dev)
    {
        var root = target == InstallTarget.Dev
            ? Environment.DevelopmentModsDirectory
            : Environment.LocalModsDirectory;
        if (!Directory.Exists(root))
        {
            return;
        }

        Assert.IsFalse(Directory.EnumerateFileSystemEntries(root)
            .Select(Path.GetFileName)
            .Any(name => name is not null &&
                (name.Contains(".staging-", StringComparison.Ordinal) ||
                 name.Contains(".backup-", StringComparison.Ordinal))));
    }

    internal static string Render(IReadOnlyList<Diagnostic> diagnostics) =>
        string.Join(
            System.Environment.NewLine,
            diagnostics.Select(diagnostic =>
                $"{diagnostic.Id}: {diagnostic.Summary} {diagnostic.Evidence}"));

    public ValueTask DisposeAsync()
    {
        preparation.Dispose();
        return ValueTask.CompletedTask;
    }
}

internal sealed class FixtureInstallationOperations(
    InstallationOperationFailure failure) : IModInstallationOperations
{
    private readonly ModInstallationOperations inner = new();
    private bool copiedTamper;

    public bool EntryExists(string path) => inner.EntryExists(path);

    public void CreateDirectory(string path) => inner.CreateDirectory(path);

    public async Task CopyFileNewAsync(
        string sourcePath,
        string destinationPath,
        CancellationToken cancellationToken)
    {
        await inner.CopyFileNewAsync(
            sourcePath,
            destinationPath,
            cancellationToken);
        if (failure == InstallationOperationFailure.TamperDuringCopy &&
            !copiedTamper &&
            !destinationPath.EndsWith(
                ".oni-mod-pipeline-owner.json",
                StringComparison.Ordinal))
        {
            copiedTamper = true;
            await File.AppendAllTextAsync(
                destinationPath,
                "tamper",
                cancellationToken);
        }
    }

    public void MoveDirectory(string sourcePath, string destinationPath)
    {
        var isStagingSwap = Path.GetFileName(sourcePath)
            .Contains(".staging-", StringComparison.Ordinal);
        if (failure == InstallationOperationFailure.Swap && isStagingSwap)
        {
            throw new IOException("Injected staging swap failure.");
        }

        inner.MoveDirectory(sourcePath, destinationPath);
        if (failure == InstallationOperationFailure.TamperAfterSwap && isStagingSwap)
        {
            File.AppendAllText(Path.Combine(destinationPath, "mod.yaml"), "tamper");
        }
    }

    public void DeleteDirectory(string path) => inner.DeleteDirectory(path);

    public Task WriteJsonCreateNewAsync<T>(
        string path,
        T value,
        CancellationToken cancellationToken) =>
        inner.WriteJsonCreateNewAsync(path, value, cancellationToken);
}
